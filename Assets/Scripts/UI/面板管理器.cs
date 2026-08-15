using System.Collections.Generic;
using UnityEngine;

// 面板管理器 = 导航路由器：唯一订阅导航事件，决定"哪个事件 → 显示哪个面板"。
// 不装面板业务逻辑（列表/购买/学习都在各面板）；各面板专注内容呈现与交互。
public sealed class 面板管理器 : MonoBehaviour
{
    public static 面板管理器 实例 { get; private set; }

    // —— 各可切换面板引用（Inspector 拖入，导航映射一目了然）——
    [SerializeField] private 主菜单面板 主菜单;
    [SerializeField] private 主视窗面板 主视窗;
    [SerializeField] private 商店面板 商店;
    [SerializeField] private 训练场面板 训练场;
    [SerializeField] private 任务板面板 任务板;
    [SerializeField] private 大地图面板 大地图;
    [SerializeField] private 小地图面板 小地图;

    [SerializeField] private GameObject 按钮预制体;     // 动态按钮共享（列表行）
    [SerializeField] private GameObject 地图节点预制体; // 地图节点按钮（节点图专用，可选；不设则用 按钮预制体）

    private readonly List<面板基类> 可切换面板 = new List<面板基类>();

    void Awake()
    {
        实例 = this;
        面板基类.按钮预制体 = 按钮预制体;
        地图渲染.节点预制体 = 地图节点预制体;

        // 先确保核心服务已装配（幂等），否则路由拿不到 EventBus
        GameBootstrap.装配();

        可切换面板.AddRange(new 面板基类[] { 主菜单, 主视窗, 商店, 训练场, 任务板, 大地图, 小地图 });
    }

    void Start()
    {
        // 订阅导航事件并路由（HUD/日志 常驻，不在路由内）
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<打开设施事件>(路由设施);
        事件.订阅<打开大地图事件>(_ => 显示(大地图));
        事件.订阅<打开小地图事件>(e => 显示(小地图, e));
        事件.订阅<打开结局事件>(_ => 显示(主视窗, new 打开结局事件()));
        事件.订阅<显示剧情事件>(e => 显示(主视窗, e));
        事件.订阅<打开战斗事件>(e => 显示(主视窗, e));
        事件.订阅<探索显示事件>(e => 显示(主视窗, e));

        // 初始只显示主菜单
        foreach (var 面板 in 可切换面板)
            if (面板 != null && 面板 != 主菜单) 面板.隐藏面板();
        主菜单?.显示面板();
    }

    // 设施事件 → 对应设施面板（返回节点随事件传入）
    private void 路由设施(打开设施事件 e)
    {
        switch (e.设施标识)
        {
            case "商店": 显示(商店, e); break;
            case "训练场": 显示(训练场, e); break;
            case "任务板": 显示(任务板, e); break;
            default: Debug.LogWarning($"[面板管理器] 未路由的设施: {e.设施标识}"); break;
        }
    }

    // 显示目标面板，隐藏其它可切换面板；上下文 传给面板的 刷新(上下文)
    public void 显示(面板基类 目标, object 上下文 = null)
    {
        foreach (var 面板 in 可切换面板)
            if (面板 != null && 面板 != 目标) 面板.隐藏面板();
        目标?.显示面板(上下文);
    }
}
