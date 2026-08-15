    // 探索选项数据：纯数据载体（不含委托），控制器按动作回调服务
    public readonly struct 探索选项数据
    {
        public readonly string 文本;
        public readonly string 动作;   // "深入" / "返回" / "战斗"
        public 探索选项数据(string 文本, string 动作) { this.文本 = 文本; this.动作 = 动作; }
    }

    // 探索显示事件：探索服务 → 探索控制器
    public readonly struct 探索显示事件
    {
        public readonly string 文本;
        public readonly 探索选项数据[] 选项;
        public 探索显示事件(string 文本, 探索选项数据[] 选项) { this.文本 = 文本; this.选项 = 选项; }
    }
