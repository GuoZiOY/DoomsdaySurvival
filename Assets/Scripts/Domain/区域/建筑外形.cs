using System;
using System.Collections.Generic;

// ============================================================
// 建筑外形：**配方 → 占地掩码**（纯 C#，零 UnityEngine；离线验证器与游戏跑的是同一份）。
//   · 掩码：bool[高 * 宽]，索引 = 行 * 宽 + 列；true = 楼体，false = 凹口（包围盒里的空地）
//   · 配方库（按拍板收敛成四个）：**矩形 / L / 凸 / 凹**；朝向 + 左右镜像是一层**通用变换** ——
//     以后想加 T / + / H / 条 / S，只加一个 case，"换朝向 / 镜像"白送，不用各写一套参数
//     （曾做过 `[` 三面围合：壁厚 1 的 U 形在区域图上更像一圈围墙而不是一栋楼，读起来和 凹 是一回事 → 已删）
//   · 朝向 90/270 会把宽高对调（L 转 90° 就是另一个缺角方向）
// 不变量（区域验证 逐颗种子断言）：
//   ① 掩码至少一格；② 掩码**连通**（一栋楼不能是两块飞地）；③ 门口格必须是掩码里的一格
// ============================================================
public static class 建筑外形
{
    public const string 矩形 = "矩形";
    public const string L型 = "L";
    public const string 凸型 = "凸";      // 下面满 + 上面中间突出
    public const string 凹型 = "凹";      // 上面中间挖掉一块（朝向 180 就是"天井朝街"）

    // 数据里能写的配方名（校验用：写错了要报出来，别静默当矩形 —— 见 建筑验证 的 数据标识核对）
    public static readonly string[] 全部形状 = { 矩形, L型, 凸型, 凹型 };

    public static bool 认得出(string 形状)
    {
        if (string.IsNullOrEmpty(形状)) return false;
        foreach (var 名 in 全部形状) if (名 == 形状) return true;
        return false;
    }

    public static string 形状清单() => string.Join(" / ", 全部形状);

    // 生成掩码。宽高可能因朝向 90/270 对调 → 通过 out 返回真正的宽高
    public static bool[] 生成(建筑外形数据 形, out int 宽, out int 高)
    {
        int w = Math.Max(1, 形?.宽 ?? 4);
        int h = Math.Max(1, 形?.高 ?? 3);
        string 形状 = string.IsNullOrEmpty(形?.形状) ? 矩形 : 形.形状;
        var 掩 = 标准形状(形状, w, h, 形);

        int 朝 = 规范化朝向(形?.朝向 ?? 0);
        for (int i = 0; i < 朝 / 90; i++) 掩 = 顺时针(掩, w, h, out w, out h);
        if (形?.左右镜像 ?? false) 掩 = 左右翻(掩, w, h);

        宽 = w;
        高 = h;
        return 掩;
    }

    // 掩码 → 文本画（日志/ASCII 验证用；'#' = 楼体，'.' = 凹口）
    public static List<string> 画(bool[] 掩, int 宽, int 高)
    {
        var 行 = new List<string>();
        for (int r = 0; r < 高; r++)
        {
            var sb = new System.Text.StringBuilder();
            for (int c = 0; c < 宽; c++) sb.Append(掩 != null && r * 宽 + c < 掩.Length && 掩[r * 宽 + c] ? '#' : '.');
            行.Add(sb.ToString());
        }
        return 行;
    }

    // 掩码是否连通（四方向；一栋楼必须是一整块）
    public static bool 连通(bool[] 掩, int 宽, int 高)
    {
        if (掩 == null || 宽 <= 0 || 高 <= 0) return false;
        int 总数 = 宽 * 高, 起点 = -1;
        for (int i = 0; i < 总数 && i < 掩.Length; i++) if (掩[i]) { 起点 = i; break; }
        if (起点 < 0) return false;
        var 已看 = new bool[总数];
        var 队 = new Queue<int>();
        队.Enqueue(起点); 已看[起点] = true;
        int 到过 = 1, 应到 = 0;
        for (int i = 0; i < 总数 && i < 掩.Length; i++) if (掩[i]) 应到++;
        int[] 横 = { 1, -1, 0, 0 }, 纵 = { 0, 0, 1, -1 };
        while (队.Count > 0)
        {
            int 当 = 队.Dequeue();
            int r = 当 / 宽, c = 当 % 宽;
            for (int d = 0; d < 4; d++)
            {
                int nc = c + 横[d], nr = r + 纵[d];
                if (nc < 0 || nr < 0 || nc >= 宽 || nr >= 高) continue;
                int 邻 = nr * 宽 + nc;
                if (已看[邻] || !掩[邻]) continue;
                已看[邻] = true; 到过++; 队.Enqueue(邻);
            }
        }
        return 到过 == 应到;
    }

    // 标准朝向（其余朝向都靠 顺时针 旋转）
    //   矩形 = 全满；L = 缺右下角；凸 = 下面满+上面中间突出；凹 = 上面中间挖掉
    private static bool[] 标准形状(string 形状, int 宽, int 高, 建筑外形数据 形)
    {
        var 掩 = new bool[宽 * 高];
        if (形状 == L型)
        {
            int 切宽 = 夹(形?.切角宽 ?? 2, 1, 宽 - 1);
            int 切高 = 夹(形?.切角高 ?? 2, 1, 高 - 1);
            for (int r = 0; r < 高; r++)
                for (int c = 0; c < 宽; c++)
                    掩[r * 宽 + c] = !(c >= 宽 - 切宽 && r >= 高 - 切高);
            return 掩;
        }
        if (形状 == 凸型)
        {
            int 突宽 = 夹(形 != null && 形.突出宽 > 0 ? 形.突出宽 : Math.Max(2, 宽 - 2), 1, 宽);
            int 突高 = 夹(形 != null && 形.突出高 > 0 ? 形.突出高 : Math.Max(1, 高 / 2), 1, 高 - 1);
            int 左 = (宽 - 突宽) / 2;
            for (int r = 0; r < 高; r++)
                for (int c = 0; c < 宽; c++)
                    掩[r * 宽 + c] = r >= 突高 || (c >= 左 && c < 左 + 突宽);   // 下半满 + 上半中间突出
            return 掩;
        }
        if (形状 == 凹型)
        {
            int 口宽 = 夹(形 != null && 形.凹口宽 > 0 ? 形.凹口宽 : Math.Max(2, 宽 - 2), 1, 宽);
            int 口深 = 夹(形 != null && 形.凹口深 > 0 ? 形.凹口深 : Math.Max(1, 高 / 2), 1, 高 - 1);
            int 左 = (宽 - 口宽) / 2;
            for (int r = 0; r < 高; r++)
                for (int c = 0; c < 宽; c++)
                    掩[r * 宽 + c] = !(r < 口深 && c >= 左 && c < 左 + 口宽);   // 上面中间挖掉一块
            return 掩;
        }
        for (int i = 0; i < 掩.Length; i++) 掩[i] = true;   // 矩形（未知形状名也按矩形兜底）
        return 掩;
    }

    // 掩码 → **底边中点格**（相对坐标；没有楼体时返回 (-1,-1)）：
    //   在掩码里最靠下的那一行里找**最长连续楼体段**，取它的中间那一格。
    //   "门开在底边"是区域层与大世界层**共用**的取法（门口格必须是掩码里的一格、且能站能走）——
    //   区域生成器 的楼门、大世界生成器 的区域门都走这里，免得同一条规则写两遍（写两遍就会漂移）。
    //   掩码为 null 按"纯矩形全满"处理（与 网格实体.覆盖 的约定一致）。
    public static (int 列, int 行) 底边中点(bool[] 掩, int 宽, int 高)
    {
        if (宽 <= 0 || 高 <= 0) return (-1, -1);
        bool 是楼体(int r, int c) => 掩 == null || (r * 宽 + c < 掩.Length && 掩[r * 宽 + c]);

        int 底行 = -1;
        for (int r = 高 - 1; r >= 0 && 底行 < 0; r--)
            for (int c = 0; c < 宽; c++)
                if (是楼体(r, c)) { 底行 = r; break; }
        if (底行 < 0) return (-1, -1);

        int 最佳起 = -1, 最佳长 = 0, 起 = -1;
        for (int c = 0; c <= 宽; c++)
        {
            if (c < 宽 && 是楼体(底行, c)) { if (起 < 0) 起 = c; continue; }
            if (起 < 0) continue;
            int 长 = c - 起;
            if (长 > 最佳长) { 最佳长 = 长; 最佳起 = 起; }
            起 = -1;
        }
        if (最佳起 < 0) return (-1, -1);
        return (最佳起 + 最佳长 / 2, 底行);
    }

    private static int 夹(int 值, int 最小, int 最大)
        => 值 < 最小 ? 最小 : (值 > 最大 ? 最大 : 值);

    private static int 规范化朝向(int 朝向)
    {
        int 朝 = 朝向 % 360;
        if (朝 < 0) 朝 += 360;
        return 朝 - 朝 % 90;   // 只认 0/90/180/270
    }

    // 顺时针 90°：原 (r0,c0) → 新 (行 c0, 列 高-1-r0)
    private static bool[] 顺时针(bool[] 掩, int 宽, int 高, out int 新宽, out int 新高)
    {
        新宽 = 高; 新高 = 宽;
        var 新 = new bool[新宽 * 新高];
        for (int r = 0; r < 新高; r++)
            for (int c = 0; c < 新宽; c++)
                新[r * 新宽 + c] = 掩[(高 - 1 - c) * 宽 + r];
        return 新;
    }

    private static bool[] 左右翻(bool[] 掩, int 宽, int 高)
    {
        var 新 = new bool[宽 * 高];
        for (int r = 0; r < 高; r++)
            for (int c = 0; c < 宽; c++)
                新[r * 宽 + c] = 掩[r * 宽 + (宽 - 1 - c)];
        return 新;
    }
}
