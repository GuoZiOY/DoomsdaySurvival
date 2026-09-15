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

// 回到区域：区域层（街上）的遭遇战结算完（胜利/逃跑）发布 → 面板管理器 把面板切回区域面板。
// 对位 房间事件.回到房间事件 / 大世界事件.回到大世界事件 —— 没有它的话战斗面板会留在最上面，
// 打完街上那一只回不到街区（v51 刀12 街上遭遇落地时才需要它）。
public readonly struct 回到区域事件 { }

// 离开区域：走出这一片（回大世界）。现在有两个来源：
//   ① 面板自己的出口按钮（面板层直接调 区域探索服务.离开()）；
//   ② **走到街口那一格**（区域生成器.出口格）—— v51 刀12 起这才是主要出路。
public readonly struct 离开区域事件 { }
