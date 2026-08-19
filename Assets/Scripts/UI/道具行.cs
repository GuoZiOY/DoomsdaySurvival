using TMPro;
using UnityEngine;

// 道具行：战斗「道具子菜单」行模板的绑定组件（挂在道具行模板上）。
// 字段：名字（含品质标签）/ 数量——不设选中图（选中是下一步"选目标"的事，子菜单内无需选中态）。
public sealed class 道具行 : MonoBehaviour
{
    public TMP_Text 名字;      // 品质标签 + 物品名（一个文本，中间空格，如 "[优秀] 治疗药水"）
    public TMP_Text 数量;      // 数量角标（数量>1 显示 ×N）
}
