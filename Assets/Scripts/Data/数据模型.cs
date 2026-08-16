using System;

    // 全部为 [Serializable] 纯数据类，字段名与旧 数据/数据模型.cs 一致，便于直接反序列化现有 JSON。

    // ================= 剧情 =================

    // 剧情效果：作用于玩家状态（正=获得，负=损失）
    [Serializable]
    public class 剧情效果
    {
        public int 生命;
        public int 魔力;
        public int 金币;
        public int 经验;
        public string 获得物品;
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
        public int 防御;
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
        public string 类型;       // "恢复" 消耗品 / "任务" 任务物品 / "武器" / "防具" / "技能书"
        public int 恢复量;        // 类型=恢复 时的恢复值（受品质倍率影响）
        public int 攻击加成;      // 类型=武器（受品质倍率影响）
        public int 防御加成;      // 类型=防具（受品质倍率影响）
        public string 技能;       // 类型=技能书 时授予的技能标识
        public int 价格;          // 商店买卖价格（0=不可买卖）
        public 品质 品质;         // 品质（默认普通，效果按倍率放大）
    }

    [Serializable]
    public class 物品根 { public 物品数据[] 物品; }

    // ================= 技能 =================

    [Serializable]
    public class 技能数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public int 消耗魔力;
        public string 类型;       // "重击" 物理倍率伤害 / "魔法" 魔法倍率伤害 / "防御" 减伤 / "回血" 恢复
        public int 数值;          // 伤害类=倍率；回血=恢复量
        public int 价格;          // 训练场学习费用
        public 品质 品质;         // 品质（基础伤害按倍率放大）
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

    // 地图节点：小地图内的节点（空地/设施/剧情/入口）。任何节点都可选挂「内部」（NPC+功能物图）。
    [Serializable]
    public class 地图节点
    {
        public string 标识;
        public string 名称;
        public string 类型;       // "空地" / "设施" / "剧情" / "入口"
        public string 设施;       // 类型=设施 时的设施标识（内部功能物靠它提供功能逻辑）
        public string 目标;       // 类型=剧情 时的剧情节点标识（可选）
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

    // ================= 区域 =================

    // 区域遭遇项：敌人 + 出现权重
    [Serializable]
    public class 遭遇项 { public string 敌人; public int 权重; }

    // 区域发现项：地点剧情节点 + 权重
    [Serializable]
    public class 发现项 { public string 节点; public int 权重; }

    // 区域资源项：效果 + 描述 + 权重
    [Serializable]
    public class 资源项 { public 剧情效果 效果; public string 文本; public int 权重; }

    // 区域：探索的舞台，含遭遇/发现/资源事件表
    [Serializable]
    public class 区域数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public int 危险度;        // 1 低 / 2 中 / 3 高
        public 遭遇项[] 遭遇;
        public 发现项[] 发现;
        public 资源项[] 资源;
        public string[] 无事文本;
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
