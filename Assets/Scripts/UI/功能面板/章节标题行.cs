using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 章节标题行：主线面板的大章标题专用行（黑色加粗标题 + 展开/折叠指示），点击切换本章内容的显示/隐藏。
public sealed class 章节标题行 : MonoBehaviour
{
    [SerializeField] private TMP_Text 标题;   // 章标题文本（含 ▸/▾ 指示）
    [SerializeField] private Image 背景;      // 行背景（可空；不接则不设）

    public void 绑定(string 标题文本, bool 折叠, System.Action 点击)
    {
        if (标题 != null) 标题.text = $"{(折叠 ? "▶" : "▼")}{标题文本}";
        if (背景 != null) 背景.color = 折叠 ? new Color(0.18f, 0.18f, 0.18f, 0.95f) : new Color(0.24f, 0.24f, 0.24f, 0.95f);
        var 按钮 = GetComponent<Button>();
        if (按钮 != null)
        {
            面板基类.设选中缩放(按钮, false);
            按钮.onClick.AddListener(() => 点击());
            音效管理器.实例?.注册按钮(按钮);
        }
    }
}