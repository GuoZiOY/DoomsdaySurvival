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

// 打开角色面板（全局，HUD 按钮触发；返回回到上个面板）
public readonly struct 打开角色面板事件 { }

// 打开节点内部图：任意节点（有 内部 数组）进入内部，携带节点 + 返回节点（回小地图用）
public readonly struct 打开节点内部事件
{
    public readonly 地图节点 节点;
    public readonly string 返回节点;
    public 打开节点内部事件(地图节点 节点, string 返回节点) { this.节点 = 节点; this.返回节点 = 返回节点; }
}

// 打开对话：进入剧情节点（内部 NPC 交谈用），携带来源节点与返回节点
public readonly struct 打开对话事件
{
    public readonly string 剧情节点;
    public readonly 地图节点 节点;    // 来源节点（返回内部用）
    public readonly string 返回节点;
    public 打开对话事件(string 剧情节点, 地图节点 节点, string 返回节点) { this.剧情节点 = 剧情节点; this.节点 = 节点; this.返回节点 = 返回节点; }
}

// 打开功能面板：内部功能物节点触发，设施逻辑 + 功能标识 + 来源节点 + 返回节点
public readonly struct 打开功能面板事件
{
    public readonly 设施逻辑 逻辑;
    public readonly string 功能标识;
    public readonly 地图节点 节点;    // 来源节点（返回内部用）
    public readonly string 返回节点;
    public 打开功能面板事件(设施逻辑 逻辑, string 功能标识, 地图节点 节点, string 返回节点)
    { this.逻辑 = 逻辑; this.功能标识 = 功能标识; this.节点 = 节点; this.返回节点 = 返回节点; }
}

// —— 地图导航事件 ——

// 地图模式
public enum 地图模式 { 大地图, 城镇, 野外 }

// 打开大地图面板
public readonly struct 打开大地图事件 { }

// 打开小地图面板：城镇标识 + 入口节点
public readonly struct 打开小地图事件
{
    public readonly string 城镇标识;
    public readonly string 入口节点;
    public 打开小地图事件(string 城镇标识, string 入口节点) { this.城镇标识 = 城镇标识; this.入口节点 = 入口节点; }
}

// 打开野外面板：区域标识 + 返回节点（回大地图用）
public readonly struct 打开野外面板事件
{
    public readonly string 区域标识;
    public readonly string 返回节点;
    public 打开野外面板事件(string 区域标识, string 返回节点) { this.区域标识 = 区域标识; this.返回节点 = 返回节点; }
}

// 地图位置变化：HUD 地点栏更新用
public readonly struct 地图位置事件
{
    public readonly 地图模式 所在模式;
    public readonly string 大节点;
    public readonly string 小节点;
    public 地图位置事件(地图模式 所在模式, string 大节点, string 小节点) { this.所在模式 = 所在模式; this.大节点 = 大节点; this.小节点 = 小节点; }
}
