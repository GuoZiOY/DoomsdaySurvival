using System;
using System.Collections.Generic;
using UnityEngine;

// 面板管理器 = 导航路由器：订阅导航事件，决定"哪个事件 → 显示哪个面板"。
// 末日《最后87天》清单：主菜单 / 角色创建 / 对话(短事件) / 战斗 / 房间(网格) / 区域(网格) / 营地 / 背包 / 交易 / 任务。
// 设施功能 → 面板 由 功能面板注册表 路由（替代原硬编码 switch）。
// 注：奇幻版的「城市地图（大地图/城镇小地图/节点内部 三级节点图）」面板 + 地图设计器 已删除；
//     大世界（节点网图）现在只有 地图服务 在跑，面板等"大世界表现法"定了再做。
public sealed class 面板管理器 : MonoBehaviour
{
    public static 面板管理器 实例 { get; private set; }

    // —— 各面板引用（Inspector 拖入；未接线的面板在路由时跳过）——
    [SerializeField] private 主菜单面板 主菜单;
    [SerializeField] private 角色创建面板 角色创建;
    [SerializeField] private 对话面板 对话;        // 短事件正文+选项（保留改造）
    [SerializeField] private 战斗沙盒面板 战斗;    // 即时制战斗棋盘面板（场景手动搭建）
    [SerializeField] private 房间面板 房间;        // 房间层（程序化网格探索：中空大房间 + 容器 + 敌人）
    [SerializeField] private 区域面板 区域;        // 区域层（一屏网格：地块制摆若干建筑 + 街道，走到楼门口进楼）
    [SerializeField] private 大世界面板 大世界;    // 大世界层（100×100 格子网格：区域副本坐在它上面；未接线则运行时自动建）
    [SerializeField] private 角色面板 角色;        // 属性/装备（改造中）

    // 末日新建面板（阶段 B 逐个补，先声明引用位）
    [SerializeField] private 面板基类 营地;        // 营地面板（待建）
    [SerializeField] private 持有面板 背包;    // 持有面板（装备区 + 装具区 + 右区 子节点；F1 打开/返回）
    [SerializeField] private 面板基类 交易;        // 交易面板（待建）
    [SerializeField] private 面板基类 任务;        // 委托面板（待建）

    [SerializeField] private GameObject 按钮预制体;     // 动态按钮共享（列表行）
    [SerializeField] private GameObject[] 常驻UI;       // 常驻 UI（日志/侧边栏/时钟/设置）：开始流程后常驻，主菜单时收起
    [SerializeField] private GameObject HUD;             // HUD 顶栏（独立 挂 Canvas 顶层）：安全屋/持有 面板 显示；主菜单/角色创建 隐藏

    public 角色创建面板 角色创建面板引用() => 角色创建;

    // ================= 零接线兜底：面板引用位没接时，当场补一个 =================
    // 为什么要有这个：引用位没接 → 事件订阅里 显示(null) 是空操作 → 表现就是"点了完全没反应"，
    // 而且 Console 里一条相关日志都没有，很难查。这里让它在运行时自建并**打一条明确的警告**。
    private 房间面板 自动房间;
    private 区域面板 自动区域;
    private 大世界面板 自动大世界;

    private 房间面板 取房间面板()
    {
        if (房间 != null) return 房间;
        if (自动房间 == null)
        {
            自动房间 = 建面板<房间面板>("房间面板（自动建）");
            Debug.LogWarning("[面板管理器] 房间 引用位没接 —— 已自动建一个 房间面板（能玩；要美观请手动搭好并拖引用位）。");
        }
        return 自动房间;
    }

    private 区域面板 取区域面板()
    {
        if (区域 != null) return 区域;
        if (自动区域 == null)
        {
            自动区域 = 建面板<区域面板>("区域面板（自动建）");
            Debug.LogWarning("[面板管理器] 区域 引用位没接 —— 已自动建一个 区域面板（能玩；要美观请手动搭好并拖引用位）。");
        }
        return 自动区域;
    }

    private 大世界面板 取大世界面板()
    {
        if (大世界 != null) return 大世界;
        if (自动大世界 == null)
        {
            自动大世界 = 建面板<大世界面板>("大世界面板（自动建）");
            Debug.LogWarning("[面板管理器] 大世界 引用位没接 —— 已自动建一个 大世界面板（能玩；要美观请手动搭好并拖引用位）。");
        }
        return 自动大世界;
    }

    // 在**本管理器所在的那个 Canvas 下**新建一个铺满的面板（和手动搭的面板同级）
    private T 建面板<T>(string 名) where T : 面板基类
    {
        var 父 = transform.parent != null ? transform.parent : transform;
        var 物体 = new GameObject(名, typeof(RectTransform));
        物体.transform.SetParent(父, false);
        var 矩形 = (RectTransform)物体.transform;
        矩形.anchorMin = Vector2.zero;
        矩形.anchorMax = Vector2.one;
        矩形.offsetMin = Vector2.zero;
        矩形.offsetMax = Vector2.zero;
        var 面板 = 物体.AddComponent<T>();
        可切换面板.Add(面板);
        物体.SetActive(false);
        return 面板;
    }

    // 注：原有一个「功能面板注册表」（功能标识 → 面板，配 注册功能面板/已注册功能 两个 API）——
    // v51 刀7e 随**设施子系统整体退役**删掉：它唯一的输入源是 `打开功能面板事件`，而那个事件**从未被发布**，
    // 注册 API 也**从未被任何系统调用**（设施功能面板基类 更是 0 子类）。

    private readonly List<面板基类> 可切换面板 = new List<面板基类>();

    // 面板切换追踪：全局面板返回用
    public 面板基类 当前显示面板 { get; private set; }
    public 面板基类 上一个面板 { get; private set; }

    private bool _返回意图;

    void Awake()
    {
        实例 = this;
        面板基类.按钮预制体 = 按钮预制体;

        GameBootstrap.装配();

        收集可切换面板();
    }

    // 收集全部可切换面板（含未接线 null 过滤）
    private void 收集可切换面板()
    {
        可切换面板.Clear();
        var 全部 = new 面板基类[] { 主菜单, 角色创建, 对话, 战斗, 大世界, 房间, 区域, 角色, 营地, 背包, 交易, 任务 };
        foreach (var 面板 in 全部)
            if (面板 != null) 可切换面板.Add(面板);
    }

    void Start()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<打开对话事件>(e => 显示(对话, e));
        事件.订阅<显示剧情事件>(e => 显示(对话, e));   // 短事件 → 对话面板（保留改造）
        事件.订阅<打开结局事件>(e => 显示(对话, e));
        事件.订阅<打开战斗事件>(e => 显示(战斗, e));   // 即时制战斗棋盘面板（布阵走 战斗开始事件）
        事件.订阅<打开房间事件>(e => 显示(取房间面板(), e));   // 房间层：进入房间时切面板
        事件.订阅<打开区域事件>(_ => 显示(取区域面板()));       // 区域层：进了一片区域时切面板
        事件.订阅<回到房间事件>(_ => 显示(房间));       // 房间里的战斗结算完 → 切回房间面板（战斗面板会随之隐藏）
        事件.订阅<打开角色面板事件>(_ => 显示(角色));
        事件.订阅<打开任务面板事件>(_ => 显示(任务));
        事件.订阅<打开营地事件>(_ => 显示(营地));   // 安全屋面板（返回营地/进入营地）
        // —— 大世界层（100×100 格子网格）——
        // 大世界探索服务 生成完世界后发 打开大地图事件 → 这里切到大世界面板。
        // 引用位没接也能跑：取大世界面板() 有零接线兜底（运行时自建 + 打一条警告）。
        事件.订阅<打开大地图事件>(e => 显示(取大世界面板(), e));
        事件.订阅<回到大世界事件>(_ => 显示(取大世界面板()));   // 大世界里的遭遇战结算完 → 切回大世界面板（战斗面板随之隐藏）

        // 初始只显示主菜单；常驻 UI 收起
        foreach (var 面板 in 可切换面板)
            if (面板 != null && 面板 != 主菜单) 面板.隐藏面板();
        主菜单?.显示面板();
        当前显示面板 = 主菜单;
        foreach (var ui in 常驻UI) if (ui != null) ui.SetActive(false);
        if (HUD != null) HUD.SetActive(false);   // 主菜单：HUD 隐藏
        ServiceRegistry.Get<EventBus>()?.发布(new 面板切换事件(主菜单));
    }

    // 显示目标面板，隐藏其它可切换面板；上下文 传给面板的 刷新(上下文)
    public void 显示(面板基类 目标, object 上下文 = null)
    {
        if (目标 == null) return;
        if (目标 == 当前显示面板)
        {
            目标.显示面板(上下文);
            return;
        }
        bool 当前侧边 = 当前显示面板 != null && 当前显示面板.侧边式面板;
        bool 目标侧边 = 目标.侧边式面板;
        bool 上下互切 = 目标侧边 && 当前侧边;
        // 战斗这类"瞬时面板"不占"上一个面板"：从它切走时保留更早那一层（地图/探索），
        // 否则打完战斗回到房间后，房间的"返回上一面板"会指回战斗结算面板
        bool 当前瞬时 = 当前显示面板 != null && 当前显示面板.瞬时面板;
        if (!上下互切 && !当前瞬时) 上一个面板 = 当前显示面板;
        bool 返回方向 = _返回意图;
        _返回意图 = false;
        // HUD 调控：HUD 顶栏 在 主菜单 与 角色创建 时 收起（未开档/创建角色 无 HUD）；
        // 安全屋面板/持有面板 等 游戏内 面板 显示 HUD。
        foreach (var ui in 常驻UI) if (ui != null) ui.SetActive(目标 != null && 目标 != 主菜单 && 目标 != 角色创建);
        if (HUD != null) HUD.SetActive(目标 != null && 目标 != 主菜单 && 目标 != 角色创建);
        foreach (var 面板 in 可切换面板)
            if (面板 != null && 面板 != 目标)
            {
                if (面板 == 当前显示面板) 面板.隐藏面板(上下互切, 返回方向);
                else 面板.gameObject.SetActive(false);
            }
        目标?.显示面板(上下文, 上下互切, 返回方向);
        当前显示面板 = 目标;
        ServiceRegistry.Get<EventBus>()?.发布(new 面板切换事件(目标));
    }

    // 全局面板返回：回到上个面板；无上一面板时兜底回主菜单
    public void 返回上一面板()
    {
        _返回意图 = true;
        var 目标 = 上一个面板 ?? 主菜单;
        显示(目标);
    }

    // 回主菜单
    public void 回主菜单()
    {
        _返回意图 = true;
        显示(主菜单);
    }

    // 显示指定面板（供注册表路由后的目标面板显示）
    public void 显示面板类型<T>(object 上下文 = null) where T : 面板基类
    {
        foreach (var 面板 in 可切换面板)
            if (面板 is T) { 显示(面板, 上下文); return; }
    }
}
