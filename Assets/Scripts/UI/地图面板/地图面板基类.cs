using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 地图面板基类：大小地图面板共用基础设施——拖拽平移/滚轮缩放、单击移动/双击进入判定、渲染容器。
// 子类只需提供：节点清单(填充节点)、当前节点判定(是当前节点)、单击动作(执行移动)、双击动作(执行进入)。
// 地图容器统一用基类 内容区（面板基类），不再另设 地图区。
public abstract class 地图面板基类 : 面板基类, IDragHandler, IScrollHandler
{
    protected RectTransform 地图内容;                  // 节点/连线容器（平移缩放只动它）
    protected float 缩放 = 1f;
    protected Vector2 平移 = Vector2.zero;
    protected readonly 双击检测 双击 = new 双击检测();

    // 子类判定某节点是否为「当前所在」
    protected abstract bool 是当前节点(string 标识);
    // 单击动作：沿互通路径移动（子类调 地图服务）
    protected abstract void 执行移动(string 标识);
    // 双击动作：进入节点（子类判断是否已站在该节点）
    protected abstract void 执行进入(string 标识);

    // 让面板自身可接收拖拽/滚轮（空白处=面板 Image 兜底）
    protected virtual void Awake()
    {
        var 图像 = GetComponent<Image>();
        if (图像 != null) 图像.raycastTarget = true;
    }

    // 渲染框架：准备容器 → 子类填充节点 → 应用平移缩放视图
    protected void 渲染框架()
    {
        if (内容区 == null) { Debug.LogWarning("[地图面板] 未在 Inspector 拖入 内容区 地图容器"); return; }
        地图内容 = 地图渲染.创建内容(内容区);
        清空(地图内容);
        填充节点();
        地图渲染.应用视图(地图内容, 缩放, 平移);
    }

    // 子类：画连线 + 用 创建节点按钮 摆节点
    protected abstract void 填充节点();

    // 创建节点按钮（当前节点金色；单击/双击由基类统一处理）
    protected void 创建节点按钮(string 标识, string 名称, float x, float y, bool 当前)
    {
        地图渲染.创建节点(地图内容, 名称, 地图渲染.归一化(内容区, x, y), 当前, false, () => 处理节点点击(标识));
    }

    // 单击=沿互通路径移动；双击=进入节点
    private void 处理节点点击(string 标识)
    {
        if (双击.点击(标识)) { 执行进入(标识); return; }
        执行移动(标识);
    }

    // 拖拽平移（灵敏度 0.5，避免地图跟着鼠标跑太快）
    public void OnDrag(PointerEventData 事件)
    {
        平移 += 事件.delta * 0.5f;
        限制平移();
        地图渲染.应用视图(地图内容, 缩放, 平移);
    }

    // 滚轮缩放：以光标为锚点，保持光标下的世界点不动（上滚放大，delta.y 向上为正；上限 2 倍）
    public void OnScroll(PointerEventData 事件)
    {
        var 旧缩放 = 缩放;
        缩放 = Mathf.Clamp(缩放 * (1f + 事件.scrollDelta.y * 0.05f), 0.6f, 2f);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(内容区, 事件.position, null, out var 光标))
            平移 = 光标 - (光标 - 平移) * (缩放 / 旧缩放);
        限制平移();
        地图渲染.应用视图(地图内容, 缩放, 平移);
    }

    // 限制平移：地图内容边缘始终不超过可视范围（内容渲染尺寸 = 布局尺寸 × 缩放）。
    // 内容比视口大 → 边沿贴视口边沿、盖满不露白边；内容比视口小（缩小）→ 仍可在屏幕内拖拽，但边缘不出屏。
    private void 限制平移()
    {
        if (内容区 == null || 地图内容 == null) return;
        var 视口 = 内容区.rect.size;
        var 内容尺寸 = 地图内容.rect.size;
        var 余量 = new Vector2(
            Mathf.Abs(内容尺寸.x * 缩放 - 视口.x) * 0.5f,
            Mathf.Abs(内容尺寸.y * 缩放 - 视口.y) * 0.5f);
        平移.x = Mathf.Clamp(平移.x, -余量.x, 余量.x);
        平移.y = Mathf.Clamp(平移.y, -余量.y, 余量.y);
    }
}
