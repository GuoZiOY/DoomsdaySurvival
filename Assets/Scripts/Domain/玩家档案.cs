using System;
using System.Collections.Generic;

    // 背包中的一种物品及数量（末日物资：食物/水/药品/弹药/零件 统一承载）
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

    // 5 大核心属性类型（加点用）：体质/力量/智慧/敏捷/意志
    public enum 属性类型 { 体质, 力量, 智慧, 敏捷, 意志 }

    // 生存状态类型（0~100 的连续状态）
    public enum 生存状态类型 { 饥饿, 口渴, 疲劳, 感染度, 士气, 噪音 }

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
    // 末日求生版：五维属性 + 生存状态（饥饿/口渴/疲劳/感染/士气）+ 物资背包（以物易物经济）
    [Serializable]
    public class 玩家档案
    {
        // 注入：装备数值解析器（由装配层接到 DataService；标识 -> 加成值），派生数值遍历已装备求和
        [NonSerialized] public Func<string, int> 攻击加成解析;
        [NonSerialized] public Func<string, int> 防御加成解析;
        [NonSerialized] public Func<string, int> 生命加成解析;
        [NonSerialized] public Func<string, int> 负重加成解析;
        [NonSerialized] public Func<string, 武器种类> 武器种类解析;   // 标识 -> 武器种类（物理弱点判定）
        [NonSerialized] public Func<string, int> 抗性加成解析;        // 标识 -> 抗性百分数贡献点（非线性封顶）
        [NonSerialized] public Dictionary<string, 词缀定义> 词缀定义表;   // 词缀实例->模板（词缀求和用）

        // —— 核心五维属性（末日求生：每次升级 +3 自由点自行分配） ——
        public int 体质 = 5;   // 生命/负重/感染抗性/疲劳恢复/饥饿耐受
        public int 力量 = 5;   // 近战伤害/负重/破门撬锁
        public int 智慧 = 5;   // 制作/急救/陷阱识别/经验获取
        public int 敏捷 = 5;   // 速度/闪避/潜行/暴击/逃跑
        public int 意志 = 5;   // 恐惧抗性/感染抵抗力/士气恢复/夜晚行动
        public int 自由属性点 = 0;   // 升级获得，自行分配到 5 大属性

        // —— 等级与经验 ——
        public int 等级 = 1;
        public int 经验 = 0;

        // —— 生存状态（0~100；除疲劳/噪音外，越高越健康） ——
        public int 生命 = 40;         // 当前生命（健康）
        public int 行动点 = 100;      // 当前行动点（探索/战斗消耗；原"精力"）
        public int 饥饿 = 30;         // 0=饱 100=饿死
        public int 口渴 = 20;         // 0=不渴 100=渴死
        public int 疲劳 = 0;          // 0=精神 100=精疲力竭（行动累积，休息恢复）
        public int 感染度 = 0;        // 0=干净 100=变异（被咬累积，抗生素/消毒治疗）
        public int 士气 = 80;         // 0=崩溃 100=高昂（事件/意志恢复）
        public int 噪音值 = 0;        // 当前地点累积（枪械/战斗产生，引来尸潮；回安全处清零）

        // —— 物资背包（以物易物经济：不再有统一货币） ——
        public List<物品堆叠> 背包 = new List<物品堆叠>();
        public int 负重占用 => 背包计数总量();   // 动态计算

        // —— 时间 ——
        public float 游戏分钟数 = 420f;           // 6:00 开始；1 现实秒 = 2 游戏分钟
        public int 游戏天数 => (int)(游戏分钟数 / 1440f);

        // —— 剧情进度 ——
        public string 当前节点 = "";               // 存档恢复用
        public string 主线阶段 = "第1天_醒来";     // 生存天数推进/解锁判定（保留字段语义）

        // —— 探索 ——
        public List<string> 已清空地点 = new List<string>();   // 原"已通关区域"：搜空的地点（可再刷，只剩丧尸）

        // —— 装备：槽位列表（8 槽），物品 槽位 声明归属，饰品自动分配 饰品1/饰品2 ——
        public List<装备记录> 装备 = new List<装备记录>();

        // —— 技能与任务 ——
        public List<技能掌握> 已学技能 = new List<技能掌握>();
        public List<任务进度> 任务 = new List<任务进度>();
        public List<日常任务> 日常 = new List<日常任务>();
        public int 日常生成日 = -1;

        // —— 幸存者（第二版：营地人口，预留字段） ——
        public List<string> 幸存者 = new List<string>();   // 第二版扩展为对象列表

        // —— 抉择记录（世界记忆：道德抉择留痕，事件引用） ——
        public List<string> 抉择记录 = new List<string>();

        // 技能熟练度等级上限
        public const int 熟练等级上限 = 5;

        // —— 派生数值（五维 → 战斗/生存） ——

        // 生命上限：50 + 每点体质 +8（超出基础5） + 每级 +5 + 装备/词缀
        public int 最大生命 => 50 + (体质 - 5) * 8 + (等级 - 1) * 5 + 装备数值(生命加成解析) + 词缀总值(词缀属性.生命);

        // 行动点上限：100 + 体质×2 + 等级×2（探索/行动资源；原"精力"）
        public int 最大行动点 => 100 + 体质 * 2 + 等级 * 2;

        // 速度：敏捷×2 + 装备/词缀速度（行动序 + 逃跑概率）
        public int 速度 => 敏捷 * 2 + 词缀总值(词缀属性.速度);

        // 负重上限：50 + 力量×5 + 体质×2 + 装备/词缀（末日核心：物资搬运限制）
        public int 负重上限 => 50 + 力量 * 5 + 体质 * 2 + 装备数值(负重加成解析) + 词缀总值(词缀属性.负重);

        // 近战伤害：力量 + 武器/词缀攻击（物理系）
        public int 近战伤害 => 力量 + 装备数值(攻击加成解析) + 词缀总值(词缀属性.攻击);

        // 枪械伤害：敏捷×0.5 + 武器/词缀攻击（远程系）
        public int 枪械伤害 => 敏捷 / 2 + 装备数值(攻击加成解析) + 词缀总值(词缀属性.攻击);

        // 总防御：体质×0.5 + 防具/词缀（皮糙肉厚 + 装备）
        public int 总防御 => 体质 / 2 + 防御加成解析合计 + 词缀总值(词缀属性.防御);

        // 暴击率%：敏捷×1 + 意志×0.5（封顶 80）
        public float 暴击概率 => Math.Min(0.8f, (敏捷 * 1f + 意志 * 0.5f) / 100f + 词缀总值(词缀属性.暴击) / 100f);

        // 闪避率%：敏捷×1 + 意志×0.3（封顶 60）
        public float 闪避概率 => Math.Min(0.6f, (敏捷 * 1f + 意志 * 0.3f) / 100f + 词缀总值(词缀属性.闪避) / 100f);

        // 潜行值：敏捷×2 + 意志×1 + 词缀（探索躲避/偷袭判定）
        public int 潜行值 => 敏捷 * 2 + 意志 + 词缀总值(词缀属性.潜行);

        // 感知：智慧×1 + 意志×1（发现资源/陷阱/先手判定）
        public int 感知 => 智慧 + 意志;

        // 恐惧抗性%：意志×1.5（夜晚/尸群/恐怖事件判定）
        public float 恐惧抗性 => Math.Min(0.8f, 意志 * 1.5f / 100f);

        // 感染抗性%：体质×1 + 意志×0.5（封顶 50，对感染累积的减免）
        public int 感染抗性 => Math.Min(50, 体质 + 意志 / 2);

        // 疲劳恢复速度（每小时恢复点数）：体质×0.5 + 2（睡觉加倍）
        public int 疲劳恢复率 => 2 + 体质 / 2;

        // 士气恢复速度（每小时）：意志×0.3 + 1
        public int 士气恢复率 => 1 + 意志 * 3 / 10;

        // 抗性百分数（装备来源）：非线性收益递减 + 硬上限 50%，防无脑堆叠免伤。
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

        // 兼容旧字段（读档迁移用）：旧"体力/智力/魔力/精力/物理/魔法"映射
        public int 体力 { get => 体质; set => 体质 = value; }
        public int 智力 { get => 智慧; set => 智慧 = value; }
        public int 魔力 { get => 疲劳; set => 疲劳 = value; }
        public int 精力 { get => 行动点; set => 行动点 = value; }
        public int 物理伤害 => 近战伤害;   // 旧战斗投影兼容：物理=近战
        public int 魔法伤害 => 枪械伤害;   // 旧战斗投影兼容：魔法槽位=枪械
        public int 最大魔力 => Math.Max(1, 最大行动点);   // 兼容占位（疲劳无上限池）
        public int 最大精力 => 最大行动点;
        public int 速度加成 => 词缀总值(词缀属性.速度);   // 旧字段兼容（速度已含词缀）
        public float 命中加成 => 0f;   // 旧字段兼容（命中系统改造后接感知）

        // 末日通用交易品（过渡货币，第二阶段迁移纯以物易物；初始 8 银）
        public int 铜币 = 800;

        // 已装备物品数值总和（标识 -> 加成 由解析器提供；未接线返回 0）
        private int 装备数值(Func<string, int> 加成)
        {
            if (加成 == null) return 0;
            int 总 = 0;
            foreach (var e in 装备)
                if (!string.IsNullOrEmpty(e.标识)) 总 += 加成(e.标识);
            return 总;
        }

        // 防御加成合计（总防御用）
        private int 防御加成合计 => 装备数值(防御加成解析);

        // 词缀总值：遍历已装备的词缀求和
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

        // 背包物资总数（负重占用）
        private int 背包计数总量()
        {
            int 总 = 0;
            foreach (var 堆叠 in 背包) if (堆叠 != null) 总 += 堆叠.数量;
            return 总;
        }

        // 是否超重（末日核心限制：带太多跑不动）
        public bool 超重 => 负重占用 > 负重上限;

        // 超重惩罚比例（0~0.5：超重越多速度越慢）
        public float 超重惩罚
        {
            get
            {
                if (!超重) return 0f;
                int 超出 = 负重占用 - 负重上限;
                return Math.Min(0.5f, 超出 / 100f);
            }
        }

        // ---------- 生存状态操作 ----------

        public void 恢复生命(int 数值) => 生命 = 夹(生命 + 数值, 0, 最大生命);
        public void 受到伤害(int 数值) => 生命 = Math.Max(0, 生命 - 数值);
        public bool 消耗行动点(int 数值)
        {
            if (行动点 < 数值) return false;
            行动点 -= 数值;
            return true;
        }
        public void 恢复行动点(int 数值) => 行动点 = 夹(行动点 + 数值, 0, 最大行动点);

        // 生存状态增减（0~100 夹取）
        public void 调整生存状态(生存状态类型 类型, int 数值)
        {
            switch (类型)
            {
                case 生存状态类型.饥饿: 饥饿 = 夹(饥饿 + 数值, 0, 100); break;
                case 生存状态类型.口渴: 口渴 = 夹(口渴 + 数值, 0, 100); break;
                case 生存状态类型.疲劳: 疲劳 = 夹(疲劳 + 数值, 0, 100); break;
                case 生存状态类型.感染度: 感染度 = 夹(感染度 + 数值, 0, 100); break;
                case 生存状态类型.士气: 士气 = 夹(士气 + 数值, 0, 100); break;
                case 生存状态类型.噪音: 噪音值 = 夹(噪音值 + 数值, 0, 100); break;
            }
        }

        // 每小时生存结算（游戏时钟调用）：饥饿/口渴上升、疲劳与士气自动调节
        public void 每小时结算()
        {
            // 饥饿/口渴随时间上升（体质/意志减缓）
            int 饿速 = 1 + (饥饿 >= 50 ? 1 : 0);
            饥饿 = 夹(饥饿 + 饿速, 0, 100);
            口渴 = 夹(口渴 + 2, 0, 100);
            // 疲劳自动下降（休息），疲劳恢复率
            疲劳 = 夹(疲劳 - 疲劳恢复率, 0, 100);
            // 士气缓慢恢复
            士气 = 夹(士气 + 士气恢复率, 0, 100);
            // 极端饥饿/口渴掉血
            if (饥饿 >= 100) 受到伤害(2);
            if (口渴 >= 100) 受到伤害(3);
        }

        // 被丧尸咬到：累积感染度（受感染抗性减免）
        public void 被感染(int 基础值)
        {
            int 实际 = Math.Max(1, 基础值 * (100 - 感染抗性) / 100);
            感染度 = 夹(感染度 + 实际, 0, 100);
        }

        // ---------- 装备 ----------

        public string 装备标识(string 槽位)
        {
            foreach (var e in 装备) if (e.槽位 == 槽位) return e.标识;
            return "";
        }

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

        public 装备记录 卸下装备(string 槽位)
        {
            for (int i = 装备.Count - 1; i >= 0; i--)
                if (装备[i].槽位 == 槽位) { var 记录 = 装备[i]; 装备.RemoveAt(i); return 记录; }
            return null;
        }

        public string 饰品目标槽()
        {
            if (string.IsNullOrEmpty(装备标识("饰品1"))) return "饰品1";
            if (string.IsNullOrEmpty(装备标识("饰品2"))) return "饰品2";
            return "饰品1";
        }

        public bool 已装备(string 标识)
        {
            foreach (var e in 装备) if (e.标识 == 标识) return true;
            return false;
        }

        // ---------- 探索（已清空地点） ----------

        public bool 已清空(string 地点标识) => 已清空地点.Contains(地点标识);
        public void 标记清空(string 地点标识)
        {
            if (!已清空地点.Contains(地点标识)) 已清空地点.Add(地点标识);
        }

        // 升级所需经验（每级递增）
        public int 升级所需经验 => 等级 * 25;

        // 数值夹取（替代 UnityEngine.Mathf，保证零 Unity 依赖）
        private static int 夹(int 值, int 最小, int 最大) => 值 < 最小 ? 最小 : (值 > 最大 ? 最大 : 值);

        // ---------- 背包（以物易物） ----------

        public void 添加物品(string 标识, int 数量 = 1)
        {
            if (string.IsNullOrEmpty(标识)) return;
            foreach (var 堆叠 in 背包)
                if (堆叠.标识 == 标识) { 堆叠.数量 += 数量; return; }
            背包.Add(new 物品堆叠(标识, 数量));
        }

        public void 添加堆叠(物品堆叠 堆叠)
        {
            if (堆叠 == null || string.IsNullOrEmpty(堆叠.标识)) return;
            背包.Add(堆叠);
        }

        public List<词缀条> 背包词缀(string 标识)
        {
            foreach (var 堆叠 in 背包)
                if (堆叠.标识 == 标识 && 堆叠.数量 > 0) return 堆叠.词缀;
            return null;
        }

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

        // ---------- 加点 ----------

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

        // ---------- 技能 ----------

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

        // ---------- 任务 ----------

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

        // ---------- 经验 ----------

        // 获得经验并处理升级链（给自由属性点 + 回 50% 状态——末日后不再满血复活）
        public bool 获得经验(int 数值)
        {
            经验 += 数值;
            bool 升级了 = false;
            while (经验 >= 升级所需经验)
            {
                经验 -= 升级所需经验;
                等级++;
                自由属性点 += 3;
                // 末日规则：升级只回 50% 生命与行动点（保留血线张力）
                生命 = Math.Max(1, 最大生命 / 2);
                行动点 = Math.Max(1, 最大行动点 / 2);
                升级了 = true;
            }
            return 升级了;
        }
    }
