using System;
using System.Collections.Generic;

    // 背包中的一种物品及数量
    [Serializable]
    public class 物品堆叠
    {
        public string 标识;
        public int 数量;
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

    // 4 大基础属性类型（加点用）
    public enum 属性类型 { 体力, 力量, 智力, 敏捷 }

    // 装备记录：已装备物品（槽位 + 标识）。存档结构（8 槽：主手/副手/头盔/盔甲/靴子/手套/饰品1/饰品2）
    [Serializable]
    public class 装备记录
    {
        public string 槽位;
        public string 标识;
        public List<词缀条> 词缀;   // 装备实例的随机词缀（随档存档）
        public string 品质;          // 合成提升后的品质覆盖（空=用模板品质；随档存档）

        public 装备记录() { }
        public 装备记录(string 槽位, string 标识) { this.槽位 = 槽位; this.标识 = 标识; }
    }

    // 玩家档案：纯 C# 领域模型（零 UnityEngine 依赖），可整体序列化存档。
    // 字段名与旧 玩家状态 一致，保证旧档字段兼容。
    [Serializable]
    public class 玩家档案
    {
        // 注入：装备数值解析器（由装配层接到 DataService；标识 -> 加成值），派生数值遍历已装备求和
        [NonSerialized] public Func<string, int> 攻击加成解析;
        [NonSerialized] public Func<string, int> 防御加成解析;
        [NonSerialized] public Func<string, int> 生命加成解析;
        [NonSerialized] public Func<string, 武器种类> 武器种类解析;   // 标识 -> 武器种类（物理弱点判定）
        [NonSerialized] public Func<string, int> 抗性加成解析;        // 标识 -> 抗性百分数贡献点（非线性封顶）
        [NonSerialized] public Dictionary<string, 词缀定义> 词缀定义表;   // 词缀实例->模板（词缀求和用）

        // —— 属性 ——
        public int 等级 = 1;
        public int 经验 = 0;
        public int 生命 = 50;
        public int 魔力 = 20;
        public int 铜币 = 800;     // 钱包（最小单位：铜币；1金=100银=10000铜）初始 8 银，前期以小面值为主
        public int 精力 = 112;     // 当前精力（探索/行动/物理技能 消耗；100+等级×2+体力×2 上限）初始匹配 等级1/体力5
        public float 游戏分钟数 = 420f;           // 6:00 开始；1 现实秒 = 2 游戏分钟
        public string 当前节点 = "";               // 剧情进度（用于存档恢复）
        public string 主线阶段 = "第一章_序章";   // 主线进度（剧情自动触发/解锁判定/主线任务）
        public List<物品堆叠> 背包 = new List<物品堆叠>();
        public List<技能掌握> 已学技能 = new List<技能掌握>();
        public List<任务进度> 任务 = new List<任务进度>();

        // —— 探索：已通关区域（可再刷，Boss 层普通化）——
        public List<string> 已通关区域 = new List<string>();

        // —— 装备：槽位列表（8 槽），物品 槽位 声明归属，饰品自动分配 饰品1/饰品2 ——
        public List<装备记录> 装备 = new List<装备记录>();

        // —— 4 大基础属性 + 自由属性点 ——
        public int 体力 = 5;
        public int 力量 = 5;
        public int 智力 = 5;
        public int 敏捷 = 5;
        public int 自由属性点 = 0;                 // 升级获得，自行分配到 4 大属性

        // 技能熟练度等级上限
        public const int 熟练等级上限 = 5;

        // —— 日常任务 ——按游戏内天数刷新：跨天重生成一批
        public List<日常任务> 日常 = new List<日常任务>();
        public int 日常生成日 = -1;   // 生成当日的游戏天数（0=第1天）；跨天重生成

        // 游戏内天数（0 起；1 现实秒=2 游戏分钟，一日=1440 分钟）
        public int 游戏天数 => (int)(游戏分钟数 / 1440f);

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

        // —— 派生数值（属性驱动伤害/生命；无基础伤害值）——
        // 力量每点提供 1 点物理伤害；智力每点提供 1 点魔法伤害；装备加成额外叠加。
        public int 物理伤害 => 力量 + 装备数值(攻击加成解析) + 词缀总值(词缀属性.攻击);
        public int 魔法伤害 => 智力 + 词缀总值(词缀属性.魔攻);
        public int 总攻击 => 物理伤害;             // 兼容现有战斗（普攻走物理）
        public int 总防御 => 防御 + 装备数值(防御加成解析) + 词缀总值(词缀属性.防御);
        // 初始 50 血；每点体力 +5 生命，每升一级 +5 生命。
        public int 最大生命 => 50 + (体力 - 5) * 5 + (等级 - 1) * 5 + 装备数值(生命加成解析) + 词缀总值(词缀属性.生命);
        public int 最大魔力 => 20 + 智力 * 3 + 等级 * 3;
        public int 最大精力 => 100 + 等级 * 2 + 体力 * 2;   // 探索/行动/物理技能 资源
        public float 暴击概率 => 敏捷 * 0.01f + 词缀总值(词缀属性.暴击) / 100f;
        public float 闪避概率 => 敏捷 * 0.01f + 词缀总值(词缀属性.闪避) / 100f;
        public float 命中加成 => 词缀总值(词缀属性.命中) / 100f;   // 命中率百分比加成
        public int 速度加成 => 词缀总值(词缀属性.速度);            // 固定速度加点（行动序）

        // 抗性百分数（装备来源）：非线性收益递减 + 硬上限 50%，防无脑堆叠免伤。
        // Σ ≤30 全额；Σ>30 超出部分减半再累计；最终封顶 50。
        public int 抗性百分比
        {
            get
            {
                if (抗性加成解析 == null) return 0;
                int 总和 = 0;
                foreach (var e in 装备)
                    if (!string.IsNullOrEmpty(e.标识)) 总和 += 抗性加成解析(e.标识);
                总和 += 词缀总值(词缀属性.抗性);   // 抗性词缀并入装备抗性（同非线性封顶）
                if (总和 <= 30) return 总和;
                return Math.Min(50, 30 + (总和 - 30) / 2);
            }
        }

        // 防御基础值（旧字段保留，等级不再直接加，靠 防具/属性）
        public int 防御 = 2;
        public int 攻击 = 0;   // 已移除基础伤害值：物伤由 力量 提供，此字段仅作兼容保留

        // 已装备物品数值总和（标识 -> 加成 由解析器提供；未接线返回 0）
        private int 装备数值(Func<string, int> 加成)
        {
            if (加成 == null) return 0;
            int 总 = 0;
            foreach (var e in 装备)
                if (!string.IsNullOrEmpty(e.标识)) 总 += 加成(e.标识);
            return 总;
        }

        private int 词缀总值(词缀属性 属性)
        {
            if (词缀定义表 == null) return 0;
            int 总 = 0;
            foreach (var e in 装备)
            {
                if (e.词缀 == null) continue;
                foreach (var c in e.词缀)
                    if (词缀定义表.TryGetValue(c.标识, out var def) && def.属性枚举 == 属性) 总 += c.数值;
            }
            return 总;
        }

        // ---------- 装备 ----------

        // 取某槽已装备的物品标识（空 = 未装备）
        public string 装备标识(string 槽位)
        {
            foreach (var e in 装备) if (e.槽位 == 槽位) return e.标识;
            return "";
        }

        // 装备到指定槽：覆盖同槽旧件并返回旧记录（含旧词缀）；原本空槽返回 null。新件词缀随实例入槽。
        public 装备记录 装备到槽(string 槽位, string 标识, List<词缀条> 词缀 = null)
        {
            foreach (var e in 装备)
                if (e.槽位 == 槽位)
                {
                    var 旧 = new 装备记录(槽位, e.标识) { 词缀 = e.词缀 };
                    e.标识 = 标识; e.词缀 = 词缀;
                    return 旧;
                }
            装备.Add(new 装备记录(槽位, 标识) { 词缀 = 词缀 });
            return null;
        }

        // 卸下：清空槽位并返回该槽的装备记录（含词缀）；空槽返回 null
        public 装备记录 卸下装备(string 槽位)
        {
            for (int i = 装备.Count - 1; i >= 0; i--)
                if (装备[i].槽位 == 槽位) { var 记录 = 装备[i]; 装备.RemoveAt(i); return 记录; }
            return null;
        }

        // 饰品槽自动分配：优先空槽（饰品1 → 饰品2），都满则覆盖饰品1
        public string 饰品目标槽()
        {
            if (string.IsNullOrEmpty(装备标识("饰品1"))) return "饰品1";
            if (string.IsNullOrEmpty(装备标识("饰品2"))) return "饰品2";
            return "饰品1";
        }

        // 某物品是否已装备（任意槽）
        public bool 已装备(string 标识)
        {
            foreach (var e in 装备) if (e.标识 == 标识) return true;
            return false;
        }

        // ---------- 探索（已通关区域） ----------

        public bool 已通关(string 区域标识) => 已通关区域.Contains(区域标识);

        public void 标记通关(string 区域标识)
        {
            if (!已通关区域.Contains(区域标识)) 已通关区域.Add(区域标识);
        }

        // 升级所需经验（每级递增）
        public int 升级所需经验 => 等级 * 25;

        // 数值夹取（替代 UnityEngine.Mathf，保证零 Unity 依赖）
        private static int 夹(int 值, int 最小, int 最大) => 值 < 最小 ? 最小 : (值 > 最大 ? 最大 : 值);

        // ---------- 背包 ----------

        public void 添加物品(string 标识, int 数量 = 1)
        {
            if (string.IsNullOrEmpty(标识)) return;
            foreach (var 堆叠 in 背包)
                if (堆叠.标识 == 标识) { 堆叠.数量 += 数量; return; }
            背包.Add(new 物品堆叠(标识, 数量));
        }

        // 添加一个携带词缀的装备实例堆叠（不与同标识普通堆叠合并，保留独立词缀）
        public void 添加堆叠(物品堆叠 堆叠)
        {
            if (堆叠 == null || string.IsNullOrEmpty(堆叠.标识)) return;
            背包.Add(堆叠);
        }

        // 取里背包中该标识首个可用堆叠的词缀（换装用；无则 null）
        public List<词缀条> 背包词缀(string 标识)
        {
            foreach (var 堆叠 in 背包)
                if (堆叠.标识 == 标识 && 堆叠.数量 > 0) return 堆叠.词缀;
            return null;
        }

        // 已装备某物品的词缀（详情显示用）
        public List<词缀条> 装备词缀(string 标识)
        {
            foreach (var e in 装备) if (e.标识 == 标识) return e.词缀;
            return null;
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

        // ---------- 数值 ----------

        public void 恢复生命(int 数值) => 生命 = 夹(生命 + 数值, 0, 最大生命);

        public void 受到伤害(int 数值) => 生命 = Math.Max(0, 生命 - 数值);

        public void 恢复魔力(int 数值) => 魔力 = 夹(魔力 + 数值, 0, 最大魔力);

        public bool 消耗魔力(int 数值)
        {
            if (魔力 < 数值) return false;
            魔力 -= 数值;
            return true;
        }

        public void 恢复精力(int 数值) => 精力 = 夹(精力 + 数值, 0, 最大精力);

        public bool 消耗精力(int 数值)
        {
            if (精力 < 数值) return false;
            精力 -= 数值;
            return true;
        }

        // ---------- 加点 ----------

        // 消耗自由属性点加到 4 大属性；返回是否成功
        public bool 加点(属性类型 类型, int 点数 = 1)
        {
            if (点数 <= 0 || 自由属性点 < 点数) return false;
            自由属性点 -= 点数;
            switch (类型)
            {
                case 属性类型.体力: 体力 += 点数; break;
                case 属性类型.力量: 力量 += 点数; break;
                case 属性类型.智力: 智力 += 点数; break;
                case 属性类型.敏捷: 敏捷 += 点数; break;
            }
            return true;
        }

        // 训练场直接增强属性（不消耗自由属性点）
        public void 训练属性(属性类型 类型, int 点数 = 1)
        {
            switch (类型)
            {
                case 属性类型.体力: 体力 += 点数; break;
                case 属性类型.力量: 力量 += 点数; break;
                case 属性类型.智力: 智力 += 点数; break;
                case 属性类型.敏捷: 敏捷 += 点数; break;
            }
        }

        // 按属性名取当前值（面板显示用）
        public int 属性值(string 名)
        {
            switch (名)
            {
                case "体力": return 体力;
                case "力量": return 力量;
                case "智力": return 智力;
                case "敏捷": return 敏捷;
                default: return 0;
            }
        }

        // ---------- 技能 ----------

        public bool 掌握技能(string 标识) => 已学技能.Exists(s => s.标识 == 标识);

        // 技能熟练等级（未学=0）
        public int 技能熟练等级(string 标识)
        {
            foreach (var s in 已学技能) if (s.标识 == 标识) return s.熟练等级;
            return 0;
        }

        // 当前级内熟练度进度（未学=0）
        public int 技能熟练度(string 标识)
        {
            foreach (var s in 已学技能) if (s.标识 == 标识) return s.熟练度;
            return 0;
        }

        // 校验技能属性前提，返回失败原因（空=通过）
        public string 技能前提失败原因(技能数据 技能)
        {
            if (技能 == null) return "技能不存在。";
            if (体力 < 技能.需要体力) return $"体力不足（需要 {技能.需要体力}）。";
            if (力量 < 技能.需要力量) return $"力量不足（需要 {技能.需要力量}）。";
            if (智力 < 技能.需要智力) return $"智力不足（需要 {技能.需要智力}）。";
            if (敏捷 < 技能.需要敏捷) return $"敏捷不足（需要 {技能.需要敏捷}）。";
            return "";
        }

        // 学习技能：属性前提不达标或已学会则失败（统一的唯一入口）
        public bool 学习技能(技能数据 技能)
        {
            if (技能 == null || 掌握技能(技能.标识)) return false;
            if (!string.IsNullOrEmpty(技能前提失败原因(技能))) return false;
            已学技能.Add(new 技能掌握(技能.标识));
            return true;
        }

        // 记录技能熟练度：使用/训练累积，满阈值升级熟练等级；返回新等级（0=未学）
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

        // ---------- 任务 ----------

        public bool 添加任务(string 标识)
        {
            foreach (var 进度 in 任务) if (进度.标识 == 标识) return false;
            任务.Add(new 任务进度 { 标识 = 标识, 数量 = 0, 已完成 = false });
            return true;
        }

        // 推进「击败」类任务计数；目标匹配由服务层在调用前判定，领域只做计数
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

        // 推进「获得物品」类任务计数
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

        // ---------- 经验 ----------

        // 获得经验并处理升级链（给自由属性点 + 满状态）；返回是否升级
        public bool 获得经验(int 数值)
        {
            经验 += 数值;
            bool 升级了 = false;
            while (经验 >= 升级所需经验)
            {
                经验 -= 升级所需经验;
                等级++;
                自由属性点 += 3;          // 升级给 3 点自由属性点
                生命 = 最大生命;
                魔力 = 最大魔力;
                升级了 = true;
            }
            return 升级了;
        }
    }
