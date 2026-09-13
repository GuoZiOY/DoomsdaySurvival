using UnityEngine;
using UnityEngine.UI;

// 装具块：中区一个预搭块（弹挂/腰封/背包）——块物体挂 物品网格面板，Inspector 配好 网格容器 引用
[System.Serializable]
public class 装具块
{
    public 装备槽 装备槽;         // 对应 装备区 上的该容器槽（引用其 槽位名——一处配置，与装备区一致）
    public 物品网格面板 面板;         // 块上挂的 物品网格面板（场景预搭）

    public string 槽位名 => 装备槽 != null ? 装备槽.槽位 : "";   // 查询键：弹挂 / 腰封 / 背包
}

// 装具区（中区，塔科夫式）：ScrollRect 内容 = 身上 3 个装具（弹挂/腰封/背包）的网格块。
//   块常驻（同装备区槽位）：穿上 → 网格显示（注入 穿戴容器视图）；没穿 → 网格隐藏（为空，无需占位元素）。
//   重建后 强制刷新 Layout Group（布局父 参数），保证 显隐/尺寸 变化即时生效。
//   场景手动预搭 3 块（各挂 物品网格面板），本组件只做：穿戴变化 → 网格显隐 + 刷新数据源。
public sealed class 装具区 : MonoBehaviour
{
    public static 装具区 实例;   // 场景挂载自动登记（持有面板打开时强制重建用）

    [SerializeField] private 装具块[] 容器块;   // 3 个预搭块（弹挂/腰封/背包，顺序随布局）
    [SerializeField] private RectTransform 布局父;   // Layout Group 所在容器（如 Content）；重建后强制刷新布局

    void Awake()
    {
        实例 = this;
        var 事件 = ServiceRegistry.Get<EventBus>();
        if (事件 != null)
        {
            // 只订阅 背包变化：装备/卸下/转移 都会发 背包变化事件（含穿戴容器增减）；属性变化 只含加点等，不影响本区（去掉避免无谓重建）
            事件.订阅<背包变化事件>(背包变化响应);
        }
        重建();
    }

    private void OnEnable() => 重建();   // F1 每次打开面板时兜底刷新

    private void OnDestroy()
    {
        if (ServiceRegistry.已注册<EventBus>())
        {
            var 事件 = ServiceRegistry.Get<EventBus>();
            if (事件 != null) 事件.取消订阅<背包变化事件>(背包变化响应);
        }
    }

    private bool 待重建;   // 脏标记：事件 → 标记，Update 合并重建（避免同帧多次全量重建，优化装备卡顿）
    private bool 待布局重建;   // 布局重建延迟到 LateUpdate：块面板 的网格渲染在其自身 Update 执行（晚于本组件 Update），
                                // 若在 重建() 里立即 ForceRebuildLayoutImmediate，布局会按"未渲染"的旧尺寸重建 → 穿上装备后布局不更新。

    void Update()
    {
        if (!待重建) return;
        待重建 = false;
        重建();
    }

    // LateUpdate：所有 Update 之后执行——此时 块面板 已按 显隐/数据源/尺寸 渲染，布局按新尺寸重建才正确
    void LateUpdate()
    {
        if (!待布局重建) return;
        待布局重建 = false;
        if (布局父 != null) LayoutRebuilder.ForceRebuildLayoutImmediate(布局父);   // 强制 Layout Group 即时重排
    }

    private void 背包变化响应(背包变化事件 _) => 待重建 = true;

    // 重建：遍历预搭块——有穿戴 → 网格显示 + 注入容器视图 + 请求刷新（脏标记合并：面板 Update 帧末统一刷新，不直接全量重载）；
    // 没穿 → 网格隐藏（为空，无需占位元素）。
    // 注：不再直接全量重建——块面板自身订阅 背包变化 会刷新，这里只做 显隐/数据源/尺寸 注入，避免双重全量重建。
    public void 重建()
    {
        var 档案 = ServiceRegistry.Get<PlayerService>()?.档案;
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        if (容器服务 == null) return;
        if (容器块 == null || 容器块.Length == 0)
        {
            Debug.LogWarning("[装具区] 未配置 容器块 数组（请拖入 弹挂/腰封/背包 三块：装备槽 + 面板 引用）。");
            return;
        }
        float 格 = 物品网格面板.格尺寸;   // 格尺寸统一常量 100（全项目一致）
        foreach (var 块 in 容器块)
        {
            if (块?.面板 == null) continue;
            var 记录 = 档案?.装备.Find(e => e.槽位 == 块.槽位名);
            bool 穿了 = 记录 != null && !string.IsNullOrEmpty(记录.标识);
            块.面板.gameObject.SetActive(穿了);   // 没穿 → 网格隐藏（空）
            if (!穿了) continue;
            块.面板.数据源 = 容器服务.穿戴容器视图(记录);   // 注入该穿戴容器视图
            块.面板.所属槽位 = 块.槽位名;   // 跨面板拖拽 允许放入/嵌套 校验用（弹挂 只装弹药 等）
            块.面板.配置视图显示();
            块.面板.立即刷新();   // 立即渲染（装备/卸下 是低频结构变化，不走脏标记——保证穿上后 网格即时显示）
        }
        待布局重建 = true;   // 布局重建延迟到 LateUpdate：块面板 的网格渲染在其自身 Update 执行（晚于本组件），
                             // 立即 ForceRebuildLayoutImmediate 会按"未渲染"的旧尺寸重建 → 穿上装备后布局不更新
    }
}
