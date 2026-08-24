using System.Collections.Generic;
using UnityEngine;

// 词缀服务：装备随机词条的生成（暗黑式）。按 物品类型 → 词缀池 + 品质 → 词缀条数 抽取掷值。
// 掉落/制作在产出装备时调用 生成() 得到该件实例的 词缀列表，随 物品堆叠/装备记录 存档。
public sealed class 词缀服务
{
    private readonly DataService 数据;

    public 词缀服务(DataService 数据) { this.数据 = 数据; }

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
            int 数值 = Random.Range(定义.最小, 定义.最大 + 1);
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

    // —— 合成/附魔 用 ——

    // 品质 → 最大词缀条数（合成判定是否可追加用）
    public static int 最大条数(品质 档)
    {
        switch (档)
        {
            case 品质.普通: return 1;
            case 品质.优秀: return 2;
            case 品质.稀有: return 3;
            case 品质.史诗: return 3;
            case 品质.英雄: return 4;
            case 品质.传奇: return 4;
            default: return 1;
        }
    }

    // 取词缀池（按指定品质，合成/附魔 升级品质后用）：属性匹配 + 词缀品质 ≤ 指定品质
    public List<词缀定义> 取池(物品数据 物品, 品质 档)
    {
        var 池 = new List<词缀定义>();
        foreach (var 定义 in 数据.词缀.Values)
            if (定义.品质档 <= 档 && 允许属性(物品, 定义.属性枚举)) 池.Add(定义);
        return 池;
    }

    // 追加一条新词缀（按指定品质取池，避开已有；池空或全已用返回 null）
    public 词缀条 追加一条(物品数据 物品, 品质 档, List<词缀条> 已有)
    {
        var 池 = 取池(物品, 档);
        if (池.Count == 0) return null;
        var 已用 = new HashSet<string>();
        if (已有 != null) foreach (var a in 已有) 已用.Add(a.标识);
        var 定义 = 抽一条(池, 已用);
        if (定义 == null) return null;
        return new 词缀条(定义.标识, Random.Range(定义.最小, 定义.最大 + 1));
    }

    // 升级一条已有词缀（同属性、更高品质、≤目标品质；无可升级返回 null）
    public 词缀条 升级一条(物品数据 物品, 词缀条 条, 品质 目标品质)
    {
        if (条 == null || !数据.词缀.TryGetValue(条.标识, out var 当前定义)) return null;
        var 候选 = new List<词缀定义>();
        foreach (var 定义 in 数据.词缀.Values)
            if (定义.属性枚举 == 当前定义.属性枚举
                && 定义.品质档 > 当前定义.品质档
                && 定义.品质档 <= 目标品质
                && 允许属性(物品, 定义.属性枚举))
                候选.Add(定义);
        if (候选.Count == 0) return null;
        var 抽 = 抽一条(候选, new HashSet<string>());
        if (抽 == null) return null;
        return new 词缀条(抽.标识, Random.Range(抽.最小, 抽.最大 + 1));
    }

    // 追加一条指定属性的词缀（镶缀用：宝石指定属性；已有该属性则原位升级）
    // 返回新词缀；若升级则替换 已有 中对应下标的条，返回 null 表示无可操作性
    public 词缀条 追加指定属性(物品数据 物品, 品质 档, 词缀属性 指定, List<词缀条> 已有)
    {
        // 已有同属性词缀 → 原位升级（同属性升到更高品质）
        if (已有 != null)
            for (int i = 0; i < 已有.Count; i++)
                if (数据.词缀.TryGetValue(已有[i].标识, out var 定) && 定.属性枚举 == 指定)
                {
                    var 升 = 升级一条(物品, 已有[i], 档);
                    if (升 != null) 已有[i] = 升;
                    return 升;   // null = 该属性已达当前品质上限
                }
        // 否则新增一条该属性词缀（避开已用标识）
        var 池 = 取池(物品, 档);
        var 候选 = new List<词缀定义>();
        foreach (var d in 池) if (d.属性枚举 == 指定) 候选.Add(d);
        if (候选.Count == 0) return null;
        var 已用 = new HashSet<string>();
        if (已有 != null) foreach (var a in 已有) 已用.Add(a.标识);
        var 抽 = 抽一条(候选, 已用);
        return 抽 == null ? null : new 词缀条(抽.标识, Random.Range(抽.最小, 抽.最大 + 1));
    }

    // —— 内部 ——

    // 品质 → 词缀条数（普通1/优秀1-2/稀有2-3/史诗3/英雄3-4/传奇4）
    private static int 条数按品质(品质 档)
    {
        switch (档)
        {
            case 品质.普通: return 1;
            case 品质.优秀: return Random.Range(1, 3);
            case 品质.稀有: return Random.Range(2, 4);
            case 品质.史诗: return 3;
            case 品质.英雄: return Random.Range(3, 5);
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
            case "武器": return 属性 == 词缀属性.攻击 || 属性 == 词缀属性.魔攻 || 属性 == 词缀属性.速度 || 属性 == 词缀属性.暴击 || 属性 == 词缀属性.命中 || 属性 == 词缀属性.潜行;
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
        float 掷 = Random.value * 总;
        foreach (var d in 池)
        {
            if (已用.Contains(d.标识)) continue;
            掷 -= Mathf.Max(0.01f, d.权重);
            if (掷 <= 0) return d;
        }
        return 池[池.Count - 1];
    }
}