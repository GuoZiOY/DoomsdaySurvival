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

    // 装备记录：已装备物品（槽位 + 标识）。存档结构（8 槽：主手/副手/头部/胸部/腿部/脚部/手部/背包）
    [Serializable]
    public class 装备记录
    {
        public string 槽位;
        public string 标识;
        public int 当前耐久;        // 当前耐久（装备实例；<=0 = 损坏失效；随档存档）
        public List<词缀条> 词缀;   // 装备实例的随机词缀（随档存档）
        public string 品质;          // 合成提升后的品质覆盖（空=用模板品质；随档存档）

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
        [NonSerialized] public Func<string, 物品形状> 形状解析;        // 标识 -> 物品形状（宽×高）
        [NonSerialized] public Func<string, int> 重量解析;            // 标识 -> 物品重量
        [NonSerialized] public Func<string, int> 堆叠上限解析;        // 标识 -> 堆叠上限（0/缺省 = 不可堆叠，每格恒 1 件）
        [NonSerialized] public Func<string, int> 最大耐久解析;        // 标识 -> 最大耐久（0 = 无耐久，不损坏）
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

        // —— 网格背包 ——
        public int 网格列 = 5;       // 默认背包 50 格（5列×10行）——测试用大背包；背包装备可扩展/缩小
        public int 网格行 = 10;
        public List<物品堆叠> 背包 = new List<物品堆叠>();

        // —— 装备：8 槽（主手/副手/头部/胸部/腿部/脚部/手部/背包） ——
        public List<装备记录> 装备 = new List<装备记录>();

        // —— 技能与任务 ——
        public List<技能掌握> 已学技能 = new List<技能掌握>();
        public List<任务进度> 任务 = new List<任务进度>();
        public List<日常任务> 日常 = new List<日常任务>();
        public int 日常生成日 = -1;

        // 按标识查日常任务（空 = 无）
        public 日常任务 查找日常(string 标识)
        {
            foreach (var 条 in 日常) if (条.标识 == 标识) return 条;
            return null;
        }

        // 替换当天日常批次并记录生成日（跨天刷新用）
        public void 覆写日常(List<日常任务> 新日常)
        {
            日常 = 新日常 ?? new List<日常任务>();
            日常生成日 = 游戏天数;
        }

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

        // ================= 派生数值 =================

        // 生命上限：100 + 每点体质 +8（超出基础5）+ 每级 +5 + 装备/词缀
        public int 最大生命 => 100 + (体质 - 5) * 8 + (等级 - 1) * 5 + 装备数值(生命加成解析) + 词缀总值(词缀属性.生命);

        // 行动点上限：100 + 体质×2 + 等级×2
        public int 最大行动点 => 100 + 体质 * 2 + 等级 * 2;

        // 速度：敏捷×2 + 装备/词缀速度（行动序 + 逃跑；伤病削弱）
        public int 速度 => Math.Max(1, 敏捷 * 2 + 词缀总值(词缀属性.速度) - (骨折 > 0 ? 骨折 / 10 : 0) - (疲劳 > 50 ? 疲劳 / 10 : 0));

        // 负重上限：50 + 力量×5 + 体质×2 + 装备/词缀
        public int 负重上限 => 50 + 力量 * 5 + 体质 * 2 + 装备数值(负重加成解析) + 词缀总值(词缀属性.负重);

        // 近战伤害：力量 + 武器/词缀（骨折削弱）
        public int 近战伤害 => Math.Max(1, 力量 + 装备数值(攻击加成解析) + 词缀总值(词缀属性.攻击) - (骨折 > 0 ? 骨折 / 10 : 0));

        // 枪械伤害：武器固定伤害（不吃属性）+ 词缀
        public int 枪械伤害 => 装备数值(攻击加成解析) + 词缀总值(词缀属性.攻击);

        // 总防御：体质×0.5 + 防具/词缀
        public int 总防御 => 体质 / 2 + 装备数值(防御加成解析) + 词缀总值(词缀属性.防御);

        // 暴击率%：敏捷×1 + 意志×0.5（封顶 80）
        public float 暴击概率 => Math.Min(0.8f, (敏捷 * 1f + 意志 * 0.5f) / 100f + 词缀总值(词缀属性.暴击) / 100f);

        // 闪避率%：敏捷×1 + 意志×0.3（封顶 60）
        public float 闪避概率 => Math.Min(0.6f, (敏捷 * 1f + 意志 * 0.3f) / 100f + 词缀总值(词缀属性.闪避) / 100f);

        // 潜行值：敏捷×2 + 意志×1 + 词缀（躲避丧尸/偷袭判定）
        public int 潜行值 => 敏捷 * 2 + 意志 + 词缀总值(词缀属性.潜行);

        // 感知：智慧×1 + 意志×1（发现物资/幸存者/先手判定）
        public int 感知 => 智慧 + 意志;

        // 恐惧抗性%：意志×1.5（夜晚/尸群/恐怖事件判定）
        public float 恐惧抗性 => Math.Min(0.8f, 意志 * 1.5f / 100f);

        // 饱食下降速度修正（体质减缓；天赋"铁胃"减缓、"胃口大"加速）
        public float 饱食下降修正
        {
            get
            {
                float 修正 = 1f - (体质 - 5) * 0.04f;   // 每点体质 -4%
                if (天赋.Contains("铁胃")) 修正 -= 0.15f;
                if (天赋.Contains("胃口大")) 修正 += 0.6f;
                return Math.Max(0.2f, 修正);
            }
        }

        // 水分下降速度修正（体质减缓；天赋"口渴快"加速）
        public float 水分下降修正
        {
            get
            {
                float 修正 = 1f - (体质 - 5) * 0.04f;
                if (天赋.Contains("口渴快")) 修正 += 0.6f;
                return Math.Max(0.2f, 修正);
            }
        }

        // 经验获取修正（天赋"快速学习者"）
        public float 经验修正 => 天赋.Contains("快速学习者") ? 1.10f : 1f;

        // 医疗品效果修正（天赋"医者仁心"）
        public float 医疗修正 => 天赋.Contains("医者仁心") ? 1.05f : 1f;

        // 制作消耗修正（天赋"节俭"）
        public float 制作消耗修正 => 天赋.Contains("节俭") ? 0.95f : 1f;

        // 夜晚行动点消耗修正（天赋"夜行者"）
        public float 夜晚消耗修正 => 天赋.Contains("夜行者") ? 0.85f : 1f;

        // 抗性百分数（装备来源）：非线性收益递减 + 硬上限 50%
        public int 抗性百分比
        {
            get
            {
                if (抗性加成解析 == null) return 0;
                int 总和 = 0;
                foreach (var e in 装备)
                    if (!string.IsNullOrEmpty(e.标识)) 总和 += 抗性加成解析(e.标识);
                总和 += 词缀总值(词缀属性.抗性);
                if (总和 <= 30) return 总和;
                return Math.Min(50, 30 + (总和 - 30) / 2);
            }
        }

        // 负重占用（当前总重）
        public int 负重占用
        {
            get
            {
                int 总 = 0;
                foreach (var 堆叠 in 背包)
                    if (堆叠 != null && 重量解析 != null) 总 += 重量解析(堆叠.标识) * 堆叠.数量;
                return 总;
            }
        }

        public bool 超重 => 负重占用 > 负重上限;

        // 超重惩罚（0~0.5：超重越多越慢）
        public float 超重惩罚
        {
            get
            {
                if (!超重) return 0f;
                int 超出 = 负重占用 - 负重上限;
                return Math.Min(0.5f, 超出 / 100f);
            }
        }

        // 伤病致弱（感冒/发烧/中毒 全属性惩罚）
        public float 伤病削弱
        {
            get
            {
                float 削弱 = 0f;
                if (感冒 > 0) 削弱 += 感冒 / 200f;
                if (发烧 > 0) 削弱 += 发烧 / 150f;
                if (中毒 > 0) 削弱 += 中毒 / 200f;
                return Math.Min(0.5f, 削弱);
            }
        }

        // ================= 生命/行动点操作 =================

        public void 恢复生命(int 数值) => 生命 = 夹(生命 + 数值, 0, 最大生命);
        public void 受到伤害(int 数值) => 生命 = Math.Max(0, 生命 - 数值);
        public bool 消耗行动点(int 数值)
        {
            if (行动点 < 数值) return false;
            行动点 -= 数值;
            return true;
        }
        public void 恢复行动点(int 数值) => 行动点 = 夹(行动点 + 数值, 0, 最大行动点);

        // 饱食/水分增减
        public void 进食(int 数值) => 饱食度 = 夹(饱食度 + 数值, 0, 100);
        public void 饮水(int 数值) => 水分度 = 夹(水分度 + 数值, 0, 100);

        // 伤病增减（0~100 夹取）
        public void 调整伤病(伤病类型 类型, int 数值)
        {
            switch (类型)
            {
                case 伤病类型.疲劳: 疲劳 = 夹(疲劳 + 数值, 0, 100); break;
                case 伤病类型.中毒: 中毒 = 夹(中毒 + 数值, 0, 100); break;
                case 伤病类型.感冒: 感冒 = 夹(感冒 + 数值, 0, 100); break;
                case 伤病类型.流血: 流血 = 夹(流血 + 数值, 0, 100); break;
                case 伤病类型.骨折: 骨折 = 夹(骨折 + 数值, 0, 100); break;
                case 伤病类型.发烧: 发烧 = 夹(发烧 + 数值, 0, 100); break;
            }
        }

        public int 伤病值(伤病类型 类型)
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

        // 每小时生存结算（游戏时钟调用）：饱食/水分下降 + 伤病持续影响
        public void 每小时结算(天气类型 天气)
        {
            // 饱食/水分随时间下降（体质/天赋修正）
            饱食度 = 夹(饱食度 - (int)(2 * 饱食下降修正), 0, 100);
            水分度 = 夹(水分度 - (int)(3 * 水分下降修正), 0, 100);
            // 雨天：水分不降（自动补水）；酷暑：水分消耗↑
            if (天气 == 天气类型.雨 || 天气 == 天气类型.雷雨) 水分度 = 夹(水分度 + 1, 0, 100);
            if (天气 == 天气类型.酷暑) 水分度 = 夹(水分度 - 2, 0, 100);
            // 寒潮：无火炉/厚衣可能感冒（感冒概率由 意志 抵抗；慢性病天赋 ×2）
            if (天气 == 天气类型.寒潮 && 感冒 == 0)
            {
                int 感冒阈值 = 意志 * 3;
                if (天赋.Contains("慢性病")) 感冒阈值 /= 2;
                if (随机(100) > 感冒阈值) 感冒 = 5;
            }
            // 疲劳缓慢恢复（行动时累积，这里基础恢复）
            疲劳 = 夹(疲劳 - 1, 0, 100);
            // 伤病持续掉血
            if (流血 > 0) 受到伤害(流血 / 20 + 1);
            if (中毒 > 0) 受到伤害(中毒 / 25 + 1);
            if (发烧 > 0) 受到伤害(发烧 / 30 + 1);
            // 饱食/水分归零：不致死，只削弱（持续少量掉血警示）
            if (饱食度 <= 0) 受到伤害(1);
            if (水分度 <= 0) 受到伤害(2);
        }

        // 睡觉结算（回营地睡觉）：恢复生命/行动点/疲劳，推进时间到次日 6:00
        public void 睡觉()
        {
            // 失眠天赋：恢复 -40%
            float 恢复系数 = 天赋.Contains("失眠") ? 0.6f : 1f;
            生命 = Math.Max(1, (int)(最大生命 * 恢复系数));
            行动点 = Math.Max(1, (int)(最大行动点 * 恢复系数));
            疲劳 = Math.Max(0, (int)(疲劳 * (1 - 0.8f * 恢复系数)));
            感冒 = Math.Max(0, 感冒 - (int)(20 * 恢复系数));
            流血 = Math.Max(0, 流血 - (int)(30 * 恢复系数));
            发烧 = Math.Max(0, 发烧 - (int)(15 * 恢复系数));
            中毒 = Math.Max(0, 中毒 - (int)(10 * 恢复系数));
            // 推进时间到次日 6:00
            游戏分钟数 = (游戏天数 + 1) * 1440f + 360f;
        }

        // ================= 装备 =================

        public string 装备标识(string 槽位)
        {
            foreach (var e in 装备) if (e.槽位 == 槽位) return e.标识;
            return "";
        }

        public 装备记录 装备到槽(string 槽位, string 标识, List<词缀条> 词缀 = null, int? 当前耐久 = null)
        {
            foreach (var e in 装备)
                if (e.槽位 == 槽位)
                {
                    var 旧 = new 装备记录(槽位, e.标识) { 词缀 = e.词缀, 当前耐久 = e.当前耐久 };
                    e.标识 = 标识; e.词缀 = 词缀;
                    e.当前耐久 = 当前耐久 ?? 有效最大耐久(标识);   // 透传实例耐久；无则按模板初始化
                    return 旧;
                }
            装备.Add(new 装备记录(槽位, 标识) { 词缀 = 词缀, 当前耐久 = 当前耐久 ?? 有效最大耐久(标识) });
            return null;
        }

        public 装备记录 卸下装备(string 槽位)
        {
            for (int i = 装备.Count - 1; i >= 0; i--)
                if (装备[i].槽位 == 槽位) { var 记录 = 装备[i]; 装备.RemoveAt(i); return 记录; }
            return null;
        }

        public bool 已装备(string 标识)
        {
            foreach (var e in 装备) if (e.标识 == 标识) return true;
            return false;
        }

        // 兼容旧引用：饰品槽自动分配（末日槽位无饰品，保留方法防旧代码断）
        public string 饰品目标槽()
        {
            if (string.IsNullOrEmpty(装备标识("饰品1"))) return "饰品1";
            if (string.IsNullOrEmpty(装备标识("饰品2"))) return "饰品2";
            return "饰品1";
        }

        // 背包装备 → 网格尺寸（默认背包 50 格 10×5 / 战术背包 5×4 / 登山包 6×5 / 腰包 4×2）
        public (int 列, int 行) 背包网格尺寸(string 包标识 = null)
        {
            if (string.IsNullOrEmpty(包标识)) 包标识 = 装备标识("背包");
            if (string.IsNullOrEmpty(包标识)) return (5, 10);   // 默认背包 50 格（5列×10行，测试）
            if (包标识.Contains("腰包")) return (4, 2);
            if (包标识.Contains("战术")) return (5, 4);
            if (包标识.Contains("登山")) return (6, 5);
            return (5, 10);
        }

        public void 应用背包装备()
        {
            var (列, 行) = 背包网格尺寸();
            网格列 = 列; 网格行 = 行;
        }

        // ================= 网格背包 =================

        // 检查某物品能否放在 (列,行)（不越界、不重叠）；排除 = 自身堆叠（移动/换位校验用，忽略其占格）
        public bool 可放置(string 标识, int 列, int 行, bool 旋转, 物品堆叠 排除 = null)
        {
            if (形状解析 == null) return false;
            var 形状 = 形状解析(标识);
            int 宽 = 旋转 ? 形状.高 : 形状.宽;
            int 高 = 旋转 ? 形状.宽 : 形状.高;
            if (列 < 0 || 行 < 0 || 列 + 宽 > 网格列 || 行 + 高 > 网格行) return false;
            foreach (var 堆叠 in 背包)
            {
                if (堆叠 == null || 堆叠 == 排除 || 堆叠.列 < 0) continue;
                if (占据(堆叠, 列, 行, 宽, 高)) return false;
            }
            return true;
        }

        // 与 可放置 相同，但可同时忽略两个堆叠（用于换位判定：两物品互换位置时双方都不算占格）
        private bool 可放置忽略两个(string 标识, int 列, int 行, bool 旋转, 物品堆叠 排除A, 物品堆叠 排除B)
        {
            if (形状解析 == null) return false;
            var 形状 = 形状解析(标识);
            int 宽 = 旋转 ? 形状.高 : 形状.宽;
            int 高 = 旋转 ? 形状.宽 : 形状.高;
            if (列 < 0 || 行 < 0 || 列 + 宽 > 网格列 || 行 + 高 > 网格行) return false;
            foreach (var 堆叠 in 背包)
            {
                if (堆叠 == null || 堆叠 == 排除A || 堆叠 == 排除B || 堆叠.列 < 0) continue;
                if (占据(堆叠, 列, 行, 宽, 高)) return false;
            }
            return true;
        }

        // 两物品是否重叠（占用格子相交）
        private bool 占据(物品堆叠 已有, int 列, int 行, int 宽, int 高)
        {
            if (形状解析 == null) return false;
            var 形状 = 形状解析(已有.标识);
            int 已宽 = 已有.旋转 ? 形状.高 : 形状.宽;
            int 已高 = 已有.旋转 ? 形状.宽 : 形状.高;
            return 列 < 已有.列 + 已宽 && 列 + 宽 > 已有.列 && 行 < 已有.行 + 已高 && 行 + 高 > 已有.行;
        }

        // 移动/旋转已入格物品到指定坐标（目标格被占则不移动，返回 false）
        public bool 移动堆叠(物品堆叠 堆叠, int 列, int 行, bool 旋转)
        {
            if (堆叠 == null || string.IsNullOrEmpty(堆叠.标识) || 堆叠.列 < 0 || 形状解析 == null) return false;
            if (堆叠.列 == 列 && 堆叠.行 == 行 && 堆叠.旋转 == 旋转) return true;   // 原位无操作
            if (!可放置(堆叠.标识, 列, 行, 旋转, 堆叠)) return false;
            堆叠.列 = 列; 堆叠.行 = 行; 堆叠.旋转 = 旋转;
            return true;
        }

        // 换位预测（拖拽投影的绿/红判定用）：两物品互换位置后是否都放得下（不改变状态）
        public bool 可换位(物品堆叠 甲, 物品堆叠 乙)
        {
            if (甲 == null || 乙 == null || 甲 == 乙) return false;
            if (甲.列 < 0 || 乙.列 < 0 || 形状解析 == null) return false;
            // 换位 = 两物品都离开原位互换：判定时需同时忽略 甲、乙（否则放对方格子时会与对方重叠而误判失败）
            return 可放置忽略两个(乙.标识, 甲.列, 甲.行, 乙.旋转, 甲, 乙)
                && 可放置忽略两个(甲.标识, 乙.列, 乙.行, 甲.旋转, 甲, 乙);
        }

        // 换位（交换两物品的位置与旋转）：互换后两件都必须放得下才执行
        public bool 换位(物品堆叠 甲, 物品堆叠 乙)
        {
            if (!可换位(甲, 乙)) return false;
            int 列 = 甲.列; 甲.列 = 乙.列; 乙.列 = 列;
            int 行 = 甲.行; 甲.行 = 乙.行; 乙.行 = 行;
            bool 转 = 甲.旋转; 甲.旋转 = 乙.旋转; 乙.旋转 = 转;
            return true;
        }

        // ================= 区域交换（整体换位） =================

        // 收集与 (列,行,宽,高) 相交的背包物品
        public List<物品堆叠> 区域内物品(int 列, int 行, int 宽, int 高)
        {
            var 结果 = new List<物品堆叠>();
            if (形状解析 == null) return 结果;
            foreach (var 堆叠 in 背包)
            {
                if (堆叠 == null || 堆叠.列 < 0) continue;
                var 形状 = 形状解析(堆叠.标识);
                int 物宽 = 堆叠.旋转 ? 形状.高 : 形状.宽;
                int 物高 = 堆叠.旋转 ? 形状.宽 : 形状.高;
                if (列 < 堆叠.列 + 物宽 && 列 + 宽 > 堆叠.列 && 行 < 堆叠.行 + 物高 && 行 + 高 > 堆叠.行) 结果.Add(堆叠);
            }
            return 结果;
        }

        // 可放置，但忽略 忽略集（移走的物品）+ 忽略A（自身原位）
        private bool 可放置忽略多个(string 标识, int 列, int 行, bool 旋转, List<物品堆叠> 忽略集, 物品堆叠 忽略A)
        {
            if (形状解析 == null) return false;
            var 形状 = 形状解析(标识);
            int 宽 = 旋转 ? 形状.高 : 形状.宽;
            int 高 = 旋转 ? 形状.宽 : 形状.高;
            if (列 < 0 || 行 < 0 || 列 + 宽 > 网格列 || 行 + 高 > 网格行) return false;
            foreach (var 堆叠 in 背包)
            {
                if (堆叠 == null || 堆叠 == 忽略A || 堆叠.列 < 0) continue;
                if (忽略集 != null && 忽略集.Contains(堆叠)) continue;
                if (占据(堆叠, 列, 行, 宽, 高)) return false;
            }
            return true;
        }

        // 把 物品集 全部摆进 (区域列,区域行,区域宽,区域高) 区域：① 先试整体平移(保持相对位置) ② 放不下再回溯。返回实际位置；无解 null
        private List<(物品堆叠, int, int)> 布局摆进(List<物品堆叠> 物品集, int 区域列, int 区域行, int 区域宽, int 区域高, 物品堆叠 忽略)
        {
            if (物品集 == null || 物品集.Count == 0) return new List<(物品堆叠, int, int)>();
            var 平移 = 平移布局(物品集, 区域列, 区域行, 区域宽, 区域高, 忽略);
            if (平移 != null) return 平移;   // 符合直觉：尽量保持相对位置整体平移
            物品集.Sort((a, b) => 堆叠面积(b).CompareTo(堆叠面积(a)));   // 回溯：面积大的先放
            var 已放 = new List<物品堆叠>();
            var 结果 = new List<(物品堆叠, int, int)>();
            return 递归摆进(物品集, 区域列, 区域行, 区域宽, 区域高, 忽略, 已放, 结果) ? 结果 : null;
        }

        // 整体平移：以 物品集 最小列/行为参考，把相对位置平移到 区域；全部放得下才返回，否则 null
        private List<(物品堆叠, int, int)> 平移布局(List<物品堆叠> 物品集, int 区域列, int 区域行, int 区域宽, int 区域高, 物品堆叠 忽略)
        {
            int minCol = int.MaxValue, minRow = int.MaxValue;
            foreach (var s in 物品集) { if (s.列 < minCol) minCol = s.列; if (s.行 < minRow) minRow = s.行; }
            var 结果 = new List<(物品堆叠, int, int)>();
            foreach (var s in 物品集)
            {
                int nc = 区域列 + (s.列 - minCol);
                int nr = 区域行 + (s.行 - minRow);
                var 形状 = 形状解析(s.标识);
                int w = s.旋转 ? 形状.高 : 形状.宽;
                int h = s.旋转 ? 形状.宽 : 形状.高;
                if (nc < 区域列 || nr < 区域行 || nc + w > 区域列 + 区域宽 || nr + h > 区域行 + 区域高) return null;   // 越出原位区
                if (与已摆重叠(s, nc, nr, 结果)) return null;   // 相对位置内部不自叠
                结果.Add((s, nc, nr));
            }
            foreach (var (s, nc, nr) in 结果)
                if (!可放置忽略多个(s.标识, nc, nr, s.旋转, 物品集, 忽略)) return null;   // 与 非目标物品 重叠
            return 结果;
        }

        private int 堆叠面积(物品堆叠 堆叠)
        {
            var 形状 = 形状解析(堆叠.标识);
            return (堆叠.旋转 ? 形状.高 : 形状.宽) * (堆叠.旋转 ? 形状.宽 : 形状.高);
        }

        private bool 递归摆进(List<物品堆叠> 物品集, int 区域列, int 区域行, int 区域宽, int 区域高, 物品堆叠 忽略, List<物品堆叠> 已放, List<(物品堆叠, int, int)> 结果)
        {
            if (已放.Count >= 物品集.Count) return true;
            var 物品 = 物品集[已放.Count];   // 按 已放 计数 顺序 处理（调用前已 面积 降序）
            var 形状 = 形状解析(物品.标识);
            int 宽 = 物品.旋转 ? 形状.高 : 形状.宽;
            int 高 = 物品.旋转 ? 形状.宽 : 形状.高;
            var 未摆 = new List<物品堆叠>();   // 未摆放的目标（仍在目标区）：忽略；已摆放的按新位置占位（用 结果 检查）
            foreach (var s in 物品集) if (!已放.Contains(s)) 未摆.Add(s);
            for (int r = 区域行; r + 高 <= 区域行 + 区域高; r++)
                for (int c = 区域列; c + 宽 <= 区域列 + 区域宽; c++)
                {
                    if (!可放置忽略多个(物品.标识, c, r, 物品.旋转, 未摆, 忽略)) continue;
                    if (与已摆重叠(物品, c, r, 结果)) continue;   // 已摆放目标在 A 原位的新位置占位，避免互相重叠
                    已放.Add(物品);
                    结果.Add((物品, c, r));
                    if (递归摆进(物品集, 区域列, 区域行, 区域宽, 区域高, 忽略, 已放, 结果)) return true;
                    已放.RemoveAt(已放.Count - 1);
                    结果.RemoveAt(结果.Count - 1);
                }
            return false;
        }

        // 新物品 (列,行) 是否与 已摆放 目标（结果里的新位置）重叠
        private bool 与已摆重叠(物品堆叠 a, int 列, int 行, List<(物品堆叠, int, int)> 已摆)
        {
            var 形状a = 形状解析(a.标识);
            int aw = a.旋转 ? 形状a.高 : 形状a.宽;
            int ah = a.旋转 ? 形状a.宽 : 形状a.高;
            foreach (var (b, 列b, 行b) in 已摆)
            {
                var 形状b = 形状解析(b.标识);
                int bw = b.旋转 ? 形状b.高 : 形状b.宽;
                int bh = b.旋转 ? 形状b.宽 : 形状b.高;
                if (列 < 列b + bw && 列 + aw > 列b && 行 < 行b + bh && 行 + ah > 行b) return true;
            }
            return false;
        }

        // 区域交换预测：拖 A 到 (目标列,目标行)，按 目标旋转 落位——空区=移动可行；目标区物品能整体搬回 A 原位才可真换位
        public bool 区域可互换(物品堆叠 A, int 目标列, int 目标行, bool 目标旋转)
        {
            if (A == null || A.列 < 0 || 形状解析 == null) return false;
            var 形状 = 形状解析(A.标识);
            int 宽 = 目标旋转 ? 形状.高 : 形状.宽;
            int 高 = 目标旋转 ? 形状.宽 : 形状.高;
            var 目标 = 区域内物品(目标列, 目标行, 宽, 高).FindAll(b => b != A);
            if (目标.Count == 0) return 可放置(A.标识, 目标列, 目标行, 目标旋转, A);   // 空区：移动
            if (!可放置忽略多个(A.标识, 目标列, 目标行, 目标旋转, 目标, A)) return false;   // A 能否进目标区
            int 原宽 = A.旋转 ? 形状.高 : 形状.宽;   // A 原位区 尺寸（按 A 当前旋转）
            int 原高 = A.旋转 ? 形状.宽 : 形状.高;
            return 布局摆进(目标, A.列, A.行, 原宽, 原高, A) != null;   // 目标区物品能否整体搬回 A 原位
        }

        // 区域交换：A 按 目标旋转 落 目标区；原目标区物品 全部 搬回 A 原位区（不改姿态）。执行前布局，失败/不安全则回滚
        public bool 区域互换(物品堆叠 A, int 目标列, int 目标行, bool 目标旋转)
        {
            if (A == null || A.列 < 0 || 形状解析 == null) return false;
            var 形状 = 形状解析(A.标识);
            int 宽 = 目标旋转 ? 形状.高 : 形状.宽;
            int 高 = 目标旋转 ? 形状.宽 : 形状.高;
            var 目标 = 区域内物品(目标列, 目标行, 宽, 高).FindAll(b => b != A);
            if (目标.Count == 0) return 移动堆叠(A, 目标列, 目标行, 目标旋转);   // 空区：移动
            if (!可放置忽略多个(A.标识, 目标列, 目标行, 目标旋转, 目标, A)) return false;
            int 原宽 = A.旋转 ? 形状.高 : 形状.宽;
            int 原高 = A.旋转 ? 形状.宽 : 形状.高;
            var 布局 = 布局摆进(目标, A.列, A.行, 原宽, 原高, A);
            if (布局 == null) return false;
            // 备份 参与者(含 形状/位置) 用于回滚
            var 备份 = new List<(物品堆叠, int, int, bool)>();
            备份.Add((A, A.列, A.行, A.旋转));
            foreach (var 物品 in 目标) 备份.Add((物品, 物品.列, 物品.行, 物品.旋转));
            A.列 = 目标列; A.行 = 目标行; A.旋转 = 目标旋转;   // A 落 目标区
            foreach (var (物品, 列, 行) in 布局) { 物品.列 = 列; 物品.行 = 行; }   // 目标物品 搬回 A 原位
            if (布局安全()) return true;   // 全网格校验：不越界、两两不重叠
            foreach (var (物品, 列, 行, 旋转) in 备份) { 物品.列 = 列; 物品.行 = 行; 物品.旋转 = 旋转; }   // 回滚
            return false;
        }

        // 校验整个背包布局：所有物品不越界、两两不重叠（区域交换后安全验证）
        public bool 布局安全()
        {
            if (形状解析 == null) return false;
            var 物品 = new List<物品堆叠>();
            foreach (var s in 背包) if (s != null && s.列 >= 0) 物品.Add(s);
            for (int i = 0; i < 物品.Count; i++)
            {
                var a = 物品[i];
                var 形状a = 形状解析(a.标识);
                int aw = a.旋转 ? 形状a.高 : 形状a.宽;
                int ah = a.旋转 ? 形状a.宽 : 形状a.高;
                if (a.列 < 0 || a.行 < 0 || a.列 + aw > 网格列 || a.行 + ah > 网格行) return false;
                for (int j = i + 1; j < 物品.Count; j++)
                {
                    var b = 物品[j];
                    var 形状b = 形状解析(b.标识);
                    int bw = b.旋转 ? 形状b.高 : 形状b.宽;
                    int bh = b.旋转 ? 形状b.宽 : 形状b.高;
                    if (a.列 < b.列 + bw && a.列 + aw > b.列 && a.行 < b.行 + bh && a.行 + ah > b.行) return false;   // 重叠
                }
            }
            return true;
        }

        // ================= 堆叠规则 =================

        // 该标识是否可堆叠（堆叠上限 > 1 才可合并；武器/防具/任务品 上限 1 = 每格独立一件）
        public bool 可堆叠(string 标识) => 堆叠上限(标识) > 1;

        // 该标识的堆叠上限（0/缺省 = 不可堆叠）
        public int 堆叠上限(string 标识) => 堆叠上限解析?.Invoke(标识) ?? 0;

        // 两堆叠能否合并：同标识 + 可堆叠 + 无词缀（词缀是装备实例，不可并入）+ 品质覆盖相同（空则忽略）+ 目标未满
        public bool 可合并(物品堆叠 目标, 物品堆叠 来源)
        {
            if (目标 == null || 来源 == null || 目标 == 来源) return false;
            if (目标.标识 != 来源.标识) return false;
            if (目标.词缀 != null && 目标.词缀.Count > 0) return false;
            if (来源.词缀 != null && 来源.词缀.Count > 0) return false;
            if (!string.IsNullOrEmpty(目标.品质) || !string.IsNullOrEmpty(来源.品质))
                if (目标.品质 != 来源.品质) return false;
            if (!可堆叠(目标.标识)) return false;
            return 目标.数量 < 堆叠上限(目标.标识);
        }

        // 合并：来源并入目标（受上限限制），返回实际并入数量；来源耗尽则移除。来源剩余留在原格（拖拽源未移动）。
        public int 合并堆叠(物品堆叠 目标, 物品堆叠 来源)
        {
            if (!可合并(目标, 来源)) return 0;
            int 空位 = 堆叠上限(目标.标识) - 目标.数量;
            int 并入 = Math.Min(空位, 来源.数量);
            if (并入 <= 0) return 0;
            目标.数量 += 并入;
            来源.数量 -= 并入;
            if (来源.数量 <= 0) 背包.Remove(来源);
            return 并入;
        }

        // 放入网格（填已有堆叠优先，满额再开新堆叠；不可堆叠每次新开一件）。返回实际放入数量（0 = 一个都放不下）。
        public int 放入网格(string 标识, int 数量 = 1)
        {
            if (string.IsNullOrEmpty(标识) || 数量 <= 0) return 0;
            int 原数量 = 数量;
            int 上限 = 堆叠上限(标识);
            // ① 先填已入格的未满堆叠（仅 列>=0、无词缀）
            foreach (var 堆叠 in 背包)
            {
                if (堆叠 == null || 堆叠.列 < 0 || 堆叠.标识 != 标识) continue;
                if (堆叠.词缀 != null && 堆叠.词缀.Count > 0) continue;
                int 空位 = 上限 - 堆叠.数量;
                if (空位 <= 0) continue;
                int 并入 = Math.Min(空位, 数量);
                堆叠.数量 += 并入;
                数量 -= 并入;
                if (数量 <= 0) return 原数量;
            }
            // ② 剩余开新堆叠（可堆叠：每堆 ≤上限；不可堆叠：每堆 1 件）
            while (数量 > 0)
            {
                int 本次 = 上限 > 1 ? Math.Min(上限, 数量) : 1;
                var 新堆叠 = new 物品堆叠(标识, 本次) { 当前耐久 = 有效最大耐久(标识) };   // 装备初始化完整耐久
                bool 放下 = false;
                for (int 行 = 0; 行 < 网格行 && !放下; 行++)
                    for (int 列 = 0; 列 < 网格列 && !放下; 列++)
                        if (可放置(标识, 列, 行, false))
                        {
                            新堆叠.列 = 列; 新堆叠.行 = 行;
                            背包.Add(新堆叠);
                            放下 = true;
                        }
                if (!放下) return 原数量 - 数量;   // 背包满了：返回实际放入数（部分已放入）
                数量 -= 本次;
            }
            return 原数量;
        }

        // 放入一个已有堆叠实例（含词缀的装备/掉落物/换装回包/旧存档迁移）：找空位放置并写入坐标；背包满返回 false（不改变该堆叠）
        public bool 放入网格堆叠(物品堆叠 堆叠)
        {
            if (堆叠 == null || string.IsNullOrEmpty(堆叠.标识) || 堆叠.数量 <= 0 || 形状解析 == null) return false;
            if (堆叠.列 >= 0) return true;   // 已在网格中
            bool 已在背包 = 背包.Contains(堆叠);   // 旧存档迁移的堆叠已在列表里（列=-1），勿重复添加
            for (int 行 = 0; 行 < 网格行; 行++)
                for (int 列 = 0; 列 < 网格列; 列++)
                    if (可放置(堆叠.标识, 列, 行, false))
                    {
                        堆叠.列 = 列; 堆叠.行 = 行;
                        if (!已在背包) 背包.Add(堆叠);
                        return true;
                    }
            return false;
        }

        // 从网格移除（数量耗尽则删除）
        public void 从网格移除(string 标识, int 数量 = 1)
        {
            var 堆叠 = 找堆叠(标识);
            if (堆叠 == null) return;
            堆叠.数量 -= 数量;
            if (堆叠.数量 <= 0) 背包.Remove(堆叠);
        }

        private 物品堆叠 找堆叠(string 标识)
        {
            foreach (var 堆叠 in 背包) if (堆叠.标识 == 标识 && 堆叠.数量 > 0) return 堆叠;
            return null;
        }

        // ================= 背包基础操作（兼容旧引用） =================

        // 添加物品（网格版）：等价于 放入网格 —— 先并入已有未满堆叠，再开新堆叠；返回实际放入数量
        public int 添加物品(string 标识, int 数量 = 1) => 放入网格(标识, 数量);

        public void 添加堆叠(物品堆叠 堆叠)
        {
            if (堆叠 == null || string.IsNullOrEmpty(堆叠.标识)) return;
            背包.Add(堆叠);
        }

        public bool 移除物品(string 标识, int 数量 = 1)
        {
            if (string.IsNullOrEmpty(标识)) return false;
            for (int i = 0; i < 背包.Count; i++)
            {
                var 堆叠 = 背包[i];
                if (堆叠.标识 == 标识 && 堆叠.数量 >= 数量)
                {
                    堆叠.数量 -= 数量;
                    if (堆叠.数量 <= 0) 背包.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public int 物品数量(string 标识)
        {
            if (string.IsNullOrEmpty(标识)) return 0;
            foreach (var 堆叠 in 背包) if (堆叠.标识 == 标识) return 堆叠.数量;
            return 0;
        }

        public bool 持有物品(string 标识) => 物品数量(标识) > 0;

        public List<词缀条> 背包词缀(string 标识)
        {
            foreach (var 堆叠 in 背包)
                if (堆叠.标识 == 标识 && 堆叠.数量 > 0) return 堆叠.词缀;
            return null;
        }

        public int 背包当前耐久(string 标识)
        {
            foreach (var 堆叠 in 背包)
                if (堆叠.标识 == 标识 && 堆叠.数量 > 0) return 堆叠.当前耐久;
            return 0;
        }

        // 已装备某物品的词缀（详情显示用）
        public List<词缀条> 装备词缀(string 标识)
        {
            foreach (var e in 装备) if (e.标识 == 标识) return e.词缀;
            return null;
        }

        // ================= 加点 =================

        public bool 加点(属性类型 类型, int 点数 = 1)
        {
            if (点数 <= 0 || 自由属性点 < 点数) return false;
            自由属性点 -= 点数;
            switch (类型)
            {
                case 属性类型.体质: 体质 += 点数; break;
                case 属性类型.力量: 力量 += 点数; break;
                case 属性类型.智慧: 智慧 += 点数; break;
                case 属性类型.敏捷: 敏捷 += 点数; break;
                case 属性类型.意志: 意志 += 点数; break;
            }
            return true;
        }

        public void 训练属性(属性类型 类型, int 点数 = 1)
        {
            switch (类型)
            {
                case 属性类型.体质: 体质 += 点数; break;
                case 属性类型.力量: 力量 += 点数; break;
                case 属性类型.智慧: 智慧 += 点数; break;
                case 属性类型.敏捷: 敏捷 += 点数; break;
                case 属性类型.意志: 意志 += 点数; break;
            }
        }

        // 直接设置某属性为指定值（职业分布用：开局职业直接给定五维分布）
        public void 设置属性(属性类型 类型, int 值)
        {
            switch (类型)
            {
                case 属性类型.体质: 体质 = 值; break;
                case 属性类型.力量: 力量 = 值; break;
                case 属性类型.智慧: 智慧 = 值; break;
                case 属性类型.敏捷: 敏捷 = 值; break;
                case 属性类型.意志: 意志 = 值; break;
            }
        }

        public int 属性值(string 名)
        {
            switch (名)
            {
                case "体质": return 体质;
                case "力量": return 力量;
                case "智慧": return 智慧;
                case "敏捷": return 敏捷;
                case "意志": return 意志;
                default: return 0;
            }
        }

        // ================= 技能 =================

        public bool 掌握技能(string 标识) => 已学技能.Exists(s => s.标识 == 标识);
        public int 技能熟练等级(string 标识)
        {
            foreach (var s in 已学技能) if (s.标识 == 标识) return s.熟练等级;
            return 0;
        }

        public int 技能熟练度(string 标识)
        {
            foreach (var s in 已学技能) if (s.标识 == 标识) return s.熟练度;
            return 0;
        }

        public string 技能前提失败原因(技能数据 技能)
        {
            if (技能 == null) return "技能不存在。";
            if (体质 < 技能.需要体力) return $"体质不足（需要 {技能.需要体力}）。";
            if (力量 < 技能.需要力量) return $"力量不足（需要 {技能.需要力量}）。";
            if (智慧 < 技能.需要智力) return $"智慧不足（需要 {技能.需要智力}）。";
            if (敏捷 < 技能.需要敏捷) return $"敏捷不足（需要 {技能.需要敏捷}）。";
            if (意志 < 技能.需要意志) return $"意志不足（需要 {技能.需要意志}）。";
            return "";
        }

        public bool 学习技能(技能数据 技能)
        {
            if (技能 == null || 掌握技能(技能.标识)) return false;
            if (!string.IsNullOrEmpty(技能前提失败原因(技能))) return false;
            已学技能.Add(new 技能掌握(技能.标识));
            return true;
        }

        public int 记录熟练度(string 标识, int 点数, int 每级阈值)
        {
            foreach (var s in 已学技能)
                if (s.标识 == 标识)
                {
                    if (s.熟练等级 >= 熟练等级上限) return s.熟练等级;
                    s.熟练度 += Math.Max(1, 点数);
                    while (s.熟练度 >= 每级阈值 && s.熟练等级 < 熟练等级上限)
                    {
                        s.熟练度 -= 每级阈值;
                        s.熟练等级++;
                    }
                    return s.熟练等级;
                }
            return 0;
        }

        // ================= 任务 =================

        public bool 添加任务(string 标识)
        {
            foreach (var 进度 in 任务) if (进度.标识 == 标识) return false;
            任务.Add(new 任务进度 { 标识 = 标识, 数量 = 0, 已完成 = false });
            return true;
        }

        public List<任务进度> 记录击败(string 敌人标识, int 目标数量 = 1)
        {
            var 完成 = new List<任务进度>();
            foreach (var 进度 in 任务)
            {
                if (进度.已完成) continue;
                if (进度.数量 < 目标数量)
                {
                    进度.数量++;
                    if (进度.数量 >= 目标数量) { 进度.已完成 = true; 完成.Add(进度); }
                }
            }
            return 完成;
        }

        public List<任务进度> 记录获得(string 物品标识, int 目标数量 = 1)
        {
            var 完成 = new List<任务进度>();
            foreach (var 进度 in 任务)
            {
                if (进度.已完成) continue;
                if (进度.数量 < 目标数量)
                {
                    进度.数量++;
                    if (进度.数量 >= 目标数量) { 进度.已完成 = true; 完成.Add(进度); }
                }
            }
            return 完成;
        }

        // ================= 经验 =================

        public bool 获得经验(int 数值)
        {
            经验 += (int)(数值 * 经验修正);
            bool 升级了 = false;
            while (经验 >= 升级所需经验)
            {
                经验 -= 升级所需经验;
                等级++;
                自由属性点 += 3;
                // 升级只回 50% 状态（末日后不再满血复活）
                生命 = Math.Max(1, 最大生命 / 2);
                行动点 = Math.Max(1, 最大行动点 / 2);
                升级了 = true;
            }
            return 升级了;
        }

        public int 升级所需经验 => 等级 * 25;

        // 兼容旧探索引用：已通关 ↔ 已清空（末日：地点搜空后可再刷）
        public bool 已通关(string 标识) => 已清空(标识);
        public void 标记通关(string 标识) => 标记清空(标识);
        public bool 消耗精力(int 数值) => 消耗行动点(数值);
        public void 恢复精力(int 数值) => 恢复行动点(数值);

        // ================= 兼容旧字段（读档迁移用） =================

        public int 体力 { get => 体质; set => 体质 = value; }
        public int 智力 { get => 智慧; set => 智慧 = value; }
        public int 魔力 { get => 行动点; set => 行动点 = value; }
        public int 精力 { get => 行动点; set => 行动点 = value; }
        public int 物理伤害 => 近战伤害;
        public int 魔法伤害 => 枪械伤害;
        public int 最大魔力 => 最大行动点;
        public int 最大精力 => 最大行动点;
        public int 速度加成 => 词缀总值(词缀属性.速度);
        public float 命中加成 => 0f;
        public int 饥饿 { get => 100 - 饱食度; set => 饱食度 = 100 - value; }
        public int 口渴 { get => 100 - 水分度; set => 水分度 = 100 - value; }
        public int 感染度 { get => 中毒; set => 中毒 = value; }
        public int 士气 { get => 100 - 疲劳; set => 疲劳 = 100 - value; }
        public int 噪音值 { get; set; }
        public int 铜币 = 0;   // 保留字段（以物易物后恒 0，兼容旧引用）

        // ================= 工具 =================

        private static int 夹(int 值, int 最小, int 最大) => 值 < 最小 ? 最小 : (值 > 最大 ? 最大 : 值);
        private static int 随机(int 上限) => 上限 <= 0 ? 0 : new System.Random().Next(上限);

        // 有效最大耐久：优先 items.json 配置；缺省给"有攻击或防御加成"的装备默认 15（武器/防具）
        public int 有效最大耐久(string 标识)
        {
            int 配 = 最大耐久解析?.Invoke(标识) ?? 0;
            if (配 > 0) return 配;
            if ((攻击加成解析?.Invoke(标识) ?? 0) > 0 || (防御加成解析?.Invoke(标识) ?? 0) > 0) return 15;
            return 0;
        }

        // 指定槽装备当前耐久
        public int 装备当前耐久(string 槽位)
        {
            foreach (var e in 装备) if (e.槽位 == 槽位) return e.当前耐久;
            return 0;
        }

        // 指定槽装备是否损坏（有装备、有耐久、且 当前耐久<=0）
        public bool 装备已损坏(string 槽位)
        {
            string 标识 = 装备标识(槽位);
            if (string.IsNullOrEmpty(标识)) return false;
            if (有效最大耐久(标识) <= 0) return false;
            foreach (var e in 装备) if (e.槽位 == 槽位) return e.当前耐久 <= 0;
            return false;
        }

        // 扣指定槽位装备耐久（不掉出负数）
        public void 扣装备耐久(string 槽位, int 量)
        {
            if (量 <= 0) return;
            foreach (var e in 装备)
                if (e.槽位 == 槽位 && 有效最大耐久(e.标识) > 0)
                {
                    e.当前耐久 = Math.Max(0, e.当前耐久 - 量);
                    return;
                }
        }

        // 扣指定标识装备耐久（任意槽位，用于按当前武器标识扣）
        public void 扣装备标识耐久(string 标识, int 量)
        {
            if (量 <= 0 || string.IsNullOrEmpty(标识)) return;
            foreach (var e in 装备)
                if (e.标识 == 标识 && 有效最大耐久(e.标识) > 0)
                {
                    e.当前耐久 = Math.Max(0, e.当前耐久 - 量);
                    return;
                }
        }

        private int 装备数值(Func<string, int> 加成)
        {
            if (加成 == null) return 0;
            int 总 = 0;
            foreach (var e in 装备)
            {
                if (string.IsNullOrEmpty(e.标识)) continue;
                if (有效最大耐久(e.标识) > 0 && e.当前耐久 <= 0) continue;   // 耐久归零：该装备加成失效
                总 += 加成(e.标识);
            }
            return 总;
        }

        private int 词缀总值(词缀属性 属性)
        {
            if (词缀定义表 == null) return 0;
            int 总 = 0;
            foreach (var e in 装备)
            {
                if (e.词缀 == null) continue;
                if (有效最大耐久(e.标识) > 0 && e.当前耐久 <= 0) continue;   // 损坏装备的词缀失效
                foreach (var c in e.词缀)
                    if (词缀定义表.TryGetValue(c.标识, out var def) && def.属性枚举 == 属性) 总 += c.数值;
            }
            return 总;
        }
    }
