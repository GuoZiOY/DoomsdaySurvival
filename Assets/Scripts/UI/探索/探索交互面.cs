using UnityEngine;
using UnityEngine.EventSystems;

// ============================================================
// 探索交互面：**整层一张透明命中面**，替掉原来"每格一张透明图 + 一个 探索格点击 组件"。
//   · 区域层 32×22：704 个 GameObject + 704 个组件 → 1 个；
//   · 大世界的格子池少维护一套"池槽 → 列/行"（原来那是"点到上一格"这类 bug 的来源）。
//
// 为什么能替（三条都核过，不是估计）：
//   ① 交互层压在物品层之下 —— Unity 取最上层命中，**点实体仍然优先给实体框**（这条一点没变）；
//   ② "空格点击/右键"本来只需要**把指针位置换算成格坐标**，不需要每格一个 raycast 目标；
//      本面 pivot = 左上(0,1)、尺寸 = 整块网格，所以局部坐标直接除以 格尺寸 就是列/行；
//   ③ 悬停：uGUI 会对悬停对象发 OnPointerMove（`BaseInputModule` 的 hovered 派发，见
//      `ExecuteEvents.pointerMoveHandler`）；指针移到实体框上时 pointerEnter 换成实体框 →
//      本面收到 OnPointerExit → 格高亮自动清掉（与旧行为一致）。
//
// ⚠ 单面之后必须自己补一条旧实现"顺手就有"的行为：指针走到**窗口外**（大世界池化时网格远大于窗口）
//   要按"离开格子"处理 —— 旧实现靠每格自己的 OnPointerExit 清高亮，单面之后没有那个事件了，
//   漏掉的话高亮会粘在最后一格上（在窗口内() 只是"拒绝更新"，它不会主动清除）。
// ============================================================
public sealed class 探索交互面 : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler
{
    public 探索网格面板 面板;
    public 探索图层接口 图层;

    private int 上次列 = -1, 上次行 = -1;

    public void OnPointerClick(PointerEventData 事件)
    {
        if (面板 == null) return;
        if (!取格(事件.position, 事件.pressEventCamera, out int 列, out int 行)) return;
        if (事件.button == PointerEventData.InputButton.Right) 面板.右键格(列, 行);
        else 面板.左键格(列, 行);
    }

    public void OnPointerMove(PointerEventData 事件)
    {
        if (面板 == null) return;
        if (!取格(事件.position, 事件.enterEventCamera, out int 列, out int 行)
            || (图层 != null && !图层.在窗口内(列, 行)))
        {
            清悬停();
            return;
        }
        if (列 == 上次列 && 行 == 上次行) return;   // 同格内移动不重复转发（旧实现靠"格没变就不发 enter"）
        上次列 = 列; 上次行 = 行;
        面板.悬停格(列, 行);
    }

    public void OnPointerExit(PointerEventData 事件) => 清悬停();

    private void 清悬停()
    {
        if (上次列 < 0 && 上次行 < 0) return;
        上次列 = 上次行 = -1;
        面板?.离开格(-1, -1);
    }

    // 屏幕点 → 格坐标（局部坐标以左上为原点，x 向右、y 向上）
    private bool 取格(Vector2 屏幕点, Camera 相机, out int 列, out int 行)
    {
        列 = 行 = -1;
        float 格 = 网格面板基类.格尺寸;
        if (格 <= 0f) return false;
        var 矩 = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(矩, 屏幕点, 相机, out var 局部)) return false;
        列 = Mathf.FloorToInt(局部.x / 格);
        行 = Mathf.FloorToInt(-局部.y / 格);
        return true;
    }
}
