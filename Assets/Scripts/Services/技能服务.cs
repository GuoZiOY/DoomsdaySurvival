using System.Collections.Generic;
using UnityEngine;

// 技能服务：技能系统统一入口——学习（属性前提校验）、熟练度记录、技能筛选。
// 武馆/学堂/技能书/剧情 学技能都走 尝试学习；战斗/训练 记录熟练度都走 记录熟练度。设施只是引用它。
public sealed class 技能服务
{
    private readonly EventBus 事件;
    private readonly DataService 数据;
    private readonly PlayerService 玩家服务;
    private 玩家档案 档案 => 玩家服务.档案;   // 动态取当前档案

    public 技能服务(EventBus 事件, DataService 数据, PlayerService 玩家服务)
    {
        this.事件 = 事件;
        this.数据 = 数据;
        this.玩家服务 = 玩家服务;
    }

    // 尝试学习：属性前提达标才能学（唯一入口）
    public bool 尝试学习(string 技能标识)
    {
        if (!数据.技能.TryGetValue(技能标识, out var 技能)) return false;
        var 原因 = 档案.技能前提失败原因(技能);
        if (!string.IsNullOrEmpty(原因)) { 音效管理器.实例?.播放失败(); 事件.发布(new 日志事件(日志类型.警告, $"无法学习 {技能.名称}：{原因}")); return false; }
        if (档案.掌握技能(技能.标识)) { 音效管理器.实例?.播放失败(); 事件.发布(new 日志事件(日志类型.系统, "你已经掌握这个技能了。")); return false; }
        if (!档案.学习技能(技能)) return false;
        事件.发布(new 日志事件(日志类型.角色, $"你学会了技能《{技能.名称}》！"));
        return true;
    }

    // 熟练度记录：使用技能+1 / 训练+点数；自动按该技能每级阈值升级熟练等级
    public int 记录熟练度(string 技能标识, int 点数)
    {
        if (!数据.技能.TryGetValue(技能标识, out var 技能)) return 0;
        return 档案.记录熟练度(技能标识, 点数, Mathf.Max(1, 技能.熟练度每级));
    }

    // 每级熟练度阈值
    public int 每级熟练度(string 技能标识)
    {
        if (!数据.技能.TryGetValue(技能标识, out var 技能)) return 100;
        return Mathf.Max(1, 技能.熟练度每级);
    }

    // 按判定筛技能（设施用来筛选自己教什么：武馆=物理 / 学堂=魔法）
    public 技能数据[] 技能列表(System.Predicate<技能数据> 判定)
    {
        var 列表 = new List<技能数据>();
        foreach (var 技能 in 数据.技能.Values) if (判定(技能)) 列表.Add(技能);
        return 列表.ToArray();
    }
}
