using UnityEngine;

// 游戏时钟：推进游戏内时间（1 现实秒 = 2 游戏分钟），整点触发 每小时结算（饱食/水分/伤病/天气）。
// 挂在场景（由「界面搭建工具」创建），UI 订阅事件刷新显示——逻辑与 UI 解耦。
public sealed class 游戏时钟 : MonoBehaviour
{
    private int 上次小时 = -1;
    private int 上次天 = -1;

    private void Update()
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null) return;
        玩家.游戏分钟数 += Time.deltaTime * 2f;

        int 当前分钟 = (int)玩家.游戏分钟数;
        int 当前小时 = 当前分钟 / 60;
        int 当前天 = (int)(玩家.游戏分钟数 / 1440f);

        // 跨天：随机新天气（收音机 预知 优先——预知只保一天，用后清零）
        if (当前天 != 上次天)
        {
            上次天 = 当前天;
            var 事件 = ServiceRegistry.Get<EventBus>();
            if (事件 != null)
            {
                var 天气 = 玩家.预知天气 >= 0 ? (天气类型)玩家.预知天气
                    : (天气类型)Random.Range(0, System.Enum.GetValues(typeof(天气类型)).Length);
                玩家.天气 = (int)天气;
                玩家.预知天气 = -1;
                事件.发布(new 天气变化事件(天气));
            }
        }

        // 整点：每小时生存结算
        if (当前小时 != 上次小时)
        {
            上次小时 = 当前小时;
            var 事件 = ServiceRegistry.Get<EventBus>();
            if (事件 == null) return;
            var 天气 = (天气类型)玩家.天气;
            玩家.每小时结算(天气);
            事件.发布(new 时间变化事件(玩家.游戏分钟数));
            事件.发布(new 生存状态变化事件(生存状态类型.饱食度, 玩家.饱食度, 0));
            事件.发布(new 生存状态变化事件(生存状态类型.水分度, 玩家.水分度, 0));
            foreach (伤病类型 类型 in System.Enum.GetValues(typeof(伤病类型)))
                事件.发布(new 伤病变化事件(类型, 玩家.伤病值(类型), 0));
            if (玩家.生命 <= 0)
                事件.发布(new 日志事件(日志类型.反馈坏, "你倒下了……世界在你眼前暗去。"));
        }
    }
}
