using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 种植箱面板：种植箱 家具 的 种植 操作面板（浮动面板基类 子类）——单 网格（4×3 容器）+ 生长 进度。
// 种植箱 = 普通 容器 家具（容器物品 = 种子/作物；允许 材料·食物原料）。
// 生长 = 自动（世界时间管理器.生长结算）：种子 随 游戏 时间 扣 生长分钟，到 0 替换 为 成熟 产物——
//   惰性 初始化（旧档 补 满）、多 批 种子 各 自 成熟、成熟 发 背包 变化 事件 刷 网格。
// 面板：1 网格 + 生长 进度（Slider 进度滑条 0→1 + TMP_Text 进度文本：距 最早 一批 成熟 剩余 时间
//       + TMP_Text 状态文本：生长中/可收获/无种子）。0.5s 轮询 平滑 刷新（生长 分钟 随 整点 结算 走）。
// 场景搭建：种植箱面板 预制体（挂 本组件 + 浮动面板基类 壳 引用）+ 1 个 物品网格面板（种植 网格）
//            + 进度 滑条/文本 + 标题/关闭（浮动面板基类 壳）。
public sealed class 种植箱面板 : 浮动面板基类
{
    private const string 预制体路径常量 = "Prefab/种植箱面板";   // Resources 路径（预制体 静态 搭建）

    // —— 预制体 引用（Inspector 拖好；实例化 后 自动 绑定） ——
    [SerializeField] private RectTransform 面板根;          // 面板根（壳）
    [SerializeField] private TextMeshProUGUI 标题文本;
    [SerializeField] private Button 关闭按钮;
    [SerializeField] private 种植箱网格 种植网格;          // 种植 网格（种子/作物；单种子 + 禁拖拽）
    [SerializeField] private Slider 生长进度滑条;            // 生长 进度（0→1；可空）
    [SerializeField] private TMP_Text 生长进度文本;          // 距 最早 一批 成熟 剩余 时间（可空）
    [SerializeField] private TMP_Text 状态文本;              // 生长 状态（生长中/可收获/无种子）

    protected override string 预制体路径 => 预制体路径常量;
    protected override RectTransform 根矩形 => 面板根;

    // 静态 工厂：家具 右键 打开 种植箱 → 种植箱面板.创建
    public static 种植箱面板 创建(RectTransform 挂载父, 物品堆叠 种植箱)
        => 创建<种植箱面板>(挂载父, 种植箱, 预制体路径常量);

    // 背包变化 订阅 句柄（动态 面板：销毁 时 取消 订阅）
    private System.Action<背包变化事件> 背包变化订阅;
    private float 上次状态刷新;   // 轮询 节流（挂机 时 生长 分钟 随 整点 结算 走，进度 需 实时 刷）

    private void Awake()
    {
        if (关闭按钮 != null)
        {
            关闭按钮.onClick.AddListener(关闭);
            音效管理器.实例?.注册按钮(关闭按钮);
        }
        // 背包变化（放 种子/收 作物）→ 刷新 生长 状态/进度
        背包变化订阅 = _ => 刷新状态();
        ServiceRegistry.Get<EventBus>()?.订阅(背包变化订阅);
    }

    // 轮询：挂机 时 生长 进度 由 世界时间管理器 推进（整点 结算）——低频 刷新 显示（0.5s）
    private void Update()
    {
        if (Time.unscaledTime - 上次状态刷新 < 0.5f) return;
        上次状态刷新 = Time.unscaledTime;
        刷新状态();
    }

    // 初始化：种植箱 = 普通 容器（非 分区）——单 网格 绑 容器 视图；面板 大小 以 预制体 为准（代码 不 覆盖）
    protected override void 初始化面板(物品堆叠 种植箱, RectTransform 挂载父)
    {
        当前容器 = 种植箱;
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        容器服务.初始化容器(种植箱);   // 确保 内部 网格 存在
        if (标题文本 != null) 标题文本.text = 种植箱.标识;
        种植网格.数据源 = 容器服务.打开(种植箱);
        种植网格.所属容器 = 种植箱;
        种植网格.立即刷新();   // 渲染 自动 设 网格容器 尺寸（网格宽+外扩×2 / 网格高+外扩×2）
        刷新状态();
    }

    // 刷新 生长 状态/进度（面板 打开 时 + 背包 变化 时 + 0.5s 轮询）
    private void 刷新状态()
    {
        var 种植箱 = 当前容器;
        if (种植箱 == null) return;
        var 数据 = ServiceRegistry.Get<DataService>();
        if (数据 == null) return;
        // 作物 标识 集合（某 种子 的 成熟 产物：土豆/蔬菜 等）——可收获 判定 用
        var 作物 = new System.Collections.Generic.HashSet<string>();
        foreach (var kv in 数据.物品)
            if (kv.Value != null && !string.IsNullOrEmpty(kv.Value.成熟产物)) 作物.Add(kv.Value.成熟产物);
        // 统计 容器 内 种子（生长时间>0 且 有 成熟产物）：
        //   生长中（生长分钟>0）→ 平均 进度 + 最早 成熟 剩余；无 生长中 且 有 作物 → 可收获
        //   生长分钟 由 世界时间管理器.生长推进 逐帧 扣减（与 净化 同 模式）→ 直接 读 即 实时
        int 生长中数 = 0;
        float 累计比例 = 0f, 最早剩余 = float.MaxValue;
        bool 有收获 = false;
        if (种植箱.容器物品 != null)
        {
            foreach (var 堆叠 in 种植箱.容器物品)
            {
                if (堆叠 == null || string.IsNullOrEmpty(堆叠.标识)) continue;
                if (!数据.物品.TryGetValue(堆叠.标识, out var 模板)) continue;
                if (模板.生长时间 > 0 && !string.IsNullOrEmpty(模板.成熟产物))
                {
                    // 种子：成熟（生长分钟<=0）已被 替换，防御 分支
                    if (堆叠.生长分钟 > 0)
                    {
                        生长中数++;
                        累计比例 += Mathf.Clamp01(1f - 堆叠.生长分钟 / (模板.生长时间 * 60f));
                        if (堆叠.生长分钟 < 最早剩余) 最早剩余 = 堆叠.生长分钟;
                    }
                }
                else if (堆叠.数量 > 0 && 作物.Contains(堆叠.标识))
                    有收获 = true;   // 已 成熟 的 作物（土豆/蔬菜 等）→ 可收获
            }
        }
        // 进度条 + 进度 文本：生长中 时 显示（平均 进度；最早 一批 剩余 时间——倒计时）
        if (生长中数 > 0)
        {
            if (生长进度滑条 != null) 生长进度滑条.value = Mathf.Clamp01(累计比例 / 生长中数);
            if (生长进度文本 != null) 生长进度文本.text = 倒计时(最早剩余);
        }
        else
        {
            if (生长进度滑条 != null) 生长进度滑条.value = 0f;
            if (生长进度文本 != null) 生长进度文本.text = "";
        }
        // 状态 文本（简版：只 报 当前 阶段；具体 时间 由 进度 文本 倒计时 显示）
        if (状态文本 != null)
        {
            if (生长中数 > 0) 状态文本.text = "生长中…";
            else if (有收获) 状态文本.text = "可以收获了";
            else 状态文本.text = "等待播种";
        }
    }

    // 倒计时 格式：剩 hh:mm 成熟（小时:分钟；剩余 生长 分钟 向上 取整，不显示 秒；进度 文本 用）
    private static string 倒计时(float 剩余分钟)
    {
        int 剩余 = Mathf.Max(0, Mathf.CeilToInt(剩余分钟));
        int 小时 = 剩余 / 60, 分钟 = 剩余 % 60;
        return $"剩 {小时:00}:{分钟:00} 成熟";
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
        if (种植网格 != null) { 种植网格.数据源 = null; 种植网格.所属容器 = null; }
    }
}
