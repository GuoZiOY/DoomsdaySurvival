using UnityEngine;

// 自动存档器：订阅关键生命周期事件，在跨天 / 战斗结束 / 返回主菜单时自动保存，
// 避免玩家强关或崩溃时丢失进度。
// ★ 刀64 起「存档」是多槽的：自动存档只写 存档规格.自动槽号，绝不覆盖玩家手动存的 1/2/3 号。
public sealed class 自动存档器
{
    private readonly EventBus 事件;
    private readonly SaveService 存档;
    private readonly PlayerService 玩家;

    private int 上次保存天数 = -1;
    private float 上次保存时间;              // 现实秒（realtimeSinceStartup，不受暂停 timeScale 影响）
    private const float 最小间隔秒 = 4f;     // 非关键点节流，避免高频连写

    public 自动存档器(EventBus 事件, SaveService 存档, PlayerService 玩家)
    {
        this.事件 = 事件;
        this.存档 = 存档;
        this.玩家 = 玩家;
        上次保存天数 = 玩家.档案.游戏天数;

        // 战斗结算后存档（战利品/经验/生命已落档）
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

    // 自动保存入口：关键点（跨天/主菜单）不节流；战后按 最小间隔秒 节流
    public void 保存(bool 关键 = false)
    {
        var 档案 = 玩家?.档案;
        if (档案 == null || string.IsNullOrEmpty(档案.当前节点)) return;   // 未开始冒险不存，避免存出坏档

        // ★ 刀64：战斗中**直接不存**（用户口径「战斗时不需要存档读档」）。
        //   不做"欠一次、战后再补"那套 —— 战斗期本来就不该产生存档，而战役结束后
        //   `战斗结束事件`（`结束战斗` 已先把 战斗中 置 false，见 BattleService.回派.cs:12→:36）
        //   会照常触发一次 保存(false)，所以不需要任何额外机制。
        //   注意这里**不动 上次保存时间**：被跳过的一次不该占用节流窗口。
        if (存档门禁.正在战斗) return;

        float 现在 = Time.realtimeSinceStartup;
        if (!关键 && 现在 - 上次保存时间 < 最小间隔秒) return;
        上次保存时间 = 现在;
        存档.保存(存档规格.自动槽号, 档案, 自动: true);
        事件?.发布(new 日志事件(日志类型.系统, "进度已自动保存。"));
    }
}
