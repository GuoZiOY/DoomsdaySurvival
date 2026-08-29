using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 搜索面板（战利品区 4，塔科夫式搜刮容器）：显示 搜索容器 内容 + 搜索机制（黑布/倒计时）。
// 流程：打开容器 → 整层黑布"尚未搜索"→ 点击 → 容器级倒计时（定义.搜索时间）→ 撤黑布
//       → 每件未搜物品 盖 黑块"？"→ 点击 → 物品级倒计时（搜索服务.物品搜索时间）→ 黑块取消 + 显现动画。
// 未搜索物品：黑块 raycastTarget 拦截 拖拽/右键（天然不可交互）；已搜索状态 存 搜索服务（会话内持久，重开容器不重搜）。
// 场景搭建：搜索面板 物体（挂本组件）→ 子物体：标题 TMP + 网格容器（RectTransform + 网格面板 组件）。
//   引用：面板 = 网格面板 组件、标题文本 = 标题 TMP、网格容器 = 网格容器 RectTransform。
// 打开入口：面板管理器.显示(背包, 搜索容器 实例)（装备背包面板 按 上下文 切到 搜索模式）。
public sealed class 搜索面板 : MonoBehaviour
{
    [SerializeField] private 网格面板 面板;          // 搜索网格（预搭：网格面板 组件）
    [SerializeField] private TMP_Text 标题文本;       // 顶部 容器名（鞋柜/冰箱…）
    [SerializeField] private RectTransform 网格容器;  // 网格区域（容器黑布 挂载点）

    private 搜索容器 当前定义;
    private GameObject 容器黑布;                       // 整层黑布（尚未搜索 + 容器倒计时）
    private readonly List<物品搜索块> 物品黑块 = new List<物品搜索块>();
    private readonly Queue<物品堆叠> 待搜索队列 = new Queue<物品堆叠>();   // 自动搜索队列（左→右、上→下）
    private bool 容器搜索中;

    private 搜索服务 服务 => ServiceRegistry.Get<搜索服务>();

    // 静态入口：打开 背包 面板 的 搜索模式（按 容器标识 反查定义；未找到 = 无操作）
    public static void 打开搜索(string 容器标识)
    {
        if (面板管理器.实例 == null) return;
        var 服务 = ServiceRegistry.Get<搜索服务>();
        if (服务 == null) return;
        var 定义 = 服务.查找容器(容器标识);
        if (定义 != null) 面板管理器.实例.显示面板类型<装备背包面板>(定义);
    }

    // 打开搜索容器：注入视图 + 显示容器级黑布（容器搜索完成前 物品 全不可见/不可交互）
    public void 打开容器(搜索容器 定义)
    {
        if (定义 == null) return;
        当前定义 = 定义;
        if (标题文本 != null) 标题文本.text = 定义.名称;
        var 视图 = 服务.打开(定义);
        if (视图 == null) return;
        // 容器网格 强制 单点 左上锚：sizeDelta 才生效（容器 = 网格 + 底盘外框 276×276 等）——
        // 拉伸锚 会 把 容器 拉满 面板，黑布/底盘 跟着 铺满（外扩 巨大 的 根源）
        网格容器.anchorMin = new Vector2(0f, 1f);
        网格容器.anchorMax = new Vector2(0f, 1f);
        网格容器.pivot = new Vector2(0f, 1f);
        网格容器.anchoredPosition = Vector2.zero;
        面板.数据源 = 视图;
        面板.所属容器 = null;   // 搜索容器 非 物品容器：跨面板转移 目标校验 走 无限制
        面板.配置视图显示();
        面板.立即刷新();
        挂容器黑布(定义.搜索时间);
    }

    // 关闭（切容器/退出搜索模式时调用）：清 黑布/黑块（物品框 随 数据源 切换 全量重建 自动销毁）
    public void 关闭()
    {
        当前定义 = null;
        容器搜索中 = false;
        音效管理器.实例?.停止搜索中();   // 防残留：关闭 面板 停止 搜索 循环
        清除黑布();
        清除物品黑块();
        面板.数据源 = null;
    }

    // ===== 容器级 黑布 =====

    private void 挂容器黑布(float 秒数)
    {
        清除黑布();
        if (网格容器 == null) return;
        var 物体 = new GameObject("容器搜索黑布", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(网格容器, false);
        物体.transform.SetAsLastSibling();   // 黑幕 置顶渲染（绝对盖住 底盘/底格/线/物品）
        var 矩形 = 物体.GetComponent<RectTransform>();
        // stretch 铺满 网格容器（容器 = 网格 + 底盘外框）：黑幕 完全 覆盖 网格 + 底盘，杜绝 漏边/漏线
        矩形.anchorMin = Vector2.zero;
        矩形.anchorMax = Vector2.one;
        矩形.offsetMin = Vector2.zero;
        矩形.offsetMax = Vector2.zero;
        var 图 = 物体.GetComponent<Image>();
        图.color = new Color(0.05f, 0.05f, 0.08f, 1f);   // 全不透明黑幕（完全盖住下层）
        var 文本物体 = new GameObject("提示", typeof(RectTransform), typeof(TextMeshProUGUI));
        文本物体.transform.SetParent(物体.transform, false);
        var 文本矩 = 文本物体.GetComponent<RectTransform>();
        文本矩.anchorMin = Vector2.zero; 文本矩.anchorMax = Vector2.one;
        文本矩.offsetMin = Vector2.zero; 文本矩.offsetMax = Vector2.zero;
        var 文本 = 文本物体.GetComponent<TextMeshProUGUI>();
        文本.text = "搜索中…";
        文本.fontSize = 26; 文本.alignment = TextAlignmentOptions.Center; 文本.color = Color.white;
        容器黑布 = 物体;
        音效管理器.实例?.开始搜索中();   // 搜索 进行中 循环音效（容器 倒计时 起，至 全部 搜完）
        开始容器搜索(秒数, 文本);   // 自动开始（塔科夫式：打开 即 搜，无需 点击）
    }

    private void 清除黑布()
    {
        if (容器黑布 != null) { Destroy(容器黑布); 容器黑布 = null; }
    }

    // 容器级 搜索（黑布 → 倒计时 → 撤布 → 挂物品黑块）
    public void 开始容器搜索(float 秒数, TMP_Text 文本)
    {
        if (容器搜索中) return;
        StartCoroutine(容器搜索倒计时(秒数, 文本));
    }

    private IEnumerator 容器搜索倒计时(float 秒数, TMP_Text 文本)
    {
        容器搜索中 = true;
        float 剩余 = Mathf.Max(秒数, 0.5f);
        while (剩余 > 0f)
        {
            剩余 -= Time.deltaTime;
            if (文本 != null) 文本.text = $"搜索中… {Mathf.CeilToInt(剩余)}s";
            yield return null;
        }
        容器搜索中 = false;
        容器搜索完成();
    }

    private void 容器搜索完成()
    {
        清除黑布();
        挂物品黑块();
        开始自动搜索();   // 塔科夫式：左→右、上→下 自动 逐个 搜索，无需 点击
    }

    // ===== 物品级 黑块 + 自动搜索流水线 =====

    private void 挂物品黑块()
    {
        清除物品黑块();
        待搜索队列.Clear();
        if (当前定义 == null || 面板.数据源 == null) return;
        // 顺序：从上到下、从左到右（行优先）——塔科夫式 逐个 揭开
        var 列表 = new List<物品堆叠>();
        foreach (var 堆叠 in 面板.数据源.背包)
            if (堆叠 != null && 堆叠.列 >= 0 && !服务.已搜索物品(当前定义, 堆叠)) 列表.Add(堆叠);
        列表.Sort((a, b) => a.行 != b.行 ? a.行.CompareTo(b.行) : a.列.CompareTo(b.列));
        foreach (var 堆叠 in 列表)
        {
            var 框 = 面板.物品框矩形(堆叠);
            if (框 == null) continue;
            var 物体 = new GameObject("物品搜索块", typeof(RectTransform), typeof(Image));
            物体.transform.SetParent(框, false);
            var 矩形 = 物体.GetComponent<RectTransform>();
            矩形.anchorMin = Vector2.zero; 矩形.anchorMax = Vector2.one;
            矩形.offsetMin = Vector2.zero; 矩形.offsetMax = Vector2.zero;
            var 图 = 物体.GetComponent<Image>();
            图.color = new Color(0.05f, 0.05f, 0.08f, 1f);   // 全不透明黑块（完全盖住物品）
            var 问体 = new GameObject("?", typeof(RectTransform), typeof(TextMeshProUGUI));
            问体.transform.SetParent(物体.transform, false);
            var 问矩 = 问体.GetComponent<RectTransform>();
            问矩.anchorMin = Vector2.zero; 问矩.anchorMax = Vector2.one;
            问矩.offsetMin = Vector2.zero; 问矩.offsetMax = Vector2.zero;
            var 问 = 问体.GetComponent<TextMeshProUGUI>();
            问.text = "？";
            问.fontSize = Mathf.Clamp(网格面板.格尺寸 * 0.5f, 18f, 48f);
            问.alignment = TextAlignmentOptions.Center; 问.color = Color.white;
            var 块 = 物体.AddComponent<物品搜索块>();
            块.堆叠 = 堆叠; 块.秒数 = 服务.物品搜索时间(null, 堆叠.标识); 块.文本 = 问;
            物品黑块.Add(块);
            待搜索队列.Enqueue(堆叠);   // 入自动搜索队列（同顺序）
        }
    }

    // 自动搜索：取队首 未搜索 物品 → 倒计时 → 揭开 → 自动 下一个（左→右、上→下）
    private void 开始自动搜索()
    {
        if (待搜索队列.Count == 0)
        {
            音效管理器.实例?.停止搜索中();   // 全部 搜完 → 停止 循环音效
            return;
        }
        var 堆叠 = 待搜索队列.Dequeue();
        物品搜索块 块 = null;
        foreach (var b in 物品黑块) if (b.堆叠 == 堆叠) { 块 = b; break; }
        if (块 == null) { 开始自动搜索(); return; }   // 黑块 未 挂上（框 缺失）→ 跳 下一个
        StartCoroutine(物品搜索倒计时(块, 堆叠));
    }

    private IEnumerator 物品搜索倒计时(物品搜索块 块, 物品堆叠 堆叠)
    {
        float 剩余 = Mathf.Max(块.秒数, 0.3f);
        while (剩余 > 0f)
        {
            剩余 -= Time.deltaTime;
            if (块.文本 != null) 块.文本.text = $"{剩余:0.0}s";
            yield return null;
        }
        // 揭开黑块 + 显现效果 + 标记已搜索 → 自动 下一个
        if (当前定义 != null && 堆叠 != null) 服务.标记已搜索(当前定义, 堆叠);
        音效管理器.实例?.播放搜索();   // 物品 搜索 出来 音效
        var 框 = 堆叠 != null ? 面板.物品框矩形(堆叠) : null;
        物品黑块.Remove(块);
        if (块 != null) Destroy(块.gameObject);
        if (框 != null) StartCoroutine(显现动画(框));
        开始自动搜索();
    }

    // 简单显现效果：物品框 从 中心 缩放 0.8→1 + 整体淡入（CanvasGroup——图标/文本 一起淡，塔科夫感）。
    // 关键 ①：TransformPoint 收 本地像素坐标（非归一化）——中心 的 本地坐标 = (0.5-pivot)×尺寸；
    // 关键 ②：position = pivot 点位置——pivot 交换 必须 保持「物体中心」世界位置 不动，否则 跳 半格。
    private static IEnumerator 显现动画(RectTransform 框)
    {
        if (框 == null) yield break;   // 空引用
        var 组 = 框.GetComponent<CanvasGroup>();
        if (组 == null)
        {
            if (框.gameObject == null) yield break;   // 物体已销毁（Unity 假 null）——AddComponent 对销毁中物体返回 null 会抛异常
            组 = 框.gameObject.AddComponent<CanvasGroup>();
        }
        if (组 == null) yield break;   // 仍拿不到（销毁中）→ 放弃动画
        var 原缩放 = 框.localScale;
        var 原pivot = 框.pivot;
        // 物体中心 的 本地坐标（相对 pivot 原点）+ 世界位置
        var 本地中心 = new Vector3((0.5f - 原pivot.x) * 框.rect.width, (0.5f - 原pivot.y) * 框.rect.height, 0f);
        var 中心 = 框.TransformPoint(本地中心);
        if (原pivot.x != 0.5f || 原pivot.y != 0.5f) 框.pivot = new Vector2(0.5f, 0.5f);
        框.position = 中心;   // pivot 点(中心) = 原中心 → 物体 视觉 不动
        框.localScale = 原缩放 * 0.8f;
        组.alpha = 0f;
        float t = 0f;
        while (t < 1f)
        {
            if (框 == null) yield break;   // 动画中途 框 被 销毁（网格 刷新/切容器）→ 放弃
            t += Time.deltaTime * 8f;   // 更快（约 0.125s）
            框.localScale = Vector3.Lerp(原缩放 * 0.8f, 原缩放, t);
            组.alpha = t;
            yield return null;
        }
        框.localScale = 原缩放;
        组.alpha = 1f;
        if (原pivot.x != 0.5f || 原pivot.y != 0.5f)
        {
            框.pivot = 原pivot;   // pivot 回 左上：中心 偏移 到 pivot 点，position 拉回 原中心
            框.position = 中心 - 框.right * (本地中心.x * 框.lossyScale.x) - 框.up * (本地中心.y * 框.lossyScale.y);
        }
    }

    private void 清除物品黑块()
    {
        foreach (var 块 in 物品黑块) if (块 != null) Destroy(块.gameObject);
        物品黑块.Clear();
    }

    // ===== 内部组件 =====

    // 物品黑块：拦截 拖拽/右键（未搜索物品 不可交互）；搜索 由 自动流水线 驱动（无需点击）
    private sealed class 物品搜索块 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public 物品堆叠 堆叠;
        public float 秒数;
        public TMP_Text 文本;
        public void OnBeginDrag(PointerEventData 事件) { }   // 拦截拖拽（黑块 未搜索 不可拖）
        public void OnDrag(PointerEventData 事件) { }
        public void OnEndDrag(PointerEventData 事件) { }
    }
}
