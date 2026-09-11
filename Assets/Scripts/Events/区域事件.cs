// 区域层导航事件：区域探索服务（Services）发布 → 区域面板/区域网格面板（UI）订阅。
// 分工与 房间事件.cs 一一对位（房间层与区域层是同一套"格子探索"外壳的两个实例）。

// 打开区域：服务生成完一片区域后发布 → 面板管理器 路由到 区域面板
public readonly struct 打开区域事件
{
    public readonly string 区域标识;
    public readonly int 种子;
    public 打开区域事件(string 区域标识, int 种子) { this.区域标识 = 区域标识; this.种子 = 种子; }
}

// 区域显示：区域探索服务 → 区域面板（信息条 + 玩家格；面板据此刷新网格与图层）
public readonly struct 区域显示事件
{
    public readonly string 信息条;
    public readonly int 玩家列;
    public readonly int 玩家行;
    public 区域显示事件(string 信息条, int 玩家列, int 玩家行)
    { this.信息条 = 信息条; this.玩家列 = 玩家列; this.玩家行 = 玩家行; }
}

// 离开区域：走出这一片（回大地图）。第 1 刀只有 Esc/侧边栏取消会发它；
// 以后加了"区域边缘的出口格"，走上去就发这个。
public readonly struct 离开区域事件 { }
