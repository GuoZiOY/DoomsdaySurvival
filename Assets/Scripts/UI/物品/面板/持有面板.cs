using UnityEngine;
using UnityEngine.UI;

// 持有面板：薄编排器（挂"持有面板"父物体）——玩家全部持有物的总界面。
// 子节点：装备区 + 装具区（常驻）+ 右区（模式面板，同级）；父物体显隐时子节点随之显隐（OnEnable/刷新 自刷）。
// 面板管理器「背包」引用位接本组件；F1 打开/再按返回；各面板自己的出口按钮 → 返回上一面板。
// 两种模式（按 上下文 切换右区）：
//   仓库模式（上下文 null）：1 装备 + 2 穿戴 + 3 仓库（塔科夫 stash）
//   搜索模式（上下文 = 搜索容器）：1 装备 + 2 穿戴 + 4 搜索容器（3 位 换 4——搜刮 尸体/容器）
// 打开时：调用 背景模糊层.显示模糊（塔科夫式——背景 模糊，面板 清晰；背景图 引用 + 模糊 参数 均 在 背景模糊层 自身）；
// 关闭时：调用 背景模糊层.隐藏模糊。其他 面板 后续 同样 调用，复用 同一 模糊层。
public sealed class 持有面板 : 面板基类
{
    [SerializeField] private 仓库面板 仓库;      // 右区 仓库面板（仓库模式：1+2+3）
    [SerializeField] private 搜索面板 搜索;    // 右区 搜索面板（搜索模式：1+2+4，3 位 换 4）
    [SerializeField] private Button 返回按钮;    // 返回按钮（Inspector 暴露引用）：点击 = 回退——返回 上一面板
                                                // （从 安全屋 储物箱「使用」打开 → 回 安全屋；F1 打开 → 回 F1 前 面板；主菜单 打开 → 回 主菜单）

    // 初始隐藏 由 场景 控制（持有面板 物体 场景 里 初始 inactive；面板管理器.显示 激活）。
    // 注意：不 在 Awake 里 SetActive(false)——物体 初始 inactive 时 Awake 延迟 到 首次 激活 才 执行，
    //       首次 打开（SetActive(true)）触发 Awake 又 关掉 = 第一次 打不开（重复一次 才 成功）。
    void Awake()
    {
        if (返回按钮 != null)
        {
            返回按钮.onClick.AddListener(() => 回退());
            音效管理器.实例?.注册按钮(返回按钮);
        }
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
        搜索?.关闭();            // 搜索模式 退出：清 黑布/黑块（物品框 随 数据源 置空 重建）
        base.隐藏面板(上下互切, 返回方向);
    }

    protected override void 刷新(object 上下文)
    {
        // 装备区刷新（背包区 = 物品网格面板 由其自身 override 刷新 自刷——显示面板 已调用 刷新）
        装备区.实例?.刷新();
        // 装具区：每次打开强制重建布局（块面板 由 本区 注入数据源，Layout 重建走 LateUpdate 延迟到渲染后）
        装具区.实例?.重建();
        // 模式切换：上下文 = 搜索容器 → 搜索模式（3 位 换 4）；否则 仓库模式
        if (上下文 is 搜索容器 容器)
        {
            if (仓库 != null) 仓库.gameObject.SetActive(false);
            if (搜索 != null) { 搜索.gameObject.SetActive(true); 搜索.打开容器(容器); }
        }
        else
        {
            if (搜索 != null) { 搜索.关闭(); 搜索.gameObject.SetActive(false); }
            if (仓库 != null) { 仓库.gameObject.SetActive(true); 仓库.刷新(); }
        }
    }

    public override bool 回退()
    {
        if (面板管理器.实例 != null) 面板管理器.实例.返回上一面板();
        return true;
    }

    public override string 取消文本 => "返回";
}
