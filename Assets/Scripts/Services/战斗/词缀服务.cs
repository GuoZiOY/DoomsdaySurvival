using System.Collections.Generic;
using UnityEngine;

// 词缀服务：装备随机词条的生成（暗黑式）。按 物品类型 → 词缀池 + 品质 → 词缀条数 抽取掷值。
// 掉落/制作在产出装备时调用 生成() 得到该件实例的 词缀列表，随 物品堆叠/装备记录 存档。
public sealed class 词缀服务
{
    private readonly DataService 数据;

    // 随机源（v51 刀33）：词缀掷值原本直接掷 UnityEngine.Random（全局静态、不可播种）。
    // 它是**战利品生成**的一部分 —— 不接进来的话，"同种子复现那一场战斗"复现不出同一件掉落物。
    // 默认种子固定（不是时间）：同一个档、同一次开局顺序 → 词缀也可复现；战斗开局会再派生子种子覆盖它。
    private 随机源 随机 = new 随机源(20260913);

    public 词缀服务(DataService 数据) { this.数据 = 数据; }

    // 战斗开局调用：把词缀掷值也纳入本场种子（见 BattleService.开始战斗）
    public void 设定随机种子(int 种子) => 随机 = new 随机源(种子);

    // 为装备生成随机词缀：非装备返回 null（无词缀）；装备返回 词缀条[]（可为空数组表示无词条）
    public List<词缀条> 生成(物品数据 物品)
    {
        if (物品 == null) return null;
        bool 装备 = 物品.类型 == "武器" || 物品.类型 == "防具" || 物品.类型 == "饰品";
        if (!装备) return null;
        var 池 = 抽取池(物品);
        if (池 == null || 池.Count == 0) return null;
        int 条数 = 条数按品质(物品.品质档);
        var 结果 = new List<词缀条>();
        var 已用 = new HashSet<string>();
        int 尝试 = 0;
        while (结果.Count < 条数 && 尝试 < 条数 * 3 && 已用.Count < 池.Count)
        {
            尝试++;
            var 定义 = 抽一条(池, 已用);
            if (定义 == null) break;
            已用.Add(定义.标识);
            int 数值 = 随机.范围(定义.最小, 定义.最大 + 1);
            结果.Add(new 词缀条(定义.标识, 数值));
        }
        return 结果.Count > 0 ? 结果 : null;
    }

    // 词缀文本（如 "猛攻 +4"）：一条词缀的展示用，按词缀品质着色（复用品质工具）
    public string 文本(词缀条 条)
    {
        if (条 == null || !数据.词缀.TryGetValue(条.标识, out var 定义)) return "";
        string 内容 = !string.IsNullOrEmpty(定义.文本) ? 定义.文本 : $"{定义.名称} +{条.数值}";
        string 色 = ColorUtility.ToHtmlStringRGB(品质工具.颜色(定义.品质档));
        return $"<color=#{色}>{内容}</color>";
    }

    // —— 注：原有一段「合成/附魔/镶缀」用的接口（最大条数 / 取池 / 追加一条 / 升级一条 / 追加指定属性）——
    // v51 刀7c 整体删掉：**全仓库 0 外部调用**（旧《项目现状评价》也把"合成/附魔/镶缀"判为奇幻遗留：
    // "装备合成/魔法附魔/镶宝石——末日不需要，强化并入制作工作台"）。
    // 保留的是**在用**的那条路：生成() / 文本()（BattleService 掉落 与 物品工具 详情都在用）。

    // —— 内部 ——

    // 品质 → 词缀条数（普通1/优秀1-2/稀有2-3/史诗3/英雄3-4/传奇4）
    // 注：v51 刀33 起**不是 static**了 —— 它要用实例的 随机源（原来 static + 直接掷 UnityEngine.Random）
    private int 条数按品质(品质 档)
    {
        switch (档)
        {
            case 品质.普通: return 1;
            case 品质.优秀: return 随机.范围(1, 3);
            case 品质.稀有: return 随机.范围(2, 4);
            case 品质.史诗: return 3;
            case 品质.英雄: return 随机.范围(3, 5);
            case 品质.传奇: return 4;
            default: return 1;
        }
    }

    // 按物品类型 + 装备品质限档 取可用词缀池：只允许 属性匹配 且 词缀品质 ≤ 装备品质 的词缀进入候选。
    // 装备品质越高 → 候选池里允许的高品质词缀越多（高品质词缀靠低权重 → 更难抽中）。
    private List<词缀定义> 抽取池(物品数据 物品)
    {
        var 池 = new List<词缀定义>();
        foreach (var 定义 in 数据.词缀.Values)
            if (定义.品质档 <= 物品.品质档 && 允许属性(物品, 定义.属性枚举)) 池.Add(定义);
        return 池;
    }

    private static bool 允许属性(物品数据 物品, 词缀属性 属性)
    {
        switch (物品.类型)
        {
            case "武器": return 属性 == 词缀属性.攻击 || 属性 == 词缀属性.速度 || 属性 == 词缀属性.暴击 || 属性 == 词缀属性.命中 || 属性 == 词缀属性.潜行;
            case "防具": return 属性 == 词缀属性.防御 || 属性 == 词缀属性.生命 || 属性 == 词缀属性.抗性 || 属性 == 词缀属性.闪避 || 属性 == 词缀属性.负重;
            case "饰品": return 属性 == 词缀属性.生命 || 属性 == 词缀属性.抗性 || 属性 == 词缀属性.暴击 || 属性 == 词缀属性.闪避 || 属性 == 词缀属性.速度 || 属性 == 词缀属性.负重 || 属性 == 词缀属性.潜行;
            default: return false;   // 非装备无词缀
        }
    }

    // 加权抽一条（按权重，避开已用）
    private 词缀定义 抽一条(List<词缀定义> 池, HashSet<string> 已用)
    {
        float 总 = 0;
        foreach (var d in 池) if (!已用.Contains(d.标识)) 总 += Mathf.Max(0.01f, d.权重);
        if (总 <= 0) return null;
        float 掷 = 随机.值() * 总;
        foreach (var d in 池)
        {
            if (已用.Contains(d.标识)) continue;
            掷 -= Mathf.Max(0.01f, d.权重);
            if (掷 <= 0) return d;
        }
        return 池[池.Count - 1];
    }
}