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
    public string 当前大节点 { get; private set; } = "灰烬镇";
    public string 当前小节点 { get; private set; } = "";

    public 地图服务(EventBus 事件, DataService 数据, PlayerService 玩家, 探索服务 探索, DialogueService 对话)
    {
        this.事件 = 事件; this.数据 = 数据; this.玩家 = 玩家; this.探索 = 探索; this.对话 = 对话;
    }

    // 打开大地图：玩家置于指定大节点（序章结束默认灰烬镇）
    public void 打开大地图(string 大节点 = "灰烬镇")
    {
        所在模式 = 地图模式.大地图;
        当前大节点 = 大节点;
        当前小节点 = "";
        事件.发布(new 地图位置事件(所在模式, 当前大节点, ""));
        事件.发布(new 打开大地图事件());
    }

    // 大地图逐步移动：只能到相邻节点；到达后按类型进城/入荒野
    public void 移动(string 目标)
    {
        // 点击当前所在节点：城镇=进城（小地图）；荒野=原地（无动作）
        if (目标 == 当前大节点)
        {
            if (数据.地图.TryGetValue(目标, out var 原地) && 原地.类型 == "城镇") 进入城镇(目标);
            return;
        }
        if (!数据.地图.TryGetValue(当前大节点, out var 当前) || 当前.连接 == null) return;
        if (Array.IndexOf(当前.连接, 目标) < 0) { 事件.发布(new 日志事件(日志类型.系统, "那里无法直接到达。")); return; }
        if (!数据.地图.TryGetValue(目标, out var 目标地点)) return;

        // 解锁物品门禁（如黑石山需羊皮纸）
        if (!string.IsNullOrEmpty(目标地点.解锁物品) && !玩家.档案.持有物品(目标地点.解锁物品))
        { 事件.发布(new 日志事件(日志类型.系统, $"你还不知道如何前往{目标地点.名称}……")); return; }

        当前大节点 = 目标;
        事件.发布(new 地图位置事件(所在模式, 当前大节点, ""));
        // 低概率移动事件（遇敌/NPC事件）在 M-D 完善

        // 到达处理：城镇→小地图；荒野→野外面板
        if (目标地点.类型 == "城镇") 进入城镇(目标);
        else if (目标地点.类型 == "荒野") 进入荒野(目标地点);
    }

    // 进入城镇小地图：玩家置于入口节点
    public void 进入城镇(string 城镇标识)
    {
        if (!数据.地图.TryGetValue(城镇标识, out var 地点)) return;
        所在模式 = 地图模式.城镇;
        当前大节点 = 城镇标识;
        当前小节点 = 地点.入口节点 ?? "";
        事件.发布(new 地图位置事件(所在模式, 当前大节点, 当前小节点));
        事件.发布(new 打开小地图事件(城镇标识, 当前小节点));
    }

    // 回到城镇小地图（设施/剧情返回用）；若当前不在城镇模式（如刚从野外回来）则先进入城镇
    public void 回到城镇地图()
    {
        if (所在模式 != 地图模式.城镇)
        {
            if (!数据.地图.TryGetValue(当前大节点, out var 地点) || 地点.类型 != "城镇") return;
            进入城镇(当前大节点);
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

    // 进入荒野：切野外面板并进入区域探索（探索服务 发 探索显示事件 驱动 UI）
    private void 进入荒野(地图地点 地点)
    {
        所在模式 = 地图模式.野外;
        string 区域标识 = string.IsNullOrEmpty(地点.区域) ? 地点.标识 : 地点.区域;
        事件.发布(new 地图位置事件(所在模式, 当前大节点, ""));
        事件.发布(new 打开野外面板事件(区域标识, 当前大节点));
        探索.进入区域(区域标识, 当前大节点);
    }

    // 城镇内逐步移动：只能到相邻设施/空地节点；到设施节点打开设施面板
    public void 小地图移动(string 目标)
    {
        if (所在模式 != 地图模式.城镇) return;
        if (!数据.地图.TryGetValue(当前大节点, out var 地点) || 地点.小地图 == null) return;
        var 当前 = 找小节点(地点, 当前小节点);
        if (当前 == null || 当前.连接 == null || Array.IndexOf(当前.连接, 目标) < 0)
        { 事件.发布(new 日志事件(日志类型.系统, "那里无法直接到达。")); return; }
        var 目标节点 = 找小节点(地点, 目标);
        if (目标节点 == null) return;

        当前小节点 = 目标;
        事件.发布(new 地图位置事件(所在模式, 当前大节点, 当前小节点));
        // 低概率镇内事件（NPC事件）在 M-D 完善

        // 到达处理：设施节点 → 打开设施内部图（三级导航，返回「城镇:标识」）；剧情节点 → 进入剧情；空地 → 无事
        if (目标节点.类型 == "设施" && !string.IsNullOrEmpty(目标节点.设施))
            事件.发布(new 打开设施内部事件(目标节点.设施, $"城镇:{当前大节点}"));
        else if (目标节点.类型 == "剧情" && !string.IsNullOrEmpty(目标节点.目标))
            对话.进入节点(目标节点.目标);
    }

    private 地图节点 找小节点(地图地点 地点, string 标识)
    {
        if (地点.小地图 == null) return null;
        foreach (var 节点 in 地点.小地图) if (节点.标识 == 标识) return 节点;
        return null;
    }
}
