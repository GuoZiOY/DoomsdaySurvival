using UnityEngine;

// 买卖面板：买卖功能（商店/武馆/学堂共用——各自 可购商品 筛选不同数据）
public sealed class 买卖面板 : 设施功能面板基类
{
    protected override string 标题文字() => $"{逻辑.名称} · 买卖";

    protected override void 渲染列表()
    {
        if (逻辑 is not 买卖功能 买卖) return;
        var 数据 = ServiceRegistry.Get<DataService>();
        // 购买区
        foreach (var 物品 in 买卖.可购商品())
        {
            var 标识 = 物品.标识;
            创建行(内容区, $"【买】{品质工具.标签(物品.品质档)}{物品.名称}（{物品.描述}）  {物品.价格}金", () => 买卖.尝试购买(标识));
        }
        // 卖出区（背包里价格>0 的）
        foreach (var 堆叠 in 买卖.可卖物品())
        {
            if (!数据.物品.TryGetValue(堆叠.标识, out var 物品)) continue;
            int 价 = Mathf.Max(1, 物品.价格 / 2);
            var 标识 = 堆叠.标识;
            创建行(内容区, $"【卖】{物品.名称} ×{堆叠.数量}  {价}金/个", () => 买卖.尝试卖出(标识));
        }
    }
}
