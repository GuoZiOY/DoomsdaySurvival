using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 拆分面板（场景手动搭建 UI，代码控制联动/显隐/执行）：
//   暴露参数：面板根（初始隐藏）+ 滑动条 + 减/加按钮 + 数量输入框 + 确认/取消按钮。
//   联动：滑条 / 加减按钮 / 输入框 三者同步（改任一，其余更新）；范围 [1, 堆叠数量-1]，默认拆半。
//   确认 → 目标面板（网格面板）执行 菜单拆分；取消/关闭 → 隐藏。
public sealed class 拆分面板 : MonoBehaviour
{
    public static 拆分面板 实例;   // 场景挂载自动登记

    [SerializeField] private RectTransform 面板根;      // 面板根（初始隐藏；显示时 SetAsLastSibling 置顶）
    [SerializeField] private Slider 数量滑条;           // 拆分数量滑动条
    [SerializeField] private Button 减按钮;             // -1
    [SerializeField] private Button 加按钮;             // +1
    [SerializeField] private TMP_InputField 数量输入;   // 直接输入拆分数量
    [SerializeField] private Button 确认按钮;           // 执行拆分
    [SerializeField] private Button 取消按钮;           // 关闭

    private 物品堆叠 当前堆叠;
    private 网格面板 目标面板;

    void Awake()
    {
        实例 = this;
        if (面板根 != null) 面板根.gameObject.SetActive(false);   // 初始隐藏
        if (数量滑条 != null) 数量滑条.onValueChanged.AddListener(滑条变化);
        if (减按钮 != null) 减按钮.onClick.AddListener(() => { if (当前堆叠 != null && 数量滑条 != null) 数量滑条.value = Mathf.FloorToInt(数量滑条.value) - 1f; });
        if (加按钮 != null) 加按钮.onClick.AddListener(() => { if (当前堆叠 != null && 数量滑条 != null) 数量滑条.value = Mathf.CeilToInt(数量滑条.value) + 1f; });
        if (数量输入 != null) 数量输入.onEndEdit.AddListener(输入提交);
        if (确认按钮 != null) 确认按钮.onClick.AddListener(确认拆分);
        if (取消按钮 != null) 取消按钮.onClick.AddListener(关闭);
    }

    // 打开：目标 = 选中堆叠 + 所在面板；范围 [1, 数量-1]，默认拆半
    public void 打开(物品堆叠 堆叠, 网格面板 面板)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);   // 物体整体隐藏时先激活（触发 Awake 登记 实例）
        if (面板根 == null || 堆叠 == null || 堆叠.数量 <= 1) return;
        当前堆叠 = 堆叠;
        目标面板 = 面板;
        if (数量滑条 != null)
        {
            数量滑条.minValue = 1f;
            数量滑条.maxValue = 堆叠.数量 - 1;
            数量滑条.wholeNumbers = true;
            数量滑条.value = Mathf.FloorToInt(堆叠.数量 / 2f);   // 默认拆半
        }
        同步显示();
        面板根.gameObject.SetActive(true);
        面板根.SetAsLastSibling();   // 置顶
        居中定位();   // 屏幕中央显示（不依赖手动搭的位置）
    }

    // 定位：屏幕中央（pivot 任意都居中）
    private void 居中定位()
    {
        var 画布 = 面板根.GetComponentInParent<Canvas>();
        var 画布根 = 画布 != null ? (RectTransform)画布.transform : null;
        if (画布根 == null) return;
        var 中心 = 画布根.TransformPoint(new Vector3(画布根.rect.xMin + 画布根.rect.width * 0.5f, 画布根.rect.yMin + 画布根.rect.height * 0.5f, 0f));
        float 宽 = 面板根.rect.width * 面板根.lossyScale.x;
        float 高 = 面板根.rect.height * 面板根.lossyScale.y;
        面板根.position = 中心 + 面板根.right * (宽 * (面板根.pivot.x - 0.5f)) - 面板根.up * (高 * (面板根.pivot.y - 0.5f));
    }

    // 滑条变化 → 输入框/加减按钮 同步
    private void 滑条变化(float 值)
    {
        if (当前堆叠 == null) return;
        int n = Mathf.RoundToInt(值);
        if (数量输入 != null) 数量输入.text = n.ToString();
        更新加减按钮(n);
    }

    // 输入框提交 → 解析并 clamp，回写滑条（触发联动）
    private void 输入提交(string 文本)
    {
        if (当前堆叠 == null || 数量滑条 == null) return;
        int n;
        if (!int.TryParse(文本, out n)) n = Mathf.RoundToInt(数量滑条.value);
        n = Mathf.Clamp(n, 1, 当前堆叠.数量 - 1);
        数量滑条.value = n;
        同步显示();
    }

    private void 同步显示()
    {
        if (当前堆叠 == null || 数量滑条 == null) return;
        int n = Mathf.RoundToInt(数量滑条.value);
        if (数量输入 != null) 数量输入.text = n.ToString();
        更新加减按钮(n);
    }

    private void 更新加减按钮(int n)
    {
        if (减按钮 != null) 减按钮.interactable = n > 1;
        if (加按钮 != null && 当前堆叠 != null) 加按钮.interactable = n < 当前堆叠.数量 - 1;
    }

    // 确认：执行拆分（目标面板 菜单拆分）后关闭
    private void 确认拆分()
    {
        if (当前堆叠 == null || 目标面板 == null || 数量滑条 == null) { 关闭(); return; }
        int n = Mathf.RoundToInt(数量滑条.value);
        if (n <= 0 || n >= 当前堆叠.数量) { 关闭(); return; }
        目标面板.菜单拆分(n);
        关闭();
    }

    public void 关闭()
    {
        if (面板根 != null) 面板根.gameObject.SetActive(false);
        当前堆叠 = null;
        目标面板 = null;
    }
}
