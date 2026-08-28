using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 网格面板：通用塔科夫式网格（主背包/仓库/容器/穿戴容器 复用），动态构建网格底座 + 物品层（纯 图像+文本，非按钮）。
// 底座：按 服务.网格列×网格行 动态生成的格子底图（数据源决定网格尺寸，尺寸变化自动重建）；每格 Outline 描边线形成视觉网格。
// 物品：按 (列,行,宽,高,旋转) 跨格铺放的 图像+文本；点击（IPointerClickHandler）选中/双击快捷操作；拖拽（IDragHandler）移动/换位。
// 拖拽交互：按住物品拖起（半透明代理跟随鼠标）→ 放空格=移动（拖拽中按 R 旋转）→ 放被占格=换位（互换位置与旋转）→ 拖出网格=取消。
// 性能：增量刷新（物品框表只动变化框）+ 静态登记表（替代 FindObjectsOfType）+ 拖拽判定缓存。
// 数据源：null = 主背包（档案.背包服务）；非空 = 外部视图（容器/仓库/穿戴容器，由 容器面板/仓库区/穿戴容器区 注入）。
public sealed class 网格面板 : 面板基类
{
    [SerializeField] private RectTransform 网格容器;   // 网格区域（左上锚定；代码动态生成底座与物品）。可运行时 绑定网格容器 覆盖（动态容器面板）
    [SerializeField] private int 固定列数 = 6;         // Inspector 固定覆盖 服务.网格列（0 = 用服务尺寸；主背包测试参数）
    [SerializeField] private int 固定行数 = 10;        // Inspector 固定覆盖 服务.网格行（0 = 用服务尺寸；主背包测试参数）
    public const float 格尺寸 = 100f;                  // 单格像素：全项目统一 100（主背包/仓库/容器/穿戴容器 一致，2K 基准）
    [SerializeField] private int 固定列数最小 = 1;       // 固定列数（格子宽）下限
    [SerializeField] private int 固定列数最大 = 100;      // 固定列数上限
    [SerializeField] private int 固定行数最小 = 1;       // 固定行数下限
    [SerializeField] private int 固定行数最大 = 100;      // 固定行数上限
    // 注：配色参数（底座色/边界色/线条色/线宽/边距/投影色 等）统一定义在 网格面板配色 静态类——组件不暴露配色，改风格只改 网格面板配色.cs。

    private 玩家档案 档案 => ServiceRegistry.Get<PlayerService>().档案;
    private DataService 数据 => ServiceRegistry.Get<DataService>();
    private 物品堆叠 选中;
    private RectTransform 底座层, 线层, 物品层;   // 网格分层（底座Grid铺格 / 分隔线 / 物品与拖拽视觉），Content 滚动容器

    // 网格数据源：由 容器面板/仓库区/穿戴容器区 注入（容器/仓库/穿戴容器 视图）。
    // 数据源 null 仅防御兜底（档案.背包服务 已废弃——不再存放物品，显示空网格）；面板内所有网格判定都走 服务（背包服务 方法）。
    private 背包服务 服务 => 数据源 ?? 档案.背包服务;
    [NonSerialized] public 背包服务 数据源;   // 注入 外部视图（容器/仓库/穿戴容器）
    [NonSerialized] public 物品堆叠 所属容器;  // 非空 = 本面板显示的容器实例（跨面板转移用；容器面板注入）
    [NonSerialized] public string 所属槽位;   // 非空 = 本面板显示的穿戴容器槽位（弹挂/腰封/背包；穿戴容器区注入）——允许放入/嵌套校验用

    // 全局活动拖拽：跨面板拖拽（主背包 ↔ 容器）的状态。发起面板在 开始拖拽 记录，任一面板 结束拖拽 时消费。
    private static 网格面板 拖拽发起面板;
    private static 背包服务 拖拽源服务;
    private static 物品堆叠 拖拽中堆叠;

    // 静态登记表（替代 FindObjectsOfType：拖拽/投影/面板命中等高频遍历用，FindObjectsOfType 场景遍历+反射很慢）
    private static readonly List<网格面板> 全部面板 = new List<网格面板>();
    public static List<网格面板> 面板登记表 => 全部面板;

    // 物品框表（增量刷新核心）：堆叠实例 → 物品框。刷新时只对 新增创建/移除销毁/移动与数值变化更新 对应框，不重建整个网格。
    // 尺寸/数据源变化才全量重建（重建网格结构），否则 刷新物品 增量复用。
    private sealed class 物品框
    {
        public RectTransform 根;       // 物品根（框层：品质底色+黑描边，点击/拖拽挂这里）
        public Image 框图;             // 品质底层色（变化才改）
        public RectTransform 内容层;   // 悬停放大（内缩内容层）
        public GameObject 高光层;      // 悬停高光
        public TMP_Text 名称;          // 物品名（创建时定，不变）
        public TMP_Text 耐久;          // 耐久文本（变化才改）
        public TMP_Text 数量;          // 数量角标（变化才改）
        public bool 存活;              // 本帧刷新命中标记（未命中 = 从网格移除 → 销毁）
        public int 上次数量;           // 数量文本去重
        public string 上次耐久;        // 耐久文本去重
        public int 上次列, 上次行;     // 上次位置（布局变化检测：移动/旋转 才重画 分隔线）
        public bool 上次旋转;
    }
    private readonly Dictionary<物品堆叠, 物品框> 物品框表 = new Dictionary<物品堆叠, 物品框>();

    // 拖拽判定缓存：落点格/旋转 未变 → 复用上次合法性判定（可放/可合并），避免每帧重算 该格物品/可存入容器/区域互换 重算法
    private int 上次判定列 = -1, 上次判定行 = -1;
    private bool 上次判定旋转, 上次判定有效, 上次可放, 上次可合并;

    // 本次网格实际尺寸（有效列/有效行 经 clamp 后）：渲染（层/线/格/物品）统一用它，避免与服务网格错位
    private int 当前列, 当前行;
    private float 上次格尺寸;   // 格尺寸变化检测（常量 100 后恒等，保留防御）→ 全量重建，避免底格/线 与物品错位
    // 公开只读（容器面板 按容器网格尺寸调整面板大小用）
    public int 渲染列 => 当前列;
    public int 渲染行 => 当前行;

    // 清空某网格层的子物体：先脱离父（避免 GridLayoutGroup 的 LayoutRebuilder 访问已销毁的格），再销毁
    private void 清空层(RectTransform 层)
    {
        if (层 == null) return;
        for (int i = 层.childCount - 1; i >= 0; i--)
        {
            var 子 = 层.GetChild(i);
            if (子 != null) { 子.SetParent(null); Destroy(子.gameObject); }
        }
    }

    // —— 拖拽状态 ——
    private 物品堆叠 拖拽源;
    private RectTransform 拖拽代理;   // 跟手物品图片（吸附格子）
    private Image 落点投影;           // 网格上的绿/红落点指示
    private GameObject 原位置影子;    // 原位置的半透明虚影
    private static bool 拖拽旋转;      // 全局拖拽预览旋转（R 键；跨面板投影共享——任意面板 Update 都按它算投影）
    private int 落点列, 落点行;        // 拖拽中最后有效投影格（放下用，不随松手重算）
    private bool 落点有效;            // 投影当前是否有效（在网格内）

    // ===== 生命周期 =====

    void Awake()
    {
        全部面板.Add(this);   // 登记表：拖拽/投影遍历替代 FindObjectsOfType
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<背包变化事件>(背包变化响应);   // 只订阅 背包变化：物品增删移 → 增量刷新
    }

    // 跨面板拖拽：本面板不是发起者时，鼠标在本面板网格上 → 显示投影（绿/红/蓝），松手由 结束拖拽 转移
    void Update()
    {
        // 脏标记合并刷新：事件（背包变化）只标记，Update 帧末合并一次刷新——装备等操作同帧多次事件不重复重建。
        // 两种刷新：网格（尺寸可能变 → 全量）> 物品（增量复用物品框）。网格刷新已含物品，执行后清掉物品标记。
        if (待刷新网格) 
            { 待刷新网格 = false; 待刷新物品 = false; 刷新网格(); }
        else if (待刷新物品) 
            { 待刷新物品 = false; 刷新物品(); }
        if (拖拽中堆叠 == null) 
            return;
        // R 旋转预览：放 Update 每帧检测（不受 OnDrag 需鼠标移动才触发的限制——静止按住也能按 R）
        if (检测按R()) 
            { 拖拽旋转 = !拖拽旋转; 更新代理尺寸(); }
        if (拖拽发起面板 == this) 
            return;
        确保投影();   // 懒创建本面板投影（发起面板才有，其他面板首次需要时创建）
        if (落点投影 == null) 
            return;

        var 鼠标 = 输入鼠标位置();
        var 伪事件 = new PointerEventData(EventSystem.current) { position = 鼠标 };
        var 下方 = 事件下方面板(伪事件);

        if (下方 != this) 
            { 落点投影.gameObject.SetActive(false); return; }
        if (!屏幕到容器相对(伪事件, out var 相对, out var 尺寸)) 
            { 落点投影.gameObject.SetActive(false); return; }

        var (物宽, 物高) = 预览占格(拖拽中堆叠);   // 按 拖拽旋转（R 预览，静态共享）→ 跨面板投影随旋转同步
        float 相对顶 = 尺寸.y - 相对.y;
        if (!落格(相对.x, 相对顶, 物宽, 物高, out int 列, out int 行))
            { 落点投影.gameObject.SetActive(false); return; }

        落点投影.gameObject.SetActive(true);
        落点投影.rectTransform.anchoredPosition = new Vector2(列 * 格尺寸, -行 * 格尺寸);
        落点投影.rectTransform.sizeDelta = new Vector2(物宽 * 格尺寸, 物高 * 格尺寸);   // 投影贴格（占格大小）
        // 目标面板校验：允许放入（容器类型限制）+ 嵌套防护（自己套自己/循环/套娃上限）+ 落点格 占位（可放置/可合并/快捷收入容器）
        bool 可放 = 目标允许放入(拖拽中堆叠.标识) && !目标禁放入(拖拽中堆叠);
        if (可放)
        {
            var 落点物品 = 服务.该格物品(列, 行);
            可放 = (落点物品 != null && 落点物品 != 拖拽中堆叠 && 服务.可合并(落点物品, 拖拽中堆叠))
                || 可存入容器(落点物品, 拖拽中堆叠)   // 快捷收入：落点格是容器且允许+有空位 → 绿
                || 服务.可放置(拖拽中堆叠.标识, 列, 行, 拖拽旋转, 拖拽中堆叠);
        }
        落点投影.color = 可放 ? 网格面板配色.放置可色 : 网格面板配色.放置禁色;
    }

    // 懒创建本面板的落点投影（挂在物品层，贴格显示）。发起面板在 创建拖拽视觉 已建；其他面板跨面板拖拽时首次建。
    private void 确保投影()
    {
        if (落点投影 != null || 物品层 == null) return;
        var 投体 = new GameObject("落点投影", typeof(RectTransform), typeof(Image));
        投体.transform.SetParent(物品层, false);
        落点投影 = 投体.GetComponent<Image>();
        落点投影.color = 网格面板配色.放置可色;
        落点投影.raycastTarget = false;
        var 投影矩形 = 投体.GetComponent<RectTransform>();
        投影矩形.anchorMin = new Vector2(0, 1);
        投影矩形.anchorMax = new Vector2(0, 1);
        投影矩形.pivot = new Vector2(0, 1);
        落点投影.gameObject.SetActive(false);
    }

    // 鼠标屏幕位置（兼容新旧输入）
    private Vector2 输入鼠标位置()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null) return UnityEngine.InputSystem.Mouse.current.position.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#endif
        return Vector2.zero;
    }

    // 拖拽结束时隐藏所有面板的跨面板投影（避免容器面板投影残留）——登记表遍历（替代 FindObjectsOfType）
    private static void 清理所有面板投影()
    {
        foreach (var 面板 in 全部面板)
            if (面板.落点投影 != null) 面板.落点投影.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        全部面板.Remove(this);   // 注销登记表
        if (ServiceRegistry.已注册<EventBus>())
        {
            var 事件 = ServiceRegistry.Get<EventBus>();
            事件.取消订阅<背包变化事件>(背包变化响应);
        }
    }

    private bool 待刷新网格, 待刷新物品;   // 脏标记：事件 → 标记，Update 合并刷新（网格=尺寸全量 / 物品=增量）

    // 事件响应：只标记脏，Update 统一刷新（优化：装备/转移同帧多个事件不重复重建）
    // 背包变化 → 物品增删移（增量；尺寸变由 刷新物品 内部兜底检测走全量）
    private void 背包变化响应(背包变化事件 _) => 待刷新物品 = true;

    // 外部请求刷新入口（穿戴容器区/仓库区/容器面板 注入数据源/尺寸后调用）：内部自动判定 全量重建 或 增量刷新
    public void 请求刷新() => 待刷新网格 = true;
    // 立即刷新（穿戴容器区 装备/卸下 后强制显隐生效用）：直接执行（低频结构变化，不走脏标记）
    public void 立即刷新() => 刷新网格();

    // —— 目标容器校验（跨面板拖拽）：所属容器（容器面板）或 所属槽位（穿戴容器块）——
    // 允许放入：容器允许类型 校验（弹挂/腰封 只装弹药；仓库/主背包 无限制）
    private bool 目标允许放入(string 标识)
    {
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        if (所属容器 != null) return 容器服务.允许放入(所属容器, 标识);
        if (!string.IsNullOrEmpty(所属槽位))
        {
            var 记录 = 档案?.装备.Find(e => e.槽位 == 所属槽位);
            if (记录 != null && !string.IsNullOrEmpty(记录.标识)) return 容器服务.允许放入(记录.标识, 标识);
        }
        return true;   // 非容器面板：无限制
    }

    // 套娃上限：容器 最多 嵌套 3 层（背包装腰封装弹挂 → 弹挂 内 物品 = 3 层；更深 拒绝）
    private const int 嵌套上限 = 3;

    // 嵌套防护：允许嵌套（塔科夫：背包套背包/腰封/弹挂——弹挂/腰封 放不下大背包 靠 内部面积设计 自然限制，后续实现），
    // 但禁止：① 自己套自己（同一容器实例放进自己）② 循环引用（A 含 B 后 B 再装 A）③ 超过套娃上限。
    private bool 目标禁放入(物品堆叠 拖入)
    {
        if (拖入 == null) return false;
        if (拖入 == 所属容器) return true;   // 自己套自己：同一容器实例 放 进 自己
        if (所属容器 != null && 嵌套包含(拖入, 所属容器, 0)) return true;   // 循环引用：拖入 的 嵌套链 含 目标容器
        if (所属容器 != null || !string.IsNullOrEmpty(所属槽位))
        {
            // 套娃上限：目标容器 深度（顶层=1）+ 拖入容器 嵌套深度 ≤ 上限
            if (目标容器深度() + 嵌套深度(拖入) > 嵌套上限) return true;
        }
        return false;
    }

    // 目标容器 当前 嵌套深度（它在 顶层 链 中 的 层数；穿戴容器块 装备记录 = 顶层 1；不在 链 = 1 兜底）
    private int 目标容器深度()
    {
        if (所属容器 == null) return 1;
        int d = 查找深度(所属容器);
        return d > 0 ? d : 1;
    }

    // 堆叠 在 顶层持有 链 中 的 层数（3 穿戴容器 + 仓库 顶层 = 1；嵌套 一层 +1）
    private int 查找深度(物品堆叠 目标)
    {
        foreach (var 槽 in new[] { "弹挂", "腰封", "背包" })
        {
            var 记录 = 档案?.装备.Find(e => e.槽位 == 槽);
            if (记录?.容器物品 == null) continue;
            int d = 找于列表(记录.容器物品, 目标, 1);
            if (d > 0) return d;
        }
        return 找于列表(档案?.仓库物品, 目标, 1);
    }

    private int 找于列表(List<物品堆叠> 列表, 物品堆叠 目标, int 深度)
    {
        if (列表 == null) return 0;
        foreach (var 堆 in 列表)
        {
            if (堆 == 目标) return 深度;
            if (堆.容器物品 != null)
            {
                int d = 找于列表(堆.容器物品, 目标, 深度 + 1);
                if (d > 0) return d;
            }
        }
        return 0;
    }

    // 拖入容器 的 嵌套链 是否 包含 目标容器（引用相等 = 循环）
    private static bool 嵌套包含(物品堆叠 容器, 物品堆叠 目标, int 深度)
    {
        if (容器.容器物品 == null || 深度 >= 3) return false;
        foreach (var 内 in 容器.容器物品)
        {
            if (内 == 目标) return true;
            if (嵌套包含(内, 目标, 深度 + 1)) return true;
        }
        return false;
    }

    // 堆叠 的 嵌套深度（容器 自身 = 1，内部 再嵌套 +1；非 容器 = 0）
    private static int 嵌套深度(物品堆叠 堆叠)
    {
        if (堆叠?.容器物品 == null) return 0;
        int 深 = 1;
        foreach (var 内 in 堆叠.容器物品)
            if (内 != null) 深 = Math.Max(深, 嵌套深度(内) + 1);
        return 深;
    }

    protected override void 刷新(object 上下文)
    {
        选中 = null;
        请求刷新();
    }

    public override bool 回退()
    {
        if (面板管理器.实例 != null) 面板管理器.实例.返回上一面板();
        return true;
    }

    public override string 取消文本 => "返回";

    // ===== 渲染 =====

    // 刷新网格：尺寸/首建变化 → 全量重建（底座+线+物品）；否则 → 增量刷新物品（复用物品框，不重建底格线）。
    // 增量刷新是性能核心：拖拽/装备只动变化的物品框，不再每次销毁重建整张网格（仓库 200 格 + 420 线）。
    private void 刷新网格()
    {
        if (网格容器 == null) return;
        // 外部视图（数据源非空）：用服务自身尺寸（容器/仓库/穿戴容器 由 注入方设置 服务.网格列/行）
        int 有效列, 有效行;
        if (数据源 != null)
        {
            有效列 = 服务.网格列;
            有效行 = 服务.网格行;
        }
        else
        {
            有效列 = 固定列数 > 0 ? 固定列数 : 服务.网格列;
            有效列 = Mathf.Clamp(有效列, 固定列数最小, 固定列数最大);
            有效行 = Mathf.Clamp(固定行数 > 0 ? 固定行数 : 服务.网格行, 固定行数最小, 固定行数最大);
            // 主背包：Inspector 参数写回 服务 网格（判定/放置 与渲染一致）；格尺寸 固定常量（网格面板.格尺寸=100）
            服务.网格列 = 有效列;
            服务.网格行 = 有效行;
        }
        if (当前列 != 有效列 || 当前行 != 有效行 || 底座层 == null || Mathf.Abs(上次格尺寸 - 格尺寸) > 0.01f)
        {
            当前列 = 有效列; 当前行 = 有效行;   // 渲染基准（层/线/格/物品统一用它）
            上次格尺寸 = 格尺寸;
            重建网格结构();
        }
        刷新物品();   // 增量：只动变化的物品框（尺寸刚变时 = 全建）
    }

    // 全量重建网格结构（仅 尺寸变化/首次）：尺寸设置 + 三层 + 底格 + 分隔线。物品框由 刷新物品 增量处理。
    private void 重建网格结构()
    {
        准备层();
        var cf = 网格容器.GetComponent<ContentSizeFitter>();
        if (cf != null) cf.enabled = false;   // 禁用可能残留的 ContentSizeFitter，避免按子对象 preferred 把 Content 撑成 0（尺寸由本方法设置）
        float 网格宽 = 当前列 * 格尺寸, 网格高 = 当前行 * 格尺寸;
        // 注：不 改动 Content 的 锚点/位置（场景 手动 配置 为准）——只 设置 尺寸。
        // 单点锚 下 sizeDelta 生效；拉伸锚 请 在 场景 配好 Content 尺寸（offset 拉伸 时 sizeDelta 无效）。
        if (数据源 != null)
            网格容器.sizeDelta = new Vector2(网格宽, 网格高);   // 容器/仓库/穿戴容器 视图：网格精确尺寸（不滚动/不撑宽）
        else
        {
            float 视口宽 = 网格容器.parent != null ? ((RectTransform)网格容器.parent).rect.width : 网格宽;
            网格容器.sizeDelta = new Vector2(Mathf.Max(视口宽, 网格宽), 网格高);   // 主背包：宽=视口宽（内容可水平居中），高=网格高（滚动）
        }
        底座层.sizeDelta = new Vector2(网格宽, 网格高);   // 三层都以 网格 为基准：顶部 + 水平居中 于 Content
        线层.sizeDelta = new Vector2(网格宽, 网格高);
        物品层.sizeDelta = new Vector2(网格宽, 网格高);
        清空层(底座层); 清空层(线层); 清空层(物品层);
        for (int 行 = 0; 行 < 当前行; 行++)
            for (int 列 = 0; 列 < 当前列; 列++)
                创建底格(列, 行);
        画分隔线();   // 线在格子之间（格子在线内）
        清空物品框表();   // 网格结构变化 → 旧物品框全部失效（下次 刷新物品 全建）
    }

    // 清空物品框表（全量重建前调用：旧框销毁，下次 刷新物品 全建）
    private void 清空物品框表()
    {
        foreach (var kv in 物品框表)
            if (kv.Value != null && kv.Value.根 != null) Destroy(kv.Value.根.gameObject);
        物品框表.Clear();
    }

    // 增量刷新物品：遍历 服务.背包 —— 新增创建 / 已有更新（位置/旋转/品质/数量/耐久）/ 移除销毁；不重建底格与分隔线。
    // 布局变化（物品 增删/移动/旋转）→ 重画 分隔线（边界 亮/内部 淡 跟随 物品——否则 增量 放入 的 物品 边界 线 不 亮）。
    private void 刷新物品()
    {
        if (物品层 == null) return;
        // 兜底：尺寸/格尺寸 可能已变（换包/外部注入新尺寸）→ 走全量重建
        if (当前列 != 服务.网格列 || 当前行 != 服务.网格行 || Mathf.Abs(上次格尺寸 - 格尺寸) > 0.01f) { 刷新网格(); return; }
        // 先复位全部存活标记：上一帧命中的框 存活=true，本帧必须从 false 起算——
        // 否则"本帧被移除的物品"的框 存活 沿用 true → 不销毁 → 物品图片残留（装备成功但图片还在 的 bug）
        foreach (var kv in 物品框表) kv.Value.存活 = false;
        bool 布局变 = false;
        foreach (var 堆叠 in 服务.背包)
        {
            if (堆叠 == null || 堆叠.列 < 0) continue;
            if (物品框表.TryGetValue(堆叠, out var 框))
            {
                if (框.上次列 != 堆叠.列 || 框.上次行 != 堆叠.行 || 框.上次旋转 != 堆叠.旋转) 布局变 = true;
                框.存活 = true;
                更新物品框(框, 堆叠);
            }
            else { 布局变 = true; var 新框 = 创建物品(堆叠); if (新框 != null) { 新框.存活 = true; 物品框表[堆叠] = 新框; } }
        }
        // 未命中的框（存活=false：物品已从网格移除）→ 销毁并从表移除
        if (物品框表.Count > 0)
        {
            List<物品堆叠> 待删 = null;
            foreach (var kv in 物品框表)
                if (!kv.Value.存活) (待删 ??= new List<物品堆叠>()).Add(kv.Key);
            if (待删 != null)
            {
                布局变 = true;
                foreach (var 堆叠 in 待删)
                {
                    if (物品框表.TryGetValue(堆叠, out var 框) && 框.根 != null) Destroy(框.根.gameObject);
                    物品框表.Remove(堆叠);
                }
            }
        }
        if (布局变) 重画分隔线();   // 物品 布局 变化 → 分隔线 重画（边界 线 跟随 物品）
    }

    // 重画分隔线：清 线层 + 重建（增量刷新 布局变化 后调用——物品边界 亮/内部淡 跟随当前布局）
    private void 重画分隔线()
    {
        if (线层 == null) return;
        清空层(线层);
        画分隔线();
    }

    // 首次准备网格分层：Content(网格容器) 下 底座层(GridLayoutGroup) / 线层 / 物品层；Content 挂 ContentSizeFitter 自撑
    private void 准备层()
    {
        if (底座层 != null) return;
        底座层 = 创建网格层("底座层");
        线层 = 创建网格层("线层");
        物品层 = 创建网格层("物品层");
        // 注：不挂 GridLayoutGroup/ContentSizeFitter——底格手动定位、Content 尺寸手动撑，
        //     避免 GridLayoutGroup 的 LayoutRebuilder 在销毁格后访问已销毁实例的 MissingReference 报错。
    }

    // 创建网格层（在 网格容器 下）：撑满 Content、pivot 左上——物品/线 以 网格 左上为原点绝对定位
    private RectTransform 创建网格层(string 名字)
    {
        var 物体 = new GameObject(名字, typeof(RectTransform));
        物体.transform.SetParent(网格容器, false);
        var r = 物体.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 1f);
        r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(当前列 * 格尺寸, 当前行 * 格尺寸);   // 层 = 网格尺寸，顶部 + 水平居中 于 Content
        return r;
    }

    // 底座一格（底图；由 GridLayoutGroup 自动铺格/对齐——不再手动定位）
    private void 创建底格(int 列, int 行)
    {
        var 物体 = new GameObject($"底格_{行}_{列}", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(底座层, false);
        var 图 = 物体.GetComponent<Image>();
        图.color = 网格面板配色.底座色;
        图.raycastTarget = false;   // 纯底图（不描边——分隔线由 画分隔线 统一绘制，格子在线内）
        定位(物体.GetComponent<RectTransform>(), 列, 行, 1, 1);   // 手动铺格（相对 底座层 左上）
    }

    // 画网格分隔线（按物品覆盖分段）：物品边界线亮；物品内部线与空格线一样淡
    private void 画分隔线()
    {
        for (int i = 0; i <= 当前列; i++)
            for (int j = 0; j < 当前行; j++)
                画竖线段(i, j, 竖线边界(i, j));
        for (int j = 0; j <= 当前行; j++)
            for (int i = 0; i < 当前列; i++)
                画横线段(i, j, 横线边界(i, j));
    }

    // 竖线段的"物品边界"判定：两侧都是空格 → 淡；同一物品内部 → 淡；否则（一侧有物品/不同物品）→ 亮
    private bool 竖线边界(int i, int j)
    {
        var 左格 = i > 0 ? 该格物品(i - 1, j) : null;
        var 右格 = i < 当前列 ? 该格物品(i, j) : null;
        if (左格 == null && 右格 == null) return false;   // 两侧都空 → 淡
        if (左格 != null && 左格 == 右格) return false;   // 同一物品内部 → 淡
        return true;                                       // 物品边缘 → 亮
    }

    // 横线段的"物品边界"判定
    private bool 横线边界(int i, int j)
    {
        var 上格 = j > 0 ? 该格物品(i, j - 1) : null;
        var 下格 = j < 当前行 ? 该格物品(i, j) : null;
        if (上格 == null && 下格 == null) return false;
        if (上格 != null && 上格 == 下格) return false;
        return true;
    }

    // 竖线 | 格边界：列 i 与 行 j 交点的一段（向下 格尺寸 高）；亮=物品边界，否则内部/空格（淡）
    private void 画竖线段(int 列, int 行, bool 亮)
    {
        var 物体 = new GameObject($"竖线_{列}_{行}", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(线层, false);
        var 图 = 物体.GetComponent<Image>();
        图.color = 亮 ? 网格面板配色.物品边界色 : 网格面板配色.线条色;
        图.raycastTarget = false;
        var 矩形 = 物体.GetComponent<RectTransform>();
        矩形.anchorMin = new Vector2(0, 1);
        矩形.anchorMax = new Vector2(0, 1);
        矩形.pivot = new Vector2(0.5f, 0.5f);
        矩形.anchoredPosition = new Vector2(列 * 格尺寸, -(行 + 0.5f) * 格尺寸);
        矩形.sizeDelta = new Vector2(网格面板配色.线宽, 格尺寸);
    }

    // 横线 ─ 格边界：行 j 与 列 i 交点的一段（向右 格尺寸 宽）
    private void 画横线段(int 列, int 行, bool 亮)
    {
        var 物体 = new GameObject($"横线_{行}_{列}", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(线层, false);
        var 图 = 物体.GetComponent<Image>();
        图.color = 亮 ? 网格面板配色.物品边界色 : 网格面板配色.线条色;
        图.raycastTarget = false;
        var 矩形 = 物体.GetComponent<RectTransform>();
        矩形.anchorMin = new Vector2(0, 1);
        矩形.anchorMax = new Vector2(0, 1);
        矩形.pivot = new Vector2(0.5f, 0.5f);
        矩形.anchoredPosition = new Vector2((列 + 0.5f) * 格尺寸, -行 * 格尺寸);
        矩形.sizeDelta = new Vector2(格尺寸, 网格面板配色.线宽);
    }

    // 物品：两层结构 —— ① 物品框（全尺寸 Image = 品质底层色 + 黑描边，点击/拖拽挂这里）→ ② 内容层（内缩 Image = 深色占位块，将来贴美术图）。
    // 品质色永远在框层：内容层内缩 物品边距，无论现在是色块还是将来的美术图，四周都会露出品质色环。
    // 返回 物品框（增量刷新：键=堆叠实例，复用更新；null = 数据缺失不创建）
    private 物品框 创建物品(物品堆叠 堆叠)
    {
        if (!数据.物品.TryGetValue(堆叠.标识, out var 物品)) return null;
        var 框 = new 物品框();
        var (宽, 高) = 服务.物品占格(堆叠);   // 领域规则：形状×旋转 → 占格
        // ① 物品框：全尺寸贴格（品质底层色；选中 = 选中底色）
        var 物体 = new GameObject($"物品_{物品.名称}", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(物品层, false);
        框.根 = 物体.GetComponent<RectTransform>();
        框.框图 = 物体.GetComponent<Image>();
        框.框图.color = 品质底层色(堆叠);   // 不再用选中底色
        var 边框 = 物体.AddComponent<Outline>();
        边框.effectColor = new Color(0f, 0f, 0f, 0.6f);
        边框.effectDistance = new Vector2(3f, -3f);
        框.根.anchorMin = new Vector2(0, 1);
        框.根.anchorMax = new Vector2(0, 1);
        框.根.pivot = new Vector2(0, 1);
        框.根.anchoredPosition = new Vector2(堆叠.列 * 格尺寸, -堆叠.行 * 格尺寸);
        框.根.sizeDelta = new Vector2(宽 * 格尺寸, 高 * 格尺寸);
        物体.AddComponent<RectMask2D>();   // 裁剪 cover 图标溢出（物品图片填满格子，超出部分裁掉）
        // ② 高光层：品质底 与 内容 之间（白色半透明，悬停时显示）
        var 高物体 = new GameObject("高光", typeof(RectTransform), typeof(Image));
        高物体.transform.SetParent(物体.transform, false);
        var 高图 = 高物体.GetComponent<Image>();
        高图.color = 网格面板配色.高光色;
        高图.raycastTarget = false;
        var 高矩 = 高物体.GetComponent<RectTransform>();
        高矩.anchorMin = Vector2.zero;
        高矩.anchorMax = Vector2.one;
        高矩.offsetMin = new Vector2(-网格面板配色.线宽 / 2f, -网格面板配色.线宽 / 2f);
        高矩.offsetMax = new Vector2(网格面板配色.线宽 / 2f, 网格面板配色.线宽 / 2f);   // 高光层覆盖到网格线条上
        高物体.SetActive(false);   // 默认隐藏，悬停显示
        框.高光层 = 高物体;
        // ③ 内容层（内缩：尺寸少 2×边距，向格内偏移——不压网格线、不叠品质环；将来替换为美术图 sprite）
        var 内容物体 = new GameObject("内容", typeof(RectTransform), typeof(Image));
        内容物体.transform.SetParent(物体.transform, false);
        var 内容图 = 内容物体.GetComponent<Image>();
        内容图.color = 网格面板配色.物品底色;
        内容图.raycastTarget = false;   // 不挡底层交互（点击/拖拽挂在物品框上）
        // 手动挂图：items.json 的 "图片" 引用 → 内容层显示精灵；无图/未挂 = 保持色块（品质色环在物品框层不受影响）
        var 未旋转 = 服务.形状解析?.Invoke(堆叠.标识) ?? new 物品形状(1, 1);   // 内容层用未旋转宽高（旋转由 rotation 承担）
        float 内容宽 = 未旋转.宽 * 格尺寸 - 网格面板配色.物品边距 * 2f;
        float 内容高 = 未旋转.高 * 格尺寸 - 网格面板配色.物品边距 * 2f;
        var 图标 = 物品图标服务.获取(物品.图片);
        if (图标 != null)
        {
            内容图.sprite = 图标;
            内容图.color = Color.white;          // 有图时不再用色底染色
            // 纯 cover：按 sprite 宽高比 等比放大至覆盖整个物品框（保持长宽比不变形；超出部分被 RectMask2D 居中裁剪）
            内容图.preserveAspect = false;
            float 图比 = 图标.bounds.size.x / 图标.bounds.size.y;
            float 框比 = 内容宽 / 内容高;
            if (图比 > 框比) 内容宽 = 内容高 * 图比;   // 图更宽扁：撑满高，宽超出（裁左右）
            else 内容高 = 内容宽 / 图比;               // 图更高瘦：撑满宽，高超出（裁上下）
        }
        var 内容矩形 = 内容物体.GetComponent<RectTransform>();
        内容矩形.anchorMin = new Vector2(0.5f, 0.5f);
        内容矩形.anchorMax = new Vector2(0.5f, 0.5f);
        内容矩形.pivot = new Vector2(0.5f, 0.5f);
        内容矩形.anchoredPosition = Vector2.zero;   // 居中于物品框
        内容矩形.sizeDelta = new Vector2(内容宽, 内容高);
        内容矩形.localRotation = Quaternion.Euler(0f, 0f, 堆叠.旋转 ? 90f : 0f);   // 图标跟随物品旋转 90°
        框.内容层 = 内容矩形;
        // 物品名文本：居中显示（美术资源缺失时以文本标识物品；叠加在物品块中央，不随内容层旋转）
        var 名称体 = new GameObject("名称", typeof(RectTransform), typeof(TextMeshProUGUI));
        名称体.transform.SetParent(物体.transform, false);
        var 名称矩 = 名称体.GetComponent<RectTransform>();
        名称矩.anchorMin = Vector2.zero;
        名称矩.anchorMax = Vector2.one;
        名称矩.offsetMin = Vector2.zero;
        名称矩.offsetMax = Vector2.zero;
        var 名称 = 名称体.GetComponent<TextMeshProUGUI>();
        名称.text = 物品.名称;
        名称.fontSize = Mathf.Clamp(格尺寸 * 0.22f, 14f, 44f);   // 字号随格尺寸（2K 基准）
        名称.alignment = TextAlignmentOptions.Center;
        名称.enableWordWrapping = true;
        名称.color = Color.white;
        名称.raycastTarget = false;
        框.名称 = 名称;
        // 耐久：有最大耐久的物品在格底显示 当前/最大；损坏变红
        int 耐久上限 = 档案.有效最大耐久(堆叠.标识);
        if (耐久上限 > 0)
        {
            var 耐体 = new GameObject("耐久", typeof(RectTransform), typeof(TextMeshProUGUI));
            耐体.transform.SetParent(物体.transform, false);
            var 耐 = 耐体.GetComponent<TextMeshProUGUI>();
            耐.text = 堆叠.当前耐久 <= 0 ? "损坏" : $"{堆叠.当前耐久}/{耐久上限}";
            耐.fontSize = 29f;
            耐.alignment = TextAlignmentOptions.Bottom;
            耐.color = 堆叠.当前耐久 <= 0 ? new Color(1f, 0.5f, 0.4f) : new Color(0.92f, 0.92f, 0.92f);
            耐.raycastTarget = false;
            var 耐矩 = 耐体.GetComponent<RectTransform>();
            耐矩.anchorMin = new Vector2(0, 0);
            耐矩.anchorMax = new Vector2(1, 0);
            耐矩.pivot = new Vector2(0.5f, 0);
            耐矩.anchoredPosition = new Vector2(0, 2f);
            耐矩.sizeDelta = new Vector2(-8f, 26f);
            框.耐久 = 耐;
            框.上次耐久 = 耐.text;
        }
        // 数量角标：可堆叠且数量>1 时，在右下角显示数字
        if (堆叠.数量 > 1)
        {
            var 数体 = new GameObject("数量", typeof(RectTransform), typeof(TextMeshProUGUI));
            数体.transform.SetParent(物体.transform, false);
            var 数 = 数体.GetComponent<TextMeshProUGUI>();
            数.text = 堆叠.数量.ToString();
            数.fontSize = 40f;
            数.alignment = TextAlignmentOptions.BottomRight;
            数.color = Color.white;
            数.raycastTarget = false;
            var 数矩 = 数体.GetComponent<RectTransform>();
            数矩.anchorMin = new Vector2(1, 0);
            数矩.anchorMax = new Vector2(1, 0);
            数矩.pivot = new Vector2(1, 0);
            数矩.anchoredPosition = new Vector2(-14f, -2f);
            数矩.sizeDelta = new Vector2(70f, 34f);
            框.数量 = 数;
            框.上次数量 = 堆叠.数量;
        }
        // ④ 点击（非按钮） + 拖拽（挂在物品框上）
        var 点击 = 物体.AddComponent<物品点击>();
        点击.堆叠 = 堆叠;
        点击.面板 = this;
        点击.物品框 = 框.根;   // 右键菜单定位参考（物品右边界）
        点击.内容层 = 内容矩形;   // 悬停放大
        点击.高光层 = 高物体;      // 悬停显示高光
        var 拖拽 = 物体.AddComponent<物品拖拽>();
        拖拽.堆叠 = 堆叠;
        拖拽.面板 = this;
        框.上次列 = 堆叠.列; 框.上次行 = 堆叠.行; 框.上次旋转 = 堆叠.旋转;   // 布局变化检测基准
        return 框;
    }

    // 更新已有物品框（增量）：位置/尺寸/旋转/品质色/数量/耐久 变化才改；不重建任何组件（性能核心：拖拽移动只改位置）
    private void 更新物品框(物品框 框, 物品堆叠 堆叠)
    {
        if (框 == null || 框.根 == null) return;
        var (宽, 高) = 服务.物品占格(堆叠);
        框.根.anchoredPosition = new Vector2(堆叠.列 * 格尺寸, -堆叠.行 * 格尺寸);
        float 新宽 = 宽 * 格尺寸, 新高 = 高 * 格尺寸;
        if (Mathf.Abs(框.根.sizeDelta.x - 新宽) > 0.01f || Mathf.Abs(框.根.sizeDelta.y - 新高) > 0.01f)
            框.根.sizeDelta = new Vector2(新宽, 新高);
        if (框.内容层 != null)
        {
            var 旋转 = Quaternion.Euler(0f, 0f, 堆叠.旋转 ? 90f : 0f);
            if (框.内容层.localRotation != 旋转) 框.内容层.localRotation = 旋转;
        }
        if (框.框图 != null)
        {
            var 色 = 品质底层色(堆叠);
            if (框.框图.color != 色) 框.框图.color = 色;
        }
        // 数量角标：>1 显示，否则隐藏
        if (框.数量 != null)
        {
            if (堆叠.数量 > 1)
            {
                if (!框.数量.gameObject.activeSelf) 框.数量.gameObject.SetActive(true);
                if (框.上次数量 != 堆叠.数量) { 框.数量.text = 堆叠.数量.ToString(); 框.上次数量 = 堆叠.数量; }
            }
            else if (框.数量.gameObject.activeSelf) { 框.数量.gameObject.SetActive(false); 框.上次数量 = 0; }
        }
        // 耐久：上限>0 显示（损坏变红），否则隐藏
        if (框.耐久 != null)
        {
            int 上限 = 档案.有效最大耐久(堆叠.标识);
            if (上限 > 0)
            {
                string 文本 = 堆叠.当前耐久 <= 0 ? "损坏" : $"{堆叠.当前耐久}/{上限}";
                if (框.上次耐久 != 文本) { 框.耐久.text = 文本; 框.上次耐久 = 文本; }
                if (!框.耐久.gameObject.activeSelf) 框.耐久.gameObject.SetActive(true);
                框.耐久.color = 堆叠.当前耐久 <= 0 ? new Color(1f, 0.5f, 0.4f) : new Color(0.92f, 0.92f, 0.92f);
            }
            else if (框.耐久.gameObject.activeSelf) { 框.耐久.gameObject.SetActive(false); 框.上次耐久 = null; }
        }
        框.上次列 = 堆叠.列; 框.上次行 = 堆叠.行; 框.上次旋转 = 堆叠.旋转;   // 更新布局基准（下一次 检测 是否 变化）
    }

    // 左上锚定定位：列/行 起点 + 宽×高 跨格
    private void 定位(RectTransform 矩形, int 列, int 行, int 宽, int 高)
    {
        矩形.anchorMin = new Vector2(0, 1);
        矩形.anchorMax = new Vector2(0, 1);
        矩形.pivot = new Vector2(0, 1);
        矩形.anchoredPosition = new Vector2(列 * 格尺寸, -行 * 格尺寸);
        矩形.sizeDelta = new Vector2(宽 * 格尺寸, 高 * 格尺寸);
    }

    // 品质底层色（物品框）：全部物品按有效品质整块着色（半透明）；普通 = 全透明，优秀~传奇 = 品质色混合。
    // 架构约定：品质色永远属于"物品框层"（全尺寸底层）——将来内容层换成美术图片后，四周仍露出品质色环。
    private Color 品质底层色(物品堆叠 堆叠)
    {
        if (堆叠 == null || !数据.物品.TryGetValue(堆叠.标识, out var 物品)) return new Color(0f, 0f, 0f, 0f);
        品质 档 = 有效品质(堆叠, 物品);
        if (档 == 品质.普通) return new Color(0f, 0f, 0f, 0f);   // 普通：全透明（无色块）
        var 色 = Color.Lerp(网格面板配色.物品底色, 品质工具.颜色(档), 0.55f);
        色.a = 网格面板配色.品质底色透明;   // 半透明（能看到底座格/分隔线，品质色仍是区分度）
        return 色;
    }

    // 拖拽代理底色：非普通 = 品质底层色；普通（全透明）→ 内容层色（不透明，跟手可见）
    private Color 物品品质底(物品堆叠 堆叠)
    {
        var 层色 = 品质底层色(堆叠);
        return 层色.a <= 0f ? 网格面板配色.物品底色 : 层色;
    }

    // 有效品质：堆叠品质覆盖（合成提升）优先，否则取物品模板品质
    private static 品质 有效品质(物品堆叠 堆叠, 物品数据 模板)
        => !string.IsNullOrEmpty(堆叠.品质) ? 数据解析.枚举<品质>(堆叠.品质) : 模板.品质档;

    // ===== 点击交互 =====

    private void 物品被点击(物品堆叠 堆叠, int 点击次数)
    {
        if (拖拽源 != null) return;   // 拖拽进行中，忽略点击（避免刷新网格销毁正在拖拽的物品框，导致 OnEndDrag 丢失）
        右键菜单.实例?.隐藏();   // 左键点击：先关闭右键菜单
        选中 = 堆叠;
        if (点击次数 >= 2)
        {
            快捷操作(堆叠);   // 双击：操作（使用/装备/打开容器）
            选中 = null;
            请求刷新();
        }
        // 单击：仅记录选中（详情由右键菜单「详情」呼出 信息面板 展示；不重建网格——否则销毁物品框会破坏双击的 clickCount 累积）
    }

    // 右键物品：选中 + 显示右键小菜单（场景手动搭建，按物品特性显示按钮；菜单定位在物品右边界外侧）
    private void 物品右键(物品堆叠 堆叠, RectTransform 物品框)
    {
        if (堆叠 == null) return;
        选中 = 堆叠;
        if (右键菜单.实例 != null)
        {
            右键菜单.实例.目标面板 = this;   // 操作目标 = 发起右键的面板（主背包/容器面板 各自正确）
            右键菜单.实例.显示(堆叠, 物品框);
        }
    }

    // ===== 右键菜单公开操作（菜单按钮点击 → 此处；作用于当前 选中） =====
    public void 菜单使用() => 使用选中();
    public void 菜单装备() => 装备选中();
    public void 菜单打开() { if (选中 != null) 打开容器(选中); }

    // 丢弃：移除 选中 物品（容器物品则连内容一起丢弃），发布 失去 事件
    public void 菜单丢弃()
    {
        if (选中 == null || 选中.列 < 0) return;
        var 丢 = 选中;
        选中 = null;
        服务.背包.Remove(丢);
        请求刷新();
        音效管理器.实例?.播放成功();   // 丢弃成功 → 按钮成功音效
        ServiceRegistry.Get<EventBus>().发布(new 背包变化事件(丢.标识, -丢.数量, 变化原因.失去));
    }

    // 右键菜单「拆分」：打开拆分面板（选中 堆叠数量>1 时）
    public void 菜单打开拆分()
    {
        if (选中 == null) { ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.反馈坏, "[背包] 拆分：未选中物品（右键目标面板未匹配）。")); return; }
        if (选中.数量 <= 1) { ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.反馈坏, $"[背包] 拆分：{选中.标识} 数量 {选中.数量} ≤ 1，不可拆分。")); return; }
        var 面板 = 拆分面板.实例;
        if (面板 == null)
        {
            var 全部 = FindObjectsOfType<拆分面板>(true);   // 含未激活物体（物体整体隐藏时 Awake 未跑，实例未登记）
            if (全部.Length > 0) 面板 = 全部[0];
        }
        if (面板 == null)
        {
            ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.反馈坏, "[背包] 场景未搭建 拆分面板（拆分面板 组件 + 面板根）。"));
            return;
        }
        面板.打开(选中, this);
    }

    // 拆分面板确认 → 执行拆分（安全放置：本网格空位优先；容器拆分时容器满 → 兜底统一放入（穿戴容器 → 仓库）；都满 → 不允许）
    public void 菜单拆分(int 数量)
    {
        if (选中 == null || 数量 <= 0 || 数量 >= 选中.数量) return;
        bool 容器视图 = 数据源 != null;   // 数据源非空 = 外部视图（容器/仓库/穿戴容器）
        var 事件 = ServiceRegistry.Get<EventBus>();
        // ① 本网格拆分（容器/仓库/穿戴容器 内）：拆出的新堆叠 自动找本网格空格
        var 新 = 服务.拆分(选中, 数量);
        if (新 != null)
        {
            音效管理器.实例?.播放成功();
            选中 = null;
            请求刷新();
            return;
        }
        // ② 容器无空位 → 兜底统一放入（穿戴容器 → 仓库；源堆叠仍在容器内减量，新堆叠进 穿戴容器/仓库）
        if (容器视图)
        {
            var 新堆叠 = new 物品堆叠(选中.标识, 数量) { 旋转 = 选中.旋转, 当前耐久 = 选中.当前耐久, 品质 = 选中.品质, 词缀 = 选中.词缀 != null ? new List<词缀条>(选中.词缀) : null };
            选中.数量 -= 数量;
            int 实放 = 档案.放入堆叠(新堆叠);
            if (实放 > 0)
            {
                音效管理器.实例?.播放成功();
                选中 = null;
                请求刷新();
                事件?.发布(new 日志事件(日志类型.反馈, $"[背包] 容器已满，拆分出的 {新堆叠.标识}×{数量} 放入穿戴容器/仓库。"));
                事件?.发布(new 背包变化事件(新堆叠.标识, 实放, 变化原因.获得));
                return;
            }
            选中.数量 += 数量;   // 都满 → 回滚源数量（安全保护：不丢物品）
        }
        // ③ 容器与 穿戴容器/仓库 均无空位 → 不允许拆分（安全保护：不丢物品）
        音效管理器.实例?.播放失败();
        事件?.发布(new 日志事件(日志类型.反馈坏, "[背包] 拆分失败：容器与 穿戴容器/仓库 均已满，无空位放置拆出物品。"));
    }

    // 悬停：内容图放大 1.05 + 高光层显示（替代选中底色）
    private void 悬停(RectTransform 内容层, GameObject 高光层, bool 进入)
    {
        if (内容层 != null) 内容层.localScale = 进入 ? new Vector3(1.05f, 1.05f, 1f) : Vector3.one;
        if (高光层 != null) 高光层.SetActive(进入);
    }

    // 双击快捷操作：恢复品=使用；装备类=换装；容器=打开；技能书=学习
    private void 快捷操作(物品堆叠 堆叠)
    {
        if (!数据.物品.TryGetValue(堆叠.标识, out var 物品)) return;
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        if (容器服务.是容器(堆叠)) { 打开容器(堆叠); return; }
        if (物品.恢复量 > 0) { 使用选中(); return; }
        if (!string.IsNullOrEmpty(物品.槽位)) { 装备选中(); return; }
        if (物品.类型 == "技能书") { 面板操作.学习技能书(档案, 物品); return; }
        音效管理器.实例?.播放失败();
    }

    // 双击容器物品：动态搭建容器面板（面板本身挂 Canvas 顶层，不受裁剪/遮挡；挂载父仅作初始位置参考）
    private void 打开容器(物品堆叠 堆叠)
    {
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        if (!容器服务.是容器(堆叠)) { 音效管理器.实例?.播放失败(); return; }
        容器服务.初始化容器(堆叠);
        音效管理器.实例?.播放成功();   // 打开容器 → 按钮成功音效（右键/双击 统一）
        // 挂载父 = 本面板 ScrollRect（Content→Viewport→ScrollRect），取左上角作为面板初始位置
        var 滚动 = 网格容器 != null ? 网格容器.parent?.parent : null;
        var 挂载父 = 滚动 != null ? (RectTransform)滚动 : 网格容器;
        容器面板.创建(挂载父, 堆叠);
    }

    // ===== 拖拽交互 =====

    // 按下进入拖拽：记录源物品 + 创建视觉（代理/投影/影子），立即开始
    private void 开始拖拽(物品堆叠 堆叠, PointerEventData 事件)
    {
        右键菜单.实例?.隐藏();   // 拖拽时关闭右键菜单
        音效管理器.实例?.播放拿起();   // 拿起物品音效
        拖拽源 = 堆叠;
        拖拽旋转 = 堆叠.旋转;
        落点有效 = false;
        上次判定有效 = false;   // 新拖拽：判定缓存失效（首次落点重算）
        // 记录全局活动拖拽（跨面板转移用）：发起面板 + 源服务 + 堆叠
        拖拽发起面板 = this;
        拖拽源服务 = 服务;
        拖拽中堆叠 = 堆叠;
        创建拖拽视觉(堆叠);
        拖拽移动(事件);
    }

    // 首次超过启动阈值：创建 跟手代理 + 落点投影 + 原位置影子
    private void 创建拖拽视觉(物品堆叠 堆叠)
    {
        // ① 跟手代理：挂 Canvas 顶层（不被 Viewport 裁剪、不被任何面板覆盖——跨面板拖拽可见）
        var 物体 = new GameObject("拖拽代理", typeof(RectTransform), typeof(Image));
        var 顶层 = GetComponentInParent<Canvas>();
        物体.transform.SetParent(顶层 != null ? 顶层.transform : transform.root, false);
        var 图 = 物体.GetComponent<Image>();
        图.raycastTarget = false;
        // 代理优先显示挂载图标；无图则用品质底色块（半透明跟手）
        var 代理物品 = 数据.物品.TryGetValue(堆叠.标识, out var 代理数据) ? 代理数据 : null;
        var 代理图标 = 代理物品 != null ? 物品图标服务.获取(代理物品.图片) : null;
        if (代理图标 != null)
        {
            图.sprite = 代理图标;
            图.color = new Color(1f, 1f, 1f, 0.85f);
            图.preserveAspect = true;
        }
        else
        {
            var 代理底 = 物品品质底(堆叠);
            图.color = new Color(代理底.r, 代理底.g, 代理底.b, 0.85f);
        }
        拖拽代理 = 物体.GetComponent<RectTransform>();
        拖拽代理.anchorMin = new Vector2(0.5f, 0.5f);   // Canvas 顶层：中心锚定，屏幕坐标定位
        拖拽代理.anchorMax = new Vector2(0.5f, 0.5f);
        拖拽代理.pivot = new Vector2(0.5f, 0.5f);   // 中心跟随鼠标
        // ② 落点投影（绿/红，贴格）——复用懒创建，尺寸由 更新代理尺寸 设置
        确保投影();
        更新代理尺寸();
        // ③ 原位置半透明影子（虚影：物品将离开的位置；内缩尺寸与物品一致）
        var 影体 = new GameObject("原位置影子", typeof(RectTransform), typeof(Image));
        影体.transform.SetParent(物品层, false);
        var 影图 = 影体.GetComponent<Image>();
        影图.color = new Color(0.65f, 0.65f, 0.7f, 0.3f);   // 半透明灰
        影图.raycastTarget = false;
        var 影矩形 = 影体.GetComponent<RectTransform>();
        影矩形.anchorMin = new Vector2(0, 1);
        影矩形.anchorMax = new Vector2(0, 1);
        影矩形.pivot = new Vector2(0, 1);
        影矩形.anchoredPosition = new Vector2(堆叠.列 * 格尺寸 + 网格面板配色.物品边距, -堆叠.行 * 格尺寸 - 网格面板配色.物品边距);
        var (影宽, 影高) = 服务.物品占格(堆叠);   // 影子 = 物品原本占格（原始旋转；旋转预览不影响它）
        影矩形.sizeDelta = new Vector2(影宽 * 格尺寸 - 网格面板配色.物品边距 * 2f, 影高 * 格尺寸 - 网格面板配色.物品边距 * 2f);
        原位置影子 = 影体;
        拖拽代理.SetAsLastSibling();   // 代理置顶渲染——否则同格的落点投影（后创建）会盖住它
    }

    // 屏幕点 → 相对容器左下（**逻辑单位**，除以 Canvas 缩放——与 格尺寸 同基准，任何分辨率/缩放下都准）。
    // 以 物品层(=网格尺寸) 为基准：内容在 Content 里居中时仍与物品坐标对齐，拖拽不错位。
    private bool 屏幕到容器相对(PointerEventData 事件, out Vector2 相对, out Vector2 容器尺寸)
    {
        var 基准 = 物品层 != null ? 物品层 : 网格容器;
        容器尺寸 = 基准.rect.size;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(基准, 事件.position, 事件.pressEventCamera, out var 世界点))
        {
            相对 = Vector2.zero;
            return false;
        }
        var 原点 = 基准.TransformPoint(new Vector3(-基准.rect.width * 基准.pivot.x, -基准.rect.height * 基准.pivot.y, 0f));
        var 缩放 = 基准.lossyScale;
        相对 = new Vector2((世界点.x - 原点.x) / 缩放.x, (世界点.y - 原点.y) / 缩放.y);   // 逻辑单位（x 向右、y 向上）
        return true;
    }

    // 拖拽中：物品图片吸附鼠标所在格（格内锁定不移动，跨格才跳）→ 投影贴格同格；R 键旋转由 Update 每帧检测
    private void 拖拽移动(PointerEventData 事件)
    {
        if (拖拽源 == null || 拖拽代理 == null) return;
        // 物品图始终跟随鼠标（挂 Canvas 顶层：跨面板拖拽不消失、不被遮挡）
        拖拽代理.gameObject.SetActive(true);
        var 顶层 = GetComponentInParent<Canvas>();
        if (顶层 != null)
        {
            Vector2 局部;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)顶层.transform, 事件.position, 事件.pressEventCamera, out 局部))
                拖拽代理.anchoredPosition = 局部;
            else 拖拽代理.position = 事件.position;
        }
        else 拖拽代理.position = 事件.position;
        // 跨面板：鼠标在别的面板（容器）上 → 本面板不显示投影（代理仍跟手）
        var 下方面板 = 事件下方面板(事件);
        if (下方面板 != null && 下方面板 != this)
        {
            落点有效 = false;
            if (落点投影 != null) 落点投影.gameObject.SetActive(false);
            return;
        }
        // 拖到 装备槽（装备区）→ 槽位高亮提示（绿=槽位兼容 / 红=不兼容），本面板不显示网格投影
        if (装备面板.实例 != null && 装备面板.实例.命中槽位(事件.position, out var 槽位名))
        {
            落点有效 = false;
            if (落点投影 != null) 落点投影.gameObject.SetActive(false);
            bool 匹配 = 数据.物品.TryGetValue(拖拽源.标识, out var 装备) && 面板操作.槽位匹配(装备.槽位, 槽位名);
            装备面板.实例.高亮槽位(槽位名, 匹配);
            return;
        }
        装备面板.实例?.清除全部高亮();
        if (!屏幕到容器相对(事件, out var 相对, out var 尺寸))
        {
            if (落点投影 != null) 落点投影.gameObject.SetActive(false);
            return;
        }
        // 投影格 = 物品中心对齐（四舍五入：偏差对称 ±半格内，1×1 精确——大物体不错位）；越界贴边（右/下侧可放）
        var (物宽, 物高) = 预览占格(拖拽源);   // 按 拖拽旋转（R 预览）计算，旋转后投影/影子同步变化
        float 相对顶 = 尺寸.y - 相对.y;
        if (!落格(相对.x, 相对顶, 物宽, 物高, out int 列, out int 行))
        {
            落点有效 = false;
            if (落点投影 != null) 落点投影.gameObject.SetActive(false);
            上次判定有效 = false;   // 出网格：缓存失效（回网格重算）
            return;   // 物品比网格大 或 网格未渲染：隐藏投影（代理仍跟手）
        }
        落点有效 = true; 落点列 = 列; 落点行 = 行;   // 记录本次投影格（放下用）
        // ② 落点投影：吸附网格贴格（鼠标在格内投影不移动，跨格才跳；绿/红/蓝指示落格合法性：可放/不可放/可合并）
        // 判定缓存：落点格/旋转 未变 → 复用上次结果（该格物品/可合并/可存入容器/区域互换 都是重算法，拖拽中每帧重算很贵）
        if (!(列 == 上次判定列 && 行 == 上次判定行 && 拖拽旋转 == 上次判定旋转 && 上次判定有效))
        {
            上次判定列 = 列; 上次判定行 = 行; 上次判定旋转 = 拖拽旋转; 上次判定有效 = true;
            var 目标 = 该格物品(列, 行);
            if (目标 != null && 目标 != 拖拽源 && 服务.可合并(目标, 拖拽源)) { 上次可放 = true; 上次可合并 = true; }
            else if (可存入容器(目标, 拖拽源)) { 上次可放 = true; 上次可合并 = false; }   // 目标格是容器物品且可存入 → 绿（松手=存入而非换位）
            else if (目标 != null && 目标 != 拖拽源 && ServiceRegistry.Get<容器服务>().是容器(目标)) { 上次可放 = false; 上次可合并 = false; }   // 严格：容器不做换位目标 → 红
            else { 上次可放 = 服务.区域可互换(拖拽源, 列, 行, 拖拽旋转); 上次可合并 = false; }   // 覆盖 移动(空区) + 整体换位(多物品/被占区)
        }
        if (落点投影 != null)
        {
            落点投影.gameObject.SetActive(true);
            落点投影.rectTransform.anchoredPosition = new Vector2(列 * 格尺寸, -行 * 格尺寸);   // 精确贴格（吸附网格）
            落点投影.color = 上次可合并 ? 网格面板配色.合并色 : (上次可放 ? 网格面板配色.放置可色 : 网格面板配色.放置禁色);
        }
    }

    // 代理尺寸 = 物品内缩（跟手图片）；投影尺寸 = 完整占格（贴格指示）——随 拖拽旋转（R 预览）同步；原位置影子保持原始占格不变
    private void 更新代理尺寸()
    {
        if (拖拽代理 == null || 拖拽源 == null) return;
        var 未旋转 = 服务.形状解析?.Invoke(拖拽源.标识) ?? new 物品形状(1, 1);
        var (宽, 高) = 预览占格(拖拽源);   // 按 拖拽旋转 计算（不写回 堆叠.旋转）
        // 代理：未旋转内缩宽高 + 随 拖拽旋转 转 90°（跟手图片跟随旋转预览）
        拖拽代理.sizeDelta = new Vector2(未旋转.宽 * 格尺寸 - 网格面板配色.物品边距 * 2f, 未旋转.高 * 格尺寸 - 网格面板配色.物品边距 * 2f);
        拖拽代理.localRotation = Quaternion.Euler(0f, 0f, 拖拽旋转 ? 90f : 0f);
        if (落点投影 != null) 落点投影.rectTransform.sizeDelta = new Vector2(宽 * 格尺寸, 高 * 格尺寸);   // 投影贴格（旋转后）
        // 注：原位置影子不更新——它表示物品原本的占格（原始旋转），旋转预览只影响新位置
    }

    // 拖拽预览占格：按 拖拽旋转（R 预览）计算宽高（不写回 堆叠.旋转；拖拽开始时与物品当前旋转一致）
    private (int 宽, int 高) 预览占格(物品堆叠 堆叠)
    {
        var 未旋转 = 服务.形状解析?.Invoke(堆叠.标识) ?? new 物品形状(1, 1);
        return 拖拽旋转 ? (未旋转.高, 未旋转.宽) : (未旋转.宽, 未旋转.高);
    }

    // R 键检测（拖拽中旋转预览）：兼容新(InputSystem)/旧(Input Manager)
    private bool 检测按R()
    {
        bool 按下 = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) 按下 = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.R)) 按下 = true;
#endif
        return 按下;
    }

    // 结束拖拽：按拖拽中最后投影的落格 → 空格=移动(带旋转) / 被占=换位 / 网格外=取消 / 别的面板上=跨网格转移。
    // 音效：成功 播放下，失败 只播失败（不再无条件播放下）。
    private void 结束拖拽(PointerEventData 事件)
    {
        var 源 = 拖拽源;
        拖拽源 = null;
        清理所有面板投影();   // 隐藏其他面板（容器）跨面板显示的投影
        装备面板.实例?.清除全部高亮();   // 装备槽高亮（背包↔装备区拖拽提示）
        if (拖拽代理 != null) { Destroy(拖拽代理.gameObject); 拖拽代理 = null; }
        if (落点投影 != null) { Destroy(落点投影.gameObject); 落点投影 = null; }
        if (原位置影子 != null) { Destroy(原位置影子); 原位置影子 = null; }
        if (源 == null) { 拖拽发起面板 = null; return; }
        bool 成功 = false;   // 各分支统一：成功 播放下，失败 只播失败
        // 跨面板：先找鼠标下方是否有别的 网格面板（容器面板/穿戴容器块/仓库）
        var 目标面板 = 事件下方面板(事件);
        if (目标面板 != null && 目标面板 != this)
        {
            成功 = 目标面板.接收跨面板转移(服务, 源, 事件);
            if (成功) 音效管理器.实例?.播放放下();
            else 音效管理器.实例?.播放失败();
            拖拽发起面板 = null; 拖拽源服务 = null; 拖拽中堆叠 = null;
            return;
        }
        拖拽发起面板 = null; 拖拽源服务 = null; 拖拽中堆叠 = null;
        // 容器面板 底座拦截：鼠标在 容器面板 上 → 落点归面板（不触发底下装备槽/面板）
        foreach (var 面板 in FindObjectsOfType<容器面板>(true))
            if (面板.命中(事件.position)) { 音效管理器.实例?.播放失败(); return; }
        // 拖到装备槽位（装备面板）→ 穿戴：槽位兼容才可穿（实例级换装到"命中槽位"；换装堆叠 内部 已播放下音效）
        if (装备面板.实例 != null && 装备面板.实例.命中槽位(事件.position, out var 槽位名) && 源 != null)
        {
            成功 = 数据.物品.TryGetValue(源.标识, out var 装备) && 面板操作.槽位匹配(装备.槽位, 槽位名)
                && 面板操作.换装堆叠(档案, 源, 槽位名, 服务);   // 指定目标槽 + 源服务（穿戴容器/主背包）
            if (成功) 请求刷新();
            else 音效管理器.实例?.播放失败();
            return;
        }
        if (!落点有效)
        {
            音效管理器.实例?.播放失败();   // 拖出网格外（无有效投影）= 取消
            return;
        }
        // 与拖拽中一致：直接用最后投影的落格（投影在哪就放在哪，不随松手鼠标重算）
        int 列 = 落点列, 行 = 落点行;
        var 目标物品 = 该格物品(列, 行);
        // 同标识可堆叠 → 合并；目标格是容器物品且允许+有空位 → 存入容器（而不是换位）；
        // 目标格是容器物品但不可存入 → 严格失败（容器不做换位目标）；否则 区域交换（空区=移动 / 被占区=整体换位）
        if (服务.合并堆叠(目标物品, 源) > 0) 成功 = true;
        else if (存入容器(目标物品, 源)) 成功 = true;   // 内部已处理 刷新/事件
        else if (目标物品 != null && 目标物品 != 源 && ServiceRegistry.Get<容器服务>().是容器(目标物品)) { }
        else if (服务.区域互换(源, 列, 行, 拖拽旋转)) 成功 = true;
        if (成功) { 请求刷新(); 音效管理器.实例?.播放放下(); }
        else 音效管理器.实例?.播放失败();
    }

    // 判定：目标格是容器物品 且 允许该类型 且 非容器类物品 且 内部有空位 → 可存入（投影显示绿色）
    private bool 可存入容器(物品堆叠 目标容器, 物品堆叠 堆叠)
    {
        if (目标容器 == null || 目标容器 == 堆叠) return false;
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        if (!容器服务.是容器(目标容器)) return false;
        if (!容器服务.允许放入(目标容器, 堆叠.标识)) return false;
        if (容器服务.是容器(堆叠)) return false;   // 嵌套限制：容器类物品不自动存入（需打开后手动放入）
        var 视图 = 容器服务.打开(目标容器);
        return 视图.寻找可放置格(堆叠) != null;
    }

    // 拖拽源 放到 目标容器物品 上：容器允许该类型 且 内部有空位 → 存入容器（而不是换位/失败）。
    // 跨网格转移 复用：目标=容器视图（其 背包 与 容器.容器物品 同一引用，直接写入容器内部）。
    private bool 存入容器(物品堆叠 目标容器, 物品堆叠 堆叠)
    {
        if (!可存入容器(目标容器, 堆叠)) return false;
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        var 视图 = 容器服务.打开(目标容器);
        var 空位 = 视图.寻找可放置格(堆叠);
        int 转移 = 容器服务.跨网格转移(服务, 堆叠, 视图, 空位.Value.列, 空位.Value.行);
        if (转移 <= 0) return false;
        ServiceRegistry.Get<EventBus>().发布(new 背包变化事件(堆叠.标识, 转移, 变化原因.获得));   // 让打开的容器面板刷新
        return true;
    }

    // 鼠标下方的 网格面板：用矩形范围判断（不依赖 raycastTarget），返回命中面面板（含自己）。
    // 主背包：检查 网格容器 矩形；容器面板：检查其 面板根 矩形（更大，拖到面板任意处都能命中）
    // 登记表遍历（替代 FindObjectsOfType——拖拽中每帧调用，FindObjectsOfType 很慢）
    private 网格面板 事件下方面板(PointerEventData 事件)
    {
        网格面板 命中 = null;
        float 最小面积 = float.MaxValue;
        foreach (var 面板 in 全部面板)
        {
            if (面板 == null || !面板.gameObject.activeInHierarchy) continue;
            RectTransform 矩形;
            if (面板.所属容器 != null)
            {
                var 容器面板 = 面板.GetComponentInParent<容器面板>();
                矩形 = 容器面板 != null ? (RectTransform)容器面板.transform : 面板.网格容器;
            }
            else 矩形 = 面板.网格容器;
            if (矩形 == null) continue;
            if (!RectTransformUtility.RectangleContainsScreenPoint(矩形, 事件.position, 事件.pressEventCamera)) continue;
            // 多个面板重叠时取最上层（面积最小 = 视觉最前）
            float 面积 = 矩形.rect.width * 矩形.rect.height;
            if (面积 < 最小面积) { 最小面积 = 面积; 命中 = 面板; }
        }
        return 命中;
    }

    // 接收跨面板转移：把 (源服务) 里的 堆叠 放到本面板 (列,行)（由 容器服务.跨网格转移 执行）。返回 是否成功（音效由调用方播）
    public bool 接收跨面板转移(背包服务 源服务, 物品堆叠 堆叠, PointerEventData 事件)
    {
        if (源服务 == null || 堆叠 == null) return false;
        if (!屏幕到容器相对(事件, out var 相对, out var 尺寸)) return false;
        var (物宽, 物高) = 服务.物品占格(堆叠);
        float 相对顶 = 尺寸.y - 相对.y;
        if (!落格(相对.x, 相对顶, 物宽, 物高, out int 列, out int 行)) return false;
        // 转移前校验：目标容器类型限制 + 嵌套防护（自己套自己/循环/套娃上限）
        if (!目标允许放入(堆叠.标识)) return false;
        if (目标禁放入(堆叠)) return false;
        // 快捷收入：落点格 是 容器物品（如 背包里 的 医疗箱）→ 直接存入该容器（无需打开容器面板；同面板拖拽已有，跨面板补齐）
        var 落点物 = 服务.该格物品(列, 行);
        if (落点物 != null && 落点物 != 堆叠 && 可存入容器(落点物, 堆叠))
        {
            var 视图 = ServiceRegistry.Get<容器服务>().打开(落点物);
            var 空位 = 视图.寻找可放置格(堆叠);
            if (空位 != null)
            {
                int 存入数 = ServiceRegistry.Get<容器服务>().跨网格转移(源服务, 堆叠, 视图, 空位.Value.列, 空位.Value.行);
                if (存入数 > 0)
                {
                    请求刷新();
                    ServiceRegistry.Get<EventBus>().发布(new 背包变化事件(堆叠.标识, 存入数, 变化原因.获得));
                    return true;
                }
            }
            return false;   // 容器不允许类型 / 已满 → 失败
        }
        int 转移 = ServiceRegistry.Get<容器服务>().跨网格转移(源服务, 堆叠, 服务, 列, 行);
        if (转移 <= 0) return false;
        请求刷新();
        ServiceRegistry.Get<EventBus>().发布(new 背包变化事件(堆叠.标识, 转移, 变化原因.获得));
        return true;
    }

    // 该格被哪个物品覆盖（领域判定，UI 只查询不计算）
    private 物品堆叠 该格物品(int 列, int 行) => 服务.该格物品(列, 行);

    // 动态容器面板：绑定网格容器（运行时覆盖 SerializeField），并重置分层缓存（下次刷新重建到新容器下）
    public void 绑定网格容器(RectTransform 容器)
    {
        网格容器 = 容器;
        底座层 = 线层 = 物品层 = null;   // 强制下次 准备层 在新容器下重建
    }

    // 外部视图显示配置（容器面板/仓库区/穿戴容器区 注入）：格尺寸 固定常量（网格面板.格尺寸=100，全项目统一）；
    // 尺寸由 服务.网格列/行 决定（外部视图：固定列数/行数 清零，用服务尺寸）
    public void 配置视图显示()
    {
        固定列数 = 0;     // 用 服务.网格列
        固定行数 = 0;     // 用 服务.网格行
    }

    // 屏幕点 是否在本面板网格矩形内（装备槽拖拽落点判断用）
    public bool 屏幕命中(Vector2 屏幕点)
    {
        var 矩形 = 网格容器;
        if (矩形 == null) return false;
        var 画布 = 矩形.GetComponentInParent<Canvas>();
        var 相机 = 画布 != null && 画布.renderMode != RenderMode.ScreenSpaceOverlay ? 画布.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(矩形, 屏幕点, 相机);
    }

    // 屏幕相对 点 → 网格落格（物品中心对齐：四舍五入，偏差对称 ±半格内，1×1 精确——大物体不错位）
    private bool 落格(float 相对x, float 相对顶, int 物宽, int 物高, out int 列, out int 行)
    {
        列 = Mathf.RoundToInt((相对x - 物宽 * 格尺寸 / 2f) / 格尺寸);
        行 = Mathf.RoundToInt((相对顶 - 物高 * 格尺寸 / 2f) / 格尺寸);
        return 列 >= 0 && 行 >= 0 && 列 < 当前列 && 行 < 当前行;
    }

    // 屏幕点 → 网格格坐标（装备拖拽落点用；物品中心对齐 + 越界贴边，同背包内拖拽判定）
    public bool 屏幕到格(Vector2 屏幕点, 物品堆叠 堆叠, out int 列, out int 行)
    {
        列 = 行 = -1;
        if (堆叠 == null) return false;
        var 伪事件 = new PointerEventData(EventSystem.current) { position = 屏幕点 };
        if (!屏幕到容器相对(伪事件, out var 相对, out var 尺寸)) return false;
        var (物宽, 物高) = 服务.物品占格(堆叠);
        float 相对顶 = 尺寸.y - 相对.y;
        return 落格(相对.x, 相对顶, 物宽, 物高, out 列, out 行);
    }

    // 当前面板网格服务（装备拖拽放置用：主背包=档案背包服务 / 容器=容器视图）
    public 背包服务 视图服务 => 服务;

    // —— 装备拖拽投影（装备区拖装备 → 背包网格：落格投影，可放绿/不可放红，同背包内拖拽）——
    public void 显示装备拖拽投影(物品堆叠 堆叠, Vector2 屏幕点, bool 强制禁 = false)
    {
        确保投影();
        if (落点投影 == null) return;
        if (强制禁)   // 保护（如"穿戴容器不能放进自己容器"）→ 全网格红框
        {
            落点投影.gameObject.SetActive(true);
            落点投影.rectTransform.anchoredPosition = Vector2.zero;
            落点投影.rectTransform.sizeDelta = new Vector2(当前列 * 格尺寸, 当前行 * 格尺寸);
            落点投影.color = 网格面板配色.放置禁色;
            return;
        }
        if (堆叠 == null || !屏幕到格(屏幕点, 堆叠, out var 列, out var 行))
        {
            落点投影.gameObject.SetActive(false);
            return;
        }
        var (宽, 高) = 服务.物品占格(堆叠);
        落点投影.gameObject.SetActive(true);
        落点投影.rectTransform.anchoredPosition = new Vector2(列 * 格尺寸, -行 * 格尺寸);
        落点投影.rectTransform.sizeDelta = new Vector2(宽 * 格尺寸, 高 * 格尺寸);
        落点投影.color = 服务.可放置(堆叠.标识, 列, 行, 堆叠.旋转) ? 网格面板配色.放置可色 : 网格面板配色.放置禁色;
    }

    public void 隐藏装备拖拽投影()
    {
        if (落点投影 != null) 落点投影.gameObject.SetActive(false);
    }

    // ===== 操作按钮 =====

    private void 使用选中()
    {
        if (选中 == null) return;
        if (!数据.物品.TryGetValue(选中.标识, out var 物品) || 物品.恢复量 <= 0) { 音效管理器.实例?.播放失败(); return; }
        面板操作.使用恢复(档案, 数据, 选中.标识);
        选中 = null;
        请求刷新();
    }

    private void 装备选中()
    {
        if (选中 == null) return;
        if (!数据.物品.TryGetValue(选中.标识, out var 物品) || string.IsNullOrEmpty(物品.槽位)) { 音效管理器.实例?.播放失败(); return; }
        if (面板操作.换装堆叠(档案, 选中, null, 服务)) 选中 = null;   // 实例级换装（源服务 = 本面板：穿戴容器/主背包）
        请求刷新();
    }

    // 右键菜单「详情」：呼出 信息面板 显示 选中 物品完整信息
    public void 菜单查看详情()
    {
        if (选中 == null) return;
        音效管理器.实例?.播放成功();   // 详情 → 按钮成功音效
        if (信息面板.实例 != null) 信息面板.实例.显示(选中, 服务);
    }

    // ===== 内部组件：非按钮点击 + 拖拽 =====

    private sealed class 物品点击 : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public 物品堆叠 堆叠;
        public 网格面板 面板;
        public RectTransform 物品框;    // 右键菜单定位参考（物品右边界）
        public RectTransform 内容层;   // 悬停放大
        public GameObject 高光层;      // 悬停显示高光
        public void OnPointerClick(PointerEventData 事件)
        {
            if (堆叠 == null || 面板 == null) return;
            if (事件.button == PointerEventData.InputButton.Right) { 面板.物品右键(堆叠, 物品框); return; }   // 右键 → 操作菜单
            面板.物品被点击(堆叠, 事件.clickCount);
        }
        public void OnPointerEnter(PointerEventData 事件) { if (面板 != null) 面板.悬停(内容层, 高光层, true); }
        public void OnPointerExit(PointerEventData 事件) { if (面板 != null) 面板.悬停(内容层, 高光层, false); }
    }

    private sealed class 物品拖拽 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public 物品堆叠 堆叠;
        public 网格面板 面板;
        public void OnBeginDrag(PointerEventData 事件) { if (堆叠 != null && 面板 != null) 面板.开始拖拽(堆叠, 事件); }
        public void OnDrag(PointerEventData 事件) { if (面板 != null) 面板.拖拽移动(事件); }
        public void OnEndDrag(PointerEventData 事件) { if (面板 != null) 面板.结束拖拽(事件); }
    }
}
