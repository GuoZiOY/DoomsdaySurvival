using System.Collections.Generic;

// 物品工具：物品展示文本的共用格式化（详情区/背包等面板复用）。
public static class 物品工具
{
    // 名称文本：品质标签 + 名称（一个文本，中间空格，如 "[稀有] 铁剑"）；物品与技能共用
    public static string 品质名称(品质 品质档, string 名称) => $"{品质工具.标签(品质档) } {名称}";

    // 有效品质：堆叠品质覆盖（合成提升）优先，否则取物品模板品质
    public static 品质 有效品质(物品堆叠 堆叠, 物品数据 模板)
        => !string.IsNullOrEmpty(堆叠?.品质) ? 数据解析.枚举<品质>(堆叠.品质) : 模板.品质档;

    // 详情区数值行：装备类合并 攻击/防御/生命/抗性 加成 + 实例词缀；恢复/技能书 特例
    public static string 数值文本(DataService 数据, 物品数据 物品, List<词缀条> 词缀 = null)
    {
        switch (物品.类型)
        {
            case "恢复": return $"恢复 {物品.恢复量} 点生命";
            case "技能书":
                if (!string.IsNullOrEmpty(物品.技能) && 数据.技能.TryGetValue(物品.技能, out var 技能))
                    return $"可学习：{技能.名称}（消耗 {技能.消耗魔力} 魔力）";
                return "";
            default:   // 武器/防具/饰品：合并 攻击/防御/生命/抗性 加成 + 词缀（换行独立列出）
                {
                    var 段 = new List<string>();
                    if (物品.攻击加成 > 0) 段.Add($"攻击 +{物品.攻击加成}");
                    if (物品.防御加成 > 0) 段.Add($"防御 +{物品.防御加成}");
                    if (物品.生命加成 > 0) 段.Add($"生命 +{物品.生命加成}");
                    if (物品.抗性 > 0) 段.Add($"抗性 +{物品.抗性}%");
                    string 基础 = string.Join("   ", 段);
                    if (词缀 != null && 词缀.Count > 0)
                    {
                        var 服务 = ServiceRegistry.Get<词缀服务>();
                        if (服务 != null)
                        {
                            var 行 = new List<string> { 基础 };
                            foreach (var 条 in 词缀) if (!string.IsNullOrEmpty(服务.文本(条))) 行.Add(服务.文本(条));
                            return string.Join("\n", 行);
                        }
                    }
                    return 基础;
                }
        }
    }

    // 完整详情文本（信息面板/物品网格面板共用）：名称/类型/描述/数值(含词缀)/形状/重量/价值/堆叠/耐久/操作提示
    public static string 构建详情(玩家档案 档案, DataService 数据, 物品堆叠 堆叠, 背包服务 服务, List<词缀条> 词缀 = null)
    {
        if (堆叠 == null) return "";
        if (!数据.物品.TryGetValue(堆叠.标识, out var 物品)) return 堆叠.标识;
        var 形状 = 服务.形状解析?.Invoke(堆叠.标识) ?? new 物品形状(1, 1);
        string 数值行 = 数值文本(数据, 物品, 词缀 ?? 堆叠.词缀);
        string 操作提示 = 物品.恢复量 > 0 ? "双击使用" : (!string.IsNullOrEmpty(物品.槽位) ? "双击装备" : "");
        int 上限 = 服务.堆叠上限(堆叠.标识);
        string 堆叠文本 = 上限 > 1 ? $"堆叠 {堆叠.数量}/{上限}" : $"数量 {堆叠.数量}";
        int 耐上 = 档案.有效最大耐久(堆叠.标识);
        string 耐文本 = 耐上 > 0 ? $" · 耐久 {堆叠.当前耐久}/{耐上}" : "";
        string 品质名称行 = 品质名称(有效品质(堆叠, 物品), 物品.名称);
        return $"<b>{品质名称行}</b>（{物品.类型}）\n{物品.描述}\n{数值行}\n形状 {形状.宽}×{形状.高} · 重量 {物品.重量} · 价值 {物品.价值} · {堆叠文本}{耐文本}\n{操作提示}";
    }
}
