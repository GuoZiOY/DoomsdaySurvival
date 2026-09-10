using System;
using System.Collections.Generic;

// ============================================================
// 房间种子：确定性随机流（房间层专用，不与已撤出的世界沙盒耦合）。
// 用途：给「房间生成」提供可复现的随机数——同模板 + 同种子 = 同布局（自检与问题复现都靠它）。
// ============================================================
public static class 房间种子
{
    public const int 生成器版本 = 1;   // 生成算法一改，这个数就要 +1（旧结构指纹不再可比）

    // FNV-1a 分段哈希：把若干标识混成一个种子（跨平台一致，不依赖 string.GetHashCode）
    public static int 混合(params object[] 部分)
    {
        unchecked
        {
            uint h = 2166136261;
            if (部分 != null)
            {
                foreach (var p in 部分)
                {
                    string s = p?.ToString() ?? "";
                    foreach (char c in s)
                    {
                        h ^= c;
                        h *= 16777619;
                    }
                    h ^= 0x1F;   // 段分隔，避免 ("ab","c") 与 ("a","bc") 撞
                    h *= 16777619;
                }
            }
            return (int)h;
        }
    }

    // 派生：主种子 + 标签（+ 版本）→ 子种子
    public static int 派生(int 种子, string 标签) => 混合(种子, 标签, 生成器版本);

    // 确定性随机流
    public static Random 随机流(int 种子) => new Random(种子);

    // 区间随机（含两端）
    public static int 范围(Random 流, int 最小, int 最大)
    {
        if (流 == null) return 最小;
        if (最大 <= 最小) return 最小;
        return 流.Next(最小, 最大 + 1);
    }

    // 轮盘赌：按权重抽一个（权重全 <= 0 时退化为等概率）
    public static T 轮盘<T>(Random 流, IList<T> 池, Func<T, int> 取权重)
    {
        if (池 == null || 池.Count == 0) return default;
        int 总 = 0;
        for (int i = 0; i < 池.Count; i++)
        {
            int w = 取权重 != null ? 取权重(池[i]) : 1;
            if (w > 0) 总 += w;
        }
        if (总 <= 0) return 池[流.Next(0, 池.Count)];
        int r = 流.Next(0, 总);
        for (int i = 0; i < 池.Count; i++)
        {
            int w = 取权重 != null ? 取权重(池[i]) : 1;
            if (w <= 0) continue;
            r -= w;
            if (r < 0) return 池[i];
        }
        return 池[池.Count - 1];
    }

    // 原地洗牌（Fisher-Yates）
    public static void 洗牌<T>(Random 流, IList<T> 列表)
    {
        if (流 == null || 列表 == null) return;
        for (int i = 列表.Count - 1; i > 0; i--)
        {
            int j = 流.Next(0, i + 1);
            (列表[i], 列表[j]) = (列表[j], 列表[i]);
        }
    }
}
