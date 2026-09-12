using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 设施功能面板基类：功能面板通用骨架——标题、返回、打开上下文注入。
// 不再强制「内容区 + 创建行」的单一形态：子类在场景搭专属布局（Tab/分区/行模板），只实现 渲染内容() 填数据。
// 返回统一走 侧边栏取消按钮（回退 = 回上一个面板），面板内不再放置 返回按钮。
// 注：原「返回设施内部（三级导航：内部图 → 小地图 → 大地图）」随 城镇小地图/节点内部 一起删掉了。
public abstract class 设施功能面板基类 : 面板基类
{
    [SerializeField] protected TMP_Text 标题;
    protected 设施逻辑 逻辑;       // 打开时由 设施打开上下文 注入

    protected virtual void Awake()
    {
        // 注：原来这里订阅 `金币变化事件`（买卖/训练改金币后整面板刷新），钩子 金币变化() 供子类轻量更新。
        // v51 刀7 一并删掉：本作以物易物**没有货币**，那个事件已被整个移除（且没有任何子类覆写过这个钩子）。
    }

    protected override void 刷新(object 上下文)
    {
        if (上下文 is 设施打开上下文 c) 逻辑 = c.逻辑;
        if (逻辑 == null) return;
        if (标题 != null) 标题.text = 标题文字();
        渲染内容();
    }

    protected abstract string 标题文字();
    protected abstract void 渲染内容();

    // 全局取消 = 回上一个面板（由 面板管理器 决定回哪）
    public override bool 回退()
    {
        if (面板管理器.实例 == null) return false;
        面板管理器.实例.返回上一面板();
        return true;
    }

    public override string 取消文本 => "返回";
}
