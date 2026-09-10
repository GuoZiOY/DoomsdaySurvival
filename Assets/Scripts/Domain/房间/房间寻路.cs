using System;
using System.Collections.Generic;

// ============================================================
// 房间寻路：A*（纯 C#，零 UnityEngine 依赖）。
// 约定：四方向（不做斜走——斜穿会让"不可穿过"的判定与视觉都变糊）；
//       每格代价恒 1；启发 = 曼哈顿距离；**固定展开顺序（右/下/左/上）** → 同输入必得同路径（可断言）。
// 可通行 = 房间数据.可通行（界内 且 无占格实体）——渲染用的 网格服务 与它同源（同一份 房间数据）。
// ============================================================
public static class 房间寻路
{
    private static readonly int[] 横 = { 1, 0, -1, 0 };   // 右 下 左 上（固定顺序，别改——改了路径就变）
    private static readonly int[] 纵 = { 0, 1, 0, -1 };

    // 起 → 终 的最短路（含首尾）。不可达返回 null；起=终 返回单点路径。
    public static List<(int 列, int 行)> 寻路(房间数据 房, (int 列, int 行) 起, (int 列, int 行) 终)
    {
        if (房 == null) return null;
        if (!房.可通行(起.列, 起.行) || !房.可通行(终.列, 终.行)) return null;
        if (起.列 == 终.列 && 起.行 == 终.行) return new List<(int 列, int 行)> { 起 };

        int 总数 = 房.列 * 房.行;
        var 代价 = new int[总数];
        var 估 = new int[总数];
        var 父 = new int[总数];
        var 状态 = new byte[总数];   // 0 未入 / 1 开放 / 2 关闭
        for (int i = 0; i < 总数; i++)
        {
            代价[i] = int.MaxValue;
            父[i] = -1;
        }

        int 起索引 = 索引(房, 起.列, 起.行), 终索引 = 索引(房, 终.列, 终.行);
        代价[起索引] = 0;
        估[起索引] = 曼哈顿(起.列, 起.行, 终.列, 终.行);
        var 开放 = new List<int> { 起索引 };
        状态[起索引] = 1;

        while (开放.Count > 0)
        {
            // 取估价值最小；并列取最先入队的（确定性）
            int 最佳位 = 0;
            for (int i = 1; i < 开放.Count; i++)
                if (估[开放[i]] < 估[开放[最佳位]]) 最佳位 = i;
            int 当前 = 开放[最佳位];
            开放.RemoveAt(最佳位);
            if (当前 == 终索引) return 重建(房, 父, 终索引);
            状态[当前] = 2;
            int 当前列 = 当前 % 房.列, 当前行 = 当前 / 房.列;
            for (int d = 0; d < 4; d++)
            {
                int 列 = 当前列 + 横[d], 行 = 当前行 + 纵[d];
                if (!房.可通行(列, 行)) continue;
                int 邻 = 索引(房, 列, 行);
                if (状态[邻] == 2) continue;
                int 新代价 = 代价[当前] + 1;
                if (新代价 >= 代价[邻]) continue;
                代价[邻] = 新代价;
                估[邻] = 新代价 + 曼哈顿(列, 行, 终.列, 终.行);
                父[邻] = 当前;
                if (状态[邻] != 1)
                {
                    开放.Add(邻);
                    状态[邻] = 1;
                }
            }
        }
        return null;
    }

    // 走到目标的"相邻可站格"（≤4 个候选各跑一次 A*，取最短那条）——点柜子 = 走到柜子旁边
    public static List<(int 列, int 行)> 寻路到相邻(房间数据 房, (int 列, int 行) 起, 房间实体 目标)
    {
        if (房 == null || 目标 == null) return null;
        List<(int 列, int 行)> 最好 = null;
        for (int r = 目标.行; r < 目标.行 + 目标.高; r++)
            for (int c = 目标.列; c < 目标.列 + 目标.宽; c++)
            {
                for (int d = 0; d < 4; d++)
                {
                    int 列 = c + 横[d], 行 = r + 纵[d];
                    if (!房.可通行(列, 行)) continue;
                    var 路 = 寻路(房, 起, (列, 行));
                    if (路 == null) continue;
                    if (最好 == null || 路.Count < 最好.Count) 最好 = 路;
                }
            }
        return 最好;
    }

    // 迷雾用：走到"已知（已探索）区域里离目标最近的可站格"——点未探索的地方时往前探，不用一格一格点
    public static List<(int 列, int 行)> 寻路到已知边缘(房间数据 房, (int 列, int 行) 起, (int 列, int 行) 目标, HashSet<int> 已知)
    {
        if (房 == null || !房.可通行(起.列, 起.行) || 已知 == null) return null;
        int 总数 = 房.列 * 房.行;
        var 父 = new int[总数];
        var 访问 = new bool[总数];
        for (int i = 0; i < 总数; i++) 父[i] = -1;
        var 队 = new Queue<int>();
        int 起索引 = 索引(房, 起.列, 起.行);
        访问[起索引] = true;
        队.Enqueue(起索引);
        int 最佳 = -1, 最佳距 = int.MaxValue;
        while (队.Count > 0)
        {
            int 当前 = 队.Dequeue();
            int 列 = 当前 % 房.列, 行 = 当前 / 房.列;
            if (已知.Contains(房间数据.编码(列, 行)))
            {
                int 距 = Math.Abs(列 - 目标.列) + Math.Abs(行 - 目标.行);
                // 并列取更靠上、再靠左的（确定性）
                if (距 < 最佳距 || (距 == 最佳距 && 最佳 >= 0 && (行 < 最佳 / 房.列 || (行 == 最佳 / 房.列 && 列 < 最佳 % 房.列))))
                {
                    最佳距 = 距;
                    最佳 = 当前;
                }
            }
            for (int d = 0; d < 4; d++)
            {
                int 邻列 = 列 + 横[d], 邻行 = 行 + 纵[d];
                if (!房.可通行(邻列, 邻行)) continue;
                int 邻 = 索引(房, 邻列, 邻行);
                if (访问[邻]) continue;
                访问[邻] = true;
                父[邻] = 当前;
                队.Enqueue(邻);
            }
        }
        if (最佳 < 0) return null;
        return 重建(房, 父, 最佳);
    }

    private static List<(int 列, int 行)> 重建(房间数据 房, int[] 父, int 终索引)
    {
        var 路 = new List<(int 列, int 行)>();
        int 当前 = 终索引;
        int 防死循环 = 房.列 * 房.行 + 1;
        while (当前 >= 0 && 防死循环-- > 0)
        {
            路.Add((当前 % 房.列, 当前 / 房.列));
            当前 = 父[当前];
        }
        路.Reverse();
        return 路;
    }

    private static int 索引(房间数据 房, int 列, int 行) => 行 * 房.列 + 列;

    private static int 曼哈顿(int 列, int 行, int 目标列, int 目标行)
        => Math.Abs(列 - 目标列) + Math.Abs(行 - 目标行);
}
