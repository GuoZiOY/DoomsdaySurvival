using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// 技能格：技能子面板「上面那 6 个技能槽」的槽格模板组件（挂在模板格根节点上，场景里默认 SetActive(false)）。
// 为什么单独一个文件（照 `知识行` / `配方行` 的做法）：槽格是**模板资产上的脚本**，与页面逻辑分开才好让用户
//   在 Unity 里单独把模板格拆出来摆；塞进 `技能子面板.cs` 会让那个文件既不只讲"页"、又成了两个组件的家。
//
// ★ 槽索引 ↔ 数据下标 ↔ 界面格号（**三处必须同一套**，写在这里免得下次又猜）：
//   槽索引 i（本组件由页面传入） == `玩家档案.战斗技能槽` 的下标 i == 界面第 i+1 格（格上写的序号 = i+1）。
//   所以「第 3 格」在代码里永远是下标 2 —— 页面按 `成长管理器.技能槽数` 遍历，不与任何写死的 6 对齐。
//
// 只做四件事，别的一概不碰：
//   ① 内容填空（序号 / 技能名 / 类别 / 品质 / 主动·被动 / 熟练 / 被动附注 / 空槽）；
//   ② **状态只用 `SetActive` 与 `interactable`** —— 被动提示、空槽提示、待装高亮各一个预置节点，
//      显隐由本类切（颜色 / 字号 / 尺寸全归场景，本文件 0 处外观 API）；
//   ③ 点击转发（`IPointerClickHandler`，与 `知识行` 同款：不用 Button 也能点）；
//   ④ **被动硬规矩**：不可点（`interactable = false` + 点击回调置空）、且**不显示 精力 / 冷却**
//      （数据里有值，但对被动永不生效 —— 显示出来就是骗玩家）。
public sealed class 技能格 : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_Text 序号;        // "1".."6"（槽位序号 = 索引 + 1）
    [SerializeField] private TMP_Text 名称;        // 技能名（已由页面按品质着色）；空槽 = 页面传"空槽"
    [SerializeField] private TMP_Text 类别;        // `技能数据.类别` 的 JSON 原字符串
    [SerializeField] private TMP_Text 品质;        // `品质工具.标签`（如 <color=…>[优秀]</color>）
    [SerializeField] private TMP_Text 被动与否;    // "主动" / "被动"
    [SerializeField] private TMP_Text 熟练;        // "Lv2/5　30/100"（满级只留 "Lv5/5"）
    [SerializeField] private TMP_Text 被动说明;    // 被动：固定提示 + 增幅/机制附注；主动 = 空
    [SerializeField] private GameObject 空槽提示;  // 空槽那行提示亮着（本类不写它的颜色/字号）
    [SerializeField] private GameObject 待装高亮;  // 待装/选中态预置高亮节点（只切显隐）
    [SerializeField] private Button 按钮;          // 可选：模板格若用 Button 做点击面，本类也接同一路

    // 被动那一行固定文案（用户钦定）：它同时解释了两件事 —— 开局就生效 + 占了槽但不能点。
    public const string 被动提示 = "被动·开局生效，占槽不可点击释放";

    private UnityEngine.Events.UnityAction 点击回调;
    private bool 可点;

    // 点击格：uGUI 事件沿层级冒泡 —— 点到格内任何子节点都算点这一格（照 `知识行`）。
    // 音效不在这里播：页面统一播（成功音随 `注册按钮`，失败音由页面按结果播）。
    public void OnPointerClick(PointerEventData 事件)
    {
        if (!可点) return;   // 被动 / 空槽的"不可点"在这里兜底（`interactable = false` 只管 Button 那条路）
        点击回调?.Invoke();
    }

    // 绑定一个槽格。
    //   技能标识为空 = 空槽；技能数据为 null = **数据缺失**（已学会但 skills.json 里没有）→ 名称附"（数据缺失）"，
    //   不编造数值。`被动` 为 true 时按被动硬规矩走（见文件头 ④）。
    public void 绑定(int 槽索引, string 技能标识, 技能数据 技能, string 名称文本, string 类别文本,
                     string 品质文本, int 熟练等级, int 熟练度, int 每级熟练度, int 熟练上限,
                     bool 待装, UnityEngine.Events.UnityAction 点击)
    {
        面板基类.设文本(序号, (槽索引 + 1).ToString());

        bool 空 = string.IsNullOrEmpty(技能标识);
        bool 被动 = 技能 != null && 技能.被动;
        可点 = !空 && !被动;          // 空槽点了没有可做的事（页面另有"空槽但无待装"的弱提示）；被动永不可点
        点击回调 = 可点 ? 点击 : null;

        if (空)
        {
            面板基类.设文本(名称, "空槽");
            面板基类.设文本(类别, "");
            面板基类.设文本(品质, "");
            面板基类.设文本(被动与否, "");
            面板基类.设文本(熟练, "");
            面板基类.设文本(被动说明, "");
        }
        else
        {
            面板基类.设文本(名称, 名称文本);
            面板基类.设文本(类别, 类别文本);
            面板基类.设文本(品质, 品质文本);
            面板基类.设文本(被动与否, 被动 ? "被动" : "主动");
            面板基类.设文本(熟练, 熟练文本(熟练等级, 熟练度, 每级熟练度, 熟练上限));
            面板基类.设文本(被动说明, 被动 ? 被动附注(技能) : "");
        }

        if (空槽提示 != null) 空槽提示.SetActive(空);
        if (待装高亮 != null) 待装高亮.SetActive(待装);

        // Button 那条路：只在每次绑定时重接一次（幂等）；被动/空槽整颗置灰 —— `interactable = false` 是
        //   状态表达，不是外观（颜色/过渡仍在场景里调，本类不碰）。
        if (按钮 != null)
        {
            按钮.onClick.RemoveAllListeners();
            if (可点) 按钮.onClick.AddListener(触发);   // 空回调 + interactable=false 双保险
            按钮.interactable = 可点;
            if (可点) 音效管理器.实例?.注册按钮(按钮);   // 幂等：悬停/点击反馈沿用项目既有口径
        }
    }

    // 只切待装/选中态：槽格是**池化复用**的（写操作后全量重算，见 `技能子面板`），
    //   所以重算时可以只翻高亮，不必把格内内容再写一遍。
    public void 绑定待装态(bool 待装)
    {
        if (待装高亮 != null) 待装高亮.SetActive(待装);
    }

    private void 触发() => 点击回调?.Invoke();

    // 熟练行：未满级 = "Lv{等级}/{上限}　{熟练度}/{每级熟练度}"；**满级只留 "Lv{上限}/{上限}"**
    //   （满级后 熟练度 不再累积、也没有下一级，继续显示 "{熟练度}/{每级}" 会让玩家以为还能再涨）。
    private static string 熟练文本(int 等级, int 熟练度, int 每级熟练度, int 上限)
        => 等级 >= 上限 ? $"Lv{上限}/{上限}" : $"Lv{等级}/{上限}　{熟练度}/{每级熟练度}";

    // 被动附注（用户钦定）：固定提示 + 按被动类型补一句"它到底做了什么"。
    //   · 增幅型（`被动类型 == "增幅"`）：亮出 `增幅属性 +值 单位`（如 "命中 +5 点" / "近战 +10 %"）；
    //   · 机制型：亮出 `描述`（它的效果写在战斗逻辑里，字段里没有可显示的数值）；
    //   · 被动类型为空/认不出：只留固定提示 —— **不猜、不编**（宁缺勿错）。
    private static string 被动附注(技能数据 技能)
    {
        if (技能 == null) return 被动提示;
        if (技能.被动类型 == "增幅" && !string.IsNullOrEmpty(技能.增幅属性))
            return $"{被动提示}（{技能.增幅属性} +{数值(技能.增幅值)} {技能.增幅单位}）";
        if (技能.被动类型 == "机制" && !string.IsNullOrEmpty(技能.描述))
            return $"{被动提示}（{技能.描述}）";
        return 被动提示;
    }

    // 增幅值的打印：整数不带小数点（10 而不是 10.0），非整数原样（2.5 还是 2.5）。
    private static string 数值(float 值)
        => Mathf.Approximately(值, Mathf.Round(值)) ? Mathf.RoundToInt(值).ToString() : 值.ToString();
}
