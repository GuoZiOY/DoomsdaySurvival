using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 背包行：背包列表「行模板」的绑定组件（挂在行模板根节点上）。
// 字段：名字（含品质标签，如 [稀有] 铁剑）/ 数量 / 选中图——其余信息（描述/数值/价格/操作）由右侧详情区承载。
// 行本体是 Button（点击=选中；双击=快速操作），接线由 装备背包子面板 负责。
public sealed class 背包行 : MonoBehaviour
{
    public TMP_Text 名字;      // 品质标签 + 物品名（一个文本，中间空格，如 "[稀有] 铁剑"）
    public TMP_Text 数量;      // 数量角标（数量>1 显示 ×N）
    public Image 选中图;       // 选中高亮（选中时显示）
}
