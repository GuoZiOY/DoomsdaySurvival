using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 日志面板：常驻左侧，订阅日志事件按类型着色追加条目（不进面板切换）。
// 接线：Inspector 把 日志滚动/视口/日志内容 拖进「日志内容」，把 日志滚动 拖进「日志滚动」。
public sealed class 日志面板 : 面板基类
{
    [SerializeField] private RectTransform 日志内容;
    [SerializeField] private ScrollRect 日志滚动;
    [SerializeField] private float 字号 = 20f;   // 日志统一字号（Inspector 可调 / 运行时 设置字号 动态改）
    private readonly List<GameObject> 条目表 = new List<GameObject>();
    private const int 上限 = 200;

    void Awake()
    {
        // 日志常驻左侧栏：不在 面板管理器 的可切换列表，始终显示
        ServiceRegistry.Get<EventBus>().订阅<日志事件>(追加);
    }

    protected override void 刷新(object 上下文) { }

    // 统一改字号：更新新条目 + 应用到已存在条目（动态改动）
    public void 设置字号(float 新字号)
    {
        字号 = 新字号;
        foreach (var 条目 in 条目表)
        {
            var 文本 = 条目.GetComponent<TMP_Text>();
            if (文本 != null) 文本.fontSize = 新字号;
        }
    }

    // 追加一条日志（动态创建 TMP 文本，动态内容可代码生成）
    private void 追加(日志事件 e)
    {
        if (日志内容 == null) return;
        var 条目 = new GameObject("日志条目", typeof(RectTransform), typeof(TextMeshProUGUI));
        条目.transform.SetParent(日志内容, false);
        var 文本 = 条目.GetComponent<TextMeshProUGUI>();
        文本.fontSize = 字号;
        文本.enableWordWrapping = true;
        文本.raycastTarget = false;
        文本.color = 游戏主题.文字;
        文本.text = 渲染(e.类型, e.文本);
        条目表.Add(条目);

        if (条目表.Count > 上限)
        {
            Destroy(条目表[0]);
            条目表.RemoveAt(0);
        }
        Canvas.ForceUpdateCanvases();
        if (日志滚动 != null) 日志滚动.verticalNormalizedPosition = 0f;
    }

    // 分层渲染：时间戳(灰) → 加粗彩色类型徽章 → 正文(近白，仅数值提亮)，不整段刷色
    private string 渲染(日志类型 类型, string 正文)
    {
        string 色 = 游戏主题.日志色(类型);
        string 徽章 = 游戏主题.日志标签(类型);
        return $"<color={游戏主题.时间戳色值}>[{当前时间()}]</color><b><color={色}>[{徽章}]</color></b> {高亮数值(正文)}";
    }

    // 把正文里的整数提亮为「高亮色」；跳过已知的 <...> 富文本片段，避免破坏已有颜色/加粗
    private static string 高亮数值(string 正文)
    {
        const string 标签 = "(<[^>]*>)";
        var 段 = System.Text.RegularExpressions.Regex.Split(正文, 标签);
        var sb = new System.Text.StringBuilder();
        foreach (var s in 段)
        {
            if (s.Length >= 1 && s[0] == '<') { sb.Append(s); continue; }
            sb.Append(System.Text.RegularExpressions.Regex.Replace(
                s, @"(\d+)", $"<color={游戏主题.高亮色值}>$1</color>"));
        }
        return sb.ToString();
    }

    private static string 当前时间()
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>().档案;
        int 分钟 = (int)玩家.游戏分钟数;
        return $"{分钟 / 60 % 24:00}:{分钟 % 60:00}";
    }
}
