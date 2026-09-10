using System;
using System.Collections.Generic;

// 沙盒数据结构：区域层 / 楼层层 共用的「网格 + 对象」数据（纯 C#，禁 UnityEngine）。
// 分工（评审定案）：
//   · 结构（本文件）＝ 地形/对象位置/实例 id，全部由种子确定性展开，不含任何玩家状态；
//   · 状态（探索沙盒服务）＝ 迷雾、当前格、搜索次数 等每局刷新的东西，撤离即弃。
public enum 沙盒地形
{
    墙,       // 不可通行、阻挡视线（外墙/房间隔墙）
    地板,     // 可通行（室内）
    街道,     // 可通行（室外）
    建筑体,   // 不可通行（建筑占位）；入口格单独用 建筑入口 对象标记
    出口,     // 可通行 + 撤离点（也是进入地区时的出生格）
    障碍,     // 不可通行（车残骸/塌方），但【不阻挡视线】——能看到对面，构成掩体感
    门,       // 真实门：关着时 不可通行 + 阻挡视线；开着时 可通行 + 不挡视线（由 沙盒格.门开 决定）
}

public enum 沙盒对象
{
    无,
    搜索点,     // 标识 = 容器实例 id；附加 = 容器定义标识（搜索_地图类型 里的 搜索容器.标识）
    敌人,       // 标识 = 敌人组标识
    尸体,       // 标识 = 搜索服务 的尸体容器 id
    建筑入口,   // 标识 = 建筑标识（点它进楼；在楼里点同一扇门 = 出楼）
    楼梯,       // 标识 = 楼梯组（同组的两层可上下）；点它上/下楼
    陷阱,       // 标识 = 陷阱定义标识（P3 用；v0 占位）
}

// 单格：地形 + 格上对象（一格里最多一个对象——够用且好理解）
public sealed class 沙盒格
{
    public 沙盒地形 地形 = 沙盒地形.墙;
    public 沙盒对象 对象 = 沙盒对象.无;
    public string 标识;    // 对象主键（实例 id / 敌人组 / 建筑标识 / 尸体容器 id）
    public string 附加;    // 对象副信息（容器定义标识 / 楼梯组 等）
    public bool 门开;      // 地形=门 时：门是否开着（关着 = 不可通行 + 挡视线）

    public 沙盒格() { }
    public 沙盒格(沙盒地形 地形) { this.地形 = 地形; }
    public 沙盒格(沙盒地形 地形, 沙盒对象 对象, string 标识 = null, string 附加 = null)
    { this.地形 = 地形; this.对象 = 对象; this.标识 = 标识; this.附加 = 附加; }

    public bool 有对象 => 对象 != 沙盒对象.无;
    public bool 是门 => 地形 == 沙盒地形.门;
}

// 一层网格（区域层 与 楼层层 共用）：格数组 + 尺寸 + 出生点
public sealed class 沙盒层数据
{
    public string 名称;          // "街道" / "1F" / "地下室"
    public string 地图类型;       // 搜索_地图类型 标识（房间→容器 的来源）
    public string 战斗棋盘;       // 本层遭遇战使用的 战斗棋盘.标识（室内/街头/工厂…）
    public int 列, 行;
    public 沙盒格[] 格;
    public int 出生列 = -1, 出生行 = -1;   // 进入本层时的落脚点（-1 = 未设）
    public int 附加入口边 = -1;            // 1F 门口朝向（0上 1下 2左 3右；-1 未定）——供区域层对齐建筑入口

    public 沙盒层数据() { }
    public 沙盒层数据(int 列, int 行)
    {
        this.列 = Math.Max(1, 列); this.行 = Math.Max(1, 行);
        格 = new 沙盒格[this.列 * this.行];
        for (int i = 0; i < 格.Length; i++) 格[i] = new 沙盒格();
    }

    public bool 在界内(int 列, int 行) => 列 >= 0 && 行 >= 0 && 列 < this.列 && 行 < this.行;

    public 沙盒格 取(int 列, int 行)
        => 在界内(列, 行) ? 格[行 * this.列 + 列] : null;

    public void 设(int 列, int 行, 沙盒地形 地形)
    {
        var 单格 = 取(列, 行);
        if (单格 != null) 单格.地形 = 地形;
    }

    public void 设对象(int 列, int 行, 沙盒对象 对象, string 标识 = null, string 附加 = null)
    {
        var 单格 = 取(列, 行);
        if (单格 == null) return;
        单格.对象 = 对象; 单格.标识 = 标识; 单格.附加 = 附加;
    }

    // 遍历（生成/校验用）
    public IEnumerable<(int 列, int 行, 沙盒格 格)> 全部()
    {
        for (int 行 = 0; 行 < this.行; 行++)
            for (int 列 = 0; 列 < this.列; 列++)
                yield return (列, 行, 取(列, 行));
    }
}

// 一栋楼：楼层列表 + 入口（入口在 1F 的某格，位于区域层街道上）
public sealed class 建筑数据
{
    public string 标识;         // 片区内的唯一标识（实例 id 前缀）
    public string 模板;         // 建筑模板.标识
    public string 名称;
    public string 地图类型;      // 搜索_地图类型 标识（房间→容器）
    public string 战斗棋盘;      // 室内遭遇用的棋盘
    public int 入口列, 入口行;    // 在区域层（街道）上的入口格 —— 由区域生成器填
    public int 入口边 = -1;      // 门口朝向（0上 1下 2左 3右；-1 未定）
    public int 内入口列 = -1, 内入口行 = -1;   // 1F 内的门口格（进入该楼时的落脚点）
    public List<沙盒层数据> 楼层 = new List<沙盒层数据>();

    public 沙盒层数据 取楼层(int 索引) => 索引 >= 0 && 索引 < 楼层.Count ? 楼层[索引] : null;
}

// 一个地区：街道网格 + 建筑列表（v0 全量生成；后续可换按需生成 + 导航栈）
public sealed class 片区数据
{
    public string 地区标识;
    public string 名称;
    public int 危险度 = 1;
    public 沙盒层数据 街道;      // 区域层网格（含 出口/建筑入口/搜索点/敌人）
    public List<建筑数据> 建筑 = new List<建筑数据>();

    public 建筑数据 找建筑(string 标识)
        => 建筑.Find(楼 => 楼 != null && 楼.标识 == 标识);
}

// 地形/可达 工具（生成器与沙盒服务共用）
// 两层语义，别混：
//   · 结构态（可通行/阻挡视线）——只按地形算，门算「可通行」：生成校验/寻路可行性用它（门总可以被推开）；
//   · 当前态（当前可通行/当前阻挡视线）——把 门开 状态算进去：移动与视野用它。
public static class 沙盒格工具
{
    public static bool 可通行(沙盒地形 地形) => 地形 != 沙盒地形.墙 && 地形 != 沙盒地形.建筑体 && 地形 != 沙盒地形.障碍;

    public static bool 阻挡视线(沙盒地形 地形) => 地形 == 沙盒地形.墙 || 地形 == 沙盒地形.建筑体;

    // 占位物（家具/尸体）：占住这一格，人走不进去——点它 = 搜刮（不是踩上去）
    public static bool 占位物(沙盒格 格)
        => 格 != null && (格.对象 == 沙盒对象.搜索点 || 格.对象 == 沙盒对象.尸体);

    // 当前可通行（门关着过不去；家具/尸体占格也过不去）
    public static bool 当前可通行(沙盒格 格)
        => 格 != null && 可通行(格.地形) && !占位物(格) && (格.地形 != 沙盒地形.门 || 格.门开);

    // 当前是否挡视线（门关着 = 挡住，看不见门后）
    public static bool 当前阻挡视线(沙盒格 格)
        => 格 != null && (阻挡视线(格.地形) || (格.地形 == 沙盒地形.门 && !格.门开));

    // 四邻（上下左右）
    public static readonly (int 列, int 行)[] 四方向 = { (0, 1), (0, -1), (1, 0), (-1, 0) };

    // 可达集（BFS，按结构态走：门视为可通行——生成校验关心的是「理论上能到」，不是「此刻门开没开」）
    public static bool[] 可达集(沙盒层数据 层, int 起列, int 起行)
    {
        var 结果 = new bool[层.列 * 层.行];
        if (层.取(起列, 起行) == null || !可通行(层.取(起列, 起行).地形)) return 结果;
        var 队列 = new Queue<(int 列, int 行)>();
        队列.Enqueue((起列, 起行));
        结果[起行 * 层.列 + 起列] = true;
        while (队列.Count > 0)
        {
            var (列, 行) = 队列.Dequeue();
            foreach (var (偏列, 偏行) in 四方向)
            {
                int 新列 = 列 + 偏列, 新行 = 行 + 偏行;
                if (!层.在界内(新列, 新行)) continue;
                int 索引 = 新行 * 层.列 + 新列;
                if (结果[索引]) continue;
                var 单格 = 层.取(新列, 新行);
                if (单格 == null || !可通行(单格.地形)) continue;
                结果[索引] = true;
                队列.Enqueue((新列, 新行));
            }
        }
        return 结果;
    }

    // 起点出发是否可达目标格（校验「容器/敌人/楼梯 不会被墙围死」）
    public static bool 可达(沙盒层数据 层, bool[] 可达集, int 列, int 行)
        => 层.在界内(列, 行) && 可达集[行 * 层.列 + 列];

    // 可达集（把「家具/尸体占格」也算作阻挡，但门仍视为可通行——门总能推开）：
    // 校验「摆完家具后，门/楼梯/楼门 还走得到、每件家具旁边都有落脚点」——生成器据此回退家具，避免堵死。
    public static bool[] 可达集含占位(沙盒层数据 层, int 起列, int 起行)
    {
        var 结果 = new bool[层.列 * 层.行];
        var 起 = 层.取(起列, 起行);
        if (起 == null || !可通行(起.地形) || 占位物(起)) return 结果;
        var 队列 = new Queue<(int 列, int 行)>();
        队列.Enqueue((起列, 起行));
        结果[起行 * 层.列 + 起列] = true;
        while (队列.Count > 0)
        {
            var (列, 行) = 队列.Dequeue();
            foreach (var (偏列, 偏行) in 四方向)
            {
                int 新列 = 列 + 偏列, 新行 = 行 + 偏行;
                if (!层.在界内(新列, 新行)) continue;
                int 索引 = 新行 * 层.列 + 新列;
                if (结果[索引]) continue;
                var 单格 = 层.取(新列, 新行);
                if (单格 == null || !可通行(单格.地形) || 占位物(单格)) continue;
                结果[索引] = true;
                队列.Enqueue((新列, 新行));
            }
        }
        return 结果;
    }

    // 该格四邻里有没有「落得下脚」的格（家具/尸体 的搜刮判定用：旁边总得站得住人）
    public static bool 有相邻落脚点(沙盒层数据 层, int 列, int 行, bool[] 可达集 = null)
    {
        foreach (var (偏列, 偏行) in 四方向)
        {
            int 邻列 = 列 + 偏列, 邻行 = 行 + 偏行;
            var 邻格 = 层.取(邻列, 邻行);
            if (邻格 == null || !可通行(邻格.地形) || 占位物(邻格)) continue;
            if (可达集 == null || 可达(层, 可达集, 邻列, 邻行)) return true;
        }
        return false;
    }
}
