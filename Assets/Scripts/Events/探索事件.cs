    // 探索选项数据：纯数据载体（不含委托），面板按动作路由到服务方法。
    // 动作："搜索" / "深入" / "返回" / "岔路:安全" / "岔路:危险" / "战斗" / "逃跑" / "选择:索引"
    public readonly struct 探索选项数据
    {
        public readonly string 文本;
        public readonly string 动作;
        public 探索选项数据(string 文本, string 动作) { this.文本 = 文本; this.动作 = 动作; }
    }

    // 探索显示事件：探索服务 → 探索面板（信息条 + 正文 + 动作集）
    public readonly struct 探索显示事件
    {
        public readonly string 信息条;   // 区域名 · 危险度    层 X/N
        public readonly string 正文;     // 层描述 / 事件文本
        public readonly 探索选项数据[] 选项;
        public 探索显示事件(string 信息条, string 正文, 探索选项数据[] 选项)
        { this.信息条 = 信息条; this.正文 = 正文; this.选项 = 选项; }
    }
