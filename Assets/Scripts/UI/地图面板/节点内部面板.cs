using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 节点内部面板：任意节点的内部图（NPC + 功能物），三级导航第三级。继承 地图面板基类（平移缩放/单击/双击复用）。
// 内部节点无"移动"概念（单击=无操作）；双击 NPC → 对话面板（剧情）；双击 功能物 → 对应功能面板（用 节点.设施 的功能逻辑）。
// 返回上级统一走 侧边栏取消按钮（回退 = 返回上级），面板内不再放置 返回按钮。
public sealed class 节点内部面板 : 地图面板基类
{
    [SerializeField] private TMP_Text 标题;
    private 地图节点 当前节点;
    private string 返回节点;
    private readonly Dictionary<string, 设施内部节点> 节点数据 = new Dictionary<string, 设施内部节点>();

    protected override bool 是当前节点(string 标识) => false;   // 内部无「当前所在」

    // 内部无移动：单击 = 无操作（只留双击进入交互）
    protected override void 执行移动(string 标识) { }

    // 双击动作：NPC → 对话；功能物 → 功能面板
    protected override void 执行进入(string 标识)
    {
        if (!节点数据.TryGetValue(标识, out var 内部节点)) return;
        var 事件 = ServiceRegistry.Get<EventBus>();
        if (内部节点.类型 == "NPC" && !string.IsNullOrEmpty(内部节点.剧情节点))
            事件.发布(new 打开对话事件(内部节点.剧情节点, 当前节点, 返回节点));
        else if (内部节点.类型 == "功能物" && !string.IsNullOrEmpty(内部节点.功能) && !string.IsNullOrEmpty(当前节点.设施))
        {
            var 逻辑 = 设施工厂.创建(当前节点.设施);
            if (逻辑 != null) 事件.发布(new 打开功能面板事件(逻辑, 内部节点.功能, 当前节点, 返回节点));
        }
        else { 音效管理器.实例?.播放失败(); 事件.发布(new 日志事件(日志类型.系统, "这里没有什么可互动的。")); }
    }

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void 刷新(object 上下文)
    {
        if (上下文 is 打开节点内部事件 e) { 当前节点 = e.节点; 返回节点 = e.返回节点; }
        if (当前节点 == null) return;
        渲染框架();
    }

    // 填充节点内部（NPC/功能物）
    protected override void 填充节点()
    {
        if (当前节点?.内部 == null) return;
        节点数据.Clear();
        foreach (var 内部节点 in 当前节点.内部)
        {
            节点数据[内部节点.标识] = 内部节点;
            创建节点按钮(内部节点.标识, 内部节点.名称, 内部节点.x, 内部节点.y, false);
        }
        设文本(标题, $"—— {当前节点.名称} ——");
    }

    // 返回：小地图（城镇:标记）或剧情节点（故事进入时）
    private void 返回上级() => 返回设施(返回节点);

    // 全局取消 = 返回上级（无返回目标则不可取消）
    public override bool 回退()
    {
        if (string.IsNullOrEmpty(返回节点)) return false;
        返回上级();
        return true;
    }

    public override string 取消文本 => "返回";
}
