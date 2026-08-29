using UnityEngine;

// 装备与背包面板：薄编排器（挂"装备与背包面板"父物体）。
// 子节点：装备区面板 + 背包区面板（同级）；父物体显隐时子节点随之显隐（OnEnable/刷新 自刷）。
// 面板管理器「背包」引用位接本组件；F1 打开/再按返回；侧边栏取消按钮 → 返回上一面板。
// 打开时：调用 背景模糊层.显示模糊（塔科夫式——背景 模糊，面板 清晰；背景图 引用 + 模糊 参数 均 在 背景模糊层 自身）；
// 关闭时：调用 背景模糊层.隐藏模糊。其他 面板 后续 同样 调用，复用 同一 模糊层。
public sealed class 装备背包面板 : 面板基类
{
    // 默认关闭：即使未接面板管理器（可切换面板列表外），也初始隐藏；由 F1/面板管理器.显示 激活
    void Awake()
    {
        gameObject.SetActive(false);
    }

    public override void 显示面板(object 上下文 = null, bool 上下互切 = false, bool 返回方向 = false)
    {
        // 塔科夫式 背景 模糊：模糊层 参数（背景图/模糊显示/模糊强度）由 背景模糊层 组件 自身 配置，这里 只 请求 显示
        背景模糊层.显示模糊();
        base.显示面板(上下文, 上下互切, 返回方向);
    }

    public override void 隐藏面板(bool 上下互切 = false, bool 返回方向 = false)
    {
        背景模糊层.隐藏模糊();   // 关闭 背包 → 隐藏 模糊层（背景 恢复）
        base.隐藏面板(上下互切, 返回方向);
    }

    protected override void 刷新(object 上下文)
    {
        // 装备区刷新（背包区 = 网格面板 由其自身 override 刷新 自刷——显示面板 已调用 刷新）
        装备面板.实例?.刷新();
        // 容器装备区：每次打开强制重建布局（块面板 由 穿戴容器区 注入数据源，Layout 重建走 LateUpdate 延迟到渲染后）
        穿戴容器区.实例?.重建();
    }

    public override bool 回退()
    {
        if (面板管理器.实例 != null) 面板管理器.实例.返回上一面板();
        return true;
    }

    public override string 取消文本 => "返回";
}
