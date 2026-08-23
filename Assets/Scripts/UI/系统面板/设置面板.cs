using UnityEngine;
using UnityEngine.UI;

// 设置面板：音量滑条 + 关闭按钮（侧边栏「设置」弹出的覆盖层，独立显隐，不走面板路由）。
// 音量：滑条 → 音效管理器.设置音量（AudioListener.volume）；打开时滑条同步当前音量。
// 继承 面板基类：复用其过渡动画（本面板覆写为 从屏右滑入 / 向右滑出），因不走面板路由，
//   由 OnEnable 驱动显隐：激活播入场、关闭按钮播退场后失活。
public sealed class 设置面板 : 面板基类
{
    [SerializeField] private Slider 音量滑条;
    [SerializeField] private Button 关闭按钮;

    // 覆盖层仅在 OnEnable 中入场（不走面板管理器），首次激活时记录正常位置
    private bool _已保证位置 = false;

    // 侧边式：从屏右滑入（向左到位），退出向右滑出
    protected override 面板过渡样式 过渡样式 => 面板过渡样式.右侧滑入右滑出;

    void Awake()
    {
        音量滑条?.onValueChanged.AddListener(v => { if (音效管理器.实例 != null) 音效管理器.实例.设置音量(v); });
        关闭按钮?.onClick.AddListener(关闭);
    }

    // 打开：同步音量 + 首次记录正常位置 + 播入场
    private void OnEnable()
    {
        if (音量滑条 != null) 音量滑条.SetValueWithoutNotify(音效管理器.当前音量);
        记录初始位置();
        播放入场();
    }

    // 关闭：播退场（向右滑出），结束后失活
    private void 关闭() => 播放退场(false, false, () => gameObject.SetActive(false));

    protected override void 刷新(object 上下文) { }
}