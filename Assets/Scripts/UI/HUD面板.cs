using TMPro;
using UnityEngine;
using DG.Tweening;

// HUD 面板：常驻顶栏，订阅玩家状态事件实时刷新（不进面板切换，始终显示）。
// 形态：纯文字。生命/行动点/饱食/水分/经验/负重/时间/地点 各设一个 TMP 文本。
// 接线：Inspector 把 HUD条 下对应文字拖进字段；未接线的项自动跳过。
// 字段复用：魔力→行动点、精力→饱食、金币→负重（场景已接线，改名会丢失引用，故沿用字段名）
public sealed class HUD面板 : 面板基类
{
    [SerializeField] private TMP_Text 生命;
    [SerializeField] private TMP_Text 魔力;      // 显示 行动点
    [SerializeField] private TMP_Text 精力;      // 显示 饱食度
    [SerializeField] private TMP_Text 经验;
    [SerializeField] private TMP_Text 金币;      // 显示 负重/负重上限
    [SerializeField] private TMP_Text 时间;
    [SerializeField] private TMP_Text 地点;

    void Awake()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<生命变化事件>(e => { 设文本(生命, 着(游戏主题.危险色值, $"生命 {e.当前}/{e.最大}")); 脉冲(生命); });
        事件.订阅<魔力变化事件>(e => { 设文本(魔力, 着(游戏主题.魔攻色值, $"行动 {e.当前}/{e.最大}")); 脉冲(魔力); });
        事件.订阅<生存状态变化事件>(e =>
        {
            if (e.类型 == 生存状态类型.饱食度) { 设文本(精力, 着(游戏主题.成功色值, $"饱食 {e.当前}")); 脉冲(精力); }
            else if (e.类型 == 生存状态类型.水分度) { 设文本(金币, 着(游戏主题.金色色值, $"水分 {e.当前}")); 脉冲(金币); }
        });
        // 伤病警告（发烧/中毒/流血/骨折 时在日志与 HUD 时间旁提示，简化为日志）
        事件.订阅<伤病变化事件>(e =>
        {
            if (e.当前 > 30 && e.变化量 > 0)
                事件.发布(new 日志事件(日志类型.反馈坏, $"伤病加重：{伤病名(e.类型)}（{e.当前}）"));
        });
        事件.订阅<经验变化事件>(e => { 设文本(经验, 着(游戏主题.高亮色值, $"Lv.{e.等级}  {e.当前经验}/{e.升级所需}exp")); if (e.升级了) 脉冲(经验); });
        事件.订阅<时间变化事件>(e => 设文本(时间, 着(游戏主题.时间戳色值, 时间文本(e.游戏分钟数))));
        事件.订阅<地图位置事件>(e => 设文本(地点, 着(游戏主题.暗淡色值, 地点名(e))));
        事件.订阅<战斗结束事件>(_ => 刷新三层());
        事件.订阅<面板切换事件>(_ => 刷新三层());
    }

    // 按玩家档案重写（兜底刷新，保证数值始终与档案一致）
    private void 刷新三层()
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null) return;
        设文本(生命, 着(游戏主题.危险色值, $"生命 {玩家.生命}/{玩家.最大生命}"));
        设文本(魔力, 着(游戏主题.魔攻色值, $"行动 {玩家.行动点}/{玩家.最大行动点}"));
        设文本(精力, 着(游戏主题.成功色值, $"饱食 {玩家.饱食度}"));
        设文本(金币, 着(游戏主题.金色色值, $"水分 {玩家.水分度}"));
        设文本(经验, 着(游戏主题.高亮色值, $"Lv.{玩家.等级}  {玩家.经验}/{玩家.升级所需经验}exp"));
        设文本(时间, 着(游戏主题.时间戳色值, 时间文本(玩家.游戏分钟数)));
    }

    protected override void 刷新(object 上下文) { }

    private static string 着(string 色, string 正文) => $"<color={色}>{正文}</color>";

    private void 脉冲(TMP_Text 文本)
    {
        if (文本 == null) return;
        var 目标 = 文本.transform;
        目标.DOKill();
        目标.localScale = Vector3.one;
        目标.DOPunchScale(Vector3.one * 0.045f, 0.2f, 3, 0.4f)
            .SetUpdate(true)
            .SetLink(文本.gameObject);
    }

    private static string 时间文本(float 分钟)
    {
        int 分钟整 = (int)分钟;
        return $"{分钟整 / 60 % 24:00}:{分钟整 % 60:00}";
    }

    private static string 伤病名(伤病类型 类型)
    {
        switch (类型)
        {
            case 伤病类型.疲劳: return "疲劳";
            case 伤病类型.中毒: return "中毒";
            case 伤病类型.感冒: return "感冒";
            case 伤病类型.流血: return "流血";
            case 伤病类型.骨折: return "骨折";
            case 伤病类型.发烧: return "发烧";
            default: return "";
        }
    }

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
