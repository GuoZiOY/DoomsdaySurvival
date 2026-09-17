using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// 知识行：知识子面板「左列表」的行模板组件（挂在模板行根节点上，场景里默认 SetActive(false)）。
// 为什么单独一个文件（照 `配方行` 的做法）：行组件是**模板资产上的脚本**，与页面逻辑分开才好让用户在 Unity 里
//   单独把模板行从场景里拆出来摆；塞进 `知识子面板.cs` 会让那个文件既不只讲"页"、又成了两个组件的家。
//
// 只做三件事，别的一概不碰：
//   ① 内容填空（名称 / 等级 / 进度 / 还差 / 持有 / 未掌握提示）；
//   ② **状态只用 `SetActive`** —— 未掌握提示、选中高亮各一个预置节点，显隐由本类切（颜色/字号/尺寸全归场景）；
//   ③ 点击转发（`IPointerClickHandler`，与 `配方行` 同款：不用 Button 也能点，行内子节点点到也算）。
public sealed class 知识行 : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_Text 名称;              // 行名（已由页面按品质着色，本类不再包颜色）
    [SerializeField] private TMP_Text 等级;              // "x / y 级"
    [SerializeField] private TMP_Text 进度;              // "累计经验 E / T"
    [SerializeField] private TMP_Text 还差;              // "还差 N 本"（满级时不写、可连同父节点隐藏）
    [SerializeField] private TMP_Text 持有;              // "持有 M 本"
    [SerializeField] private GameObject 未掌握提示;      // 未掌握那行提示（未掌握 = 亮）
    [SerializeField] private GameObject 选中高亮;        // 选中态预置高亮节点（选中 = 亮；本类不写颜色）
    [SerializeField] private Button 按钮;                // 可选：模板行若用 Button 做点击面，本类也接同一路

    private UnityEngine.Events.UnityAction 点击回调;

    // 点击行：uGUI 事件沿层级冒泡 —— 点到行内任何子节点都算点这一行（照 `配方行`）。
    // 音效不在这里播放：选中音由页面统一播（只在真的换了选中项时才响，连点同一行不会响两遍）。
    public void OnPointerClick(PointerEventData 事件) => 触发();

    private void 触发() => 点击回调?.Invoke();

    public void 绑定(string 名称文本, string 等级文本, string 进度文本, string 还差文本,
                     string 持有文本, bool 掌握, bool 选中, UnityEngine.Events.UnityAction 点击)
    {
        点击回调 = 点击;
        面板基类.设文本(名称, 名称文本);
        面板基类.设文本(等级, 等级文本);
        面板基类.设文本(进度, 进度文本);
        面板基类.设文本(还差, 还差文本);
        面板基类.设文本(持有, 持有文本);

        // 未掌握那行提示：**内容由场景写死**（"未掌握　首次读满解锁"），本类只管亮不亮 ——
        //   文案留在场景里，用户改字不用找代码。
        if (未掌握提示 != null) 未掌握提示.SetActive(!掌握);
        if (选中高亮 != null) 选中高亮.SetActive(选中);

        // 按钮那条路（模板行若挂了 Button）：只在首次绑定时接线一次，之后换的是"这次点谁"的回调
        if (按钮 != null)
        {
            按钮.onClick.RemoveAllListeners();
            按钮.onClick.AddListener(触发);
            音效管理器.实例?.注册按钮(按钮);   // 幂等：悬停/点击反馈沿用项目既有口径
        }
    }

    // 只切选中态：列表行是**池化复用**的（点一行不能重建整个列表，见 `知识子面板.点击行`），
    //   所以刷新选中态时必须能"只翻高亮"，不能连带把行内其它内容再写一遍 —— 免得日后有人在 `绑定` 里
    //   加了带副作用的写法，行会在"点一下"的时候被意外重算。
    public void 绑定选中态(bool 选中)
    {
        if (选中高亮 != null) 选中高亮.SetActive(选中);
    }
}
