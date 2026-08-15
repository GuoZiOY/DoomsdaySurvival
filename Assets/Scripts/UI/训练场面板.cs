using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 训练场面板：技能学习的交互表现（列出全部技能，点击学习）。
public sealed class 训练场面板 : 面板基类
{
    [SerializeField] private TMP_Text 标题;
    [SerializeField] private RectTransform 技能列表;
    [SerializeField] private Button 返回按钮;
    private string 返回节点;

    void Awake()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<金币变化事件>(_ => 刷新(null));   // 学习后刷新已学状态
        返回按钮?.onClick.AddListener(() => 返回设施(返回节点));
    }

    protected override void 刷新(object 上下文)
    {
        if (上下文 is 打开设施事件 e) 返回节点 = e.返回节点;
        设文本(标题, "—— 训练场 ——");
        清空(技能列表);
        var 玩家 = ServiceRegistry.Get<PlayerService>().档案;
        var 数据 = ServiceRegistry.Get<DataService>();
        foreach (var 技能 in 数据.技能.Values)
        {
            var 标识 = 技能.标识;
            创建行(技能列表, $"{技能.名称}（{技能.描述}）  {技能.价格}金", () => 学习(玩家, 数据, 标识));
        }
    }

    private void 学习(玩家档案 玩家, DataService 数据, string 技能标识)
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        if (!数据.技能.TryGetValue(技能标识, out var 技能)) return;
        if (玩家.掌握技能(技能标识)) { 事件.发布(new 日志事件(日志类型.系统, "你已经掌握这个技能了。")); return; }
        if (玩家.金币 < 技能.价格) { 事件.发布(new 日志事件(日志类型.反馈坏, $"金币不足（需要 {技能.价格}）。")); return; }
        玩家.金币 -= 技能.价格;
        玩家.学习技能(技能标识);
        事件.发布(new 金币变化事件(玩家.金币, -技能.价格));
        事件.发布(new 日志事件(日志类型.反馈, $"你学会了技能：{技能.名称}（-{技能.价格} 金币）"));
    }
}
