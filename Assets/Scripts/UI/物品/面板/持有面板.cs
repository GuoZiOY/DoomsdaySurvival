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

    // ================= 侧边栏让位（"UI 适应"· 由用户手测的数值驱动） =================
    // 侧边栏弹出时盖住右区最右边一条 → 这两组各自"位置左移 + 收窄"：
    //     装具区    ：Pos X −30、Width −60
    //     仓库/搜索 ：Pos X −70、Width −20
    //
    // ★ 这四个数是用户在编辑器里用矩形工具拖到"看着合适"之后报的**实测增量**，不是我推导的。
    //   之前那版改 `offsetMax`（语义 = "只动右边界"）在装具区上对、在仓库/搜索上**必然不对** ——
    //   这两件连**左边界也左移了 60**（不是单纯拉右边界）。所以这里直接改
    //   `anchoredPosition.x` / `sizeDelta.x` —— 就是 Inspector 上那两个数（Pos X / Width）。
    //   注释里给的"改前 → 改后"是用户当时报的绝对值，方便对账。
    [Header("侧边栏让位（侧边栏弹出时右区怎么挪）")]
    [SerializeField] private float 装具区位置增量 = -30f;   // 1190 → 1160
    [SerializeField] private float 装具区宽度增量 = -60f;   //  740 → 680
    [SerializeField] private float 右区位置增量 = -70f;     // -500 → -570（仓库 / 搜索 共用）
    [SerializeField] private float 右区宽度增量 = -20f;     // 1000 →  980

    private sealed class 让位记录
    {
        public RectTransform 件;
        public Vector2 基线;   // (Pos X, Width) —— 未让位时的值
        public Vector2 增量;
        // ★ 宽度"按内容自适应"的组件。有它在，我们写进 `sizeDelta.x` 的宽度会被它下一次布局重算顶掉
        //   （而它不管 `anchoredPosition`，所以位置留住了、宽度没留住 —— 正是实测到的现象）。
        //   让位期间把它临时改成 `Unconstrained`，复位时放回去并重建布局。
        public UnityEngine.UI.ContentSizeFitter 拟合器;
        public UnityEngine.UI.ContentSizeFitter.FitMode 原横向;
        public bool 有布局组;
    }
    private readonly System.Collections.Generic.List<让位记录> 让位表 = new System.Collections.Generic.List<让位记录>();
    private bool 让位表已建, 已让位;

    // ★ 找 装具区 —— **不能只信 `装具区.实例`**。
    //   那个静态单例是在 `装具区.Awake` 里赋值的，而 持有面板 与它**同帧激活、Awake 顺序不保证** →
    //   曾经在这里拿到 null，于是让位表里只有 2 件（仓库 + 搜索），**装具区的宽度永远不变**
    //   （实测日志：`侧边栏让位：开（2 件）`）。
    //   直接在本面板子树里找（`true` = 含未激活），绕开 Awake 顺序这个不确定性。
    private RectTransform 找装具区()
    {
        var 具 = 装具区.实例 != null ? 装具区.实例 : GetComponentInChildren<装具区>(true);
        return 具 != null ? 具.transform as RectTransform : null;
    }

    private void 建让位表()
    {
        if (让位表已建) return;
        void 加(RectTransform 件, Vector2 增量)
        {
            if (件 == null) return;
            var 拟合 = 件.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            让位表.Add(new 让位记录
            {
                件 = 件,
                基线 = new Vector2(件.anchoredPosition.x, 件.sizeDelta.x),
                增量 = 增量,
                拟合器 = 拟合,
                原横向 = 拟合 != null ? 拟合.horizontalFit : UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained,
                有布局组 = 件.GetComponent<UnityEngine.UI.LayoutGroup>() != null,
            });
        }
        加(找装具区(), new Vector2(装具区位置增量, 装具区宽度增量));
        加(仓库 != null ? 仓库.transform as RectTransform : null, new Vector2(右区位置增量, 右区宽度增量));
        加(搜索 != null ? 搜索.transform as RectTransform : null, new Vector2(右区位置增量, 右区宽度增量));
        // 一件都没拿到就**下次再试**（`装具区.实例` 这个静态单例可能还没准备好）——
        // 上一版在这里把空表钉死，表现是"让位完全不动、也没有任何日志"。
        // 期望 3 件（装具区 / 仓库 / 搜索）。**没凑齐就先不钉死** —— 上一版"只要非空就钉死"，
        // 于是缺一件就永远缺（实测日志 `开（2 件）`：装具区缺失 → 它的宽度永远不变）。
        // 注：只有在还没让位过的时候才敢清表重来；已经让位过就保留现状（清表会让基线变成"让位后的值"）。
        if (让位表.Count < 3 && !已让位)
        {
            int 拿到 = 让位表.Count;
            让位表.Clear();
            Debug.LogWarning($"[持有面板] 侧边栏让位：只拿到 {拿到} 件（要 3 件：装具区/仓库/搜索）—— 稍后重试。");
            return;
        }
        让位表已建 = true;
        Debug.Log($"[持有面板] 侧边栏让位表建好：{让位表.Count} 件 —— " + string.Join("、",
            让位表.ConvertAll(x => $"{x.件.name}(x={x.基线.x:0},宽={x.基线.y:0}" +
                                   (x.拟合器 != null ? $",自适应={x.原横向}" : "") + (x.有布局组 ? ",有布局组" : "") + ")")));
    }

    // 让位 / 复位（幂等）。只动 x 与宽度，**y 保留当前值**（免得踩到别的系统正在改的 y）。
    private void 应用让位(bool 让位)
    {
        建让位表();
        if (让位表.Count == 0 || 让位 == 已让位) return;
        已让位 = 让位;
        foreach (var 记 in 让位表)
        {
            if (记.件 == null) continue;
            // ① 先处理"自适应宽度"：让位时关掉它，否则下面写的宽度会被它重算顶掉
            if (记.拟合器 != null)
                记.拟合器.horizontalFit = 让位 ? UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained : 记.原横向;
            // ② 写位置与宽度（只动 x / 宽，y 与高保留当前值）
            记.件.anchoredPosition = new Vector2(记.基线.x + (让位 ? 记.增量.x : 0f), 记.件.anchoredPosition.y);
            记.件.sizeDelta = new Vector2(记.基线.y + (让位 ? 记.增量.y : 0f), 记.件.sizeDelta.y);
            // ③ 复位时让自适应重新算一遍（算出来的就是基线宽度），并重建布局
            if (!让位 && 记.拟合器 != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(记.件);
        }
        Debug.Log($"[持有面板] 侧边栏让位：{(让位 ? "开" : "关")}（{让位表.Count} 件）");
    }

    private void OnEnable() => 应用让位(侧边栏面板.实例 != null && 侧边栏面板.实例.可见);

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
        // 侧边栏开合时跟着让位 / 复位（本面板初始 inactive → 那之前的广播收不到，
        // 所以 `OnEnable` 里还会按当前状态对齐一次）
        ServiceRegistry.Get<EventBus>()?.订阅<侧边栏显隐变化事件>(e => 应用让位(e.显示中));
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
