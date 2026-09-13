using System;
using System.Collections.Generic;

// ============================================================
// 敌人特性：**只留给敌人的那份"词缀"**（v51 刀45；用户 2026-09-13 拍板）
//
// 为什么装备侧砍掉、敌人侧保留（用户原话："词缀或许对于敌人来说有用，但对于装备来说不切实际"）：
//   装备上"随机 +5% 暴击"是 ARPG 的强化学，末日不成立；但**敌人变异**是这个世界观里最自然的东西
//   （感染者跑得快、皮厚、一巴掌更疼）——同样的"随机修正"机制，用在敌人身上就对了。
//
// 本刀只做 **Tier 1：属性型**（生成时一次性改数，**零战斗钩子**）：
//   迅捷 / 厚皮 / 强壮 / 凶暴 / 滑溜 / 巨体 / 迟钝 / 远视 …
//   Tier 2（事件型：狂暴/再生/爆裂/感染）要动 伤害 / 死亡 / 推进 三个钩子 —— 按用户拍板**以后单独一刀**。
//
// 来源（用户拍板：**只要随机的**）：战斗开始时逐只掷，走 `随机源`（刀33）→ 同种子可复现。
//   数据里不写死任何敌人带什么特性（Boss 也一样随机）；代价是"Boss 可能没特性"，这是用户接受的取舍。
//
// 效果落点（`战斗单位` 上已有的字段，全部纯数值）：生命 / 攻击 / 防御 / 速度 / 暴击 / 闪避 / 攻击距离 / 行动间隔
// ============================================================
public static class 敌人特性
{
    // 效果类型（JSON 里 `效果` 写这些名字；写错会被 数据引用核对 + DataService 报出来）
    // 效果名常量与清单的**单一真源在 Data**（`敌人特性效果名`）：DataService 校验也要用，而 Data 不能引用 Domain。
    public const string 生命 = 敌人特性效果名.生命;         // 百分比：+50 = 生命上限 ×1.5
    public const string 攻击 = 敌人特性效果名.攻击;         // 百分比
    public const string 防御 = 敌人特性效果名.防御;         // 点数：+15 = 减伤 +15%（1 点 = 1%）
    public const string 速度 = 敌人特性效果名.速度;         // 百分比
    public const string 暴击 = 敌人特性效果名.暴击;         // 点数：+15 = 暴击率 +15%（0.15）
    public const string 闪避 = 敌人特性效果名.闪避;         // 点数
    public const string 攻击距离 = 敌人特性效果名.攻击距离; // 点数（格）
    public const string 攻击间隔 = 敌人特性效果名.攻击间隔; // 百分比：-20 = 出手更快（间隔 ×0.8）
    public const string 移动间隔 = 敌人特性效果名.移动间隔; // 百分比

    public static string[] 全部效果 => 敌人特性效果名.全部;

    public static bool 认得出(string 效果) => 敌人特性效果名.认得出(效果);

    // 抽特性：逐条按权重轮盘抽，**不重复**（同一条不会叠两次）
    //   概率 = 精英率（普怪也会中，只是概率低）；条数 = 1 条起，命中"双特性率"再补 1 条（最多 `最多条数`）
    //   为什么用传入的"逐条概率"而不是"整体概率"：调用方（BattleService）只想要一个旋钮，
    //   而"每只敌人独立掷"也正好让 50 个种子的验证器能看到分布。
    public static List<敌人特性定义> 抽取(Dictionary<string, 敌人特性定义> 表, 随机源 随机,
        float 精英率, float 双特性率, int 最多条数 = 2)
    {
        var 出 = new List<敌人特性定义>();
        if (表 == null || 表.Count == 0 || 随机 == null) return 出;
        if (随机.值() >= 精英率) return 出;                       // 没中：普通敌人
        var 池 = new List<敌人特性定义>();
        foreach (var d in 表.Values) if (d != null && !string.IsNullOrEmpty(d.标识) && d.权重 > 0f) 池.Add(d);
        if (池.Count == 0) return 出;
        int 条数 = 随机.值() < 双特性率 ? Math.Max(1, 最多条数) : 1;
        for (int i = 0; i < 条数 && 池.Count > 0; i++)
        {
            var 选中 = 轮盘(池, 随机);
            if (选中 == null) break;
            出.Add(选中);
            池.Remove(选中);   // 不重复
        }
        return 出;
    }

    private static 敌人特性定义 轮盘(List<敌人特性定义> 池, 随机源 随机)
    {
        float 总 = 0f;
        foreach (var d in 池) 总 += d.权重;
        if (总 <= 0f) return 池[0];
        float 掷 = 随机.值() * 总;
        foreach (var d in 池)
        {
            掷 -= d.权重;
            if (掷 < 0f) return d;
        }
        return 池[池.Count - 1];
    }

    // 把特性应用到单位（生成时**一次性**改数；调完就把 最大生命/生命 一起抬到新上限）
    public static void 应用(战斗单位 单位, IList<敌人特性定义> 特性们)
    {
        if (单位 == null || 特性们 == null) return;
        int 原最大生命 = 单位.最大生命;
        foreach (var 特 in 特性们)
        {
            if (特?.效果们 == null) continue;
            foreach (var 效 in 特.效果们)
            {
                if (效 == null) continue;
                switch (效.效果)
                {
                    case 生命: 单位.最大生命 = 百分比(单位.最大生命, 效.数值); break;
                    case 攻击: 单位.基础近战 = 百分比(单位.基础近战, 效.数值); 单位.基础远程 = 百分比(单位.基础远程, 效.数值); break;
                    case 防御: 单位.基础物防 += 效.数值; break;
                    case 速度: 单位.基础速度 = 百分比(单位.基础速度, 效.数值); break;
                    case 暴击: 单位.暴击率 += 效.数值 / 100f; break;
                    case 闪避: 单位.闪避率 += 效.数值 / 100f; break;
                    case 攻击距离: 单位.攻击距离 = Math.Max(1, 单位.攻击距离 + 效.数值); break;
                    case 攻击间隔: 单位.攻击间隔秒 = 百分比(单位.攻击间隔秒, 效.数值); break;
                    case 移动间隔: 单位.移动间隔秒 = 百分比(单位.移动间隔秒, 效.数值); break;
                }
            }
        }
        // 生命上限变了 → 按**原比例**给当前生命（新生成的单位本来满血，这里等于直接抬满）
        if (单位.最大生命 != 原最大生命 && 原最大生命 > 0 && 单位.生命 >= 原最大生命) 单位.生命 = 单位.最大生命;
        if (单位.暴击率 > 0.8f) 单位.暴击率 = 0.8f;   // 与 战斗单位.当前暴击率 的封顶一致
        if (单位.闪避率 > 0.6f) 单位.闪避率 = 0.6f;
    }

    private static int 百分比(int 值, int 百分) => Math.Max(1, (int)Math.Round(值 * (1f + 百分 / 100f)));
    private static float 百分比(float 值, int 百分) => Math.Max(0.1f, 值 * (1f + 百分 / 100f));

    // 名字带前缀（"迅捷的感染者"）：精英要能一眼看出来。多条时用"、"连起来
    public static string 命名(string 原名, IList<敌人特性定义> 特性们)
    {
        if (特性们 == null || 特性们.Count == 0) return 原名;
        var 词 = new List<string>();
        foreach (var 特 in 特性们) if (特 != null && !string.IsNullOrEmpty(特.名称)) 词.Add(特.名称);
        return 词.Count == 0 ? 原名 : $"{string.Join("、", 词)}的{原名}";
    }

    // 特性标签文本（战斗提示/图鉴用）："迅捷（速度 +30%）" 这种，取第一条效果
    public static string 文本(敌人特性定义 特)
    {
        if (特 == null) return "";
        if (特.效果们 == null || 特.效果们.Length == 0 || 特.效果们[0] == null) return 特.名称 ?? "";
        var 效 = 特.效果们[0];
        string 号 = 效.数值 >= 0 ? "+" : "";
        string 尾 = (效.效果 == 防御 || 效.效果 == 暴击 || 效.效果 == 闪避 || 效.效果 == 攻击距离) ? "" : "%";
        return $"{特.名称}（{效.效果} {号}{效.数值}{尾}）";
    }
}
