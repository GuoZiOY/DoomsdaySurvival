using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================
// 房间网格面板：网格面板基类 子类（对位 家具网格面板）——只写「房间里的实体语义」钩子。
// 实体承载：墙 / 容器 / 尸体 / 敌人 / 玩家 都是 物品堆叠（由 房间探索服务 同步进 数据源 网格服务）；
// 通行判定 = 该格有没有实体 —— 与 A* 用的是同一份 房间数据，不会两边打架。
//
// 外观（"简单美术"，全部由 房间贴图 程序化生成，零美术资源）：
//   墙   = 墙顶竖纹贴图铺满整格（相邻墙无缝连成一片）+ 只在与"非墙"相邻的边描一条暗边（转角闭合）
//   容器 = 按名称挑剪影（柜 / 冰箱 / 纸箱 / 货架）+ 名称淡字；翻过 → 灰化 + 打勾（还能再打开，右键「接着翻」）
//   尸体 = 躺姿剪影；敌人 = 圆盘 + 丧尸剪影 + 血条；玩家 = 圆盘 + 人形 + 圆环 + "你"
// 分层：底盘/底座层/线层/物品层 由基类管；地表/迷雾/路径/效果/交互 由 房间图层 管。
// ⚠ 本类**绝对不能写 void Update()**：基类的 Update 是 private，子类再写一个会隐藏它，
//   基类的刷新调度（待刷新网格/待刷新物品/拖拽）会整个失效。每帧逻辑一律放 房间图层。
// ============================================================
public sealed class 房间网格面板 : 网格面板基类
{
    public 房间面板 宿主;   // 宿主面板（信息条/提示行/离开；可在 Inspector 拖入，也可留空）

    // 玩家令牌的位置由 房间图层 平滑接管（否则基类增量刷新会把它一把拽回格子中心）
    [NonSerialized] public bool 接管玩家位置;

    private 房间图层 图层缓存;

    private 房间探索服务 房间服务 => ServiceRegistry.Get<房间探索服务>();
    private 房间数据 房间 => 房间服务?.当前房间;

    // 供 房间图层 用：基类四层里 物品层（判断重排）、底座层（判断重建）
    public RectTransform 物品层引用 => 物品层;
    public RectTransform 底座层引用 => 底座层;

    // 取某实体（按实例标识）的可见框——房间图层 用它平滑驱动玩家令牌
    public RectTransform 实体框矩形(string 标识)
    {
        var 网格 = 房间服务?.网格;
        if (网格 == null || string.IsNullOrEmpty(标识)) return null;
        foreach (var 堆叠 in 网格.网格物品)
            if (堆叠 != null && 堆叠.标识 == 标识) return 物品框矩形(堆叠);
        return null;
    }


    public 房间图层 图层()
    {
        if (图层缓存 == null) 图层缓存 = GetComponent<房间图层>();
        return 图层缓存;
    }

    // ================= 实体框 =================

    protected override 物品框 创建实体框(物品堆叠 堆叠)
    {
        if (堆叠 == null) return null;
        var 实体 = 找实体(堆叠.标识);
        var (宽, 高) = 格数(堆叠);
        bool 是墙 = 实体 != null && 实体.类型 == 房间实体类型.墙;
        bool 是玩家 = 实体 != null && 实体.是玩家;
        bool 是敌人 = 实体 != null && 实体.类型 == 房间实体类型.敌人;
        bool 是尸体 = 实体 != null && 实体.类型 == 房间实体类型.尸体;

        var 物体 = new GameObject($"实体_{堆叠.标识}", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(物品层, false);
        var 框 = new 物品框();
        框.根 = 物体.GetComponent<RectTransform>();
        物体.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // 框根透明（只留贴图与描边）
        物体.AddComponent<CanvasGroup>();                                // 已搜灰化 / 迷雾压暗 用
        if (!是墙 && !是玩家 && !是敌人)   // 墙靠相邻描边、令牌靠圆盘，不要方块黑框
        {
            var 描边 = 物体.AddComponent<Outline>();
            描边.effectColor = 网格面板配色.房间实体描边;
            描边.effectDistance = 物品描边距离;
        }
        定位(框.根, 堆叠.列, 堆叠.行, 宽, 高);
        框.高光层 = 创建高光层(物体.transform);

        float 整宽 = 宽 * 格尺寸, 整高 = 高 * 格尺寸;
        if (是墙)
        {
            var 墙图 = 摆满(物体.transform, "墙", 房间贴图.墙顶(), Color.white, 整宽, 整高);
            框.内容层 = 墙图.rectTransform;
            建墙边(物体.transform, 实体, 整宽, 整高);
        }
        else if (是玩家 || 是敌人)
        {
            float 边长 = Mathf.Min(整宽, 整高) - 18f;
            var 偏移 = new Vector2(0f, 6f);
            var 盘 = 摆居中(物体.transform, "盘", 房间贴图.圆盘(), 边长, 偏移);
            摆居中(物体.transform, "剪影", 房间贴图.剪影(), 边长, 偏移);
            if (是玩家)
                摆居中(物体.transform, "环", 房间贴图.圆环(), 边长, 偏移).color = 网格面板配色.玩家令牌边;
            框.内容层 = 盘.rectTransform;
            框.框图 = 盘;   // 令牌：圆盘按阵营上色（更新时改色）
            框.名称 = 建名称(物体.transform, 是玩家 ? "你" : (实体?.名称 ?? ""), new Vector2(0.5f, 0f), new Vector2(格尺寸 * 宽, 20f), 15f);
        }        else if (是尸体)
        {
            var 图 = 摆居中(物体.transform, "尸体", 房间贴图.尸体(), Mathf.Min(整宽, 整高) - 12f, Vector2.zero);
            框.内容层 = 图.rectTransform;
            框.名称 = 建名称(物体.transform, "尸体", new Vector2(0.5f, 0.5f), new Vector2(整宽, 整高), Mathf.Clamp(格尺寸 * 0.20f, 13f, 26f));
        }
        else
        {
            var 图 = 摆满(物体.transform, "容器", 房间贴图.容器(实体?.名称), Color.white, 整宽 - 10f, 整高 - 10f);
            框.内容层 = 图.rectTransform;
            框.名称 = 建名称(物体.transform, 实体?.名称 ?? 堆叠.标识, new Vector2(0.5f, 0.5f), new Vector2(整宽, 整高), Mathf.Clamp(格尺寸 * 0.20f, 13f, 26f));
            if (亮底(实体?.名称)) 框.名称.color = new Color(0.12f, 0.12f, 0.14f, 0.85f);   // 浅色容器（冰箱/冷柜）上用深字，不然看不清
        }

        if (!是墙 && !是玩家 && !是敌人) 建边界线(物体.transform);   // 容器/尸体 画自己的边界线（墙连成一片、令牌用圆盘，都不画）
        挂接交互(框, 堆叠, 框.内容层);
        return 框;
    }

    protected override void 更新实体框(物品框 框, 物品堆叠 堆叠)
    {
        if (框?.根 == null || 堆叠 == null) return;
        var 实体 = 找实体(堆叠.标识);
        var (宽, 高) = 格数(堆叠);
        // 玩家令牌：位置由 房间图层 平滑接管时，这里不要把它拽回格子（否则会和插值打架）
        if (!(接管玩家位置 && 实体 != null && 实体.是玩家))
            框.根.anchoredPosition = new Vector2(格x(堆叠.列, 堆叠.行), -格y(堆叠.列, 堆叠.行));
        var 目标尺寸 = new Vector2(宽 * 格尺寸, 高 * 格尺寸);
        if ((框.根.sizeDelta - 目标尺寸).sqrMagnitude > 0.01f) 框.根.sizeDelta = 目标尺寸;

        // 可见性按"档"：玩家永远画；敌人只在 亮（核心）里画；墙 只要不是全黑就画（白天记忆 / 夜晚弱视野都看得见轮廓）；
        // 容器/尸体 只在 亮 或 白天的暗记忆 里画（夜晚弱视野里看不见要翻的东西）
        var 档 = 房间服务?.档(实体.列, 实体.行) ?? 视野档.亮;
        bool 亮 = 档 == 视野档.亮;
        bool 显示 = 实体 == null;
        if (实体 != null)
        {
            if (实体.是玩家) 显示 = true;
            else if (实体.类型 == 房间实体类型.敌人) 显示 = 亮;
            else if (实体.类型 == 房间实体类型.墙) 显示 = 档 != 视野档.全黑;
            else 显示 = 亮 || 档 == 视野档.暗记忆;
        }
        if (框.根.gameObject.activeSelf != 显示) 框.根.gameObject.SetActive(显示);
        if (!显示) return;

        var 组 = 框.根.GetComponent<CanvasGroup>();
        if (组 != null)
        {
            float 透明 = 1f;
            if (实体 != null && 已翻过(实体)) 透明 = 网格面板配色.已搜灰化;
            else if (实体 != null && !实体.是玩家 && !亮) 透明 = 0.55f;   // 阴影里的东西压暗，与"眼前"区分
            if (Mathf.Abs(组.alpha - 透明) > 0.01f) 组.alpha = 透明;
        }
        if (框.框图 != null && 实体 != null)
        {
            var 色 = 实体色(实体);
            if (框.框图.color != 色) 框.框图.color = 色;
        }
        if (框.名称 != null && 实体 != null)
        {
            string 文本 = 已翻过(实体) ? $"{实体.名称} ✓" : 实体.名称;
            if (框.名称.text != 文本) 框.名称.text = 文本;
        }
    }

    // ================= 交互钩子 =================

    protected override void 单击实体(物品堆叠 堆叠, int 点击次数)
    {
        var 实体 = 找实体(堆叠?.标识);
        if (实体 == null) return;
        // 点容器/敌人/尸体 = 走到它的**相邻格**（与点空格同一条反馈路径，见 点格反馈）；
        // 点墙也走这里：走得过去 = 站到墙边，走不过去 = 「那边过不去」+ 错误音 + 闪红，不再是"点了没反应"。
        点格反馈(实体.列, 实体.行);
    }

    protected override void 右键实体(物品堆叠 堆叠, RectTransform 框)
    {
        var 实体 = 找实体(堆叠?.标识);
        if (实体 == null || 右键菜单.实例 == null) return;
        if (实体.类型 != 房间实体类型.容器 && 实体.类型 != 房间实体类型.尸体 && 实体.类型 != 房间实体类型.敌人) return;
        右键菜单.实例.目标面板 = this;
        var 条目 = new List<(string 文本, Action 动作)>();
        if (实体.类型 == 房间实体类型.敌人)
        {
            条目.Add(("查看", () => 查看(实体)));
        }
        else if (房间服务 != null && !房间服务.够得着(实体))
        {
            // 隔着屋子不能翻：只给「移动到此处」（= 走到它旁边停下，与左键点它是同一件事）
            条目.Add(("移动到此处", () => 房间服务?.点格(实体.列, 实体.行)));
        }
        else
        {
            // 容器不是一次性的：没翻过 = 「搜索」（首次扣行动点）；翻过 = 「接着翻」（不扣，进度原样留着）
            if (!已翻过(实体)) 条目.Add(("搜索", () => 搜索(实体)));
            else 条目.Add(("接着翻", () => 搜索(实体)));
            条目.Add(("查看", () => 查看(实体)));
        }
        if (条目.Count == 0) return;
        右键菜单.实例.显示动作(条目, 框);
    }

    // 房间里的东西不可拖（基类拖拽机制自然休眠：投影/跨面板都不用管）
    protected override bool 完成拖拽(PointerEventData 事件, 物品堆叠 堆叠) => false;
    protected override bool 允许跨面板() => false;
    protected override bool 允许开始拖拽(物品堆叠 堆叠) => false;

    // 这一处翻过没有 —— 一律问 房间探索服务（战局进度，跨"再进这间房"仍保持）。
    // 不看 房间实体.已搜：那只是"本趟点过"的表现位，房间一重生成就没了，用它会让翻过的柜子又变回没翻过。
    private bool 已翻过(房间实体 实体) => 房间服务?.已翻过(实体) == true;

    private void 搜索(房间实体 实体)
    {
        房间服务?.搜索容器(实体);
        图层()?.刷新();
    }

    private void 查看(房间实体 实体)
    {
        string 文本 = 实体.类型 switch
        {
            房间实体类型.敌人 => $"{实体.名称}：它还没发现你，或者正在等你靠近。",
            房间实体类型.尸体 => "一具尸体。翻一翻也许还有东西。",
            _ => 实体.容器定义 != null && !string.IsNullOrEmpty(实体.容器定义.描述)
                ? 实体.容器定义.描述
                : $"{实体.名称}：看起来能翻一翻。",
        };
        ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.探索, 文本));
    }

    // ================= 空格点击（由 房间格点击 转发）=====

    public void 左键格(int 列, int 行) => 点格反馈(列, 行);

    // 点一格的统一反馈（空格 / 实体 都走这里）——**点下去当场就有反应**，不等这一格走完：
    //   ① 音效（走得了 = 按钮成功音；走不了 = 错误音）；
    //   ② 落点圈（效果层 在目标格立刻弹一圈，0.30 秒淡出）；
    //   ③ 路径立刻改画 + 令牌当场拐弯（服务 点格 直接换路径；视觉由 房间图层.推进移动 连续接管）。
    private void 点格反馈(int 列, int 行)
    {
        var 服 = 房间服务;
        if (服 == null || !服.探索中) return;
        if (服.点格(列, 行))
        {
            音效管理器.实例?.播放成功();
            图层()?.落点(列, 行);
        }
        else
        {
            音效管理器.实例?.播放失败();
            图层()?.闪红(列, 行);
        }
        图层()?.刷新();
    }

    public void 右键格(int 列, int 行)
    {
        // 空格右键暂不弹菜单（右键菜单只用于 容器/尸体/敌人 这类实体）
    }

    public void 悬停格(int 列, int 行) => 图层()?.悬停(列, 行);
    public void 离开格(int 列, int 行) => 图层()?.悬停(-1, -1);

    // ================= 外观工具 =================

    // 铺满整块（墙与容器用：相邻格子的贴图无缝接在一起）
    private static Image 摆满(Transform 父, string 名, Sprite 精灵, Color 色, float 宽, float 高)
    {
        var 图 = UI工具.创建图(父, 名, 精灵, 色, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        UI工具.设锚(图.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(宽, 高));
        图.raycastTarget = false;
        return 图;
    }

    // 居中摆放（令牌与剪影用）
    private static Image 摆居中(Transform 父, string 名, Sprite 精灵, float 边长, Vector2 偏移)
    {
        var 图 = UI工具.创建图(父, 名, 精灵, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        UI工具.设锚(图.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 偏移, new Vector2(边长, 边长));
        图.raycastTarget = false;
        return 图;
    }

    private static TMP_Text 建名称(Transform 父, string 文本, Vector2 锚点, Vector2 尺寸, float 字号)
    {
        var 名 = UI工具.创建文本(父, "名称", 文本, 字号, TextAlignmentOptions.Center);
        UI工具.设锚(名.rectTransform, 锚点, 锚点, Vector2.zero, 尺寸);
        名.enableWordWrapping = true;
        名.color = new Color(1f, 1f, 1f, 0.72f);
        名.raycastTarget = false;
        return 名;
    }

    // 浅色剪影（冰箱/冷柜）→ 名称改用深色
    private static bool 亮底(string 名称)
        => !string.IsNullOrEmpty(名称) && (名称.Contains("冰箱") || 名称.Contains("冷柜"));

    // 墙的转角闭合：只在与"非墙"相邻的那条边描一条暗边（相邻墙之间不描 → 连成一整片墙）
    private void 建墙边(Transform 父, 房间实体 墙, float 宽, float 高)
    {
        var 房 = 房间;
        if (房 == null || 墙 == null) return;
        if (!是墙(房, 墙.列, 墙.行 - 1)) 建线条(父, "边上", new Vector2(0.5f, 1f), new Vector2(宽, 3f));
        if (!是墙(房, 墙.列, 墙.行 + 1)) 建线条(父, "边下", new Vector2(0.5f, 0f), new Vector2(宽, 3f));
        if (!是墙(房, 墙.列 - 1, 墙.行)) 建线条(父, "边左", new Vector2(0f, 0.5f), new Vector2(3f, 高));
        if (!是墙(房, 墙.列 + 1, 墙.行)) 建线条(父, "边右", new Vector2(1f, 0.5f), new Vector2(3f, 高));
    }

    private static bool 是墙(房间数据 房, int 列, int 行)
    {
        if (房 == null || !房.界内(列, 行)) return false;   // 界外 = 不是墙（外圈要描边，房间才有轮廓）
        var 实体 = 房.格上实体(列, 行);
        return 实体 != null && 实体.类型 == 房间实体类型.墙;
    }

    private static void 建线条(Transform 父, string 名, Vector2 锚点, Vector2 尺寸)
    {
        var 图 = UI工具.创建图(父, 名, null, new Color(0f, 0f, 0f, 0.8f), 锚点, 锚点);
        UI工具.设锚(图.rectTransform, 锚点, 锚点, Vector2.zero, 尺寸);
        图.raycastTarget = false;
    }

    // ================= 内部 =================

    private 房间实体 找实体(string 标识)
    {
        if (string.IsNullOrEmpty(标识) || 房间 == null) return null;
        return 房间.按标识(标识);
    }

    private (int 宽, int 高) 格数(物品堆叠 堆叠)
    {
        var 网格 = 房间服务?.网格;
        return 网格 != null ? 网格.物品占格(堆叠) : (1, 1);
    }

    private Color 实体色(房间实体 实体)
    {
        if (实体 == null) return 网格面板配色.底座色;
        switch (实体.类型)
        {
            case 房间实体类型.敌人: return 网格面板配色.敌人令牌色;
            case 房间实体类型.玩家: return 网格面板配色.玩家令牌色;
            default: return Color.white;   // 贴图已带色（墙/容器/尸体），不再上色
        }
    }
}
