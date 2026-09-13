using UnityEngine;
using UnityEngine.EventSystems;

// 浮动面板基类：浮动面板 的 通用 壳 逻辑（创建/防重/关闭/拖拽移动/点击置顶/屏幕内限制/命中判定）。
// 引用 不 序列化 在 基类——子类 各自 SerializeField（根矩形/标题/关闭 等 留 子类，预制体 引用 不 断），
// 基类 通过 抽象 属性/虚方法 取用。网格 内容 由 子类 注入。
// 设计：宿主管壳（创建/防重/拖拽/限屏），子类管内容（网格/按钮/信息）——网格 复用 网格面板基类。
public abstract class 浮动面板基类 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    // 已打开的容器实例 → 面板基类；同一容器不可重复打开（双击/连点防重）——所有 浮动容器面板 子类 共用 防重表
    private static readonly System.Collections.Generic.Dictionary<物品堆叠, 浮动面板基类> 已打开
        = new System.Collections.Generic.Dictionary<物品堆叠, 浮动面板基类>();

    // ===== 布局常量（子类 定稿 面板 尺寸 用） =====
    protected const float 顶部预留 = 76f;    // 网格 顶部 距 面板 顶部（标题/按钮 区 高度）
    protected const float 左右留白 = 16f;    // 网格 两侧 距 面板 边缘（水平 居中）
    protected const float 底边距 = 16f;      // 网格 底部 距 面板 底缘
    protected const float 最小面板宽 = 360f;
    protected const float 最小面板高 = 250f;
    protected const float 初始右偏比例 = 0.15f;
    protected const float 初始下偏 = 20f;
    protected const float 兜底右偏比例 = 0.4f;

    // 子类 提供：预制体 路径（Resources）——不同 面板 各自 预制体
    protected abstract string 预制体路径 { get; }
    // 子类 提供：根矩形（拖拽/置顶/限屏/命中 用）——子类 字段 名 可 各异（容器面板 = 根矩形），这里 抽象 属性 转发
    protected abstract RectTransform 根矩形 { get; }
    // 子类：绑定 标题 + 初始化 内容（网格/按钮/信息）；面板 尺寸 定稿 在 这里
    protected abstract void 初始化面板(物品堆叠 容器, RectTransform 挂载父);
    // 子类：清理 内容 数据源（关闭 时）
    protected virtual void 清理内容() { }

    protected 物品堆叠 当前容器;
    private Vector2 拖拽偏移;

    // 创建（子类 工厂 调用）：加载 子类 预制体 + 注入 容器 + 初始化
    protected static T 创建<T>(RectTransform 挂载父, 物品堆叠 容器, string 预制体路径) where T : 浮动面板基类
    {
        if (容器 == null) return null;
        // 防重：同一容器已打开 → 提到最上层并复用，不重复创建（所有 子类 共用 防重表）
        if (已打开.TryGetValue(容器, out var 已有) && 已有 != null)
        {
            已有.根矩形.SetAsLastSibling();   // 聚焦已有面板（顶到最上层）
            return 已有 as T;
        }
        var 预制 = Resources.Load<T>(预制体路径);
        if (预制 == null)
        {
            Debug.LogError($"[浮动容器面板] 找不到预制体 Resources/{预制体路径}（请用 预制体 搭建，并 拖好 引用）。");
            return null;
        }
        var 面板 = Instantiate(预制);
        // 挂 Canvas 顶层：不受 ScrollRect/Viewport 裁剪、不被任何面板覆盖（与跨面板拖拽代理一致）
        var 画布 = 挂载父.GetComponentInParent<Canvas>();
        var 顶层 = 画布 != null ? (RectTransform)画布.transform : 挂载父;
        面板.transform.SetParent(顶层, false);
        面板.transform.SetAsLastSibling();
        // 锚点 居中（面板 宽高 = 预制体 原样：手搭 预制体 定 多少 就 多少，代码 不 覆盖）
        var 根 = 面板.根矩形;
        UI工具.设锚点(根, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        面板.当前容器 = 容器;
        已打开[容器] = 面板;
        面板.初始化面板(容器, 挂载父);   // 尺寸 定稿（容器面板 按 网格 自适应——在 此 之后 定位 才 用 最终 尺寸）
        发开合日志($"打开了 {容器名(容器)}。");   // 打开 家具/容器 → 日志（[角色]）
        // 初始 位置：面板 左上 ≈ 挂载父 左上 右下 偏移（避 原 挂载 点）；居中 锚 → 减 画布 中心
        var 相机 = 画布 != null && 画布.renderMode != RenderMode.ScreenSpaceOverlay ? 画布.worldCamera : null;
        Vector2 挂载父左上;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(顶层,
                RectTransformUtility.WorldToScreenPoint(相机, 挂载父.TransformPoint(new Vector3(挂载父.rect.xMin, 挂载父.rect.yMax, 0f))),
                相机, out 挂载父左上))
            根.anchoredPosition = 挂载父左上 - 顶层.rect.center + new Vector2(挂载父.rect.width * 初始右偏比例 + 根.rect.width * 0.5f, -初始下偏 - 根.rect.height * 0.5f);
        else
            根.anchoredPosition = new Vector2(顶层.rect.width * 兜底右偏比例, -初始下偏) - 顶层.rect.center;
        面板.限制在屏幕内();
        return 面板;
    }

    // 家具/容器 显示名：家具实例（编码 带等级）→ 家具定义.名称；物品容器 → 物品.标识（打开日志 用）
    private static string 容器名(物品堆叠 容器)
    {
        if (容器 == null) return "";
        var 数据 = ServiceRegistry.Get<DataService>();
        if (数据 == null) return 容器.标识;
        var (定义, _) = 家具工具.解码(容器.标识);
        if (数据.家具.TryGetValue(定义, out var 家具)) return 家具.名称;
        return 数据.物品.TryGetValue(容器.标识, out var 物) ? 物.标识 : 容器.标识;
    }

    // 打开/关闭 家具 日志（[角色]：玩家 对 家具/容器 的 操作）——静态工厂（打开）/ 关闭() 共用
    private static void 发开合日志(string 文本)
        => ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.角色, 文本));

    // 把面板位置限制在父（Canvas 顶层）范围内：创建后/拖拽时调用，防止面板出屏（锚点 居中 → 对称 范围）
    protected void 限制在屏幕内()
    {
        var 父 = 根矩形.parent as RectTransform;
        if (父 == null) return;
        var 位置 = 根矩形.anchoredPosition;
        float 父宽 = 父.rect.width, 父高 = 父.rect.height;
        float 面板宽 = 根矩形.sizeDelta.x, 面板高 = 根矩形.sizeDelta.y;
        float 余宽 = Mathf.Max(0f, (父宽 - 面板宽) * 0.5f);   // 中心 锚：可 偏移 的 半宽（面板 比 父 大 → 0 = 居中）
        float 余高 = Mathf.Max(0f, (父高 - 面板高) * 0.5f);
        位置.x = Mathf.Clamp(位置.x, -余宽, 余宽);
        位置.y = Mathf.Clamp(位置.y, -余高, 余高);
        根矩形.anchoredPosition = 位置;
    }

    // 关闭（子类 关闭按钮 / 外部 调用）：解除 防重 登记 + 置空 数据源 + 销毁。
    // 可否关闭：子类 拦截（如 制作面板：会话 里 有 物品 放 不 回 玩家 → 拒绝 关闭，防 丢失）
    public void 关闭()
    {
        if (!可否关闭()) return;
        if (当前容器 != null)
        {
            var 名 = 容器名(当前容器);
            if (!string.IsNullOrEmpty(名)) 发开合日志($"关闭了 {名}。");   // 关闭 家具/容器 → 日志
            已打开.Remove(当前容器);
        }
        当前容器 = null;
        清理内容();
        Destroy(gameObject);
    }

    // 子类 覆写：关闭 前 检查/善后（返回 false = 取消 关闭）。默认 允许。
    protected virtual bool 可否关闭() => true;

    // 屏幕点 是否在 本根矩形（底座）矩形 内——拖拽落点 拦截用
    public bool 命中(Vector2 屏幕点)
    {
        if (根矩形 == null) return false;
        var 画布 = 根矩形.GetComponentInParent<Canvas>();
        var 相机 = 画布 != null && 画布.renderMode != RenderMode.ScreenSpaceOverlay ? 画布.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(根矩形, 屏幕点, 相机);
    }

    protected virtual void OnDestroy()
    {
        if (当前容器 != null) 已打开.Remove(当前容器);
    }

    public void OnPointerDown(PointerEventData 事件)
    {
        if (根矩形 != null) 根矩形.SetAsLastSibling();
    }

    public void OnBeginDrag(PointerEventData 事件)
    {
        Vector2 屏幕;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform.parent, 事件.position, 事件.pressEventCamera, out 屏幕))
            拖拽偏移 = 根矩形.anchoredPosition - 屏幕;
    }
    public void OnDrag(PointerEventData 事件)
    {
        Vector2 屏幕;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform.parent, 事件.position, 事件.pressEventCamera, out 屏幕)) return;
        根矩形.anchoredPosition = 屏幕 + 拖拽偏移;
        限制在屏幕内();
    }
    public void OnEndDrag(PointerEventData 事件) { }
}
