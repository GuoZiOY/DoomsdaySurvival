using System;

// 成长管理器：属性加点/等级/经验/技能 领域逻辑（玩家档案 组合的子管理器——职责分离、调度器模式）。
// 负责：五维 加点/训练/设置/查询；等级与经验（升级回 50% 状态——经 玩家.生存管理）；技能 掌握/熟练/前提校验。
// 数据（属性/经验/已学技能/天赋）由 玩家档案 持有；状态上限 经 玩家.生存管理 读取。
public sealed class 成长管理器
{
    public readonly 玩家档案 玩家;
    public 成长管理器(玩家档案 玩家) { this.玩家 = 玩家; }

    // ================= 加点 =================

    public bool 加点(属性类型 类型, int 点数 = 1)
    {
        if (点数 <= 0 || 玩家.自由属性点 < 点数) return false;
        玩家.自由属性点 -= 点数;
        训练属性(类型, 点数);
        return true;
    }

    public void 训练属性(属性类型 类型, int 点数 = 1)
    {
        switch (类型)
        {
            case 属性类型.体质: 玩家.体质 += 点数; break;
            case 属性类型.力量: 玩家.力量 += 点数; break;
            case 属性类型.智慧: 玩家.智慧 += 点数; break;
            case 属性类型.敏捷: 玩家.敏捷 += 点数; break;
            case 属性类型.意志: 玩家.意志 += 点数; break;
        }
    }

    // 直接设置某属性为指定值（职业分布用：开局职业直接给定五维分布）
    public void 设置属性(属性类型 类型, int 值)
    {
        switch (类型)
        {
            case 属性类型.体质: 玩家.体质 = 值; break;
            case 属性类型.力量: 玩家.力量 = 值; break;
            case 属性类型.智慧: 玩家.智慧 = 值; break;
            case 属性类型.敏捷: 玩家.敏捷 = 值; break;
            case 属性类型.意志: 玩家.意志 = 值; break;
        }
    }

    public int 属性值(string 名)
    {
        switch (名)
        {
            case "体质": return 玩家.体质;
            case "力量": return 玩家.力量;
            case "智慧": return 玩家.智慧;
            case "敏捷": return 玩家.敏捷;
            case "意志": return 玩家.意志;
            default: return 0;
        }
    }

    // ================= 经验 =================

    public bool 获得经验(int 数值)
    {
        玩家.经验 += (int)(数值 * 经验修正);
        bool 升级了 = false;
        while (玩家.经验 >= 升级所需经验)
        {
            玩家.经验 -= 升级所需经验;
            玩家.等级++;
            玩家.自由属性点 += 3;
            // 升级只回 50% 状态（末日后不再满血复活）
            玩家.生命 = Math.Max(1, 玩家.生存管理.最大生命 / 2);
            玩家.行动点 = Math.Max(1, 玩家.生存管理.最大行动点 / 2);
            升级了 = true;
        }
        return 升级了;
    }

    public int 升级所需经验 => 玩家.等级 * 25;

    // ================= 技能 =================

    public bool 掌握技能(string 标识) => 玩家.已学技能.Exists(s => s.标识 == 标识);
    public int 技能熟练等级(string 标识)
    {
        foreach (var s in 玩家.已学技能) if (s.标识 == 标识) return s.熟练等级;
        return 0;
    }

    public int 技能熟练度(string 标识)
    {
        foreach (var s in 玩家.已学技能) if (s.标识 == 标识) return s.熟练度;
        return 0;
    }

    public string 技能前提失败原因(技能数据 技能)
    {
        if (技能 == null) return "技能不存在。";
        if (玩家.体质 < 技能.需要体力) return $"体质不足（需要 {技能.需要体力}）。";
        if (玩家.力量 < 技能.需要力量) return $"力量不足（需要 {技能.需要力量}）。";
        if (玩家.智慧 < 技能.需要智力) return $"智慧不足（需要 {技能.需要智力}）。";
        if (玩家.敏捷 < 技能.需要敏捷) return $"敏捷不足（需要 {技能.需要敏捷}）。";
        if (玩家.意志 < 技能.需要意志) return $"意志不足（需要 {技能.需要意志}）。";
        return "";
    }

    public bool 学习技能(技能数据 技能)
    {
        if (技能 == null || 掌握技能(技能.标识)) return false;
        if (!string.IsNullOrEmpty(技能前提失败原因(技能))) return false;
        玩家.已学技能.Add(new 技能掌握(技能.标识));
        填入技能槽(技能.标识);   // 学习自动 填 战斗技能槽（固定 6 槽，填第一个空槽）
        return true;
    }

    // 战斗技能槽：固定 6 槽，自动填第一个空槽（已在槽/槽满 则不动）
    public void 填入技能槽(string 标识)
    {
        if (string.IsNullOrEmpty(标识)) return;
        玩家.战斗技能槽 ??= new System.Collections.Generic.List<string>();
        if (玩家.战斗技能槽.Contains(标识)) return;
        for (int i = 0; i < 6; i++)
        {
            while (玩家.战斗技能槽.Count <= i) 玩家.战斗技能槽.Add(null);
            if (string.IsNullOrEmpty(玩家.战斗技能槽[i])) { 玩家.战斗技能槽[i] = 标识; return; }
        }
    }

    public int 记录熟练度(string 标识, int 点数, int 每级阈值)
    {
        foreach (var s in 玩家.已学技能)
            if (s.标识 == 标识)
            {
                if (s.熟练等级 >= 玩家档案.熟练等级上限) return s.熟练等级;
                s.熟练度 += Math.Max(1, 点数);
                while (s.熟练度 >= 每级阈值 && s.熟练等级 < 玩家档案.熟练等级上限)
                {
                    s.熟练度 -= 每级阈值;
                    s.熟练等级++;
                }
                return s.熟练等级;
            }
        return 0;
    }

    // —— 天赋 修正系数 ——
    public float 经验修正 => 玩家.天赋.Contains("快速学习者") ? 1.10f : 1f;
    public float 医疗修正 => 玩家.天赋.Contains("医者仁心") ? 1.05f : 1f;
    public float 制作消耗修正 => 玩家.天赋.Contains("节俭") ? 0.95f : 1f;
    public float 夜晚消耗修正 => 玩家.天赋.Contains("夜行者") ? 0.85f : 1f;
}
