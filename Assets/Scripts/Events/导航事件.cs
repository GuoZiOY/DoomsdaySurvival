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

// 打开功能面板：设施逻辑 + 功能标识（原「内部功能物节点触发」那一层随 城镇小地图/节点内部 一起拆掉）
public readonly struct 打开功能面板事件
{
    public readonly 设施逻辑 逻辑;
    public readonly string 功能标识;
    public 打开功能面板事件(设施逻辑 逻辑, string 功能标识)
    { this.逻辑 = 逻辑; this.功能标识 = 功能标识; }
}

// —— 地图导航事件 ——

// 地图模式：末日只有两种——在大世界节点图上，或在某个荒野（区域网格）里
public enum 地图模式 { 大地图, 野外 }

// 打开大地图面板（节点图）
public readonly struct 打开大地图事件 { }

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
    public 地图位置事件(地图模式 所在模式, string 大节点) { this.所在模式 = 所在模式; this.大节点 = 大节点; }
}

// 面板切换：面板管理器 显示面板后发布（侧边栏等常驻 UI 据此刷新按钮显隐/状态）
public readonly struct 面板切换事件
{
    public readonly 面板基类 目标;
    public 面板切换事件(面板基类 目标) { this.目标 = 目标; }
}
