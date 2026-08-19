using TMPro;
using UnityEngine;

// HUD 面板：常驻顶栏，订阅玩家状态事件实时刷新（不进面板切换，始终显示）。
// 接线：Inspector 把 HUD条 下的 生命/魔力/金币/时间/地点 拖进对应字段。
public sealed class HUD面板 : 面板基类
{
    [SerializeField] private TMP_Text 生命;
    [SerializeField] private TMP_Text 魔力;
    [SerializeField] private TMP_Text 精力;
    [SerializeField] private TMP_Text 金币;
    [SerializeField] private TMP_Text 时间;
    [SerializeField] private TMP_Text 地点;

    void Awake()
    {
        // HUD 常驻顶栏：不在 面板管理器 的可切换列表，始终显示
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<生命变化事件>(e => 设文本(生命, $"生命 {e.当前}/{e.最大}"));
        事件.订阅<魔力变化事件>(e => 设文本(魔力, $"魔 {e.当前}/{e.最大}"));
        事件.订阅<精力变化事件>(e => 设文本(精力, $"精力 {e.当前}/{e.最大}"));
        事件.订阅<金币变化事件>(e => 设文本(金币, 货币工具.文本(e.当前)));
        事件.订阅<地图位置事件>(e => 设文本(地点, 地点名(e)));

        // 初始值（装配期事件可能已发出）
        var 玩家 = ServiceRegistry.Get<PlayerService>().档案;
        设文本(生命, $"生命 {玩家.生命}/{玩家.最大生命}");
        设文本(魔力, $"魔 {玩家.魔力}/{玩家.最大魔力}");
        设文本(精力, $"精力 {玩家.精力}/{玩家.最大精力}");
        设文本(金币, 货币工具.文本(玩家.铜币));
        设文本(时间, 时间文本(玩家));
    }

    protected override void 刷新(object 上下文) { }

    private static string 时间文本(玩家档案 玩家)
    {
        int 分钟 = (int)玩家.游戏分钟数;
        return $"{分钟 / 60 % 24:00}:{分钟 % 60:00}";
    }

    // 按地图模式拼当前地点名
    private string 地点名(地图位置事件 e)
    {
        var 数据 = ServiceRegistry.Get<DataService>();
        string 大名 = 数据.地图.TryGetValue(e.大节点, out var 大) ? 大.名称 : e.大节点;
        switch (e.所在模式)
        {
            case 地图模式.城镇:
                string 小名 = e.小节点;
                if (数据.地图.TryGetValue(e.大节点, out var 镇) && 镇.小地图 != null)
                    foreach (var 节点 in 镇.小地图) if (节点.标识 == e.小节点) { 小名 = 节点.名称; break; }
                return $"{大名}·{小名}";
            case 地图模式.野外: return $"{大名}（野外）";
            default: return 大名;
        }
    }
}
