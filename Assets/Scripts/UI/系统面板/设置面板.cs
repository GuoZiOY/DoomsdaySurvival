using UnityEngine;
using UnityEngine.UI;

// 设置面板（全局覆盖层）：音量滑条 + 关闭按钮。主菜单「设置」与游戏中侧边栏「设置」都打开它。
// 独立显隐（不走面板路由）：打开 = SetActive(true) + 同步音量；关闭 = SetActive(false)。
public sealed class 设置面板 : MonoBehaviour
{
    [SerializeField] private Slider 音量滑条;
    [SerializeField] private Button 关闭按钮;

    void Awake()
    {
        音量滑条?.onValueChanged.AddListener(v => { if (音效管理器.实例 != null) 音效管理器.实例.设置音量(v); });
        关闭按钮?.onClick.AddListener(关闭);
    }

    // 打开：同步当前音量 + 激活
    public void 打开()
    {
        if (音量滑条 != null) 音量滑条.SetValueWithoutNotify(音效管理器.当前音量);
        gameObject.SetActive(true);
    }

    // 关闭：失活
    public void 关闭() => gameObject.SetActive(false);

    // 开关切换（侧边栏用）
    public void 切换()
    {
        if (gameObject.activeSelf) 关闭(); else 打开();
    }
}
