using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 属性行引用：属性页一个基础属性行的静态引用（数值文本 + 加点按钮）
[Serializable]
public class 属性行引用
{
    public TMP_Text 值;
    public Button 加点;
}

// 属性子面板：角色面板 [属性] Tab 页的组件（挂在「属性页」物体上）。
// 静态引用布局（无预制体实例化）：四维 + 加点按钮 + 派生能力 全部 Inspector 数组引用，刷新原地更新数值。
// 自刷新：订阅 属性/生命/精力变化事件；父面板只负责 Tab 切换，不参与本页内容。
public sealed class 属性子面板 : MonoBehaviour
{
    // 五维行（索引 = 体质/力量/智慧/敏捷/意志）；行 = 值文本 + 加点按钮
    [SerializeField] private 属性行引用[] 四维;
    // 派生能力值文本（索引 = 生命/行动点/近战/枪械/暴击/闪避/负重/感知）
    [SerializeField] private TMP_Text[] 能力值;

    private static readonly 属性类型[] 四维类型 = { 属性类型.体质, 属性类型.力量, 属性类型.智慧, 属性类型.敏捷, 属性类型.意志 };

    void Awake()
    {
        for (int i = 0; i < 四维类型.Length && i < (四维?.Length ?? 0); i++)
        {
            var 类型 = 四维类型[i];
            var 行 = 四维[i];
            if (行?.加点 != null) 行.加点.onClick.AddListener(() => 面板操作.加点(ServiceRegistry.Get<PlayerService>().档案, 类型));
        }
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件?.订阅<属性变化事件>(_ => 刷新());
        事件?.订阅<生命变化事件>(_ => 刷新());
        事件?.订阅<精力变化事件>(_ => 刷新());
        刷新();
    }

    // 页面激活时兜底刷新一次（父面板切 Tab 用 SetActive 控制显隐）
    private void OnEnable() => 刷新();

    // 原地刷新数值（无克隆、无重建）
    public void 刷新()
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null) return;
        bool 可加点 = 玩家.自由属性点 > 0;

        // 五维（值 + 加点按钮显隐）
        var 四维值 = new[] { 玩家.体质, 玩家.力量, 玩家.智慧, 玩家.敏捷, 玩家.意志 };
        for (int i = 0; i < 四维值.Length && i < (四维?.Length ?? 0); i++)
        {
            var 行 = 四维[i];
            if (行 == null) continue;
            面板基类.设文本(行.值, $"{四维值[i]}");
            if (行.加点 != null) 行.加点.gameObject.SetActive(可加点);
        }

        // 派生能力（生命/行动点只显示上限——属性面板看成长上限，不看随时变化的当前值）
        var 能力文本 = new[] {
            $"{玩家.最大生命}", $"{玩家.最大行动点}",
            $"{玩家.近战伤害}", $"{玩家.枪械伤害}",
            $"{Mathf.RoundToInt(玩家.暴击概率 * 100)}%", $"{Mathf.RoundToInt(玩家.闪避概率 * 100)}%",
            $"{玩家.负重上限}", $"{玩家.感知}" };
        for (int i = 0; i < 能力文本.Length && i < (能力值?.Length ?? 0); i++)
            面板基类.设文本(能力值[i], 能力文本[i]);
    }
}
