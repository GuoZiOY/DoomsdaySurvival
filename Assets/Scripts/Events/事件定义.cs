    // 日志类型：决定前缀与颜色（v39 语义收敛 + v40 补 角色——探索/战斗/生存/获得/警告/系统/内心/角色；
    // 原 剧情/操作 并入 系统，原 反馈/反馈坏 拆为 获得/警告，任务 消息并入 系统/获得；
    // 角色 = 角色身上发生的操作（装备/卸下/使用/学技能/训练），与 获得（物品入包）区分）
    public enum 日志类型 { 探索, 战斗, 生存, 获得, 警告, 系统, 内心, 角色 }

    // 背包变化原因
    public enum 变化原因 { 获得, 失去, 消耗, 出售, 购买 }

    // —— 玩家状态 ——

    // 生命变化：携带当前/最大/本次变化量，UI 据此刷新
    public readonly struct 生命变化事件
    {
        public readonly int 当前; public readonly int 最大; public readonly int 变化量;
        public 生命变化事件(int 当前, int 最大, int 变化量) { this.当前 = 当前; this.最大 = 最大; this.变化量 = 变化量; }
    }

    // 精力变化：探索/行动/物理技能 消耗，休息/睡觉 恢复
    public readonly struct 精力变化事件
    {
        public readonly int 当前; public readonly int 最大; public readonly int 变化量;
        public 精力变化事件(int 当前, int 最大, int 变化量) { this.当前 = 当前; this.最大 = 最大; this.变化量 = 变化量; }
    }

    // 注：原来这里还有一个 `金币变化事件`（当前/变化量）—— v51 刀7 删掉：
    // 本作是以物易物、**没有货币**，它的唯一发布者(效果结算)与唯一订阅者(设施功能面板基类)一并拆了。

    // 时间变化：游戏内分钟数推进（世界时间管理器 整点结算/跳时 后发布，UI 据此刷新）
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
        // 原有一个 5 参"兼容旧签名"构造（体力/智力/意志缺省 5）—— 全仓库 0 调用，v51 刀7 删。
    }

    // 生存状态变化：饱食度/水分度（HUD 与状态栏刷新；float 精确 0.1）
    public readonly struct 生存状态变化事件
    {
        public readonly 生存状态类型 类型; public readonly float 当前; public readonly float 变化量;
        public 生存状态变化事件(生存状态类型 类型, float 当前, float 变化量) { this.类型 = 类型; this.当前 = 当前; this.变化量 = 变化量; }
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

    // 背包变化：物品增删移/数量变化。物品标识 空串 = 通用刷新（不关心具体物品，如清空背包/换装整体刷新）；非空 = 具体物品变化。
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

    // 日志即事件：任何系统发日志事件，日志视图订阅渲染。
    // 过程 = true：过程性消息（如战斗中逐条演出文本），只短暂展示、不沉淀进历史；false = 结果/摘要/叙事，入历史。
    public readonly struct 日志事件
    {
        public readonly 日志类型 类型; public readonly string 文本; public readonly bool 过程;
        public 日志事件(日志类型 类型, string 文本, bool 过程 = false) { this.类型 = 类型; this.文本 = 文本; this.过程 = 过程; }
    }

    // —— 战斗 ——

    public readonly struct 战斗结束事件
    {
        public readonly bool 胜利; public readonly string 结算文本; public readonly string 尸体容器;
        public 战斗结束事件(bool 胜利, string 结算文本, string 尸体容器 = "")
        { this.胜利 = 胜利; this.结算文本 = 结算文本; this.尸体容器 = 尸体容器; }
    }

    // 战斗消息：战斗视图正文渲染用
    public readonly struct 战斗消息事件
    {
        public readonly string 文本;
        public 战斗消息事件(string 文本) { this.文本 = 文本; }
    }

    // —— 天赋（机制型天赋的事件钩子） ——

    // 致命伤害事件：玩家生命即将归零时发（天赋服务 检查 钢铁意志/医者仁心）
    public readonly struct 致命伤害事件
    {
        public readonly 战斗单位 玩家;
        public 致命伤害事件(战斗单位 玩家) { this.玩家 = 玩家; }
    }

    // 制作完成事件：安全屋制作家具（工作台/灶台/医疗站）产出后发（天赋服务 检查 美食家）
    public readonly struct 制作完成事件
    {
        public readonly string 产物;   // 产物物品标识
        public readonly int 数量;      // 本次产出数量
        public readonly string 类型;   // 制作家具类型："工作台"/"灶台"/"医疗站"（美食家 = 灶台 饮食 双倍）
        public 制作完成事件(string 产物, int 数量, string 类型) { this.产物 = 产物; this.数量 = 数量; this.类型 = 类型; }
    }
