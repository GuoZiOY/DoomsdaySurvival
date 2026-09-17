using UnityEngine;
using TMPro;

// 逐级行：知识子面板「右详情 · 逐级表」的行模板组件（挂在模板行根节点上，场景里默认 SetActive(false)）。
// 一行 = 一级：等级 · 门槛 · 状态 · 该级描述 · 该级解锁/加成清单。
//
// 状态为什么用**三个预置节点 + `SetActive`**，而不是改文本颜色：
//   用户口径是"外观全归 Unity"（配色/字号只在场景里调）。三态各放一个节点（标记或文字都行），
//   哪个亮由页面算好后传进来 —— 代码里因此不出现任何配色/字号 API（自查项）。
public sealed class 逐级行 : MonoBehaviour
{
    [SerializeField] private TMP_Text 等级;   // "3 级 · 累计 300"
    [SerializeField] private TMP_Text 状态;   // 兜底：三态文本（用户若只放一个文本节点就写这里）
    [SerializeField] private TMP_Text 描述;   // `知识等级数据.描述`
    [SerializeField] private TMP_Text 清单;   // 结构化清单：解锁配方 / 解锁技能 / 加成属性，每项一行
    [SerializeField] private GameObject 已获得标记;
    [SerializeField] private GameObject 下一级标记;
    [SerializeField] private GameObject 未获得标记;

    // 三态常量：页面与本类共用一套名字，避免两边各写一份字符串而漂掉。
    public const string 已获得 = "已获得";
    public const string 下一级 = "下一级";
    public const string 未获得 = "未获得";

    public void 绑定(string 等级文本, string 状态文本, string 描述文本, string 清单文本)
    {
        面板基类.设文本(等级, 等级文本);
        面板基类.设文本(状态, 状态文本);
        面板基类.设文本(描述, 描述文本);
        面板基类.设文本(清单, 清单文本);

        // 三态显隐：**只看状态字符串**（页面传进来的就是这三种之一）。
        //   为什么不用枚举：这两个类的字段是 Inspector 序列化的，字符串在这里是"人也能看懂"的最小约定
        //   （用户在场景里对着节点名搭，不需要再去对照枚举序号）。
        if (已获得标记 != null) 已获得标记.SetActive(状态文本 == 已获得);
        if (下一级标记 != null) 下一级标记.SetActive(状态文本 == 下一级);
        if (未获得标记 != null) 未获得标记.SetActive(状态文本 == 未获得);
    }
}
