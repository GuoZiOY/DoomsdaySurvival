using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 商品行：买卖面板「购买货架」行模板的绑定组件（挂在商品行模板上）。
// 简洁展示：名字（含品质标签）/ 价格——详细信息和买入按钮在右侧详情区；点击行选中（选中图高亮）。
public sealed class 商品行 : MonoBehaviour
{
    public TMP_Text 名称;      // 品质标签 + 物品名（一个文本，中间空格，如 "[优秀] 木剑"）
    public TMP_Text 价格;      // 价格（如 30 金）
    public Image 选中图;       // 选中高亮（点击行进详情区）
}
