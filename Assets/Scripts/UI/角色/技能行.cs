using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// 技能行：技能子面板「下面 · 已学技能列表」的行模板组件（挂在模板行根节点上，场景里默认 SetActive(false)）。
// 一行 = 一个**已学会**的技能（未学会的不列 —— 用户钦定）；行序 = `玩家档案.已学技能` 的原始顺序（学习先后）。
//
// 为什么单独一个文件（照 `知识行` / `配方行` 的做法）：行组件是**模板资产上的脚本**，与页面逻辑分开才好让用户
//   在 Unity 里单独把模板行拆出来摆。
//
// 只做三件事，别的一概不碰：
//   ① 内容填空（名称 / 类别 / 主动·被动 / 熟练 / 已装标记）；
//   ② **状态只用 `SetActive`** —— 待装（选中）高亮一个预置节点，显隐由本类切（颜色 / 字号 / 尺寸全归场景）；
//   ③ 点击转发（`IPointerClickHandler`，与 `知识行` 同款：不用 Button 也能点，行内子节点点到也算行）。
public sealed class 技能行 : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_Text 名称;          // 技能名（**已由页面按品质着色**，本类不再包颜色）
    [SerializeField] private TMP_Text 类别;          // `技能数据.类别` 的 JSON 原字符串
    [SerializeField] private TMP_Text 主动与否;      // "主动" / "被动·不可释放"
    [SerializeField] private TMP_Text 熟练;          // "Lv2/5　30/100"（满级只留 "Lv5/5"）
    [SerializeField] private TMP_Text 已装;          // "已装·第3格" / "未装"
    [SerializeField] private GameObject 待装高亮;    // 待装（选中）态预置高亮节点（本类不写颜色）
    [SerializeField] private Button 按钮;            // 可选：模板行若用 Button 做点击面，本类也接同一路

    private UnityEngine.Events.UnityAction 点击回调;

    // 点击行：uGUI 事件沿层级冒泡 —— 点到行内任何子节点都算点这一行（照 `配方行`）。
    // 音效不在这里播放：待装/取消由页面统一决定（选中的反馈音只在状态真的变了时响）。
    public void OnPointerClick(PointerEventData 事件) => 点击回调?.Invoke();

    public void 绑定(string 名称文本, string 类别文本, bool 被动, string 熟练文本, string 已装文本,
                     bool 待装, UnityEngine.Events.UnityAction 点击)
    {
        点击回调 = 点击;
        面板基类.设文本(名称, 名称文本);
        面板基类.设文本(类别, 类别文本);
        面板基类.设文本(主动与否, 被动 ? "被动·不可释放" : "主动");
        面板基类.设文本(熟练, 熟练文本);
        面板基类.设文本(已装, 已装文本);
        if (待装高亮 != null) 待装高亮.SetActive(待装);

        if (按钮 != null)
        {
            按钮.onClick.RemoveAllListeners();
            按钮.onClick.AddListener(() => 点击回调?.Invoke());
            音效管理器.实例?.注册按钮(按钮);   // 幂等：悬停/点击反馈沿用项目既有口径
        }
    }

    // 只切待装（选中）态：列表行是**池化复用**的，标待装时不必把行内内容再写一遍
    //   —— 免得日后有人在 `绑定` 里加了带副作用的写法，行会在"点一下"的时候被意外重算。
    public void 绑定待装态(bool 待装)
    {
        if (待装高亮 != null) 待装高亮.SetActive(待装);
    }
}
