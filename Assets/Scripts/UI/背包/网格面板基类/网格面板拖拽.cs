using UnityEngine;
using UnityEngine.EventSystems;

// 网格面板拖拽：挂 网格面板 的 空白 拖拽 层——鼠标 **中键** 按住 拖拽 移动 整个 面板（屏幕 内 限制）。
// EventSystem 的 拖拽 事件 默认 主键（左键）——中键 需 手动：IPointerDownHandler 检测 中键 按下 开始，
// Update 里 中键 按住 期间 跟随 鼠标（左键 拖拽 家具 不受 影响）。
public sealed class 网格面板拖拽 : MonoBehaviour, IPointerDownHandler
{
    private RectTransform 目标;   // 被 移动 的 面板（网格容器）
    private Canvas 画布;
    private Vector3 拖拽偏移;
    private bool 拖拽中;

    public void 初始化(RectTransform 目标, Canvas 画布)
    {
        this.目标 = 目标;
        this.画布 = 画布;
    }

    // 中键 按下：记录 偏移，开始 拖拽（左键/右键 忽略——左键 是 家具 拖拽/菜单）
    public void OnPointerDown(PointerEventData 事件)
    {
        if (目标 == null || 事件.button != PointerEventData.InputButton.Middle) return;
        var 相机 = 画布 != null && 画布.renderMode != RenderMode.ScreenSpaceOverlay ? 画布.worldCamera : null;
        Vector3 世界;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(目标, 事件.position, 相机, out 世界))
            拖拽偏移 = 目标.position - 世界;
        拖拽中 = true;
    }

    void Update()
    {
        if (!拖拽中 || 目标 == null) return;
        if (!中键按住()) { 拖拽中 = false; return; }   // 中键 松开 → 停止
        var 画布根 = 画布 != null ? (RectTransform)画布.transform : null;
        if (画布根 == null) { 拖拽中 = false; return; }
        var 相机 = 画布 != null && 画布.renderMode != RenderMode.ScreenSpaceOverlay ? 画布.worldCamera : null;
        Vector3 世界;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(画布根, 鼠标位置(), 相机, out 世界))
        {
            目标.position = 世界 + 拖拽偏移;
            限制在画布内();
        }
    }

    // 面板 整体 限制 在 画布 内（世界 坐标 矩形）
    private void 限制在画布内()
    {
        if (目标 == null || 画布 == null) return;
        var 画布根 = (RectTransform)画布.transform;
        var 位置 = 目标.position;
        float 宽 = 目标.rect.width * 目标.lossyScale.x;
        float 高 = 目标.rect.height * 目标.lossyScale.y;
        var 屏左下 = 画布根.TransformPoint(new Vector3(画布根.rect.xMin, 画布根.rect.yMin, 0f));
        var 屏右上 = 画布根.TransformPoint(new Vector3(画布根.rect.xMax, 画布根.rect.yMax, 0f));
        float 屏左 = 屏左下.x, 屏右 = 屏右上.x, 屏底 = 屏左下.y, 屏顶 = 屏右上.y;
        float 面板左 = 位置.x - 目标.right.x * (宽 * 目标.pivot.x);
        float 面板顶 = 位置.y + 目标.up.y * (高 * (1f - 目标.pivot.y));
        面板左 = Mathf.Clamp(面板左, 屏左, Mathf.Max(屏左, 屏右 - 宽));
        面板顶 = Mathf.Clamp(面板顶, Mathf.Min(屏顶, 屏底 + 高), 屏顶);
        位置.x = 面板左 + 目标.right.x * (宽 * 目标.pivot.x);
        位置.y = 面板顶 - 目标.up.y * (高 * (1f - 目标.pivot.y));
        目标.position = 位置;
    }

    // 中键 按住（新输入系统优先；旧 Input 兜底——新输入 激活 时 绝不 触碰 Input 类）
    private bool 中键按住()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null) return UnityEngine.InputSystem.Mouse.current.middleButton.isPressed;
#endif
        return Input.GetMouseButton(2);
    }

    // 鼠标 屏幕 位置（新输入系统优先；旧 Input 兜底）
    private Vector2 鼠标位置()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null) return UnityEngine.InputSystem.Mouse.current.position.ReadValue();
#endif
        return Input.mousePosition;
    }
}
