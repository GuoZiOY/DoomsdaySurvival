using UnityEngine;
using UnityEngine.UI;

// 主菜单面板（末日《最后87天》）：开始新游戏 / 继续游戏 / 设置 / 退出。
// 接线：Inspector 把 新游戏/继续/设置/退出 按钮 + 全局设置面板 拖进对应字段。
// 新游戏 → 角色创建面板；继续 → 读档（无存档禁用）；设置 → 打开全局设置面板；退出 → Application.Quit()。
public sealed class 主菜单面板 : 面板基类
{
    [SerializeField] private Button 新游戏按钮;
    [SerializeField] private Button 继续按钮;
    [SerializeField] private Button 设置按钮;
    [SerializeField] private Button 退出按钮;
    // 注：设置 / 存档 两个覆盖层**不在这里留引用位** —— 开关收口在 `面板管理器`（打开设置() / 打开存档()），
    //   主菜单只是快捷方式（原来这里也各拖了一份，同一个面板两处引用位）。

    void Awake()
    {
        if (新游戏按钮 != null) 新游戏按钮.onClick.AddListener(开始新游戏);
        if (继续按钮 != null) 继续按钮.onClick.AddListener(继续游戏);
        if (设置按钮 != null) 设置按钮.onClick.AddListener(打开设置);
        if (退出按钮 != null) 退出按钮.onClick.AddListener(退出游戏);
    }

    protected override void 刷新(object 上下文)
    {
        // 每次显示主菜单：刷新"继续"可用状态
        bool 有存档 = ServiceRegistry.Get<SaveService>()?.有任何可读存档() ?? false;
        if (继续按钮 != null) 继续按钮.interactable = 有存档;
    }

    // 开始新游戏：打开 角色创建面板（确认后才真正创建档案）
    private void 开始新游戏()
    {
        var 管理器 = 面板管理器.实例;
        if (管理器 != null && 管理器.角色创建面板引用() != null)
            管理器.显示(管理器.角色创建面板引用());
        else
        {
            // 面板未接线兜底：直接默认构筑（退役军人 + 全默认）
            var 玩家 = ServiceRegistry.Get<PlayerService>();
            玩家.待选职业 = "退役军人";
            玩家.待选天赋.Clear();
            玩家.待选天赋.Add("战斗直觉");
            玩家.新游戏();
            ServiceRegistry.Get<大世界探索服务>()?.打开默认世界();
        }
    }

    // 继续游戏：打开存档面板（读取模式）。★ 刀64：不再"直接读最近那一个档" ——
    // 一个存档游戏必须让玩家看见有哪些档、各自是谁、什么时候存的。无档时按钮已禁用，这里兜底。
    private void 继续游戏()
    {
        var 存档 = ServiceRegistry.Get<SaveService>();
        if (!(存档?.有任何可读存档() ?? false))
        {
            音效管理器.实例?.播放失败();
            ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.警告, "没有可继续的存档。"));
            return;
        }
        面板管理器.实例?.打开存档(true);
    }

    // 设置：打开全局设置面板
    private void 打开设置()
    {
        面板管理器.实例?.打开设置();
    }

    // 退出游戏
    private void 退出游戏()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
