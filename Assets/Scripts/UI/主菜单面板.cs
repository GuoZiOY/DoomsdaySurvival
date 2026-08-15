using UnityEngine;
using UnityEngine.UI;

// 主菜单面板：新游戏 / 继续冒险（读档）。
// 接线：Inspector 把 新游戏按钮/继续按钮 拖进对应字段。
public sealed class 主菜单面板 : 面板基类
{
    [SerializeField] private Button 新游戏按钮;
    [SerializeField] private Button 继续按钮;

    void Awake()
    {
        if (新游戏按钮 != null) 新游戏按钮.onClick.AddListener(() => 开始游戏(false));
        if (继续按钮 != null) 继续按钮.onClick.AddListener(() => 开始游戏(true));
    }

    protected override void 刷新(object 上下文) { }

    // 开始游戏：新游戏或读档 → 进入当前剧情节点（显示剧情事件驱动 主视窗面板 打开）
    private void 开始游戏(bool 读档)
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>();
        var 对话 = ServiceRegistry.Get<DialogueService>();
        if (读档) 玩家.读档(); else 玩家.新游戏();
        对话.进入节点(玩家.档案.当前节点);
    }
}
