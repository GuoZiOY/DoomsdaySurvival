using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 设施内部面板：设施内部节点图（NPC + 功能物），三级导航第三级。继承 地图面板基类（平移缩放/双击复用）。
// 双击 NPC → 对话面板（进入剧情）；双击 功能物 → 对应功能面板。
public sealed class 设施内部面板 : 地图面板基类
{
    [SerializeField] private TMP_Text 标题;
    [SerializeField] private Button 返回按钮;
    private string 当前设施标识;
    private string 返回节点;
    private readonly Dictionary<string, 设施内部节点> 节点数据 = new Dictionary<string, 设施内部节点>();

    protected override bool 是当前节点(string 标识) => false;   // 设施内部无「当前所在」

    // 双击动作：NPC → 对话；功能物 → 功能面板
    protected override void 执行进入(string 标识)
    {
        if (!节点数据.TryGetValue(标识, out var 节点)) return;
        var 事件 = ServiceRegistry.Get<EventBus>();
        if (节点.类型 == "NPC" && !string.IsNullOrEmpty(节点.剧情节点))
            事件.发布(new 打开对话事件(节点.剧情节点, 当前设施标识, 返回节点));
        else if (节点.类型 == "功能物" && !string.IsNullOrEmpty(节点.功能))
        {
            var 逻辑 = 设施工厂.创建(当前设施标识);
            if (逻辑 != null) 事件.发布(new 打开功能面板事件(逻辑, 节点.功能, 返回节点));
        }
    }

    protected override void Awake()
    {
        base.Awake();
        返回按钮?.onClick.AddListener(返回小地图);
    }

    protected override void 刷新(object 上下文)
    {
        if (上下文 is 打开设施内部事件 e) { 当前设施标识 = e.设施标识; 返回节点 = e.返回节点; }
        if (string.IsNullOrEmpty(当前设施标识)) return;
        选中节点 = "";
        渲染框架();
    }

    // 填充设施内部节点（NPC/功能物）
    protected override void 填充节点()
    {
        var 数据 = ServiceRegistry.Get<DataService>();
        if (!数据.设施.TryGetValue(当前设施标识, out var 定义) || 定义.内部节点 == null) return;
        节点数据.Clear();
        foreach (var 节点 in 定义.内部节点)
        {
            节点数据[节点.标识] = 节点;
            创建节点按钮(节点.标识, 节点.名称, 节点.x, 节点.y, false);
        }
        设文本(标题, $"—— {定义.名称} ——");
    }

    // 返回：小地图（城镇:标记）或剧情节点（故事进入设施时）
    private void 返回小地图() => 返回设施(返回节点);
}
