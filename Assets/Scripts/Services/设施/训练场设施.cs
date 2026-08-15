using System.Collections.Generic;

// 训练场设施：技能学习 业务规则（UI 无关，反馈走事件）
public sealed class 训练场设施 : 设施逻辑
{
    // 可学习技能：全部技能
    public 技能数据[] 可学习技能()
    {
        var 列表 = new List<技能数据>();
        foreach (var 技能 in 数据.技能.Values) 列表.Add(技能);
        return 列表.ToArray();
    }

    // 尝试学习：扣金币并掌握
    public bool 尝试学习(string 技能标识)
    {
        if (!数据.技能.TryGetValue(技能标识, out var 技能)) return false;
        if (玩家.掌握技能(技能标识)) { 事件.发布(new 日志事件(日志类型.系统, "你已经掌握这个技能了。")); return false; }
        if (玩家.金币 < 技能.价格) { 事件.发布(new 日志事件(日志类型.反馈坏, $"金币不足（需要 {技能.价格}）。")); return false; }
        玩家.金币 -= 技能.价格;
        玩家.学习技能(技能标识);
        事件.发布(new 金币变化事件(玩家.金币, -技能.价格));
        事件.发布(new 日志事件(日志类型.反馈, $"你学会了技能：{技能.名称}（-{技能.价格} 金币）"));
        return true;
    }
}
