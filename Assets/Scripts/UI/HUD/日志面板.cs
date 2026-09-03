using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 日志面板（v39 重做）：左下角透明消息流——订阅日志事件逐条追加，统一时长后淡出消失，
// 同屏只保留少量条目；全程穿透点击（不拦截其下方 UI）。调试/接线消息已改走 Console，不再进本面板。
// 语义收敛 7 类（探索/战斗/生存/获得/警告/系统/内心），渲染 = [第X天 HH:MM] 彩色徽章 + 正文（数值提亮）。
// 会话历史：内存记录非过程消息（战斗逐条 = 过程消息，只展示不沉淀），入口后续再加。
// 接线：Inspector 把 日志滚动/视口/日志内容 拖进「日志内容」，把 日志滚动 拖进「日志滚动」。
public sealed class 日志面板 : 面板基类
{
    [SerializeField] private RectTransform 日志内容;
    [SerializeField] private ScrollRect 日志滚动;
    [SerializeField] private float 字号 = 17f;      // 日志统一字号（Inspector 可调）
    [SerializeField] private float 停留秒数 = 6f;    // 每条日志停留时长（统一时长后淡出消失）
    [SerializeField] private float 淡出秒数 = 0.4f;  // 淡出动画时长
    [SerializeField] private int 同屏上限 = 8;       // 同时显示的日志条数（超出从最旧淡出）
    [SerializeField] private int 历史上限 = 300;     // 会话历史记录上限（内存，不随档）

    private readonly List<GameObject> 条目表 = new List<GameObject>();
    private readonly List<日志记录> 历史 = new List<日志记录>();

    // 会话历史记录（内存：非过程消息沉淀；供后续「历史列表」入口读取）
    public struct 日志记录
    {
        public 日志类型 类型; public string 文本; public int 第几天; public int 时; public int 分;
    }

    // 背包变化等订阅句柄（动态面板：销毁时取消订阅，防残留订阅撞已销毁实例）
    private System.Action<日志事件> 日志订阅;

    void Awake()
    {
        // 日志常驻左下消息流：不在 面板管理器 的可切换列表，始终显示
        日志订阅 = 追加;
        ServiceRegistry.Get<EventBus>()?.订阅(日志订阅);
        // 穿透：本面板树所有 Image 不拦截点击（滚轮/滚动条由代码控制，无需点击命中）
        foreach (var 图 in GetComponentsInChildren<Image>(true)) 图.raycastTarget = false;
    }

    protected override void 刷新(object 上下文) { }

    // 销毁：取消日志订阅（场景卸载/面板销毁都安全）
    private void OnDestroy()
    {
        if (日志订阅 != null)
        {
            ServiceRegistry.Get<EventBus>()?.取消订阅(日志订阅);
            日志订阅 = null;
        }
    }

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

    // 追加一条日志（动态创建 TMP 文本）：显示 → 停留 → 淡出 → 销毁；超出同屏上限的旧条提前淡出
    private void 追加(日志事件 e)
    {
        if (日志内容 == null) return;
        var (第几天, 时, 分) = 当前时刻();
        // 非过程消息 → 沉淀会话历史（战斗逐条=过程消息不入历史，结果摘要才入）
        if (!e.过程)
        {
            历史.Add(new 日志记录 { 类型 = e.类型, 文本 = e.文本, 第几天 = 第几天, 时 = 时, 分 = 分 });
            if (历史.Count > 历史上限) 历史.RemoveAt(0);
        }
        // 面板隐藏（主菜单等收起）期间：不建条目（协程在 inactive 下不推进会堆积），仅历史已记
        if (!gameObject.activeInHierarchy) return;

        var 条目 = new GameObject("日志条目", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(CanvasGroup));
        条目.transform.SetParent(日志内容, false);
        var 文本 = 条目.GetComponent<TextMeshProUGUI>();
        文本.fontSize = 字号;
        文本.enableWordWrapping = true;
        文本.raycastTarget = false;
        文本.color = 游戏主题.文字;
        文本.text = 渲染(e.类型, e.文本, 第几天, 时, 分);
        条目表.Add(条目);
        StartCoroutine(条目生命周期(条目));

        // 同屏上限：最旧的提前淡出（保持消息流精简）
        while (条目表.Count > 同屏上限)
        {
            var 旧 = 条目表[0];
            条目表.RemoveAt(0);
            if (旧 != null) StartCoroutine(淡出销毁(旧));
        }
        滚到底();   // 布局变更后滚到最新一条
    }

    // 滚到底部（最新消息可见；滚动区未搭/无内容时静默）
    private void 滚到底()
    {
        if (日志滚动 == null) return;
        Canvas.ForceUpdateCanvases();
        日志滚动.verticalNormalizedPosition = 0f;
    }

    // 条目生命周期：停留 统一时长 → 淡出销毁（从条目表移除）
    private IEnumerator 条目生命周期(GameObject 条目)
    {
        yield return new WaitForSecondsRealtime(停留秒数);
        条目表.Remove(条目);
        yield return StartCoroutine(淡出销毁(条目));
    }

    private IEnumerator 淡出销毁(GameObject 条目)
    {
        if (条目 == null) yield break;
        var 组 = 条目.GetComponent<CanvasGroup>();
        if (组 == null) { Destroy(条目); yield break; }
        float 流逝 = 0f;
        while (流逝 < 淡出秒数)
        {
            流逝 += Time.unscaledDeltaTime;
            组.alpha = Mathf.Clamp01(1f - 流逝 / 淡出秒数);
            yield return null;
        }
        Destroy(条目);
    }

    // 分层渲染：[第X天 HH:MM](灰) → 加粗彩色类型徽章 → 正文(近白，仅数值提亮)，不整段刷色
    private string 渲染(日志类型 类型, string 正文, int 第几天, int 时, int 分)
    {
        string 色 = 游戏主题.日志色(类型);
        string 徽章 = 游戏主题.日志标签(类型);
        return $"<color={游戏主题.时间戳色值}>[第{第几天}天 {时:00}:{分:00}]</color><color={色}>[{徽章}]</color> {高亮数值(正文)}";
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

    // 当前游戏时刻：第几天(从1起) / 时 / 分（分钟0 = 第1天 00:00）
    private static (int, int, int) 当前时刻()
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        float 分钟 = 玩家 != null ? 玩家.游戏分钟数 : 0f;
        int 整 = (int)分钟;
        return (整 / 1440 + 1, 整 / 60 % 24, 整 % 60);
    }

    // 供后续历史入口读取（只读快照）
    public IReadOnlyList<日志记录> 会话历史 => 历史;
}
