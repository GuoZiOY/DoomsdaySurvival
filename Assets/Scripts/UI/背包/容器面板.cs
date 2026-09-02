using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 容器面板：浮动面板基类 子类（单网格）——弹药箱/医疗箱/冰箱 等 纯容器 家具/物品 打开 网格 视图。
// 预制体（Assets/Resources/Prefab/容器面板.prefab）搭好 底座/标题/关闭按钮/网格容器（挂 物品网格面板）。
// 引用（面板根/标题文本/关闭按钮/网格容器/容器网格）全 序列化 在 本类（预制体 已 存 引用，不 因 抽 壳 丢 失）；
// 基类（浮动面板基类）通过 抽象 属性 取 面板根/预制体路径，壳 逻辑（创建/防重/拖拽/置顶/限屏）复用。
public sealed class 容器面板 : 浮动面板基类
{
    private const string 预制体路径常量 = "Prefab/容器面板";   // Resources 路径（预制体 静态 搭建）

    // —— 预制体 引用（Inspector 拖好；实例化 后 自动 绑定） ——
    [SerializeField] private RectTransform 面板根;      // 面板根（含 Image 底座，可拖拽；尺寸 动态 控制）——字段名 与 预制体 一致（引用 不 断）
    [SerializeField] private RectTransform 网格容器;      // 容器内部网格的 Content（尺寸 动态 控制）
    [SerializeField] private 物品网格面板 容器网格;        // 物品网格面板 组件（预制体 已 挂）
    [SerializeField] private TextMeshProUGUI 标题文本;    // 顶部 容器名（显示容器 时 赋值）
    [SerializeField] private Button 关闭按钮;             // 右上 关闭按钮（Awake 自动 挂 关闭()）

    protected override string 预制体路径 => 预制体路径常量;
    protected override RectTransform 根矩形 => 面板根;   // 基类 壳 逻辑 用（拖拽/置顶/限屏/命中）

    // 供 物品网格面板 双击/家具 右键 调用：实例化预制体 + 注入容器数据（保持 对外 API 不变）
    public static 容器面板 创建(RectTransform 挂载父, 物品堆叠 容器)
        => 创建<容器面板>(挂载父, 容器, 预制体路径常量);

    // Awake：关闭按钮 绑定（子类 覆写 基类 Awake？基类 无 Awake——壳 无 关闭 绑定；子类 这里 绑）
    private void Awake()
    {
        if (关闭按钮 != null)
        {
            关闭按钮.onClick.AddListener(关闭);
            音效管理器.实例?.注册按钮(关闭按钮);
        }
    }

    // 初始化：绑定 标题 + 注入 容器 网格 视图 + 面板/网格 尺寸 伸缩（样式 在 预制体）
    protected override void 初始化面板(物品堆叠 容器, RectTransform 挂载父)
    {
        当前容器 = 容器;
        var 服务 = ServiceRegistry.Get<容器服务>();
        服务.初始化容器(容器);
        var 视图 = 服务.打开(容器);
        容器网格.数据源 = 视图;
        容器网格.所属容器 = 容器;
        if (标题文本 != null) 标题文本.text = 容器.标识;   // 顶部 容器名（弹药箱/冰箱…）
        容器网格.立即刷新();   // 同步重建（此刻 渲染网格宽/高 才是 容器 实际 尺寸——含 形状 块偏移）
        // 面板根尺寸 = 预留 + 网格 内容 尺寸；网格容器 居中锚 → 水平 居中、垂直 按 预留 定位。
        float 网格宽 = 容器网格.渲染网格宽;
        float 网格高 = 容器网格.渲染网格高;
        float 外扩 = 网格面板配色.底盘外扩;   // 容器 = 网格 + 底盘外框（四周 各 外扩）
        面板根.sizeDelta = new Vector2(
            Mathf.Max(最小面板宽, 网格宽 + 外扩 * 2f + 左右留白 * 2f),
            Mathf.Max(最小面板高, 顶部预留 + 网格高 + 外扩 * 2f + 底边距));
        网格容器.sizeDelta = new Vector2(网格宽 + 外扩 * 2f, 网格高 + 外扩 * 2f);
        // 居中锚 定位：网格 底部 距 面板 底缘 = 底边距（则 顶部 距 面板 顶 = 顶部预留，标题/按钮 区）
        网格容器.anchoredPosition = new Vector2(0f, (底边距 - 顶部预留) / 2f);
    }

    // 屏幕点 是否在 本面板 网格容器 矩形 内——自己面板 拖拽 松手 判定用：
    // 网格内 松手 = 同面板 移动/换位（放行）；底座空白区（标题/边距，非网格）= 拦截（不穿透 下层）
    public bool 命中网格(Vector2 屏幕点)
    {
        if (网格容器 == null) return false;
        var 画布 = 网格容器.GetComponentInParent<Canvas>();
        var 相机 = 画布 != null && 画布.renderMode != RenderMode.ScreenSpaceOverlay ? 画布.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(网格容器, 屏幕点, 相机);
    }

    // 关闭：解除 防重 登记 + 网格 数据源 置空（基类 关闭 调 清理内容）
    protected override void 清理内容()
    {
        if (容器网格 != null) { 容器网格.数据源 = null; 容器网格.所属容器 = null; }
    }
}
