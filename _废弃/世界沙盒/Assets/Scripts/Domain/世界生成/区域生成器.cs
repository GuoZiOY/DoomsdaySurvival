using System;
using System.Collections.Generic;

// 区域生成器：地区模板 → 一片城区（区域层街道网格 + 建筑入口 + 街道搜索点 + 敌人 + 出口）——纯 C#，确定性。
//
// 尺度约定（重要）：街道网格是「粗粒度」的——一栋楼在街上只占一个 2~3 格的门面块，
//   楼里那套 9×6 的 BSP 房间是**独立网格**（建筑生成器产出）。两者不需要尺寸对齐。
//   这样街道可以同时容纳多栋楼，而不用担心「一栋居民房就吃掉整张图」。
//
// 生成-校验-重试-兜底：每轮尝试后 BFS 校验「建筑入口 / 搜索点 / 敌人 全部从出生点可达」，不满足换下一轮种子。
public static class 区域生成器
{
    private const int 最大尝试 = 16;

    // 生成一片城区。取建筑模板 / 取地图类型 = 注入的查表委托（Domain 不持有 DataService）
    public static 片区数据 生成(long 世界种子, 地区模板 模板,
        Func<string, 建筑模板> 取建筑模板, Func<string, 搜索地图类型> 取地图类型)
    {
        if (模板 == null) return null;
        int 列 = Math.Max(10, 模板.网格列), 行 = Math.Max(8, 模板.网格行);
        for (int 尝试 = 0; 尝试 < 最大尝试; 尝试++)
        {
            var 随机 = 沙盒种子.随机流(沙盒种子.派生(世界种子, "地区", 模板.标识, 尝试));
            var 片区 = 尝试生成(世界种子, 随机, 模板, 列, 行, 取建筑模板, 取地图类型);
            if (片区 != null) return 片区;
        }
        // 兜底：最简空场（只有围墙 + 出口 + 出生点），保证「进得去、出得来」
        return 空场兜底(模板, 列, 行);
    }

    // ===== 一次尝试 =====

    private static 片区数据 尝试生成(long 世界种子, Random 随机, 地区模板 模板, int 列, int 行,
        Func<string, 建筑模板> 取建筑模板, Func<string, 搜索地图类型> 取地图类型)
    {
        var 街道 = new 沙盒层数据(列, 行)
        {
            名称 = 模板.名称,
            地图类型 = 模板.街道地图类型,
            战斗棋盘 = string.IsNullOrEmpty(模板.战斗棋盘) ? "街头" : 模板.战斗棋盘,
        };
        // ① 铺街道 + 四周封边（建筑体：不可通行、阻挡视线）
        for (int c = 0; c < 列; c++)
            for (int r = 0; r < 行; r++)
                街道.设(c, r, (c == 0 || r == 0 || c == 列 - 1 || r == 行 - 1) ? 沙盒地形.建筑体 : 沙盒地形.街道);

        var 片区 = new 片区数据
        {
            地区标识 = 模板.标识,
            名称 = 模板.名称,
            危险度 = 模板.危险度,
            街道 = 街道,
        };

        // ② 出口 = 贴着边界的街道格（撤离点，也是进入地区的出生点）
        var 出口 = 边界街道格(随机, 街道);
        if (出口 == null) return null;
        街道.设(出口.Value.列, 出口.Value.行, 沙盒地形.出口);
        街道.出生列 = 出口.Value.列; 街道.出生行 = 出口.Value.行;

        // ③ 建筑：中心建筑（必建）+ 配套池（权重 + 数量区间），落在槽位里
        var 建筑清单 = 抽建筑清单(随机, 模板, 取建筑模板);
        var 槽位 = 划槽位(列, 行, Math.Max(1, 建筑清单.Count));
        沙盒种子.洗牌(随机, 槽位);
        int 序号 = 0;
        foreach (var 建筑模板项 in 建筑清单)
        {
            if (序号 >= 槽位.Count) break;                  // 槽位不够：多余的楼不放
            var 槽 = 槽位[序号];
            var 占地 = 放占地(随机, 街道, 槽);
            if (占地 == null) { 序号++; continue; }
            // 占地格 = 建筑体
            for (int c = 占地.Value.列; c < 占地.Value.列 + 占地.Value.宽; c++)
                for (int r = 占地.Value.行; r < 占地.Value.行 + 占地.Value.高; r++)
                    街道.设(c, r, 沙盒地形.建筑体);

            string 建筑标识 = $"{模板.标识}_{序号 + 1}_{建筑模板项.标识}";
            var 搜索类型 = 取地图类型?.Invoke(建筑模板项.地图类型);
            var 楼 = 建筑生成器.生成(世界种子, 模板.标识, 建筑标识, 建筑模板项, 搜索类型);
            if (楼 == null || 楼.楼层.Count == 0) { 序号++; continue; }
            // 楼门：在门面上开一扇【真门】（关着，点它进屋）——优先朝街的那一面
            var 门格 = 开楼门(随机, 街道, 占地.Value, 列, 行);
            楼.入口列 = 门格?.列 ?? -1; 楼.入口行 = 门格?.行 ?? -1;
            if (门格 != null)
            {
                var 门单格 = 街道.取(门格.Value.列, 门格.Value.行);
                if (门单格 != null) 门单格.标识 = 楼.标识;   // 门挂上楼标识：点它 = 进这栋楼
            }
            片区.建筑.Add(楼);
            序号++;
        }

        // ④ 街道搜索点（容器实例 id 确定性派生；容器定义来自 街道地图类型）
        var 散落容器 = 街道容器池(随机, 取地图类型?.Invoke(模板.街道地图类型));
        int 搜索点数 = Math.Max(0, 模板.搜索点数量);
        for (int i = 0; i < 搜索点数 && 散落容器.Count > 0; i++)
        {
            var 落点 = 随机街道格(随机, 街道);
            if (落点 == null) break;
            string 容器 = 散落容器[随机.Next(散落容器.Count)];
            街道.设对象(落点.Value.列, 落点.Value.行, 沙盒对象.搜索点,
                $"{模板.标识}#街#{落点.Value.列}_{落点.Value.行}#{容器}", 容器);
        }

        // ⑤ 障碍（掩体：不可通行但不阻挡视线）
        for (int i = 0; i < Math.Max(0, 模板.障碍数量); i++)
        {
            var 落点 = 随机街道格(随机, 街道);
            if (落点 != null) 街道.设(落点.Value.列, 落点.Value.行, 沙盒地形.障碍);
        }

        // ⑥ 敌人（街道遭遇）
        if (!string.IsNullOrEmpty(模板.敌人池))
        {
            int 敌数 = 沙盒种子.范围(随机, Math.Max(0, 模板.敌人数量最小), Math.Max(0, 模板.敌人数量最大));
            for (int i = 0; i < 敌数; i++)
            {
                var 落点 = 随机街道格(随机, 街道);
                if (落点 != null) 街道.设对象(落点.Value.列, 落点.Value.行, 沙盒对象.敌人, 模板.敌人池);
            }
        }

        // ⑦ 校验①（结构）：出生点出发，所有对象格（含楼门）必须可达；出口必须存在
        var 可达 = 沙盒格工具.可达集(街道, 街道.出生列, 街道.出生行);
        foreach (var (c, r, 格) in 街道.全部())
        {
            if (格 == null) continue;
            if (!格.有对象 && !格.是门) continue;
            if (格.对象 == 沙盒对象.敌人) continue;   // 敌人不要求可达（可能站在障碍后面，走到旁边即可遭遇）
            if (!沙盒格工具.可达(街道, 可达, c, r)) return null;
        }

        // ⑧ 校验②（家具不得堵路）：街道家具占格后，楼门/出口仍要到得了，且每件家具旁边有落脚点；
        // 不满足就撤掉最后摆的一件家具（有界循环，确定性）
        for (int 撤销 = 0; 撤销 <= Math.Max(0, 模板.搜索点数量); 撤销++)
        {
            if (街道布局可行(街道)) break;
            var 撤点 = 找最后一件家具(街道);
            if (撤点 == null) break;
            var 撤格 = 街道.取(撤点.Value.列, 撤点.Value.行);
            if (撤格 == null) break;
            撤格.对象 = 沙盒对象.无; 撤格.标识 = null; 撤格.附加 = null;
        }
        if (!街道布局可行(街道)) return null;
        return 片区;
    }

    // 含家具阻挡的可行性：楼门（门）/出口 可达，且每件家具旁边有可站立落脚点
    private static bool 街道布局可行(沙盒层数据 街道)
    {
        var 含占位 = 沙盒格工具.可达集含占位(街道, 街道.出生列, 街道.出生行);
        foreach (var (列, 行, 格) in 街道.全部())
        {
            if (格 == null) continue;
            if (沙盒格工具.占位物(格))
            {
                if (!沙盒格工具.有相邻落脚点(街道, 列, 行, 含占位)) return false;
                continue;
            }
            if (格.是门 || 格.地形 == 沙盒地形.出口)
                if (!沙盒格工具.可达(街道, 含占位, 列, 行)) return false;
        }
        return true;
    }

    private static (int 列, int 行)? 找最后一件家具(沙盒层数据 层)
    {
        (int 列, int 行)? 结果 = null;
        foreach (var (列, 行, 格) in 层.全部())
            if (格 != null && 沙盒格工具.占位物(格)) 结果 = (列, 行);
        return 结果;
    }

    // ===== 建筑清单与槽位 =====

    // 抽建筑清单：中心建筑 1 栋 + 配套池按权重/数量区间抽；总数受 建筑数量区间 约束
    private static List<建筑模板> 抽建筑清单(Random 随机, 地区模板 模板, Func<string, 建筑模板> 取建筑模板)
    {
        var 清单 = new List<建筑模板>();
        if (取建筑模板 == null) return 清单;
        if (!string.IsNullOrEmpty(模板.中心建筑))
        {
            var 中心 = 取建筑模板(模板.中心建筑);
            if (中心 != null) 清单.Add(中心);
        }
        int 目标总数 = 沙盒种子.范围(随机, Math.Max(0, 模板.建筑数量最小), Math.Max(0, 模板.建筑数量最大));
        var 池 = new List<建筑配套>();
        var 权重 = new List<int>();
        if (模板.配套建筑池 != null)
            foreach (var 配 in 模板.配套建筑池)
                if (配 != null && !string.IsNullOrEmpty(配.建筑)) { 池.Add(配); 权重.Add(配.权重); }
        for (int i = 0; i < 目标总数 && 池.Count > 0; i++)
        {
            int 命中 = 沙盒种子.轮盘(随机, 权重);
            if (命中 < 0) break;
            var 配 = 池[命中];
            int 已有 = 清单.FindAll(t => t != null && t.标识 == 配.建筑).Count;
            int 上限 = Math.Max(1, 配.数量最大);
            if (已有 >= 上限) { 权重[命中] = 0; continue; }
            var t2 = 取建筑模板(配.建筑);
            if (t2 != null) 清单.Add(t2);
        }
        // 总数超上限 → 截断（保留中心建筑）
        int 总上限 = Math.Max(1, 模板.建筑数量最大) + (string.IsNullOrEmpty(模板.中心建筑) ? 0 : 1);
        if (清单.Count > 总上限) 清单.RemoveRange(总上限, 清单.Count - 总上限);
        return 清单;
    }

    // 槽位：把内部区域均分成 N 个候选位（每格 5×4 上下），供建筑摆放
    private static List<(int 列, int 行, int 宽, int 高)> 划槽位(int 列, int 行, int 数量)
    {
        var 结果 = new List<(int, int, int, int)>();
        int 内列 = 列 - 2, 内行 = 行 - 2;
        int 槽列数 = Math.Max(1, Math.Min(3, 内列 / 5));
        int 槽行数 = Math.Max(1, Math.Min(3, 内行 / 4));
        int 槽宽 = Math.Max(3, 内列 / 槽列数), 槽高 = Math.Max(3, 内行 / 槽行数);
        for (int sr = 0; sr < 槽行数; sr++)
            for (int sc = 0; sc < 槽列数; sc++)
            {
                int 起列 = 1 + sc * 槽宽, 起行 = 1 + sr * 槽高;
                int 宽 = Math.Min(槽宽, 列 - 1 - 起列), 高 = Math.Min(槽高, 行 - 1 - 起行);
                if (宽 < 3 || 高 < 3) continue;
                结果.Add((起列, 起行, 宽, 高));
            }
        if (结果.Count == 0) 结果.Add((1, 1, Math.Max(3, 内列), Math.Max(3, 内行)));
        // 建筑数多于槽位时循环复用（重复槽位里放不下第二栋楼时，该栋自然跳过）
        int 原有 = 结果.Count;
        for (int i = 0; 结果.Count < 数量; i++) 结果.Add(结果[i % 原有]);
        return 结果;
    }

    // 在槽内放一个 2~3 格的门面块（与槽边留 1 格）
    private static (int 列, int 行, int 宽, int 高)? 放占地(Random 随机, 沙盒层数据 街道, (int 列, int 行, int 宽, int 高) 槽)
    {
        for (int 尝试 = 0; 尝试 < 12; 尝试++)
        {
            int 宽 = 沙盒种子.范围(随机, 2, 3), 高 = 沙盒种子.范围(随机, 2, 3);
            int 最大列 = 槽.列 + 槽.宽 - 宽 - 1, 最大行 = 槽.行 + 槽.高 - 高 - 1;
            if (最大列 < 槽.列 + 1 || 最大行 < 槽.行 + 1) continue;
            int 列 = 沙盒种子.范围(随机, 槽.列 + 1, 最大列);
            int 行 = 沙盒种子.范围(随机, 槽.行 + 1, 最大行);
            // 与已有建筑体/对象保持 1 格距离，且四周不能压到边界墙
            bool 空 = true;
            for (int c = 列 - 1; c <= 列 + 宽 && 空; c++)
                for (int r = 行 - 1; r <= 行 + 高 && 空; r++)
                {
                    var 单格 = 街道.取(c, r);
                    if (单格 == null || 单格.地形 != 沙盒地形.街道 || 单格.有对象) 空 = false;
                }
            if (空) return (列, 行, 宽, 高);
        }
        return null;
    }

    // 在建筑门面上开一扇真门（= 该楼的出入口，也是一个 沙盒对象.建筑入口）：
    // 取门面上「朝街」的一格改成 地形=门 + 对象=建筑入口；关着，玩家点它进屋。
    private static (int 列, int 行)? 开楼门(Random 随机, 沙盒层数据 街道, (int 列, int 行, int 宽, int 高) 占地, int 列数, int 行数)
    {
        // 候选：门面四边里，外侧邻格是空街道的那些格（朝街优先）
        var 候选 = new List<(int 列, int 行)>();
        for (int c = 占地.列; c < 占地.列 + 占地.宽; c++)
        {
            var 上 = 街道.取(c, 占地.行 - 1);
            if (上 != null && 上.地形 == 沙盒地形.街道 && !上.有对象) 候选.Add((c, 占地.行));
            var 下 = 街道.取(c, 占地.行 + 占地.高);
            if (下 != null && 下.地形 == 沙盒地形.街道 && !下.有对象) 候选.Add((c, 占地.行 + 占地.高 - 1));
        }
        for (int r = 占地.行; r < 占地.行 + 占地.高; r++)
        {
            var 左 = 街道.取(占地.列 - 1, r);
            if (左 != null && 左.地形 == 沙盒地形.街道 && !左.有对象) 候选.Add((占地.列, r));
            var 右 = 街道.取(占地.列 + 占地.宽, r);
            if (右 != null && 右.地形 == 沙盒地形.街道 && !右.有对象) 候选.Add((占地.列 + 占地.宽 - 1, r));
        }
        if (候选.Count == 0) return null;
        var 门 = 候选[随机.Next(候选.Count)];
        var 单格 = 街道.取(门.列, 门.行);
        if (单格 == null) return null;
        单格.地形 = 沙盒地形.门; 单格.门开 = false;
        单格.对象 = 沙盒对象.建筑入口; 单格.标识 = null; 单格.附加 = null;   // 标识随后由调用方写入楼标识
        return 门;
    }

    // ===== 小工具 =====

    // 贴边界的街道格（出口用）
    private static (int 列, int 行)? 边界街道格(Random 随机, 沙盒层数据 层)
    {
        var 候选 = new List<(int 列, int 行)>();
        for (int c = 1; c < 层.列 - 1; c++)
        {
            if (层.取(c, 1)?.地形 == 沙盒地形.街道) 候选.Add((c, 1));
            if (层.取(c, 层.行 - 2)?.地形 == 沙盒地形.街道) 候选.Add((c, 层.行 - 2));
        }
        for (int r = 2; r < 层.行 - 2; r++)
        {
            if (层.取(1, r)?.地形 == 沙盒地形.街道) 候选.Add((1, r));
            if (层.取(层.列 - 2, r)?.地形 == 沙盒地形.街道) 候选.Add((层.列 - 2, r));
        }
        if (候选.Count == 0) return null;
        return 候选[随机.Next(候选.Count)];
    }

    // 随机一个空街道格（无对象）
    private static (int 列, int 行)? 随机街道格(Random 随机, 沙盒层数据 层)
    {
        var 候选 = new List<(int 列, int 行)>();
        foreach (var (c, r, 格) in 层.全部())
            if (格 != null && 格.地形 == 沙盒地形.街道 && !格.有对象) 候选.Add((c, r));
        if (候选.Count == 0) return null;
        return 候选[随机.Next(候选.Count)];
    }

    // 街道容器池：街道地图类型 下所有房间的容器标识（搜索点从这里抽）
    private static List<string> 街道容器池(Random 随机, 搜索地图类型 地图类型)
    {
        var 池 = new List<string>();
        if (地图类型?.房间 == null) return 池;
        foreach (var 房间 in 地图类型.房间)
        {
            if (房间?.容器 == null) continue;
            foreach (var 容器 in 房间.容器)
                if (容器 != null && !string.IsNullOrEmpty(容器.标识)) 池.Add(容器.标识);
        }
        return 池;
    }

    // 兜底：空场（围墙 + 出口 + 出生点）——保证功能可用，不做美化
    private static 片区数据 空场兜底(地区模板 模板, int 列, int 行)
    {
        var 街道 = new 沙盒层数据(列, 行) { 名称 = 模板.名称, 地图类型 = 模板.街道地图类型, 战斗棋盘 = 模板.战斗棋盘 };
        for (int c = 0; c < 列; c++)
            for (int r = 0; r < 行; r++)
                街道.设(c, r, (c == 0 || r == 0 || c == 列 - 1 || r == 行 - 1) ? 沙盒地形.建筑体 : 沙盒地形.街道);
        int 出列 = 1, 出行 = 1;
        街道.设(出列, 出行, 沙盒地形.出口);
        街道.出生列 = 出列; 街道.出生行 = 出行;
        return new 片区数据 { 地区标识 = 模板.标识, 名称 = 模板.名称, 危险度 = 模板.危险度, 街道 = 街道 };
    }
}
