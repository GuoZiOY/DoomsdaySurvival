using System.Collections.Generic;
using UnityEngine;

// 买卖逻辑：购买/卖出 的共用实现（商店通用 / 武馆兵器 / 学堂魔法物 复用，可购商品按判定筛选）
public sealed class 买卖逻辑
{
    private readonly 玩家档案 玩家;
    private readonly DataService 数据;
    private readonly EventBus 事件;

    public 买卖逻辑(玩家档案 玩家, DataService 数据, EventBus 事件)
    {
        this.玩家 = 玩家;
        this.数据 = 数据;
        this.事件 = 事件;
    }

    // 可购商品：按判定筛选（商店通用 / 武馆兵器 / 学堂魔法物）
    public 物品数据[] 可购商品(System.Predicate<物品数据> 判定)
    {
        var 列表 = new List<物品数据>();
        foreach (var 物品 in 数据.物品.Values) if (判定(物品)) 列表.Add(物品);
        return 列表.ToArray();
    }

    // 尝试购买：扣铜币，物品一律入背包（武器/防具不再自动装备——装备统一走角色面板的 [装备]，转移模型）。价格单位 = 铜币
    public bool 尝试购买(string 物品标识)
    {
        if (!数据.物品.TryGetValue(物品标识, out var 物品)) return false;
        if (玩家.铜币 < 物品.价格) { 音效管理器.实例?.播放失败(); 事件.发布(new 日志事件(日志类型.反馈坏, $"铜币不足（需要 {货币工具.文本(物品.价格)}）。")); return false; }
        玩家.铜币 -= 物品.价格;
        玩家.添加物品(物品.标识);
        事件.发布(new 金币变化事件(玩家.铜币, -物品.价格));
        事件.发布(new 背包变化事件(物品.标识, 1, 变化原因.购买));
        事件.发布(new 日志事件(日志类型.反馈, $"购买 {物品.名称}（-{货币工具.文本(物品.价格)}）"));
        return true;
    }

    // 可卖物品：背包里 价格>0 且通过设施判定（商店通用/武馆收装备/学堂收魔法物——由各设施决定收什么）
    public 物品堆叠[] 可卖物品(System.Predicate<物品数据> 判定)
    {
        var 列表 = new List<物品堆叠>();
        foreach (var 堆叠 in 玩家.背包)
            if (数据.物品.TryGetValue(堆叠.标识, out var 物品) && 物品.价格 > 0 && (判定 == null || 判定(物品))) 列表.Add(堆叠);
        return 列表.ToArray();
    }

    // 尝试卖出：按半价回收（最低 1 铜币）
    public bool 尝试卖出(string 物品标识)
    {
        if (!数据.物品.TryGetValue(物品标识, out var 物品) || 物品.价格 <= 0) return false;
        if (!玩家.移除物品(物品标识)) { 音效管理器.实例?.播放失败(); 事件.发布(new 日志事件(日志类型.反馈坏, "你没有这件物品。")); return false; }
        int 价 = Mathf.Max(1, 物品.价格 / 2);
        玩家.铜币 += 价;
        事件.发布(new 金币变化事件(玩家.铜币, 价));
        事件.发布(new 日志事件(日志类型.反馈, $"卖出 {物品.名称}（+{货币工具.文本(价)}）"));
        return true;
    }
}
