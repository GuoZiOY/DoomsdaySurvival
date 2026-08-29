using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 设施功能面板基类：功能面板通用骨架——标题、返回设施内部、打开上下文注入。
// 不再强制「内容区 + 创建行」的单一形态：子类在场景搭专属布局（Tab/分区/行模板），只实现 渲染内容() 填数据。
// 返回统一走 侧边栏取消按钮（回退 = 返回设施内部），面板内不再放置 返回按钮。
public abstract class 设施功能面板基类 : 面板基类
{
    [SerializeField] protected TMP_Text 标题;
    protected 设施逻辑 逻辑;       // 打开时由 设施打开上下文 注入
    protected 地图节点 节点;       // 来源节点（返回内部用）
    protected string 返回节点;

    protected virtual void Awake()
    {
        // 金币变化默认整面板刷新（买卖/训练等改金币后刷新）；子类可覆写 金币变化() 做轻量更新
        ServiceRegistry.Get<EventBus>()?.订阅<金币变化事件>(_ => 金币变化());
    }

    // 金币变化钩子：子类覆写可只更新余额等，避免整面板重绘（保持列表滚动位置）
    protected virtual void 金币变化() => 刷新(null);

    protected override void 刷新(object 上下文)
    {
        if (上下文 is 设施打开上下文 c) { 逻辑 = c.逻辑; 节点 = c.节点; 返回节点 = c.返回节点; }
        if (逻辑 == null) return;
        if (标题 != null) 标题.text = 标题文字();
        渲染内容();
    }

    protected abstract string 标题文字();
    protected abstract void 渲染内容();

    // 返回节点内部（三级导航）
    protected void 返回设施内部()
    {
        if (节点 == null) return;
        ServiceRegistry.Get<EventBus>().发布(new 打开节点内部事件(节点, 返回节点));
    }

    // 全局取消 = 返回设施内部（无来源节点则不可取消）
    public override bool 回退()
    {
        if (节点 == null) return false;
        返回设施内部();
        return true;
    }

    public override string 取消文本 => "返回";
}
