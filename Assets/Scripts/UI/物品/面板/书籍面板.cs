using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 书籍面板：书籍 阅读 操作面板（浮动面板基类 子类）。
// 展示 书名/级别/剩余耐久/成功率，点「开始阅读」→ 书籍服务.开始阅读（堆叠.阅读分钟 = 阅读时间分）→
//   世界时间管理器 逐帧 推进 → 读满 → 自动 结算（扣耐久 + 判定 习得/失败）。
// 面板 0.5s 轮询 刷新 进度条 + 状态；耐久 耗尽 书籍 崩解 → 面板 自动 关闭。
// 场景搭建：书籍面板 预制体（挂 本组件 + 浮动面板基类 壳）+ 标题/关闭/开始阅读按钮 + 进度滑条/文本 + 状态文本。
public sealed class 书籍面板 : 浮动面板基类
{
    private const string 预制体路径常量 = "Prefab/书籍面板";

    [SerializeField] private RectTransform 面板根;
    [SerializeField] private TextMeshProUGUI 标题文本;
    [SerializeField] private Button 关闭按钮;
    [SerializeField] private Button 开始阅读按钮;
    [SerializeField] private Slider 进度滑条;
    [SerializeField] private TMP_Text 进度文本;
    [SerializeField] private TMP_Text 状态文本;

    protected override string 预制体路径 => 预制体路径常量;
    protected override RectTransform 根矩形 => 面板根;

    private 物品数据 书籍;
    private System.Action<背包变化事件> 背包变化订阅;
    private float 上次刷新;

    // 静态 工厂：右键 书籍「阅读」/ 双击 → 书籍面板.创建
    public static 书籍面板 创建(RectTransform 挂载父, 物品堆叠 书籍)
        => 创建<书籍面板>(挂载父, 书籍, 预制体路径常量);

    private void Awake()
    {
        if (关闭按钮 != null) { 关闭按钮.onClick.AddListener(关闭); 音效管理器.实例?.注册按钮(关闭按钮); }
        if (开始阅读按钮 != null) 开始阅读按钮.onClick.AddListener(开始阅读);
        背包变化订阅 = _ => 刷新状态();
        ServiceRegistry.Get<EventBus>()?.订阅(背包变化订阅);
    }

    private void Update()
    {
        if (Time.unscaledTime - 上次刷新 < 0.5f) return;
        上次刷新 = Time.unscaledTime;
        刷新状态();
    }

    protected override void 初始化面板(物品堆叠 堆叠, RectTransform 挂载父)
    {
        当前容器 = 堆叠;
        var 数据 = ServiceRegistry.Get<DataService>();
        if (数据 != null && 数据.物品.TryGetValue(堆叠.标识, out 书籍))
        {
            if (标题文本 != null) 标题文本.text = 书籍.标识;
        }
        刷新状态();
    }

    private void 开始阅读()
    {
        if (当前容器 == null || 书籍 == null) return;
        var 服务 = ServiceRegistry.Get<书籍服务>();
        if (服务 == null) return;
        if (当前容器.当前耐久 <= 0) { 音效管理器.实例?.播放失败(); 刷新状态(); return; }
        服务.开始阅读(当前容器, 书籍);
        音效管理器.实例?.播放成功();
        刷新状态();
    }

    private void 刷新状态()
    {
        if (当前容器 == null || 书籍 == null) return;
        var 服务 = ServiceRegistry.Get<书籍服务>();
        if (服务 == null) return;
        bool 在读 = 当前容器.阅读分钟 > 0;
        float 总 = 书籍.阅读时间分 > 0 ? 书籍.阅读时间分 : 1f;
        float 进度 = 在读 ? Mathf.Clamp01(1f - 当前容器.阅读分钟 / 总) : 0f;
        if (进度滑条 != null) 进度滑条.value = 进度;
        if (进度文本 != null)
            进度文本.text = 在读 ? $"研读中… 剩 {Mathf.CeilToInt(当前容器.阅读分钟)} 分钟" : "";
        if (状态文本 != null)
        {
            if (当前容器.当前耐久 <= 0)
                状态文本.text = "这本书已经散架了。";
            else if (在读)
                状态文本.text = $"正在研读 {书籍.标识}……";
            else
                状态文本.text = $"级别：{书级别名(书籍.书籍级别)}　成功率：{Mathf.RoundToInt(服务.成功率(书籍) * 100)}%　剩余耐久：{当前容器.当前耐久}";
        }
        // 书 已 崩解（耐久 ≤0 或 已 移除）→ 面板 自动 关闭
        if (当前容器.当前耐久 <= 0) { 关闭(); }
    }

    private static string 书级别名(string 级别)
    {
        switch (级别)
        {
            case "低": return "低·普通";
            case "中": return "中";
            case "高": return "高";
            case "顶": return "顶·英雄";
            case "传奇": return "传奇";
            default: return 级别;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (背包变化订阅 != null) { ServiceRegistry.Get<EventBus>()?.取消订阅(背包变化订阅); 背包变化订阅 = null; }
    }
}