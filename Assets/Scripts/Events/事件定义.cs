    // 日志类型：决定前缀与颜色（系统/剧情/操作/反馈/反馈坏/内心 + 战斗/探索/任务）
    public enum 日志类型 { 系统, 剧情, 操作, 反馈, 反馈坏, 内心, 战斗, 探索, 任务 }

    // 背包变化原因
    public enum 变化原因 { 获得, 失去, 消耗, 出售, 购买 }

    // —— 玩家状态 ——

    // 生命变化：携带当前/最大/本次变化量，UI 据此刷新
    public readonly struct 生命变化事件
    {
        public readonly int 当前; public readonly int 最大; public readonly int 变化量;
        public 生命变化事件(int 当前, int 最大, int 变化量) { this.当前 = 当前; this.最大 = 最大; this.变化量 = 变化量; }
    }

    public readonly struct 魔力变化事件
    {
        public readonly int 当前; public readonly int 最大; public readonly int 变化量;
        public 魔力变化事件(int 当前, int 最大, int 变化量) { this.当前 = 当前; this.最大 = 最大; this.变化量 = 变化量; }
    }

    // 精力变化：探索/行动/物理技能 消耗，休息/睡觉 恢复
    public readonly struct 精力变化事件
    {
        public readonly int 当前; public readonly int 最大; public readonly int 变化量;
        public 精力变化事件(int 当前, int 最大, int 变化量) { this.当前 = 当前; this.最大 = 最大; this.变化量 = 变化量; }
    }

    public readonly struct 金币变化事件
    {
        public readonly int 当前; public readonly int 变化量;
        public 金币变化事件(int 当前, int 变化量) { this.当前 = 当前; this.变化量 = 变化量; }
    }

    // 时间变化：游戏内分钟数推进（游戏时钟 每分钟发布一次，UI 据此刷新）
    public readonly struct 时间变化事件
    {
        public readonly float 游戏分钟数;
        public 时间变化事件(float 游戏分钟数) { this.游戏分钟数 = 游戏分钟数; }
    }

    public readonly struct 经验变化事件
    {
        public readonly int 等级; public readonly int 当前经验; public readonly int 升级所需; public readonly bool 升级了;
        public 经验变化事件(int 等级, int 当前经验, int 升级所需, bool 升级了)
        { this.等级 = 等级; this.当前经验 = 当前经验; this.升级所需 = 升级所需; this.升级了 = 升级了; }
    }

    // 属性变化：角色面板刷新 5 大核心属性与自由点
    public readonly struct 属性变化事件
    {
        public readonly int 体质; public readonly int 力量; public readonly int 智慧; public readonly int 敏捷; public readonly int 意志; public readonly int 自由属性点;
        public 属性变化事件(int 体质, int 力量, int 智慧, int 敏捷, int 意志, int 自由属性点)
        { this.体质 = 体质; this.力量 = 力量; this.智慧 = 智慧; this.敏捷 = 敏捷; this.意志 = 意志; this.自由属性点 = 自由属性点; }
        // 兼容旧签名（体力=体质，智力=智慧，意志缺省 5）
        public 属性变化事件(int 体力, int 力量, int 智力, int 敏捷, int 自由属性点)
            : this(体力, 力量, 智力, 敏捷, 5, 自由属性点) { }
    }

    // 生存状态变化：饱食度/水分度（HUD 与状态栏刷新）
    public readonly struct 生存状态变化事件
    {
        public readonly 生存状态类型 类型; public readonly int 当前; public readonly int 变化量;
        public 生存状态变化事件(生存状态类型 类型, int 当前, int 变化量) { this.类型 = 类型; this.当前 = 当前; this.变化量 = 变化量; }
    }

    // 伤病变化：疲劳/中毒/感冒/流血/骨折/发烧（严重度 0~100，0=无）
    public readonly struct 伤病变化事件
    {
        public readonly 伤病类型 类型; public readonly int 当前; public readonly int 变化量;
        public 伤病变化事件(伤病类型 类型, int 当前, int 变化量) { this.类型 = 类型; this.当前 = 当前; this.变化量 = 变化量; }
    }

    // 天气变化：每日随机生成后发布（HUD/探索面板刷新）
    public readonly struct 天气变化事件
    {
        public readonly 天气类型 天气;
        public 天气变化事件(天气类型 天气) { this.天气 = 天气; }
    }

    // —— 背包 ——

    public readonly struct 背包变化事件
    {
        public readonly string 物品标识; public readonly int 数量; public readonly 变化原因 原因;
        public 背包变化事件(string 物品标识, int 数量, 变化原因 原因) { this.物品标识 = 物品标识; this.数量 = 数量; this.原因 = 原因; }
    }

    // —— 任务 ——

    public readonly struct 任务进度事件
    {
        public readonly string 任务标识; public readonly int 进度; public readonly int 目标; public readonly bool 已完成;
        public 任务进度事件(string 任务标识, int 进度, int 目标, bool 已完成)
        { this.任务标识 = 任务标识; this.进度 = 进度; this.目标 = 目标; this.已完成 = 已完成; }
    }

    public readonly struct 任务完成事件
    {
        public readonly string 任务标识; public readonly string 奖励文本;
        public 任务完成事件(string 任务标识, string 奖励文本) { this.任务标识 = 任务标识; this.奖励文本 = 奖励文本; }
    }

    // 日常任务（悬赏）变更：悬赏板/系统任务面板 据此刷新
    public readonly struct 日常任务变更事件 { }

    // —— 日志 ——

    // 日志即事件：任何系统发日志事件，日志视图订阅渲染
    public readonly struct 日志事件
    {
        public readonly 日志类型 类型; public readonly string 文本;
        public 日志事件(日志类型 类型, string 文本) { this.类型 = 类型; this.文本 = 文本; }
    }

    // —— 战斗 ——

    public readonly struct 战斗结束事件
    {
        public readonly bool 胜利; public readonly string 结算文本;
        public 战斗结束事件(bool 胜利, string 结算文本) { this.胜利 = 胜利; this.结算文本 = 结算文本; }
    }

    // 战斗消息：战斗视图正文渲染用
    public readonly struct 战斗消息事件
    {
        public readonly string 文本;
        public 战斗消息事件(string 文本) { this.文本 = 文本; }
    }
