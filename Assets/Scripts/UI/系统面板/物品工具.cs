using System.Collections.Generic;

// 物品工具：物品展示文本的共用格式化（详情区/背包等面板复用）。
public static class 物品工具
{
    // 名称文本：品质标签 + 名称（一个文本，中间空格，如 "[稀有] 铁剑"）；物品与技能共用
    public static string 品质名称(品质 品质档, string 名称) => $"{品质工具.标签(品质档) } {名称}";

    // 详情区数值行：装备类合并 攻击/防御/生命 加成；恢复/技能书 特例
    public static string 数值文本(DataService 数据, 物品数据 物品)
    {
        switch (物品.类型)
        {
            case "恢复": return $"恢复 {物品.恢复量} 点生命";
            case "技能书":
                if (!string.IsNullOrEmpty(物品.技能) && 数据.技能.TryGetValue(物品.技能, out var 技能))
                    return $"可学习：{技能.名称}（消耗 {技能.消耗魔力} 魔力）";
                return "";
            default:   // 武器/防具/饰品：合并 攻击/防御/生命 加成
                {
                    var 段 = new List<string>();
                    if (物品.攻击加成 > 0) 段.Add($"攻击 +{物品.攻击加成}");
                    if (物品.防御加成 > 0) 段.Add($"防御 +{物品.防御加成}");
                    if (物品.生命加成 > 0) 段.Add($"生命 +{物品.生命加成}");
                    return string.Join("   ", 段);
                }
        }
    }
}
