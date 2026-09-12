using System;
using System.Collections.Generic;

// ============================================================
// 迷雾位图：把"这一张图我走过哪些格"在 `HashSet<格码>` 与 Base64 字符串之间来回搬。
//
// 为什么需要它：永久迷雾（大世界 / 区域随档保留，见 docs/大世界迷雾与永久记忆设计.md）要进 玩家档案，
//   而 `玩家档案` 走的是 `JsonUtility` —— **它不能序列化 Dictionary，也不好塞大块数字**。
//   位图 + Base64 是最省事、最省体积的写法：
//     · 大世界 100×100 = 10000 格 = 1250 字节 → Base64 1668 字符
//     · 区域 32×22 = 704 格 = 88 字节 → Base64 120 字符（7 片 ≈ 840 字符）
//   合计约 2.5 KB 的存档文本，PlayerPrefs 完全吃得下。
//
// 纯 C#（零 UnityEngine）→ **编解码能离线验**：`Tools/大世界验证` 会断言"打包再解包逐格一致"。
//   位图这类东西最容易在"最后一格 / 最后一个字节"上差一位，而那种错在游戏里只表现为
//   "某几格记忆莫名丢了"，几乎不可能靠试玩发现 —— 所以必须有对称性断言。
//
// 索引口径：**行优先**（idx = 行 * 列数 + 列），与 `网格数据.编码` 的 `列*1000+行` **不是**一回事：
//   编码是"全局唯一格码"（含负坐标/超界的冗余空间），位图要的是**紧凑下标**。两者别混用。
// ============================================================
public static class 迷雾位图
{
    // 打包：格码集合 → Base64（列数 × 行数 的位图）
    public static string 打包(IEnumerable<int> 已探索格码, int 列数, int 行数)
    {
        if (列数 <= 0 || 行数 <= 0) return "";
        int 总格 = 列数 * 行数;
        var 字节 = new byte[(总格 + 7) / 8];
        if (已探索格码 != null)
            foreach (int 码 in 已探索格码)
            {
                if (码 < 0) continue;
                int 列 = 码 / 1000, 行 = 码 % 1000;
                if (列 < 0 || 列 >= 列数 || 行 < 0 || 行 >= 行数) continue;   // 界外的丢掉（不该有，但不崩）
                int 位 = 行 * 列数 + 列;
                字节[位 >> 3] |= (byte)(1 << (位 & 7));
            }
        return Convert.ToBase64String(字节);
    }

    // 解包：Base64 → 格码集合（损坏/空串 → 空集，**不抛异常**：存档坏了不该让游戏进不去）
    public static HashSet<int> 解包(string 文本, int 列数, int 行数)
    {
        var 结果 = new HashSet<int>();
        if (string.IsNullOrEmpty(文本) || 列数 <= 0 || 行数 <= 0) return 结果;
        byte[] 字节;
        try { 字节 = Convert.FromBase64String(文本); }
        catch (FormatException) { return 结果; }
        int 总格 = 列数 * 行数;
        for (int 位 = 0; 位 < 总格; 位++)
        {
            int i = 位 >> 3;
            if (i >= 字节.Length) break;
            if ((字节[i] & (1 << (位 & 7))) == 0) continue;
            int 行 = 位 / 列数, 列 = 位 % 列数;
            结果.Add(网格数据.编码(列, 行));
        }
        return 结果;
    }
}
