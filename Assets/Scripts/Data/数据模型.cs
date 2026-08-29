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
        public int 饱食;       // 正=进食（饱食上升）；负=饥饿（下降）
        public int 水分;       // 正=饮水（水分上升）；负=脱水（下降）
        public int 疲劳;       // 正=增加疲劳；负=恢复
        public int 中毒;       // 正=中毒加深；负=解毒
        public int 感冒;       // 正=受寒加重；负=治疗
        public int 流血;       // 正=流血加重；负=包扎
        public int 骨折;       // 正=骨折；负=固定恢复
        public int 发烧;       // 正=发烧；负=退烧
        public int 金币;
        public int 经验;
        public string 获得物品;
        public int 获得数量;   // 获得物品的数量（缺省按 1；JsonUtility 缺失=0，结算时兜底 1）
        public string 失去物品;
        public string 学习技能;   // 剧情传授技能
        public string 接取任务;   // 剧情接取任务
        public 物品获得项[] 获得物品表;
        public 物品失去项[] 失去物品表;

        // 按伤病类型取变化值（效果结算用）
        public int 伤病变化(伤病类型 类型)
        {
            switch (类型)
            {
                case 伤病类型.疲劳: return 疲劳;
                case 伤病类型.中毒: return 中毒;
                case 伤病类型.感冒: return 感冒;
                case 伤病类型.流血: return 流血;
                case 伤病类型.骨折: return 骨折;
                case 伤病类型.发烧: return 发烧;
                default: return 0;
            }
        }
    }

    // 剧情效果的多物品项
    [Serializable]
    public class 物品获得项 { public string 标识; public int 数量 = 1; }
    [Serializable]
    public class 物品失去项 { public string 标识; public int 数量 = 1; }

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
        public string 主线阶段;   // 非空则进入该节点时更新玩家主线阶段（剧情推进标记）
        public string 下一节点;   // 选项为空时自动进入的节点（剧情链连续播放）
        public 剧情选项[] 选项;
    }

    [Serializable]
    public class 剧情根 { public 剧情节点[] 节点; public 区域剧情路由[] 区域剧情; }

    // 区域剧情路由：进入某地点（城镇/荒野）时按当前主线阶段自动触发剧情（防重复靠阶段前进）
    [Serializable]
    public class 区域剧情路由
    {
        public string 区域;       // 地图地点标识（城镇/荒野）
        public string 阶段;       // 命中所需的 玩家档案.主线阶段
        public string 节点;       // 命中后进入的剧情节点
        public string 需要物品;   // 可选：需持有该物品才命中
        public string 需要任务;   // 可选：需该任务已完成才命中
    }

    // ================= 敌人 =================

    [Serializable]
    public class 敌人数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public int 生命;
        public int 攻击;
        public int 防御;          // 物防（敌人护盾 = 防御 × 护盾系数）
        public int 魔防;          // 魔防
        public int 速度;          // 行动顺序（速度队列）
        public int 等级 = 1;      // 敌人等级
        public 抗性配置[] 抗性;   // 抗性表：伤害类型→倍率
        public AI行动项[] 行动表; // AI行动表
        public string 目标策略;   // 攻击目标策略：随机 / 残血 / 低防
        public int 货币奖励;
        public int 经验奖励;
        public string 掉落物品;   // 可选掉落物品标识
        public float 掉落概率;    // 0~1
        // —— 弱点/护盾（破防机制） ——
        public string 五行属性;    // "金"/"木"/"水"/"火"/"土"/"无"；魔法弱点 = 技能五行克制敌人五行
        public 五行 五行枚举 => 数据解析.枚举<五行>(五行属性);
        public string[] 物理弱点;  // 武器种类数组（"剑"/"匕首"…）；命中该类武器 = 物理弱点
        public float 护盾系数 = 2f; // 护盾 = 防御 × 系数（默认 2）
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
        public string 类型;       // 末日：武器/防具/食物/水/医疗品/弹药/材料/技能书/任务品
        public string 种类;       // 材料细分（塔科夫式，仅 类型=材料 时使用）：其他/医疗用品/建筑材料/日常用品/工具/易燃物品/电子产品/能源物品/贵重物品/情报物品
        public int 恢复量;        // 食物/水/医疗品 的恢复值（食物→饱食、水→水分、医疗→生命）
        public string 恢复目标;   // "饱食"/"水分"/"生命"/"疲劳"/"中毒"/"感冒"/"流血"/"骨折"/"发烧"
        public int 攻击加成;      // 类型=武器（近战吃力量；远程固定伤害即此值）
        public int 防御加成;      // 类型=防具
        public int 生命加成;      // 类型=防具：生命上限加成
        public int 负重加成;      // 类型=防具/背包：负重上限加成
        public int 抗性;          // 类型=防具：抗性百分数贡献点
        public string 槽位;       // 装备类槽位（"主手"/"副手"/"头部"/"胸部"/"腿部"/"脚部"/"手部"/"弹挂"/"腰封"/"背包"）
        public string 武器种类;    // 类型=武器（"刀"/"斧"/"棍棒"/"匕首"/"弓"/"弩"/"手枪"/"步枪"/"霰弹枪"）
        public 武器种类 武器种类枚举 => 数据解析.枚举<武器种类>(武器种类);
        public string 近程远程;   // "近战"/"远程"（战斗伤害来源区分）
        public string 技能;       // 类型=技能书 时授予的技能标识
        // —— 网格背包 ——
        public int 形状宽 = 1;    // 网格占用宽
        public int 形状高 = 1;    // 网格占用高
        public int 重量 = 1;      // 负重占用
        public int 堆叠上限;      // 单格堆叠上限（0/缺省 = 不可堆叠，每格恒 1 件；武器/防具/任务品=1，弹药/消耗品/材料>1）
        public string 图片;       // 物品图标引用（Resources 名/子精灵名；空 = 无图，内容层显示色块）
        public int 最大耐久;      // 武器/防具最大耐久（0 = 无耐久，不损坏）
        // —— 容器（塔科夫式嵌套容器） ——
        public bool 是容器;       // 是否为容器（背包/弹药箱/医疗包/保险箱...）
        public int 容器列;        // 容器内部网格列数（0 = 非容器）
        public int 容器行;        // 容器内部网格行数
        public string 容器允许类型;  // 可选：仅允许某类物品进入（"弹药"/"医疗品"/"材料"）；空 = 任意
        public 容器形状块[] 容器形状;  // 容器内部形状（塔科夫式：多个小矩形拼合的并集，非"整矩形挖洞"）。
                                     // 每块 {列,行,宽,高} 为一块可用矩形；null/空 = 整矩形全部可用。
                                     // 例：弹挂 4×2 总面积 8 = 4 块 1×2 竖条拼合。
                                     // 兼容：旧数据可用 容器形状字符串掩码（'1'=可用）表达，本字段优先。
        public string[] 容器形状掩码;  // 兼容旧定义：每行一个字符串，'1'=可用格、'0'=空洞；容器形状 非空时忽略本字段
        // —— 以物易物 ——
        public int 价值 = 1;      // 1~100 价值点数
        public int 价格 => 价值;  // 兼容旧引用（原"价格"字段语义）
        // —— 品质与战斗内使用 ——
        public string 品质;       // "普通"/"优秀"/"稀有"...（JsonUtility 不认枚举名，字符串+转换）
        public 品质 品质档 => 数据解析.枚举<品质>(品质);
        public bool 战斗内使用;         // 是否可在战斗中使用（医疗品/弹药）
        public string 使用目标;         // "我方单体"/"敌方单体"…
        public 目标类型 使用目标枚举 => 数据解析.枚举<目标类型>(使用目标);
        public string 使用效果;         // "恢复"/"增益"/"减益"
        public 效果类型 使用效果枚举 => 数据解析.枚举<效果类型>(使用效果);
        public string 挂载Buff;         // 增益/减益挂载的buff标识
        // —— 宝石（保留，末日可作稀有材料） ——
        public string 指定词缀属性;
        public 词缀属性 指定词缀属性枚举 => 数据解析.枚举<词缀属性>(指定词缀属性);
    }

    [Serializable]
    public class 物品根 { public 物品数据[] 物品; }

    // 容器形状块：一块可用矩形（塔科夫式容器内部形状由多块拼合）。坐标 0 基，从容器内部左上角起算。
    [Serializable]
    public class 容器形状块
    {
        public int 列;   // 块左上角列（0 基）
        public int 行;   // 块左上角行（0 基）
        public int 宽;   // 块宽（格）
        public int 高;   // 块高（格）
        public 容器形状块() { }
        public 容器形状块(int 列, int 行, int 宽, int 高) { this.列 = 列; this.行 = 行; this.宽 = 宽; this.高 = 高; }
    }

    // ================= 词缀（暗黑式随机装备词条） =================

    // 词缀可作用的属性派系（攻击=物攻、魔攻=独立、暴击/命中/闪避/速度 为战斗副属性；负重/潜行 为末日生存副属性）
    public enum 词缀属性 { 攻击, 防御, 生命, 抗性, 暴击, 命中, 闪避, 速度, 魔攻, 负重, 潜行 }

    // 词缀定义（affixes.json）：一条随机词条的模板（品质 + 属性 + 数值区间 + 生成权重）。
    // 品质档 = 词缀自带的 6 档（普通~传奇，复用 品质 枚举）：数值强度/出现概率/装备限档/显示色 均由此驱动。
    [Serializable]
    public class 词缀定义
    {
        public string 标识;
        public string 名称;
        public string 品质;        // "普通"/"优秀"/"稀有"/"史诗"/"英雄"/"传奇"
        public 品质 品质档 => 数据解析.枚举<品质>(品质);
        public string 文本;        // 展示语（如 "攻击 +N"，N 为实际值），可为空则自动拼
        public string 属性;        // "攻击"/"防御"/"生命"/"抗性"/"暴击"/"命中"/"闪避"/"速度"/"魔攻"
        public 词缀属性 属性枚举 => 数据解析.枚举<词缀属性>(属性);
        public int 最小;           // 数值区间（含）
        public int 最大;
        public float 权重 = 1f;    // 池内抽取权重（高品质词缀权重低 → 更难出现）
    }
    [Serializable]
    public class 词缀根 { public 词缀定义[] 词缀; }

    // 词缀实例：装备随机生成的词条（标识 + 已掷定的数值），随 物品堆叠/装备记录 存档
    [Serializable]
    public class 词缀条
    {
        public string 标识;
        public int 数值;
        public 词缀条() { }
        public 词缀条(string 标识, int 数值) { this.标识 = 标识; this.数值 = 数值; }
    }

    // ================= 战斗系统 =================

    // 伤害类型：近战/远程/真实（末日：物理→近战，魔法→远程）
    // 注："魔法" 成员仅作旧代码编译兼容（映射远程），战斗系统改造后移除
    public enum 伤害类型 { 物理, 远程, 魔法 = 远程, 真实 }
    // 武器种类：末日武器（近战：刀/斧/棍棒/匕首；远程：弓/弩/手枪/步枪/霰弹枪）
    public enum 武器种类 { 无, 刀, 斧, 棍棒, 匕首, 弓, 弩, 手枪, 步枪, 霰弹枪 }
    // 武器规则：通用（所有武器可用）/ 专向（仅指定武器可用）/ 弱向（指定武器伤害↑，其余武器可用但伤害↓）
    public enum 武器规则 { 通用, 专向, 弱向 }
    // 行动目标类型：决定目标选择集合
    public enum 目标类型 { 敌方单体, 敌方全体, 敌方两名, 我方单体, 我方全体, 自己 }
    // 技能类别
    public enum 技能类别 { 攻击, 治疗, 增益, 减益, 控制, 净化 }
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
        public string 结算时机;   // 异常持续伤害："开始"=回合开始扣血（缺省）/ "结束"=回合结束扣血
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
    public class 敌人组根 { public 敌人组数据[] 敌人组; public 助战组数据[] 助战组; }

    // 助战组（encounters.json）：剧情战斗的我方伙伴组合（与敌方「敌人组」语义分离，用「成员」而非「敌人」）
    [Serializable]
    public class 助战组项 { public string 标识; public int 数量 = 1; }
    [Serializable]
    public class 助战组数据 { public string 标识; public string 名称; public 助战组项[] 成员; }

    // ================= 技能 =================

    // 技能伤害方式：攻击技能的计算方式
    // 倍率 = 攻 × 数值（现有公式，受品质/熟练/防御/暴击/抗性影响）
    // 固定 = 直接打出 数值 点伤害（不受任何因素影响）
    // 附加 = 普攻伤害 + 数值（在角色伤害值上追加固定量）
    public enum 技能伤害方式 { 倍率, 固定, 附加 }

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
        public string 伤害方式;   // "倍率" / "固定" / "附加"（缺省=倍率）
        public string 派系;       // 物理 / 魔法（缺省：攻击+物理伤害=物理，其余=魔法）；决定消耗 精力/魔力
        public bool 物理派系 => 派系 == "物理" || (string.IsNullOrEmpty(派系) && 类别枚举 == 技能类别.攻击 && 伤害类型 == "物理");
        public 技能伤害方式 伤害方式枚举 => 数据解析.枚举<技能伤害方式>(伤害方式);
        public float 数值;        // 倍率=倍率系数（重击=1.5 / 横扫斩=0.8）；固定=固定伤害值；附加=追加伤害量；治疗=恢复量
        public string 挂载Buff;   // 增益/减益/控制 挂载的buff标识
        public int 冷却;          // 使用后冷却回合（0=无）
        public int 价格;          // 训练场学习费用
        public string 品质;       // "普通"/"优秀"/"稀有"...（JsonUtility 不认枚举名，字符串+转换）
        public 品质 品质档 => 数据解析.枚举<品质>(品质);
        public int 需要体力, 需要力量, 需要智力, 需要敏捷, 需要意志;   // 学习/释放前提（0=无要求）
        public int 熟练伤害加成 = 10;   // 每级 +% 伤害
        public int 熟练消耗减少 = 5;    // 每级 -% 消耗
        public int 熟练度每级 = 100;    // 每级熟练度阈值（累积满升级熟练等级）
        // —— 武器规则 / 五行（弱点/破防机制） ——
        public string 武器种类;   // 物理技能的所需武器种类（专向/弱向 判定用）
        public 武器种类 武器种类枚举 => 数据解析.枚举<武器种类>(武器种类);
        public string 武器规则;   // "通用" / "专向" / "弱向"（缺省=通用）
        public 武器规则 武器规则枚举 => 数据解析.枚举<武器规则>(武器规则);
        public string 五行属性;   // 魔法技能的五行（"金"/"木"/"水"/"火"/"土"；魔法弱点判定）
        public 五行 五行枚举 => 数据解析.枚举<五行>(五行属性);
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
        public string 章节;       // 主线归属章节标题（如 "第一章 · 黑石山的阴影"）；空=非主线（支线/杂项，不按章分组）
        public string 目标类型;   // "击败" / "获得物品" / "主线阶段"
        public string 目标标识;
        public int 目标数量;
        public int 奖励金币;
        public int 奖励经验;
        public string 奖励物品;   // 可空
    }

    [Serializable]
    public class 任务根 { public 任务数据[] 任务; }

    // ================= 日常任务（悬赏） =================

    // 日常任务：各行各业的告示板/委托栏按游戏内天数随机生成的一批（与系统任务面板「日常」同批共用）。
    // 范围：装备（铁匠铺） / 食物（厨房） / 药剂（药房） / 通用（任务板、镇长府；可击杀或收集）。
    // 目标类型：击杀 / 收集 —— 均需回对应设施委托栏互动「提交」结算发奖（收集扣道具）。
    [Serializable]
    public class 日常任务
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public string 范围;       // "装备" / "食物" / "药剂" / "通用"
        public string 目标类型;   // "击杀" / "收集"
        public string 目标标识;   // 敌人标识（击杀） / 物品标识（收集）
        public int 目标数量;
        public int 进度;          // 击杀=已击杀数；收集=展示用（提交校验读背包实际持有数）
        public int 奖励金币;
        public int 奖励经验;
        public bool 已接取;       // 已在悬赏板认领（接取后才在系统任务面板「日常」栏可见）
        public bool 已领取;       // 已结算
    }

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
        public string 解锁阶段;   // 需要主线阶段达到才能前往（空=无限制）
        public string 解锁提示;   // 可选：未解锁时的提示文本
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

    // ================= 制作（recipes.json） =================

    // 配方材料项：物品 + 数量
    [Serializable]
    public class 配方材料 { public string 物品; public int 数量 = 1; }

    // 配方：根据材料制作新物品（铁匠铺=装备 / 厨房=食物 / 药房=药剂）；图纸物品解锁
    [Serializable]
    public class 配方数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public string 产物;          // 成品物品标识
        public int 产物数量 = 1;
        public 配方材料[] 材料;      // 所需材料（全部凑齐才能制作）
        public string 类型;          // "装备" / "食物" / "药剂"（对应设施筛选）
        public string 图纸;          // 所需图纸物品标识（持有才解锁显示/制作；空=无需图纸）
    }

    [Serializable]
    public class 配方根 { public 配方数据[] 配方; }

    // ================= 职业（开局选择） =================

    // 职业属性分布项（五维其一 + 值）——职业直接给定五维分布（总和 25），非加成
    [Serializable]
    public class 属性加成项 { public string 属性; public int 点数; }

    // 职业初始装备项
    [Serializable]
    public class 初始装备项 { public string 标识; public int 数量 = 1; }

    [Serializable]
    public class 职业数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public 属性加成项[] 属性分布;   // 职业五维分布（直接给值，总和 25，区分职业）
        public string 初始技能;          // 初始学会的生存技能（可空）
        public string 天赋;             // 职业固有天赋特效（可空，字符串标识）
        public 初始装备项[] 初始装备;    // 开局装备/物资
        public string 开局文本;          // 开局叙事（第一段文字）
    }

    [Serializable]
    public class 职业根 { public 职业数据[] 职业; }

    // ================= 天赋（正负特质，PZ 式） =================

    // 天赋效果项：作用于属性/状态（点数字段 + 数值）
    [Serializable]
    public class 天赋效果项
    {
        public string 目标;    // "体质"/"力量"/"智慧"/"敏捷"/"意志"/"饱食"/"水分"/"经验"/"医疗"/"制作"/"夜晚"
        public float 数值;     // 属性=加减点数；状态=百分比修正
    }

    [Serializable]
    public class 天赋数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public int 点数;       // 正面=花费（正数）；负面=返还（负数）
        public 天赋效果项[] 效果;
    }

    [Serializable]
    public class 天赋根 { public 天赋数据[] 天赋; }

    // ================= 天气（每日随机） =================

    [Serializable]
    public class 天气数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public int 权重;       // 随机权重（季节可调）
        public string 效果文本; // 探索/战斗时的效果描述
    }

    [Serializable]
    public class 天气根 { public 天气数据[] 天气; }

    // ================= 安全屋家具 =================

    [Serializable]
    public class 家具数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public int 解锁等级;   // 安全屋达到该等级才可建造
        public int 价格;       // 建造所需价值点数（以物易物材料）
        public 配方材料[] 材料; // 建造所需材料
        public int 最大等级;   // 家具可升级到的等级
    }

    [Serializable]
    public class 家具根 { public 家具数据[] 家具; }

    // ================= 搜索容器（塔科夫式搜刮） =================
    // 层级：地图类型（居民房）→ 房间（玄关/客厅/厨房…）→ 容器（鞋柜/冰箱…）。
    // 搜刮 = 打开 容器：首次打开按 搜索表 权重随机生成物品（复用 背包服务 网格算法）；
    // 容器级 搜索时间（黑布倒计时）+ 物品级 搜索时间（每件物品的黑块倒计时；0 = 按价值推导）。

    [Serializable]
    public class 搜索地图类型
    {
        public string 标识;   // "居民房"
        public string 名称;
        public 搜索房间[] 房间;
    }

    [Serializable]
    public class 搜索房间
    {
        public string 标识;   // "玄关"
        public string 名称;
        public 搜索容器[] 容器;
    }

    [Serializable]
    public class 搜索容器
    {
        public string 标识;        // "鞋柜"
        public string 名称;
        public string 描述;
        public int 容器列 = 3;     // 网格宽（格）
        public int 容器行 = 3;     // 网格高（格）
        public 容器形状块[] 容器形状;  // 可选：拼合形状（空 = 整矩形）
        public string 容器允许类型;    // 可选：仅允许该类型物品进入（如 "医疗品"）；空 = 任意
        public float 搜索时间 = 5f;    // 容器级 搜索秒数（整体黑布倒计时）
        public 搜索条目[] 搜索表;      // 随机生成表
    }

    [Serializable]
    public class 搜索条目
    {
        public string 物品标识;
        public int 权重 = 1;       // 出现权重（越大越常见）
        public int 数量最小 = 1;
        public int 数量最大 = 1;
        public float 搜索时间;     // 物品级 搜索秒数（0 = 按价值推导）
    }

    [Serializable]
    public class 搜索地图类型根 { public 搜索地图类型[] 地图类型; }

