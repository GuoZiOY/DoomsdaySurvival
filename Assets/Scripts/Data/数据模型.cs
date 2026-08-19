using System;

    // 全部为 [Serializable] 纯数据类，字段名与旧 数据/数据模型.cs 一致，便于直接反序列化现有 JSON。

    // ================= 剧情 =================

    // 剧情效果：作用于玩家状态（正=获得，负=损失）
    [Serializable]
    public class 剧情效果
    {
        public int 生命;
        public int 魔力;
        public int 精力;       // 正=恢复 / 负=消耗（休息事件等）
        public int 金币;
        public int 经验;
        public string 获得物品;
        public int 获得数量;   // 获得物品的数量（缺省按 1；JsonUtility 缺失=0，结算时兜底 1）
        public string 失去物品;
    }

    // 剧情选项：分支；目标可为节点 / "战斗:敌:胜节点" / "设施:标识" / "探索:区域:返回" / "区域:区域" / "__结束" 等
    [Serializable]
    public class 剧情选项
    {
        public string 文本;
        public string 目标;
        public string 需要物品;   // 持有该物品才显示（条件分支）
        public 剧情效果 效果;
    }

    // 剧情节点：一段文字 + 可选效果 + 可选强制战斗 + 可选结构化面板 + 选项分支
    [Serializable]
    public class 剧情节点
    {
        public string 标识;
        public string 文本;
        public 剧情效果 效果;
        public string 战斗;       // 非空则进入即开战：战斗:敌人标识:胜利节点标识
        public string 面板;       // 非空则进入构建结构化面板（M4 起废弃，改用设施）
        public 剧情选项[] 选项;
    }

    [Serializable]
    public class 剧情根 { public 剧情节点[] 节点; }

    // ================= 敌人 =================

    [Serializable]
    public class 敌人数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public int 生命;
        public int 攻击;
        public int 防御;          // 物防
        public int 魔防;          // 魔防
        public int 速度;          // 行动顺序（速度队列）
        public int 等级 = 1;      // 敌人等级
        public 抗性配置[] 抗性;   // 抗性表：伤害类型→倍率
        public AI行动项[] 行动表; // AI行动表
        public string 目标策略;   // 攻击目标策略：随机 / 残血 / 低防
        public int 金币奖励;
        public int 经验奖励;
        public string 掉落物品;   // 可选掉落物品标识
        public float 掉落概率;    // 0~1
    }

    [Serializable]
    public class 敌人根 { public 敌人数据[] 敌人; }

    // ================= 物品 =================

    [Serializable]
    public class 物品数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public string 类型;       // "恢复" 消耗品 / "任务" 任务物品 / "武器" / "防具" / "饰品" / "技能书"
        public int 恢复量;        // 类型=恢复 时的恢复值（受品质倍率影响）
        public int 攻击加成;      // 类型=武器（受品质倍率影响）
        public int 防御加成;      // 类型=防具/饰品（受品质倍率影响）
        public int 生命加成;      // 类型=防具/饰品：生命上限加成（词缀差异化后续）
        public string 槽位;       // 装备类：放入的槽位（"主手"/"副手"/"头盔"/"盔甲"/"靴子"/"手套"/"饰品"）
        public string 技能;       // 类型=技能书 时授予的技能标识
        public int 价格;          // 商店买卖价格（单位：铜币，1金=10000铜；0=不可买卖）
        public string 品质;       // "普通"/"优秀"/"稀有"...（JsonUtility 不认枚举名，字符串+转换）
        public 品质 品质档 => 数据解析.枚举<品质>(品质);
        // —— 战斗内使用 ——
        public bool 战斗内使用;         // 是否可在战斗中使用（恢复/增益/减益道具）
        public string 使用目标;         // "我方单体"/"敌方单体"…
        public 目标类型 使用目标枚举 => 数据解析.枚举<目标类型>(使用目标);
        public string 使用效果;         // "恢复"/"增益"/"减益"
        public 效果类型 使用效果枚举 => 数据解析.枚举<效果类型>(使用效果);
        public string 挂载Buff;         // 增益/减益挂载的buff标识（0=纯数值）
    }

    [Serializable]
    public class 物品根 { public 物品数据[] 物品; }

    // ================= 战斗系统 =================

    // 伤害类型：物理/魔法/真实。可扩展（八方旅人式：火焰/寒冰/圣光…）
    public enum 伤害类型 { 物理, 魔法, 真实 }
    // 行动目标类型：决定目标选择集合
    public enum 目标类型 { 敌方单体, 敌方全体, 我方单体, 我方全体, 自己 }
    // 技能类别
    public enum 技能类别 { 攻击, 治疗, 增益, 减益, 控制 }
    // Buff 类型
    public enum Buff类型 { 增益, 减益, 异常, 护盾 }
    // 物品使用效果
    public enum 效果类型 { 恢复, 增益, 减益 }

    // Buff 定义（buffs.json）：增益/减益/异常/护盾 的通用定义
    [Serializable]
    public class Buff定义
    {
        public string 标识;
        public string 名称;
        public string 类型;        // "增益"/"减益"/"异常"/"护盾"
        public Buff类型 类型枚举 => 数据解析.枚举<Buff类型>(类型);
        public string 属性;        // 增益/减益作用属性："攻击"/"魔攻"/"防御"/"魔防"/"速度"（异常/护盾留空）
        public int 数值;          // 增益/减益=±百分比(20=±20%)；异常=每回合伤害；护盾=护盾量
        public int 持续回合 = 1;   // 增益/减益/异常 持续回合（护盾=耗尽即消，不看回合）
        public int 最大层数 = 1;   // 可叠层上限（>1 允许叠层）
        public bool 控制;         // 异常=控制类(麻痹/眩晕，跳过行动)；持续伤害=异常且非控制
        public string 描述;
    }

    [Serializable]
    public class Buff根 { public Buff定义[] Buffs; }

    // 敌人抗性项：伤害类型 → 倍率（弱点×1.5 / 抵抗×0.5）
    [Serializable]
    public class 抗性配置
    {
        public string 类型;        // "物理"/"魔法"/"真实"
        public 伤害类型 类型枚举 => 数据解析.枚举<伤害类型>(类型);
        public float 倍率 = 1f;
    }

    // 敌人 AI 行动项：行动 + 权重 + 可选条件
    [Serializable]
    public class AI行动项
    {
        public string 行动;      // "普攻" 或 技能标识
        public int 权重 = 1;
        public string 条件;      // 可选："生命<30" 等；空=无条件
    }

    // 敌人组（encounters.json）：战斗发起的敌人组合
    [Serializable]
    public class 敌人组项 { public string 敌人; public int 数量 = 1; }
    [Serializable]
    public class 敌人组数据 { public string 标识; public string 名称; public 敌人组项[] 敌人; }
    [Serializable]
    public class 敌人组根 { public 敌人组数据[] 敌人组; }

    // ================= 技能 =================

    [Serializable]
    public class 技能数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public int 消耗魔力;
        public string 类别;       // JSON中文名："攻击"/"治疗"/"增益"/"减益"/"控制"
        public 技能类别 类别枚举 => 数据解析.枚举<技能类别>(类别);
        public string 目标;       // "敌方单体"/"敌方全体"/"我方单体"/"我方全体"/"自己"
        public 目标类型 目标枚举 => 数据解析.枚举<目标类型>(目标);
        public string 伤害类型;   // "物理"/"魔法"/"真实"
        public 伤害类型 伤害类型枚举 => 数据解析.枚举<伤害类型>(伤害类型);
        public int 数值;          // 攻击=倍率；治疗=恢复量；增益/减益/控制=效果值
        public string 挂载Buff;   // 增益/减益/控制 挂载的buff标识
        public int 冷却;          // 使用后冷却回合（0=无）
        public int 价格;          // 训练场学习费用
        public string 品质;       // "普通"/"优秀"/"稀有"...（JsonUtility 不认枚举名，字符串+转换）
        public 品质 品质档 => 数据解析.枚举<品质>(品质);
        public int 需要体力, 需要力量, 需要智力, 需要敏捷;   // 学习/释放前提（0=无要求）
        public int 熟练伤害加成 = 10;   // 每级 +% 伤害
        public int 熟练消耗减少 = 5;    // 每级 -% 消耗
        public int 熟练度每级 = 100;    // 每级熟练度阈值（累积满升级熟练等级）
    }

    // 技能掌握：玩家已学技能 + 熟练度（使用/训练累积，满阈值升级熟练等级）
    [Serializable]
    public class 技能掌握
    {
        public string 标识;
        public int 熟练等级 = 1;
        public int 熟练度 = 0;   // 当前级内熟练度进度
        public 技能掌握() { }
        public 技能掌握(string 标识) { this.标识 = 标识; }
    }

    [Serializable]
    public class 技能根 { public 技能数据[] 技能; }

    // ================= 训练项目 =================

    // 训练项目：训练场属性训练（增强 4 大基础属性）
    [Serializable]
    public class 训练项目
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public string 属性;       // "体力"/"力量"/"智力"/"敏捷"
        public int 加成;          // 每次 +N
        public int 花费;
    }

    [Serializable]
    public class 训练项目根 { public 训练项目[] 训练项目; }

    // ================= 任务 =================

    [Serializable]
    public class 任务数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public string 目标类型;   // "击败" / "获得物品"
        public string 目标标识;
        public int 目标数量;
        public int 奖励金币;
        public int 奖励经验;
        public string 奖励物品;   // 可空
    }

    [Serializable]
    public class 任务根 { public 任务数据[] 任务; }

    // ================= 地图 =================

    // 地图节点：小地图内的节点（入口/设施/空）。任何节点都可选挂「内部」（NPC+功能物图）。
    [Serializable]
    public class 地图节点
    {
        public string 标识;
        public string 名称;
        public string 类型;       // "入口"（双击离开城镇）/ "设施"（挂设施标识）/ "空"（普通点，靠内部交互）
        public string 设施;       // 类型=设施 时的设施标识（内部功能物靠它提供功能逻辑）
        public string 目标;       // 保留字段（旧剧情入口），暂不读取
        public float x;           // 0~100 归一化坐标
        public float y;
        public string[] 连接;     // 相邻节点标识
        public 设施内部节点[] 内部; // 可选：该节点的内部图（NPC + 功能物 节点）
    }

    // 大地图地点（节点）：城镇/荒野
    [Serializable]
    public class 地图地点
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public float x;           // 0~100 归一化坐标
        public float y;
        public string 类型;       // "城镇" / "荒野"
        public string 区域;       // 类型=荒野 时关联的区域标识（探索用）
        public string 入口节点;   // 类型=城镇 时进入小地图的起始节点
        public string 目标;       // 剧情节点标识；或 "探索:区域标识"
        public string 解锁物品;   // 需要持有才能前往
        public string[] 连接;     // 相邻大地图节点标识
        public 地图节点[] 小地图; // 类型=城镇 时的内部节点图（设施网络）
    }

    [Serializable]
    public class 地图根 { public 地图地点[] 地点; }

    // ================= 区域（探索系统：深度分层） =================

    // 区域遭遇项：敌人组 + 出现权重 + 遭遇形态（空=遇见 [战斗][逃跑]；"被偷袭"=敌方先手强制战；"偷袭"=我方先手可选）
    [Serializable]
    public class 遭遇项 { public string 敌人; public int 权重; public string 形态; }

    // 区域发现项：地点剧情节点 + 权重
    [Serializable]
    public class 发现项 { public string 节点; public int 权重; }

    // 区域资源项：效果 + 描述 + 权重 + 获取消耗精力（[是]=扣精力+获得 / [否]=略过）
    [Serializable]
    public class 资源项 { public 剧情效果 效果; public string 文本; public int 权重; public int 消耗精力 = 1; }

    // 选择选项：选择类事件的一个选项（战斗 / 效果 / 进剧情节点 三选一或组合）
    [Serializable]
    public class 选择选项
    {
        public string 文本;
        public string 战斗;        // 可选：敌人组标识（遭遇）
        public 剧情效果 效果;      // 可选：获得/损失（资源）
        public string 节点;        // 可选：剧情节点（发现）
    }

    // 选择事件：宝箱陷阱等多分支事件（选项走 战斗/效果/节点）
    [Serializable]
    public class 选择事件 { public string 文本; public 选择选项[] 选项; }

    // 探索事件表：一层（或岔路一侧）的随机事件池
    [Serializable]
    public class 探索事件表
    {
        public 遭遇项[] 遭遇;
        public 资源项[] 资源;
        public 发现项[] 发现;
        public string[] 无事文本;
        public 选择事件[] 选择;
    }

    // 岔路数据：进入层先选路，锁定一侧事件表
    [Serializable]
    public class 岔路数据
    {
        public string 文本;
        public 探索事件表 安全;
        public 探索事件表 危险;
    }

    // 探索层：事件表 或 岔路 或 Boss 层（Boss 通关后普通化，用 通关后 事件表）
    [Serializable]
    public class 探索层数据
    {
        public int 编号;
        public string 描述;
        public int 搜索阈值 = 3;       // 搜索满后通路进入候选
        public float 通路概率 = 0.5f;  // 之后每次搜索发现通路的概率
        public string 通路文本;        // 可选：发现通路时的事件文本（缺省通用文案）
        public 探索事件表 事件表;      // 普通层
        public 岔路数据 岔路;          // 岔路层（优先于 事件表）
        public string Boss;            // Boss 层：敌人组标识（优先于 事件表）
        public 探索事件表 通关后;      // Boss 层通关后的事件表（缺省=无事）
    }

    // 区域：探索的舞台，层[] 数据驱动（数组长度 = 层数，底层 Boss）
    [Serializable]
    public class 区域数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public int 危险度;        // 1 低 / 2 中 / 3 高
        public string 通关文案;
        public 探索层数据[] 层;
    }

    [Serializable]
    public class 区域根 { public 区域数据[] 区域; }

    // ================= 设施 =================

    // 节点内部节点：内部图节点（NPC 或 功能物）。NPC 就是 类型=NPC 的节点，无单独数组。
    [Serializable]
    public class 设施内部节点
    {
        public string 标识;
        public string 名称;
        public string 类型;       // "NPC" / "功能物"
        public string 功能;       // 类型=功能物 时：教学/买卖/训练/任务/恢复/睡觉
        public string 剧情节点;   // 类型=NPC 时：交谈进入的剧情节点
        public float x;           // 0-100 归一化坐标
        public float y;
        public string[] 连接;     // 相邻节点标识
    }

    // 设施定义：逻辑类型为 C# 类名（设施工厂反射实例化）。内部已归地图节点所有。
    [Serializable]
    public class 设施定义
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public string 逻辑类型;
        public string 视图;
        public string 数据;       // 可选：该设施自己的数据文件
        public string[] 地点;     // 挂接的地点标识
    }

    [Serializable]
    public class 设施根 { public 设施定义[] 设施; }
