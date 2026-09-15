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

    // ================= ★ 侧边栏让位（"UI 适应"） =================
    // 侧边栏弹出时会盖住右区最右边一条 → 这三件整体左移一个栏宽，让出位置；侧边栏收起时移回去。
    //
    // **不用你拖引用位**：数组留空就自动收三件 —— 仓库 / 搜索（本组件已有的引用位）+ 装具区（静态单例）。
    // 想显式指定就把三个 RectTransform 拖进来（拖了就以你为准，自动收不再生效）。
    //
    // 为什么只改 `offsetMax.x` 而不是改 `sizeDelta`/`anchoredPosition`：
    //   `offsetMax` 的语义是"**右边线相对锚点右边界的内缩**"，与左边界（`offsetMin`）互不影响。
    //   所以无论这三件在场景里是"水平拉伸锚"还是"点锚 + 固定尺寸"，
    //   "右边界左移 80、左边界不动" 的效果都成立 —— 不用去关心它们各自怎么摆的。
    [SerializeField] private RectTransform[] 让位区;
    [SerializeField] private float 让位宽 = 80f;

    private Vector2[] 让位基线;   // 各件"未让位"时的 offsetMax。**只记一次** —— 在被移动之后再记就把"让位后"当成基线了
    private bool 已让位;

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
        确保让位表();   // 先记基线（此刻还没被移动过）
        ServiceRegistry.Get<EventBus>()?.订阅<侧边栏显隐变化事件>(e => 应用让位(e.显示中));
    }

    // 收集"要让位"的三件并记下它们的原始 offsetMax（只在第一次调用时做）
    private void 确保让位表()
    {
        if (让位基线 != null) return;
        var 表 = new System.Collections.Generic.List<RectTransform>();
        if (让位区 != null && 让位区.Length > 0)
        {
            foreach (var 件 in 让位区) if (件 != null) 表.Add(件);
        }
        else
        {
            if (仓库 != null && 仓库.transform is RectTransform 仓) 表.Add(仓);
            if (搜索 != null && 搜索.transform is RectTransform 搜) 表.Add(搜);
            if (装具区.实例 != null && 装具区.实例.transform is RectTransform 具) 表.Add(具);
        }
        让位区 = 表.ToArray();   // 写回字段：Inspector 里能看到实际生效的是哪三件
        让位基线 = new Vector2[让位区.Length];
        for (int i = 0; i < 让位区.Length; i++) 让位基线[i] = 让位区[i].offsetMax;
    }

    // 让位 / 复位（幂等：状态没变就什么都不做）
    private void 应用让位(bool 让位)
    {
        确保让位表();
        if (让位区 == null || 让位区.Length == 0 || 让位 == 已让位) return;
        已让位 = 让位;
        var 偏移 = 让位 ? new Vector2(-让位宽, 0f) : Vector2.zero;
        for (int i = 0; i < 让位区.Length; i++)
            if (让位区[i] != null) 让位区[i].offsetMax = 让位基线[i] + 偏移;
    }

    // 侧边栏现在可见吗（拿不到侧边栏就当作"不可见" = 不让位，保持原样最安全）
    private static bool 侧边栏可见 => 侧边栏面板.实例 != null && 侧边栏面板.实例.可见;

    public override void 显示面板(object 上下文 = null, bool 上下互切 = false, bool 返回方向 = false)
    {
        // ★ 每次打开都按侧边栏当前状态对齐一次 —— 因为"侧边栏开合"的事件可能发生在
        //   本面板还没打开的时候（本面板初始 inactive，Awake 延迟到首次激活才跑 → 那之前的广播收不到）。
        应用让位(侧边栏可见);
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
