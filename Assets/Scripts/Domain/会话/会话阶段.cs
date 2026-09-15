// 会话阶段（共用枚举 —— 按项目约定「共用枚举归最底层」，放 Domain，不放 Services）。
//
// 为什么放最底层：`Events/导航事件.cs` 的 `会话变化事件` 要带它，`Services/会话/游戏会话.cs` 要持它，
//   UI 要读它。放在 Services 会让 `Events → Services` 成为一条**层次违规**
//   （`Tools/分层核对.ps1` 当场报了出来）。放 Domain 之后：Events → Domain、Services → Domain、UI → Domain
//   全是合法的向下依赖。
//
// 四个值 = 只留有消费者的。四个值的含义与"谁写"见 `Services/会话/游戏会话.cs` 的文件头。
public enum 会话阶段
{
    主菜单,      // 标题界面（含"还没建角色"）
    角色创建,    // 开局构筑
    探索,        // 安全屋 / 大世界 / 区域 / 建筑 / 房间 —— 所有"在游戏里但不打架"的时刻
    战斗,        // 战斗沙盒面板在前台
}
