// ============================================================
// 配件加成：**物品 → 某类加成值** 的唯一口径（配件与装备本体共用）。
// 位置在 Data 而不是 Domain（刀36 挪）：它唯一的输入是 物品数据 这个 **DTO**，DataService 数据校验也要用它
//   → 放最底层就没有 Data → Domain 的反向依赖（域层/UI 再用都是允许方向）。与 建筑外形.cs 同一个理由。
// 为什么单独一个文件：装备派生数值有三处入口（装备管理器 求和、详情文本、配件槽 UI 提示），
//   口径写三遍就会漂移 —— 这里一份，谁要用谁调。
// 对应关系（改 物品数据 字段名时**必须同时改这里**，否则静默变 0）：
//   攻击→攻击加成 / 防御→防御加成 / 生命→生命加成 / 抗性→抗性 / 负重→负重加成
//   暴击→暴击加成 / 命中→命中加成 / 闪避→闪避加成 / 速度→速度加成 / 潜行→潜行加成
// 可正可负：负面值就是"取舍"（消音器 潜行↑ 攻击↓）。
// ============================================================
public static class 配件加成
{
    public static int 取(物品数据 物, 加成类型 类)
    {
        if (物 == null) return 0;
        switch (类)
        {
            case 加成类型.攻击: return 物.攻击加成;
            case 加成类型.防御: return 物.防御加成;
            case 加成类型.生命: return 物.生命加成;
            case 加成类型.抗性: return 物.抗性;
            case 加成类型.负重: return 物.负重加成;
            case 加成类型.暴击: return 物.暴击加成;
            case 加成类型.命中: return 物.命中加成;
            case 加成类型.闪避: return 物.闪避加成;
            case 加成类型.速度: return 物.速度加成;
            case 加成类型.潜行: return 物.潜行加成;
            case 加成类型.弹匣容量: return 物.弹匣容量加成;   // 弹匣槽配件改造容量（扩容弹匣）
            default: return 0;
        }
    }

    // 加成类型 → 中文短名（详情文本用；"暴击 +3%" 那种）
    public static string 名(加成类型 类)
    {
        switch (类)
        {
            case 加成类型.攻击: return "攻击";
            case 加成类型.防御: return "防御";
            case 加成类型.生命: return "生命上限";
            case 加成类型.抗性: return "抗性";
            case 加成类型.负重: return "负重";
            case 加成类型.暴击: return "暴击率";
            case 加成类型.命中: return "命中率";
            case 加成类型.闪避: return "闪避率";
            case 加成类型.速度: return "速度";
            case 加成类型.潜行: return "潜行";
            case 加成类型.弹匣容量: return "弹匣容量";
            case 加成类型.医疗: return "医疗效果";
            default: return "加成";
        }
    }

    // 是不是百分比类（UI 显示时加 %）
    public static bool 是百分比(加成类型 类)
        => 类 == 加成类型.暴击 || 类 == 加成类型.命中 || 类 == 加成类型.闪避 || 类 == 加成类型.抗性 || 类 == 加成类型.医疗;

    // 列出这个物品能提供的加成（详情/提示用）：零值不列
    public static System.Collections.Generic.List<(加成类型 类, int 值)> 全部(物品数据 物)
    {
        var 出 = new System.Collections.Generic.List<(加成类型, int)>();
        foreach (加成类型 类 in System.Enum.GetValues(typeof(加成类型)))
        {
            int v = 取(物, 类);
            if (v != 0) 出.Add((类, v));
        }
        return 出;
    }
}