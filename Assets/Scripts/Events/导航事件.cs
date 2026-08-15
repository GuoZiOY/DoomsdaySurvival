// UI 导航事件：逻辑层只发事件，UI 管理器订阅后切换/填充面板（保持逻辑不碰 UI）

// 打开设施面板：标识 + 返回节点
public readonly struct 打开设施事件
{
    public readonly string 设施标识;
    public readonly string 返回节点;
    public 打开设施事件(string 设施标识, string 返回节点) { this.设施标识 = 设施标识; this.返回节点 = 返回节点; }
}

// 打开战斗面板
public readonly struct 打开战斗事件
{
    public readonly string 返回节点;
    public 打开战斗事件(string 返回节点) { this.返回节点 = 返回节点; }
}

// 打开探索面板
public readonly struct 打开探索事件
{
    public readonly string 返回节点;
    public 打开探索事件(string 返回节点) { this.返回节点 = 返回节点; }
}

// 打开结局面板
public readonly struct 打开结局事件 { }

// 打开主菜单面板
public readonly struct 打开主菜单事件 { }
