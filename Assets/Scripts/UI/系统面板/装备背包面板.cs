// 装备与背包面板：薄编排器（挂"装备与背包面板"父物体）。
// 子节点：装备区面板 + 背包区面板（同级）；父物体显隐时子节点随之显隐（OnEnable/刷新 自刷）。
// 面板管理器「背包」引用位接本组件；F1 打开/再按返回；侧边栏取消按钮 → 返回上一面板。
public sealed class 装备背包面板 : 面板基类
{
    // 默认关闭：即使未接面板管理器（可切换面板列表外），也初始隐藏；由 F1/面板管理器.显示 激活
    void Awake()
    {
        gameObject.SetActive(false);
    }

    protected override void 刷新(object 上下文)
    {
        // 装备区刷新（背包区 = 网格背包面板 由其自身 override 刷新 自刷——显示面板 已调用 刷新）
        装备面板.实例?.刷新();
        // 容器装备区：每次打开强制重建布局（首次打开时 主背包面板/网格 可能尚未就绪，OnEnable 兜底重建会用到旧尺寸）
        穿戴容器区.实例?.重建();
    }

    public override bool 回退()
    {
        if (面板管理器.实例 != null) 面板管理器.实例.返回上一面板();
        return true;
    }

    public override string 取消文本 => "返回";
}
