using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 制作面板：根据配方用材料制作新物品（铁匠铺=装备 / 厨房=食物 / 药房=药剂）。
// 布局：配方列表（配方行：成品名/材料/状态）+ 详情区（选中配方的材料明细 + [制作] 按钮）。
// 配方由 图纸 物品解锁（制作设施.可用配方 已过滤）；返回统一走侧边栏取消。
public sealed class 制作面板 : 设施功能面板基类
{
    // 内容区 继承自 面板基类（配方列表容器）
    [SerializeField] private GameObject 配方行模板;      // 配方行模板（挂 配方行）
    [SerializeField] private RectTransform 详情区;       // 选中配方详情（初始隐藏）
    [SerializeField] private TMP_Text 详情文本;
    [SerializeField] private Button 制作按钮;

    private 制作功能 制作 => 逻辑 as 制作功能;
    private string 选中配方标识;

    protected override void Awake()
    {
        base.Awake();
        // 背包变化（扣材料/得产物）→ 整面板刷新（材料数量/可制作状态更新）
        ServiceRegistry.Get<EventBus>()?.订阅<背包变化事件>(_ => 刷新(null));
    }

    protected override string 标题文字() => $"{逻辑.名称} · {(逻辑 is 制作设施 设施 ? 设施.类型提示() : "制作")}";

    protected override void 渲染内容()
    {
        if (制作 == null) return;
        清空(内容区);
        var 数据 = ServiceRegistry.Get<DataService>();
        foreach (var 配方 in 制作.可用配方())
        {
            if (!数据.物品.TryGetValue(配方.产物, out var 成品)) continue;
            var 标识 = 配方.标识;
            var 行 = 面板基类.创建模板<配方行>(内容区, 配方行模板);
            if (行 == null) continue;
            行.绑定(配方, 成品, 制作.材料文本(配方), 制作.材料足够(配方), 标识 == 选中配方标识, () => 选中配方(标识));
        }
        刷新详情();
    }

    private void 选中配方(string 标识)
    {
        选中配方标识 = 标识;
        刷新详情();
    }

    // 详情区：成品名 + 材料清单 + 描述 + 图纸需求 + [制作]（材料足才可点）
    private void 刷新详情()
    {
        var 数据 = ServiceRegistry.Get<DataService>();
        if (详情区 != null) 详情区.gameObject.SetActive(!string.IsNullOrEmpty(选中配方标识));
        if (制作按钮 != null) { 制作按钮.onClick.RemoveAllListeners(); 制作按钮.gameObject.SetActive(false); }
        if (string.IsNullOrEmpty(选中配方标识) || !数据.配方.TryGetValue(选中配方标识, out var 配方))
        {
            设文本(详情文本, "");
            return;
        }
        string 文本 = $"【{(数据.物品.TryGetValue(配方.产物, out var 成品) ? 物品工具.品质名称(成品.品质档, 成品.名称) : 配方.产物)}】\n材料：{制作.材料文本(配方)}\n\n{配方.描述}";
        if (!string.IsNullOrEmpty(配方.图纸))
            文本 += $"\n\n（需图纸：{((数据.物品.TryGetValue(配方.图纸, out var 图)) ? 图.名称 : 配方.图纸)}）";
        设文本(详情文本, 文本);
        if (制作按钮 != null)
        {
            制作按钮.gameObject.SetActive(true);
            制作按钮.interactable = 制作.材料足够(配方);
            制作按钮.onClick.AddListener(() => { if (制作.尝试制作(配方.标识)) 刷新(null); });
        }
    }
}
