using System.Collections.Generic;
using UnityEngine;

// 训练逻辑（末日版）：属性训练 + 技能熟练训练（训练场/安全屋 复用）。
// 训练消耗：行动点（体力训练）+ 食物（补给）；不消耗货币（以物易物经济无货币）。
public sealed class 训练逻辑
{
    // 每次熟练训练增加的熟练度点数
    public const int 熟练训练点数 = 20;
    // 属性训练消耗行动点
    public const int 训练行动点 = 15;
    // 熟练训练消耗行动点
    public const int 熟练训练行动点 = 10;
    // 训练所需食物（面包等）
    public const string 训练食物 = "面包";

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

    // 尝试属性训练：耗行动点 + 食物，增强对应基础属性（不消耗自由属性点）
    public bool 尝试训练(string 项目标识)
    {
        if (!数据.训练项目.TryGetValue(项目标识, out var 项目)) return false;
        if (玩家.行动点 < 训练行动点) { 音效管理器.实例?.播放失败(); 事件.发布(new 日志事件(日志类型.反馈坏, "行动点不足，先休息一下。")); return false; }
        if (!玩家.持有物品(训练食物)) { 音效管理器.实例?.播放失败(); 事件.发布(new 日志事件(日志类型.反馈坏, $"训练需要 {食物名()} 补充体力。")); return false; }
        玩家.消耗行动点(训练行动点);
        玩家.移除物品(训练食物);
        玩家.训练属性(解析属性(项目.属性), 项目.加成);
        事件.发布(new 精力变化事件(玩家.行动点, 玩家.最大行动点, -训练行动点));
        事件.发布(new 背包变化事件(训练食物, -1, 变化原因.消耗));
        事件.发布(new 属性变化事件(玩家.体质, 玩家.力量, 玩家.智慧, 玩家.敏捷, 玩家.意志, 玩家.自由属性点));
        事件.发布(new 日志事件(日志类型.反馈, $"完成{项目.名称}！{项目.属性}+{项目.加成}（耗 {训练行动点} 行动点 + 1 {食物名()}）"));
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

    // 熟练训练花费（末日：行动点制，不再收费）
    public int 熟练训练花费(string 技能标识) => 熟练训练行动点;

    // 每级熟练度阈值
    public int 每级熟练度(技能数据 技能) => Mathf.Max(1, 技能.熟练度每级);

    // 尝试熟练训练：耗行动点加熟练度点数（满阈值升级熟练等级）
    public bool 尝试熟练训练(string 技能标识)
    {
        if (!数据.技能.TryGetValue(技能标识, out var 技能)) return false;
        if (!玩家.掌握技能(技能标识)) return false;
        if (玩家.技能熟练等级(技能标识) >= 玩家档案.熟练等级上限) { 音效管理器.实例?.播放失败(); 事件.发布(new 日志事件(日志类型.系统, $"{技能.名称} 的熟练度已满。")); return false; }
        if (玩家.行动点 < 熟练训练行动点) { 音效管理器.实例?.播放失败(); 事件.发布(new 日志事件(日志类型.反馈坏, "行动点不足，先休息一下。")); return false; }
        玩家.消耗行动点(熟练训练行动点);
        int 新等级 = ServiceRegistry.Get<技能服务>().记录熟练度(技能标识, 熟练训练点数);
        事件.发布(new 精力变化事件(玩家.行动点, 玩家.最大行动点, -熟练训练行动点));
        事件.发布(new 日志事件(日志类型.反馈, $"{技能.名称} 熟练度+{熟练训练点数}（{玩家.技能熟练度(技能标识)}/级），熟练等级 {新等级}"));
        return true;
    }

    private string 食物名() => 数据.物品.TryGetValue(训练食物, out var 物品) ? 物品.标识 : 训练食物;

    // 属性名 → 属性类型
    private static 属性类型 解析属性(string 名)
    {
        switch (名)
        {
            case "体质": return 属性类型.体质;
            case "力量": return 属性类型.力量;
            case "智慧": return 属性类型.智慧;
            case "敏捷": return 属性类型.敏捷;
            case "意志": return 属性类型.意志;
            default: return 属性类型.力量;
        }
    }
}
