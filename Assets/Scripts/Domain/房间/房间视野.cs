using System;
using System.Collections.Generic;

// ============================================================
// 房间视野：以玩家为中心、**边长是单数（3/5/7/9…）的正方形**可见区（纯 C#，零 UnityEngine 依赖）。
// 为什么是奇数边长：玩家要正好落在正方形正中——边长 7 = 左右各 3 格 + 自己那一格。
// 遮挡：**方形视野不做视线遮挡**（墙不挡视线）——单房间是空场，遮挡几乎不影响观感；
//       将来要做"墙后不可见"，把 视线通 接回 可见集 即可（函数保留，Bresenham 逐格判定）。
// 三态常量保留（未探索 / 已探索 / 可见）：当前表现层只区分"可见 / 不是"，已探索仍作调试与寻路记录。
// ============================================================
public static class 房间视野
{
    public const byte 未探索 = 0;
    public const byte 已探索 = 1;
    public const byte 可见 = 2;

    public const int 最小边长 = 3;
    public const int 最大边长 = 41;

    // 以 (列,行) 为中心、边长 为单数 的正方形可见集（元素 = 房间数据.编码），越界部分裁掉。
    public static HashSet<int> 可见集(房间数据 房, int 列, int 行, int 边长)
    {
        var 集 = new HashSet<int>();
        if (房 == null || !房.界内(列, 行)) return 集;
        int 半 = 奇数化(边长) / 2;
        for (int 行2 = 行 - 半; 行2 <= 行 + 半; 行2++)
            for (int 列2 = 列 - 半; 列2 <= 列 + 半; 列2++)
                if (房.界内(列2, 行2)) 集.Add(房间数据.编码(列2, 行2));
        return 集;
    }

    // 边长单数化（3/5/7/9…）：偶数 +1，越界夹到 [3, 41]
    public static int 奇数化(int 值)
    {
        int v = 值 < 最小边长 ? 最小边长 : (值 > 最大边长 ? 最大边长 : 值);
        return v % 2 == 0 ? v + 1 : v;
    }

    // 是否在以 (列,行) 为中心、边长 的方形内（切比雪夫距离 ≤ 半边长）
    public static bool 方形内(int 列, int 行, int 目标列, int 目标行, int 边长)
    {
        int 半 = 奇数化(边长) / 2;
        return Math.Abs(目标列 - 列) <= 半 && Math.Abs(目标行 - 行) <= 半;
    }

    // 视线：Bresenham 走一遍，中间格（不含起终点）有阻挡视线的实体 → 不通。
    // 当前方形视野未启用它（见文件头说明）；保留给"墙后不可见"和多房间户型使用。
    public static bool 视线通(房间数据 房, int 起列, int 起行, int 止列, int 止行)
    {
        if (房 == null) return false;
        if (起列 == 止列 && 起行 == 止行) return true;
        int 列 = 起列, 行 = 起行;
        int dx = Math.Abs(止列 - 起列), dy = Math.Abs(止行 - 起行);
        int 步x = 起列 < 止列 ? 1 : -1, 步y = 起行 < 止行 ? 1 : -1;
        int 误差 = dx - dy;
        int 防死循环 = dx + dy + 2;
        while (防死循环-- > 0)
        {
            if (列 == 止列 && 行 == 止行) return true;
            int 误差2 = 误差 * 2;
            if (误差2 > -dy)
            {
                误差 -= dy;
                列 += 步x;
            }
            if (误差2 < dx)
            {
                误差 += dx;
                行 += 步y;
            }
            if (列 == 止列 && 行 == 止行) return true;
            if (房.阻挡视线(列, 行)) return false;   // 中间格被墙/高柜挡住
        }
        return false;
    }
}
