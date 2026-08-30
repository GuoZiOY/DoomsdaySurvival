using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 搜索面板（战利品区 4，塔科夫式搜刮容器）：显示 搜索容器 内容 + 搜索机制（黑布/倒计时）。
// 流程：打开容器 → 整层黑布 自动 倒计时 → 撤黑布 → 每件未搜物品 黑块"？" 自动 逐个 搜索（左→右、上→下）→ 揭开 + 显现动画。
// 未搜索物品：黑块 拦截 拖拽/右键（天然不可交互）；已搜索状态 存 搜索服务（会话内持久，重开容器 已搜完 直接可见 不重搜）。
// 箱中箱：搜索网格 内 双击 容器物品 → 4 区 原位 替换（多级嵌套）。
// 场景搭建：搜索面板 物体（挂本组件）→ 子物体：标题 TMP + 网格容器（RectTransform + 网格面板 组件）。
public sealed class 搜索面板 : MonoBehaviour
{
    // ===== 常量（视觉/资源 统一） =====
    private const string 放大镜路径 = "Art/放大镜";   // 放大镜图（未放 = null → 不显示 扫描动效）
    private const string 斜纹路径 = "Art/黑灰斜纹";    // 黑幕 背景 纹理（未放 = null → 纯色 兜底）
    private const float 黑幕镜尺寸 = 32f;             // 黑幕 放大镜 尺寸
    private const float 黑块镜尺寸 = 26f;             // 黑块 放大镜 尺寸
    private const float 黑幕扫描半径 = 28f;           // 黑幕 扫描 圆周 半径
    private const float 黑块扫描半径 = 12f;           // 黑块 扫描 圆周 半径
    private const float 扫描速度 = 2f;                // 扫描 圈/秒
    private static readonly Color 遮盖色 = new Color(0.05f, 0.05f, 0.08f, 1f);   // 黑幕/黑块 底色

    [SerializeField] private 网格面板 面板;          // 搜索网格（预搭：网格面板 组件）
    [SerializeField] private TMP_Text 标题文本;       // 顶部 容器名（鞋柜/冰箱…）
    [SerializeField] private RectTransform 网格容器;  // 网格区域（容器黑布 挂载点）

    private 搜索容器 当前定义;   // 搜索容器（非空 = 搜索容器模式；嵌套容器 = null）
    private string 当前标识;      // 当前容器标识（已搜索 状态 键；搜索容器.标识 或 嵌套容器.标识）
    private GameObject 容器黑布;  // 整层黑布（容器 倒计时）
    private readonly List<物品搜索块> 物品黑块 = new List<物品搜索块>();   // 全部 未搜完 的黑块（关闭/切容器 时 整体 销毁）
    private readonly Queue<物品搜索块> 待搜索队列 = new Queue<物品搜索块>();   // 自动搜索队列（存 块 引用，免 查找）
    private bool 容器搜索中;

    private 搜索服务 服务 => ServiceRegistry.Get<搜索服务>();

    // 静态资源缓存（Resources.Load 只 一次）
    private static Sprite 放大镜缓存, 斜纹缓存;
    private static Sprite 放大镜图() => 放大镜缓存 != null ? 放大镜缓存 : 放大镜缓存 = Resources.Load<Sprite>(放大镜路径);
    private static Sprite 斜纹图() => 斜纹缓存 != null ? 斜纹缓存 : 斜纹缓存 = Resources.Load<Sprite>(斜纹路径);

    // ===== 打开 =====

    // 静态入口：打开 背包 面板 的 搜索模式（按 容器标识 反查定义）
    public static void 打开搜索(string 容器标识)
    {
        if (面板管理器.实例 == null) return;
        var 服务 = ServiceRegistry.Get<搜索服务>();
        if (服务 == null) return;
        var 定义 = 服务.查找容器(容器标识);
        if (定义 != null) 面板管理器.实例.显示面板类型<持有面板>(定义);
    }

    // 打开搜索容器
    public void 打开容器(搜索容器 定义)
    {
        if (定义 == null) return;
        当前定义 = 定义;
        当前标识 = 定义.标识;
        if (标题文本 != null) 标题文本.text = 定义.名称;
        var 视图 = 服务.打开(定义);
        if (视图 != null) 
            显示视图(视图, null, 定义.搜索时间);
    }

    // 打开嵌套容器（箱中箱：搜索面板 内 双击 容器物品 → 4 区 原位 替换，同样 走 搜索 流程）
    public void 打开嵌套(物品堆叠 容器)
    {
        if (容器 == null) return;
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        容器服务.初始化容器(容器);
        var 视图 = 容器服务.打开(容器);
        当前定义 = null;   // 嵌套容器 非 搜索容器
        当前标识 = 容器.标识;
        if (标题文本 != null) 
            标题文本.text = 容器.标识;
        float 嵌套时间 = Mathf.Clamp(视图.网格列 * 视图.网格行 * 0.1f, 2f, 6f);   // 嵌套 搜索时间（按 格数 推导）
        显示视图(视图, 容器, 嵌套时间);
    }

    // 注入视图 + 强制 单点 左上锚（sizeDelta 才生效）+ 已搜完 → 跳过 搜索 直接 显示
    private void 显示视图(背包服务 视图, 物品堆叠 所属, float 搜索时间)
    {
        UI工具.设锚点(网格容器, new Vector2(0, 1), new Vector2(0, 1));   // 强制 单点 左上锚：sizeDelta 才生效
        网格容器.anchoredPosition = Vector2.zero;
        面板.数据源 = 视图;
        面板.所属容器 = 所属;   // 搜索容器 = null（无类型限制）；嵌套 = 容器（允许类型 校验）
        面板.搜索宿主 = this;   // 箱中箱：网格 内 双击 容器 → 原位 替换
        面板.配置视图显示();
        面板.立即刷新();
        bool 有未搜索 = false;
        foreach (var 堆叠 in 视图.背包)
            if (堆叠 != null && 堆叠.列 >= 0 && !服务.已搜索物品(当前标识, 堆叠)) 
            { 
                有未搜索 = true; 
                break; 
            }
        if (有未搜索) 
            挂容器黑布(搜索时间);
        else 挂物品黑块();   // 已搜完：黑块 跳过 已搜索 → 全可见（队列空 → 无动作）
    }

    // 关闭（切容器/退出搜索模式时调用）
    public void 关闭()
    {
        StopAllCoroutines();   // 停 残留 倒计时 协程
        当前定义 = null;
        当前标识 = null;
        容器搜索中 = false;
        音效管理器.实例?.停止搜索中();   // 防残留：关闭 面板 停止 搜索 循环
        清除黑布();
        清除物品黑块();
        面板.数据源 = null;
        面板.搜索宿主 = null;
    }

    // ===== 容器级 黑布 =====

    private void 挂容器黑布(float 秒数)
    {
        清除黑布();
        if (网格容器 == null) return;
        var 物体 = new GameObject("容器搜索黑布", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(网格容器, false);
        物体.transform.SetAsLastSibling();   // 黑幕 置顶（盖住 底盘/底格/线/物品）
        var 矩形 = 物体.GetComponent<RectTransform>();
        UI工具.铺满(矩形);   // stretch 铺满 容器（= 网格 + 底盘外框）
        var 图 = 物体.GetComponent<Image>();
        var 斜纹 = 斜纹图();
        图.sprite = 斜纹;
        图.color = 斜纹 != null ? Color.white : 遮盖色;
        var 文本 = UI工具.创建文本(物体.transform, "提示", "搜索中…", 26f, TextAlignmentOptions.Center);
        UI工具.铺满(文本.rectTransform);
        var 镜图 = 放大镜图();
        if (镜图 != null)
        {
            var 镜矩 = UI工具.创建居中图(物体.transform, "放大镜", 镜图, new Vector2(黑幕镜尺寸, 黑幕镜尺寸), new Vector2(0f, 100f));
            物体.AddComponent<扫描动效>().初始化(镜矩, 黑幕扫描半径, 扫描速度 * 0.75f).开始();   // 黑幕 放大镜 圆周 扫描
        }
        容器黑布 = 物体;
        音效管理器.实例?.开始搜索中();   // 搜索 循环音效（容器 倒计时 起，至 全部 搜完）
        开始容器搜索(秒数, 文本);   // 自动开始（打开 即 搜）
    }

    private void 清除黑布()
    {
        if (容器黑布 != null) { Destroy(容器黑布); 容器黑布 = null; }
    }

    // 容器级 搜索：倒计时 → 撤布 → 挂物品黑块 → 自动 物品 搜索
    private void 开始容器搜索(float 秒数, TMP_Text 文本)
    {
        if (容器搜索中) return;
        容器搜索中 = true;
        StartCoroutine(倒计时(秒数, 剩余 =>
        {
            if (文本 != null) 文本.text = $"搜索中… {剩余:0.0}s";
        }, () =>
        {
            容器搜索中 = false;
            清除黑布();
            挂物品黑块();
            开始自动搜索();
        }));
    }

    // ===== 物品级 黑块 + 自动搜索流水线 =====

    private void 挂物品黑块()
    {
        清除物品黑块();
        待搜索队列.Clear();
        if (string.IsNullOrEmpty(当前标识) || 面板.数据源 == null) return;
        // 顺序：从上到下、从左到右（行优先）——塔科夫式 逐个 揭开
        var 列表 = new List<物品堆叠>();
        foreach (var 堆叠 in 面板.数据源.背包)
            if (堆叠 != null && 堆叠.列 >= 0 && !服务.已搜索物品(当前标识, 堆叠)) 列表.Add(堆叠);
        列表.Sort((a, b) => a.行 != b.行 ? a.行.CompareTo(b.行) : a.列.CompareTo(b.列));
        var 镜图 = 放大镜图();
        foreach (var 堆叠 in 列表)
        {
            var 框 = 面板.物品框矩形(堆叠);
            if (框 == null) continue;
            var 物体 = new GameObject("物品搜索块", typeof(RectTransform), typeof(Image));
            物体.transform.SetParent(框, false);
            var 矩形 = 物体.GetComponent<RectTransform>();
            UI工具.铺满(矩形);
            物体.GetComponent<Image>().color = 遮盖色;   // 全不透明黑块
            var 问 = UI工具.创建文本(物体.transform, "?", "？", Mathf.Clamp(网格面板.格尺寸 * 0.5f, 18f, 48f), TextAlignmentOptions.Center);
            UI工具.铺满(问.rectTransform);
            var 块 = 物体.AddComponent<物品搜索块>();
            RectTransform 放大镜 = null;
            if (镜图 != null)
            {
                放大镜 = UI工具.创建居中图(物体.transform, "放大镜", 镜图, new Vector2(黑块镜尺寸, 黑块镜尺寸), Vector2.zero);
                放大镜.gameObject.SetActive(false);   // 仅 当前 搜索 中 显示
            }
            块.初始化(堆叠, 服务.物品搜索时间(null, 堆叠.标识), 问, 放大镜);
            物品黑块.Add(块);
            待搜索队列.Enqueue(块);   // 队列 存 块（免 查找）
        }
    }

    // 自动搜索：取队首 黑块 → 倒计时 → 揭开 → 自动 下一个
    private void 开始自动搜索()
    {
        if (待搜索队列.Count == 0)
        {
            音效管理器.实例?.停止搜索中();   // 全部 搜完 → 停止 循环音效
            return;
        }
        var 块 = 待搜索队列.Dequeue();
        if (块 == null) { 开始自动搜索(); return; }   // 防御：块 异常 缺失 → 跳 下一个
        块.开始扫描();   // 放大镜 圆周 扫描 动效
        StartCoroutine(倒计时(块.秒数, 剩余 =>
        {
            if (块.文本 != null) 块.文本.text = $"{剩余:0.0}s";
        }, () => 物品搜索完成(块)));
    }

    private void 物品搜索完成(物品搜索块 块)
    {
        var 堆叠 = 块 != null ? 块.堆叠 : null;
        if (!string.IsNullOrEmpty(当前标识) && 堆叠 != null) 服务.标记已搜索(当前标识, 堆叠);
        音效管理器.实例?.播放搜索();   // 物品 搜索 出来 音效
        var 框 = 堆叠 != null ? 面板.物品框矩形(堆叠) : null;
        物品黑块.Remove(块);
        if (块 != null) Destroy(块.gameObject);
        if (框 != null) StartCoroutine(显现动画(框));
        开始自动搜索();
    }

    private void 清除物品黑块()
    {
        foreach (var 块 in 物品黑块) if (块 != null) Destroy(块.gameObject);
        物品黑块.Clear();
        if (待搜索队列 != null) 待搜索队列.Clear();
    }

    // ===== 通用 协程/UI 辅助 =====

    // 通用 倒计时：每帧 回调（剩余秒）+ 结束 回调（容器/物品 复用）
    private static IEnumerator 倒计时(float 秒数, Action<float> 每帧, Action 完成)
    {
        float 剩余 = Mathf.Max(秒数, 0.3f);
        while (剩余 > 0f)
        {
            剩余 -= Time.deltaTime;
            每帧?.Invoke(Mathf.Max(剩余, 0f));
            yield return null;
        }
        完成?.Invoke();
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

    // ===== 内部组件 =====

    // 通用 扫描 动效：放大镜 围绕 挂载点 做 小幅度 圆周 运动（容器 黑幕 / 物品 黑块 共用）
    private sealed class 扫描动效 : MonoBehaviour
    {
        private RectTransform 放大镜;
        private float 半径, 速度;
        private bool 运行中;

        public 扫描动效 初始化(RectTransform 放大镜, float 半径, float 速度)
        {
            this.放大镜 = 放大镜;
            this.半径 = 半径;
            this.速度 = 速度;
            return this;
        }

        // 启动 扫描（显示 放大镜 + 圆周 运动；挂载物体 销毁 时 协程 自然 停止）
        public void 开始()
        {
            if (放大镜 == null || 运行中) return;
            运行中 = true;
            放大镜.gameObject.SetActive(true);
            StartCoroutine(圆周());
        }

        private IEnumerator 圆周()
        {
            float 角度 = 0f;
            while (运行中 && 放大镜 != null)
            {
                角度 += Time.deltaTime * 360f * 速度;
                float 弧度 = 角度 * Mathf.Deg2Rad;
                放大镜.anchoredPosition = new Vector2(Mathf.Cos(弧度) * 半径, Mathf.Sin(弧度) * 半径);
                yield return null;
            }
        }
    }

    // 物品黑块：拦截 拖拽/右键（未搜索物品 不可交互）；搜索 由 自动流水线 驱动
    private sealed class 物品搜索块 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public 物品堆叠 堆叠;
        public float 秒数;
        public TMP_Text 文本;
        private 扫描动效 扫描;

        public void 初始化(物品堆叠 堆叠, float 秒数, TMP_Text 文本, RectTransform 放大镜)
        {
            this.堆叠 = 堆叠;
            this.秒数 = 秒数;
            this.文本 = 文本;
            if (放大镜 != null) 扫描 = gameObject.AddComponent<扫描动效>().初始化(放大镜, 黑块扫描半径, 扫描速度);
        }

        public void 开始扫描() => 扫描?.开始();

        public void OnBeginDrag(PointerEventData 事件) { }   // 拦截拖拽（黑块 未搜索 不可拖）
        public void OnDrag(PointerEventData 事件) { }
        public void OnEndDrag(PointerEventData 事件) { }
    }
}
