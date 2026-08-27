using UnityEngine;
using UnityEngine.UI;

// 穿戴容器块：中区一个预搭块（弹挂/腰封/背包）——块物体挂 网格背包面板，Inspector 配好 网格容器 引用
[System.Serializable]
public class 穿戴容器块
{
    public 装备槽 装备槽;         // 对应 装备面板 上的该容器槽（引用其 槽位名——一处配置，与装备区一致）
    public 网格背包面板 面板;     // 块上挂的 网格背包面板（场景预搭）

    public string 槽位名 => 装备槽 != null ? 装备槽.槽位 : "";   // 查询键：弹挂 / 腰封 / 背包
}

// 穿戴容器区（中区，塔科夫式）：ScrollRect 内容 = 身上 3 个穿戴容器（弹挂/腰封/背包）的网格块。
//   块常驻（同装备区槽位）：穿上 → 网格显示（注入 穿戴容器视图）；没穿 → 网格隐藏（为空，无需占位元素）。
//   重建后 强制刷新 Layout Group（布局父 参数），保证 显隐/尺寸 变化即时生效。
//   场景手动预搭 3 块（各挂 网格背包面板），本组件只做：穿戴变化 → 网格显隐 + 刷新数据源。
public sealed class 穿戴容器区 : MonoBehaviour
{
    [SerializeField] private 穿戴容器块[] 容器块;   // 3 个预搭块（弹挂/腰封/背包，顺序随布局）
    [SerializeField] private RectTransform 布局父;   // Layout Group 所在容器（如 Content）；重建后强制刷新布局

    void Awake()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        if (事件 != null)
        {
            事件.订阅<背包变化事件>(背包变化响应);
            事件.订阅<属性变化事件>(属性变化响应);   // 装备/卸下 发属性变化 → 穿戴容器增减
        }
        重建();
    }

    private void OnEnable() => 重建();   // F1 每次打开面板时兜底刷新

    private void OnDestroy()
    {
        if (ServiceRegistry.已注册<EventBus>())
        {
            var 事件 = ServiceRegistry.Get<EventBus>();
            if (事件 != null)
            {
                事件.取消订阅<背包变化事件>(背包变化响应);
                事件.取消订阅<属性变化事件>(属性变化响应);
            }
        }
    }

    private void 背包变化响应(背包变化事件 _) => 重建();
    private void 属性变化响应(属性变化事件 _) => 重建();

    // 重建：遍历预搭块——有穿戴 → 网格显示 + 注入容器视图 + 重载；没穿 → 网格隐藏（为空，无需占位元素）
    public void 重建()
    {
        var 档案 = ServiceRegistry.Get<PlayerService>()?.档案;
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        if (容器服务 == null) return;
        if (容器块 == null || 容器块.Length == 0)
        {
            ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.反馈坏, "[穿戴容器区] 未配置 容器块 数组（请拖入 弹挂/腰封/背包 三块：装备槽 + 面板 引用）。"));
            return;
        }
        float 格 = 网格背包面板.主背包面板 != null ? 网格背包面板.主背包面板.格子尺寸 : 133f;
        foreach (var 块 in 容器块)
        {
            if (块?.面板 == null) continue;
            var 记录 = 档案?.装备.Find(e => e.槽位 == 块.槽位名);
            bool 穿了 = 记录 != null && !string.IsNullOrEmpty(记录.标识);
            Debug.Log($"[穿戴容器区] 槽位={块.槽位名} 穿了={穿了} 面板={块.面板.name} 记录={记录?.标识} 装备数={档案?.装备.Count}", 块.面板);
            块.面板.gameObject.SetActive(穿了);   // 没穿 → 网格隐藏（空）
            if (!穿了) continue;
            块.面板.数据源 = 容器服务.穿戴容器视图(记录);   // 注入该穿戴容器视图
            块.面板.配置容器显示(格);
            块.面板.重载网格();
        }
        if (布局父 != null) LayoutRebuilder.ForceRebuildLayoutImmediate(布局父);   // 强制 Layout Group 即时重排
    }
}
