    // 探索按钮数据：探索面板固定三按钮（探索/是/否）之一；文本+动作（空=该按钮隐藏）
    // 动作："搜索" / "战斗" / "逃跑" / "偷袭" / "绕开" / "强行突破" / "悄悄绕行" / "资源:是" / "资源:否" / "岔路:安全" / "岔路:危险" / "选择:索引"
    public readonly struct 探索按钮数据
    {
        public readonly string 文本;
        public readonly string 动作;
        public 探索按钮数据(string 文本, string 动作) { this.文本 = 文本; this.动作 = 动作; }
        public bool 有效 => !string.IsNullOrEmpty(文本) && !string.IsNullOrEmpty(动作);
    }

    // 探索显示事件：探索服务 → 探索面板（信息条 + 正文 + 固定三按钮）
    // 按钮功能固定：探索=搜索（仅可搜索情景显示）；是=肯定类（战斗/偷袭/强行突破/安全/资源是/选项1）；
    //   否=否定类（逃跑/绕开/悄悄绕行/危险/资源否/选项2）。文本随情景填充；返回统一走侧边栏取消。
    public readonly struct 探索显示事件
    {
        public readonly string 信息条;   // 区域名 · 危险度    层 X/N    精力
        public readonly string 正文;     // 层描述 / 事件文本
        public readonly 探索按钮数据 探索;
        public readonly 探索按钮数据 是;
        public readonly 探索按钮数据 否;
        public 探索显示事件(string 信息条, string 正文, 探索按钮数据 探索, 探索按钮数据 是, 探索按钮数据 否)
        { this.信息条 = 信息条; this.正文 = 正文; this.探索 = 探索; this.是 = 是; this.否 = 否; }
    }
