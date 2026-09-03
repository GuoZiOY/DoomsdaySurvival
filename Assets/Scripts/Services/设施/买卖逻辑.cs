using System.Collections.Generic;
using UnityEngine;

// 买卖逻辑（末日版：以物易物）：购买/卖出 的共用实现（交易站/黑市 复用）。
// 价值点数制：物品.价值（1~100），等值交换——买方用背包物资折价支付。
// 卖出 → 换得"交易额度"（会话内价值凭证）；购买 → 优先扣额度，不足再按价值从高到低扣背包物资。
public sealed class 买卖逻辑
{
    private readonly 玩家档案 玩家;
    private readonly DataService 数据;
    private readonly EventBus 事件;

    // 当前会话交易额度（卖出累计，购买优先消耗）
    public int 交易额度 { get; private set; }

    public 买卖逻辑(玩家档案 玩家, DataService 数据, EventBus 事件)
    {
        this.玩家 = 玩家;
        this.数据 = 数据;
        this.事件 = 事件;
    }

    // 可购商品：按判定筛选（交易站通用 / 各设施自定义）
    public 物品数据[] 可购商品(System.Predicate<物品数据> 判定)
    {
        var 列表 = new List<物品数据>();
        foreach (var 物品 in 数据.物品.Values) if (判定(物品)) 列表.Add(物品);
        return 列表.ToArray();
    }

    // 尝试购买：支付等值价值（额度或物资），物品入背包
    public bool 尝试购买(string 物品标识)
    {
        if (!数据.物品.TryGetValue(物品标识, out var 物品)) return false;
        int 需价值 = 物品.价值;
        if (背包总价值() < 需价值)
        {
            音效管理器.实例?.播放失败();
            事件.发布(new 日志事件(日志类型.警告, $"物资价值不足（需要 {需价值}，当前 {背包总价值()}）。"));
            return false;
        }
        if (!扣除价值(需价值)) return false;
        玩家.添加物品(物品.标识);
        事件.发布(new 背包变化事件(物品.标识, 1, 变化原因.购买));
        事件.发布(new 日志事件(日志类型.获得, $"以物换物获得 {物品.标识}（价值 {需价值}）。"));
        return true;
    }

    // 可卖物品：背包里 价值>0 且通过设施判定
    public 物品堆叠[] 可卖物品(System.Predicate<物品数据> 判定)
    {
        var 列表 = new List<物品堆叠>();
        foreach (var 堆叠 in 玩家.所有持有物品())
            if (数据.物品.TryGetValue(堆叠.标识, out var 物品) && 物品.价值 > 0 && (判定 == null || 判定(物品))) 列表.Add(堆叠);
        return 列表.ToArray();
    }

    // 尝试卖出：物品离包，获得等值交易额度
    public bool 尝试卖出(string 物品标识)
    {
        if (!数据.物品.TryGetValue(物品标识, out var 物品) || 物品.价值 <= 0) return false;
        if (!玩家.移除物品(物品标识)) { 音效管理器.实例?.播放失败(); 事件.发布(new 日志事件(日志类型.警告, "你没有这件物品。")); return false; }
        交易额度 += 物品.价值;
        事件.发布(new 背包变化事件(物品标识, -1, 变化原因.出售));
        事件.发布(new 日志事件(日志类型.获得, $"你换出 {物品.标识}，获得价值 {物品.价值} 的交易额度。"));
        return true;
    }

    // 背包总价值（额度 + 物资）
    private int 背包总价值()
    {
        int 总 = 交易额度;
        foreach (var 堆叠 in 玩家.所有持有物品())
            if (数据.物品.TryGetValue(堆叠.标识, out var 物品)) 总 += 物品.价值 * 堆叠.数量;
        return 总;
    }

    // 扣除价值：先扣交易额度，再按价值从高到低扣背包物资
    private bool 扣除价值(int 价值)
    {
        int 剩余 = 价值;
        if (交易额度 >= 剩余) { 交易额度 -= 剩余; return true; }
        剩余 -= 交易额度; 交易额度 = 0;
        var 可扣 = new List<(物品堆叠 堆叠, int 单位价值)>();
        foreach (var 堆叠 in 玩家.所有持有物品())
            if (数据.物品.TryGetValue(堆叠.标识, out var 物品) && 物品.价值 > 0 && 堆叠.数量 > 0)
                可扣.Add((堆叠, 物品.价值));
        可扣.Sort((a, b) => b.单位价值.CompareTo(a.单位价值));
        foreach (var (堆叠, 单位价值) in 可扣)
        {
            if (剩余 <= 0) break;
            int 扣件 = Mathf.Min(堆叠.数量, (剩余 + 单位价值 - 1) / 单位价值);
            玩家.移除物品(堆叠.标识, 扣件);
            事件.发布(new 背包变化事件(堆叠.标识, -扣件, 变化原因.消耗));
            剩余 -= 扣件 * 单位价值;
        }
        return 剩余 <= 0;
    }
}
