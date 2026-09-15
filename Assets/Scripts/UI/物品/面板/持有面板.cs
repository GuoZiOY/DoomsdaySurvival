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

    // ================= 侧边栏让位（"UI 适应"） =================
    // 侧边栏弹出时盖住右区最右边一条 → 这两组各自"位置左移 + 收窄"：
    //     装具区    ：Pos X −30、Width −60   （1190→1160、740→680）
    //     仓库/搜索 ：Pos X −70、Width −20   （−500→−570、1000→980）
    // 这四个数是**手测出来的实测增量**（在编辑器里用矩形工具拖到看着合适），不是推导的。
    // 三件一律走 Inspector 引用（项目惯例：全静态场景搭建 + 拖引用）——
    // `装具区让位` **必须拖**：不能靠 `装具区.实例`（那个静态单例在它自己的 Awake 里赋值，
    // 与 持有面板 **同帧激活、顺序不保证** → 曾经拿到 null，于是装具区的宽度永远不变）。
    [Header("侧边栏让位（侧边栏弹出时右区怎么挪）")]
    [SerializeField] private RectTransform 装具区让位;
    [SerializeField] private float 装具区位置增量 = -30f;
    [SerializeField] private float 装具区宽度增量 = -60f;
    [SerializeField] private float 右区位置增量 = -70f;
    [SerializeField] private float 右区宽度增量 = -20f;

    private sealed class 让位记录
    {
        public RectTransform 件;
        public float 基X, 基宽;
        public float 位置增量, 宽度增量;
        // 宽度"按内容自适应"的组件：让位期间要临时关掉它，否则写进 `sizeDelta.x` 的宽度会被它下一次
        // 布局重算顶掉（它不管 `anchoredPosition` → 位置留住了、宽度没留住，正是实测到的现象）。
        public UnityEngine.UI.ContentSizeFitter 拟合器;
        public UnityEngine.UI.ContentSizeFitter.FitMode 原横向;
    }
    private 让位记录[] 让位表;
    private bool 已让位;

    private void 建让位表()
    {
        if (让位表 != null) return;
        var 表 = new System.Collections.Generic.List<让位记录>();
        加(表, 装具区让位, 装具区位置增量, 装具区宽度增量);
        加(表, 仓库 != null ? 仓库.transform as RectTransform : null, 右区位置增量, 右区宽度增量);
        加(表, 搜索 != null ? 搜索.transform as RectTransform : null, 右区位置增量, 右区宽度增量);
        让位表 = 表.ToArray();

        static void 加(System.Collections.Generic.List<让位记录> 表, RectTransform 件, float 位置增量, float 宽度增量)
        {
            if (件 == null) return;
            var 拟合 = 件.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            表.Add(new 让位记录
            {
                件 = 件,
                基X = 件.anchoredPosition.x,
                基宽 = 件.sizeDelta.x,
                位置增量 = 位置增量,
                宽度增量 = 宽度增量,
                拟合器 = 拟合,
                原横向 = 拟合 != null ? 拟合.horizontalFit : UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained,
            });
        }
    }

    // 让位 / 复位（幂等）。只动 x 与宽度，**y 与高保留当前值**（免得踩到别的系统正在改的）。
    private void 应用让位(bool 让位)
    {
        建让位表();
        if (让位 == 已让位) return;
        已让位 = 让位;
        foreach (var 记 in 让位表)
        {
            if (记.件 == null) continue;
            // ① 自适应宽度：让位时关掉它，否则下面写的宽度会被它重算顶掉
            if (记.拟合器 != null)
                记.拟合器.horizontalFit = 让位 ? UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained : 记.原横向;
            // ② 写位置与宽度
            记.件.anchoredPosition = new Vector2(记.基X + (让位 ? 记.位置增量 : 0f), 记.件.anchoredPosition.y);
            记.件.sizeDelta = new Vector2(记.基宽 + (让位 ? 记.宽度增量 : 0f), 记.件.sizeDelta.y);
            // ③ 复位时让自适应重新算一遍（算出来的就是基线宽度）
            if (!让位 && 记.拟合器 != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(记.件);
        }
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
            // ★ 用户 2026-09-15：右区怎么显示，看"在不在家"：
            //     在安全屋：显示仓库（家底）
            //     在外面  ：**两个都不显示**（右区空着 —— 外面的家底不该随手可整理）
            //   搜索面板**只在"搜容器"时才出现**（见上面 `上下文 is 搜索容器` 那条分支）——
            //   自己主动打开背包（F1 / 侧边栏）永远不进搜索模式，所以这里一律收起搜索。
            //   "外面" = 有探索层活着（大世界/区域/房间 任一 `探索中`）；在安全屋时三层都是空的。
            //   为什么不用"当前显示面板是不是安全屋面板"判断：本面板一显示，当前面板就是**自己**了，恒为假。
            //   注：装备区 / 装具区 **一直显示** —— 本面板只切右区这个模式位，从不碰它们。
            bool 在外面 = 外出中;
            if (仓库 != null)
            {
                仓库.gameObject.SetActive(!在外面);
                if (!在外面) 仓库.刷新();
            }
            if (搜索 != null)
            {
                搜索.关闭();                       // 退出搜索态（清黑布/黑块）
                搜索.gameObject.SetActive(false);
            }
        }
    }

    // 外出中？（三个探索层任一活着 = 不在安全屋）
    private static bool 外出中
        => (探索中<大世界探索服务>()) || (探索中<区域探索服务>()) || (探索中<房间探索服务>());

    private static bool 探索中<T>() where T : 格子探索服务
        => ServiceRegistry.已注册<T>() && ServiceRegistry.Get<T>()?.探索中 == true;

    public override bool 回退()
    {
        if (面板管理器.实例 != null) 面板管理器.实例.返回上一面板();
        return true;
    }

    public override string 取消文本 => "返回";
}
