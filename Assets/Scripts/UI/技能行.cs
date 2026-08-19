using TMPro;
using UnityEngine;

// 技能行：战斗「技能子菜单」行模板的绑定组件（挂在技能行模板上）。
// 字段：技能名（含品质标签）/ 消耗（MP）/ 数值（伤害倍率/恢复量等）。
public sealed class 技能行 : MonoBehaviour
{
    public TMP_Text 技能名;    // 品质标签 + 技能名（一个文本，中间空格，如 "[稀有] 火球术"）
    public TMP_Text 消耗;
    public TMP_Text 数值;
}
