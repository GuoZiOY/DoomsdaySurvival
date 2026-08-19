using UnityEngine;

// 面板操作：玩家档案的通用面板操作（加点/换装/技能书/恢复品）——多个面板/子面板共用，避免重复实现。
// 只做领域操作 + 发事件，不碰 UI；UI 订阅事件自行刷新。
public static class 面板操作
{
    // 加点：消耗 1 自由属性点，成功后发 属性变化事件 + 日志
    public static void 加点(玩家档案 玩家, 属性类型 类型)
    {
        if (!玩家.加点(类型)) return;
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.发布(new 属性变化事件(玩家.体力, 玩家.力量, 玩家.智力, 玩家.敏捷, 玩家.自由属性点));
        事件.发布(new 日志事件(日志类型.反馈, $"{类型} +1"));
    }

    // 换装（转移模型）：按 物品.槽位 自动定位槽（饰品→饰品1/饰品2 空槽优先），旧装备回背包 + 新装备入槽 + 背包扣 1。
    // 触发 背包变化/属性变化（派生数值随装备变化）事件，UI 自刷新。
    public static void 换装(玩家档案 玩家, 物品数据 物品)
    {
        string 槽 = 物品.槽位;
        if (string.IsNullOrEmpty(槽)) return;   // 非装备类防误用
        if (槽 == "饰品") 槽 = 玩家.饰品目标槽();
        string 旧标识 = 玩家.装备到槽(槽, 物品.标识);
        玩家.移除物品(物品.标识, 1);
        var 事件 = ServiceRegistry.Get<EventBus>();
        if (!string.IsNullOrEmpty(旧标识) && 旧标识 != 物品.标识)
        {
            玩家.添加物品(旧标识, 1);   // 同槽旧装备回背包（堆叠合并）
            事件.发布(new 背包变化事件(旧标识, 1, 变化原因.获得));
        }
        事件.发布(new 背包变化事件(物品.标识, -1, 变化原因.消耗));
        事件.发布(new 属性变化事件(玩家.体力, 玩家.力量, 玩家.智力, 玩家.敏捷, 玩家.自由属性点));
        事件.发布(new 日志事件(日志类型.反馈, $"装备了 {物品.名称}。"));
    }

    // 卸下（转移模型）：清空指定槽 + 背包加回 1 件（堆叠自动合并）
    public static void 卸下(玩家档案 玩家, string 槽位)
    {
        string 标识 = 玩家.卸下装备(槽位);
        if (string.IsNullOrEmpty(标识)) return;
        玩家.添加物品(标识, 1);
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.发布(new 背包变化事件(标识, 1, 变化原因.获得));
        事件.发布(new 属性变化事件(玩家.体力, 玩家.力量, 玩家.智力, 玩家.敏捷, 玩家.自由属性点));
        string 名称 = ServiceRegistry.Get<DataService>().物品.TryGetValue(标识, out var 物品) ? 物品.名称 : 标识;
        事件.发布(new 日志事件(日志类型.反馈, $"卸下了 {名称}。"));
    }

    // 技能书：技能系统统一入口（属性前提达标才学），成功才消耗书本
    public static void 学习技能书(玩家档案 玩家, 物品数据 物品)
    {
        if (string.IsNullOrEmpty(物品.技能)) return;
        if (ServiceRegistry.Get<技能服务>().尝试学习(物品.技能))
        {
            玩家.移除物品(物品.标识);
            ServiceRegistry.Get<EventBus>().发布(new 背包变化事件(物品.标识, -1, 变化原因.消耗));
        }
    }

    // 使用恢复品（战斗外）：扣物品 + 恢复 + 发事件
    public static void 使用恢复(玩家档案 玩家, DataService 数据, string 标识)
    {
        if (!数据.物品.TryGetValue(标识, out var 物品) || 物品.类型 != "恢复") return;
        if (!玩家.移除物品(标识)) return;
        int 恢复 = Mathf.Max(1, Mathf.RoundToInt(物品.恢复量 * 品质工具.倍率(物品.品质档)));
        玩家.恢复生命(恢复);
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.发布(new 生命变化事件(玩家.生命, 玩家.最大生命, 恢复));
        事件.发布(new 背包变化事件(标识, -1, 变化原因.消耗));
        事件.发布(new 日志事件(日志类型.反馈, $"你使用了{物品.名称}，恢复 {恢复} 点生命。"));
    }
}
