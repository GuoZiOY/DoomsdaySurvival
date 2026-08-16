// ================= 功能接口 =================
// 设施逻辑 实现哪几个接口 = 拥有哪几个功能；功能面板按接口取数据（跨设施复用）

// 教学：学技能（武馆=物理 / 学堂=魔法，各自实现筛选）
public interface 教学功能 { 技能数据[] 可学技能(); bool 尝试学习(string 标识); }
// 买卖：买/卖（商店通用 / 武馆兵器 / 学堂魔法物，各自实现筛选）
public interface 买卖功能 { 物品数据[] 可购商品(); bool 尝试购买(string 标识); 物品堆叠[] 可卖物品(); bool 尝试卖出(string 标识); }
// 训练：属性训练 + 技能熟练训练（训练场/武馆/学堂共用）
public interface 训练功能
{
    训练项目[] 可训项目();
    bool 尝试训练(string 标识);
    技能数据[] 可练熟练技能();   // 已学未满级
    int 熟练训练花费(string 技能标识);
    int 每级熟练度(技能数据 技能);
    bool 尝试熟练训练(string 技能标识);
}
// 任务：接取任务（任务栏）
public interface 任务功能 { 任务数据[] 可接任务(); bool 尝试接取(string 标识); }
// 恢复：教堂
public interface 恢复功能 { string 恢复描述(); bool 尝试恢复(); }
// 睡觉：家
public interface 睡觉功能 { string 睡觉描述(); bool 尝试睡觉(); }

// ================= 设施逻辑基类 =================

// 设施逻辑基类：设施的功能系统（业务规则），完全不碰 UI；反馈走 EventBus 事件。
// 子类实现各自领域方法，由 设施工厂 按 facilities.json 的 逻辑类型 反射实例化。
// 注意：NPC/内部节点 属 设施定义（facilities.json 数据），面板直接读 DataService.设施，不在逻辑里重复。
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
