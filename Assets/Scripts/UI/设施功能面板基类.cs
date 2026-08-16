using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 设施功能面板基类：功能面板通用骨架（标题 + 列表区 + 返回设施内部）。
// 子类只实现 标题文字()/渲染列表()；数据从 逻辑（设施逻辑实例）经 功能接口 取，跨设施复用。
public abstract class 设施功能面板基类 : 面板基类
{
    [SerializeField] protected TMP_Text 标题;
    [SerializeField] protected RectTransform 列表区;
    [SerializeField] private Button 返回按钮;
    protected 设施逻辑 逻辑;       // 打开时由 设施打开上下文 注入
    protected string 返回节点;

    protected virtual void Awake()
    {
        返回按钮?.onClick.AddListener(返回设施内部);
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<金币变化事件>(_ => 刷新(null));   // 买卖/训练等改金币后刷新
    }

    protected override void 刷新(object 上下文)
    {
        if (上下文 is 设施打开上下文 c) { 逻辑 = c.逻辑; 返回节点 = c.返回节点; }
        if (逻辑 == null) return;
        设文本(标题, 标题文字());
        清空(列表区);
        渲染列表();
    }

    protected abstract string 标题文字();
    protected abstract void 渲染列表();

    // 返回设施内部（三级导航）
    protected void 返回设施内部()
    {
        if (逻辑 == null) return;
        ServiceRegistry.Get<EventBus>().发布(new 打开设施内部事件(逻辑.标识, 返回节点));
    }
}
