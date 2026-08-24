using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 悬停反馈：给按钮挂悬停放大 / 按下收缩（无动画，直接改缩放，简单可靠）。
// 挂在按钮根节点上，与 Button 共存。
// 自动注册：场景按钮 Start 时全量扫描、动态按钮在创建点调用 注册按钮；已有组件跳过（幂等）。
public sealed class 悬停反馈 : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float 悬停缩放 = 1.05f;   // 鼠标悬停放大比例
    [SerializeField] private float 按下缩放 = 0.92f;   // 按下收缩比例

    // 基座缩放：Tab/排序 等选中态固定放大（如 1.08），悬停/按下在其基础上再叠倍率，退出回到基座
    private float 基座 = 1f;

    // 注册单个按钮：已有 悬停反馈 则跳过（幂等）；否则添加组件
    public static void 注册按钮(Button 按钮)
    {
        if (按钮 == null) return;
        if (按钮.GetComponent<悬停反馈>() != null) return;
        按钮.gameObject.AddComponent<悬停反馈>();
    }

    // 全场景扫描注册（仿 音效管理器：跳过预制体资产，只挂场景实例）
    public static void 全场景注册()
    {
        foreach (var 按钮 in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (按钮 == null || 按钮.gameObject.scene.name == null) continue;
            注册按钮(按钮);
        }
    }

    public void OnPointerEnter(PointerEventData 事件) => 应用(基座 * 悬停缩放);
    public void OnPointerExit(PointerEventData 事件) => 应用(基座);
    public void OnPointerDown(PointerEventData 事件) => 应用(基座 * 按下缩放);
    public void OnPointerUp(PointerEventData 事件) => 应用(基座 * 悬停缩放);

    // 设基座：选中态切换（Tab/排序等），直接落到新基座
    public void 设基座(float 缩放)
    {
        基座 = 缩放;
        transform.localScale = Vector3.one * 基座;
    }

    private void 应用(float 目标)
    {
        transform.localScale = Vector3.one * 目标;
    }
}
