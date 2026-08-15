// 设施逻辑基类：设施的功能系统（业务规则），完全不碰 UI；反馈走 EventBus 事件。
// 子类实现各自领域方法，由 设施工厂 按 facilities.json 的 逻辑类型 反射实例化。
public abstract class 设施逻辑
{
    public string 标识 { get; private set; }
    public string 名称 { get; private set; }
    public 玩家档案 当前玩家 => 玩家;   // 面板读属性用
    protected 玩家档案 玩家;
    protected DataService 数据;
    protected EventBus 事件;

    // 工厂创建后调用：注入环境
    public void 装配(string 标识, string 名称, 玩家档案 玩家, DataService 数据, EventBus 事件)
    {
        this.标识 = 标识;
        this.名称 = 名称;
        this.玩家 = 玩家;
        this.数据 = 数据;
        this.事件 = 事件;
    }
}

// 设施打开上下文：路由器把 逻辑 + 返回节点 一起传给设施面板
public readonly struct 设施打开上下文
{
    public readonly 设施逻辑 逻辑;
    public readonly string 返回节点;
    public 设施打开上下文(设施逻辑 逻辑, string 返回节点) { this.逻辑 = 逻辑; this.返回节点 = 返回节点; }
}
