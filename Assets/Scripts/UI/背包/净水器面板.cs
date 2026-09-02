using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 净水器面板：净水器 家具 的 3 分区 操作面板（浮动面板基类 子类）——脏水区/过滤区/净水区 3 个 功能区 网格。
// 净水器 = 分区容器 家具（容器物品 = 3 个 子容器 堆叠：脏水/过滤/净水——各 独立 列表/尺寸/允许类型）。
// 净化：脏水区 扣 1 脏水 + 过滤区 扣 1 木炭 → 净水区 产 1 水。
// 场景搭建：净水器面板 预制体（挂 本组件 + 浮动面板基类 壳 引用）+ 3 个 物品网格面板（分区 网格）
//            + 净化按钮 + 标题/关闭（浮动面板基类 壳）。
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
    [SerializeField] private Button 净化按钮;               // 净化（扣 脏水+木炭 → 产 水）
    [SerializeField] private TMP_Text 状态文本;             // 净化 状态（可选：缺水/缺炭/净化成功）

    protected override string 预制体路径 => 预制体路径常量;
    protected override RectTransform 根矩形 => 面板根;

    // 静态 工厂：家具 右键 打开 净水器 → 净水器面板.创建
    public static 净水器面板 创建(RectTransform 挂载父, 物品堆叠 净水器)
        => 创建<净水器面板>(挂载父, 净水器, 预制体路径常量);

    // 净化 所需 材料（分区 过滤 逻辑 也 按 此 白名单）
    private const string 脏水标识 = "脏水";
    private const string 木炭标识 = "木炭";
    private const string 净水标识 = "水";

    private void Awake()
    {
        if (关闭按钮 != null)
        {
            关闭按钮.onClick.AddListener(关闭);
            音效管理器.实例?.注册按钮(关闭按钮);
        }
        if (净化按钮 != null)
        {
            净化按钮.onClick.AddListener(净化);
            音效管理器.实例?.注册按钮(净化按钮);
        }
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

    // 净化：脏水区 扣 1 脏水 + 过滤区 扣 1 木炭 → 净水区 产 1 水
    private void 净化()
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null) return;
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        var 净水器 = 当前容器;
        if (净水器 == null) return;
        var 脏水区 = 容器服务.分区子容器(净水器, 0);
        var 过滤区 = 容器服务.分区子容器(净水器, 1);
        var 净水区 = 容器服务.分区子容器(净水器, 2);
        if (脏水区 == null || 过滤区 == null || 净水区 == null) return;
        // 检查 材料
        if (!区内有(脏水区, 脏水标识) || !区内有(过滤区, 木炭标识))
        {
            刷新状态("需要 脏水 + 木炭 才能净化。");
            音效管理器.实例?.播放失败();
            return;
        }
        // 扣 材料（从 各区 扣 1）
        区扣(脏水区, 脏水标识, 1);
        区扣(过滤区, 木炭标识, 1);
        // 产 净水（净水区 放 水；满 → 失败 回滚？先 简单：放不下 提示）
        var 净水视图 = 容器服务.打开(净水区);
        int 放入 = 净水视图.放入网格(净水标识, 1);
        if (放入 <= 0)
        {
            // 回滚：材料 返还
            var 脏视图 = 容器服务.打开(脏水区);
            脏视图.放入网格(脏水标识, 1);
            var 滤视图 = 容器服务.打开(过滤区);
            滤视图.放入网格(木炭标识, 1);
            刷新状态("净水区 满了，净化失败。");
            音效管理器.实例?.播放失败();
            return;
        }
        ServiceRegistry.Get<EventBus>()?.发布(new 背包变化事件(净水标识, 放入, 变化原因.获得));
        ServiceRegistry.Get<EventBus>()?.发布(new 背包变化事件(脏水标识, -1, 变化原因.消耗));
        ServiceRegistry.Get<EventBus>()?.发布(new 背包变化事件(木炭标识, -1, 变化原因.消耗));
        刷新状态("净化成功：获得 水。");
        音效管理器.实例?.播放成功();
    }

    // 分区 内 是否 有 该 标识
    private static bool 区内有(物品堆叠 子容器, string 标识)
    {
        if (子容器?.容器物品 == null) return false;
        foreach (var 堆叠 in 子容器.容器物品)
            if (堆叠 != null && 堆叠.标识 == 标识 && 堆叠.数量 > 0) return true;
        return false;
    }

    // 从 分区 扣 指定 标识 数量（耗尽 移除）
    private static void 区扣(物品堆叠 子容器, string 标识, int 数量)
    {
        if (子容器?.容器物品 == null) return;
        for (int i = 子容器.容器物品.Count - 1; i >= 0; i--)
        {
            var 堆叠 = 子容器.容器物品[i];
            if (堆叠 == null || 堆叠.标识 != 标识) continue;
            堆叠.数量 -= 数量;
            if (堆叠.数量 <= 0) 子容器.容器物品.RemoveAt(i);
            return;
        }
    }

    // 刷新 状态 文本（净化 反馈；空 = 提示 说明）
    private void 刷新状态(string 文本 = "")
    {
        if (状态文本 == null) return;
        if (string.IsNullOrEmpty(文本)) 文本 = "把 脏水 放上面、木炭 放中间，点 净化 得 净水。";
        状态文本.text = 文本;
    }

    // 关闭：数据源 置空（基类 关闭 调 清理内容）
    protected override void 清理内容()
    {
        foreach (var 网格 in new[] { 脏水区网格, 过滤区网格, 净水区网格 })
            if (网格 != null) { 网格.数据源 = null; 网格.所属容器 = null; }
    }
}
