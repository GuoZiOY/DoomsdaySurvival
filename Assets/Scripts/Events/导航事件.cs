// UI 导航事件：逻辑层只发事件，UI 管理器订阅后切换/填充面板（保持逻辑不碰 UI）
// 注：原有的 `打开设施事件`（设施标识 + 返回节点）与 `打开功能面板事件`（设施逻辑 + 功能标识）
//     随 v51 刀7e 的**设施子系统整体退役**一起删 —— 前者 0 订阅、后者从未被发布。

// 打开战斗面板
public readonly struct 打开战斗事件
{
    public readonly string 返回节点;
    public 打开战斗事件(string 返回节点) { this.返回节点 = 返回节点; }
}

// 打开结局面板
public readonly struct 打开结局事件 { }

// 打开主菜单面板
public readonly struct 打开主菜单事件 { }

// 打开角色面板（全局，HUD 按钮触发；返回回到上个面板）
public readonly struct 打开角色面板事件 { }

// 打开营地面板（安全屋：返回营地/进入营地 时）
public readonly struct 打开营地事件 { }

// 打开任务面板（全局）——侧边栏「任务」按钮调出 系统任务面板（主线/支线/日常）
public readonly struct 打开任务面板事件 { }

// 打开对话：进入剧情节点（NPC 交谈用）
public readonly struct 打开对话事件
{
    public readonly string 剧情节点;
    public 打开对话事件(string 剧情节点) { this.剧情节点 = 剧情节点; }
}

// —— 地图导航事件 ——

// 地图模式：末日只有两种——在大世界（格子网格）上，或在某个区域副本（区域网格）里
public enum 地图模式 { 大地图, 野外 }

// 打开大地图：大世界探索服务 生成完那张 100×100 的格子世界后发布 → 面板管理器 路由到 大世界面板。
// v51 起带数据（原来是空结构体，且**一个订阅者都没有** → 主菜单"新游戏/继续"静默无反应；这次一并修掉）。
public readonly struct 打开大地图事件
{
    public readonly string 世界标识;
    public readonly int 种子;
    public 打开大地图事件(string 世界标识, int 种子) { this.世界标识 = 世界标识; this.种子 = 种子; }
}

// 地图位置变化：HUD 地点栏更新用
public readonly struct 地图位置事件
{
    public readonly 地图模式 所在模式;
    public readonly string 大节点;
    public 地图位置事件(地图模式 所在模式, string 大节点) { this.所在模式 = 所在模式; this.大节点 = 大节点; }
}

// 面板切换：面板管理器 显示面板后发布（侧边栏等常驻 UI 据此刷新按钮显隐/状态）
public readonly struct 面板切换事件
{
    public readonly 面板基类 目标;
    public 面板切换事件(面板基类 目标) { this.目标 = 目标; }
}

// 会话阶段变化：`面板管理器.显示()` 切到**另一个阶段**的界面后发布（见 游戏会话 的文件头）。
// 与 `面板切换事件` 的区别：那个是"换了哪个**面板**"（每次切都发）；这个是"换了哪个**阶段**"
//   （只在跨界时发 —— 大世界→区域→楼里 一路都是「探索」，不会发）。
// 谁订阅：需要"换阶段时做一次对齐"的系统（现在是 常驻UI 显隐；将来 输入路由 / 暂停策略 也读它）。
public readonly struct 会话变化事件
{
    public readonly 会话阶段 阶段;
    public 会话变化事件(会话阶段 阶段) { this.阶段 = 阶段; }
}

// 侧边栏开合：玩家用 HUD 的开关收起/呼出右侧功能栏时发布（只在**可见性真的变了**时发）。
// 谁订阅：会被侧边栏盖住的右区部件 → 整体左移一个栏宽"让位"（`持有面板` 的 装具区 / 仓库 / 搜索）。
// 为什么由侧边栏自己发、而不是让别人去问它：**可见性的主人只该有一个**，它变了就广播一次，
//   订阅方各自决定要不要让位（将来别的面板也能用，不用再改侧边栏）。
public readonly struct 侧边栏显隐变化事件
{
    public readonly bool 显示中;
    public 侧边栏显隐变化事件(bool 显示中) { this.显示中 = 显示中; }
}
