using System;
using System.Collections.Generic;

    // 网格背包中的一件物品（末日物资：食物/水/药品/弹药/材料/装备 统一承载）
    [Serializable]
    public class 物品堆叠
    {
        public string 标识;
        public int 数量;
        public int 列 = -1;        // 网格列位置（-1 = 未放入网格）
        public int 行 = -1;        // 网格行位置
        public bool 旋转;          // 是否旋转 90°
        public int 当前耐久;        // 当前耐久（装备实例；<=0 = 损坏失效；随档存档）
        public List<词缀条> 词缀;   // 装备实例的随机词缀（非装备=null/空，随档存档）
        public string 品质;          // 合成提升后的品质覆盖（空=用模板品质；随档存档）
        // —— 容器实例（塔科夫式嵌套容器）：是容器的物品才有内部网格 ——
        public int 容器列;          // 实例网格列数（缺省用模板；随档存档）
        public int 容器行;          // 实例网格行数
        public List<物品堆叠> 容器物品;   // 容器内部物品（与 背包服务.背包 同构）；null = 非容器/空容器

        public 物品堆叠() { }
        public 物品堆叠(string 标识, int 数量) { this.标识 = 标识; this.数量 = 数量; }
    }

    // 任务进度（可序列化存档）
    [Serializable]
    public class 任务进度
    {
        public string 标识;
        public int 数量;
        public bool 已完成;
    }

    // 5 大核心属性类型（加点用）：体质/力量/智慧/敏捷/意志
    public enum 属性类型 { 体质, 力量, 智慧, 敏捷, 意志 }

    // 生存状态类型（0~100 的连续状态）：饱食/水分 为消耗型
    public enum 生存状态类型 { 饱食度, 水分度 }

    // 伤病类型（6 种，严重度 0~100，0=无）
    public enum 伤病类型 { 疲劳, 中毒, 感冒, 流血, 骨折, 发烧 }

    // 天气类型（7 种，每日随机）
    public enum 天气类型 { 晴, 雨, 雾, 雷雨, 寒潮, 沙暴, 酷暑 }

    // 物品形状（网格背包占用：宽×高，可旋转）
    [Serializable]
    public class 物品形状
    {
        public int 宽 = 1;
        public int 高 = 1;
        public 物品形状() { }
        public 物品形状(int 宽, int 高) { this.宽 = 宽; this.高 = 高; }
    }

    // 装备记录：已装备物品（槽位 + 标识）。存档结构（槽位：主手/副手/头部/胸部/腿部/脚部/手部 + 容器位 弹挂/腰封/背包）
    [Serializable]
    public class 装备记录
    {
        public string 槽位;
        public string 标识;
        public int 当前耐久;        // 当前耐久（装备实例；<=0 = 损坏失效；随档存档）
        public List<词缀条> 词缀;   // 装备实例的随机词缀（随档存档）
        public string 品质;          // 合成提升后的品质覆盖（空=用模板品质；随档存档）
        public string 来源;          // 装备前所在网格（"主背包"/"仓库"；空=主背包，旧档兼容）。卸下/回滚 时"从哪来回哪去"

        // —— 穿戴容器（弹挂/腰封/背包）：穿戴后内部网格与物品（随档存档；非容器装备 = 默认 0/null）——
        public int 容器列;                   // 实例网格列数（缺省用模板）
        public int 容器行;                   // 实例网格行数
        public List<物品堆叠> 容器物品;      // 容器内部物品（与 背包服务.背包 同构）；null = 非容器/空容器

        public 装备记录() { }
        public 装备记录(string 槽位, string 标识) { this.槽位 = 槽位; this.标识 = 标识; }
    }

    // 安全屋家具实例（等级解锁 + 多种家具）
    [Serializable]
    public class 家具实例
    {
        public string 标识;   // 床/储物柜/工作台/火炉/种植箱/凝水器/加固栅栏/医疗台
        public int 等级;      // 家具自身等级（1~3，升级强化效果）

        public 家具实例() { }
        public 家具实例(string 标识, int 等级 = 1) { this.标识 = 标识; this.等级 = 等级; }
    }

    // 玩家档案：纯 C# 领域模型（零 UnityEngine 依赖），可整体序列化存档。
    // 末日求生版：职业 + 五维属性 + 伤病6种 + 饱食/水分 + 行动点 + 网格背包 + 天气 + 安全屋
    [Serializable]
    public class 玩家档案
    {
        // 注入：装备数值解析器（由装配层接到 DataService；标识 -> 加成值），派生数值遍历已装备求和
        [NonSerialized] public Func<string, int> 攻击加成解析;
        [NonSerialized] public Func<string, int> 防御加成解析;
        [NonSerialized] public Func<string, int> 生命加成解析;
        [NonSerialized] public Func<string, int> 负重加成解析;
        [NonSerialized] public Func<string, 武器种类> 武器种类解析;   // 标识 -> 武器种类
        [NonSerialized] public Func<string, int> 抗性加成解析;        // 标识 -> 抗性百分数贡献点
        [NonSerialized] public Func<string, 物品形状> 形状解析;        // 标识 -> 物品形状（宽×高）——接背包服务
        [NonSerialized] public Func<string, int> 重量解析;            // 标识 -> 物品重量——接背包服务
        [NonSerialized] public Func<string, int> 堆叠上限解析;        // 标识 -> 堆叠上限——接背包服务
        [NonSerialized] public Func<string, int> 最大耐久解析;        // 标识 -> 最大耐久（0 = 无耐久，不损坏）
        [NonSerialized] public Func<string, (int 列, int 行)> 容器尺寸解析;   // 标识 -> 容器模板网格尺寸（弹挂/腰封/背包 穿戴时初始化）
        [NonSerialized] public Dictionary<string, 词缀定义> 词缀定义表;   // 词缀实例->模板

        // —— 身份：职业与天赋 ——
        public string 职业 = "";                        // 职业标识（开局选择）
        public string 角色名 = "无名幸存者";            // 角色名（开局输入，默认无名）
        public List<string> 天赋 = new List<string>();  // 正负天赋选中的标识列表
        public Dictionary<string, float> 天赋冷却 = new Dictionary<string, float>();   // 天赋标识 → 上次触发游戏分钟（机制冷却）

        // —— 核心五维属性（基础 5 + 职业加成 + 自由点 + 天赋） ——
        public int 体质 = 5;
        public int 力量 = 5;
        public int 智慧 = 5;
        public int 敏捷 = 5;
        public int 意志 = 5;
        public int 自由属性点 = 0;

        // —— 等级与经验 ——
        public int 等级 = 1;
        public int 经验 = 0;

        // —— 生命与生存状态 ——
        public int 生命 = 100;        // 当前生命（健康）
        public int 行动点 = 100;      // 当前行动点（探索/战斗消耗）
        public int 饱食度 = 100;      // 0~100：高=饱，低=饿
        public int 水分度 = 100;      // 0~100：高=水足，低=渴

        // —— 伤病（6 种，严重度 0~100，0=无） ——
        public int 疲劳 = 0;          // 行动/战斗累积，睡觉恢复
        public int 中毒 = 0;          // 腐食/被咬感染伤口，解毒剂治疗
        public int 感冒 = 0;          // 淋雨/受寒，药/火炉自愈
        public int 流血 = 0;          // 被攻击，绷带包扎
        public int 骨折 = 0;          // 坠落/重击，夹板固定长期恢复
        public int 发烧 = 0;          // 伤口恶化/重伤，抗生素治疗

        // —— 网格背包（数据与方法内聚于 背包服务；背包/网格列/网格行 随存档序列化，解析器委托除外） ——
        public 背包服务 背包服务 = new 背包服务();   // 网格尺寸/背包列表/网格算法 全部在此

        // —— 仓库（stash，收纳型大容器；不占负重，塔科夫右区） ——
        public List<物品堆叠> 仓库物品 = new List<物品堆叠>();
        public int 仓库列 = 10;   // 仓库网格列
        public int 仓库行 = 20;   // 仓库网格行

        // 仓库视图：把 仓库物品 包成 背包服务（复用全部网格/拖拽/跨网格转移逻辑）——持有管理器 统一提供
        public 背包服务 仓库视图() => 持有管理.仓库视图();

        // —— 装备：8 槽（主手/副手/头部/胸部/腿部/脚部/手部/背包） ——
        public List<装备记录> 装备 = new List<装备记录>();

        // —— 子管理器（职责分离·调度器模式）：本类 = 数据 + 装配 + 门面转发；领域逻辑 全部委托给 子管理器 ——
        [NonSerialized] public 装备管理器 装备管理;   // 装备/穿戴容器/装备驱动派生数值
        [NonSerialized] public 持有管理器 持有管理;   // 统一持有（穿戴容器+仓库+嵌套容器）/放入/移除
        [NonSerialized] public 生存管理器 生存管理;   // 生命/行动点/伤病/时间结算
        [NonSerialized] public 成长管理器 成长管理;   // 加点/经验/技能
        [NonSerialized] public 任务管理器 任务管理;   // 任务/日常

        // 装配子管理器（PlayerService 接线解析器 时调用；读档后 委托/管理器 需重新装配）
        public void 装配管理器()
        {
            装备管理 = new 装备管理器(this);
            持有管理 = new 持有管理器(this);
            生存管理 = new 生存管理器(this);
            成长管理 = new 成长管理器(this);
            任务管理 = new 任务管理器(this);
        }

        // —— 技能与任务 ——
        public List<技能掌握> 已学技能 = new List<技能掌握>();
        public List<任务进度> 任务 = new List<任务进度>();
        public List<日常任务> 日常 = new List<日常任务>();
        public int 日常生成日 = -1;

        // 按标识查日常任务（空 = 无）——任务管理器
        public 日常任务 查找日常(string 标识) => 任务管理.查找日常(标识);

        // 替换当天日常批次并记录生成日（跨天刷新用）——任务管理器
        public void 覆写日常(List<日常任务> 新日常) => 任务管理.覆写日常(新日常);

        // —— 安全屋 ——
        public int 安全屋等级 = 1;
        public List<家具实例> 家具 = new List<家具实例>();

        // —— 幸存者（第二版预留） ——
        public List<string> 幸存者 = new List<string>();

        // —— 抉择记录（世界记忆） ——
        public List<string> 抉择记录 = new List<string>();

        // —— 时间与天气 ——
        public float 游戏分钟数 = 420f;      // 6:00 开始；1 现实秒 = 2 游戏分钟
        public int 天气 = (int)天气类型.晴;  // 当前天气（每日随机）
        public int 游戏天数 => (int)(游戏分钟数 / 1440f);

        // —— 剧情进度（存档恢复用） ——
        public string 当前节点 = "";
        public string 主线阶段 = "";   // 兼容旧剧情系统（末日不使用，保留字段防断）

        // 末日地图位置（存档恢复用：当前所在大节点）
        public string 当前大节点 = "营地";

        // —— 探索 ——
        public List<string> 已清空地点 = new List<string>();

        public bool 已清空(string 地点标识) => 已清空地点.Contains(地点标识);
        public void 标记清空(string 地点标识)
        {
            if (!已清空地点.Contains(地点标识)) 已清空地点.Add(地点标识);
        }

        // 技能熟练度等级上限
        public const int 熟练等级上限 = 5;

        // ================= 派生数值（门面转发：装备/生存/成长 管理器） =================

        // 生命上限（生存管理器：体质/等级/装备/词缀）
        public int 最大生命 => 生存管理.最大生命;
        // 行动点上限（生存管理器）
        public int 最大行动点 => 生存管理.最大行动点;
        // 速度（生存管理器：敏捷/词缀/伤病）
        public int 速度 => 生存管理.速度;
        // 负重上限（装备管理器）
        public int 负重上限 => 装备管理.负重上限;
        // 近战伤害（装备管理器）
        public int 近战伤害 => 装备管理.近战伤害;
        // 枪械伤害（装备管理器）
        public int 枪械伤害 => 装备管理.枪械伤害;
        // 总防御（装备管理器）
        public int 总防御 => 装备管理.总防御;
        // 暴击率%（装备管理器）
        public float 暴击概率 => 装备管理.暴击概率;
        // 闪避率%（装备管理器）
        public float 闪避概率 => 装备管理.闪避概率;
        // 潜行值（装备管理器）
        public int 潜行值 => 装备管理.潜行值;
        // 感知（装备管理器）
        public int 感知 => 装备管理.感知;
        // 恐惧抗性%（装备管理器）
        public float 恐惧抗性 => 装备管理.恐惧抗性;
        // 饱食下降修正（生存管理器）
        public float 饱食下降修正 => 生存管理.饱食下降修正;
        // 水分下降修正（生存管理器）
        public float 水分下降修正 => 生存管理.水分下降修正;
        // 经验获取修正（成长管理器）
        public float 经验修正 => 成长管理.经验修正;
        // 医疗品效果修正（成长管理器）
        public float 医疗修正 => 成长管理.医疗修正;
        // 制作消耗修正（成长管理器）
        public float 制作消耗修正 => 成长管理.制作消耗修正;
        // 夜晚行动点消耗修正（成长管理器）
        public float 夜晚消耗修正 => 成长管理.夜晚消耗修正;
        // 抗性百分数（装备管理器）
        public int 抗性百分比 => 装备管理.抗性百分比;
        // 负重占用（装备管理器：3 穿戴容器 内物品重量，仓库不计）
        public int 负重占用 => 装备管理.负重占用;
        // 超重（装备管理器）
        public bool 超重 => 装备管理.超重;
        // 超重惩罚（装备管理器）
        public float 超重惩罚 => 装备管理.超重惩罚;
        // 伤病致弱（生存管理器）
        public float 伤病削弱 => 生存管理.伤病削弱;

        // ================= 生命/行动点/伤病/结算（门面转发：生存管理器） =================

        public void 恢复生命(int 数值) => 生存管理.恢复生命(数值);
        public void 受到伤害(int 数值) => 生存管理.受到伤害(数值);
        public bool 消耗行动点(int 数值) => 生存管理.消耗行动点(数值);
        public void 恢复行动点(int 数值) => 生存管理.恢复行动点(数值);
        public void 进食(int 数值) => 生存管理.进食(数值);
        public void 饮水(int 数值) => 生存管理.饮水(数值);
        public void 调整伤病(伤病类型 类型, int 数值) => 生存管理.调整伤病(类型, 数值);
        public int 伤病值(伤病类型 类型) => 生存管理.伤病值(类型);
        public void 每小时结算(天气类型 天气) => 生存管理.每小时结算(天气);
        public void 睡觉() => 生存管理.睡觉();

        // ================= 装备（门面转发：装备管理器） =================

        public string 装备标识(string 槽位) => 装备管理.装备标识(槽位);
        // 按 装备记录 完整恢复槽位（穿回/回滚用，含容器数据/来源）
        public 装备记录 装备到槽(string 槽位, 装备记录 记录) => 装备管理.装备到槽(槽位, 记录);
        public 装备记录 装备到槽(string 槽位, string 标识, List<词缀条> 词缀 = null, int? 当前耐久 = null, 物品堆叠 容器源 = null, string 来源 = null)
            => 装备管理.装备到槽(槽位, 标识, 词缀, 当前耐久, 容器源, 来源);
        public 装备记录 卸下装备(string 槽位) => 装备管理.卸下装备(槽位);
        public bool 已装备(string 标识) => 装备管理.已装备(标识);
        // 兼容旧引用：饰品槽自动分配
        public string 饰品目标槽() => 装备管理.饰品目标槽();
        // 背包装备 → 网格尺寸
        public (int 列, int 行) 背包网格尺寸(string 包标识 = null) => 装备管理.背包网格尺寸(包标识);
        public void 应用背包装备() => 装备管理.应用背包装备();
        // 有效最大耐久 / 装备耐久
        public int 有效最大耐久(string 标识) => 装备管理.有效最大耐久(标识);
        public int 装备当前耐久(string 槽位) => 装备管理.装备当前耐久(槽位);
        public bool 装备已损坏(string 槽位) => 装备管理.装备已损坏(槽位);
        public void 扣装备耐久(string 槽位, int 量) => 装备管理.扣装备耐久(槽位, 量);
        public void 扣装备标识耐久(string 标识, int 量) => 装备管理.扣装备标识耐久(标识, 量);
        public List<词缀条> 装备词缀(string 标识) => 装备管理.装备词缀(标识);


        // ================= 统一持有入口（门面转发：持有管理器——3 穿戴容器 + 仓库 + 嵌套容器（弹药箱等）） =================

        public List<物品堆叠> 所有持有物品() => 持有管理.所有持有物品();
        public bool 持有物品(string 标识) => 持有管理.持有物品(标识);
        public int 物品数量(string 标识) => 持有管理.物品数量(标识);
        public bool 移除物品(string 标识, int 数量 = 1) => 持有管理.移除物品(标识, 数量);
        public void 移除堆叠实例(物品堆叠 堆叠) => 持有管理.移除堆叠实例(堆叠);
        public List<词缀条> 背包词缀(string 标识) => 持有管理.背包词缀(标识);
        public int 背包当前耐久(string 标识) => 持有管理.背包当前耐久(标识);
        public int 放入物品(string 标识, int 数量 = 1) => 持有管理.放入物品(标识, 数量);
        public int 放入堆叠(物品堆叠 堆叠) => 持有管理.放入堆叠(堆叠);
        public 背包服务 穿戴容器视图(string 槽位) => 持有管理.穿戴容器视图(槽位);

        // 旧主背包 网格数据（废弃：不再存放游戏物品——物品统一在 3 穿戴容器 + 仓库 + 嵌套容器；
        // 保留 网格算法 与 旧引用 兼容；网格面板 数据源 null 时显示空网格兜底）
        public List<物品堆叠> 背包 => 持有管理.背包;
        public int 网格列 { get => 持有管理.网格列; set => 持有管理.网格列 = value; }
        public int 网格行 { get => 持有管理.网格行; set => 持有管理.网格行 = value; }

        // 网格放置（背包服务 算法 转发；外部视图 用各自 服务，不经过本类）
        public bool 可放置(string 标识, int 列, int 行, bool 旋转, 物品堆叠 排除 = null) => 背包服务.可放置(标识, 列, 行, 旋转, 排除);
        public bool 移动堆叠(物品堆叠 堆叠, int 列, int 行, bool 旋转) => 背包服务.移动堆叠(堆叠, 列, 行, 旋转);
        public bool 可换位(物品堆叠 甲, 物品堆叠 乙) => 背包服务.可换位(甲, 乙);
        public bool 换位(物品堆叠 甲, 物品堆叠 乙) => 背包服务.换位(甲, 乙);
        public 物品堆叠 该格物品(int 列, int 行) => 背包服务.该格物品(列, 行);
        public (int 宽, int 高) 物品占格(物品堆叠 堆叠) => 背包服务.物品占格(堆叠);
        public int 已用格数() => 背包服务.已用格数();
        public List<物品堆叠> 区域内物品(int 列, int 行, int 宽, int 高) => 背包服务.区域内物品(列, 行, 宽, 高);
        public bool 区域可互换(物品堆叠 A, int 目标列, int 目标行, bool 目标旋转) => 背包服务.区域可互换(A, 目标列, 目标行, 目标旋转);
        public bool 区域互换(物品堆叠 A, int 目标列, int 目标行, bool 目标旋转) => 背包服务.区域互换(A, 目标列, 目标行, 目标旋转);
        public bool 布局安全() => 背包服务.布局安全();
        public bool 可堆叠(string 标识) => 背包服务.可堆叠(标识);
        public int 堆叠上限(string 标识) => 背包服务.堆叠上限(标识);
        public bool 可合并(物品堆叠 目标, 物品堆叠 来源) => 背包服务.可合并(目标, 来源);
        public int 合并堆叠(物品堆叠 目标, 物品堆叠 来源) => 背包服务.合并堆叠(目标, 来源);

        // 放入/移除（统一持有入口重定向：穿戴容器 → 仓库；旧"主背包"转发 已废弃，不再往 档案.背包 放物品）
        public int 放入网格(string 标识, int 数量 = 1) => 持有管理.放入网格(标识, 数量);
        public bool 放入网格堆叠(物品堆叠 堆叠) => 持有管理.放入网格堆叠(堆叠);
        public void 从网格移除(string 标识, int 数量 = 1) => 持有管理.从网格移除(标识, 数量);
        public int 添加物品(string 标识, int 数量 = 1) => 持有管理.添加物品(标识, 数量);
        public void 添加堆叠(物品堆叠 堆叠) => 持有管理.添加堆叠(堆叠);

        // ================= 加点/经验/技能（门面转发：成长管理器） =================

        public bool 加点(属性类型 类型, int 点数 = 1) => 成长管理.加点(类型, 点数);
        public void 训练属性(属性类型 类型, int 点数 = 1) => 成长管理.训练属性(类型, 点数);
        public void 设置属性(属性类型 类型, int 值) => 成长管理.设置属性(类型, 值);
        public int 属性值(string 名) => 成长管理.属性值(名);
        public bool 掌握技能(string 标识) => 成长管理.掌握技能(标识);
        public int 技能熟练等级(string 标识) => 成长管理.技能熟练等级(标识);
        public int 技能熟练度(string 标识) => 成长管理.技能熟练度(标识);
        public string 技能前提失败原因(技能数据 技能) => 成长管理.技能前提失败原因(技能);
        public bool 学习技能(技能数据 技能) => 成长管理.学习技能(技能);
        public int 记录熟练度(string 标识, int 点数, int 每级阈值) => 成长管理.记录熟练度(标识, 点数, 每级阈值);
        public bool 获得经验(int 数值) => 成长管理.获得经验(数值);
        public int 升级所需经验 => 成长管理.升级所需经验;

        // ================= 任务（门面转发：任务管理器） =================

        public bool 添加任务(string 标识) => 任务管理.添加任务(标识);
        public List<任务进度> 记录击败(string 敌人标识, int 目标数量 = 1) => 任务管理.记录击败(敌人标识, 目标数量);
        public List<任务进度> 记录获得(string 物品标识, int 目标数量 = 1) => 任务管理.记录获得(物品标识, 目标数量);

        // 兼容旧探索引用：已通关 ↔ 已清空（末日：地点搜空后可再刷）
        public bool 已通关(string 标识) => 已清空(标识);
        public void 标记通关(string 标识) => 标记清空(标识);
        public bool 消耗精力(int 数值) => 消耗行动点(数值);
        public void 恢复精力(int 数值) => 恢复行动点(数值);

        // ================= 兼容旧字段（读档迁移用；旧奇幻 RPG 引用） =================

        public int 体力 { get => 体质; set => 体质 = value; }
        public int 智力 { get => 智慧; set => 智慧 = value; }
        public int 魔力 { get => 行动点; set => 行动点 = value; }
        public int 精力 { get => 行动点; set => 行动点 = value; }
        public int 物理伤害 => 近战伤害;
        public int 魔法伤害 => 枪械伤害;
        public int 最大魔力 => 最大行动点;
        public int 最大精力 => 最大行动点;
        public int 速度加成 => 装备管理.速度加成;
        public float 命中加成 => 0f;
        public int 饥饿 { get => 100 - 饱食度; set => 饱食度 = 100 - value; }
        public int 口渴 { get => 100 - 水分度; set => 水分度 = 100 - value; }
        public int 感染度 { get => 中毒; set => 中毒 = value; }
        public int 士气 { get => 100 - 疲劳; set => 疲劳 = 100 - value; }
        public int 噪音值 { get; set; }
        public int 铜币 = 0;   // 保留字段（以物易物后恒 0，兼容旧引用）
    }
