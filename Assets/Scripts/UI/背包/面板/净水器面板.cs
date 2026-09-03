using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 净水器面板：净水器 家具 的 3 分区 操作面板（浮动面板基类 子类）——脏水区/过滤区/净水区 3 个 功能区 网格。
// 净水器 = 分区容器 家具（容器物品 = 3 个 子容器 堆叠：脏水/过滤/净水——各 独立 列表/尺寸/允许类型）。
// 净化 = 自动（世界时间管理器.净化推进）：每 净化间隔分（净水器=60 游戏分钟）脏水 1+木炭 1 → 水 1——
//   材料 齐 + 净水区 可放 才 计时；缺 材料/净水区 满 → 冻结；净水区 满 卡 进度（取 走 水 立即 出）。
// 面板：3 网格 + 净化 进度（Slider 进度滑条 0→1 + TMP_Text 进度文本：距 下 批 出水 剩余 分钟）。
// 场景搭建：净水器面板 预制体（挂 本组件 + 浮动面板基类 壳 引用）+ 3 个 物品网格面板（分区 网格）
//            + 进度 滑条/文本 + 标题/关闭（浮动面板基类 壳）。
public sealed class 净水器面板 : 浮动面板基类
{
    private const string 预制体路径常量 = "Prefab/净水器面板";   // Resources 路径（预制体 静态 搭建）

    // —— 预制体 引用（Inspector 拖好；实例化 后 自动 绑定） ——
    [SerializeField] private RectTransform 面板根;          // 面板根（壳）
    [SerializeField] private TextMeshProUGUI 标题文本;
    [SerializeField] private Button 关闭按钮;
    [SerializeField] private 物品网格面板 脏水区网格;       // 脏水区（只收 脏水）
    [SerializeField] private 物品网格面板 过滤区网格;       // 过滤区（只收 木炭）
    [SerializeField] private 物品网格面板 净水区网格;       // 净水区（只收 水——产物）
    [SerializeField] private Slider 净化进度滑条;           // 净化 进度（0→1；可空）
    [SerializeField] private TMP_Text 净化进度文本;         // 距 下 批 出水 剩余 分钟（可空）
    [SerializeField] private TMP_Text 状态文本;             // 净化 状态（缺 脏水/木炭、净水区 满 提示）

    protected override string 预制体路径 => 预制体路径常量;
    protected override RectTransform 根矩形 => 面板根;

    // 净化 所需 材料（分区 过滤 逻辑 也 按 此 白名单；自动 净化 配置 走 家具数据.净化材料A/B/产物）
    private const string 脏水标识 = "脏水";
    private const string 木炭标识 = "木炭";
    private const string 净水标识 = "水";

    // 静态 工厂：家具 右键 打开 净水器 → 净水器面板.创建
    public static 净水器面板 创建(RectTransform 挂载父, 物品堆叠 净水器)
        => 创建<净水器面板>(挂载父, 净水器, 预制体路径常量);

    // 背包变化 订阅 句柄（动态 面板：销毁 时 取消 订阅）
    private System.Action<背包变化事件> 背包变化订阅;
    private float 上次状态刷新;   // 轮询 节流（挂机 时 净化 分钟 随 整点 结算 走，进度 需 实时 刷）

    private void Awake()
    {
        if (关闭按钮 != null)
        {
            关闭按钮.onClick.AddListener(关闭);
            音效管理器.实例?.注册按钮(关闭按钮);
        }
        // 背包变化（放 材料/取 水）→ 刷新 净化 状态/进度
        背包变化订阅 = _ => 刷新状态();
        ServiceRegistry.Get<EventBus>()?.订阅(背包变化订阅);
    }

    // 轮询：挂机 时 净化 进度 由 世界时间管理器 推进（整点 结算）——低频 刷新 显示（0.5s）
    private void Update()
    {
        if (Time.unscaledTime - 上次状态刷新 < 0.5f) return;
        上次状态刷新 = Time.unscaledTime;
        刷新状态();
    }

    // 初始化：净水器 是 分区容器——3 网格 各 绑 一个 分区 子容器 视图
    protected override void 初始化面板(物品堆叠 净水器, RectTransform 挂载父)
    {
        当前容器 = 净水器;
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        容器服务.初始化容器(净水器);   // 确保 子容器 存在
        if (标题文本 != null) 标题文本.text = 净水器.标识;
        // 分区 0 = 脏水，1 = 过滤，2 = 净水（按 家具.容器分区 顺序）
        int 分区数 = 容器服务.分区数量(净水器);
        if (分区数 >= 1) 绑定网格(脏水区网格, 容器服务.分区子容器(净水器, 0));
        if (分区数 >= 2) 绑定网格(过滤区网格, 容器服务.分区子容器(净水器, 1));
        if (分区数 >= 3) 绑定网格(净水区网格, 容器服务.分区子容器(净水器, 2));
        刷新状态();
    }

    // 绑 网格：子容器 → 网格服务 视图（数据源）+ 允许放入（分区 过滤）
    private void 绑定网格(物品网格面板 网格, 物品堆叠 子容器)
    {
        if (网格 == null || 子容器 == null) return;
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        网格.数据源 = 容器服务.打开(子容器);
        网格.所属容器 = 子容器;   // 拖入 校验：允许放入 走 子容器（分区 过滤）
        网格.立即刷新();
    }

    // 刷新 净化 状态/进度（面板 打开 时 + 背包 变化 时）
    private void 刷新状态()
    {
        var 净水器 = 当前容器;
        if (净水器 == null) return;
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        var 数据 = ServiceRegistry.Get<DataService>();
        if (容器服务 == null || 数据 == null) return;
        var (定义标识, _) = 家具工具.解码(净水器.标识);
        if (!数据.家具.TryGetValue(定义标识, out var 定义)) return;
        // 净化 配置：间隔分 / 材料A / 材料B / 产物
        if (定义.净化间隔分 <= 0) return;
        var 脏水区 = 容器服务.分区子容器(净水器, 0);
        var 过滤区 = 容器服务.分区子容器(净水器, 1);
        var 净水区 = 容器服务.分区子容器(净水器, 2);
        bool 有脏水 = 区内有(脏水区, 定义.净化材料A);
        bool 有木炭 = 区内有(过滤区, 定义.净化材料B);
        bool 净水可放 = 区内可放(容器服务, 净水区, 定义.净化产物);
        // 进度：剩余 分钟 = 净化分钟（已 由 世界时间管理器 结算 维护；<=0 补 满）
        float 剩余 = 净水器.净化分钟 > 0 ? 净水器.净化分钟 : 定义.净化间隔分;
        float 比例 = Mathf.Clamp01(1f - 剩余 / 定义.净化间隔分);
        if (净化进度滑条 != null) 净化进度滑条.value = 比例;
        if (净化进度文本 != null) 净化进度文本.text = $"距下批出水：{Mathf.CeilToInt(剩余)} 分钟";
        // 状态 文本
        if (状态文本 != null)
        {
            if (!有脏水 && !有木炭) 状态文本.text = $"把 {定义.净化材料A} 放上面、{定义.净化材料B} 放中间，会自动净化。";
            else if (!有脏水) 状态文本.text = $"缺少 {定义.净化材料A}。";
            else if (!有木炭) 状态文本.text = $"缺少 {定义.净化材料B}。";
            else if (!净水可放) 状态文本.text = "净水区已满，取走水后继续净化。";
            else 状态文本.text = $"净化中… 每 {定义.净化间隔分} 分钟产 {定义.净化产物}×{定义.净化产物数量}。";
        }
    }

    // 分区 内 是否 有 该 标识
    private static bool 区内有(物品堆叠 子容器, string 标识)
    {
        if (子容器?.容器物品 == null) return false;
        foreach (var 堆叠 in 子容器.容器物品)
            if (堆叠 != null && 堆叠.标识 == 标识 && 堆叠.数量 > 0) return true;
        return false;
    }

    // 分区 是否 可 放入（净水区 满 判定）
    private static bool 区内可放(容器服务 容器服务, 物品堆叠 子容器, string 标识)
    {
        if (子容器?.容器物品 == null) return false;
        foreach (var 堆叠 in 子容器.容器物品)
            if (堆叠 != null && 堆叠.标识 == 标识 && 堆叠.数量 > 0) return true;
        var 视图 = 容器服务.打开(子容器);
        return 视图.寻找可放置格(new 物品堆叠(标识, 1)) != null;
    }

    // 销毁：取消 背包变化 订阅
    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (背包变化订阅 != null)
        {
            ServiceRegistry.Get<EventBus>()?.取消订阅(背包变化订阅);
            背包变化订阅 = null;
        }
    }

    // 关闭：数据源 置空（基类 关闭 调 清理内容）
    protected override void 清理内容()
    {
        foreach (var 网格 in new[] { 脏水区网格, 过滤区网格, 净水区网格 })
            if (网格 != null) { 网格.数据源 = null; 网格.所属容器 = null; }
    }
}
