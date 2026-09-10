using System;
using System.Globalization;
using System.Text;

// 沙盒种子：确定性生成的唯一"启动键"来源（纯 C#，禁 UnityEngine）。
// 用途：世界/地区/建筑/楼层 全部由「世界种子 + 层级标识」派生出的子种子展开，同种子永远同结果。
//
// 设计约束（评审定案，改前先想清楚）：
//  ① 分段哈希必须带分隔符——否则 ("区域","AB") 与 ("区域A","B") 会撞成同一个世界；
//  ② 一切参与哈希的数值先「整数化」，浮点一律 InvariantCulture——否则小数点/逗号受系统区域影响，跨机不一致；
//  ③ 生成链路严禁使用 string.GetHashCode() / 字典遍历顺序（.NET 每进程随机化字符串哈希，同一存档两次跑出不同世界）；
//  ④ 生成器版本进存档：System.Random 是「流式」PRNG——生成代码改一行（或只是少调一次 Next），
//     同一颗种子展开出的世界就会变。版本不符必须显式「重掷/迁移」，不允许静默重建。
public static class 沙盒种子
{
    // 生成器版本：任何会改变「种子 → 结构」映射的改动都必须 +1（并写进存档，供迁移判定）
    public const int 生成器版本 = 1;

    // ===== 分段哈希 =====

    // 混合（FNV-1a 64 位）：任意分段 → 64 位稳定哈希。分段先经 规范化 拼成带分隔符的文本。
    public static long 混合64(params object[] 部分)
    {
        const ulong 偏移基数 = 14695981039346656037UL;
        const ulong 质数 = 1099511628211UL;
        ulong 哈希 = 偏移基数;
        string 文本 = 拼(部分);
        for (int i = 0; i < 文本.Length; i++)
        {
            哈希 ^= 文本[i];
            哈希 *= 质数;
        }
        return unchecked((long)哈希);
    }

    // 混合（取低 32 位）：需要 int 种子处使用
    public static int 混合(params object[] 部分) => unchecked((int)混合64(部分));

    // 派生：世界种子 + 层级标识 → 子种子（区域/建筑/楼层 逐级下钻都走这里）
    public static long 派生(long 世界种子, params object[] 部分)
    {
        var 合并 = new object[(部分?.Length ?? 0) + 1];
        合并[0] = 世界种子;
        if (部分 != null) Array.Copy(部分, 0, 合并, 1, 部分.Length);
        return 混合64(合并);
    }

    // 稳定字符串（带分隔符）：规范化各分段后用 '|' 连接
    public static string 拼(params object[] 部分)
    {
        if (部分 == null || 部分.Length == 0) return "";
        var 缓冲 = new StringBuilder();
        for (int i = 0; i < 部分.Length; i++)
        {
            if (i > 0) 缓冲.Append('|');   // 分隔符：消除拼接歧义（本文件存在的第一理由）
            缓冲.Append(规范化(部分[i]));
        }
        return 缓冲.ToString();
    }

    // 分段 → 稳定文本：数值整数化 + 不变文化；字符串原样（调用方负责不要塞易变内容，如时间戳/哈希码）
    private static string 规范化(object 值)
    {
        switch (值)
        {
            case null: return "";
            case string 文本: return 文本;
            case bool 布尔: return 布尔 ? "1" : "0";
            case int 整数: return 整数.ToString(CultureInfo.InvariantCulture);
            case long 长整数: return 长整数.ToString(CultureInfo.InvariantCulture);
            case float 单精: return 取整(单精).ToString(CultureInfo.InvariantCulture);
            case double 双精: return 取整(双精).ToString(CultureInfo.InvariantCulture);
            case Enum 枚举: return Convert.ToInt32(枚举).ToString(CultureInfo.InvariantCulture);
            default: return 值.ToString();   // 兜底：自定义类型请自行保证稳定（不要用默认 ToString 含糊输出）
        }
    }

    // 浮点 → 整数（四舍五入到整）：坐标/尺寸一律按整格参与哈希
    private static long 取整(double 值) => (long)Math.Round(值, MidpointRounding.AwayFromZero);

    // ===== 确定性随机流 =====

    // 由（可为负的）64 位种子起一条确定性随机流。注意：流是「有状态顺序」的——生成算法改动会平移整条流。
    public static Random 随机流(long 种子)
    {
        // 先混合再截断，避免相邻种子的高低位差异被 System.Random 的取模吞掉
        long 搅动 = 混合64(种子, "流");
        return new Random(unchecked((int)(搅动 ^ (搅动 >> 32))));
    }

    // 便捷：从世界种子 + 层级分段 直接起一条随机流
    public static Random 随机流(long 世界种子, params object[] 部分) => 随机流(派生(世界种子, 部分));

    // ===== 小工具（生成器共用，避免各写一份） =====

    // 闭区间随机整数
    public static int 范围(Random 随机, int 最小, int 最大)
    {
        if (最大 <= 最小) return 最小;
        return 随机.Next(最小, 最大 + 1);
    }

    // 洗牌（Fisher-Yates）：列表顺序随机化（生成器里"随机顺序"一律走这里，保证确定性）
    public static void 洗牌<T>(Random 随机, System.Collections.Generic.IList<T> 列表)
    {
        if (列表 == null) return;
        for (int i = 列表.Count - 1; i > 0; i--)
        {
            int j = 随机.Next(i + 1);
            (列表[i], 列表[j]) = (列表[j], 列表[i]);
        }
    }

    // 权重轮盘：返回命中下标（权重 <=0 视为 1）；全空返回 -1
    public static int 轮盘(Random 随机, System.Collections.Generic.IList<int> 权重)
    {
        if (权重 == null || 权重.Count == 0) return -1;
        int 总 = 0;
        for (int i = 0; i < 权重.Count; i++) 总 += Math.Max(1, 权重[i]);
        if (总 <= 0) return -1;
        int 掷 = 随机.Next(总);
        for (int i = 0; i < 权重.Count; i++)
        {
            掷 -= Math.Max(1, 权重[i]);
            if (掷 < 0) return i;
        }
        return 权重.Count - 1;
    }
}
