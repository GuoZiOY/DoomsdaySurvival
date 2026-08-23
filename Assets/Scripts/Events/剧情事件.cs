    // 剧情选项数据：纯数据载体（不含委托），UI 渲染后用目标回调服务
    public readonly struct 剧情选项数据
    {
        public readonly string 文本;
        public readonly string 目标;
        public 剧情选项数据(string 文本, string 目标) { this.文本 = 文本; this.目标 = 目标; }
    }

    // 显示剧情事件：对话引擎 → StoryController
    public readonly struct 显示剧情事件
    {
        public readonly string 文本;
        public readonly 剧情选项数据[] 选项;
        public readonly string 自动目标;   // 非空且选项为空：文本显示完后自动进入（剧情链连续播放）
        public 显示剧情事件(string 文本, 剧情选项数据[] 选项, string 自动目标 = "")
        { this.文本 = 文本; this.选项 = 选项; this.自动目标 = 自动目标; }
    }
