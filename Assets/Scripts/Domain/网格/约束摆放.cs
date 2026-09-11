using System;
using System.Collections.Generic;

// ============================================================
// 约束摆放：与"摆什么"无关的**摆放引擎**（纯 C#，零 UnityEngine 依赖）。
//   ① 把世界划成区块 → ② 大件优先 + 区块配额 + 约束采样（锚点洗牌逐个试）→ ③ 放不下的到全图补位。
// 谁用：房间生成器（容器 / 敌人）、区域生成器（建筑占地 / 街道障碍）。
// 引擎只认"宽 / 高 + 怎么变成实体"，不认语义；RNG 只走外面传进来的那一条流 → 同种子同布局。
// 不变量（由调用方在"校验 / 保底"里判，引擎只保证尝试时合法）：
//   不越界、不压通道格、彼此至少隔 1 格走道。
// ============================================================
public static class 约束摆放
{
    public const int 物件距墙 = 1;             // 占位物离内墙至少 1 格（留绕行走道）
    public const int 锚点尝试上限 = 60;         // 一个区块里最多试几个锚点
    public const float 占格占比上限 = 0.35f;    // 占位物总占格 ≤ 物件区格数 × 该比例（防塞爆）

    // 一件要摆的东西：引擎只关心宽高和"怎么变成实体"
    //   建(世界, 列, 行, 序号) → 实体；序号从 0 起、每成功一件 +1（标识格式由调用方定，引擎不碰）
    public sealed class 占位物
    {
        public int 宽 = 1, 高 = 1;
        public Func<网格数据, int, int, int, 网格实体> 建;
    }

    // 一类要摆的东西：一次 摆() 里按清单顺序依次摆（**顺序会影响 RNG，调用方别乱换**）
    public sealed class 清单
    {
        public List<占位物> 项 = new List<占位物>();
        public bool 大件优先 = true;            // 先 2×2、再长条、再 1×1（提高成功率）
        public bool 避开区块心;                 // 优先避开"含 约束.区块心 的那个区块"（敌人：别一进门贴脸）
        public bool 失败后立即全图补位;          // true = 这个区块放不下就当场换全图；false = 整份走完再统一补位
    }

    public sealed class 约束
    {
        public List<(int 列, int 行)> 通道格 = new List<(int, int)>();   // 不许压的格（入口 / 门 / 楼梯及其内侧）
        public int 物件距墙 = 约束摆放.物件距墙;                          // 同名的外层常量要写全名，否则被自己遮蔽
        public int 锚点尝试上限 = 约束摆放.锚点尝试上限;
        public (int 列, int 行)? 区块心 = null;    // "避开区块"的参照格（房间传入口格）
    }

    public sealed class 区块
    {
        public int 左, 上, 右, 下;
        public 区块(int 左, int 上, int 右, int 下) { this.左 = 左; this.上 = 上; this.右 = 右; this.下 = 下; }
        public bool 含(int 列, int 行) => 列 >= 左 && 列 <= 右 && 行 >= 上 && 行 <= 下;
    }

    // ================= 主编排 =================

    public static void 摆(网格数据 世界, Random 流, 约束 约束, List<清单> 清单们)
    {
        if (世界 == null || 约束 == null || 清单们 == null) return;
        int 区左 = 1 + 约束.物件距墙, 区上 = 1 + 约束.物件距墙;
        int 区右 = 世界.列 - 2 - 约束.物件距墙, 区下 = 世界.行 - 2 - 约束.物件距墙;
        var 区块 = 划区块(区左, 区上, 区右, 区下);
        var 全图 = new 区块(区左, 区上, 区右, 区下);

        for (int 单 = 0; 单 < 清单们.Count; 单++)
        {
            var 这份 = 清单们[单];
            if (这份?.项 == null || 这份.项.Count == 0) continue;

            var 待放 = new List<占位物>(这份.项);
            if (这份.大件优先) 待放.Sort((a, b) => (b.宽 * b.高).CompareTo(a.宽 * a.高));

            // 区块顺序：默认 0..N-1；"避开区块心"的那一类，把那个区块挪到最后（配额轮流分配 → 不堆一角）
            var 块序 = new List<int>();
            for (int i = 0; i < 区块.Count; i++) 块序.Add(i);
            if (这份.避开区块心 && 约束.区块心 != null)
            {
                int 避开 = 找区块(区块, 约束.区块心.Value.列, 约束.区块心.Value.行);
                if (避开 >= 0) { 块序.Remove(避开); 块序.Add(避开); }
            }
            if (块序.Count == 0) 块序.Add(-1);

            int 序 = 0;
            var 失败 = new List<占位物>();
            for (int i = 0; i < 待放.Count; i++)
            {
                var 块 = 块序[i % 块序.Count];
                bool 成 = 试放(世界, 流, 块 >= 0 ? 区块[块] : 全图, 待放[i], 约束, ref 序);
                if (成) continue;
                if (这份.失败后立即全图补位) 试放(世界, 流, 全图, 待放[i], 约束, ref 序);
                else 失败.Add(待放[i]);
            }
            if (!这份.失败后立即全图补位)
                for (int i = 0; i < 失败.Count; i++) 试放(世界, 流, 全图, 失败[i], 约束, ref 序);
        }
    }

    // 一个区块内试放一件：锚点洗牌后逐个试，成功就把实体加进世界
    private static bool 试放(网格数据 世界, Random 流, 区块 块, 占位物 项, 约束 约束, ref int 序)
    {
        if (项?.建 == null) return false;
        var 点 = 候选锚点(流, 块);
        int 试 = 0;
        foreach (var (c, r) in 点)
        {
            if (试++ >= 约束.锚点尝试上限) break;
            if (!位置合法(世界, 约束, c, r, 项.宽, 项.高)) continue;
            世界.实体.Add(项.建(世界, c, r, 序));
            序++;
            return true;
        }
        return false;
    }

    // ================= 区块 =================

    public static List<区块> 划区块(int 区左, int 区上, int 区右, int 区下)
    {
        int 宽 = 区右 - 区左 + 1, 高 = 区下 - 区上 + 1;
        int 横 = 宽 >= 12 ? 4 : (宽 >= 8 ? 3 : 2);
        int 纵 = 高 >= 8 ? 3 : 2;
        var 块 = new List<区块>();
        for (int j = 0; j < 纵; j++)
            for (int i = 0; i < 横; i++)
            {
                int 左 = 区左 + 宽 * i / 横;
                int 右 = 区左 + 宽 * (i + 1) / 横 - 1;
                int 上 = 区上 + 高 * j / 纵;
                int 下 = 区上 + 高 * (j + 1) / 纵 - 1;
                if (右 >= 左 && 下 >= 上) 块.Add(new 区块(左, 上, 右, 下));
            }
        return 块;
    }

    public static int 找区块(List<区块> 区块, int 列, int 行)
    {
        for (int i = 0; i < 区块.Count; i++)
            if (区块[i].含(列, 行)) return i;
        return -1;
    }

    // 候选锚点 = 区块内所有格（洗牌后逐个试）
    private static List<(int 列, int 行)> 候选锚点(Random 流, 区块 块)
    {
        var 点 = new List<(int 列, int 行)>();
        for (int r = 块.上; r <= 块.下; r++)
            for (int c = 块.左; c <= 块.右; c++)
                点.Add((c, r));
        确定性随机.洗牌(流, 点);
        return 点;
    }

    // ================= 约束判定 =================

    // 位置合法：足迹在物件区内、不压通道格、**足迹里不能有墙**、与已有占位物至少隔 1 格
    public static bool 位置合法(网格数据 世界, 约束 约束, int 列, int 行, int 宽, int 高)
    {
        int 区左 = 1 + 约束.物件距墙, 区上 = 1 + 约束.物件距墙;
        int 区右 = 世界.列 - 2 - 约束.物件距墙, 区下 = 世界.行 - 2 - 约束.物件距墙;
        if (列 < 区左 || 行 < 区上) return false;
        if (列 + 宽 - 1 > 区右 || 行 + 高 - 1 > 区下) return false;
        var 通路 = 约束.通道格;
        if (通路 != null)
            for (int i = 0; i < 通路.Count; i++)
                if (覆盖(列, 行, 宽, 高, 通路[i].列, 通路[i].行)) return false;
        // 足迹里不能有墙：外圈墙由"物件区"边界管，但**后来补的墙**（楼梯间的隔墙等）必须在这里挡
        for (int r = 行; r < 行 + 高; r++)
            for (int c = 列; c < 列 + 宽; c++)
            {
                var 格 = 世界.格上实体(c, r);
                if (格 != null && 格.类型 == 网格实体类型.墙) return false;
            }
        for (int i = 0; i < 世界.实体.Count; i++)
        {
            var e = 世界.实体[i];
            if (e == null || e.是玩家) continue;
            if (e.类型 == 网格实体类型.墙) continue;   // 外圈墙由"物件区"边界管
            // 间距 ≥ 1：把候选足迹外扩一格，与已有物重叠即为太近
            if (列 - 1 < e.列 + e.宽 && 列 + 宽 + 1 > e.列 && 行 - 1 < e.行 + e.高 && 行 + 高 + 1 > e.行) return false;
        }
        return true;
    }

    public static bool 覆盖(int 列, int 行, int 宽, int 高, int 查询列, int 查询行)
        => 查询列 >= 列 && 查询列 < 列 + 宽 && 查询行 >= 行 && 查询行 < 行 + 高;

    // 离任何通道格的曼哈顿距离是否 < 距
    public static bool 离通道太近(网格数据 世界, 约束 约束, int 列, int 行, int 距)
    {
        var 通路 = 约束?.通道格;
        if (通路 == null) return false;
        for (int i = 0; i < 通路.Count; i++)
            if (Math.Abs(列 - 通路[i].列) + Math.Abs(行 - 通路[i].行) < 距) return true;
        return false;
    }

    // 保底放置用：放宽到"内区"，但通道格及其四邻一律留空（别把门口堵死），足迹里也不能有墙
    public static bool 保底可放(网格数据 世界, 约束 约束, int 列, int 行, int 宽, int 高)
    {
        if (列 < 1 || 行 < 1 || 列 + 宽 - 1 > 世界.列 - 2 || 行 + 高 - 1 > 世界.行 - 2) return false;
        for (int r = 行; r < 行 + 高; r++)
            for (int c = 列; c < 列 + 宽; c++)
            {
                var 格 = 世界.格上实体(c, r);
                if (格 != null && 格.类型 == 网格实体类型.墙) return false;   // 后来补的墙（楼梯间隔墙）
            }
        var 通路 = 约束?.通道格;
        if (通路 != null)
            for (int r = 行; r < 行 + 高; r++)
                for (int c = 列; c < 列 + 宽; c++)
                    for (int i = 0; i < 通路.Count; i++)
                        if (Math.Abs(c - 通路[i].列) <= 1 && Math.Abs(r - 通路[i].行) <= 1) return false;
        for (int i = 0; i < 世界.实体.Count; i++)
        {
            var e = 世界.实体[i];
            if (e == null || e.是玩家 || e.类型 == 网格实体类型.墙) continue;
            if (列 - 1 < e.列 + e.宽 && 列 + 宽 + 1 > e.列 && 行 - 1 < e.行 + e.高 && 行 + 高 + 1 > e.行) return false;
        }
        return true;
    }

    // 占格总量上限：物件区格数 × 占比
    public static int 占格上限(int 列, int 行, int 物件距墙 = 物件距墙, float 占比上限 = 占格占比上限)
    {
        int 物件区列 = Math.Max(1, 列 - 2 * (1 + 物件距墙));
        int 物件区行 = Math.Max(1, 行 - 2 * (1 + 物件距墙));
        return Math.Max(4, (int)(物件区列 * 物件区行 * 占比上限));
    }

    // ================= 保底布局用的走位 =================

    // 内区边框一圈格（顺时针，从左上角起）
    public static List<(int 列, int 行)> 取内环格(网格数据 世界)
    {
        var 环 = new List<(int 列, int 行)>();
        int 左 = 1, 上 = 1, 右 = 世界.列 - 2, 下 = 世界.行 - 2;
        for (int c = 左; c <= 右; c++) 环.Add((c, 上));
        for (int r = 上 + 1; r <= 下; r++) 环.Add((右, r));
        for (int c = 右 - 1; c >= 左; c--) 环.Add((c, 下));
        for (int r = 下 - 1; r > 上; r--) 环.Add((左, r));
        return 环;
    }

    // 从中心往外螺旋找一个空格（离通道格至少 距 格）
    public static (int 列, int 行)? 找中段空格(网格数据 世界, 约束 约束, int 中列, int 中行, int 序号, int 离通道距 = 3)
    {
        for (int 半径 = 0; 半径 < Math.Max(世界.列, 世界.行); 半径++)
            for (int dr = -半径; dr <= 半径; dr++)
                for (int dc = -半径; dc <= 半径; dc++)
                {
                    if (Math.Max(Math.Abs(dc), Math.Abs(dr)) != 半径) continue;
                    int c = 中列 + dc + 序号, r = 中行 + dr;
                    if (!世界.界内(c, r)) continue;
                    if (!世界.可通行(c, r)) continue;
                    if (离通道太近(世界, 约束, c, r, 离通道距)) continue;
                    return (c, r);
                }
        return null;
    }
}
