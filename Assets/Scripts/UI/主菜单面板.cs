using UnityEngine;
using UnityEngine.UI;

// 主菜单面板：新游戏（→ 开局构筑）/ 继续冒险（读档）。
// 接线：Inspector 把 新游戏按钮/继续按钮 拖进对应字段。
public sealed class 主菜单面板 : 面板基类
{
    [SerializeField] private Button 新游戏按钮;
    [SerializeField] private Button 继续按钮;

    void Awake()
    {
        if (新游戏按钮 != null) 新游戏按钮.onClick.AddListener(新游戏);
        if (继续按钮 != null) 继续按钮.onClick.AddListener(读档);
    }

    protected override void 刷新(object 上下文) { }

    // 新游戏：打开 开局构筑面板（选职业/分配点/选天赋），确认后才真正创建档案
    private void 新游戏()
    {
        var 管理器 = 面板管理器.实例;
        if (管理器 != null && 管理器.开局构筑面板引用() != null)
            管理器.显示(管理器.开局构筑面板引用());
        else
        {
            // 面板未接线兜底：直接默认构筑（退役军人 + 全默认）
            var 玩家 = ServiceRegistry.Get<PlayerService>();
            玩家.待选职业 = "退役军人";
            玩家.待选天赋.Clear();
            玩家.待选天赋.Add("战术本能");
            玩家.新游戏();
            ServiceRegistry.Get<地图服务>().打开大地图("营地");
        }
    }

    // 继续冒险：读档
    private void 读档()
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>();
        玩家.读档();
        // 读档后进入地图（末日无剧情链，直接回地图）
        ServiceRegistry.Get<地图服务>().打开大地图(string.IsNullOrEmpty(玩家.档案.当前大节点) ? "营地" : 玩家.档案.当前大节点);
    }
}
