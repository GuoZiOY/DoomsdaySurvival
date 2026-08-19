using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 卖出行：买卖面板「卖出」行模板的绑定组件（挂在卖出行模板上）。
// 简洁展示：名字（含品质标签）/ 数量——详细信息和卖出按钮在右侧详情区；点击行选中（选中图高亮）。
public sealed class 卖出行 : MonoBehaviour
{
    public TMP_Text 名称;      // 品质标签 + 物品名（一个文本，中间空格，如 "[优秀] 皮甲"）
    public TMP_Text 数量;      // 持有数量（如 ×2）
    public Image 选中图;       // 选中高亮（点击行进详情区）
}
