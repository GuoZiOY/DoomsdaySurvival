// ============================================================
// 配件槽：**哪件装备能装哪种配件**（纯函数，唯一口径）。
// 位置在 Data 而不是 Domain（刀36 挪）：只吃 物品数据 DTO，且 DataService 数据校验要用 —— 同 建筑外形.cs。
// 为什么单独一份：槽位规则会被三处问（装配校验 / 详情面板显示可用槽 / 数据校验），
//   写三遍必漂移（这个项目在"门口四邻"上栽过两次，同一种病）。
//
// 配件槽（`物品数据.槽位` 对 类型=配件 的取值）：枪口 / 瞄具 / 弹匣 / 枪托 / 握把 / 刃口 / 插板
//   · 枪械（手枪/步枪/霰弹枪）：枪口 / 瞄具 / 弹匣 / 枪托 / 握把
//   · 弓弩（弓/弩）：瞄具 / 握把（没有枪管与弹匣，装不了枪口/弹匣/枪托）
//   · 近战（刀/斧/棍棒/匕首）：握把 / 刃口
//   · 防具：插板（胸口/腹部都算"身上能塞板子"的地方）
// 刀55：**"是不是枪械"改由 `武器种类` 判**（原先用 `攻击距离 > 1`）——
//   否则长柄近战（射程 2：长木棍/草叉/自制长矛/撬棍/钢管）会被当成枪，开出一整套枪械槽。
//
// 不变量（数据校验 会查）：配件的 `槽位` 必须在这个清单里；
//   配件 `槽位` 与装备可用槽位不匹配 → 装不上（不是静默装到别的槽）。
// ============================================================
public static class 配件槽
{
    public const string 枪口 = "枪口";
    public const string 瞄具 = "瞄具";
    public const string 弹匣 = "弹匣";
    public const string 枪托 = "枪托";
    public const string 握把 = "握把";
    public const string 刃口 = "刃口";
    public const string 插板 = "插板";

    public static readonly string[] 全部 = { 枪口, 瞄具, 弹匣, 枪托, 握把, 刃口, 插板 };

    public static bool 认得出(string 槽) => System.Array.IndexOf(全部, 槽) >= 0;

    // 这件装备上有哪些配件槽（空数组 = 不能装任何配件）
    public static string[] 装备可用(物品数据 装备)
    {
        if (装备 == null) return System.Array.Empty<string>();
        if (装备.类型 == "武器")
        {
            var 种 = 装备.武器种类枚举;
            if (武器种类判定.是枪械(种)) return new[] { 枪口, 瞄具, 弹匣, 枪托, 握把 };
            if (武器种类判定.是弓弩(种)) return new[] { 瞄具, 握把 };
            return new[] { 握把, 刃口 };   // 近战（含射程 2 的长柄）
        }
        if (装备.类型 == "防具")
        {
            // 插板只装在"躯干类"装备上：胸部 / 弹挂（腰封是弹匣袋，装不了板子）
            if (装备.槽位 == "胸部" || 装备.槽位 == "弹挂") return new[] { 插板 };
            return System.Array.Empty<string>();
        }
        return System.Array.Empty<string>();
    }

    // 配件物品 能否装到 装备 上
    public static bool 允许(物品数据 装备, 物品数据 配件)
    {
        if (装备 == null || 配件 == null || 配件.类型 != "配件") return false;
        if (!认得出(配件.槽位)) return false;
        foreach (var 槽 in 装备可用(装备)) if (槽 == 配件.槽位) return true;
        return false;
    }
}
