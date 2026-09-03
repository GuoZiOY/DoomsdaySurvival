using UnityEngine;
using UnityEngine.EventSystems;

// ============================================================
// 网格面板基类.交互 —— 分部类：交互分派（点击 / 右键 → 钩子）+ 内部组件（物品点击 / 物品拖拽）。
// 与 网格面板基类.cs 同一 partial 类（共享字段与方法）；纯组织性拆分，无行为改动。
// ============================================================
public abstract partial class 网格面板基类
{
    private void 物品被点击(物品堆叠 堆叠, int 点击次数)
    {
        if (拖拽源 != null) return;
        右键菜单.实例?.隐藏();
        选中 = 堆叠;
        单击实体(堆叠, 点击次数);   // 钩子：子类 处理 选中 高亮/双击 语义
    }

    private void 物品右键(物品堆叠 堆叠, RectTransform 物品框)
    {
        if (堆叠 == null) return;
        选中 = 堆叠;
        右键实体(堆叠, 物品框);   // 钩子：物品 菜单 / 家具 菜单
    }

    // ============================================================
    // 内部组件：非按钮点击 + 拖拽（调用 基类 分派 → 钩子）
    // ============================================================
    protected sealed class 物品点击 : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public 物品堆叠 堆叠;
        public 网格面板基类 面板;
        public RectTransform 物品框;
        public RectTransform 内容层;
        public GameObject 高光层;
        public void OnPointerClick(PointerEventData 事件)
        {
            if (堆叠 == null || 面板 == null) return;
            if (事件.button == PointerEventData.InputButton.Right) { 面板.物品右键(堆叠, 物品框); return; }
            面板.物品被点击(堆叠, 事件.clickCount);
        }
        public void OnPointerEnter(PointerEventData 事件) { if (面板 != null) 面板.悬停(内容层, 高光层, true); }
        public void OnPointerExit(PointerEventData 事件) { if (面板 != null) 面板.悬停(内容层, 高光层, false); }
    }

    protected sealed class 物品拖拽 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public 物品堆叠 堆叠;
        public 网格面板基类 面板;
        public void OnBeginDrag(PointerEventData 事件) { if (堆叠 != null && 面板 != null) 面板.开始拖拽(堆叠, 事件); }
        public void OnDrag(PointerEventData 事件) { if (面板 != null) 面板.拖拽移动(事件); }
        public void OnEndDrag(PointerEventData 事件) { if (面板 != null) 面板.结束拖拽(事件); }
    }
}
