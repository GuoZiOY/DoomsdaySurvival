using System;
using System.Collections.Generic;
using UnityEngine;

// 面板管理器 = 导航路由器：订阅导航事件，决定"哪个事件 → 显示哪个面板"。
// 末日《最后87天》清单：主菜单 / 角色创建 / 对话(短事件) / 战斗 / 探索 / 城市地图 / 营地 / 背包 / 交易 / 任务 / 角色。
// 设施功能 → 面板 由 功能面板注册表 路由（替代原硬编码 switch）。
public sealed class 面板管理器 : MonoBehaviour
{
    public static 面板管理器 实例 { get; private set; }

    // —— 各面板引用（Inspector 拖入；未接线的面板在路由时跳过）——
    [SerializeField] private 主菜单面板 主菜单;
    [SerializeField] private 角色创建面板 角色创建;
    [SerializeField] private 对话面板 对话;        // 短事件正文+选项（保留改造）
    [SerializeField] private 战斗面板 战斗;
    [SerializeField] private 探索面板 探索;
    [SerializeField] private 角色面板 角色;        // 属性/装备（改造中）

    // 末日新建面板（阶段 B 逐个补，先声明引用位）
    [SerializeField] private 面板基类 城市地图;    // 城市三级地图（待建）
    [SerializeField] private 面板基类 营地;        // 营地面板（待建）
    [SerializeField] private 面板基类 背包;        // 网格背包面板（待建）
    [SerializeField] private 面板基类 交易;        // 交易面板（待建）
    [SerializeField] private 面板基类 任务;        // 委托面板（待建）

    [SerializeField] private GameObject 按钮预制体;     // 动态按钮共享（列表行）
    [SerializeField] private GameObject 地图节点预制体; // 地图节点按钮（节点图专用）
    [SerializeField] private GameObject[] 常驻UI;       // 常驻 UI（HUD/日志/侧边栏/时钟/设置）：开始流程后常驻，主菜单时收起

    public 角色创建面板 角色创建面板引用() => 角色创建;

    // 功能面板注册表：功能标识 -> 面板（末日营地/交易/制作等入口；替代硬编码 switch）
    private readonly Dictionary<string, Func<object, 面板基类>> 功能面板注册表 = new Dictionary<string, Func<object, 面板基类>>();

    // 注册功能路由（装配时由各系统调用；返回 true = 已处理）
    public void 注册功能面板(string 功能标识, Func<object, 面板基类> 路由)
    {
        功能面板注册表[功能标识] = 路由;
    }

    public bool 已注册功能(string 功能标识) => 功能面板注册表.ContainsKey(功能标识);

    private readonly List<面板基类> 可切换面板 = new List<面板基类>();

    // 面板切换追踪：全局面板返回用
    public 面板基类 当前显示面板 { get; private set; }
    public 面板基类 上一个面板 { get; private set; }

    private bool _返回意图;

    void Awake()
    {
        实例 = this;
        面板基类.按钮预制体 = 按钮预制体;
        地图渲染.节点预制体 = 地图节点预制体;

        GameBootstrap.装配();

        收集可切换面板();
    }

    // 收集全部可切换面板（含未接线 null 过滤）
    private void 收集可切换面板()
    {
        可切换面板.Clear();
        var 全部 = new 面板基类[] { 主菜单, 角色创建, 对话, 战斗, 探索, 角色, 城市地图, 营地, 背包, 交易, 任务 };
        foreach (var 面板 in 全部)
            if (面板 != null) 可切换面板.Add(面板);
    }

    void Start()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<打开对话事件>(e => 显示(对话, e));
        事件.订阅<显示剧情事件>(e => 显示(对话, e));   // 短事件 → 对话面板（保留改造）
        事件.订阅<打开结局事件>(e => 显示(对话, e));
        事件.订阅<打开战斗事件>(e => 显示(战斗, e));
        事件.订阅<探索显示事件>(e => 显示(探索, e));
        事件.订阅<打开探索事件>(e => 显示(探索, e));
        事件.订阅<打开野外面板事件>(e => 显示(探索, e));
        事件.订阅<打开角色面板事件>(_ => 显示(角色));
        事件.订阅<打开任务面板事件>(_ => 显示(任务));
        事件.订阅<打开大地图事件>(_ => 显示(城市地图));
        // 功能面板事件 → 注册表路由（未注册则日志提示）
        事件.订阅<打开功能面板事件>(e =>
        {
            if (功能面板注册表.TryGetValue(e.功能标识, out var 路由))
                显示(路由(new 设施打开上下文(e.逻辑, e.节点, e.返回节点)));
            else
                Debug.LogWarning($"[面板管理器] 未注册功能面板: {e.功能标识}");
        });

        // 初始只显示主菜单；常驻 UI 收起
        foreach (var 面板 in 可切换面板)
            if (面板 != null && 面板 != 主菜单) 面板.隐藏面板();
        主菜单?.显示面板();
        当前显示面板 = 主菜单;
        foreach (var ui in 常驻UI) if (ui != null) ui.SetActive(false);
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
        if (!上下互切) 上一个面板 = 当前显示面板;
        bool 返回方向 = _返回意图;
        _返回意图 = false;
        foreach (var ui in 常驻UI) if (ui != null) ui.SetActive(目标 != 主菜单 && 目标 != null);
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
