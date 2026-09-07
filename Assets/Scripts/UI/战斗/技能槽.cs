using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 技能槽：战斗沙盒 技能栏 的 单个 固定槽（场景手动搭建，Inspector 接线；战斗沙盒面板 持有 6 个 数组）。
// 组成：图标 Image（类别色底）+ 名称 TMP_Text + 冷却遮罩（Image·Filled·Radial360）+ 冷却文本。
// 交互：Button 点击 → 转发 外壳 开始选技能；冷却中 禁用 + 遮罩 fillAmount=剩余/总（倒计时）+ 剩余轮数。
public sealed class 技能槽 : MonoBehaviour
{
    [SerializeField] private Image 图标;          // 类别色 底（攻击=红/治疗=绿/增益=蓝/减益=紫/控制=黄）
    [SerializeField] private TMP_Text 名称;        // 技能名（底部）
    [SerializeField] private Image 冷却遮罩;       // 冷却遮罩（Image 类型=Filled，方式=Radial360；fillAmount = 剩余/总 作倒计时）
    [SerializeField] private TMP_Text 冷却文本;    // 遮罩 中央 剩余轮数

    private Button 按钮;
    private 战斗沙盒面板 外壳 => GetComponentInParent<战斗沙盒面板>();
    public string 当前技能标识 { get; private set; }   // 本槽 绑定 的 技能标识；空槽 = null
    private int 当前冷却总轮;                          // 绑定 技能 的 总冷却轮数（fillAmount 分母）

    private static readonly Color 空槽色 = new Color(0.2f, 0.2f, 0.22f, 1f);

    void Awake()
    {
        按钮 = GetComponent<Button>();
        if (按钮 != null) 按钮.onClick.AddListener(() => 外壳?.点击技能槽(当前技能标识));
    }

    // 绑定 技能（空/无效 → 空槽 状态）
    public void 绑定(string 技能标识, DataService 数据)
    {
        当前技能标识 = string.IsNullOrEmpty(技能标识) ? null : 技能标识;
        if (当前技能标识 == null || 数据 == null || !数据.技能.TryGetValue(技能标识, out var 技能))
        {
            设为空槽();
            return;
        }
        当前冷却总轮 = Mathf.Max(1, 技能.冷却);
        if (图标 != null) 图标.color = 类别色(技能);
        if (名称 != null) 名称.text = 技能.名称;
        if (冷却遮罩 != null) { 冷却遮罩.gameObject.SetActive(false); 冷却遮罩.fillAmount = 0f; }
        if (冷却文本 != null) 冷却文本.text = "";
        if (按钮 != null) 按钮.interactable = true;
    }

    // 空槽：灰底 + 空槽 + 禁用
    private void 设为空槽()
    {
        当前冷却总轮 = 0;
        if (图标 != null) 图标.color = 空槽色;
        if (名称 != null) 名称.text = "空槽";
        if (冷却遮罩 != null) { 冷却遮罩.gameObject.SetActive(false); 冷却遮罩.fillAmount = 0f; }
        if (冷却文本 != null) 冷却文本.text = "";
        if (按钮 != null) 按钮.interactable = false;
    }

    // 每帧 冷却 刷新：剩余轮 > 0 → 遮罩（fillAmount = 剩余/总，Radial360 倒计时）+ 禁用 + 文本；否则 清
    public void 刷新冷却(int 剩余轮)
    {
        if (当前技能标识 == null) return;
        bool 冷却中 = 剩余轮 > 0;
        if (冷却遮罩 != null)
        {
            冷却遮罩.gameObject.SetActive(冷却中);
            冷却遮罩.fillAmount = 冷却中 ? Mathf.Clamp01((float)剩余轮 / 当前冷却总轮) : 0f;
        }
        if (冷却文本 != null) 冷却文本.text = 冷却中 ? $"{剩余轮}轮" : "";
        if (按钮 != null) 按钮.interactable = !冷却中;
    }

    private static Color 类别色(技能数据 技能) => 技能.类别枚举 switch
    {
        技能类别.攻击 => new Color(0.78f, 0.28f, 0.24f, 1f),
        技能类别.治疗 => new Color(0.35f, 0.8f, 0.45f, 1f),
        技能类别.增益 => new Color(0.45f, 0.62f, 1f, 1f),
        技能类别.减益 => new Color(0.6f, 0.4f, 0.8f, 1f),
        技能类别.控制 => new Color(0.9f, 0.72f, 0.3f, 1f),
        _ => new Color(0.5f, 0.6f, 0.7f, 1f),   // 净化
    };
}
