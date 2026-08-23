using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

// 悬停反馈：给按钮挂悬停放大 / 按下收缩的平滑反馈（挂在按钮根节点上，与 Button 共存）。
// DOTween 实现：悬停/离开平滑缩放，按下向内"萌"一下（Punch）。
// tween 全部 SetUpdate(true) 不受暂停(timeScale=0)影响；SetLink(gameObject) 物体销毁自动 kill，防悬挂动画。
// 自动注册：像音效管理器一样——场景按钮 Start 时全量扫描、动态按钮在创建点调用 注册按钮；
// 已有 悬停反馈 组件的按钮自动跳过（幂等，不重复挂）。
public sealed class 悬停反馈 : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float 悬停缩放 = 1.05f;   // 鼠标悬停放大比例
    [SerializeField] private float 按下缩放 = 0.92f;   // 按下收缩比例
    [SerializeField] private float 动画时长 = 0.1f;   // 缩放动画时长（秒）

    private Tween 缩放补间;

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

    public void OnPointerEnter(PointerEventData 事件) => 播放缩放(基座 * 悬停缩放);
    public void OnPointerExit(PointerEventData 事件) => 播放缩放(基座);
    public void OnPointerDown(PointerEventData 事件)
    {
        // 按下：向内"萌"一下（负向 Punch，从基座先内缩再弹回），比纯缩放更"活"
        if (缩放补间 != null && 缩放补间.IsActive()) 缩放补间.Kill();
        缩放补间 = transform.DOPunchScale(-Vector3.one * (1f - 按下缩放), 动画时长 * 1.6f, 6, 0.4f)
            .SetUpdate(true)      // 不受暂停/时间缩放影响
            .SetLink(gameObject); // 物体销毁自动 kill
    }
    public void OnPointerUp(PointerEventData 事件) => 播放缩放(基座 * 悬停缩放);

    // 基座缩放：Tab/排序 等选中态固定放大（如 1.08），悬停/按下在其基础上再叠倍率，退出回到基座
    private float 基座 = 1f;

    public void 设基座(float 缩放)
    {
        // 打断进行中的动画，直接落到新基座（选中态切换）
        if (缩放补间 != null && 缩放补间.IsActive()) 缩放补间.Kill();
        基座 = 缩放;
        transform.localScale = Vector3.one * 基座;
    }

    // 平滑补间到目标缩放（打断上一次动画）；目标 = 基座 × 倍率
    private void 播放缩放(float 目标)
    {
        if (缩放补间 != null && 缩放补间.IsActive()) 缩放补间.Kill();
        缩放补间 = transform.DOScale(目标, 动画时长)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)      // 不受暂停/时间缩放影响
            .SetLink(gameObject); // 物体销毁自动 kill
    }
}
