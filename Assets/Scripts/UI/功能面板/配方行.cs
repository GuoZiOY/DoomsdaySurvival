using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 配方行：制作面板的配方列表专用行（成品名 + 材料摘要 + 三态背景）。
// 三态：可制作=淡绿高亮背景；缺材料=暗灰背景；选中=金色背景 + 缩放放大（面板基类.设选中缩放）。
public sealed class 配方行 : MonoBehaviour
{
    [SerializeField] private TMP_Text 名字;   // [品质] 成品名
    [SerializeField] private TMP_Text 材料;   // 材料×数量 摘要
    [SerializeField] private Image 背景;      // 行背景（高亮/暗/选中金）

    private static readonly Color 高亮色 = new Color(0.42f, 0.55f, 0.4f, 0.9f);      // 可制作：淡绿高亮
    private static readonly Color 缺材色 = new Color(0.32f, 0.32f, 0.32f, 0.85f);    // 缺材料：暗灰
    private static readonly Color 选中色 = new Color(0.65f, 0.52f, 0.28f, 0.95f);    // 选中：金色

    public void 绑定(配方数据 配方, 物品数据 成品, string 材料文本, bool 可制作, bool 选中, System.Action 点击)
    {
        if (名字 != null) 名字.text = 物品工具.品质名称(成品.品质档, 成品.名称);
        if (材料 != null) 材料.text = 材料文本;
        // 三态背景：选中 > 可制作 > 缺材料（可制作性用背景亮度体现，不单列状态文字）
        if (背景 != null) 背景.color = 选中 ? 选中色 : (可制作 ? 高亮色 : 缺材色);
        var 按钮 = GetComponent<Button>();
        if (按钮 != null)
        {
            面板基类.设选中缩放(按钮, 选中);   // 选中态 = 缩放放大（用户原则：选中态缩放）
            按钮.onClick.AddListener(() => 点击());
            音效管理器.实例?.注册按钮(按钮);
        }
    }
}
