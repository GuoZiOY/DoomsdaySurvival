using UnityEngine;
using UnityEngine.EventSystems;

// 房间格点击：挂在"交互层"的每格透明图上，把 左键 / 右键 / 悬停 转交给 房间网格面板（列/行 在创建时注入）。
// 说明：交互层位于 物品层 之下——Unity 取最上层命中，所以**点实体优先给实体框**，没被覆盖的格子才落到这里
//       （空格点击与实体点击天然分流，不需要手工判定谁压着谁）。
public sealed class 房间格点击 : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public 房间网格面板 面板;
    public int 列, 行;

    public void OnPointerClick(PointerEventData 事件)
    {
        if (面板 == null) return;
        if (事件.button == PointerEventData.InputButton.Right) 面板.右键格(列, 行);
        else 面板.左键格(列, 行);
    }

    public void OnPointerEnter(PointerEventData 事件) => 面板?.悬停格(列, 行);

    public void OnPointerExit(PointerEventData 事件) => 面板?.离开格(列, 行);
}
