using System.Collections.Generic;
using UnityEngine;

// 面板管理器 = 导航路由器：订阅导航事件，决定"哪个事件 → 显示哪个面板"。
// 三级导航：大地图 → 小地图 → 设施内部图（对话/功能面板）。
public sealed class 面板管理器 : MonoBehaviour
{
    public static 面板管理器 实例 { get; private set; }

    // —— 各面板引用（Inspector 拖入）——
    [SerializeField] private 主菜单面板 主菜单;
    [SerializeField] private 主视窗面板 主视窗;
    [SerializeField] private 对话面板 对话;
    [SerializeField] private 大地图面板 大地图;
    [SerializeField] private 小地图面板 小地图;
    [SerializeField] private 节点内部面板 节点内部;
    [SerializeField] private 买卖面板 买卖;
    [SerializeField] private 训练面板 训练;
    [SerializeField] private 任务面板 任务;
    [SerializeField] private 教学面板 教学;
    [SerializeField] private 恢复面板 恢复;
    [SerializeField] private 睡觉面板 睡觉;
    [SerializeField] private 角色面板 角色;

    [SerializeField] private GameObject 按钮预制体;     // 动态按钮共享（列表行）
    [SerializeField] private GameObject 地图节点预制体; // 地图节点按钮（节点图专用，可选；不设则用 按钮预制体）

    private readonly List<面板基类> 可切换面板 = new List<面板基类>();

    // 面板切换追踪：角色面板等全局面板返回用
    public 面板基类 当前显示面板 { get; private set; }
    public 面板基类 上一个面板 { get; private set; }

    void Awake()
    {
        实例 = this;
        面板基类.按钮预制体 = 按钮预制体;
        地图渲染.节点预制体 = 地图节点预制体;

        // 先确保核心服务已装配（幂等），否则路由拿不到 EventBus
        GameBootstrap.装配();

        可切换面板.AddRange(new 面板基类[] { 主菜单, 主视窗, 对话, 大地图, 小地图, 节点内部, 买卖, 训练, 任务, 教学, 恢复, 睡觉, 角色 });
    }

    void Start()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<打开设施事件>(路由设施);
        事件.订阅<打开节点内部事件>(e => 显示(节点内部, e));
        事件.订阅<打开功能面板事件>(路由功能);
        事件.订阅<打开对话事件>(e => 显示(对话, e));
        事件.订阅<显示剧情事件>(e => 显示(对话, e));   // 剧情从主视窗剥离到对话面板
        事件.订阅<打开大地图事件>(_ => 显示(大地图));
        事件.订阅<打开小地图事件>(e => 显示(小地图, e));
        事件.订阅<打开结局事件>(_ => 显示(主视窗, new 打开结局事件()));
        事件.订阅<打开战斗事件>(e => 显示(主视窗, e));
        事件.订阅<探索显示事件>(e => 显示(主视窗, e));
        事件.订阅<打开角色面板事件>(_ => 显示(角色));

        // 初始只显示主菜单
        foreach (var 面板 in 可切换面板)
            if (面板 != null && 面板 != 主菜单) 面板.隐藏面板();
        主菜单?.显示面板();
        当前显示面板 = 主菜单;
    }

    // 显示目标面板，隐藏其它可切换面板；上下文 传给面板的 刷新(上下文)
    public void 显示(面板基类 目标, object 上下文 = null)
    {
        if (目标 == null) return;
        if (目标 != 当前显示面板) 上一个面板 = 当前显示面板;
        foreach (var 面板 in 可切换面板)
            if (面板 != null && 面板 != 目标) 面板.隐藏面板();
        目标?.显示面板(上下文);
        当前显示面板 = 目标;
    }

    // 全局面板（角色面板等）返回：回到上个面板
    public void 返回上一面板()
    {
        if (上一个面板 != null) 显示(上一个面板);
    }

    // 设施事件（故事/旧入口）→ 找当前城镇的设施节点 → 节点内部
    private void 路由设施(打开设施事件 e)
    {
        var 节点 = ServiceRegistry.Get<地图服务>().找设施节点(e.设施标识);
        if (节点 != null) 显示(节点内部, new 打开节点内部事件(节点, e.返回节点));
        else Debug.LogWarning($"[面板管理器] 当前城镇找不到设施节点: {e.设施标识}");
    }

    // 功能事件 → 对应功能面板（节点内部功能物触发）
    private void 路由功能(打开功能面板事件 e)
    {
        面板基类 目标 = null;
        switch (e.功能标识)
        {
            case "买卖": 目标 = 买卖; break;
            case "训练": 目标 = 训练; break;
            case "任务": 目标 = 任务; break;
            case "教学": 目标 = 教学; break;
            case "恢复": 目标 = 恢复; break;
            case "睡觉": 目标 = 睡觉; break;
            default: Debug.LogWarning($"[面板管理器] 未路由的功能: {e.功能标识}"); return;
        }
        显示(目标, new 设施打开上下文(e.逻辑, e.节点, e.返回节点));
    }
}
