using System.Collections.Generic;
using DG.Tweening;
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
    [SerializeField] private Color 行动者高亮色 = new Color(1f, 0.84f, 0.5f, 0.30f);    // 行动者抬升时的"轮到TA"淡金光（可在 Inspector 调）
    private Button 按钮;

    // —— 演出参数（可在 Inspector 调）——
    [SerializeField] private float 受击震幅 = 6f;       // 受击横向震颤幅度（像素）
    [SerializeField] private int 受击震次 = 4;          // 受击震颤次数
    [SerializeField] private float 受击时长 = 0.25f;    // 受击震颤时长
    [SerializeField] private float 受伤变暗 = 0.45f;    // 受击短暂变暗的最大灰暗强度(0~1)
    [SerializeField] private float 阵亡时长 = 0.4f;     // 死亡淡出+倒下时长
    [SerializeField] private float 阵亡右移 = 190f;    // 死亡向右退场位移（像素）
    [SerializeField] private float 阵亡倒角 = 70f;     // 死亡倒下旋转角（度，倾向右侧）
    [SerializeField] private float 强调抬升 = 14f;      // 行动者强调抬升高度（像素）
    [SerializeField] private float 治疗脉强 = 0.06f;   // 治疗脉冲缩放强度

    public 战斗单位 单位 { get; private set; }
    private System.Action<战斗单位> 点击回调;
    private Tween 震动补间, 强调补间, 死亡补间;

    void Awake()
    {
        按钮 = GetComponent<Button>();
        if (按钮 != null) 按钮.onClick.AddListener(() => 点击回调?.Invoke(单位));
        if (蒙版 != null) 蒙版.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        震动补间?.Kill(); 强调补间?.Kill(); 死亡补间?.Kill();
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
        var 段 = new List<string>();
        if (单位.有护盾)
            段.Add(单位.破防 ? "[破防]" : $"[护盾 {单位.护盾}/{单位.护盾上限}]");
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
        蒙版.DOKill();   // 接管可能仍在飞的行动者高亮淡出，确保目标选择色即时生效
        蒙版.gameObject.SetActive(true);
        蒙版.color = 可选 ? 可选蒙版色 : 不可选蒙版色;
    }

    // 清除高亮：隐藏蒙版（战斗开始/返回/回合切换时，恢复干净卡面）
    public void 清除高亮()
    {
        if (蒙版 == null) return;
        蒙版.DOKill();
        蒙版.gameObject.SetActive(false);
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

    // 阵亡退场动画时长（供 战斗面板 在演出队列里等它播完再移除；与 阵亡时长 字段默认一致）
    public const float 阵亡动画时长 = 0.4f;

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

    // ===== 演出方法 =====

    // 受击震颤 + 短暂变暗（伤害命中表现）。暴击/真实伤害 震颤更狠（传入严重=true）
    public void 受击(bool 严重)
    {
        死亡补间?.Kill();
        var 矩形 = (RectTransform)transform;
        矩形.DOKill();
        强调补间?.Kill();
        float 幅 = 严重 ? 受击震幅 * 1.6f : 受击震幅;
        震动补间 = 矩形.DOShakeAnchorPos(受击时长, new Vector2(幅, 幅 * 0.5f), 严重 ? 受击震次 + 2 : 受击震次, 70)
            .SetUpdate(true)
            .SetLink(gameObject);
        // 短暂变暗再恢复（用 canvas 增返太麻烦，直接对锚定位置 + 缩放模拟轻微顿挫即可；变暗用 Image 蒙版色控制会冲突，故跳过纯色变暗）
    }

    // 阵亡：向右倒下（旋转）+ 右移 + 缩小 + 淡出（代替瞬间消失），退场幕向右侧
    public void 阵亡()
    {
        if (死亡补间 != null && 死亡补间.IsActive()) return;
        震动补间?.Kill();
        var 矩形 = (RectTransform)transform;
        强调补间?.Kill();
        // 先停交互，避免死亡时被点到
        按钮 = GetComponent<Button>();
        if (按钮 != null) 按钮.interactable = false;
        // 用自身 CanvasGroup 控制整体透明；无则临时加一个
        var 组 = GetComponent<CanvasGroup>();
        if (组 == null) 组 = gameObject.AddComponent<CanvasGroup>();
        Vector2 原位 = 矩形.anchoredPosition;
        组.alpha = 1f;
        矩形.rotation = Quaternion.identity;
        死亡补间 = DOTween.Sequence()
            .Append(矩形.DORotate(new Vector3(0f, 0f, -阵亡倒角), 阵亡时长).SetEase(Ease.InCubic))
            .Join(矩形.DOAnchorPos(原位 + new Vector2(阵亡右移, -20f), 阵亡时长).SetEase(Ease.InCubic))
            .Join(矩形.DOScale(Vector3.one * 0.9f, 阵亡时长).SetEase(Ease.InCubic))
            .Join(组.DOFade(0f, 阵亡时长).SetEase(Ease.InCubic))
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    // 行动者强调：轻微冲击抬升 + 放大 + 一道淡金"轮到TA"高亮（Punch 自动回归原位，不污染布局）。
    public void 强调行动()
    {
        死亡补间?.Kill();
        var 矩形 = (RectTransform)transform;
        矩形.DOKill();
        强调补间 = DOTween.Sequence()
            .Append(矩形.DOPunchAnchorPos(new Vector2(0f, 强调抬升), 0.4f, 6, 0.5f).SetEase(Ease.OutQuad))
            .Join(矩形.DOPunchScale(Vector3.one * 0.05f, 0.4f, 4, 0.5f))
            .SetUpdate(true)
            .SetLink(gameObject);
        // 行动者高亮：蒙版浮现淡金光并淡出。淡出后不强制隐藏蒙版，避免打断目标选择；设可选/清除高亮 会接管。
        if (蒙版 != null)
        {
            蒙版.DOKill();
            蒙版.gameObject.SetActive(true);
            蒙版.color = 行动者高亮色;
            蒙版.DOFade(0f, 0.5f).SetUpdate(true).SetLink(gameObject);
        }
    }

    // 治疗脉冲：柔和膨胀回落
    public void 治疗脉冲()
    {
        死亡补间?.Kill();
        var 矩形 = (RectTransform)transform;
        矩形.DOKill();
        强调补间 = 矩形.DOPunchScale(Vector3.one * 治疗脉强, 0.35f, 4, 0.5f)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    // 重置演出：布阵/回合切换/选目标时清干净（Punch 系动画已自动回归，这里只需复位缩放与透明、恢复交互）
    public void 重置演出()
    {
        震动补间?.Kill(); 强调补间?.Kill();
        var 矩形 = (RectTransform)transform;
        var 组 = GetComponent<CanvasGroup>();
        if (组 != null) { 组.DOKill(); 组.alpha = 1f; }
        if (按钮 == null) 按钮 = GetComponent<Button>();
        if (按钮 != null) 按钮.interactable = true;
        矩形.localScale = Vector3.one;
    }
}
