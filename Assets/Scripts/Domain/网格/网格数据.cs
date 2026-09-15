using System;
using System.Collections.Generic;
using System.Text;

// ============================================================
// 房间层 数据（纯 C#，零 UnityEngine 依赖）——一个「完全中空的大房间」+ 网格实体。
// 定位：四层结构（世界→区域→建筑→房间）最里面那一层。
// 唯一的运行期真相：可通行 / 阻挡视线 / 占位 全查这里；渲染用的 网格服务 由 房间探索服务 从这里同步过去。
// 实体同构：墙 / 容器 / 尸体 / 敌人 / 玩家 都是 网格实体，只有 类型 + 阵营 不同。
// ============================================================

// 注：新值一律**追加在末尾**（枚举值可能进过旧的临时数据/日志，插在中间会让老值的语义漂移）
//     区域 / 临时建筑 = 大世界层（100×100 格子世界）上的两类实体，见 docs/大世界网格与副本设计.md
public enum 网格实体类型 { 墙, 容器, 尸体, 敌人, 玩家, 门, 建筑, 障碍, 楼梯, 界外, 区域, 临时建筑 }
public enum 网格阵营 { 玩家, 敌人, 中立 }

// 网格实体：占格的东西（玩家 占格=false——自己站着的格得能走）
[Serializable]
public class 网格实体
{
    public string 标识;              // 实例 id：房间标识#序号（绝不拿定义标识当实例键：一间房可能两个"货架"）
    public 网格实体类型 类型;
    public 网格阵营 阵营;
    public string 定义标识;           // 容器定义标识 / 敌人定义标识 / "墙" / "尸体"
    public string 名称;
    public int 列, 行;
    public int 宽 = 1, 高 = 1;
    public int 朝向;                 // 0下 1左 2上 3右（门 = 开在哪条外墙上；其余实体只影响渲染）
    public bool 占格 = true;          // 挡住通行
    public bool 挡视线 = true;
    public bool 已搜;                // 容器 / 尸体：搜过了
    public 搜索容器 容器定义;          // 容器 / 尸体：内部网格与搜索表（可空）

    // —— 门专用 ——
    public string 通向 = "";          // 通向的房间模板标识；空 = 通向外面（出楼）
    public bool 是大门;               // 正门（外墙上那扇，通向外面）
    public string 锁钥匙 = "";        // 开这把锁要的物品标识；空 = 无锁（踩上去就过）
    public bool 已解锁;               // 本趟已解锁（跨"再进这间房"由 房间探索服务 记住并写回）

    // —— 建筑专用 ——
    // 门口格（编码后的"列,行"，-1 = 没这门）：建筑自己身上那格**能站能走**的门洞。
    // 有了它，"门"就长在建筑边上（而不是在建筑外面那一格），走上去才进楼。
    public int 门口格 = -1;
    // 占地掩码（**非矩形楼**）：null = 纯矩形（宽×高全都算）；
    // 否则逐格 true/false（索引 = (行-行0) * 掩码列 + (列-列0)）→ 能表达 L / [ / T / 十字 等任意形状。
    public bool[] 形状掩码;
    public int 掩码列;

    // —— 区域副本专用（大世界上的区域占地）——
    // 解锁条件（空 = 一开始就能进）：由 大世界生成器 从 世界.json 带过来，服务层靠它决定"进不进得去"。
    // 放在实体上而不是查表：区域实体本来就是每次生成产物，条件跟着它走最省心。
    public 解锁条件项[] 解锁;

    // 门锁着吗：写了钥匙标识且还没解锁 → 锁着（占格、挡视线，只能站在门前）
    public bool 锁着 => 类型 == 网格实体类型.门 && !string.IsNullOrEmpty(锁钥匙) && !已解锁;

    // 是不是玩家实体 —— ⚠ **按类型判，不能按阵营判**（v52 修）：
    //   原来写的是 `阵营 == 网格阵营.玩家`，而 `网格阵营 { 玩家, 敌人, 中立 }` 的**第一个成员就是"玩家"**
    //   → 阵营的**缺省值就是玩家** → 任何"忘了写阵营"的实体（容器/障碍/敌人…）都会被当成玩家：
    //     `网格数据.玩家()` 会返回它、`约束摆放` 会跳过它、三个面板会把它的点击关掉。
    //   现在全仓库恰好每处都写了阵营所以没炸，但那是**走运**，不是设计 —— 按类型判就再也不看运气。
    public bool 是玩家 => 类型 == 网格实体类型.玩家;

    public bool 是敌人 => 类型 == 网格实体类型.敌人;

    // 是否覆盖某格（矩形 + 占地掩码：非矩形楼只覆盖掩码为 true 的那些格）
    public bool 覆盖(int 查询列, int 查询行)
    {
        if (查询列 < 列 || 查询列 >= 列 + 宽 || 查询行 < 行 || 查询行 >= 行 + 高) return false;
        if (形状掩码 == null) return true;
        int i = (查询行 - 行) * Math.Max(1, 掩码列) + (查询列 - 列);
        return i >= 0 && i < 形状掩码.Length && 形状掩码[i];
    }

    // 与另一实体是否重叠（含"贴在一起"由调用方另判间距）
    public bool 重叠(网格实体 他)
        => 他 != null && 列 < 他.列 + 他.宽 && 列 + 宽 > 他.列 && 行 < 他.行 + 他.高 && 行 + 高 > 他.行;

    // 与另一实体的切比雪夫距离（用于"至少隔一格走道"的间距判定）
    public int 间距(网格实体 他)
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

    // ★ 刀65：实体深副本（实体没有指向别的实体的引用，所以"深"到这一层就到头了）。
    // `去容器定义`：`容器定义` 是**指向 DataService 模板的共享引用**（房间生成器 直接把模板给了实体），
    //   把它放进战局快照会让每一张搜索表都被复制进存档 → 快照置空、读档时按 `定义标识` 回填。
    public 网格实体 副本(bool 去容器定义)
    {
        var 新 = new 网格实体
        {
            标识 = 标识, 类型 = 类型, 阵营 = 阵营, 定义标识 = 定义标识, 名称 = 名称,
            列 = 列, 行 = 行, 宽 = 宽, 高 = 高, 朝向 = 朝向,
            占格 = 占格, 挡视线 = 挡视线, 已搜 = 已搜,
            通向 = 通向, 是大门 = 是大门, 锁钥匙 = 锁钥匙, 已解锁 = 已解锁,
            门口格 = 门口格, 掩码列 = 掩码列,
            容器定义 = 去容器定义 ? null : 容器定义,
        };
        新.形状掩码 = 形状掩码 == null ? null : (bool[])形状掩码.Clone();
        新.解锁 = 解锁;   // 解锁条件 是纯数据、运行期只读 → 共享引用即可
        return 新;
    }
}

// 房间：尺寸 + 入口 + 实体列表（墙/容器/尸体/敌人/玩家混在一张表里）
[Serializable]
public sealed class 网格数据
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
    public List<网格实体> 实体 = new List<网格实体>();

    // 生成统计（自检/日志用）
    public int 重掷次数;
    public bool 走了保底布局;
    public int 被移除的坏容器;
    public int 被移除的坏敌人;
    public int 门开不出;   // 想在墙上开门但位置排不开（自检会报；>0 = 数据里门太多 / 房间太小）
    public int 区域摆不下;  // 大世界层：区域的占地摆不进世界边界（自检会报；>0 = 区域坐标或尺寸写歪了）
    public int 撤掉的障碍;  // 大世界层：撒完障碍后连通被破坏，从这个数目的障碍被撤回（>0 = 障碍太多/太挤）
    public int 撤掉的敌人;  // 大世界层：同理，但撤的是敌人（障碍撤完还不够连通时才会动它）

    // ===== 查询 =====

    public bool 界内(int 查询列, int 查询行)
        => 查询列 >= 0 && 查询行 >= 0 && 查询列 < 列 && 查询行 < 行;

    // 第一个覆盖该格且"占格"的实体（墙/容器/敌人/尸体；玩家不算）
    // 例外：**建筑 / 区域 / 临时建筑 的门口格**能站能走 ——
    //   · 建筑：门长在建筑自己边上（而不是建筑外面那一格），走上去才进楼；
    //   · 区域（大世界层）：一片街区是"不能穿过的整块"，只在**门口那一格**进去（其余格照旧挡路）；
    //   · 临时建筑（大世界层）：同理（搜索翻出来的那一间，走门口那一格进去）。
    // 三种都是"占格物 + 一个能站能走的门洞"这同一个语义，所以豁免条件是同一份。
    public 网格实体 占位(int 查询列, int 查询行)
    {
        if (!界内(查询列, 查询行)) return null;
        for (int i = 0; i < 实体.Count; i++)
        {
            var e = 实体[i];
            if (e == null || !e.占格) continue;
            if (!e.覆盖(查询列, 查询行)) continue;
            if (是带门洞的占格物(e) && e.门口格 == 编码(查询列, 查询行)) continue;
            return e;
        }
        return null;
    }

    // 带门洞的占格物（建筑 / 区域 / 临时建筑）：只有它们享有"门口格能站能走"的豁免
    public static bool 是带门洞的占格物(网格实体 实体)
        => 实体 != null && (实体.类型 == 网格实体类型.建筑
                         || 实体.类型 == 网格实体类型.区域
                         || 实体.类型 == 网格实体类型.临时建筑);

    // 该格上的任意实体（含玩家——渲染/命中用）
    public 网格实体 格上实体(int 查询列, int 查询行)
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

    public 网格实体 按标识(string 标识)
    {
        if (string.IsNullOrEmpty(标识)) return null;
        for (int i = 0; i < 实体.Count; i++)
            if (实体[i] != null && 实体[i].标识 == 标识) return 实体[i];
        return null;
    }

    public 网格实体 玩家()
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
    public List<网格实体> 取类型(网格实体类型 类型)
    {
        var 结果 = new List<网格实体>();
        for (int i = 0; i < 实体.Count; i++)
            if (实体[i] != null && 实体[i].类型 == 类型) 结果.Add(实体[i]);
        return 结果;
    }

    public int 容器总数() => 取类型(网格实体类型.容器).Count + 取类型(网格实体类型.尸体).Count;
    public int 敌人总数() => 取类型(网格实体类型.敌人).Count;

    // ================= 门 =================

    public List<网格实体> 门列表() => 取类型(网格实体类型.门);

    // 楼梯列表（建筑层：外墙上那一格"凸"，上下楼就走它）
    public List<网格实体> 楼梯列表() => 取类型(网格实体类型.楼梯);

    // 结构实体：占格=false 但**是路**的那些（门 / 楼梯）——它们的内侧格必须留空、必须可达
    public List<网格实体> 结构实体()
    {
        var 结果 = new List<网格实体>();
        for (int i = 0; i < 实体.Count; i++)
        {
            var e = 实体[i];
            if (e == null) continue;
            if (e.类型 == 网格实体类型.门 || e.类型 == 网格实体类型.楼梯) 结果.Add(e);
        }
        return 结果;
    }

    // 门的内侧格（门里那一格）：进门落在这里；也是"生成时必须留空"的关键格
    // 门开在下边（朝向 0）→ 里面在上方，以此类推
    public (int 列, int 行) 门内侧格(网格实体 门)
    {
        if (门 == null) return (入口列, 入口行);
        switch (门.朝向)
        {
            case 2: return (门.列, 门.行 + 1);    // 门在上边 → 里面在下方
            case 1: return (门.列 + 1, 门.行);    // 门在左边 → 里面在右方
            case 3: return (门.列 - 1, 门.行);    // 门在右边 → 里面在左方
            default: return (门.列, 门.行 - 1);   // 门在下边 → 里面在上方
        }
    }

    // 找"通向某个房间模板"的那扇门（从门进来时，落点就是它的内侧格；找不到返回 null）
    public 网格实体 找门通向(string 模板标识)
    {
        if (string.IsNullOrEmpty(模板标识)) return null;
        var 门们 = 门列表();
        for (int i = 0; i < 门们.Count; i++)
            if (门们[i].通向 == 模板标识) return 门们[i];
        return null;
    }

    // 大门（通向外面那扇）
    public 网格实体 大门()
    {
        var 门们 = 门列表();
        for (int i = 0; i < 门们.Count; i++)
            if (门们[i].是大门) return 门们[i];
        return null;
    }

    // 通道格：门格 + 门内侧格 + **楼梯格 + 楼梯内侧格** + 入口格 —— 摆放容器/敌人时必须避开（门口/楼梯口不能堵死）
    public List<(int 列, int 行)> 通道格()
    {
        var 格 = new List<(int 列, int 行)> { (入口列, 入口行) };
        var 结构 = 结构实体();
        for (int i = 0; i < 结构.Count; i++)
        {
            格.Add((结构[i].列, 结构[i].行));
            格.Add(门内侧格(结构[i]));
        }
        return 格;
    }

    public int 已搜数()
    {
        int n = 0;
        for (int i = 0; i < 实体.Count; i++)
            if (实体[i] != null && 实体[i].已搜 && (实体[i].类型 == 网格实体类型.容器 || 实体[i].类型 == 网格实体类型.尸体)) n++;
        return n;
    }

    public void 移除实体(网格实体 目标)
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
    public string 指纹()    {
        var sb = new StringBuilder();
        sb.Append($"{模板标识}|{列}x{行}|入口{入口列},{入口行}|");
        var 排序 = new List<网格实体>(实体);
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
            sb.Append($"{e.类型}:{e.定义标识}@{e.列},{e.行} {e.宽}x{e.高} 挡视{(e.挡视线 ? 1 : 0)}" +
                      (e.类型 == 网格实体类型.门 ? $" 朝向{e.朝向} 通向{(string.IsNullOrEmpty(e.通向) ? "外面" : e.通向)};" : ";"));
        return sb.ToString();
    }

    // ★ 刀65：整层世界的深副本（战局快照用，见 存档战局.cs 的说明）。
    // `去容器定义` 一路传到实体上 —— 理由见 网格实体.副本。
    public 网格数据 副本(bool 去容器定义)
    {
        var 新 = new 网格数据
        {
            模板标识 = 模板标识, 名称 = 名称, 描述 = 描述, 列 = 列, 行 = 行,
            危险度 = 危险度, 视野边长 = 视野边长, 战斗棋盘 = 战斗棋盘, 敌人组 = 敌人组,
            入口列 = 入口列, 入口行 = 入口行,
            重掷次数 = 重掷次数, 走了保底布局 = 走了保底布局,
            被移除的坏容器 = 被移除的坏容器, 被移除的坏敌人 = 被移除的坏敌人,
            门开不出 = 门开不出, 区域摆不下 = 区域摆不下,
            撤掉的障碍 = 撤掉的障碍, 撤掉的敌人 = 撤掉的敌人,
        };
        for (int i = 0; i < 实体.Count; i++)
        {
            var e = 实体[i];
            if (e != null) 新.实体.Add(e.副本(去容器定义));
        }
        return 新;
    }
}

// 容器定义 → 在房间网格上的占格与挡视线。
// 说明：搜索容器 的 容器列/容器行 是「容器内部储物网格」尺寸，不等于它在房间里的占地；
//       这里按内部尺寸推导占地（启发式）——小柜 1×1、立柜/货架 2×1 或 1×2、大柜（冷柜/药柜）2×2。
//       以后要精确控制，可在 搜索容器 上加显式「占用宽/高」字段覆盖本推导。
public static class 容器规格
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
