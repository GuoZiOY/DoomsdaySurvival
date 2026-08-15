using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 训练场面板：训练场设施的交互表现（薄视图）。两区：属性训练 + 技能熟练训练。
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
        事件.订阅<金币变化事件>(_ => 刷新(null));   // 训练后刷新状态
        返回按钮?.onClick.AddListener(() => 返回设施(返回节点));
    }

    protected override void 刷新(object 上下文)
    {
        if (上下文 is 设施打开上下文 c) { 训练场 = c.逻辑 as 训练场设施; 返回节点 = c.返回节点; }
        if (训练场 == null) return;
        var 玩家 = 训练场.当前玩家;
        设文本(标题, $"—— {训练场.名称} ——");
        清空(技能列表);

        // 属性训练区
        foreach (var 项目 in 训练场.可训练项目())
        {
            var 标识 = 项目.标识;
            int 当前 = 玩家.属性值(项目.属性);
            创建行(技能列表, $"【属性】{项目.名称}（{项目.属性} {当前}→{当前 + 项目.加成}）  {项目.花费}金", () => 训练场.尝试属性训练(标识));
        }

        // 技能熟练训练区（+熟练度点数，满了升级熟练等级）
        foreach (var 技能 in 训练场.可练熟练技能())
        {
            var 标识 = 技能.标识;
            int 等级 = 玩家.技能熟练等级(标识);
            int 进度 = 玩家.技能熟练度(标识);
            int 每级 = 训练场.每级熟练度(技能);
            int 花费 = 训练场.熟练训练花费(标识);
            创建行(技能列表, $"【熟练】{技能.名称} 等级{等级}/{玩家档案.熟练等级上限}  熟练 {进度}/{每级}  +{训练场设施.熟练训练点数}    {花费}金", () => 训练场.尝试熟练训练(标识));
        }
    }
}
