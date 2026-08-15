using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// 悬停反馈：给按钮挂悬停放大 / 按下收缩的平滑反馈（挂在按钮根节点上，与 Button 共存）。
// 纯 uGUI 实现（协程），零第三方依赖；让余烬风按钮"活"起来。
public sealed class 悬停反馈 : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float 悬停缩放 = 1.03f;   // 鼠标悬停放大比例
    [SerializeField] private float 按下缩放 = 0.97f;   // 按下收缩比例
    [SerializeField] private float 动画时长 = 0.08f;   // 缩放动画时长（秒）

    private Coroutine 当前协程;

    public void OnPointerEnter(PointerEventData 事件) => 播放缩放(悬停缩放);
    public void OnPointerExit(PointerEventData 事件) => 播放缩放(1f);
    public void OnPointerDown(PointerEventData 事件) => 播放缩放(按下缩放);
    public void OnPointerUp(PointerEventData 事件) => 播放缩放(悬停缩放);

    // 平滑补间到目标缩放（打断上一次动画）
    private void 播放缩放(float 目标)
    {
        if (当前协程 != null) StopCoroutine(当前协程);
        当前协程 = StartCoroutine(缩放动画(目标));
    }

    private IEnumerator 缩放动画(float 目标)
    {
        var 起点 = transform.localScale;
        var 终点 = Vector3.one * 目标;
        float 流逝 = 0f;
        while (流逝 < 动画时长)
        {
            流逝 += Time.unscaledDeltaTime;                 // 不受暂停影响
            transform.localScale = Vector3.Lerp(起点, 终点, 流逝 / 动画时长);
            yield return null;
        }
        transform.localScale = 终点;
    }
}
