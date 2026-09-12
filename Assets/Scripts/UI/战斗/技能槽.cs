using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 技能槽：战斗沙盒 技能栏 的 单个 固定槽（场景手动搭建，Inspector 接线；战斗沙盒面板 持有 6 个 数组）。
// 组成：图标 Image（类别色底）+ 名称 TMP_Text + 冷却遮罩（Image·Filled·Radial360）+ 冷却文本。
// 交互：Button 点击 → 转发 外壳 开始选技能；冷却中 禁用 + 遮罩 fillAmount=剩余/总（倒计时）+ 剩余秒。
// 冷却 = 现实秒（战斗单位.技能冷却 每帧 推进），非 轮。
public sealed class 技能槽 : MonoBehaviour
{
    [SerializeField] private Image 图标;          // 类别色 底（攻击=红/治疗=绿/增益=蓝/减益=紫/控制=黄）
    [SerializeField] private TMP_Text 名称;        // 技能名（底部）
    [SerializeField] private Image 冷却遮罩;       // 冷却遮罩（Image 类型=Filled，方式=Radial360；fillAmount = 剩余/总 作倒计时）
    [SerializeField] private TMP_Text 冷却文本;    // 遮罩 中央 剩余秒

    private Button 按钮;
    private 战斗沙盒面板 外壳 => GetComponentInParent<战斗沙盒面板>();
    public string 当前技能标识 { get; private set; }   // 本槽 绑定 的 技能标识；空槽 = null
    private float 当前冷却总秒;                       // 绑定 技能 的 总冷却秒（fillAmount 分母）
    private string 名字原文;                           // 绑定 技能名 原文（蓄力 标记 后缀 用）
    private Color 底色 = Color.white;                 // 绑定 技能 的 类别色（不可用 时 在此基础上 压暗）

    private static readonly Color 空槽色 = new Color(0.2f, 0.2f, 0.22f, 1f);
    private const float 不可用压暗 = 0.42f;            // "放不出来"的 图标 压暗 系数
    private const float 不可用字透 = 0.55f;            // "放不出来"的 名称 透明度

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
        当前冷却总秒 = Mathf.Max(1, 技能.冷却);
        底色 = 类别色(技能);
        if (图标 != null) 图标.color = 底色;
        if (名称 != null) { 名称.text = 技能.名称; 名字原文 = 技能.名称; }
        if (冷却遮罩 != null) { 冷却遮罩.gameObject.SetActive(false); 冷却遮罩.fillAmount = 0f; }
        if (冷却文本 != null) 冷却文本.text = "";
        if (按钮 != null) 按钮.interactable = true;
    }

    // 空槽：灰底 + 空槽 + 禁用
    private void 设为空槽()
    {
        当前冷却总秒 = 0;
        名字原文 = null;
        if (图标 != null) 图标.color = 空槽色;
        if (名称 != null) 名称.text = "空槽";
        if (冷却遮罩 != null) { 冷却遮罩.gameObject.SetActive(false); 冷却遮罩.fillAmount = 0f; }
        if (冷却文本 != null) 冷却文本.text = "";
        if (按钮 != null) 按钮.interactable = false;
    }

    // 每帧 状态刷新（面板 轮询）：冷却 + 蓄力 + 可用性 —— **三样合并成一次调用**。
    // ★ v51 刀17（档1 #9）：为什么必须合并 —— `interactable` 原来是 `刷新冷却` 的私有领地
    //   （每帧无条件 `= !冷却中`），再让"可用性"另外写一次就是"谁后写谁赢"的隐性 bug
    //   （本项目已经栽过这种跟订阅/写入顺序有关的坑）。合并后 interactable 全局只有这一个写入点。
    // 不可用（精力不足/无弹药/缺消耗物/武器不对）时：图标压暗 + 名称变淡 + 按钮禁用 + 冷却文本位显示原因。
    public void 刷新状态(float 冷却剩余秒, bool 蓄力中, bool 可用, string 不可用原因)
    {
        if (当前技能标识 == null) return;
        bool 冷却中 = 冷却剩余秒 > 0f;
        if (图标 != null) 图标.color = 冷却中 || 可用 ? 底色 : 底色 * 不可用压暗;
        if (名称 != null)
        {
            名称.text = 蓄力中 ? 名字原文 + "（蓄力中）" : 名字原文;
            名称.alpha = 可用 || 冷却中 ? 1f : 不可用字透;
        }
        if (冷却遮罩 != null)
        {
            冷却遮罩.gameObject.SetActive(冷却中);
            冷却遮罩.fillAmount = 冷却中 ? Mathf.Clamp01(冷却剩余秒 / Mathf.Max(0.1f, 当前冷却总秒)) : 0f;
        }
        // 冷却中 优先 显示 倒计时；否则 显示"为什么 放不出"（短字，留在槽内，不再依赖 6 秒就消失的日志）
        if (冷却文本 != null) 冷却文本.text = 冷却中 ? $"{Mathf.CeilToInt(冷却剩余秒)}秒" : (可用 ? "" : 不可用原因 ?? "");
        if (按钮 != null) 按钮.interactable = 可用;
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
