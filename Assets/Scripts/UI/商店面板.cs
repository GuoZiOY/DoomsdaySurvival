using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 商店面板：酒馆商店的交互表现（购买补给与装备）。
// 打开由 面板管理器 路由（刷新 里从 打开设施事件 取返回节点）；本面板只处理自己的交互。
public sealed class 商店面板 : 面板基类
{
    [SerializeField] private TMP_Text 标题;
    [SerializeField] private RectTransform 商品列表;
    [SerializeField] private Button 返回按钮;
    private string 返回节点;

    void Awake()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<金币变化事件>(_ => 刷新(null));   // 购买后刷新可购状态
        返回按钮?.onClick.AddListener(() => 返回设施(返回节点));
    }

    protected override void 刷新(object 上下文)
    {
        if (上下文 is 打开设施事件 e) 返回节点 = e.返回节点;
        设文本(标题, "—— 酒馆 · 补给 ——");
        清空(商品列表);
        var 玩家 = ServiceRegistry.Get<PlayerService>().档案;
        var 数据 = ServiceRegistry.Get<DataService>();
        foreach (var 物品 in 数据.物品.Values)
        {
            if (物品.价格 <= 0 || 物品.类型 == "任务") continue;
            var 标识 = 物品.标识;
            创建行(商品列表, $"{物品.名称}（{物品.描述}）  {物品.价格}金", () => 购买(玩家, 数据, 标识));
        }
    }

    // 购买：扣金币，武器/防具直接装备，其余入包，发事件
    private void 购买(玩家档案 玩家, DataService 数据, string 物品标识)
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        if (!数据.物品.TryGetValue(物品标识, out var 物品)) return;
        if (玩家.金币 < 物品.价格) { 事件.发布(new 日志事件(日志类型.反馈坏, $"金币不足（需要 {物品.价格}）。")); return; }
        玩家.金币 -= 物品.价格;
        if (物品.类型 == "武器") 玩家.武器标识 = 物品.标识;
        else if (物品.类型 == "防具") 玩家.防具标识 = 物品.标识;
        else 玩家.添加物品(物品.标识);
        事件.发布(new 金币变化事件(玩家.金币, -物品.价格));
        事件.发布(new 日志事件(日志类型.反馈, $"购买 {物品.名称}（-{物品.价格} 金币）"));
    }
}
