using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 日志折叠：挂在「日志面板」上，控制日志栏折叠/展开。
// 折叠后只留一条窄把手（含折叠按钮），展开时恢复完整宽度；宽度用协程平滑过渡。
// 所有引用由「界面搭建工具」在搭建时自动填入（也可在 Inspector 手动指定）。
public sealed class 日志折叠 : MonoBehaviour
{
    [SerializeField] private RectTransform 面板;          // 日志面板本体（改宽度用）
    [SerializeField] private GameObject 滚动区;           // 日志滚动区（折叠时隐藏）
    [SerializeField] private GameObject 标题区;           // 日志标题行（折叠时隐藏）
    [SerializeField] private TMP_Text 折叠图标;           // 折叠按钮上的图标（◀ / ▶）
    [SerializeField] private float 展开宽度 = 160f;   // 960×540 小窗下日志栏宽度
    [SerializeField] private float 折叠宽度 = 22f;
    [SerializeField] private float 动画时长 = 0.22f;

    private bool 已折叠;
    private Coroutine 当前协程;
    private Button 上次按钮;   // 幂等：重复绑定前先移除旧监听

    // 由搭建工具调用：绑定折叠按钮点击（重复调用安全）
    public void 绑定折叠按钮(Button 按钮)
    {
        if (上次按钮 != null) 上次按钮.onClick.RemoveAllListeners();
        上次按钮 = 按钮;
        if (按钮 != null) 按钮.onClick.AddListener(切换);
    }

    private void 切换()
    {
        已折叠 = !已折叠;
        if (当前协程 != null) StopCoroutine(当前协程);
        当前协程 = StartCoroutine(宽度动画(已折叠 ? 折叠宽度 : 展开宽度));
        if (滚动区 != null) 滚动区.SetActive(!已折叠);
        if (标题区 != null) 标题区.SetActive(!已折叠);
        if (折叠图标 != null) 折叠图标.text = 已折叠 ? ">>" : "<<";
    }

    private IEnumerator 宽度动画(float 目标宽度)
    {
        if (面板 == null) yield break;
        float 起点 = 面板.sizeDelta.x;
        float 流逝 = 0f;
        while (流逝 < 动画时长)
        {
            流逝 += Time.unscaledDeltaTime;
            float 当前 = Mathf.Lerp(起点, 目标宽度, 流逝 / 动画时长);
            面板.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 当前);
            yield return null;
        }
        面板.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 目标宽度);
    }
}
