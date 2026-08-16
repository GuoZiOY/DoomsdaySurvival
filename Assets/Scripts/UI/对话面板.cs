using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 对话面板：剧情/对话/结局渲染（从主视窗剥离，职责分离）。NPC 交谈触发剧情也在此显示。
// 主剧情无返回按钮；从设施内部 NPC 进来时显示「返回设施内部」；结局显示「返回主菜单」。
public sealed class 对话面板 : 面板基类
{
    [SerializeField] private TMP_Text 正文;
    [SerializeField] private RectTransform 选项区;
    [SerializeField] private Button 返回按钮;       // 返回设施内部（NPC 交谈进来时）
    [SerializeField] private Button 返回主菜单;     // 结局时显示
    [SerializeField] private 主菜单面板 主菜单;      // 返回主菜单 的跳转目标

    private 地图节点 返回节点数据;   // 空=主剧情；非空=从某节点内部进来（NPC 交谈）
    private string 返回节点;         // 上级返回节点（回小地图用）

    void Awake()
    {
        // 剧情显示由 面板管理器 订阅 显示剧情事件 路由到这里
        返回按钮?.onClick.AddListener(返回设施内部);
        返回主菜单?.onClick.AddListener(() => 面板管理器.实例.显示(主菜单));
    }

    protected override void 刷新(object 上下文)
    {
        if (上下文 is 打开对话事件 对话)
        {
            // NPC 交谈：记住来源节点，进入剧情节点（触发 显示剧情事件 再次渲染）
            返回节点数据 = 对话.节点;
            返回节点 = 对话.返回节点;
            ServiceRegistry.Get<DialogueService>().进入节点(对话.剧情节点);
            return;
        }
        if (上下文 is 显示剧情事件 e) { 渲染剧情(e); return; }
        if (上下文 is 打开结局事件) 渲染结局();
    }

    // 渲染剧情：正文/选项（选项只服务剧情分支）
    private void 渲染剧情(显示剧情事件 e)
    {
        if (返回按钮 != null) 返回按钮.gameObject.SetActive(返回节点数据 != null);
        if (返回主菜单 != null) 返回主菜单.gameObject.SetActive(false);
        设文本(正文, e.文本);
        清空(选项区);
        if (e.选项 == null) return;
        foreach (var 选项 in e.选项)
        {
            var 目标 = 选项.目标;
            创建行(选项区, 选项.文本, () => ServiceRegistry.Get<DialogueService>().处理选项(目标), true, true);   // 选项：去LayoutElement + 文字居中
        }
    }

    // 渲染结局：序章收尾（属剧情内容，故归对话面板）
    private void 渲染结局()
    {
        if (返回按钮 != null) 返回按钮.gameObject.SetActive(false);
        if (返回主菜单 != null) 返回主菜单.gameObject.SetActive(true);
        设文本(正文, "灰烬镇的余烬还在燃烧。龙在山的深处沉睡。\n\n你的故事，才刚刚开始。");
        清空(选项区);
        创建行(选项区, "踏入灰烬镇（开放大地图）", () => ServiceRegistry.Get<地图服务>().打开大地图("灰烬镇"), true, true);
    }

    // 全局右键：NPC 交谈进来时返回设施内部；主剧情不响应（线性强制）
    public override void 回退()
    {
        if (返回节点数据 != null) 返回设施内部();
    }

    // 返回节点内部（NPC 交谈进来时）
    private void 返回设施内部()
    {
        if (返回节点数据 == null) return;
        var 节点 = 返回节点数据;
        返回节点数据 = null;
        ServiceRegistry.Get<EventBus>().发布(new 打开节点内部事件(节点, 返回节点));
    }
}
