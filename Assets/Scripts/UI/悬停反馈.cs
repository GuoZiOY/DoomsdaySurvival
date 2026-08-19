using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 悬停反馈：给按钮挂悬停放大 / 按下收缩的平滑反馈（挂在按钮根节点上，与 Button 共存）。
// 纯 uGUI 实现（协程），零第三方依赖；让余烬风按钮"活"起来。
// 自动注册：像音效管理器一样——场景按钮 Start 时全量扫描、动态按钮在创建点调用 注册按钮；
// 已有 悬停反馈 组件的按钮自动跳过（幂等，不重复挂）。
public sealed class 悬停反馈 : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float 悬停缩放 = 1.05f;   // 鼠标悬停放大比例
    [SerializeField] private float 按下缩放 = 0.95f;   // 按下收缩比例
    [SerializeField] private float 动画时长 = 0.1f;   // 缩放动画时长（秒）

    private Coroutine 当前协程;

    // 注册单个按钮：已有 悬停反馈 则跳过（幂等）；否则添加组件
    public static void 注册按钮(Button 按钮)
    {
        if (按钮 == null) return;
        if (按钮.GetComponent<悬停反馈>() != null) return;   // 已有可跳过，避免重复
        按钮.gameObject.AddComponent<悬停反馈>();
    }

    // 全场景扫描注册（仿 音效管理器.HookSceneButtons：跳过预制体资产，只挂场景实例）
    public static void 全场景注册()
    {
        foreach (var 按钮 in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (按钮 == null || 按钮.gameObject.scene.name == null) continue;
            注册按钮(按钮);
        }
    }

    public void OnPointerEnter(PointerEventData 事件) => 播放缩放(悬停缩放);
    public void OnPointerExit(PointerEventData 事件) => 播放缩放(1f);
    public void OnPointerDown(PointerEventData 事件) => 播放缩放(按下缩放);
    public void OnPointerUp(PointerEventData 事件) => 播放缩放(悬停缩放);

    // 基座缩放：Tab/排序 等选中态固定放大（如 1.08），悬停/按下在其基础上再叠倍率，退出回到基座
    private float 基座 = 1f;

    public void 设基座(float 缩放)
    {
        基座 = 缩放;
        transform.localScale = Vector3.one * 基座;
    }

    // 平滑补间到目标缩放（打断上一次动画）；目标 = 基座 × 倍率
    private void 播放缩放(float 倍率)
    {
        if (当前协程 != null) StopCoroutine(当前协程);
        当前协程 = StartCoroutine(缩放动画(基座 * 倍率));
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
