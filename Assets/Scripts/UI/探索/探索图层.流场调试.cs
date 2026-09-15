using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// ============================================================
// 探索图层 · 流场调试叠层（**游玩时直接盖在真实格子上**）
//
// 为什么单独一个 partial 文件：探索图层.cs 已经 900+ 行，而这是**调试工具**，不该混进主流程。
// 与 网格面板基类.渲染.cs / .交互.cs / .拖拽.cs 同一个组织方式。
//
// 它画的是**游戏里正在跑的那份场**：`流场.建场(...)` 现算 —— 和敌人 AI 用的是同一个函数。
// 于是"敌人为什么往那边走 / 它看不看得见我 / 这个柜子后面那条路通不通"全都能当场看出来。
//
// 热键（都只在探索面板激活时有效）：
//   F2  开/关整个叠层
//   F3  切目标：跟随玩家 ↔ 固定（Shift+左键点格子 = 把目标钉在那一格）
//   F4  数字（积分场）开关
//   F5  重算（大网格不自动跟随玩家，见下）
//   F6  箭头（方向场）开关
//   F7  挡格着色开关
//
// 大网格（>2500 格）**不自动跟随玩家**：一次建场要 3 个 int[格数]（大世界 120 KB），
// 每走一步重建是每秒几百 KB 的垃圾。手动 F5 就够——看场是"停下来看一眼"的动作，不是每帧都要。
// ============================================================
public abstract partial class 探索图层
{
    private bool 流场调试;
    private 流场 调试场;
    private 网格数据 调试场世界;          // 场是按哪个世界算的（换世界要重算）
    private int 调试目标列 = -1, 调试目标行 = -1;
    private bool 调试跟玩家 = true;
    private bool 调试数字 = true, 调试箭头 = true, 调试挡格 = true, 调试敌人 = true;
    private GUIStyle 样式数字, 样式箭头, 样式小字;
    private Texture2D 白图;                // 半透明底色块用（1×1）
    private int 调试可达, 调试最远;

    private const int 自动跟随上限格 = 2500;   // 超过就不自动跟随（见文件头）

    // ================= 每帧：热键 =================

    private void 流场调试_每帧()
    {
        if (调试键(KeyCode.F2)) 流场调试 = !流场调试;
        if (!流场调试) return;
        if (调试键(KeyCode.F3)) { 调试跟玩家 = !调试跟玩家; 调试目标列 = -1; }
        if (调试键(KeyCode.F4)) 调试数字 = !调试数字;
        if (调试键(KeyCode.F5)) { 调试目标列 = -1; 调试目标行 = -1; }   // 清目标 → 下一帧重算
        if (调试键(KeyCode.F6)) 调试箭头 = !调试箭头;
        if (调试键(KeyCode.F7)) 调试挡格 = !调试挡格;
        if (调试键(KeyCode.F8)) 调试敌人 = !调试敌人;

        // Shift + 左键：把目标钉在这一格（能点在墙上/柜子上 —— 场会**吸附**到最近可走格，正好演示那一步）
        if (Shift按住() && 调试键(KeyCode.Mouse0))
        {
            var 格 = 屏幕点转格(输入鼠标位置());
            if (格 != null)
            {
                调试跟玩家 = false;
                调试目标列 = 格.Value.列;
                调试目标行 = 格.Value.行;
            }
        }
    }

    // 新(InputSystem)/旧(Input Manager) 两套都认 —— 与 玩家输入系统.检测按下 同一写法
    private static bool 调试键(KeyCode 键)
    {
        bool 按下 = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            switch (键)
            {
                case KeyCode.F2: if (Keyboard.current.f2Key.wasPressedThisFrame) 按下 = true; break;
                case KeyCode.F3: if (Keyboard.current.f3Key.wasPressedThisFrame) 按下 = true; break;
                case KeyCode.F4: if (Keyboard.current.f4Key.wasPressedThisFrame) 按下 = true; break;
                case KeyCode.F5: if (Keyboard.current.f5Key.wasPressedThisFrame) 按下 = true; break;
                case KeyCode.F6: if (Keyboard.current.f6Key.wasPressedThisFrame) 按下 = true; break;
                case KeyCode.F7: if (Keyboard.current.f7Key.wasPressedThisFrame) 按下 = true; break;
                case KeyCode.F8: if (Keyboard.current.f8Key.wasPressedThisFrame) 按下 = true; break;
                case KeyCode.Mouse0: if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) 按下 = true; break;
            }
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(键)) 按下 = true;
#endif
        return 按下;
    }

    private static bool Shift按住()
    {
        bool 按下 = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed)) 按下 = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) 按下 = true;
#endif
        return 按下;
    }

    private static Vector2 输入鼠标位置()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null) return Mouse.current.position.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#else
        return Vector2.zero;
#endif
    }

    // ================= 屏幕 ↔ 格 =================

    // 视口局部 → 屏幕。`RectTransformUtility.WorldToScreenPoint` 在 Overlay 画布下传 null 相机。
    private Camera 画布相机()
    {
        var 画布 = GetComponentInParent<Canvas>();
        if (画布 == null) return null;
        return 画布.renderMode == RenderMode.ScreenSpaceOverlay ? null : 画布.worldCamera;
    }

    // ⚠ 坐标语义（第一版这里错了，记下来）：
    //   `相机位置` 那套公式（见 在窗口内）算出来的是**"相对视口左上角"**的坐标（x 向右、y 向下）。
    //   而 `RectTransform.TransformPoint` 吃的局部坐标是**"相对 pivot"**的 —— 视口 pivot 不是 (0,0) 时
    //   两者差一个 (rect.xMin, rect.yMax)，表现就是整层叠层**整体偏移**。
    //   所以这里显式做一次换算：左上角 ↔ pivot。
    private Vector3 左上角转局部(float 左上x, float 左上y)
    {
        var r = 视口.rect;
        return new Vector3(r.xMin + 左上x, r.yMax - 左上y, 0f);
    }

    private bool 格屏幕中心(int 列2, int 行2, out Vector2 屏幕)
    {
        屏幕 = Vector2.zero;
        if (视口 == null || 列2 < 0 || 行2 < 0 || 列2 >= 列 || 行2 >= 行) return false;
        float 格 = Mathf.Max(1f, 网格面板基类.格尺寸);
        // 与 在窗口内 / 摆格 / 更新实体框 同一套：相对视口左上角
        float 左上x = 列2 * 格 + 相机位置.x + 格 * 0.5f;
        float 左上y = 行2 * 格 - 相机位置.y + 格 * 0.5f;
        var 世界 = 视口.TransformPoint(左上角转局部(左上x, 左上y));
        屏幕 = RectTransformUtility.WorldToScreenPoint(画布相机(), 世界);
        return true;
    }

    // 一格在屏幕上占多少像素（画布缩放 / CanvasScaler 都算进去 —— 不要自己拿格尺寸当像素）
    private float 格屏幕边长()
    {
        if (视口 == null) return 8f;
        float 格 = Mathf.Max(1f, 网格面板基类.格尺寸);
        var 相机 = 画布相机();
        var a = RectTransformUtility.WorldToScreenPoint(相机, 视口.TransformPoint(左上角转局部(0f, 0f)));
        var b = RectTransformUtility.WorldToScreenPoint(相机, 视口.TransformPoint(左上角转局部(格, 0f)));
        return Mathf.Max(2f, Mathf.Abs(b.x - a.x));
    }

    private (int 列, int 行)? 屏幕点转格(Vector2 屏幕)
    {
        if (视口 == null) return null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(视口, 屏幕, 画布相机(), out var 局部)) return null;
        var r = 视口.rect;
        float 左上x = 局部.x - r.xMin;      // 局部(pivot) → 左上角
        float 左上y = r.yMax - 局部.y;
        float 格 = Mathf.Max(1f, 网格面板基类.格尺寸);
        int c = Mathf.FloorToInt((左上x - 相机位置.x) / 格);
        int r2 = Mathf.FloorToInt((左上y + 相机位置.y) / 格);
        if (c < 0 || r2 < 0 || c >= 列 || r2 >= 行) return null;
        return (c, r2);
    }

    // ================= 算场 =================

    private void 流场调试_确保场()
    {
        var 服 = 服务;
        var 世界 = 服?.当前世界;
        if (世界 == null) { 调试场 = null; 调试场世界 = null; return; }

        // 这一帧"想要的目标格"
        int 要列 = 调试目标列, 要行 = 调试目标行;
        if (调试跟玩家 && 世界.列 * 世界.行 <= 自动跟随上限格) { 要列 = 服.玩家列; 要行 = 服.玩家行; }
        if (要列 < 0 || 要行 < 0) { 要列 = 服.玩家列; 要行 = 服.玩家行; }

        // ★ 重算判据必须拿**上一张场实际用的目标**（调试场.请求列/行）去比。
        //   第一版写成"先把 调试目标列 赋值成 要列，再拿它跟 要列 比" → 比较恒真 →
        //   第一次建完就永远提前 return，数字/箭头再不更新（用户报的"没有实时更新"就是这个）。
        if (调试场 != null && 世界 == 调试场世界
            && 要列 == 调试场.请求列 && 要行 == 调试场.请求行) return;

        调试目标列 = 要列;
        调试目标行 = 要行;
        调试场世界 = 世界;
        调试场 = 流场.建场(世界, 要列, 要行);
        调试可达 = 0; 调试最远 = 0;
        for (int r = 0; r < 世界.行; r++)
            for (int c = 0; c < 世界.列; c++)
            {
                int d = 调试场.距值(c, r);
                if (d < 0) continue;
                调试可达++;
                if (d > 调试最远) 调试最远 = d;
            }
    }

    // ================= OnGUI：画在真实格子上 =================

    private void 流场调试_画()
    {
        if (!流场调试) return;
        var 服 = 服务;
        if (服?.当前世界 == null || 视口 == null) return;
        流场调试_确保场();
        if (调试场 == null) return;

        建样式();
        float 边长 = 格屏幕边长();
        float 半 = 边长 * 0.5f;
        int 潜行 = ServiceRegistry.Get<PlayerService>()?.档案?.潜行值 ?? 0;

        var (首列, 首行, 末列, 末行) = 窗口格区间(0);
        // 挡格只查一次（每格 占位 是线性扫描，1 万格 × 600 实体 = 太贵）→ 摊平成位图
        var 挡 = 流场.建挡表(服.当前世界, false);

        for (int r = 首行; r <= 末行; r++)
            for (int c = 首列; c <= 末列; c++)
            {
                if (!格屏幕中心(c, r, out var 屏)) continue;
                float x = 屏.x - 半, y = (Screen.height - 屏.y) - 半;
                if (x < -边长 || y < -边长 || x > Screen.width || y > Screen.height) continue;   // 屏外不画

                bool 是挡 = 挡[r * 服.当前世界.列 + c];
                if (是挡)
                {
                    if (调试挡格) 色块(new Rect(x, y, 边长, 边长), new Color(0.35f, 0.35f, 0.4f, 0.45f));
                    continue;   // 挡格上不画数字/箭头（场里它没有距离）
                }
                int 距 = 调试场.距值(c, r);
                if (距 < 0)
                {
                    if (调试挡格) 色块(new Rect(x, y, 边长, 边长), new Color(0.15f, 0.15f, 0.18f, 0.45f));
                    continue;
                }

                // 方格底：按距离上色（近=青绿，远=暗红）——与 Editor 窗口同一套观感
                if (调试挡格) 色块(new Rect(x, y, 边长, 边长), 距离色(距, 调试最远, 0.28f));

                if (调试数字 && 边长 >= 11f)
                {
                    样式数字.fontSize = Mathf.RoundToInt(边长 * 0.52f);
                    样式数字.normal.textColor = 距离色(距, 调试最远, 1f);
                    GUI.Label(new Rect(x, y, 边长, 边长), 距.ToString(), 样式数字);
                }
                if (调试箭头 && 边长 >= 9f)
                {
                    var 下 = 调试场.下一步(c, r);
                    if (下 != null)
                    {
                        int dc = 下.Value.列 - c, dr = 下.Value.行 - r;
                        string 字 = dc > 0 ? "→" : dc < 0 ? "←" : dr > 0 ? "↓" : "↑";
                        样式箭头.fontSize = Mathf.RoundToInt(边长 * 0.78f);
                        GUI.Label(new Rect(x, y, 边长, 边长), 字, 样式箭头);
                    }
                }
            }

        // 敌人：**按 AI 三态上色**（蓝静默 / 黄巡逻 / 红追踪）—— 测 AI 最直接的一眼。
        //   读的是只读口 敌人AI态只读()，不改任何状态；位置本来就每帧在变，所以这块天然实时。
        if (调试敌人)
            foreach (var 敌 in 服.当前世界.取类型(网格实体类型.敌人))
            {
                if (敌 == null) continue;
                var 态 = 服.敌人AI态只读(敌);
                Color 色 = 态 == null ? new Color(0.9f, 0.9f, 0.4f, 0.5f)
                    : 态.状态 == 敌人状态.静默 ? new Color(0.4f, 0.75f, 1f, 0.55f)
                    : 态.状态 == 敌人状态.巡逻 ? new Color(0.95f, 0.9f, 0.35f, 0.55f)
                    : new Color(1f, 0.3f, 0.25f, 0.7f);
                色块(格屏幕框(敌.列, 敌.行, 边长), 色);
            }

        // 目标 / 源（吸附后会分开）+ 玩家
        if (调试场.有源) 色块(格屏幕框(调试场.源列, 调试场.源行, 边长), new Color(0.3f, 1f, 0.45f, 0.30f));
        色块(格屏幕框(调试目标列, 调试目标行, 边长), new Color(1f, 1f, 0.25f, 0.30f));
        色块(格屏幕框(服.玩家列, 服.玩家行, 边长), new Color(1f, 1f, 1f, 0.30f));

        画面板(服, 潜行, 边长);
    }

    private Rect 格屏幕框(int 列2, int 行2, float 边长)
    {
        if (!格屏幕中心(列2, 行2, out var 屏)) return new Rect(-99f, -99f, 0f, 0f);
        return new Rect(屏.x - 边长 * 0.5f, (Screen.height - 屏.y) - 边长 * 0.5f, 边长, 边长);
    }

    private static Color 距离色(int 距, int 最远, float 透明)
    {
        float t = 最远 <= 0 ? 0f : Mathf.Clamp01(距 / (float)最远);
        var 色 = Color.Lerp(new Color(0.45f, 1f, 0.85f), new Color(1f, 0.35f, 0.3f), t);
        色.a = 透明;
        return 色;
    }

    private void 色块(Rect 区, Color 色)
    {
        if (白图 == null)
        {
            白图 = new Texture2D(1, 1);
            白图.SetPixel(0, 0, Color.white);
            白图.Apply();
        }
        var 原 = GUI.color;
        GUI.color = 色;
        GUI.DrawTexture(区, 白图);
        GUI.color = 原;
    }

    private void 建样式()
    {
        if (样式数字 == null)
            样式数字 = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        if (样式箭头 == null)
        {
            样式箭头 = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            样式箭头.normal.textColor = new Color(0.6f, 1f, 0.8f, 0.9f);
        }
        if (样式小字 == null)
            样式小字 = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
    }

    private void 画面板(格子探索服务 服, int 潜行, float 边长)
    {
        bool 吸附 = 调试场.有源 && (调试场.源列 != 调试场.请求列 || 调试场.源行 != 调试场.请求行);
        bool 过期 = 调试跟玩家 && 服.当前世界.列 * 服.当前世界.行 > 自动跟随上限格
                    && (调试目标列 != 服.玩家列 || 调试目标行 != 服.玩家行);
        var 区 = new Rect(8f, 8f, 286f, 148f);
        色块(区, new Color(0f, 0f, 0f, 0.72f));
        GUILayout.BeginArea(new Rect(区.x + 8f, 区.y + 6f, 区.width - 16f, 区.height - 10f));
        GUILayout.Label($"<b>流场</b>　目标 ({调试目标列},{调试目标行}){(吸附 ? "  ⚠吸附→源 " + 调试场.源列 + "," + 调试场.源行 : "")}", 样式小字);
        GUILayout.Label($"可达 {调试可达} 格　最远 {调试最远} 步　本层 {服.噪音层系数:0.##}　潜行 {潜行}", 样式小字);
        GUILayout.Label(过期 ? "<color=#ffcc44>⚠ 大网格不自动跟随 —— 按 F5 重算</color>" : "目标：" + (调试跟玩家 ? "跟随玩家" : "固定"), 样式小字);
        GUILayout.Label("F2 关　F3 目标　F4 数字　F5 重算　F6 箭头　F7 挡格　F8 敌人　Shift+左键 = 钉目标", 样式小字);
        GUILayout.Label($"数字 {(调试数字 ? "开" : "关")}　箭头 {(调试箭头 ? "开" : "关")}　挡格 {(调试挡格 ? "开" : "关")}　敌人 {(调试敌人 ? "开" : "关")}　格 {边长:0}px", 样式小字);
        GUILayout.EndArea();
    }
}
