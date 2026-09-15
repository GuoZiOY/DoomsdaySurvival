using UnityEngine;

// 自动存档器：订阅关键生命周期事件，在跨天 / 主线推进 / 战斗结束 / 返回主菜单时自动保存，
// 避免玩家强关或崩溃时丢失进度。手动「保存」按钮仍由侧边栏负责（同一档案覆盖式写入）。
public sealed class 自动存档器
{
    private readonly EventBus 事件;
    private readonly SaveService 存档;
    private readonly PlayerService 玩家;

    private int 上次保存天数 = -1;
    private float 上次保存时间;              // 现实秒（realtimeSinceStartup，不受暂停 timeScale 影响）
    private const float 最小间隔秒 = 4f;     // 非关键点节流，避免战斗中高频连写

    public 自动存档器(EventBus 事件, SaveService 存档, PlayerService 玩家)
    {
        this.事件 = 事件;
        this.存档 = 存档;
        this.玩家 = 玩家;
        上次保存天数 = 玩家.档案.游戏天数;

        // 战斗结算后存档（金币/经验/生命已落档）
        事件.订阅<战斗结束事件>(_ => 保存(false));
        // 跨天检测：游戏天数变化即关键存档（日常任务次日会重刷新）
        事件.订阅<时间变化事件>(_ => 检查跨天());
        // 返回主菜单时存档一次（结束本次冒险的关键点）
        事件.订阅<面板切换事件>(_ => { if (面板管理器.实例?.当前显示面板 is 主菜单面板) 保存(true); });
    }

    private void 检查跨天()
    {
        var 档案 = 玩家.档案;
        if (档案 == null || 档案.游戏天数 == 上次保存天数) return;
        上次保存天数 = 档案.游戏天数;
        保存(true);
    }

    // 自动保存入口：关键点（跨天/主线/主菜单）不节流；战斗等中频点按 最小间隔秒 节流
    public void 保存(bool 关键 = false)
    {
        var 档案 = 玩家?.档案;
        if (档案 == null || string.IsNullOrEmpty(档案.当前节点)) return;   // 未开始冒险不存，避免存出坏档
        float 现在 = Time.realtimeSinceStartup;
        if (!关键 && 现在 - 上次保存时间 < 最小间隔秒) return;
        上次保存时间 = 现在;
        // ★ 刀64：自动存档写**独立槽位** —— 它绝不覆盖玩家手动存的 1/2/3 号。
        //   以前只有一个键、覆盖式写入 → "自动存档"和"手动存档"是同一份，玩家想留一个昨天的档都留不住；
        //   更糟的是自动存档没写成功时会把手动档毁掉。
        存档.保存(存档规格.自动槽号, 档案, 自动: true);
        事件?.发布(new 日志事件(日志类型.系统, "进度已自动保存。"));
    }
}