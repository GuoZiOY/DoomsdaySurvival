using System;
using System.Collections.Generic;
using System.Text;

// ============================================================
// 房间层 数据（纯 C#，零 UnityEngine 依赖）——一个「完全中空的大房间」+ 网格实体。
// 定位：四层结构（世界→区域→建筑→房间）最里面那一层。
// 唯一的运行期真相：可通行 / 阻挡视线 / 占位 全查这里；渲染用的 网格服务 由 房间探索服务 从这里同步过去。
// 实体同构：墙 / 容器 / 尸体 / 敌人 / 玩家 都是 房间实体，只有 类型 + 阵营 不同。
// ============================================================

public enum 房间实体类型 { 墙, 容器, 尸体, 敌人, 玩家 }
public enum 房间阵营 { 玩家, 敌人, 中立 }

// 网格实体：占格的东西（玩家 占格=false——自己站着的格得能走）
[Serializable]
public class 房间实体
{
    public string 标识;              // 实例 id：房间标识#序号（绝不拿定义标识当实例键：一间房可能两个"货架"）
    public 房间实体类型 类型;
    public 房间阵营 阵营;
    public string 定义标识;           // 容器定义标识 / 敌人定义标识 / "墙" / "尸体"
    public string 名称;
    public int 列, 行;
    public int 宽 = 1, 高 = 1;
    public int 朝向;                 // 0下 1左 2上 3右（只影响渲染）
    public bool 占格 = true;          // 挡住通行
    public bool 挡视线 = true;
    public bool 已搜;                // 容器 / 尸体：搜过了
    public 搜索容器 容器定义;          // 容器 / 尸体：内部网格与搜索表（可空）

    public bool 是玩家 => 阵营 == 房间阵营.玩家;

    // 是否覆盖某格
    public bool 覆盖(int 查询列, int 查询行)
        => 查询列 >= 列 && 查询列 < 列 + 宽 && 查询行 >= 行 && 查询行 < 行 + 高;

    // 与另一实体是否重叠（含"贴在一起"由调用方另判间距）
    public bool 重叠(房间实体 他)
        => 他 != null && 列 < 他.列 + 他.宽 && 列 + 宽 > 他.列 && 行 < 他.行 + 他.高 && 行 + 高 > 他.行;

    // 与另一实体的切比雪夫距离（用于"至少隔一格走道"的间距判定）
    public int 间距(房间实体 他)
    {
        if (他 == null) return 99;
        int 左 = Math.Max(列, 他.列), 右 = Math.Min(列 + 宽, 他.列 + 他.宽);
        int 上 = Math.Max(行, 他.行), 下 = Math.Min(行 + 高, 他.行 + 他.高);
        int 横 = 左 - (右 - 1);   // <=0 表示投影重叠
        int 纵 = 上 - (下 - 1);
        int 横距 = 横 > 0 ? 横 : 0;
        int 纵距 = 纵 > 0 ? 纵 : 0;
        return Math.Max(横距, 纵距);
    }
}

// 房间：尺寸 + 入口 + 实体列表（墙/容器/尸体/敌人/玩家混在一张表里）
[Serializable]
public sealed class 房间数据
{
    public string 模板标识 = "";
    public string 名称 = "";
    public string 描述 = "";
    public int 列 = 16, 行 = 10;
    public int 危险度 = 1;
    public int 视野边长 = 9;   // 可见方形边长（单数；玩家在正中）
    public string 战斗棋盘 = "室内";
    public string 敌人组 = "";
    public int 入口列, 入口行;
    public List<房间实体> 实体 = new List<房间实体>();

    // 生成统计（自检/日志用）
    public int 重掷次数;
    public bool 走了保底布局;
    public int 被移除的坏容器;
    public int 被移除的坏敌人;

    // ===== 查询 =====

    public bool 界内(int 查询列, int 查询行)
        => 查询列 >= 0 && 查询行 >= 0 && 查询列 < 列 && 查询行 < 行;

    // 第一个覆盖该格且"占格"的实体（墙/容器/敌人/尸体；玩家不算）
    public 房间实体 占位(int 查询列, int 查询行)
    {
        if (!界内(查询列, 查询行)) return null;
        for (int i = 0; i < 实体.Count; i++)
        {
            var e = 实体[i];
            if (e == null || !e.占格) continue;
            if (e.覆盖(查询列, 查询行)) return e;
        }
        return null;
    }

    // 该格上的任意实体（含玩家——渲染/命中用）
    public 房间实体 格上实体(int 查询列, int 查询行)
    {
        if (!界内(查询列, 查询行)) return null;
        for (int i = 0; i < 实体.Count; i++)
        {
            var e = 实体[i];
            if (e != null && e.覆盖(查询列, 查询行)) return e;
        }
        return null;
    }

    public bool 可通行(int 查询列, int 查询行)
        => 界内(查询列, 查询行) && 占位(查询列, 查询行) == null;

    public bool 阻挡视线(int 查询列, int 查询行)
    {
        if (!界内(查询列, 查询行)) return true;
        for (int i = 0; i < 实体.Count; i++)
        {
            var e = 实体[i];
            if (e == null || !e.挡视线) continue;
            if (e.覆盖(查询列, 查询行)) return true;
        }
        return false;
    }

    public 房间实体 按标识(string 标识)
    {
        if (string.IsNullOrEmpty(标识)) return null;
        for (int i = 0; i < 实体.Count; i++)
            if (实体[i] != null && 实体[i].标识 == 标识) return 实体[i];
        return null;
    }

    public 房间实体 玩家()
    {
        for (int i = 0; i < 实体.Count; i++)
            if (实体[i] != null && 实体[i].是玩家) return 实体[i];
        return null;
    }

    public int 玩家列()
    {
        var p = 玩家();
        return p != null ? p.列 : 入口列;
    }

    public int 玩家行()
    {
        var p = 玩家();
        return p != null ? p.行 : 入口行;
    }

    public void 移动玩家(int 列, int 行)
    {
        var p = 玩家();
        if (p == null) return;
        p.列 = 列;
        p.行 = 行;
    }

    // 按类型取实体（数量都很小，直接遍历，不做缓存字典——避免"缓存没同步"这类 bug）
    public List<房间实体> 取类型(房间实体类型 类型)
    {
        var 结果 = new List<房间实体>();
        for (int i = 0; i < 实体.Count; i++)
            if (实体[i] != null && 实体[i].类型 == 类型) 结果.Add(实体[i]);
        return 结果;
    }

    public int 容器总数() => 取类型(房间实体类型.容器).Count + 取类型(房间实体类型.尸体).Count;
    public int 敌人总数() => 取类型(房间实体类型.敌人).Count;

    public int 已搜数()
    {
        int n = 0;
        for (int i = 0; i < 实体.Count; i++)
            if (实体[i] != null && 实体[i].已搜 && (实体[i].类型 == 房间实体类型.容器 || 实体[i].类型 == 房间实体类型.尸体)) n++;
        return n;
    }

    public void 移除实体(房间实体 目标)
    {
        if (目标 == null) return;
        实体.Remove(目标);
    }

    // 从入口格出发的可达集（可通行格）
    public HashSet<int> 可达集()
    {
        var 集 = new HashSet<int>();
        var 队 = new Queue<(int 列, int 行)>();
        if (!可通行(入口列, 入口行)) return 集;
        集.Add(编码(入口列, 入口行));
        队.Enqueue((入口列, 入口行));
        int[] 横 = { 0, 0, -1, 1 };
        int[] 纵 = { -1, 1, 0, 0 };
        while (队.Count > 0)
        {
            var (c, r) = 队.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                int nc = c + 横[i], nr = r + 纵[i];
                if (!可通行(nc, nr)) continue;
                if (!集.Add(编码(nc, nr))) continue;
                队.Enqueue((nc, nr));
            }
        }
        return 集;
    }

    // 可站格连通分量数（>1 = 有孤岛，生成时判失败）
    public int 连通分量数()
    {
        var 已看 = new HashSet<int>();
        int 分量 = 0;
        for (int r = 0; r < 行; r++)
            for (int c = 0; c < 列; c++)
            {
                if (!可通行(c, r) || 已看.Contains(编码(c, r))) continue;
                分量++;
                var 队 = new Queue<(int 列, int 行)>();
                队.Enqueue((c, r));
                已看.Add(编码(c, r));
                int[] 横 = { 0, 0, -1, 1 };
                int[] 纵 = { -1, 1, 0, 0 };
                while (队.Count > 0)
                {
                    var (cc, rr) = 队.Dequeue();
                    for (int i = 0; i < 4; i++)
                    {
                        int nc = cc + 横[i], nr = rr + 纵[i];
                        if (!可通行(nc, nr) || !已看.Add(编码(nc, nr))) continue;
                        队.Enqueue((nc, nr));
                    }
                }
            }
        return 分量;
    }

    public static int 编码(int 列, int 行) => 列 * 1000 + 行;

    // 结构指纹（确定性自检：同种子两次生成必须一模一样）
    public string 指纹()
    {
        var sb = new StringBuilder();
        sb.Append($"{模板标识}|{列}x{行}|入口{入口列},{入口行}|");
        var 排序 = new List<房间实体>(实体);
        排序.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.类型.ToString(), b.类型.ToString());
            if (c != 0) return c;
            c = string.CompareOrdinal(a.定义标识 ?? "", b.定义标识 ?? "");
            if (c != 0) return c;
            c = a.列.CompareTo(b.列);
            return c != 0 ? c : a.行.CompareTo(b.行);
        });
        foreach (var e in 排序)
            sb.Append($"{e.类型}:{e.定义标识}@{e.列},{e.行} {e.宽}x{e.高} 挡视{(e.挡视线 ? 1 : 0)};");
        return sb.ToString();
    }
}

// 容器定义 → 在房间网格上的占格与挡视线。
// 说明：搜索容器 的 容器列/容器行 是「容器内部储物网格」尺寸，不等于它在房间里的占地；
//       这里按内部尺寸推导占地（启发式）——小柜 1×1、立柜/货架 2×1 或 1×2、大柜（冷柜/药柜）2×2。
//       以后要精确控制，可在 搜索容器 上加显式「占用宽/高」字段覆盖本推导。
public static class 房间容器规格
{
    public static void 占格(搜索容器 定义, out int 宽, out int 高)
    {
        宽 = 1;
        高 = 1;
        if (定义 == null) return;
        int 内宽 = Math.Max(1, 定义.容器列), 内高 = Math.Max(1, 定义.容器行);
        bool 长条 = (内宽 >= 5 && 内高 <= 4) || (内高 >= 5 && 内宽 <= 4);
        if (长条)
        {
            宽 = 内宽 >= 内高 ? 2 : 1;
            高 = 内宽 >= 内高 ? 1 : 2;
            return;
        }
        if (内宽 >= 5 && 内高 >= 5)
        {
            宽 = 2;
            高 = 2;
            return;
        }
        if (内宽 >= 4 || 内高 >= 4)
        {
            宽 = 内宽 >= 内高 ? 2 : 1;
            高 = 内宽 >= 内高 ? 1 : 2;
        }
    }

    // 高柜挡视线（冷柜/药柜/储物架 这类 内部高 >= 5 的），矮柜（鞋柜/垃圾桶/收银机）不挡
    public static bool 挡视线(搜索容器 定义) => 定义 != null && 定义.容器行 >= 5;
}
