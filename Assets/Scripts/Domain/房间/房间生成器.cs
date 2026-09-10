using System;
using System.Collections.Generic;

// ============================================================
// 房间生成器：一个「完全中空的大房间」，按种子摆可搜索容器与敌人（纯 C#，可离线跑 200 种子自检）。
// 三阶段：定尺（抽内容清单）→ 分区配额 + 约束采样（防堆一角 / 防贴死）→ 连通校验与重试（保底布局兜底）。
// 不变量（任何一条破了就是 bug）：
//   ① 物件不越界、不重叠、不压入口格；两个占位物之间至少隔 1 格走道；
//   ② 每个容器至少有一个相邻可站格，且该格与入口连通（否则就是"看得见搜不着的死内容"）；
//   ③ 每个敌人的相邻可站格同样与入口连通；
//   ④ 可站格无孤岛（连通分量 = 1）；
//   ⑤ 同模板 + 同种子 = 同布局（确定性）。
// ============================================================
public static class 房间生成器
{
    // 容器表取用：(地图类型, 房间) → 该房间的容器定义列表（房间 传空 = 该地图类型下全部房间）
    public delegate List<搜索容器> 容器表取用(string 地图类型, string 房间);
    // 敌人组取用：敌人组标识 → 展开成「一只一项」的敌人定义标识列表
    public delegate List<string> 敌人组取用(string 敌人组标识);

    public const int 最大重掷 = 5;
    public const int 最小列 = 8, 最大列 = 26;
    public const int 最小行 = 6, 最大行 = 18;

    private const int 锚点尝试上限 = 60;
    private const int 物件距墙 = 1;        // 物件离内墙至少 1 格（留绕行走道）
    private const float 占格占比上限 = 0.35f;   // 容器总占格 ≤ 物件区格数 × 该比例（防塞爆）

    public static 房间数据 生成(int 种子, 房间模板 模板, 容器表取用 取容器, 敌人组取用 取敌人)
    {
        if (模板 == null) return null;

        int 容器下限, 敌人下限;
        var 容器清单 = 抽容器(种子, 模板, 取容器, out 容器下限);
        var 敌人清单 = 抽敌人(种子, 模板, 取敌人, out 敌人下限);

        for (int 次 = 0; 次 <= 最大重掷; 次++)
        {
            var 房 = 造空房(种子, 模板);
            摆物件(房, 种子, 次, 容器清单, 敌人清单);
            if (校验(房, 容器下限, 敌人下限))
            {
                房.重掷次数 = 次;
                return 房;
            }
        }

        // 兜底：沿内墙一圈隔格摆（天然保证走道与连通），敌人放中段空地
        var 兜底 = 造空房(种子, 模板);
        保底布局(兜底, 容器清单, 敌人清单);
        兜底.走了保底布局 = true;
        兜底.重掷次数 = 最大重掷 + 1;
        return 兜底;
    }

    // ================= 内容清单（只抽一次，重掷只换摆法，不换内容） =================

    private static List<搜索容器> 抽容器(int 种子, 房间模板 模板, 容器表取用 取容器, out int 下限)
    {
        下限 = 0;
        var 清单 = new List<搜索容器>();
        var 池 = 模板.容器池;
        if (池 == null || 取容器 == null) return 清单;

        var 流 = 房间种子.随机流(房间种子.派生(种子, "容器清单"));
        foreach (var 项 in 池)
        {
            if (项 == null) continue;
            var 表 = 取容器(项.地图类型, 项.房间);
            if (表 == null || 表.Count == 0) continue;
            int 数量 = 房间种子.范围(流, Math.Max(0, 项.数量最小), Math.Max(0, 项.数量最大));
            int 实际最小 = Math.Min(数量, Math.Max(0, 项.数量最小));
            下限 += 实际最小;
            for (int i = 0; i < 数量; i++)
            {
                var 定义 = 房间种子.轮盘(流, 表, _ => 1);
                if (定义 != null) 清单.Add(定义);
            }
        }

        // 占格上限保护：超了就按池权重从低到高裁（权重低的先让位）
        裁剪到占格上限(清单, 池, 模板);
        if (下限 > 清单.Count) 下限 = 清单.Count;
        if (下限 < 1 && 清单.Count > 0) 下限 = 1;
        return 清单;
    }

    private static void 裁剪到占格上限(List<搜索容器> 清单, 房间容器池项[] 池, 房间模板 模板)
    {
        int 列 = 夹(模板.列, 最小列, 最大列), 行 = 夹(模板.行, 最小行, 最大行);
        int 物件区列 = Math.Max(1, 列 - 2 * (1 + 物件距墙));
        int 物件区行 = Math.Max(1, 行 - 2 * (1 + 物件距墙));
        int 上限 = Math.Max(4, (int)(物件区列 * 物件区行 * 占格占比上限));
        int 已占 = 0;
        for (int i = 0; i < 清单.Count; i++)
        {
            房间容器规格.占格(清单[i], out int 宽, out int 高);
            已占 += 宽 * 高;
        }
        while (已占 > 上限 && 清单.Count > 1)
        {
            var 去 = 清单[清单.Count - 1];
            清单.RemoveAt(清单.Count - 1);
            房间容器规格.占格(去, out int 宽, out int 高);
            已占 -= 宽 * 高;
        }
    }

    private static List<string> 抽敌人(int 种子, 房间模板 模板, 敌人组取用 取敌人, out int 下限)
    {
        下限 = 0;
        var 清单 = new List<string>();
        if (模板 == null || string.IsNullOrEmpty(模板.敌人组) || 取敌人 == null) return 清单;
        var 展开 = 取敌人(模板.敌人组);
        if (展开 == null || 展开.Count == 0) return 清单;

        int 要 = 夹(展开.Count, Math.Max(0, 模板.敌人数量最小), Math.Max(0, 模板.敌人数量最大));
        if (要 <= 0) return 清单;
        var 流 = 房间种子.随机流(房间种子.派生(种子, "敌人清单"));
        for (int i = 0; i < 要; i++)
        {
            // 不够就从展开表里循环取（保证数量落在模板区间内）
            int 源 = i % 展开.Count;
            string 标识 = 展开[源];
            if (i >= 展开.Count) 标识 = 展开[房间种子.范围(流, 0, 展开.Count - 1)];
            清单.Add(标识);
        }
        下限 = Math.Min(Math.Max(0, 模板.敌人数量最小), 清单.Count);
        return 清单;
    }

    // ================= 造壳 =================

    private static 房间数据 造空房(int 种子, 房间模板 模板)
    {
        var 房 = new 房间数据
        {
            模板标识 = 模板.标识 ?? "",
            名称 = string.IsNullOrEmpty(模板.名称) ? 模板.标识 : 模板.名称,
            描述 = 模板.描述 ?? "",
            列 = 夹(模板.列, 最小列, 最大列),
            行 = 夹(模板.行, 最小行, 最大行),
            危险度 = Math.Max(1, 模板.危险度),
            视野边长 = 房间视野.奇数化(视野规则.白天边长默认),   // 兜底值；真正的边长由 房间探索服务 按"角色 + 时段"写入
            战斗棋盘 = 模板.战斗棋盘 ?? "",
            敌人组 = 模板.敌人组 ?? ""
        };
        建墙(房);
        取入口格(种子, 房, 模板.入口边);
        房.实体.Add(new 房间实体
        {
            标识 = $"{房.模板标识}#玩家",
            类型 = 房间实体类型.玩家,
            阵营 = 房间阵营.玩家,
            定义标识 = "玩家",
            名称 = "你",
            列 = 房.入口列,
            行 = 房.入口行,
            占格 = false,       // 自己站着的格要能走
            挡视线 = false
        });
        return 房;
    }

    private static void 建墙(房间数据 房)
    {
        int 序 = 0;
        for (int c = 0; c < 房.列; c++)
        {
            加墙(房, c, 0, ref 序);
            加墙(房, c, 房.行 - 1, ref 序);
        }
        for (int r = 1; r < 房.行 - 1; r++)
        {
            加墙(房, 0, r, ref 序);
            加墙(房, 房.列 - 1, r, ref 序);
        }
    }

    private static void 加墙(房间数据 房, int 列, int 行, ref int 序)
    {
        房.实体.Add(new 房间实体
        {
            标识 = $"{房.模板标识}#墙{序++}",
            类型 = 房间实体类型.墙,
            阵营 = 房间阵营.中立,
            定义标识 = "墙",
            名称 = "墙",
            列 = 列,
            行 = 行,
            占格 = true,
            挡视线 = true
        });
    }

    private static void 取入口格(int 种子, 房间数据 房, string 边)
    {
        var 流 = 房间种子.随机流(房间种子.派生(种子, "入口"));
        int 内左 = 1, 内上 = 1, 内右 = 房.列 - 2, 内下 = 房.行 - 2;
        int 抖 = 房间种子.范围(流, -2, 2);
        string 用边 = string.IsNullOrEmpty(边) ? "下" : 边;
        if (用边 == "上")
        {
            房.入口行 = 内上;
            房.入口列 = 夹((内左 + 内右) / 2 + 抖, 内左, 内右);
            return;
        }
        if (用边 == "左")
        {
            房.入口列 = 内左;
            房.入口行 = 夹((内上 + 内下) / 2 + 抖, 内上, 内下);
            return;
        }
        if (用边 == "右")
        {
            房.入口列 = 内右;
            房.入口行 = 夹((内上 + 内下) / 2 + 抖, 内上, 内下);
            return;
        }
        房.入口行 = 内下;
        房.入口列 = 夹((内左 + 内右) / 2 + 抖, 内左, 内右);
    }

    // ================= 摆放（分区配额 + 约束采样） =================

    private static void 摆物件(房间数据 房, int 种子, int 轮次, List<搜索容器> 容器清单, List<string> 敌人清单)
    {
        var 流 = 房间种子.随机流(房间种子.派生(种子, $"摆放{轮次}"));

        // 物件区（离内墙 1 格）
        int 区左 = 1 + 物件距墙, 区上 = 1 + 物件距墙;
        int 区右 = 房.列 - 2 - 物件距墙, 区下 = 房.行 - 2 - 物件距墙;
        var 区块 = 划区块(区左, 区上, 区右, 区下);

        // 大件优先（先放 2×2，再放长条，再放 1×1）——提高成功率
        var 待放 = new List<(搜索容器 定义, int 宽, int 高, int 权重)>();
        for (int i = 0; i < 容器清单.Count; i++)
        {
            房间容器规格.占格(容器清单[i], out int 宽, out int 高);
            待放.Add((容器清单[i], 宽, 高, 1));
        }
        待放.Sort((a, b) => (b.宽 * b.高).CompareTo(a.宽 * a.高));

        int 容器序 = 0;
        var 失败项 = new List<(搜索容器 定义, int 宽, int 高, int 权重)>();
        for (int i = 0; i < 待放.Count; i++)
        {
            var 项 = 待放[i];
            var 块 = 区块[i % 区块.Count];   // 配额：轮流分配（配合区块内随机 → 不堆一角）
            if (!试放(房, 流, 块, 项.定义, 项.宽, 项.高, ref 容器序)) 失败项.Add(项);
        }
        // 待放队列：全物件区补位
        var 全图 = new 区块矩形(区左, 区上, 区右, 区下);
        for (int i = 0; i < 失败项.Count; i++)
            试放(房, 流, 全图, 失败项[i].定义, 失败项[i].宽, 失败项[i].高, ref 容器序);

        // 敌人：优先放"不与入口同区块"的位置（避免一进门贴脸）
        int 入口块 = 找区块(区块, 房.入口列, 房.入口行);
        var 敌序 = new List<int>();
        for (int i = 0; i < 区块.Count; i++)
            if (i != 入口块) 敌序.Add(i);
        if (入口块 >= 0) 敌序.Add(入口块);
        if (敌序.Count == 0) for (int i = 0; i < 区块.Count; i++) 敌序.Add(i);

        int 敌人计数 = 0;
        for (int i = 0; i < 敌人清单.Count; i++)
        {
            var 块 = 区块[敌序[i % 敌序.Count]];
            if (!试放敌人(房, 流, 块, 敌人清单[i], ref 敌人计数))
                试放敌人(房, 流, 全图, 敌人清单[i], ref 敌人计数);
        }
    }

    private sealed class 区块矩形
    {
        public int 左, 上, 右, 下;
        public 区块矩形(int 左, int 上, int 右, int 下) { this.左 = 左; this.上 = 上; this.右 = 右; this.下 = 下; }
        public bool 含(int 列, int 行) => 列 >= 左 && 列 <= 右 && 行 >= 上 && 行 <= 下;
    }

    private static List<区块矩形> 划区块(int 区左, int 区上, int 区右, int 区下)
    {
        int 宽 = 区右 - 区左 + 1, 高 = 区下 - 区上 + 1;
        int 横 = 宽 >= 12 ? 4 : (宽 >= 8 ? 3 : 2);
        int 纵 = 高 >= 8 ? 3 : 2;
        var 块 = new List<区块矩形>();
        for (int j = 0; j < 纵; j++)
            for (int i = 0; i < 横; i++)
            {
                int 左 = 区左 + 宽 * i / 横;
                int 右 = 区左 + 宽 * (i + 1) / 横 - 1;
                int 上 = 区上 + 高 * j / 纵;
                int 下 = 区上 + 高 * (j + 1) / 纵 - 1;
                if (右 >= 左 && 下 >= 上) 块.Add(new 区块矩形(左, 上, 右, 下));
            }
        return 块;
    }

    private static int 找区块(List<区块矩形> 区块, int 列, int 行)
    {
        for (int i = 0; i < 区块.Count; i++)
            if (区块[i].含(列, 行)) return i;
        return -1;
    }

    // 候选锚点 = 区块内所有格（洗牌后逐个试）
    private static List<(int 列, int 行)> 候选锚点(Random 流, 区块矩形 块)
    {
        var 点 = new List<(int 列, int 行)>();
        for (int r = 块.上; r <= 块.下; r++)
            for (int c = 块.左; c <= 块.右; c++)
                点.Add((c, r));
        房间种子.洗牌(流, 点);
        return 点;
    }

    private static bool 试放(房间数据 房, Random 流, 区块矩形 块, 搜索容器 定义, int 宽, int 高, ref int 序号)
    {
        if (定义 == null) return false;
        var 点 = 候选锚点(流, 块);
        int 试 = 0;
        foreach (var (c, r) in 点)
        {
            if (试++ >= 锚点尝试上限) break;
            if (!位置合法(房, c, r, 宽, 高)) continue;
            房.实体.Add(new 房间实体
            {
                标识 = $"{房.模板标识}#容器{序号++}",
                类型 = 房间实体类型.容器,
                阵营 = 房间阵营.中立,
                定义标识 = 定义.标识,
                名称 = string.IsNullOrEmpty(定义.名称) ? 定义.标识 : 定义.名称,
                列 = c,
                行 = r,
                宽 = 宽,
                高 = 高,
                占格 = true,
                挡视线 = 房间容器规格.挡视线(定义),
                容器定义 = 定义
            });
            return true;
        }
        return false;
    }

    private static bool 试放敌人(房间数据 房, Random 流, 区块矩形 块, string 敌人标识, ref int 序号)
    {
        if (string.IsNullOrEmpty(敌人标识)) return false;
        var 点 = 候选锚点(流, 块);
        int 试 = 0;
        foreach (var (c, r) in 点)
        {
            if (试++ >= 锚点尝试上限) break;
            if (!位置合法(房, c, r, 1, 1)) continue;
            房.实体.Add(new 房间实体
            {
                标识 = $"{房.模板标识}#敌人{序号++}",
                类型 = 房间实体类型.敌人,
                阵营 = 房间阵营.敌人,
                定义标识 = 敌人标识,
                名称 = 敌人标识,
                列 = c,
                行 = r,
                占格 = true,
                挡视线 = true
            });
            return true;
        }
        return false;
    }

    // 位置合法：足迹在物件区内、不压入口格、与已有占位物至少隔 1 格
    private static bool 位置合法(房间数据 房, int 列, int 行, int 宽, int 高)
    {
        int 区左 = 1 + 物件距墙, 区上 = 1 + 物件距墙;
        int 区右 = 房.列 - 2 - 物件距墙, 区下 = 房.行 - 2 - 物件距墙;
        if (列 < 区左 || 行 < 区上) return false;
        if (列 + 宽 - 1 > 区右 || 行 + 高 - 1 > 区下) return false;
        if (覆盖(列, 行, 宽, 高, 房.入口列, 房.入口行)) return false;
        for (int i = 0; i < 房.实体.Count; i++)
        {
            var e = 房.实体[i];
            if (e == null || e.是玩家) continue;
            if (e.类型 == 房间实体类型.墙) continue;   // 外圈墙由"物件区"边界管
            // 间距 ≥ 1：把候选足迹外扩一格，与已有物重叠即为太近
            if (列 - 1 < e.列 + e.宽 && 列 + 宽 + 1 > e.列 && 行 - 1 < e.行 + e.高 && 行 + 高 + 1 > e.行) return false;
        }
        return true;
    }

    private static bool 覆盖(int 列, int 行, int 宽, int 高, int 查询列, int 查询行)
        => 查询列 >= 列 && 查询列 < 列 + 宽 && 查询行 >= 行 && 查询行 < 行 + 高;

    // ================= 连通校验 =================

    private static bool 校验(房间数据 房, int 容器下限, int 敌人下限)
    {
        var 可达 = 房.可达集();
        if (可达.Count == 0) return false;

        // 坏容器：没有任何相邻可站格落在可达集里 → 移除（不动整间房，避免大改观感）
        var 容器们 = 房.取类型(房间实体类型.容器);
        foreach (var e in 容器们)
            if (!有相邻可达格(房, 可达, e))
            {
                房.移除实体(e);
                房.被移除的坏容器++;
            }

        var 敌人们 = 房.取类型(房间实体类型.敌人);
        foreach (var e in 敌人们)
            if (!有相邻可达格(房, 可达, e))
            {
                房.移除实体(e);
                房.被移除的坏敌人++;
            }

        if (房.容器总数() < 容器下限) return false;
        if (房.敌人总数() < 敌人下限) return false;
        if (房.连通分量数() > 1) return false;
        return true;
    }

    private static bool 有相邻可达格(房间数据 房, HashSet<int> 可达, 房间实体 e)
    {
        for (int r = e.行; r < e.行 + e.高; r++)
            for (int c = e.列; c < e.列 + e.宽; c++)
            {
                if (检查(房, 可达, c - 1, r)) return true;
                if (检查(房, 可达, c + 1, r)) return true;
                if (检查(房, 可达, c, r - 1)) return true;
                if (检查(房, 可达, c, r + 1)) return true;
            }
        return false;
    }

    private static bool 检查(房间数据 房, HashSet<int> 可达, int 列, int 行)
        => 房.界内(列, 行) && 可达.Contains(房间数据.编码(列, 行));

    // ================= 保底布局（必定成功） =================
    // 沿内区边框隔一格摆容器（天然留出走道，中心全空 → 必然连通可达），敌人放中段空地。

    private static void 保底布局(房间数据 房, List<搜索容器> 容器清单, List<string> 敌人清单)
    {
        var 环 = 取内环格(房);
        int 容器序 = 0, 已放 = 0;
        for (int i = 0; i < 环.Count && 已放 < 容器清单.Count; i += 2)
        {
            var (c, r) = 环[i];
            var 定义 = 容器清单[已放];
            房间容器规格.占格(定义, out int 宽, out int 高);
            if (!保底可放(房, c, r, 宽, 高)) continue;
            房.实体.Add(new 房间实体
            {
                标识 = $"{房.模板标识}#容器{容器序++}",
                类型 = 房间实体类型.容器,
                阵营 = 房间阵营.中立,
                定义标识 = 定义.标识,
                名称 = string.IsNullOrEmpty(定义.名称) ? 定义.标识 : 定义.名称,
                列 = c,
                行 = r,
                宽 = 宽,
                高 = 高,
                占格 = true,
                挡视线 = 房间容器规格.挡视线(定义),
                容器定义 = 定义
            });
            已放++;
        }

        int 敌序 = 0;
        int 中列 = 房.列 / 2, 中行 = 房.行 / 2;
        for (int i = 0; i < 敌人清单.Count; i++)
        {
            var 位 = 找中段空格(房, 中列, 中行, i);
            if (位 == null) break;
            房.实体.Add(new 房间实体
            {
                标识 = $"{房.模板标识}#敌人{敌序++}",
                类型 = 房间实体类型.敌人,
                阵营 = 房间阵营.敌人,
                定义标识 = 敌人清单[i],
                名称 = 敌人清单[i],
                列 = 位.Value.列,
                行 = 位.Value.行,
                占格 = true,
                挡视线 = true
            });
        }
    }

    private static List<(int 列, int 行)> 取内环格(房间数据 房)
    {
        var 环 = new List<(int 列, int 行)>();
        int 左 = 1, 上 = 1, 右 = 房.列 - 2, 下 = 房.行 - 2;
        for (int c = 左; c <= 右; c++) 环.Add((c, 上));
        for (int r = 上 + 1; r <= 下; r++) 环.Add((右, r));
        for (int c = 右 - 1; c >= 左; c--) 环.Add((c, 下));
        for (int r = 下 - 1; r > 上; r--) 环.Add((左, r));
        return 环;
    }

    private static bool 保底可放(房间数据 房, int 列, int 行, int 宽, int 高)
    {
        if (列 < 1 || 行 < 1 || 列 + 宽 - 1 > 房.列 - 2 || 行 + 高 - 1 > 房.行 - 2) return false;
        // 入口格及其四邻都留空（别把门口堵死）
        for (int r = 行; r < 行 + 高; r++)
            for (int c = 列; c < 列 + 宽; c++)
            {
                if (Math.Abs(c - 房.入口列) <= 1 && Math.Abs(r - 房.入口行) <= 1) return false;
            }
        for (int i = 0; i < 房.实体.Count; i++)
        {
            var e = 房.实体[i];
            if (e == null || e.是玩家 || e.类型 == 房间实体类型.墙) continue;
            if (列 - 1 < e.列 + e.宽 && 列 + 宽 + 1 > e.列 && 行 - 1 < e.行 + e.高 && 行 + 高 + 1 > e.行) return false;
        }
        return true;
    }

    private static (int 列, int 行)? 找中段空格(房间数据 房, int 中列, int 中行, int 序号)
    {
        for (int 半径 = 0; 半径 < Math.Max(房.列, 房.行); 半径++)
            for (int dr = -半径; dr <= 半径; dr++)
                for (int dc = -半径; dc <= 半径; dc++)
                {
                    if (Math.Max(Math.Abs(dc), Math.Abs(dr)) != 半径) continue;
                    int c = 中列 + dc + 序号, r = 中行 + dr;
                    if (!房.界内(c, r)) continue;
                    if (!房.可通行(c, r)) continue;
                    if (Math.Abs(c - 房.入口列) + Math.Abs(r - 房.入口行) < 3) continue;
                    return (c, r);
                }
        return null;
    }

    private static int 夹(int 值, int 最小, int 最大)
    {
        if (值 < 最小) return 最小;
        if (值 > 最大) return 最大;
        return 值;
    }
}
