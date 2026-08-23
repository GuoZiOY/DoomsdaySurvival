using TMPro;
using UnityEngine;
using DG.Tweening;

// HUD 面板：常驻顶栏，订阅玩家状态事件实时刷新（不进面板切换，始终显示）。
// 形态：纯文字。生命/魔力/精力/经验/金币/时间/地点 各设一个 TMP 文本。
// 接线：Inspector 把 HUD条 下对应文字拖进字段；未接线的项自动跳过。
public sealed class HUD面板 : 面板基类
{
    [SerializeField] private TMP_Text 生命;   // 保留原字段名（场景已接线；改名会丢失引用）
    [SerializeField] private TMP_Text 魔力;
    [SerializeField] private TMP_Text 精力;
    [SerializeField] private TMP_Text 经验;   // exp 当前/所需（含 等级）；新增字段需额外接线
    [SerializeField] private TMP_Text 金币;
    [SerializeField] private TMP_Text 时间;
    [SerializeField] private TMP_Text 地点;

    void Awake()
    {
        // HUD 常驻顶栏：不在 面板管理器 的可切换列表，始终显示
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<生命变化事件>(e => { 设文本(生命, 着(游戏主题.危险色值, $"生命 {e.当前}/{e.最大}")); 脉冲(生命); });
        事件.订阅<魔力变化事件>(e => { 设文本(魔力, 着(游戏主题.魔攻色值, $"魔 {e.当前}/{e.最大}")); 脉冲(魔力); });
        事件.订阅<精力变化事件>(e => { 设文本(精力, 着(游戏主题.成功色值, $"精力 {e.当前}/{e.最大}")); 脉冲(精力); });
        // 经验仅在升级瞬间脉冲（战斗/刷怪高频推进时不打扰），其余更新只改文本
        事件.订阅<经验变化事件>(e => { 设文本(经验, 着(游戏主题.高亮色值, $"Lv.{e.等级}  {e.当前经验}/{e.升级所需}exp")); if (e.升级了) 脉冲(经验); });
        事件.订阅<金币变化事件>(e => { 设文本(金币, 着(游戏主题.金色色值, 货币工具.文本(e.当前))); 脉冲(金币); });
        事件.订阅<时间变化事件>(e => 设文本(时间, 着(游戏主题.时间戳色值, 时间文本(e.游戏分钟数))));
        事件.订阅<地图位置事件>(e => 设文本(地点, 着(游戏主题.暗淡色值, 地点名(e))));

        // 兜底：战斗/结算等场景常不发布三层状态事件，切回面板或战斗结束按玩家档案重刷
        事件.订阅<战斗结束事件>(_ => 刷新三层());
        事件.订阅<面板切换事件>(_ => 刷新三层());
    }

    // 按玩家档案重写生命/魔力/精力/经验/金币（兜底刷新，保证数值始终与档案一致）
    private void 刷新三层()
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null) return;
        设文本(生命, 着(游戏主题.危险色值, $"生命 {玩家.生命}/{玩家.最大生命}"));
        设文本(魔力, 着(游戏主题.魔攻色值, $"魔 {玩家.魔力}/{玩家.最大魔力}"));
        设文本(精力, 着(游戏主题.成功色值, $"精力 {玩家.精力}/{玩家.最大精力}"));
        设文本(经验, 着(游戏主题.高亮色值, $"Lv.{玩家.等级}  {玩家.经验}/{玩家.升级所需经验}exp"));
        设文本(金币, 着(游戏主题.金色色值, 货币工具.文本(玩家.铜币)));
        设文本(时间, 着(游戏主题.时间戳色值, 时间文本(玩家.游戏分钟数)));
    }

    protected override void 刷新(object 上下文) { }

    // 富文本着色（配合 TMP 富文本；文本组件的 rich text 需开启，默认即开）
    private static string 着(string 色, string 正文) => $"<color={色}>{正文}</color>";

    // 数值变化脉冲：文本"萌"一下（punch 缩放），让"数值变了"本身有反馈。
    // SetUpdate(true) 不受暂停影响；SetLink 物体销毁自动 kill。
    private void 脉冲(TMP_Text 文本)
    {
        if (文本 == null) return;
        var 目标 = 文本.transform;
        目标.DOKill();                                  // 打断上一次脉冲，保证起点一致
        目标.localScale = Vector3.one;                  // 回到基线再 punch（HUD 文本基线为 1）
        目标.DOPunchScale(Vector3.one * 0.045f, 0.2f, 3, 0.4f)
            .SetUpdate(true)
            .SetLink(文本.gameObject);
    }

    private static string 时间文本(float 分钟)
    {
        int 分钟整 = (int)分钟;
        return $"{分钟整 / 60 % 24:00}:{分钟整 % 60:00}";
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