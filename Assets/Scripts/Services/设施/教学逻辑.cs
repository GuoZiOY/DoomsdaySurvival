using System.Collections.Generic;

// 教学逻辑：技能学习 的共用实现（武馆=物理 / 学堂=魔法 复用；属性前提达标才能学）
public sealed class 教学逻辑
{
    private readonly 玩家档案 玩家;
    private readonly DataService 数据;
    private readonly EventBus 事件;

    public 教学逻辑(玩家档案 玩家, DataService 数据, EventBus 事件)
    {
        this.玩家 = 玩家;
        this.数据 = 数据;
        this.事件 = 事件;
    }

    // 可学技能：按判定筛选（武馆=物理技能 / 学堂=魔法技能）
    public 技能数据[] 可学技能(System.Predicate<技能数据> 判定)
    {
        var 列表 = new List<技能数据>();
        foreach (var 技能 in 数据.技能.Values) if (判定(技能)) 列表.Add(技能);
        return 列表.ToArray();
    }

    // 尝试学习：属性前提达标才能学（任何途径同此校验）
    public bool 尝试学习(string 标识)
    {
        if (!数据.技能.TryGetValue(标识, out var 技能)) return false;
        var 原因 = 玩家.技能前提失败原因(技能);
        if (!string.IsNullOrEmpty(原因)) { 事件.发布(new 日志事件(日志类型.反馈坏, $"无法学习 {技能.名称}：{原因}")); return false; }
        if (玩家.掌握技能(技能.标识)) { 事件.发布(new 日志事件(日志类型.系统, "你已经掌握这个技能了。")); return false; }
        if (!玩家.学习技能(技能)) return false;
        事件.发布(new 日志事件(日志类型.反馈, $"你学会了技能：{技能.名称}！"));
        return true;
    }
}
