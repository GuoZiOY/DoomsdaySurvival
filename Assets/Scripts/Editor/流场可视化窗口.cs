using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
// 流场可视化（EditorWindow）：把**游戏里正在跑的那份流场**画出来 —— 为了展示与调试。
//
// 为什么做成 Editor 窗口而不是离线图：
//   · 它读的是**运行中的服务**（ServiceRegistry 里的 大世界/区域/房间 三层），
//     所以看到的是"此刻这一局"的场：玩家在哪、敌人在哪、它们处于静默/巡逻/追踪。
//   · 场本身由 `流场.建场(...)` 现算 —— **和游戏同一个函数**，不存在"画的是另一套算法"。
//
// 打开：菜单 末日/调试/流场可视化　（Play 模式下方才有数据；Edit 模式会提示）
//
// 画什么：
//   ① 积分场热力（到目标的步数）—— 这就是"热力图"
//   ② 方向场箭头（4 邻里代价最小的那一格）—— 这就是"向量场"
//   ③ 挡格（占格物：墙/容器/障碍/建筑/区域；敌人对场是透明的，见 流场.cs 文件头）
//   ④ 敌人（按 AI 三态上色）+ 出生点 + 巡逻半径 / 追击上限圈
//   ⑤ 玩家、目标格、源格（目标格不可走时会**吸附**到最近可走格，窗口里会标出"被吸附"）
//   ⑥ 听距圈：选一档噪音强度，画出每只敌人实际听得见的半径（含潜行削弱与层系数）
//
// ⚠ 只读：本窗口**不修改任何游戏状态**（敌人 AI 态走 敌人AI态只读()，不创建）。
// ============================================================
public sealed class 流场可视化窗口 : EditorWindow
{
    private enum 层 { 大世界, 区域, 房间 }

    private 层 选层 = 层.大世界;
    private 流场 场;
    private 网格数据 场所在世界;      // 场是按哪个世界算的（换世界 / 换层要重算）
    private int 场目标列 = -1, 场目标行 = -1;
    private Texture2D 图;
    private bool[] 挡表;              // 流场.建挡表 的结果（唯一判据）
    private byte[] 类型码;            // 每格的"是什么"（画挡格用；零=空）
    private int 最大距;
    private int 可达格数;
    private string 状态 = "";

    // 显示开关
    private bool 显热力 = true, 显箭头 = true, 显挡格 = true, 显敌人 = true;
    private bool 显出生圈 = true, 显巡逻圈 = true, 显听距 = false;
    private bool 跟随玩家 = true;
    private int 听距档;               // 下标进 听距档表
    private int 格像素 = 7;
    private Vector2 滚动;

    // 目标模式：0 = 跟随玩家 / 1 = 营地门口（大世界）/ 2 = 手填
    private int 目标模式;
    private int 手填列, 手填行;

    private static readonly (string 名, int 强度)[] 听距档表 =
    {
        ("走一格 3", 噪音强度.走一格),
        ("翻柜子 15", 噪音强度.搜索容器首次),
        ("撬锁 22", 噪音强度.撬锁),
        ("大世界搜索 30", 噪音强度.大世界搜索),
        ("开枪 90", 噪音强度.开枪),
    };

    [MenuItem("末日/调试/流场可视化")]
    public static void 打开()
    {
        var w = GetWindow<流场可视化窗口>("流场可视化");
        w.minSize = new Vector2(560f, 420f);
        w.Show();
    }

    private void OnDestroy()
    {
        if (图 != null) DestroyImmediate(图);
    }

    // Play 模式下方有数据 —— 每帧重画（敌人会动）
    private void Update()
    {
        if (Application.isPlaying) Repaint();
    }

    private void OnGUI()
    {
        var 服 = 取服务();
        if (服 == null || 服.当前世界 == null)
        {
            EditorGUILayout.HelpBox(Application.isPlaying
                ? $"{选层}层 现在没有世界（还没进这一层 / 已经出来了）。切一层，或者先出门。"
                : "请先进入 Play 模式再打开这个窗口 —— 它画的是**运行中**的那份场。", MessageType.Info);
        }
        画工具条(服);
        if (服 == null || 服.当前世界 == null) return;
        EditorGUILayout.Space(2f);
        画开关();
        EditorGUILayout.Space(2f);
        确保场(服);
        if (场 == null) { EditorGUILayout.LabelField("场算不出来（目标格不合法？）"); return; }
        画状态行(服);
        EditorGUILayout.Space(2f);
        画图(服);
    }

    // ================= 取服务 =================

    private 格子探索服务 取当前服务;
    private 格子探索服务 取服务()
    {
        // 三层里"真的有世界"的那一层优先（玩家在楼里时大世界的 当前世界 仍然在 —— 那是另一回事，别抢）
        格子探索服务 房 = ServiceRegistry.Get<房间探索服务>();
        格子探索服务 区 = ServiceRegistry.Get<区域探索服务>();
        格子探索服务 大 = ServiceRegistry.Get<大世界探索服务>();
        switch (选层)
        {
            case 层.房间: return 房;
            case 层.区域: return 区;
            default: return 大;
        }
    }

    // ================= 工具条 =================

    private void 画工具条(格子探索服务 服)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUILayout.LabelField("层", GUILayout.Width(20f));
            foreach (层 L in new[] { 层.大世界, 层.区域, 层.房间 })
            {
                bool 有 = 取那层(L)?.当前世界 != null;
                var 原 = GUI.backgroundColor;
                if (有) GUI.backgroundColor = new Color(0.55f, 0.85f, 0.6f);
                if (GUILayout.Toggle(选层 == L, L.ToString(), EditorStyles.toolbarButton, GUILayout.Width(58f)) && 选层 != L)
                {
                    选层 = L;
                    场 = null; 图 = null;    // 换层 → 场与图都作废
                }
                GUI.backgroundColor = 原;
            }
            GUILayout.Space(10f);
            EditorGUILayout.LabelField("目标", GUILayout.Width(28f));
            if (GUILayout.Toggle(目标模式 == 0, "跟玩家", EditorStyles.toolbarButton, GUILayout.Width(56f)) && 目标模式 != 0) { 目标模式 = 0; 场 = null; }
            if (GUILayout.Toggle(目标模式 == 1, "营地门口", EditorStyles.toolbarButton, GUILayout.Width(66f)) && 目标模式 != 1) { 目标模式 = 1; 场 = null; }
            if (GUILayout.Toggle(目标模式 == 2, "手填", EditorStyles.toolbarButton, GUILayout.Width(44f)) && 目标模式 != 2) { 目标模式 = 2; 场 = null; }
            if (目标模式 == 2)
            {
                手填列 = EditorGUILayout.IntField(手填列, EditorStyles.toolbarTextField, GUILayout.Width(40f));
                手填行 = EditorGUILayout.IntField(手填行, EditorStyles.toolbarTextField, GUILayout.Width(40f));
                if (GUILayout.Button("重算", EditorStyles.toolbarButton, GUILayout.Width(40f))) 场 = null;
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("放大", EditorStyles.toolbarButton, GUILayout.Width(38f))) 格像素 = Mathf.Min(24, 格像素 + 1);
            if (GUILayout.Button("缩小", EditorStyles.toolbarButton, GUILayout.Width(38f))) 格像素 = Mathf.Max(2, 格像素 - 1);
            EditorGUILayout.LabelField($"{格像素}px", GUILayout.Width(34f));
        }
    }

    private 格子探索服务 取那层(层 L)
    {
        switch (L)
        {
            case 层.房间: return ServiceRegistry.Get<房间探索服务>();
            case 层.区域: return ServiceRegistry.Get<区域探索服务>();
            default: return ServiceRegistry.Get<大世界探索服务>();
        }
    }

    private void 画开关()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            显热力 = GUILayout.Toggle(显热力, " 热力(积分场)", GUILayout.Width(102f));
            显箭头 = GUILayout.Toggle(显箭头, " 箭头(方向场)", GUILayout.Width(102f));
            显挡格 = GUILayout.Toggle(显挡格, " 挡格", GUILayout.Width(62f));
            显敌人 = GUILayout.Toggle(显敌人, " 敌人", GUILayout.Width(58f));
            显出生圈 = GUILayout.Toggle(显出生圈, " 出生点", GUILayout.Width(66f));
            显巡逻圈 = GUILayout.Toggle(显巡逻圈, " 巡逻/追击圈", GUILayout.Width(92f));
            跟随玩家 = GUILayout.Toggle(跟随玩家, " 目标跟随玩家", GUILayout.Width(104f));
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            显听距 = GUILayout.Toggle(显听距, " 听距圈", GUILayout.Width(70f));
            听距档 = EditorGUILayout.Popup(听距档, Array.ConvertAll(听距档表, x => x.名), GUILayout.Width(130f));
            EditorGUILayout.LabelField("（听距 = 听觉半径 × 实际强度/50 × 层系数；实际强度 = 强度 × 潜行削弱）", EditorStyles.miniLabel);
        }
    }

    // ================= 算场 =================

    private void 确保场(格子探索服务 服)
    {
        var 世界 = 服.当前世界;
        int 列, 行;
        switch (目标模式)
        {
            case 1:
                if (选层 == 层.大世界) { var 门 = 大世界生成器.营地门口格(世界); 列 = 门.列; 行 = 门.行; }
                else if (选层 == 层.区域) { var 口 = 区域生成器.出口格(世界); 列 = 口.列; 行 = 口.行; }
                else { 列 = 世界.入口列; 行 = 世界.入口行; }
                break;
            case 2: 列 = 手填列; 行 = 手填行; break;
            default: 列 = 服.玩家列; 行 = 服.玩家行; break;
        }
        bool 变了 = 世界 != 场所在世界 || 列 != 场目标列 || 行 != 场目标行;
        if (!变了 && 场 != null) return;

        场所在世界 = 世界;
        场目标列 = 列;
        场目标行 = 行;
        场 = 流场.建场(世界, 列, 行);          // ★ 与游戏**同一个函数**
        挡表 = 流场.建挡表(世界, false);        // ★ 同一个判据
        类型码 = 建类型码(世界);
        最大距 = 1; 可达格数 = 0;
        for (int r = 0; r < 世界.行; r++)
            for (int c = 0; c < 世界.列; c++)
            {
                int d = 场.距值(c, r);
                if (d < 0) continue;
                可达格数++;
                if (d > 最大距) 最大距 = d;
            }
        建图(世界, 服);
    }

    // 每格"是什么"：**一次遍历实体填足迹**（不能逐格 格上实体() —— 大世界 1 万格 × 600 实体 = 千万级）
    private static byte[] 建类型码(网格数据 世界)
    {
        var 码 = new byte[世界.列 * 世界.行];
        foreach (var e in 世界.实体)
        {
            if (e == null || !e.占格) continue;
            byte k = 0;
            switch (e.类型)
            {
                case 网格实体类型.墙: k = 1; break;
                case 网格实体类型.障碍: k = 2; break;
                case 网格实体类型.区域: k = 3; break;
                case 网格实体类型.建筑: k = 4; break;
                case 网格实体类型.临时建筑: k = 5; break;
                case 网格实体类型.容器: k = 6; break;
                case 网格实体类型.敌人: k = 7; break;
                default: k = 8; break;
            }
            for (int r = e.行; r < e.行 + e.高; r++)
                for (int c = e.列; c < e.列 + e.宽; c++)
                {
                    if (!世界.界内(c, r) || !e.覆盖(c, r)) continue;
                    码[r * 世界.列 + c] = k;
                }
        }
        return 码;
    }

    private static Color32 挡色(byte k)
    {
        switch (k)
        {
            case 1: return new Color32(58, 58, 64, 255);      // 墙
            case 2: return new Color32(96, 78, 58, 255);      // 障碍
            case 3: return new Color32(70, 92, 110, 255);     // 区域
            case 4: return new Color32(84, 104, 72, 255);     // 建筑
            case 5: return new Color32(104, 88, 120, 255);    // 临时建筑
            case 6: return new Color32(112, 96, 76, 255);     // 容器
            default: return new Color32(72, 72, 72, 255);
        }
    }

    // 热力配色：近（蓝绿）→ 远（黄红）
    private static Color32 热力色(int 距, int 最大)
    {
        float t = Mathf.Clamp01(最大 <= 0 ? 0f : 距 / (float)最大);
        return Color32.Lerp(new Color32(38, 96, 118, 255), new Color32(150, 74, 46, 255), t);
    }

    private void 建图(网格数据 世界, 格子探索服务 服)
    {
        if (图 != null) DestroyImmediate(图);
        图 = new Texture2D(世界.列, 世界.行, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var 像素 = new Color32[世界.列 * 世界.行];
        var 不可达 = new Color32(26, 26, 28, 255);
        for (int r = 0; r < 世界.行; r++)
            for (int c = 0; c < 世界.列; c++)
            {
                int i = r * 世界.列 + c;
                Color32 色;
                if (显挡格 && 挡表[i]) 色 = 挡色(类型码[i]);            // 挡格最优先（它是"场为什么这么走"的原因）
                else if (挡表[i]) 色 = new Color32(44, 44, 48, 255);
                else
                {
                    int d = 场.距值(c, r);
                    色 = d < 0 ? 不可达 : (显热力 ? 热力色(d, 最大距) : new Color32(60, 62, 66, 255));
                }
                像素[(世界.行 - 1 - r) * 世界.列 + c] = 色;   // Texture2D 原点在左下 → 翻 Y
            }
        图.SetPixels32(像素);
        图.Apply();
    }

    // ================= 状态行 =================

    private void 画状态行(格子探索服务 服)
    {
        var 世界 = 服.当前世界;
        bool 吸附 = 场.源列 != 场.请求列 || 场.源行 != 场.请求行;
        string 源 = 场.有源 ? $"({场.源列},{场.源行})" : "**没有源**（附近全是墙）";
        EditorGUILayout.LabelField(
            $"目标 ({场目标列},{场目标行})　源 {源}{(吸附 ? "　⚠ 被吸附（目标格不可走）" : "")}　" +
            $"可达 {可达格数} 格　最远 {最大距} 步　世界 {世界.列}×{世界.行}　移动 {服.移动游戏分钟} 游戏分钟/格　层系数 {服.噪音层系数:0.##}",
            EditorStyles.miniLabel);
    }

    // ================= 画图 =================

    private void 画图(格子探索服务 服)
    {
        var 世界 = 服.当前世界;
        float 宽 = 世界.列 * 格像素, 高 = 世界.行 * 格像素;
        using (var 滚 = new EditorGUILayout.ScrollViewScope(滚动, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
        {
            滚动 = 滚.scrollPosition;
            var 区 = GUILayoutUtility.GetRect(宽, 高, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            if (Event.current.type != EventType.Repaint) return;

            if (图 != null) GUI.DrawTexture(区, 图, ScaleMode.StretchToFill, false);

            // 箭头（方向场）：4 邻里距最小的那一格
            if (显箭头 && 格像素 >= 5)
            {
                var 箭色 = new Color(1f, 1f, 1f, 0.55f);
                float t = Mathf.Max(1f, 格像素 * 0.22f);
                for (int r = 0; r < 世界.行; r++)
                    for (int c = 0; c < 世界.列; c++)
                    {
                        int d = 场.距值(c, r);
                        if (d <= 0) continue;
                        var 下 = 场.下一步(c, r);
                        if (下 == null) continue;
                        float cx = 区.x + (c + 0.5f) * 格像素, cy = 区.y + (r + 0.5f) * 格像素;
                        float dx = 下.Value.列 - c, dy = 下.Value.行 - r;
                        float 长 = 格像素 * 0.34f;
                        float x1 = cx - dx * 长 * 0.5f, y1 = cy - dy * 长 * 0.5f;
                        float x2 = cx + dx * 长 * 0.5f, y2 = cy + dy * 长 * 0.5f;
                        if (dx != 0) EditorGUI.DrawRect(new Rect(Mathf.Min(x1, x2), cy - t * 0.5f, Mathf.Abs(x2 - x1), t), 箭色);
                        else EditorGUI.DrawRect(new Rect(cx - t * 0.5f, Mathf.Min(y1, y2), t, Mathf.Abs(y2 - y1)), 箭色);
                    }
            }

            // 目标 / 源
            if (场.有源)
            {
                EditorGUI.DrawRect(格区(区, 场.源列, 场.源行, 世界), new Color(0.35f, 1f, 0.45f, 0.75f));
                EditorGUI.DrawRect(格区(区, 场目标列, 场目标行, 世界), new Color(1f, 1f, 0.25f, 0.55f));
            }

            // 玩家
            EditorGUI.DrawRect(格区(区, 服.玩家列, 服.玩家行, 世界), new Color(1f, 1f, 1f, 0.85f));

            if (显敌人) 画敌人(区, 世界, 服);
        }
    }

    private static Rect 格区(Rect 区, int 列, int 行, 网格数据 世界)
    {
        float px = 区.width / 世界.列;
        return new Rect(区.x + 列 * px, 区.y + 行 * px, px, px);
    }

    private void 画敌人(Rect 区, 网格数据 世界, 格子探索服务 服)
    {
        int 潜行 = ServiceRegistry.Get<PlayerService>()?.档案?.潜行值 ?? 0;
        float 层系数 = 服.噪音层系数;
        int 强度 = 听距档表[Mathf.Clamp(听距档, 0, 听距档表.Length - 1)].强度;

        foreach (var 敌 in 世界.取类型(网格实体类型.敌人))
        {
            if (敌 == null) continue;
            var 态 = 服.敌人AI态只读(敌);
            var 数据 = 服.取敌数据(敌);
            Color 色 = new Color(0.85f, 0.85f, 0.35f, 0.9f);   // 未知态 = 黄
            if (态 != null)
            {
                switch (态.状态)
                {
                    case 敌人状态.静默: 色 = new Color(0.45f, 0.75f, 1f, 0.9f); break;    // 蓝：蜷着
                    case 敌人状态.巡逻: 色 = new Color(0.85f, 0.85f, 0.35f, 0.9f); break;  // 黄：在晃
                    default: 色 = new Color(1f, 0.35f, 0.3f, 0.95f); break;               // 红：追你
                }
            }
            EditorGUI.DrawRect(格区(区, 敌.列, 敌.行, 世界), 色);

            float px = 区.width / 世界.列;
            Vector2 中心 = new Vector2(区.x + (敌.列 + 0.5f) * px, 区.y + (敌.行 + 0.5f) * px);

            // 出生点 + 巡逻半径 / 追击上限：把"它被什么约束着"直接画出来
            if (态 != null && 态.已记出生点 && 显出生圈 && 数据 != null)
            {
                if (显巡逻圈 && 数据.巡逻半径 > 0)
                    画圈(中心, 区.x + (态.出生列 + 0.5f) * px, 区.y + (态.出生行 + 0.5f) * px, 数据.巡逻半径 * px,
                          new Color(0.5f, 0.8f, 1f, 0.5f));
                if (显巡逻圈 && 数据.追击上限 > 0)
                    画圈(中心, 区.x + (态.出生列 + 0.5f) * px, 区.y + (态.出生行 + 0.5f) * px, 数据.追击上限 * px,
                          new Color(1f, 0.55f, 0.35f, 0.35f));
                EditorGUI.DrawRect(格区(区, 态.出生列, 态.出生行, 世界), new Color(0.5f, 0.8f, 1f, 0.35f));
            }
            // 听距圈：这只敌人"实际听得见多远"（含潜行削弱与层系数）
            if (显听距 && 数据 != null)
            {
                float 实 = 大世界敌人AI.实际强度(强度, 潜行);
                float 听 = 大世界敌人AI.听距(数据, 实, 层系数);
                画圈(中心, 中心.x, 中心.y, 听 * px, new Color(1f, 1f, 0.4f, 0.30f));
            }
            // 标签：状态 + 步数（格够大才画，免得糊成一片）
            if (格像素 >= 9)
                EditorGUI.LabelField(new Rect(中心.x - 30f, 中心.y - 8f, 60f, 16f),
                    $"{(态 == null ? "?" : 态.状态名)}{(态 == null ? "" : " " + 态.步数)}", EditorStyles.miniLabel);
        }
    }

    // 画圆：UNITY 的 EditorGUI 没有画圆 API —— 用 32 段小方块拼（够看，不引素材）
    private static void 画圈(Vector2 _, float 心x, float 心y, float 半径, Color 色)
    {
        if (半径 < 2f) return;
        const int 段 = 40;
        float 周长 = 2f * Mathf.PI * 半径;
        float 块 = Mathf.Max(1.5f, 周长 / 段 * 1.4f);
        for (int i = 0; i < 段; i++)
        {
            float a = i * 2f * Mathf.PI / 段;
            EditorGUI.DrawRect(new Rect(心x + Mathf.Cos(a) * 半径 - 块 * 0.5f, 心y + Mathf.Sin(a) * 半径 - 块 * 0.5f, 块, 块), 色);
        }
    }
}
