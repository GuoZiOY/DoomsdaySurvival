using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 任务板面板：任务接取与进度的交互表现。
public sealed class 任务板面板 : 面板基类
{
    [SerializeField] private TMP_Text 标题;
    [SerializeField] private RectTransform 任务列表;
    [SerializeField] private Button 返回按钮;
    private string 返回节点;

    void Awake()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<任务进度事件>(_ => 刷新(null));   // 接取/推进后刷新进度
        返回按钮?.onClick.AddListener(() => 返回设施(返回节点));
    }

    protected override void 刷新(object 上下文)
    {
        if (上下文 is 打开设施事件 e) 返回节点 = e.返回节点;
        设文本(标题, "—— 任务板 ——");
        清空(任务列表);
        var 玩家 = ServiceRegistry.Get<PlayerService>().档案;
        var 数据 = ServiceRegistry.Get<DataService>();
        foreach (var 任务 in 数据.任务.Values)
        {
            var 标识 = 任务.标识;
            string 进度 = "接取";
            foreach (var p in 玩家.任务)
                if (p.标识 == 标识) 进度 = p.已完成 ? "已完成" : $"{p.数量}/{任务.目标数量}";
            string 文字 = $"{任务.名称}（{任务.描述}）  +{任务.奖励金币}金 +{任务.奖励经验}经验  [{进度}]";
            创建行(任务列表, 文字, () => ServiceRegistry.Get<QuestService>().接取(标识));
        }
    }
}
