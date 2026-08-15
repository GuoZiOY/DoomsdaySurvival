using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 地图面板基类：大小地图面板共用基础设施——拖拽平移/滚轮缩放、节点选中/双击判定、渲染容器。
// 子类只需提供：节点清单(填充节点)、当前节点判定(是当前节点)、双击动作(执行进入)。
public abstract class 地图面板基类 : 面板基类, IDragHandler, IScrollHandler
{
    [SerializeField] protected RectTransform 地图区;   // Inspector 拖入的地图容器
    protected RectTransform 地图内容;                  // 节点/连线容器（平移缩放只动它）
    protected float 缩放 = 1f;
    protected Vector2 平移 = Vector2.zero;
    protected string 选中节点;
    protected readonly Dictionary<string, TMP_Text> 节点文字 = new Dictionary<string, TMP_Text>();
    protected readonly 双击检测 双击 = new 双击检测();

    // 子类判定某节点是否为「当前所在」
    protected abstract bool 是当前节点(string 标识);
    // 双击动作（移动/进入/离开）
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
        if (地图区 == null) { Debug.LogWarning("[地图面板] 未在 Inspector 拖入 地图区 容器"); return; }
        地图内容 = 地图渲染.创建内容(地图区);
        清空(地图内容);
        节点文字.Clear();
        填充节点();
        地图渲染.应用视图(地图内容, 缩放, 平移);
    }

    // 子类：画连线 + 用 创建节点按钮 摆节点
    protected abstract void 填充节点();

    // 创建节点按钮并登记（双击/选中由基类统一处理；当前节点金色、选中钢蓝）
    protected void 创建节点按钮(string 标识, string 名称, float x, float y, bool 当前)
    {
        var 文本 = 地图渲染.创建节点(地图内容, 名称, 地图渲染.归一化(地图区, x, y), 当前, 标识 == 选中节点, () => 处理节点点击(标识));
        if (文本 != null) 节点文字[标识] = 文本;
    }

    // 单击=选中（原位改色）；双击=执行进入
    private void 处理节点点击(string 标识)
    {
        if (双击.点击(标识)) { 执行进入(标识); return; }
        // 取消旧选中（旧节点若是当前所在则回金色）
        if (节点文字.TryGetValue(选中节点, out var 旧))
            旧.color = 是当前节点(选中节点) ? 游戏主题.金色 : 游戏主题.文字;
        选中节点 = 标识;
        if (节点文字.TryGetValue(标识, out var 新)) 新.color = 游戏主题.选中色;
    }

    // 拖拽平移
    public void OnDrag(PointerEventData 事件)
    {
        平移 += 事件.delta;
        地图渲染.应用视图(地图内容, 缩放, 平移);
    }

    // 滚轮缩放：以光标为锚点，保持光标下的世界点不动
    public void OnScroll(PointerEventData 事件)
    {
        var 旧缩放 = 缩放;
        缩放 = Mathf.Clamp(缩放 * (1f - 事件.scrollDelta.y * 0.1f), 0.6f, 3f);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(地图区, 事件.position, null, out var 光标))
            平移 = 光标 - (光标 - 平移) * (缩放 / 旧缩放);
        地图渲染.应用视图(地图内容, 缩放, 平移);
    }
}
