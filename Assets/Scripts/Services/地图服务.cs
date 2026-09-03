using System;
using UnityEngine;

// 地图服务：大地图/城镇小地图 的节点逐步移动与到达处理。
// 大地图=城镇/荒野大节点；城镇=设施节点网络；荒野=闯关式野外面板（由 探索服务 承担）。
public sealed class 地图服务
{
    private readonly EventBus 事件;
    private readonly DataService 数据;
    private readonly PlayerService 玩家;
    private readonly 探索服务 探索;
    private readonly DialogueService 对话;

    public 地图模式 所在模式 { get; private set; } = 地图模式.大地图;
    public string 当前大节点 { get; private set; } = "营地";
    public string 当前小节点 { get; private set; } = "";

    public 地图服务(EventBus 事件, DataService 数据, PlayerService 玩家, 探索服务 探索, DialogueService 对话)
    {
        this.事件 = 事件; this.数据 = 数据; this.玩家 = 玩家; this.探索 = 探索; this.对话 = 对话;
    }

    // 打开大地图：玩家置于指定大节点（末日默认营地）
    public void 打开大地图(string 大节点 = "营地")
    {
        所在模式 = 地图模式.大地图;
        当前大节点 = 大节点;
        当前小节点 = "";
        玩家.档案.当前大节点 = 大节点;
        事件.发布(new 地图位置事件(所在模式, 当前大节点, ""));
        事件.发布(new 打开大地图事件());
    }

    // 大地图逐步移动：只能到相邻节点（只改位置，不进入；进入靠 双击 → 进入当前）
    public void 移动(string 目标)
    {
        if (目标 == 当前大节点) return;   // 已在该节点
        if (!数据.地图.TryGetValue(当前大节点, out var 当前) || 当前.连接 == null) return;
        if (Array.IndexOf(当前.连接, 目标) < 0) { 音效管理器.实例?.播放失败(); 事件.发布(new 日志事件(日志类型.系统, "那里无法直接到达。")); return; }
        if (!数据.地图.TryGetValue(目标, out var 目标地点)) return;

        // 解锁物品门禁（如黑石山需羊皮纸）
        if (!string.IsNullOrEmpty(目标地点.解锁物品) && !玩家.档案.持有物品(目标地点.解锁物品))
        { 事件.发布(new 日志事件(日志类型.系统, $"你还不知道如何前往{目标地点.名称}……")); return; }

        // 解锁阶段门禁（如 风谷村/大草原 需主线推进到某阶段）
        if (!string.IsNullOrEmpty(目标地点.解锁阶段) && !主线阶段工具.达到(玩家.档案.主线阶段, 目标地点.解锁阶段))
        {
            string 提示 = string.IsNullOrEmpty(目标地点.解锁提示) ? $"你还不知道如何前往{目标地点.名称}……" : 目标地点.解锁提示;
            事件.发布(new 日志事件(日志类型.系统, 提示));
            return;
        }

        当前大节点 = 目标;
        事件.发布(new 地图位置事件(所在模式, 当前大节点, ""));
        // 低概率移动事件（遇敌/NPC事件）在 M-D 完善
    }

    // 进入当前所在大节点：城镇→小地图；荒野→探索；营地→营地面板（暂日志）；其它类型防御性失败反馈
    public void 进入当前()
    {
        if (!数据.地图.TryGetValue(当前大节点, out var 地点)) return;
        if (地点.类型 == "城镇") 进入城镇(当前大节点);
        else if (地点.类型 == "荒野") 进入荒野(地点);
        else if (地点.类型 == "营地") { 事件.发布(new 日志事件(日志类型.系统, "你回到了安全屋。")); 返回营地(); }
        else { 音效管理器.实例?.播放失败(); 事件.发布(new 日志事件(日志类型.系统, "这里没什么可进入的。")); }
    }

    // 返回营地：末日安全屋（睡觉/仓库/制作/医疗/收音机/交易）——打开 安全屋面板（营地面板）
    public void 返回营地()
    {
        档案.睡觉();
        事件.发布(new 生命变化事件(档案.生命, 档案.最大生命, 档案.生命));
        事件.发布(new 精力变化事件(档案.行动点, 档案.最大行动点, 档案.行动点));
        foreach (伤病类型 类型 in System.Enum.GetValues(typeof(伤病类型)))
            事件.发布(new 伤病变化事件(类型, 档案.伤病值(类型), 0));
        事件.发布(new 日志事件(日志类型.生存, "你在安全屋睡了一觉。天亮了，新的一天。"));
        事件.发布(new 打开营地事件());   // 打开 安全屋面板
    }

    private 玩家档案 档案 => 玩家.档案;

    // 进入城镇小地图：玩家置于入口节点；若命中区域剧情路由则自动触发剧情（不进入小地图）
    // 这是「从大地图进入」的外部入口，会查路由；剧情内的 地图:小地图 走 回到城镇地图（跳过路由）
    public void 进入城镇(string 城镇标识)
    {
        if (!数据.地图.TryGetValue(城镇标识, out var 地点)) return;
        if (自动触发剧情(城镇标识)) return;
        直接进入城镇(城镇标识);
    }

    // 直接进入城镇小地图（不查区域剧情路由：剧情内返回小地图用，避免剧情→地图:小地图→再触发剧情 的死循环）
    private void 直接进入城镇(string 城镇标识)
    {
        if (!数据.地图.TryGetValue(城镇标识, out var 地点)) return;
        所在模式 = 地图模式.城镇;
        当前大节点 = 城镇标识;
        当前小节点 = 地点.入口节点 ?? "";
        事件.发布(new 地图位置事件(所在模式, 当前大节点, 当前小节点));
        事件.发布(new 打开小地图事件(城镇标识, 当前小节点));
    }

    // 回到城镇小地图（设施/剧情返回用）；若当前不在城镇模式（如刚从野外回来）则先进入城镇（跳过路由）
    public void 回到城镇地图()
    {
        if (所在模式 != 地图模式.城镇)
        {
            if (!数据.地图.TryGetValue(当前大节点, out var 地点) || 地点.类型 != "城镇") return;
            直接进入城镇(当前大节点);
            return;
        }
        事件.发布(new 打开小地图事件(当前大节点, 当前小节点));
    }

    // 离开城镇 → 回大地图（玩家在该城镇大节点）
    public void 离开城镇()
    {
        所在模式 = 地图模式.大地图;
        当前小节点 = "";
        事件.发布(new 地图位置事件(所在模式, 当前大节点, ""));
        事件.发布(new 打开大地图事件());
    }

    // 进入荒野：先查区域剧情路由（自动触发主线剧情），未命中才进入探索模式
    private void 进入荒野(地图地点 地点)
    {
        string 区域标识 = string.IsNullOrEmpty(地点.区域) ? 地点.标识 : 地点.区域;
        if (自动触发剧情(地点.标识)) return;
        所在模式 = 地图模式.野外;
        事件.发布(new 地图位置事件(所在模式, 当前大节点, ""));
        事件.发布(new 打开野外面板事件(区域标识, 当前大节点));
        探索.进入区域(区域标识, 当前大节点);
    }

    // 区域剧情路由：当前主线阶段 + 地点 → 命中即自动进入剧情节点（阶段前进后不再命中，防重复）
    private bool 自动触发剧情(string 地点标识)
    {
        foreach (var 路由 in 数据.区域剧情)
        {
            if (路由.区域 != 地点标识 || 玩家.档案.主线阶段 != 路由.阶段) continue;
            if (!string.IsNullOrEmpty(路由.需要物品) && !玩家.档案.持有物品(路由.需要物品)) continue;
            if (!string.IsNullOrEmpty(路由.需要任务) && !ServiceRegistry.Get<QuestService>().任务已完成(路由.需要任务)) continue;
            对话.进入节点(路由.节点);
            return true;
        }
        return false;
    }

    // 城镇内逐步移动：只能到相邻节点（只改位置，不自动进入内部；进入靠 双击 → 进入当前小节点）
    public void 小地图移动(string 目标)
    {
        if (所在模式 != 地图模式.城镇) return;
        if (目标 == 当前小节点) return;   // 已在该节点
        if (!数据.地图.TryGetValue(当前大节点, out var 地点) || 地点.小地图 == null) return;
        var 当前 = 找小节点(地点, 当前小节点);
        if (当前 == null || 当前.连接 == null || Array.IndexOf(当前.连接, 目标) < 0)
        { 音效管理器.实例?.播放失败(); 事件.发布(new 日志事件(日志类型.系统, "那里无法直接到达。")); return; }
        var 目标节点 = 找小节点(地点, 目标);
        if (目标节点 == null) return;

        当前小节点 = 目标;
        事件.发布(new 地图位置事件(所在模式, 当前大节点, 当前小节点));
        // 低概率镇内事件（NPC事件）在 M-D 完善
    }

    // 进入当前小节点：入口→离开城镇；有「内部」→打开节点内部图；否则空节点无法进入（失败反馈）
    public void 进入当前小节点()
    {
        if (所在模式 != 地图模式.城镇) return;
        if (!数据.地图.TryGetValue(当前大节点, out var 地点) || 地点.小地图 == null) return;
        var 当前 = 找小节点(地点, 当前小节点);
        if (当前 == null) return;
        if (当前.类型 == "入口") { 离开城镇(); return; }
        if (当前.内部 != null && 当前.内部.Length > 0)
        {
            事件.发布(new 打开节点内部事件(当前, $"城镇:{当前大节点}"));
            return;
        }
        // 空节点：无入口无内部 → 无法进入（快速双击这类节点时给失败反馈，否则会静默无提示）
        音效管理器.实例?.播放失败();
        事件.发布(new 日志事件(日志类型.系统, "这里没有什么可进入的。"));
    }

    private 地图节点 找小节点(地图地点 地点, string 标识)
    {
        if (地点.小地图 == null) return null;
        foreach (var 节点 in 地点.小地图) if (节点.标识 == 标识) return 节点;
        return null;
    }

    // 按设施标识找当前城镇里的节点（故事 设施: 入口 / 返回内部用）
    public 地图节点 找设施节点(string 设施标识)
    {
        if (!数据.地图.TryGetValue(当前大节点, out var 地点) || 地点.小地图 == null) return null;
        foreach (var 节点 in 地点.小地图)
            if (节点.设施 == 设施标识) return 节点;
        return null;
    }
}
