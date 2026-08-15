using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 任务板面板：任务板设施的交互表现（薄视图——数据从 任务板设施 逻辑拿，点行调逻辑方法）。
public sealed class 任务板面板 : 面板基类
{
    [SerializeField] private TMP_Text 标题;
    [SerializeField] private RectTransform 任务列表;
    [SerializeField] private Button 返回按钮;
    private string 返回节点;
    private 任务板设施 任务板;

    void Awake()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<任务进度事件>(_ => 刷新(null));   // 接取/推进后刷新进度
        返回按钮?.onClick.AddListener(() => 返回设施(返回节点));
    }

    protected override void 刷新(object 上下文)
    {
        if (上下文 is 设施打开上下文 c) { 任务板 = c.逻辑 as 任务板设施; 返回节点 = c.返回节点; }
        if (任务板 == null) return;
        设文本(标题, $"—— {任务板.名称} ——");
        清空(任务列表);
        foreach (var 任务 in 任务板.可接任务())
        {
            var 标识 = 任务.标识;
            string 文字 = $"{任务.名称}（{任务.描述}）  +{任务.奖励金币}金 +{任务.奖励经验}经验  [{任务板.进度文本(标识)}]";
            创建行(任务列表, 文字, () => 任务板.尝试接取(标识));
        }
    }
}
