using System;
using System.Collections.Generic;

    // 背包中的一种物品及数量
    [Serializable]
    public class 物品堆叠
    {
        public string 标识;
        public int 数量;

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

    // 玩家档案：纯 C# 领域模型（零 UnityEngine 依赖），可整体序列化存档。
    // 字段名与旧 玩家状态 一致，保证旧档字段兼容。
    [Serializable]
    public class 玩家档案
    {
        // 注入：装备加成解析器（由装配层接到 DataService；标识 -> 加成值）
        [NonSerialized] public Func<string, int> 武器攻击解析;
        [NonSerialized] public Func<string, int> 防具防御解析;

        // —— 属性 ——
        public int 等级 = 1;
        public int 经验 = 0;
        public int 生命 = 20;
        public int 魔力 = 20;
        public int 金币 = 3;
        public float 游戏分钟数 = 420f;           // 6:00 开始；1 现实秒 = 2 游戏分钟
        public string 武器标识 = "生锈短剑";         // 初始装备
        public string 防具标识 = "";                // 初始无防具
        public string 当前节点 = "";               // 剧情进度（用于存档恢复）
        public List<物品堆叠> 背包 = new List<物品堆叠>();
        public List<技能掌握> 已学技能 = new List<技能掌握>();
        public List<任务进度> 任务 = new List<任务进度>();

        // —— 4 大基础属性 + 自由属性点 ——
        public int 体力 = 5;
        public int 力量 = 5;
        public int 智力 = 5;
        public int 敏捷 = 5;
        public int 自由属性点 = 0;                 // 升级获得，自行分配到 4 大属性

        // 技能熟练度等级上限
        public const int 熟练等级上限 = 5;

        // —— 派生数值（总属性 = 基础 + 属性 + 装备）——
        public int 物理伤害 => 力量 * 2 + (武器攻击解析?.Invoke(武器标识) ?? 0);
        public int 魔法伤害 => 智力 * 2;
        public int 总攻击 => 物理伤害;             // 兼容现有战斗（普攻走物理）
        public int 总防御 => 防御 + (防具防御解析?.Invoke(防具标识) ?? 0);
        public int 最大生命 => 20 + 体力 * 5 + 等级 * 5;
        public int 最大魔力 => 20 + 智力 * 3 + 等级 * 3;
        public float 暴击概率 => 敏捷 * 0.01f;
        public float 闪避概率 => 敏捷 * 0.01f;

        // 防御基础值（旧字段保留，等级不再直接加，靠 防具/属性）
        public int 防御 = 2;
        public int 攻击 = 5;

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
