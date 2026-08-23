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
    [SerializeField] private 战斗面板 战斗;
    [SerializeField] private 大地图面板 大地图;
    [SerializeField] private 小地图面板 小地图;
    [SerializeField] private 节点内部面板 节点内部;
    [SerializeField] private 买卖面板 买卖;
    [SerializeField] private 训练面板 训练;
    [SerializeField] private 日常任务面板 日常任务;       // 悬赏板设施 → 日常任务（悬赏）面板
    [SerializeField] private 系统任务面板 任务系统;       // 侧边栏「任务」→ 主线/支线/日常面板
    [SerializeField] private 教学面板 教学;
    [SerializeField] private 恢复面板 恢复;
    [SerializeField] private 睡觉面板 睡觉;
    [SerializeField] private 制作面板 制作;
    [SerializeField] private 角色面板 角色;
    [SerializeField] private 探索面板 探索;   // 深度分层区域探索

    [SerializeField] private GameObject 按钮预制体;     // 动态按钮共享（列表行）
    [SerializeField] private GameObject 地图节点预制体; // 地图节点按钮（节点图专用，可选；不设则用 按钮预制体）
    [SerializeField] private GameObject[] 常驻UI;       // 第二级常驻 UI（HUD条/日志面板/底部状态栏/游戏时钟）：开始流程后常驻，主菜单时收起

    private readonly List<面板基类> 可切换面板 = new List<面板基类>();

    // 面板切换追踪：角色面板等全局面板返回用
    public 面板基类 当前显示面板 { get; private set; }
    public 面板基类 上一个面板 { get; private set; }

    private bool _返回意图;   // 本次 显示() 是否为「返回」方向（返回上一面板 / 关闭全局面板 / 回主菜单）

    void Awake()
    {
        实例 = this;
        面板基类.按钮预制体 = 按钮预制体;
        地图渲染.节点预制体 = 地图节点预制体;

        // 先确保核心服务已装配（幂等），否则路由拿不到 EventBus
        GameBootstrap.装配();

        可切换面板.AddRange(new 面板基类[] { 主菜单, 主视窗, 对话, 战斗, 大地图, 小地图, 节点内部, 买卖, 训练, 日常任务, 任务系统, 教学, 恢复, 睡觉, 制作, 角色, 探索 });
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
        事件.订阅<打开结局事件>(e => 显示(对话, e));
        事件.订阅<打开战斗事件>(e => 显示(战斗 != null ? 战斗 : 主视窗, e));
        事件.订阅<探索显示事件>(e => 显示(探索, e));               // 探索 → 探索面板（替换原主视窗死路由）
        事件.订阅<打开探索事件>(e => 显示(探索, e));               // 剧情"探索:"/“区域:”指令 → 探索面板
        事件.订阅<打开野外面板事件>(e => 显示(探索, e));           // 地图进入荒野 → 探索面板
        事件.订阅<打开角色面板事件>(_ => 显示(角色));
        事件.订阅<打开任务面板事件>(_ => 显示(任务系统));

        // 初始只显示主菜单（Level1）：中央视窗框架、HUD/日志 等二级 UI 全部收起
        foreach (var 面板 in 可切换面板)
            if (面板 != null && 面板 != 主菜单) 面板.隐藏面板();
        主菜单?.显示面板();
        当前显示面板 = 主菜单;
        if (主视窗 != null && 主视窗.容器模式) 主视窗.gameObject.SetActive(false);
        foreach (var ui in 常驻UI) if (ui != null) ui.SetActive(false);
        ServiceRegistry.Get<EventBus>()?.发布(new 面板切换事件(主菜单));
    }

    // 显示目标面板，隐藏其它可切换面板；上下文 传给面板的 刷新(上下文)
    public void 显示(面板基类 目标, object 上下文 = null)
    {
        if (目标 == null) return;
        // 自切（目标==当前显示面板）：不播动画，直接刷新内容
        if (目标 == 当前显示面板)
        {
            面板基类.跳过下次动画 = true;
            目标.显示面板(上下文);
            return;
        }
        // 侧边↔侧边互切（如 角色↔任务）：用上下切换动画；从主界面进入保持各自的右滑
        bool 当前侧边 = 当前显示面板 != null && 当前显示面板.侧边式面板;
        bool 目标侧边 = 目标.侧边式面板;
        bool 上下互切 = 目标侧边 && 当前侧边;
        // 互切时保持「上一个面板」不变——它始终记录进入侧边面板之前的内容面板（对话/地图等），
        // 这样互切后来回取消都能回到原本的内容面板，而不是被覆盖成侧边面板自身
        if (!上下互切) 上一个面板 = 当前显示面板;
        bool 返回方向 = _返回意图;
        _返回意图 = false;
        // 先激活 主视窗框架 + 常驻UI（内容面板是其子节点：父级必须先激活，子面板 StartCoroutine 才不报 inactive）
        if (主视窗 != null && 主视窗.容器模式) 主视窗.gameObject.SetActive(目标 != 主菜单);
        foreach (var ui in 常驻UI) if (ui != null) ui.SetActive(目标 != 主菜单);
        foreach (var 面板 in 可切换面板)
            if (面板 != null && 面板 != 目标 && !(面板 is 主视窗面板))
            {
                // 只有当前显示面板才播退场动画（让位给新面板）；其余直接收起，避免动画中断残留导致面板 active 却不可见。
                // 主视窗=容器，其子面板各自显隐，容器的显隐由上面统一 SetActive 接管，跳过以免父级关闭连带子面板失效。
                if (面板 == 当前显示面板) 面板.隐藏面板(上下互切, 返回方向);
                else 面板.gameObject.SetActive(false);
            }
        目标?.显示面板(上下文, 上下互切, 返回方向);
        当前显示面板 = 目标;
        ServiceRegistry.Get<EventBus>()?.发布(new 面板切换事件(目标));
    }

    // 全局面板（角色/任务等）返回：回到上个面板；无上一面板时兜底回主视窗
    public void 返回上一面板()
    {
        _返回意图 = true;
        显示(上一个面板 ?? 主视窗);
    }

    // 全局面板关闭 = 回主视窗（真退出）。不用 返回上一面板：平级全局面板间互切时单层「上一个面板」会被覆盖成环，回不到主界面。
    public void 关闭全局面板()
    {
        _返回意图 = true;
        显示(主视窗);
    }

    // 回主菜单：主菜单归 面板管理器 管，各面板不应持有 主菜单 引用（对话结局等用）
    public void 回主菜单()
    {
        _返回意图 = true;
        显示(主菜单);
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
            case "任务": 目标 = 日常任务; break;
            case "教学": 目标 = 教学; break;
            case "恢复": 目标 = 恢复; break;
            case "睡觉": 目标 = 睡觉; break;
            case "制作": 目标 = 制作; break;
            default: Debug.LogWarning($"[面板管理器] 未路由的功能: {e.功能标识}"); return;
        }
        if (目标 == null) { Debug.LogWarning($"[面板管理器] 功能面板未在场景接线（拖入对应面板引用）: {e.功能标识}"); return; }
        显示(目标, new 设施打开上下文(e.逻辑, e.节点, e.返回节点));
    }
}
