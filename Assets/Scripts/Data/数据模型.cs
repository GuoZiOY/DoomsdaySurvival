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
        public int 防御;          // 减伤百分比（1 点 = 1%，封顶 50%）
        public int 速度;          // 行动条速率（每游戏分钟填充点数）
        public int 等级 = 1;      // 敌人等级
        public AI行动项[] 行动表; // AI行动表
        public string 目标策略;   // 攻击目标策略：随机 / 残血 / 低防
        public int 货币奖励;
        public int 经验奖励;
        public string 掉落物品;   // 可选掉落物品标识
        public float 掉落概率;    // 0~1
        // —— 战斗沙盒（行动条即时制 + 棋盘） ——
        public string 移动AI;       // 自动移动策略："冲锋"（向玩家逼近）/ "风筝"（保持攻击距离边缘）/ "驻守"（原地不动）；空 = 冲锋
        public int 攻击距离 = 1;    // 攻击射程（格）：近战 1、远程 >1（决定何时进入攻击范围自动停下）
        public int 身形 = 1;        // 占格宽度（1 = 单格；2 = 横向占 2 格的大体型）
    }

    [Serializable]
    public class 敌人根 { public 敌人数据[] 敌人; }

    // ================= 物品 =================

    [Serializable]
    public class 物品数据
    {
        public string 标识;       // 唯一键（存档/配方/搜索引用）+ 显示名 + 图标引用（三合一：显示/图标 均用 标识）
        public string 描述;       // 物品描述（信息面板/详情 显示）
        public string 类型;       // 末日 九大类：武器/防具/饮食/医疗/容器/材料/弹药/技能书/任务品
        public string 种类;       // 二级分类（按 类型 解释）：饮食=食物(固体)/饮品(液体)；医疗=恢复(回血)/创伤(止血·骨折·手术)/治愈(疾病)；
                                  //   材料=医疗用品/建筑材料/日常用品/工具/燃料物品(燃烧供能)/能源产品(供电)/电子产品/贵重物品/情报物品/食物原料/图纸/其他
        public int 恢复量;        // 饮食/医疗 的恢复值（饮食→饱食/水分、医疗→生命/伤病）
        public string 恢复目标;   // "饱食"/"水分"/"生命"/"疲劳"/"中毒"/"感冒"/"流血"/"骨折"/"发烧"
        public int 攻击加成;      // 类型=武器（近战吃力量；远程固定伤害即此值）
        public int 防御加成;      // 类型=防具
        public int 生命加成;      // 类型=防具：生命上限加成
        public int 负重加成;      // 类型=防具/背包：负重上限加成
        public int 抗性;          // 类型=防具：抗性百分数贡献点
        public string 槽位;       // 装备类槽位（"主手"/"副手"/"头部"/"胸部"/"腿部"/"脚部"/"手部"/"弹挂"/"腰封"/"背包"）
        public string 武器种类;    // 类型=武器（"刀"/"斧"/"棍棒"/"匕首"/"弓"/"弩"/"手枪"/"步枪"/"霰弹枪"）
        public 武器种类 武器种类枚举 => 数据解析.枚举<武器种类>(武器种类);
        public int 攻击距离;      // 攻击射程（格）：1=近战（吃力量加成）、>1=远程（武器固定伤害）；0 = 按 1
        public string 套装;       // 套装体系（防具/武器 按 职业/群体 成套）："工装"/"户外"/"医生"/"安保"/"警察"/"消防"/"军用"；空 = 散件
        public string 技能;       // 类型=技能书 时授予的技能标识（书籍·技能书 复用此字段指向 技能）
        // —— 书籍系统（类型="书籍"） ——
        public string 书籍种类;   // 书籍子类："技能书" / "配方书" / "蓝图"（空 = 非书籍）
        public string 书籍级别;   // 书籍难度档："低" / "中" / "高" / "顶" / "传奇"（决定 成功率/可读次数/每次耐久消耗）
        public int 阅读时间分;    // 阅读 所需 游戏分钟（读满 才 判定 习得）
        public string[] 配方池;   // 配方书：可习得 的 配方标识 集合（按权重 抽 1 个；已习得 降权）
        public int 熟练度点;      // 技能书：已学会 时 每 读满 一次 增 加 的 熟练度（0 = 用 默认：每级阈值一半）
        // —— 网格背包 ——
        public int 形状宽 = 1;    // 网格占用宽
        public int 形状高 = 1;    // 网格占用高
        public int 重量 = 1;      // 负重占用
        public int 堆叠上限;      // 单格堆叠上限（0/缺省 = 不可堆叠，每格恒 1 件；武器/防具/任务品=1，弹药/消耗品/材料>1）
        public int 最大耐久;      // 武器/防具最大耐久（0 = 无耐久，不损坏）
        public int 保质期;        // 食物/食材 保质期（游戏小时；0 = 永不变质）——腐坏系统用
        // —— 作物（种植箱：种子 → 成熟 产物 替换）——
        public int 生长时间;       // 种子 → 成熟 所需 游戏小时（0 = 非种子/不可种）
        public string 成熟产物;    // 成熟后 替换 为 的 物品标识（空 = 不可种）
        // —— 容器（塔科夫式嵌套容器） ——
        public bool 是容器;       // 是否为容器（背包/弹药箱/医疗包/保险箱...）
        public int 容器列;        // 容器内部网格列数（0 = 非容器）
        public int 容器行;        // 容器内部网格行数
        public string 容器允许类型;  // 可选：仅允许某类物品进入（"弹药"/"医疗"/"材料"）；空 = 任意
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

    // 词缀可作用的属性派系（攻击=近战/远程通用、暴击/命中/闪避/速度 为战斗副属性；负重/潜行 为末日生存副属性）
    public enum 词缀属性 { 攻击, 防御, 生命, 抗性, 暴击, 命中, 闪避, 速度, 负重, 潜行 }

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

    // 伤害类型：物理（近战）/ 远程（枪械弓弩）/ 真实（持续伤害·固定伤，无视防御）
    public enum 伤害类型 { 物理, 远程, 真实 }
    // 武器种类：末日武器（近战：刀/斧/棍棒/匕首；远程：弓/弩/手枪/步枪/霰弹枪）
    public enum 武器种类 { 无, 刀, 斧, 棍棒, 匕首, 弓, 弩, 手枪, 步枪, 霰弹枪 }
    // 武器规则：通用（所有武器可用）/ 专向（仅指定武器可用）/ 弱向（指定武器伤害↑，其余武器可用但伤害↓）
    public enum 武器规则 { 通用, 专向, 弱向 }
    // 行动目标类型：决定目标选择集合
    public enum 目标类型 { 敌方单体, 敌方全体, 敌方两名, 我方单体, 我方全体, 自己 }
    // 技能类别
    public enum 技能类别 { 攻击, 治疗, 增益, 减益, 控制, 净化 }
    // Buff 类型
    public enum Buff类型 { 增益, 减益, 异常 }
    // 物品使用效果
    public enum 效果类型 { 恢复, 增益, 减益 }

    // Buff 定义（buffs.json）：增益/减益/异常 的通用定义
    [Serializable]
    public class Buff定义
    {
        public string 标识;
        public string 名称;
        public string 类型;        // "增益"/"减益"/"异常"
        public Buff类型 类型枚举 => 数据解析.枚举<Buff类型>(类型);
        public string 属性;        // 增益/减益作用属性："攻击"/"防御"/"速度"（异常留空）
        public int 数值;          // 增益/减益=±百分比(20=±20%)；异常=每轮伤害
        public int 持续回合 = 1;   // 增益/减益/异常 持续轮数
        public int 最大层数 = 1;   // 可叠层上限（>1 允许叠层）
        public bool 控制;         // 异常=控制类(麻痹/眩晕，跳过行动)；持续伤害=异常且非控制
        public string 结算时机;   // 异常持续伤害："开始"=轮开始扣血（缺省）/ "结束"=轮结束扣血
        public string 描述;
    }

    [Serializable]
    public class Buff根 { public Buff定义[] Buffs; }

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
        public int 消耗精力;      // 释放消耗的行动点（物理/远程/治疗 技能统一耗精力）
        public string 类别;       // JSON中文名："攻击"/"治疗"/"增益"/"减益"/"控制"
        public 技能类别 类别枚举 => 数据解析.枚举<技能类别>(类别);
        public string 目标;       // "敌方单体"/"敌方全体"/"我方单体"/"我方全体"/"自己"
        public 目标类型 目标枚举 => 数据解析.枚举<目标类型>(目标);
        public string 伤害类型;   // "物理"/"远程"/"真实"
        public 伤害类型 伤害类型枚举 => 数据解析.枚举<伤害类型>(伤害类型);
        public string 伤害方式;   // "倍率" / "固定" / "附加"（缺省=倍率）
        public 技能伤害方式 伤害方式枚举 => 数据解析.枚举<技能伤害方式>(伤害方式);
        public float 数值;        // 倍率=倍率系数（重击=1.5 / 横扫斩=0.8）；固定=固定伤害值；附加=追加伤害量；治疗=恢复量
        public string 挂载Buff;   // 增益/减益/控制 挂载的buff标识
        public int 冷却;          // 使用后冷却轮数（0=无）
        public int 价格;          // 训练场学习费用
        public string 品质;       // "普通"/"优秀"/"稀有"...（JsonUtility 不认枚举名，字符串+转换）
        public 品质 品质档 => 数据解析.枚举<品质>(品质);
        public int 需要体力, 需要力量, 需要智力, 需要敏捷, 需要意志;   // 学习/释放前提（0=无要求）
        public int 熟练伤害加成 = 10;   // 每级 +% 伤害
        public int 熟练消耗减少 = 5;    // 每级 -% 消耗
        public int 熟练度每级 = 100;    // 每级熟练度阈值（累积满升级熟练等级）
        // —— 武器规则 / 攻击距离（战斗沙盒） ——
        public string 武器种类;   // 物理技能的所需武器种类（专向/弱向 判定用）
        public 武器种类 武器种类枚举 => 数据解析.枚举<武器种类>(武器种类);
        public string 武器规则;   // "通用" / "专向" / "弱向"（缺省=通用）
        public 武器规则 武器规则枚举 => 数据解析.枚举<武器规则>(武器规则);
        public int 攻击距离;      // 技能攻击距离（格）：1=近战（贴脸）、>1=远程（需直线视线）；0 = 按 1
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

    // 配方材料项：物品 + 数量（家具 建造/修复/升级 材料 与 制作 配方 共用）
    [Serializable]
    public class 配方材料 { public string 物品; public int 数量 = 1; }

    // 配方：安全屋制作家具（工作台/灶台/医疗站）用材料制作新物品。
    // 类型 = 制作家具标识（"工作台"=材料/武器/防具/工具、"灶台"=饮食、"医疗站"=医疗）；
    // 解锁 = 家具等级（需要等级）+ 图纸（持有解锁）；消耗 = 制作时间（推进游戏分钟）+ 精力（行动点）；
    // 饱食/水分 消耗 = 基础速率(每游戏时 2) × 消耗倍率 × 制作时数（制作服务 统一结算；倍率 0.5 倍数 设计）。
    [Serializable]
    public class 配方数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public string 类型;          // 制作家具标识："工作台" / "灶台" / "医疗站"
        public string 产物;          // 成品物品标识
        public int 产物数量 = 1;
        public 配方材料[] 材料;      // 所需材料（全部凑齐才能制作；跨 穿戴容器/仓库 统一扣）
        public string 解锁图纸;      // 所需图纸物品标识（持有才解锁显示/制作；空=无需图纸）
        public bool 需习得;        // true = 必须 通过 书籍·配方书 阅读 习得（已习得配方 集合 含 此 标识）才 解锁；蓝图/等级 之外 的 第三种 解锁 来源
        public int 需要等级 = 1;     // 制作家具 最低 等级（1/2/3；工作台/灶台/医疗站 均 自建 1 级起）
        public string 品质;          // 配方品质："普通"/"优秀"/"稀有"/"史诗"/"英雄"（书籍·配方书 池 筛选/权重 用；空=普通）
        public int 制作时间分 = 30;  // 制作推进的游戏分钟（饱食/水分 按 基础速率 × 消耗倍率 随此时间扣）
        public float 消耗倍率 = 1f;  // 制作消耗倍率（0.5 倍数：1.0=普通 / 1.5=费力 / 0.5=轻松）
        public int 消耗精力 = 5;     // 制作消耗的行动点
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

    // 家具升级需求：升级消耗材料 + 升级后占格（-1 = 不变）。索引 0 = 1→2 级，1 = 2→3 级…
    [Serializable]
    public class 家具升级需求
    {
        public 配方材料[] 材料;   // 升级所需材料
        public int 形状宽 = -1;   // 升级后占格宽（-1 = 不变）
        public int 形状高 = -1;   // 升级后占格高（-1 = 不变）
        public int 容器列 = -1;   // 升级后 家具容器 内部网格列（-1 = 不变）
        public int 容器行 = -1;   // 升级后 家具容器 内部网格行（-1 = 不变）
        public 容器分区数据[] 容器分区;   // 升级后 分区容器 各 子区 网格 尺寸（净水器 等；null = 不变）
    }

    // 家具定义（家具.json）：建造与升级的数据驱动
    [Serializable]
    public class 家具数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public int 解锁等级;        // 安全屋达到该等级才可建造
        public int 价格;            // 建造所需价值点数（以物易物材料）
        public 配方材料[] 材料;     // 建造（1级）所需材料
        public int 最大等级;        // 家具可升级到的等级
        public string 功能类型;     // 睡觉 / 制作 / 仓库 / 情报 / 取暖（效果值解释用）
        public int 形状宽 = 1;      // 1级占格
        public int 形状高 = 1;
        public 家具升级需求[] 升级; // 每级升级（索引0=1→2级）：材料 + 形状变化
        public int[] 效果;          // 每级效果值（索引0=1级；含义按 功能类型 解释）
        // —— 家具容器（冰箱等：打开 网格面板 的 家具） ——
        public bool 是容器;          // 是否为可打开容器（右键"打开" → 网格面板）
        public int 容器列;           // 1级 容器内部网格列（0 = 非容器家具）
        public int 容器行;           // 1级 容器内部网格行
        public string 容器允许类型;  // 可选：仅允许该类型进入（"饮食"/"材料"；"|"分隔多类型）；空 = 任意
        public string 容器允许种类;  // 可选：二级分类过滤（如 "食物原料"）；空 = 不按种类过滤
        public float[] 保质期倍率;   // 每级：容器内 易腐物品 保质期 延长倍率（如 [3,5,8] = 3/5/8 倍；0/未配 = 常温）
        // —— 分区容器 家具（净水器 等：内部 分 多个 独立 子区——每区 独立 网格/允许类型） ——
        public 容器分区数据[] 容器分区;   // 非空 = 分区容器（忽略 容器列/行/允许类型——各分区 自己 配置）
        // —— 自动 净化 家具（净水器 等：随时间 自动 将 分区0 材料A + 分区1 材料B → 分区2 产物） ——
        public int 净化间隔分;            // 每批 净化 所需 游戏 分钟（0 = 非 净化 家具）
        public string 净化材料A;          // 分区0 消耗（净水器 = 脏水）
        public string 净化材料B;          // 分区1 消耗（净水器 = 木炭）
        public string 净化产物;           // 分区2 产出（净水器 = 水）
        public int 净化产物数量 = 1;      // 每批 产出 数量
    }

    // 容器分区（净水器 的 脏水区/过滤区/净水区 等）：每区 一个 独立 子容器
    [Serializable]
    public class 容器分区数据
    {
        public string 名称;          // 区名（标题/标识：脏水/过滤/净水）
        public int 列 = 3;           // 分区 网格列
        public int 行 = 3;           // 分区 网格行
        public string 允许类型;      // 该区 仅 允许 的类型（"材料"等；空 = 任意）
        public string 允许种类;      // 该区 二级 种类 过滤（可选）
    }

    [Serializable]
    public class 家具根 { public 家具数据[] 家具; }

    // 家具工具：家具实例标识 编码/解码（实例标识 含 等级后缀："储物箱_2"；0 级 = 破损（待修复）"床_0"；等级 1 不带后缀）
    public static class 家具工具
    {
        public static string 编码(string 定义标识, int 等级) => 等级 switch
        {
            0 => $"{定义标识}_0",   // 0 级 = 破损（修复 后 变 1 级）
            1 => 定义标识,
            _ => $"{定义标识}_{等级}",
        };

        public static (string 定义, int 等级) 解码(string 实例标识)
        {
            if (!string.IsNullOrEmpty(实例标识))
            {
                int 分隔 = 实例标识.LastIndexOf('_');
                if (分隔 > 0 && int.TryParse(实例标识.Substring(分隔 + 1), out int 等级))
                    return (实例标识.Substring(0, 分隔), 等级);
            }
            return (实例标识, 1);
        }
    }

    // ================= 收音机情报（情报.json） =================

    // 情报条目：收音机 收听 播报内容（等级 = 收音机家具等级门槛）
    [Serializable]
    public class 情报条目
    {
        public string 文本;
        public int 等级 = 1;   // 需要 收音机 达到该等级 才可播报
    }

    [Serializable]
    public class 情报根 { public 情报条目[] 情报; }

    // ================= 搜索容器（塔科夫式搜刮） =================
    // 层级：地图类型（居民房）→ 房间（玄关/客厅/厨房…）→ 容器（鞋柜/冰箱…）。
    // 搜刮 = 打开 容器：首次打开按 搜索表 权重随机生成物品（复用 网格服务 网格算法）；
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
        public string 容器允许类型;    // 可选：仅允许该类型物品进入（如 "医疗"）；空 = 任意
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

    // ================= 战斗棋盘（战斗沙盒：横向长方形小棋盘） =================
    // 宽 > 高（如 12×7）。我方左侧出生、敌方右侧出生（遭遇方位决定站位）。
    // 障碍 = 预置模板标识（"街头"/"工厂"/"室内" 等；空 = 无障碍）；第一版用固定模板表，后续可程序化。
    [Serializable]
    public class 战斗棋盘数据
    {
        public string 标识;
        public string 名称;
        public int 宽 = 12;         // 棋盘宽（列数，横向）
        public int 高 = 7;          // 棋盘高（行数）
        public string 障碍模板;     // 障碍布局模板标识（空 = 无障碍全通）
        public int 我方出生列 = 0;  // 玩家/我方出生列（行 = 中段）
        public int 敌方出生列 = 11; // 敌人出生列
    }

    [Serializable]
    public class 战斗棋盘根 { public 战斗棋盘数据[] 棋盘; }

