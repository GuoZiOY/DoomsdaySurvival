using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 容器面板：预制体（静态搭建 UI）+ 动态数据注入 的浮动容器面板（塔科夫式）。
// 预制体（Assets/Resources/Prefab/容器面板.prefab）搭好 底座/标题/关闭按钮/网格容器（挂 网格面板），
// 本组件 暴露 引用（面板根/网格容器/容器网格/标题文本），动态控制：标题 文本、面板根 与 网格容器 尺寸（随 容器 内容 伸缩）。
// 样式（字号/颜色/间距/透明度）全在 预制体 调，改 预制体 即 生效。
// 关闭时销毁；与主背包跨网格拖拽转移（矩形判断，见 网格面板.事件下方面板）。
public sealed class 容器面板 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    // 已打开的容器实例 → 面板；同一容器不可重复打开（双击/连点防重）
    private static readonly System.Collections.Generic.Dictionary<物品堆叠, 容器面板> 已打开
        = new System.Collections.Generic.Dictionary<物品堆叠, 容器面板>();

    // ===== 布局常量（网格容器 居中锚；面板 尺寸 与 网格 位置 由 这些 预留 决定） =====
    private const float 顶部预留 = 76f;    // 网格 顶部 距 面板 顶部（标题/按钮 区 高度；改 预制体 时 相应 调 标题 位置）
    private const float 左右留白 = 16f;    // 网格 两侧 距 面板 边缘（水平 居中）
    private const float 底边距 = 16f;      // 网格 底部 距 面板 底缘
    private const float 最小面板宽 = 360f;       // 面板宽度下限（容纳顶部行：标题+按钮）
    private const float 最小面板高 = 250f;       // 面板高度下限（顶部行 + 2×2 网格 + 底边距）
    private const float 初始宽 = 480f, 初始高 = 420f;   // 创建时占位尺寸（显示容器时按网格覆盖）
    private const float 初始右偏比例 = 0.15f;    // 初始位置：挂载父左上 向右偏移比例（×挂载父宽）
    private const float 初始下偏 = 20f;          // 初始位置：向下偏移
    private const float 兜底右偏比例 = 0.4f;     // 位置换算失败兜底：Canvas 左上偏右比例
    private const string 预制体路径 = "Prefab/容器面板";   // Resources 路径（预制体 静态 搭建）

    // —— 预制体 引用（Inspector 拖好；实例化 后 自动 绑定） ——
    [SerializeField] private RectTransform 面板根;      // 面板根（含 Image 底座，可拖拽；尺寸 动态 控制）
    [SerializeField] private RectTransform 网格容器;    // 容器内部网格的 Content（尺寸 动态 控制）
    [SerializeField] private 网格面板 容器网格;          // 网格面板 组件（预制体 已 挂）
    [SerializeField] private TextMeshProUGUI 标题文本;   // 顶部 容器名（显示容器 时 赋值）
    [SerializeField] private Button 关闭按钮;            // 右上 关闭按钮（Awake 自动 挂 关闭()）

    private 物品堆叠 当前容器;
    private Vector2 拖拽偏移;

    // 预制体 引用 绑定：关闭按钮 自动 挂 关闭() + 注册 成功音效（动态实例化，音效管理器 场景扫描 覆盖不到）
    private void Awake()
    {
        if (关闭按钮 != null)
        {
            关闭按钮.onClick.AddListener(关闭);
            音效管理器.实例?.注册按钮(关闭按钮);   // 点击 → 按钮成功音效
        }
    }

    // 供 网格面板 双击时调用：实例化预制体 + 注入容器数据
    public static 容器面板 创建(RectTransform 挂载父, 物品堆叠 容器)
    {
        if (容器 == null) return null;
        // 防重：同一容器已打开 → 提到最上层并复用，不重复创建
        容器面板 已有;
        if (已打开.TryGetValue(容器, out 已有) && 已有 != null)
        {
            已有.面板根.SetAsLastSibling();   // 聚焦已有面板（顶到最上层）
            return 已有;
        }
        var 预制 = Resources.Load<容器面板>(预制体路径);
        if (预制 == null)
        {
            Debug.LogError($"[容器面板] 找不到预制体 Resources/{预制体路径}（请用 预制体 搭建容器面板 UI，并 拖好 引用）。");
            return null;
        }
        var 面板 = Instantiate(预制);
        // 挂 Canvas 顶层：不受 ScrollRect/Viewport 裁剪、不被任何面板覆盖（与跨面板拖拽代理一致）
        var 画布 = 挂载父.GetComponentInParent<Canvas>();
        var 顶层 = 画布 != null ? (RectTransform)画布.transform : 挂载父;
        面板.transform.SetParent(顶层, false);
        面板.transform.SetAsLastSibling();   // 容器面板在 Canvas 最上层（拖拽代理创建时再顶到其上）
        // 左上锚定 + 初始位置：挂载父 左上 → Canvas 局部坐标 → 右下偏移（避开原挂载点）
        var 根 = 面板.面板根;
        根.anchorMin = new Vector2(0, 1);
        根.anchorMax = new Vector2(0, 1);
        根.pivot = new Vector2(0, 1);
        var 相机 = 画布 != null && 画布.renderMode != RenderMode.ScreenSpaceOverlay ? 画布.worldCamera : null;
        Vector2 挂载父左上;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(顶层,
                RectTransformUtility.WorldToScreenPoint(相机, 挂载父.TransformPoint(new Vector3(挂载父.rect.xMin, 挂载父.rect.yMax, 0f))),
                相机, out 挂载父左上))
            根.anchoredPosition = 挂载父左上 - new Vector2(顶层.rect.xMin, 顶层.rect.yMax) + new Vector2(挂载父.rect.width * 初始右偏比例, -初始下偏);
        else
            根.anchoredPosition = new Vector2(顶层.rect.width * 兜底右偏比例, -初始下偏);   // 兜底：Canvas 左上偏右（屏幕内）
        根.sizeDelta = new Vector2(初始宽, 初始高);   // 初始占位（实际按容器网格尺寸在 显示容器 里覆盖）
        面板.当前容器 = 容器;
        已打开[容器] = 面板;   // 登记：同一容器只允许一个面板
        面板.显示容器(容器);
        return 面板;
    }

    // 显示容器：注入容器视图数据源并渲染（标题/网格尺寸/数据源——仅 动态 数据，样式 在 预制体）
    private void 显示容器(物品堆叠 容器)
    {
        当前容器 = 容器;
        var 服务 = ServiceRegistry.Get<容器服务>();
        服务.初始化容器(容器);
        var 视图 = 服务.打开(容器);
        容器网格.数据源 = 视图;
        容器网格.所属容器 = 容器;
        if (标题文本 != null) 标题文本.text = 容器.标识;   // 顶部 容器名（弹药箱/医疗箱…）
        容器网格.立即刷新();   // 同步重建（此刻 渲染网格宽/高 才是 容器 实际 尺寸——含 形状 块偏移）
        // 面板根尺寸 = 预留 + 网格 内容 尺寸；网格容器 居中锚 → 水平 居中、垂直 按 预留 定位。
        float 网格宽 = 容器网格.渲染网格宽;
        float 网格高 = 容器网格.渲染网格高;
        面板根.sizeDelta = new Vector2(
            Mathf.Max(最小面板宽, 网格宽 + 左右留白 * 2f),
            Mathf.Max(最小面板高, 顶部预留 + 网格高 + 底边距));
        网格容器.sizeDelta = new Vector2(网格宽, 网格高);
        // 居中锚 定位：网格 底部 距 面板 底缘 = 底边距（则 顶部 距 面板 顶 = 顶部预留，标题/按钮 区）
        网格容器.anchoredPosition = new Vector2(0f, (底边距 - 顶部预留) / 2f);
        限制在屏幕内();   // 面板尺寸定稿后自动校正位置，确保创建出来就在屏幕内
    }

    // 把面板位置限制在父（Canvas 顶层）范围内：创建后/拖拽时调用，防止面板出屏（出屏既看不见也点不到）
    private void 限制在屏幕内()
    {
        var 父 = 面板根.parent as RectTransform;
        if (父 == null) return;
        var 位置 = 面板根.anchoredPosition;
        float 父宽 = 父.rect.width, 父高 = 父.rect.height;
        float 面板宽 = 面板根.sizeDelta.x, 面板高 = 面板根.sizeDelta.y;
        位置.x = Mathf.Clamp(位置.x, 0f, Mathf.Max(0f, 父宽 - 面板宽));      // 贴左 → 贴右（父宽<面板宽时锁贴左兜底）
        位置.y = Mathf.Clamp(位置.y, Mathf.Min(0f, -(父高 - 面板高)), 0f);   // 贴顶 → 贴底
        面板根.anchoredPosition = 位置;
    }

    public void 关闭()
    {
        if (当前容器 != null) 已打开.Remove(当前容器);
        当前容器 = null;
        if (容器网格 != null) { 容器网格.数据源 = null; 容器网格.所属容器 = null; }
        Destroy(gameObject);
    }

    // 屏幕点 是否在 本面板根（底座）矩形 内——拖拽落点 拦截用：鼠标在 容器面板 上时，落点归面板，
    // 不 触发 底下 装备槽/其他 面板（底座 不可穿透）
    public bool 命中(Vector2 屏幕点)
    {
        if (面板根 == null) return false;
        var 画布 = 面板根.GetComponentInParent<Canvas>();
        var 相机 = 画布 != null && 画布.renderMode != RenderMode.ScreenSpaceOverlay ? 画布.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(面板根, 屏幕点, 相机);
    }

    // 屏幕点 是否在 本面板 网格容器 矩形 内——自己面板 拖拽 松手 判定用：
    // 网格内 松手 = 同面板 移动/换位（放行）；底座空白区（标题/边距，非网格）= 拦截（不穿透 下层）
    public bool 命中网格(Vector2 屏幕点)
    {
        if (网格容器 == null) return false;
        var 画布 = 网格容器.GetComponentInParent<Canvas>();
        var 相机 = 画布 != null && 画布.renderMode != RenderMode.ScreenSpaceOverlay ? 画布.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(网格容器, 屏幕点, 相机);
    }

    // 兜底：面板被其他方式销毁时同步清理登记
    private void OnDestroy()
    {
        if (当前容器 != null) 已打开.Remove(当前容器);
    }

    // 点击 置顶：按下 本面板 任意 区域 → SetAsLastSibling（渲染 顺序 = 交互 焦点 = 视觉 最上层）。
    // 叠放 时 "最上层 = 最后 点击" = 用户 当前 操作 面板 → 归属 判定（sibling 顺序）无 歧义；
    // 且 符合 视觉 直觉：点哪个 面板 就 浮到 最前。拖拽 按下 也 先 置顶（OnPointerDown 先于 OnBeginDrag）。
    public void OnPointerDown(PointerEventData 事件)
    {
        if (面板根 != null) 面板根.SetAsLastSibling();
    }

    // 面板拖拽移动（整面板可拖，限制在 Canvas 内）
    public void OnBeginDrag(PointerEventData 事件)
    {
        Vector2 屏幕;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform.parent, 事件.position, 事件.pressEventCamera, out 屏幕))
            拖拽偏移 = 面板根.anchoredPosition - 屏幕;
    }
    public void OnDrag(PointerEventData 事件)
    {
        Vector2 屏幕;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform.parent, 事件.position, 事件.pressEventCamera, out 屏幕)) return;
        面板根.anchoredPosition = 屏幕 + 拖拽偏移;
        限制在屏幕内();   // 拖拽中同样限制在屏幕内
    }
    public void OnEndDrag(PointerEventData 事件) { }
}
