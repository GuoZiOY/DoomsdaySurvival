using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 商店面板：商店设施的交互表现（薄视图——数据从 商店设施 逻辑拿，点行调逻辑方法）。
public sealed class 商店面板 : 面板基类
{
    [SerializeField] private TMP_Text 标题;
    [SerializeField] private RectTransform 商品列表;
    [SerializeField] private Button 返回按钮;
    private string 返回节点;
    private 商店设施 商店;

    void Awake()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<金币变化事件>(_ => 刷新(null));   // 购买/卖出后刷新可购可卖状态
        返回按钮?.onClick.AddListener(() => 返回设施(返回节点));
    }

    protected override void 刷新(object 上下文)
    {
        if (上下文 is 设施打开上下文 c) { 商店 = c.逻辑 as 商店设施; 返回节点 = c.返回节点; }
        if (商店 == null) return;
        设文本(标题, $"—— {商店.名称} ——");
        清空(商品列表);
        // 购买区
        foreach (var 物品 in 商店.可购买商品())
        {
            var 标识 = 物品.标识;
            创建行(商品列表, $"【买】{物品.名称}（{物品.描述}）  {物品.价格}金", () => 商店.尝试购买(标识));
        }
        // 卖出区（背包里价格>0 的）
        foreach (var 堆叠 in 商店.可卖出物品())
        {
            if (!ServiceRegistry.Get<DataService>().物品.TryGetValue(堆叠.标识, out var 物品)) continue;
            int 价 = Mathf.Max(1, 物品.价格 / 2);
            var 标识 = 堆叠.标识;
            创建行(商品列表, $"【卖】{物品.名称} ×{堆叠.数量}  {价}金/个", () => 商店.尝试卖出(标识));
        }
    }
}
