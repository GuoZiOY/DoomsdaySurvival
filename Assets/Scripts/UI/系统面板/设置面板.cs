using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 设置面板：音量滑条 + 关闭按钮（侧边栏「设置」弹出的覆盖层，独立显隐，不走面板路由）。
// 音量：滑条 → 音效管理器.设置音量（AudioListener.volume）；打开时滑条同步当前音量。
public sealed class 设置面板 : MonoBehaviour
{
    [SerializeField] private Slider 音量滑条;
    [SerializeField] private Button 关闭按钮;

    void Awake()
    {
        音量滑条?.onValueChanged.AddListener(v => { if (音效管理器.实例 != null) 音效管理器.实例.设置音量(v); });
        关闭按钮?.onClick.AddListener(() => gameObject.SetActive(false));
    }

    // 打开时同步滑条到当前音量
    private void OnEnable()
    {
        if (音量滑条 != null) 音量滑条.SetValueWithoutNotify(音效管理器.当前音量);
    }
}
