using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 网格面板：通用塔科夫式网格（主背包/仓库/容器/穿戴容器 复用），动态构建网格底座 + 物品层（纯 图像+文本，非按钮）。
// 架构（partial 分部，按职责拆分文件，本文件 = 门面/调度）：
//   网格面板.渲染.cs —— 网格结构 / 底格 / 分隔线 / 物品框 / 容器形状（块偏移/口袋框）
//   网格面板.拖拽.cs —— 拖拽代理 / 落点投影 / 跨面板 / R 旋转 / 落格
//   网格面板.交互.cs —— 点击 / 右键菜单 / 使用 / 装备 / 拆分 / 嵌套校验
// 本文件保留：字段 / 生命周期（Awake/Update/OnDestroy）/ 事件调度 / 公共 API / 内部交互组件。
// 数据源：null = 主背包（档案.背包服务）；非空 = 外部视图（容器/仓库/穿戴容器，由 容器面板/仓库区/穿戴容器区 注入）。
public sealed partial class 网格面板 : 面板基类
{
    [SerializeField] private RectTransform 网格容器;   // 网格区域（左上锚定；代码动态生成底座与物品）。可运行时 绑定网格容器 覆盖（动态容器面板）
    [SerializeField] private int 固定列数 = 6;         // Inspector 固定覆盖 服务.网格列（0 = 用服务尺寸；主背包测试参数）
    [SerializeField] private int 固定行数 = 10;        // Inspector 固定覆盖 服务.网格行（0 = 用服务尺寸；主背包测试参数）
    public const float 格尺寸 = 90f;                   // 单格像素：全项目统一 90（主背包/仓库/容器/穿戴容器 一致，2K 基准，90 兼顾格内文字可读性）
    [SerializeField] private int 固定列数最小 = 1;       // 固定列数（格子宽）下限
    [SerializeField] private int 固定列数最大 = 100;      // 固定列数上限
    [SerializeField] private int 固定行数最小 = 1;       // 固定行数下限
    [SerializeField] private int 固定行数最大 = 100;      // 固定行数上限
    // 注：配色参数（底座色/边界色/线条色/线宽/边距/投影色 等）统一定义在 网格面板配色 静态类——组件不暴露配色，改风格只改 网格面板配色.cs。

    private 玩家档案 档案 => ServiceRegistry.Get<PlayerService>().档案;
    private DataService 数据 => ServiceRegistry.Get<DataService>();
    private 物品堆叠 选中;
    private RectTransform 底盘;                   // 网格整体 衬底（整块 底盘图，比 网格层 四周 各大 1px）
    private RectTransform 底座层, 线层, 物品层;   // 网格分层（底座 / 分隔线 / 物品），Content 滚动容器

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
    // 渲染网格 实际 宽高（含 容器形状 块偏移 缝隙）：容器面板 适配 面板根 大小 用
    public float 渲染网格宽 => 当前列 * 格尺寸 + 最右偏移();
    public float 渲染网格高 => 当前行 * 格尺寸;

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
        Vector2 鼠标 = 输入鼠标位置();   // 方法级：R 旋转 与 跨面板投影 共用（避免 CS0136 重名）
        var 伪事件 = new PointerEventData(EventSystem.current) { position = 鼠标 };
        // R 旋转预览：放 Update 每帧检测（不受 OnDrag 需鼠标移动才触发的限制——静止按住也能按 R）。
        // 仅 发起面板 检测（避免多个激活面板同帧重复翻转 = 转两次等于没转）。
        if (拖拽发起面板 == this && 检测按R())
        {
            拖拽旋转 = !拖拽旋转;
            更新代理尺寸();
            // 静止按 R 不触发 OnDrag → 手动触发一次完整投影刷新（旋转后 落格/尺寸/颜色 立即同步，否则投影停在旧格位）
            拖拽移动(伪事件);
        }
        if (拖拽发起面板 == this)
            return;
        确保投影();   // 懒创建本面板投影（发起面板才有，其他面板首次需要时创建）
        if (落点投影 == null)
            return;

        var 下方 = 事件下方面板(伪事件);

        if (下方 != this)
            { 落点投影.gameObject.SetActive(false); return; }
        if (鼠标在容器面板底座上(伪事件))   // 容器面板 底座 阻隔：鼠标在 容器面板 上 → 本面板 不显示 投影
            { 落点投影.gameObject.SetActive(false); return; }
        if (!屏幕到容器相对(伪事件, out var 相对, out var 尺寸))
            { 落点投影.gameObject.SetActive(false); return; }

        var (物宽, 物高) = 预览占格(拖拽中堆叠);   // 按 拖拽旋转（R 预览，静态共享）→ 跨面板投影随旋转同步
        float 相对顶 = 尺寸.y - 相对.y;
        if (!落格(相对.x, 相对顶, 物宽, 物高, out int 列, out int 行))
            { 落点投影.gameObject.SetActive(false); return; }

        落点投影.gameObject.SetActive(true);
        落点投影.rectTransform.anchoredPosition = new Vector2(格x(列, 行), -行 * 格尺寸);
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

    void OnDestroy()
    {
        全部面板.Remove(this);   // 注销登记表
        if (ServiceRegistry.已注册<EventBus>())
        {
            var 事件 = ServiceRegistry.Get<EventBus>();
            事件.取消订阅<背包变化事件>(背包变化响应);
        }
    }

    // ===== 事件 / 刷新调度 =====

    private bool 待刷新网格, 待刷新物品;   // 脏标记：事件 → 标记，Update 合并刷新（网格=尺寸全量 / 物品=增量）

    // 事件响应：只标记脏，Update 统一刷新（优化：装备/转移同帧多个事件不重复重建）
    // 背包变化 → 物品增删移（增量；尺寸变由 刷新物品 内部兜底检测走全量）
    private void 背包变化响应(背包变化事件 _) => 待刷新物品 = true;

    // 外部请求刷新入口（穿戴容器区/仓库区/容器面板 注入数据源/尺寸后调用）：内部自动判定 全量重建 或 增量刷新
    public void 请求刷新() => 待刷新网格 = true;
    // 立即刷新（穿戴容器区 装备/卸下 后强制显隐生效用）：直接执行（低频结构变化，不走脏标记）
    public void 立即刷新() => 刷新网格();
    // 强制全量重建（配色修改后应用用）：清分层缓存 → 网格结构 全量重绘（底格/线/物品 用新配色）
    public void 强制重建()
    {
        if (底盘 != null) { Destroy(底盘.gameObject); 底盘 = null; }
        底座层 = 线层 = 物品层 = null;   // 强制 下次 刷新 重建 分层
        当前列 = 当前行 = 0;             // 强制 尺寸 检测 触发 重建网格结构
        上次格尺寸 = 0f;
        刷新网格();
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

    // 当前面板网格服务（装备拖拽放置用：主背包=档案背包服务 / 容器=容器视图）
    public 背包服务 视图服务 => 服务;

    // 动态容器面板：绑定网格容器（运行时覆盖 SerializeField），并重置分层缓存（下次刷新重建到新容器下）
    public void 绑定网格容器(RectTransform 容器)
    {
        网格容器 = 容器;
        if (底盘 != null) { Destroy(底盘.gameObject); 底盘 = null; }
        底座层 = 线层 = 物品层 = null;   // 强制下次 准备层 在新容器下重建
    }

    // 外部视图显示配置（容器面板/仓库区/穿戴容器区 注入）：格尺寸 固定常量（网格面板.格尺寸）；
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

    // 网格区域（搜索黑布 挂载点；搜索面板 用）
    public RectTransform 网格区域 => 网格容器;

    // 物品框矩形（搜索黑块 挂载点；按 堆叠 实例 查 当前框，未渲染/不存在 = null）
    public RectTransform 物品框矩形(物品堆叠 堆叠)
    {
        if (堆叠 == null) return null;
        return 物品框表.TryGetValue(堆叠, out var 框) ? 框.根 : null;
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
