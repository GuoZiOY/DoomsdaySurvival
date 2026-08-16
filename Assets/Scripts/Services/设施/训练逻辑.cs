using System.Collections.Generic;
using UnityEngine;

// 训练逻辑：属性训练 + 技能熟练训练 的共用实现（训练场/武馆/学堂 复用，避免重复）
public sealed class 训练逻辑
{
    // 每次熟练训练增加的熟练度点数
    public const int 熟练训练点数 = 20;

    private readonly 玩家档案 玩家;
    private readonly DataService 数据;
    private readonly EventBus 事件;

    public 训练逻辑(玩家档案 玩家, DataService 数据, EventBus 事件)
    {
        this.玩家 = 玩家;
        this.数据 = 数据;
        this.事件 = 事件;
    }

    // 属性训练项目（training.json）
    public 训练项目[] 可训项目()
    {
        var 列表 = new List<训练项目>();
        foreach (var 项目 in 数据.训练项目.Values) 列表.Add(项目);
        return 列表.ToArray();
    }

    // 尝试属性训练：花金币增强对应基础属性（不消耗自由属性点）
    public bool 尝试训练(string 项目标识)
    {
        if (!数据.训练项目.TryGetValue(项目标识, out var 项目)) return false;
        if (玩家.金币 < 项目.花费) { 事件.发布(new 日志事件(日志类型.反馈坏, $"金币不足（需要 {项目.花费}）。")); return false; }
        玩家.金币 -= 项目.花费;
        玩家.训练属性(解析属性(项目.属性), 项目.加成);
        事件.发布(new 金币变化事件(玩家.金币, -项目.花费));
        事件.发布(new 属性变化事件(玩家.体力, 玩家.力量, 玩家.智力, 玩家.敏捷, 玩家.自由属性点));
        事件.发布(new 日志事件(日志类型.反馈, $"完成{项目.名称}！{项目.属性}+{项目.加成}（-{项目.花费} 金币）"));
        return true;
    }

    // 可练熟练技能：已学且未满级
    public 技能数据[] 可练熟练技能()
    {
        var 列表 = new List<技能数据>();
        foreach (var 掌握 in 玩家.已学技能)
            if (掌握.熟练等级 < 玩家档案.熟练等级上限 && 数据.技能.TryGetValue(掌握.标识, out var 技能))
                列表.Add(技能);
        return 列表.ToArray();
    }

    // 熟练训练花费：按当前熟练等级 30×N（N=当前等级）
    public int 熟练训练花费(string 技能标识) => 30 * Mathf.Max(1, 玩家.技能熟练等级(技能标识));

    // 每级熟练度阈值
    public int 每级熟练度(技能数据 技能) => Mathf.Max(1, 技能.熟练度每级);

    // 尝试熟练训练：花金币加熟练度点数（满阈值升级熟练等级）
    public bool 尝试熟练训练(string 技能标识)
    {
        if (!数据.技能.TryGetValue(技能标识, out var 技能)) return false;
        if (!玩家.掌握技能(技能标识)) return false;
        if (玩家.技能熟练等级(技能标识) >= 玩家档案.熟练等级上限) { 事件.发布(new 日志事件(日志类型.系统, $"{技能.名称} 的熟练度已满。")); return false; }
        int 花费 = 熟练训练花费(技能标识);
        if (玩家.金币 < 花费) { 事件.发布(new 日志事件(日志类型.反馈坏, $"金币不足（需要 {花费}）。")); return false; }
        玩家.金币 -= 花费;
        int 新等级 = 玩家.记录熟练度(技能标识, 熟练训练点数, Mathf.Max(1, 技能.熟练度每级));
        事件.发布(new 金币变化事件(玩家.金币, -花费));
        事件.发布(new 日志事件(日志类型.反馈, $"{技能.名称} 熟练度+{熟练训练点数}（{玩家.技能熟练度(技能标识)}/级），熟练等级 {新等级}（-{花费} 金币）"));
        return true;
    }

    // 属性名 → 属性类型
    private static 属性类型 解析属性(string 名)
    {
        switch (名)
        {
            case "体力": return 属性类型.体力;
            case "力量": return 属性类型.力量;
            case "智力": return 属性类型.智力;
            case "敏捷": return 属性类型.敏捷;
            default: return 属性类型.力量;
        }
    }
}
