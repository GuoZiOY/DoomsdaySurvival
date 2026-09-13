using System.Collections.Generic;
using UnityEngine;

// BattleService · AI 分部 —— 敌人 AI（旧回合制入口/抽行动/选目标）
// （v51 刀25 纯组织性拆分：**零逻辑改动**，只是把一个 1486 行的文件按原有分节切开。
//    「哪些该真的拆成类」的判据见 docs/优化实施进度.md §一之十一：先拆文件，再沉纯逻辑。）
public sealed partial class BattleService
{
    // ===== 敌人AI（P6） =====
    private void 敌人行动(战斗单位 敌人)
    {
        var 敌数据 = 敌人.源数据;
        string 行动 = 敌数据?.行动表 != null && 敌数据.行动表.Length > 0 ? AI抽行动(敌数据) : "普攻";
        // 治疗/增益类技能（我方单体/我方全体/自己 目标）：选友方（同伴）目标，否则会误治疗玩家
        战斗单位 目标;
        if (行动 != "普攻" && 数据.技能.TryGetValue(行动, out var 技能) && 技能.目标枚举 != 目标类型.敌方单体 && 技能.目标枚举 != 目标类型.敌方全体)
            目标 = AI选友方(敌人, 技能);
        else 目标 = AI选目标(敌人, 敌数据);
        if (行动 == "普攻") 执行攻击(敌人, 目标);
        else 执行技能(敌人, 行动, 目标);
    }

    // 治疗/增益选友方目标：同伴 = 与施法者同阵营；治疗选最残血，其余选第一个
    private 战斗单位 AI选友方(战斗单位 施法者, 技能数据 技能)
    {
        var 同伴 = 施法者.是否我方 ? 我方 : 敌方;
        var 存活 = 同伴.FindAll(u => u.存活);
        if (存活.Count == 0) return 施法者.是否我方 ? 玩家 : 敌方[0];
        if (技能.目标枚举 == 目标类型.自己) return 施法者;
        if (技能.目标枚举 == 目标类型.我方单体)
        {
            存活.Sort((a, b) => a.生命.CompareTo(b.生命));
            return 存活[0];
        }
        return 存活[0];
    }

    private string AI抽行动(敌人数据 敌数据)
    {
        int 总 = 0; foreach (var a in 敌数据.行动表) 总 += Mathf.Max(1, a.权重);
        int 掷 = Random.Range(0, 总);
        foreach (var a in 敌数据.行动表)
        {
            掷 -= Mathf.Max(1, a.权重);
            if (掷 < 0) return a.行动;
        }
        return "普攻";
    }

    private 战斗单位 AI选目标(战斗单位 敌人, 敌人数据 敌数据)
    {
        var 存活 = 我方.FindAll(u => u.存活);
        if (存活.Count == 0) return null;
        if (敌数据?.目标策略 == "残血")
        {
            存活.Sort((a, b) => a.生命.CompareTo(b.生命));
            return 存活[0];
        }
        return 存活[Random.Range(0, 存活.Count)];
    }

}
