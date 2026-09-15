// 只为"离线编译 玩家档案 的依赖闭包"而存在的最小桩（**不参与任何断言**）。
//
// 为什么需要它：领域层里有**已知的越层引用** ——
//   · `Domain/玩家/生存管理器.cs:79`（睡觉 → `世界时间管理器.同步整点基准()` + `ServiceRegistry.Get`）
//   · `Domain/容器/容器服务.cs`（读 `DataService`）
// 这些是项目早就记录在案的层次问题，修它不属于刀64。**本验证器一次都不会走到那些分支**：
// 它只做"构造 玩家档案 / 改字段 / 比指纹 / 算整点结算"，从不调用 睡觉 / 打开容器。
//
// ★ 诚实的边界声明（写在这里免得日后被误读）：
//   本文件的桩**不是被测对象**，也没有任何断言依赖它们。真正被断言的东西
//   （`玩家档案` / `存档模型` / `整点结算`）**零桩**，是从 Assets 原样链接进来的源码。
//   若哪天有人把断言写到依赖桩的路径上，那就是"验的是副本" —— 那种改动必须被拒绝。
public static class ServiceRegistry
{
    public static bool 已注册<T>() => false;
    public static T Get<T>() => default(T);
    public static void Register<T>(T 服务) { }
    public static void 清除() { }
}

// 桩：`生存管理器.睡觉()` 会用到的两个成员（本验证器不会调 睡觉）
public sealed class 世界时间管理器
{
    public float 整点基准 => 0f;
    public void 同步整点基准() { }
    public void 读档后对齐(float 存档基准分钟) { }
    public void 推进(float 现实秒) { }
    public void 跳时(float 游戏分钟) { }
}
