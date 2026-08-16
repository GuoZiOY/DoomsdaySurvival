using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 战斗单位卡牌：战斗单位（敌人/我方共用同一模板）的呈现。
// 场景模板结构: 名称TMP + 生命文本TMP + 魔力文本TMP + 状态文本TMP + 蒙版Image(覆盖整卡,初始隐藏) + Button(卡牌自身)。
// 文字血条(D17-B) · 文字buff行(D19-A) · 卡牌可点击选目标(D18-A)。
// 目标选择高亮走「蒙版」：可选中=显示蒙版+金色半透明；否则隐藏（基础图永不动，不碰 interactable，避免变黑）。
public sealed class 战斗单位卡牌 : MonoBehaviour
{
    [SerializeField] private TMP_Text 名称;
    [SerializeField] private TMP_Text 生命文本;
    [SerializeField] private TMP_Text 魔力文本;
    [SerializeField] private TMP_Text 状态文本;
    [SerializeField] private Image 蒙版;     // 覆盖整卡的高亮蒙版（初始隐藏），目标选择时显示
    [SerializeField] private Color 可选蒙版色 = new Color(0.85f, 0.64f, 0.25f, 0.35f);   // 可选中高亮色（可在 Inspector 调）
    [SerializeField] private Color 不可选蒙版色 = new Color(0.45f, 0.45f, 0.5f, 0.25f);  // 选目标中但不可选（灰半透明）
    private Button 按钮;

    public 战斗单位 单位 { get; private set; }
    private System.Action<战斗单位> 点击回调;

    void Awake()
    {
        按钮 = GetComponent<Button>();
        if (按钮 != null) 按钮.onClick.AddListener(() => 点击回调?.Invoke(单位));
        if (蒙版 != null) 蒙版.gameObject.SetActive(false);
    }

    // 绑定单位 + 点击回调；名称着色（玩家金 / 敌人危险红）
    public void 绑定(战斗单位 单位, System.Action<战斗单位> 点击回调)
    {
        this.单位 = 单位; this.点击回调 = 点击回调;
        名称.text = 单位.名称;
        名称.color = 单位.是否我方 ? 游戏主题.金色 : 游戏主题.危险;
        刷新();
    }

    // 刷新 生命/魔力/状态 文本
    public void 刷新()
    {
        if (单位 == null) return;
        生命文本.text = $"生命 {单位.生命}/{单位.最大生命}";
        if (单位.最大魔力 > 0)
        {
            魔力文本.gameObject.SetActive(true);
            魔力文本.text = $"魔力 {单位.魔力}/{单位.最大魔力}";
        }
        else 魔力文本.gameObject.SetActive(false);
        状态文本.text = 状态文本串();
    }

    private string 状态文本串()
    {
        if (单位.Buffs.Count == 0) return "";
        var 段 = new List<string>();
        foreach (var b in 单位.Buffs)
        {
            string 名 = b.定义.名称;
            if (b.定义.类型枚举 != Buff类型.护盾 && b.剩余回合 > 0) 名 += $" {b.剩余回合}回合";
            if (b.层数 > 1) 名 += $"×{b.层数}";
            段.Add($"[{名}]");
        }
        return string.Join(" ", 段);
    }

    // 目标选择态：显示蒙版并着色（可选=亮色，不可选=暗色）
    public void 设可选(bool 可选)
    {
        if (蒙版 == null) return;
        蒙版.gameObject.SetActive(true);
        蒙版.color = 可选 ? 可选蒙版色 : 不可选蒙版色;
    }

    // 清除高亮：隐藏蒙版（战斗开始/返回/回合切换时，恢复干净卡面）
    public void 清除高亮()
    {
        if (蒙版 != null) 蒙版.gameObject.SetActive(false);
    }

    // 行动动画：卡牌向上跳一下再落回（表示该单位行动了）
    private Coroutine 跳跃协程;
    public void 行动跳跃()
    {
        if (跳跃协程 != null) StopCoroutine(跳跃协程);
        跳跃协程 = StartCoroutine(跳跃动画());
    }

    // 行动跳跃时长（供 战斗面板 等待动画完成后结算逻辑）
    public const float 行动跳跃时长 = 0.5f;

    private System.Collections.IEnumerator 跳跃动画()
    {
        var 矩形 = (RectTransform)transform;
        var 原 = 矩形.anchoredPosition;
        const float 高度 = 18f;
        float t = 0f;
        while (t < 行动跳跃时长)
        {
            t += Time.deltaTime;
            矩形.anchoredPosition = 原 + new Vector2(0f, 高度 * Mathf.Sin(Mathf.PI * t / 行动跳跃时长));   // 正弦弧：起→顶→落
            yield return null;
        }
        矩形.anchoredPosition = 原;
    }
}
