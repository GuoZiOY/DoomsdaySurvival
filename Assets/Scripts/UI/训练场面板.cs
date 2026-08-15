using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 训练场面板：训练场设施的交互表现（薄视图——数据从 训练场设施 逻辑拿，点行调逻辑方法）。
public sealed class 训练场面板 : 面板基类
{
    [SerializeField] private TMP_Text 标题;
    [SerializeField] private RectTransform 技能列表;
    [SerializeField] private Button 返回按钮;
    private string 返回节点;
    private 训练场设施 训练场;

    void Awake()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<金币变化事件>(_ => 刷新(null));   // 学习后刷新已学状态
        返回按钮?.onClick.AddListener(() => 返回设施(返回节点));
    }

    protected override void 刷新(object 上下文)
    {
        if (上下文 is 设施打开上下文 c) { 训练场 = c.逻辑 as 训练场设施; 返回节点 = c.返回节点; }
        if (训练场 == null) return;
        设文本(标题, $"—— {训练场.名称} ——");
        清空(技能列表);
        foreach (var 技能 in 训练场.可学习技能())
        {
            var 标识 = 技能.标识;
            创建行(技能列表, $"{技能.名称}（{技能.描述}）  {技能.价格}金", () => 训练场.尝试学习(标识));
        }
    }
}
