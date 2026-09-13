using System.Collections.Generic;

// ============================================================
// 敌人 AI（纯 C#，零 UnityEngine）—— "抽哪一招 / 打谁"这三件事的**唯一一份实现**。
//
// 为什么抽出来（v51 刀28，审计 D15）：
//   ① 原来 AI 有**三条入口**（选定敌人意图 / 自动攻击 / 旧回合制 敌人行动），目标策略各不相同；
//   ② `AI抽行动` 在同一轮被调**两次**（选定意图时抽一次决定"要不要放技能"，满条执行时又抽一次）
//      → JSON 权重被采样两次：实际技能率与配置不符，而且"读条显示攻击、满条却甩技能"（承诺制被破坏）；
//   ③ 随机源是不可播种的 `UnityEngine.Random` → 抽招结果无法离线复现。
//   抽成纯函数 + **掷点由调用方给** 之后，三条都能被断言钉住。
//
// 调用方负责：掷点（`Random.Range(0, 敌人AI.权重总和(敌数据))`）、把存活列表查出来、以及执行招式。
// ============================================================
public static class 敌人AI
{
    public const string 普攻 = "普攻";

    // 权重总和（调用方拿它去掷点）。权重 < 1 一律按 1 算 —— 与拆分前 `Mathf.Max(1, 权重)` 同口径
    public static int 权重总和(敌人数据 敌数据)
    {
        if (敌数据?.行动表 == null) return 0;
        int 总 = 0;
        foreach (var a in 敌数据.行动表) 总 += Max1(a.权重);
        return 总;
    }

    // 按权重抽一条行动。掷点 ∈ [0, 权重总和)；越界（含空表）→ 普攻
    public static string 抽行动(敌人数据 敌数据, int 掷点)
    {
        if (敌数据?.行动表 == null || 敌数据.行动表.Length == 0) return 普攻;
        int 掷 = 掷点;
        foreach (var a in 敌数据.行动表)
        {
            掷 -= Max1(a.权重);
            if (掷 < 0) return a.行动;
        }
        return 普攻;
    }

    // 选目标（敌方那一侧）：目标策略 "残血" → 血最少的；其余 → 按掷点随机。
    // 存活列表为空 → null（调用方自己兜底）。**不改动传入的列表**（内部先复制再排序）。
    public static 战斗单位 选目标(敌人数据 敌数据, List<战斗单位> 存活, int 掷点)
    {
        if (存活 == null || 存活.Count == 0) return null;
        if (敌数据?.目标策略 == "残血")
        {
            var 排序 = new List<战斗单位>(存活);
            排序.Sort((a, b) => a.生命.CompareTo(b.生命));
            return 排序[0];
        }
        return 存活[Clamp(掷点, 0, 存活.Count - 1)];
    }

    // 选友方（治疗/增益那类技能）：自己 → 施法者；我方单体 → 最残血；其余（我方全体）→ 第一个。
    // 存活列表为空 → 兜底（调用方给：原实现是"我方则玩家、否则敌方第一个"）。
    public static 战斗单位 选友方(战斗单位 施法者, 技能数据 技能, List<战斗单位> 存活, 战斗单位 兜底)
    {
        if (存活 == null || 存活.Count == 0) return 兜底;
        if (技能 != null && 技能.目标枚举 == 目标类型.自己) return 施法者;
        if (技能 != null && 技能.目标枚举 == 目标类型.我方单体)
        {
            var 排序 = new List<战斗单位>(存活);
            排序.Sort((a, b) => a.生命.CompareTo(b.生命));
            return 排序[0];
        }
        return 存活[0];
    }

    private static int Max1(int 权重) => 权重 < 1 ? 1 : 权重;
    private static int Clamp(int v, int 小, int 大) => v < 小 ? 小 : (v > 大 ? 大 : v);
}
