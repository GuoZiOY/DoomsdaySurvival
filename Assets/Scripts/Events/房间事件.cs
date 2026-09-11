using System;

    // ================= 房间层事件 =================
    // 分工：房间探索服务（Services）发布事件 → 房间面板/房间网格面板（UI）订阅。
    // 注意：房间**不再有自己的信息条与提示行** ——
    //   ① 房间名/危险度/已搜/敌 走 地图位置事件 → 常驻 HUD 的「地点」位显示；
    //   ② 房间里的提示走 日志事件（探索类）→ 左下角日志播报。

    // 打开房间：由 快速测试面板 / 上层（区域·建筑层，后续）触发 → 面板管理器 路由到 房间面板
    public readonly struct 打开房间事件
    {
        public readonly string 模板标识;
        public readonly int 种子;
        public 打开房间事件(string 模板标识, int 种子)
        {
            this.模板标识 = 模板标识;
            this.种子 = 种子;
        }
    }

    // 离开房间：走大门出去 / 上层要求退出 → 面板管理器 回上一个面板（**服务本身不碰 UI**）
    public readonly struct 离开房间事件 { }

    // 回到房间：房间里的战斗结算完（胜利 / 逃跑）→ 面板管理器 把面板切回**房间面板**。
    // 为什么不能靠 房间显示事件：那个每走一步都发（还发在"打开容器搜索面板"之后），拿它切面板会把搜索面板顶掉。
    public readonly struct 回到房间事件 { }

    // 房间显示刷新：面板据此重画网格（实体/迷雾/路径）；其余细节面板直接读服务
    public readonly struct 房间显示事件
    {
        public readonly string 信息条;   // 调试用摘要（展示已由 HUD / 日志各自接管）
        public readonly int 玩家列;
        public readonly int 玩家行;
        public 房间显示事件(string 信息条, int 玩家列, int 玩家行)
        {
            this.信息条 = 信息条;
            this.玩家列 = 玩家列;
            this.玩家行 = 玩家行;
        }
    }
