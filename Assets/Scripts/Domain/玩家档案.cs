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
        public int 最大生命 = 20;
        public int 生命 = 20;
        public int 攻击 = 5;
        public int 防御 = 2;
        public int 金币 = 3;
        public int 最大魔力 = 20;
        public int 魔力 = 20;
        public float 游戏分钟数 = 420f;           // 6:00 开始；1 现实秒 = 2 游戏分钟
        public string 武器标识 = "生锈短剑";         // 初始装备
        public string 防具标识 = "";                // 初始无防具
        public string 当前节点 = "";               // 剧情进度（用于存档恢复）
        public List<物品堆叠> 背包 = new List<物品堆叠>();
        public List<string> 已学技能 = new List<string>();
        public List<任务进度> 任务 = new List<任务进度>();

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

        // ---------- 装备 ----------

        public int 总攻击 => 攻击 + (武器攻击解析?.Invoke(武器标识) ?? 0);

        public int 总防御 => 防御 + (防具防御解析?.Invoke(防具标识) ?? 0);

        // ---------- 技能 ----------

        public bool 掌握技能(string 标识) => 已学技能.Contains(标识);

        public bool 学习技能(string 标识)
        {
            if (掌握技能(标识)) return false;
            已学技能.Add(标识);
            return true;
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

        // 获得经验并处理升级链（满血、属性提升）；返回是否升级
        public bool 获得经验(int 数值)
        {
            经验 += 数值;
            bool 升级了 = false;
            while (经验 >= 升级所需经验)
            {
                经验 -= 升级所需经验;
                等级++;
                最大生命 += 6;
                攻击 += 1;
                防御 += 1;
                生命 = 最大生命;
                升级了 = true;
            }
            return 升级了;
        }
    }
