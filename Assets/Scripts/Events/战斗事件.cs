// 战斗事件契约：战斗状态机 → 战斗面板 渲染。UI 只订阅不轮询、不能改战斗数据。

// 战斗开始：携带我方/敌方单位数组（面板据此布阵）
public readonly struct 战斗开始事件
{
    public readonly 战斗单位[] 我方;
    public readonly 战斗单位[] 敌方;
    public 战斗开始事件(战斗单位[] 我方, 战斗单位[] 敌方) { this.我方 = 我方; this.敌方 = 敌方; }
}

// 回合开始
public readonly struct 回合开始事件
{
    public readonly int 回合数;
    public 回合开始事件(int 回合数) { this.回合数 = 回合数; }
}

// 行动轮换：当前行动单位 + 是否玩家回合（面板据此开/关按钮）
public readonly struct 行动轮换事件
{
    public readonly 战斗单位 行动单位;
    public readonly bool 是否玩家回合;
    public 行动轮换事件(战斗单位 行动单位, bool 是否玩家回合) { this.行动单位 = 行动单位; this.是否玩家回合 = 是否玩家回合; }
}

// 伤害事件：数值/类型/暴击/闪避
public readonly struct 伤害事件
{
    public readonly 战斗单位 目标;
    public readonly int 数值;
    public readonly 伤害类型 类型;
    public readonly bool 暴击;
    public readonly bool 闪避;
    public 伤害事件(战斗单位 目标, int 数值, 伤害类型 类型, bool 暴击, bool 闪避)
    { this.目标 = 目标; this.数值 = 数值; this.类型 = 类型; this.暴击 = 暴击; this.闪避 = 闪避; }
}

// 治疗事件
public readonly struct 治疗事件
{
    public readonly 战斗单位 目标;
    public readonly int 数值;
    public 治疗事件(战斗单位 目标, int 数值) { this.目标 = 目标; this.数值 = 数值; }
}

// 状态变化事件：buff 添加/移除（面板刷新状态栏）
public readonly struct 状态变化事件
{
    public readonly 战斗单位 单位;
    public readonly string Buff标识;
    public readonly int 层数;
    public readonly int 剩余回合;
    public readonly bool 移除;
    public 状态变化事件(战斗单位 单位, string Buff标识, int 层数, int 剩余回合, bool 移除)
    { this.单位 = 单位; this.Buff标识 = Buff标识; this.层数 = 层数; this.剩余回合 = 剩余回合; this.移除 = 移除; }
}

// 目标变化事件：单位死亡（面板移除卡牌/剔除目标）
public readonly struct 目标变化事件
{
    public readonly 战斗单位 单位;
    public 目标变化事件(战斗单位 单位) { this.单位 = 单位; }
}
