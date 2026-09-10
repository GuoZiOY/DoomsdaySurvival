using System;
using System.Collections.Generic;

// 房间切块器：把一块矩形「递归二分」成 N 个房间块，块之间留 1 格缝（= 可开门的墙）——纯 C#，确定性。
// 算法血统：由 安全屋户型.切块（已在项目里跑通、带利用率/无空洞校验的那套）提炼为通用版本——
//   楼层层 / 安全屋 共用同一内核，不新写第二套 BSP。
//
// 规则：
//  · 块 = 房间矩形（叶子尺寸即房间尺寸），整块矩形铺满；未覆盖格由调用方当「墙」处理；
//  · 按各房间「目标面积」贪心分两组递归（比例 + 抖动），横切/竖切先按长宽比强制，避免细长条；
//  · 每层仅尝试有限次：失败返回 null，调用方重试/兜底（生成-校验-重试-兜底 是本项目既定范式）；
//  · 需贴边（如门厅/阳台/楼梯间要靠外墙）：叶子必须至少有一条边贴着本矩形的外边界。
public readonly struct 切块请求
{
    public readonly string 标识;      // 房间标识（"玄关"/"客厅"/…；调用方自定语义）
    public readonly int 最小宽, 最小高;   // 能装下内容的最小尺寸（可旋转判定）
    public readonly int 目标宽, 目标高;   // 期望尺寸（决定切块比例）
    public readonly bool 需贴边;          // 是否必须贴外边界

    public 切块请求(string 标识, int 最小宽, int 最小高, int 目标宽, int 目标高, bool 需贴边 = false)
    {
        this.标识 = 标识;
        this.最小宽 = Math.Max(1, 最小宽);
        this.最小高 = Math.Max(1, 最小高);
        this.目标宽 = Math.Max(1, 目标宽);
        this.目标高 = Math.Max(1, 目标高);
        this.需贴边 = 需贴边;
    }
}

public readonly struct 切块结果
{
    public readonly string 标识;
    public readonly int 列, 行, 宽, 高;
    public 切块结果(string 标识, int 列, int 行, int 宽, int 高)
    { this.标识 = 标识; this.列 = 列; this.行 = 行; this.宽 = 宽; this.高 = 高; }

    public bool 含格(int 列, int 行)
        => 列 >= this.列 && 列 < this.列 + 宽 && 行 >= this.行 && 行 < this.行 + 高;

    public bool 中心(out int 列, out int 行)
    { 列 = this.列 + 宽 / 2; 行 = this.行 + 高 / 2; return true; }
}

public static class 房间切块器
{
    // ===== 主入口 =====

    // 切块：失败返回 null（调用方重试；多次失败走 兜底）
    public static List<切块结果> 切块(Random 随机, int 宽, int 高, IList<切块请求> 请求)
    {
        if (随机 == null || 请求 == null || 请求.Count == 0 || 宽 < 1 || 高 < 1) return null;
        var 剩余 = new List<切块请求>(请求);
        var 块 = 递归(随机, 0, 0, 宽, 高, 剩余, true, true, true, true);
        if (块 == null || 块.Count != 请求.Count) return null;
        return 块;
    }

    // 兜底：网格均分（优先 1×N 条带，其次 N×M 网格）——保证出房间，绝不返回空。
    // 装不下的请求被丢弃（调用方应在兜底前先按容量裁剪房间数）。
    public static List<切块结果> 兜底(int 宽, int 高, IList<切块请求> 请求)
    {
        var 结果 = new List<切块结果>();
        if (请求 == null || 请求.Count == 0 || 宽 < 1 || 高 < 1) return 结果;
        int 数 = 请求.Count;
        // 试 1..4 行；每行 列数 = ceil(数/行数)，选第一个「每格都装得下」的排法
        for (int 行数 = 1; 行数 <= Math.Min(4, 数); 行数++)
        {
            int 列数 = (数 + 行数 - 1) / 行数;
            if (宽 < 列数 * 2 - 1 || 高 < 行数 * 2 - 1) continue;
            int 格宽 = (宽 - (列数 - 1)) / 列数;
            int 格高 = (高 - (行数 - 1)) / 行数;
            if (格宽 < 1 || 格高 < 1) continue;
            // 全部房间都要装得下（可旋转）
            bool 全部可 = true;
            for (int i = 0; i < 数 && 全部可; i++)
            {
                var r = 请求[i];
                if (!((格宽 >= r.最小宽 && 格高 >= r.最小高) || (格宽 >= r.最小高 && 格高 >= r.最小宽))) 全部可 = false;
            }
            if (!全部可) continue;
            for (int i = 0; i < 数; i++)
            {
                int 列号 = i % 列数, 行号 = i / 列数;
                int 起列 = 列号 * (格宽 + 1), 起行 = 行号 * (格高 + 1);
                // 最后一列/行吃掉余量，避免右侧留空
                int 本宽 = 列号 == 列数 - 1 ? 宽 - 起列 : 格宽;
                int 本高 = 行号 == 行数 - 1 ? 高 - 起行 : 格高;
                if (本宽 < 1 || 本高 < 1) { 结果.Clear(); break; }
                结果.Add(new 切块结果(请求[i].标识, 起列, 起行, 本宽, 本高));
            }
            if (结果.Count == 数) return 结果;
            结果.Clear();
        }
        // 极端兜底：整块给第一个请求
        结果.Add(new 切块结果(请求[0].标识, 0, 0, 宽, 高));
        return 结果;
    }

    // ===== 相邻与门 =====

    // 公共边界格：两块之间隔 1 格缝时，返回缝上的墙格（以及两侧对应的房间格），供开门用。空 = 不相邻。
    public static List<(int 墙列, int 墙行, int 甲列, int 甲行, int 乙列, int 乙行)> 公共边界(切块结果 甲, 切块结果 乙)
    {
        var 结果 = new List<(int, int, int, int, int, int)>();
        // 甲在左、乙在右（缝 1 列）
        if (甲.列 + 甲.宽 + 1 == 乙.列)
        {
            int 起 = Math.Max(甲.行, 乙.行), 止 = Math.Min(甲.行 + 甲.高, 乙.行 + 乙.高) - 1;
            for (int 行 = 起; 行 <= 止; 行++)
                结果.Add((甲.列 + 甲.宽, 行, 甲.列 + 甲.宽 - 1, 行, 乙.列, 行));
        }
        else if (乙.列 + 乙.宽 + 1 == 甲.列)
        {
            int 起 = Math.Max(甲.行, 乙.行), 止 = Math.Min(甲.行 + 甲.高, 乙.行 + 乙.高) - 1;
            for (int 行 = 起; 行 <= 止; 行++)
                结果.Add((乙.列 + 乙.宽, 行, 甲.列, 行, 乙.列 + 乙.宽 - 1, 行));
        }
        // 甲在上、乙在下（缝 1 行）
        else if (甲.行 + 甲.高 + 1 == 乙.行)
        {
            int 起 = Math.Max(甲.列, 乙.列), 止 = Math.Min(甲.列 + 甲.宽, 乙.列 + 乙.宽) - 1;
            for (int 列 = 起; 列 <= 止; 列++)
                结果.Add((列, 甲.行 + 甲.高, 列, 甲.行 + 甲.高 - 1, 列, 乙.行));
        }
        else if (乙.行 + 乙.高 + 1 == 甲.行)
        {
            int 起 = Math.Max(甲.列, 乙.列), 止 = Math.Min(甲.列 + 甲.宽, 乙.列 + 乙.宽) - 1;
            for (int 列 = 起; 列 <= 止; 列++)
                结果.Add((列, 乙.行 + 乙.高, 列, 甲.行, 列, 乙.行 + 乙.高 - 1));
        }
        return 结果;
    }

    // ===== 内部：递归二分 =====

    private static List<切块结果> 递归(Random 随机, int 列, int 行, int 宽, int 高, List<切块请求> 请求,
        bool 贴上, bool 贴下, bool 贴左, bool 贴右)
    {
        if (请求.Count == 1)
        {
            var 单 = 请求[0];
            // 尺寸校验：可旋转（宽高互换）也必须装得下
            bool 尺寸可 = (宽 >= 单.最小宽 && 高 >= 单.最小高) || (宽 >= 单.最小高 && 高 >= 单.最小宽);
            if (!尺寸可) return null;
            if (单.需贴边 && !(贴上 || 贴下 || 贴左 || 贴右)) return null;
            return new List<切块结果> { new 切块结果(单.标识, 列, 行, 宽, 高) };
        }

        // 分组：按目标面积贪心均衡（每次给当前面积和小的那组）
        var 组A = new List<切块请求>(); var 组B = new List<切块请求>();
        long 和A = 0, 和B = 0;
        foreach (var r in 请求)
        {
            long 面积 = (long)r.目标宽 * r.目标高;
            if (和A <= 和B) { 组A.Add(r); 和A += 面积; }
            else { 组B.Add(r); 和B += 面积; }
        }

        // 两种归属（A 上/左 或 A 下/右）+ 多次尝试（比例抖动）
        var 变体 = new[] { (组上: 组A, 组下: 组B, 上面积: 和A, 下面积: 和B),
                           (组上: 组B, 组下: 组A, 上面积: 和B, 下面积: 和A) };
        foreach (var 变 in 变体)
        {
            for (int 尝试 = 0; 尝试 < 16; 尝试++)
            {
                // 强制先切长边：块不细长、利用率高
                bool 横切;
                if (宽 > 高 * 2) 横切 = false;
                else if (高 > 宽 * 2) 横切 = true;
                else 横切 = 随机.Next(2) == 0;

                if (横切)
                {
                    int 需上 = 需求高(变.组上, 宽), 需下 = 需求高(变.组下, 宽);
                    int 可用 = 高 - 1;   // 减 1 格 缝
                    if (需上 == int.MaxValue || 需下 == int.MaxValue || 需上 + 需下 > 可用) continue;
                    long 总 = Math.Max(1, 变.上面积 + 变.下面积);
                    int 比例 = (int)((long)可用 * 变.上面积 / 总);
                    int 上高 = Clamp(比例 + 随机.Next(3) - 1, 需上, 可用 - 需下);
                    var 子上 = 递归(随机, 列, 行, 宽, 上高, 变.组上, 贴上, false, 贴左, 贴右);
                    if (子上 == null) continue;
                    var 子下 = 递归(随机, 列, 行 + 上高 + 1, 宽, 高 - 上高 - 1, 变.组下, false, 贴下, 贴左, 贴右);
                    if (子下 == null) continue;
                    子上.AddRange(子下);
                    return 子上;
                }
                else
                {
                    int 需左 = 需求宽(变.组上, 高), 需右 = 需求宽(变.组下, 高);
                    int 可用 = 宽 - 1;
                    if (需左 == int.MaxValue || 需右 == int.MaxValue || 需左 + 需右 > 可用) continue;
                    long 总 = Math.Max(1, 变.上面积 + 变.下面积);
                    int 比例 = (int)((long)可用 * 变.上面积 / 总);
                    int 左宽 = Clamp(比例 + 随机.Next(3) - 1, 需左, 可用 - 需右);
                    var 子左 = 递归(随机, 列, 行, 左宽, 高, 变.组上, 贴上, 贴下, 贴左, false);
                    if (子左 == null) continue;
                    var 子右 = 递归(随机, 列 + 左宽 + 1, 行, 宽 - 左宽 - 1, 高, 变.组下, 贴上, 贴下, false, 贴右);
                    if (子右 == null) continue;
                    子左.AddRange(子右);
                    return 子左;
                }
            }
        }
        return null;
    }

    private static int Clamp(int 值, int 最小, int 最大)
    {
        if (最大 < 最小) 最大 = 最小;
        return 值 < 最小 ? 最小 : (值 > 最大 ? 最大 : 值);
    }

    // 组在固定宽度内至少需要多高（每个房间按最小尺寸、可旋转）
    private static int 需求高(List<切块请求> 组, int 可用宽)
    {
        int 高 = 1;
        foreach (var r in 组)
        {
            if (可用宽 >= r.最小宽) 高 = Math.Max(高, r.最小高);
            else if (可用宽 >= r.最小高) 高 = Math.Max(高, r.最小宽);
            else return int.MaxValue;
        }
        return 高;
    }

    // 组在固定高度内至少需要多宽
    private static int 需求宽(List<切块请求> 组, int 可用高)
    {
        int 宽 = 1;
        foreach (var r in 组)
        {
            if (可用高 >= r.最小高) 宽 = Math.Max(宽, r.最小宽);
            else if (可用高 >= r.最小宽) 宽 = Math.Max(宽, r.最小高);
            else return int.MaxValue;
        }
        return 宽;
    }
}
