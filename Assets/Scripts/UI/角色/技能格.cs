using UnityEngine;
using TMPro;

// 技能格：技能子面板「6 个技能槽」的槽格模板组件（节点 = 一张 Image + 一个 Text，**不是按钮**）。
//   本类只写名字（**不写着色**：文本颜色归场景 / 预制体）；换槽走技能行右键的小菜单，格子不接收点击。
public sealed class 技能格 : MonoBehaviour
{
    [SerializeField] private TMP_Text 名称;        // 技能名；空槽 = `空槽文案`

    public const string 空槽文案 = "空槽";

    public void 绑定(string 技能标识, string 名称文本)
        => 面板基类.设文本(名称, string.IsNullOrEmpty(技能标识) ? 空槽文案 : 名称文本);
}
