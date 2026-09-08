using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 战斗单位视图：战斗单位（身体圆 + 信息卡 合一）预制体的 手动组件（场景/预制体 搭建，Inspector 接线）。
// 照 技能槽 手动范式：引用位 全部 [SerializeField]；代码只负责 数值/外观/点击转发，尺寸字体素材布局 全在预制体。
//
// 【预制体结构约定】Resources/Prefab/战斗单位.prefab（根 挂 本组件）
//   根（RectTransform：代码 强制 锚(0,0.5) pivot(中) 尺寸 0——移动只改 x，勿依赖根的锚定）
//   ├─ 身体圆  Image（纯 视觉，无需 Button/无需接线；选中 统一 走 信息卡）
//   └─ 信息卡  RectTransform(含 Image 底 + Button) ← 信息卡 引用位；Button.onClick → 点卡()（选中）
//       ├─ 名字     TMP_Text（中文用 Silver 等 TMP 字体；字号 16~18）
//       ├─ 血条     Slider（0~1 只读显示：背景=暗轨，Fill Area 前景=亮红）← 血条 引用位
//       └─ 行动条   Slider（0~1 只读显示：Fill 前景=橙黄）              ← 行动条 引用位
//     可选：状态文本 TMP_Text（卡 顶 上方 显示 眩晕/流血…，自 搭 后 拖 引用位；不 拖 = 不 显示）
//
// 【Slider 只读条 搭建要点】（血条/行动条 不用 Scrollbar——Scrollbar 语义是滚动内容；
//   用 Slider：填充区靠锚点拉伸，无 sprite 依赖，不会再出现 裸 Image.Filled 不成条 的坑）
//   ① Slider 保留 Background + Fill Area（Fill 前景上色）；删除或留空 Handle Area；
//   ② Slider 组件：Min=0 Max=1 Whole Numbers 关；勾 取消 interactable（或 组件 上 无 Button）；
//   ③ Background/Fill 的 Image 全部取消 Raycast Target——否则会挡 信息卡 的点击选中。
//
// 观感（字号/卡宽高/配色/素材）以预制体为准；本组件只按 单位 数据覆盖：名字、圆色(阵营/可选)、条 value/颜色。
public sealed class 战斗单位视图 : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private RectTransform 信息卡;    // 信息卡 根（代码 摆 上下槽位 y；含 Image 卡底——点击 由 本组件 处理，无需 Button）
    [SerializeField] private Image 身体圆;            // 身体圆（纯 视觉：raycast 关；需 sprite 才可见）
    [SerializeField] private TMP_Text 名字;           // 名字（绑定 单位.名称）
    [SerializeField] private Slider 血条;             // 血条（Slider 0~1 只读：背景 暗轨 + Fill Area 前景）
    [SerializeField] private Slider 行动条;           // 行动条（Slider 0~1 只读：Fill 前景）
    [SerializeField] private TMP_Text 状态文本;       // 状态 文本（眩晕/流血… 显示在 卡 顶 上方，场景 自 搭 后 接线；可空 = 不显示）

    private static readonly Color 我方色 = new Color(0.30f, 0.55f, 0.36f, 1f);
    private static readonly Color 敌色 = new Color(0.62f, 0.30f, 0.28f, 1f);
    private static readonly Color 可选色 = new Color(0.95f, 0.85f, 0.3f, 1f);
    private static readonly Color 名字色 = Color.white;
    private static readonly Color 名字可选色 = new Color(0.95f, 0.85f, 0.3f, 1f);

    public 战斗单位 单位 { get; private set; }
    public RectTransform 卡变换 => 信息卡;             // 供 轨道 摆 上下槽位 y
    private 战斗轨道 轨道 => GetComponentInParent<战斗轨道>();

    // 绑定 单位：写 名字、圆 基础 尺寸（直径 × 身形 加宽）与 阵营色；随后 由 轨道 每帧 刷新外观
    public void 绑定(战斗单位 单位, float 棋子直径)
    {
        this.单位 = 单位;
        if (单位 == null) return;
        整理只读条(血条); 整理只读条(行动条);   // 条 只读 + 不 挡 卡 的 点击（选中 目标 走 信息卡）
        if (名字 != null) 名字.text = 单位.名称;
        if (身体圆 != null)
        {
            float 宽 = 棋子直径 * Mathf.Max(1, 单位.身形);
            身体圆.rectTransform.sizeDelta = new Vector2(宽, 棋子直径);
            身体圆.color = 单位.是否我方 ? 我方色 : 敌色;
            身体圆.raycastTarget = false;   // 身体圆 纯 视觉：目标 选中 统一 走 信息卡
        }
        刷新外观(false);
    }

    // 血条/行动条 = 只读 显示条：禁 交互 + 子 Image 关 Raycast（否则 会 挡 信息卡 的 点击 选中）
    private static void 整理只读条(Slider 条)
    {
        if (条 == null) return;
        条.interactable = false;
        foreach (var 图 in 条.GetComponentsInChildren<Image>(true)) 图.raycastTarget = false;
    }

    // 每帧 刷新：可选 高亮（圆/名字 变金）+ 血条/行动条 fill
    public void 刷新外观(bool 可选)
    {
        if (单位 == null) return;
        bool 我方 = 单位.是否我方;
        if (身体圆 != null) 身体圆.color = 可选 ? 可选色 : (我方 ? 我方色 : 敌色);
        if (名字 != null) 名字.color = 可选 ? 名字可选色 : 名字色;
        float 血比 = 单位.最大生命 > 0 ? (float)单位.生命 / 单位.最大生命 : 0f;
        if (血条 != null) 血条.value = Mathf.Clamp01(血比);
        if (行动条 != null) 行动条.value = Mathf.Clamp01(单位.行动条 / 100f);
        行动条意图色(单位.当前意图);
        刷新状态文本();
    }

    // ===== 信息卡 状态 文本（眩晕/流血 等 Buff 名 拼接；状态文本 = 场景 自 搭 的 TMP，接线；改变 才 重设） =====
    private string 上次状态文本 = "";

    private void 刷新状态文本()
    {
        if (状态文本 == null) return;   // 未 接线（场景 自 搭）→ 跳过
        string 内容 = 状态内容();
        if (内容 == 上次状态文本) return;
        上次状态文本 = 内容;
        bool 有 = !string.IsNullOrEmpty(内容);
        if (状态文本.gameObject.activeSelf != 有) 状态文本.gameObject.SetActive(有);
        if (有) 状态文本.text = 内容;
    }

    // 状态 摘要：眩晕 + 各 Buff（层数 >1 带 ×n）
    private string 状态内容()
    {
        if (单位 == null) return "";
        var 片段 = new List<string>();
        if (单位.眩晕剩余秒 > 0f) 片段.Add("眩晕");
        foreach (var b in 单位.Buffs)
            if (b != null && b.定义 != null)
                片段.Add(b.层数 > 1 ? $"{b.定义.名称}×{b.层数}" : b.定义.名称);
        return string.Join("·", 片段);
    }

    // 行动条 前景 颜色：按 读条 意图（移动 金 / 攻击 血红 / 技能 紫 / 戒备 灰蓝）；前景 自动 找（Slider fillRect 的 Image）
    private Image 行动条前景缓存;
    private static readonly Color 意图移动色 = new Color(1f, 0.84f, 0.30f, 1f);    // 金黄
    private static readonly Color 意图攻击色 = new Color(0.88f, 0.16f, 0.12f, 1f);   // 血红
    private static readonly Color 意图技能色 = new Color(0.55f, 0.42f, 0.9f, 1f);    // 紫
    private static readonly Color 意图戒备色 = new Color(0.55f, 0.6f, 0.65f, 1f);    // 灰蓝

    private void 行动条意图色(战斗单位.意图类型 意图)
    {
        if (行动条 == null || 行动条.fillRect == null) return;
        if (行动条前景缓存 == null)
            行动条前景缓存 = 行动条.fillRect.GetComponent<Image>() ?? 行动条.fillRect.GetComponentInChildren<Image>();
        if (行动条前景缓存 != null) 行动条前景缓存.color = 意图 switch
        {
            战斗单位.意图类型.攻击 => 意图攻击色,
            战斗单位.意图类型.技能 => 意图技能色,
            战斗单位.意图类型.戒备 => 意图戒备色,
            _ => 意图移动色,
        };
    }

    // —— 点击（无需 预制体 Button）：信息卡 卡底 Image 开 Raycast → 点击 事件 冒泡 到 根 → 本组件 处理 ——
    public void OnPointerClick(PointerEventData 事件) => 点卡();

    // 身体圆 纯 视觉（点 无 交互）；点圆 回调 保留 但 空（旧 预制体 若 接线 不 报错）
    public void 点圆()
    {
    }

    private float 上次点卡时间;   // 防 双 触发（本组件 处理 + 旧 Button 并存 时）
    private Coroutine 弹性句柄;

    public void 点卡()
    {
        // 无 目标 选择 时：点 卡 不 给 任何 反馈（不 响、不 弹）
        if (轨道 == null || !轨道.正在选目标) return;
        if (Time.unscaledTime - 上次点卡时间 < 0.05f) return;   // 同一 次 点击 只 响应 一次
        上次点卡时间 = Time.unscaledTime;
        音效管理器.实例?.播放成功();   // 点击 反馈：按钮 音效
        弹性反馈();
        if (单位 != null) 轨道?.处理卡点击(单位);
    }

    // 弹性 反馈：信息卡 先 缩 后 回弹 复位（0.88 → 1.08 → 1）
    private void 弹性反馈()
    {
        if (信息卡 == null) return;
        if (弹性句柄 != null) StopCoroutine(弹性句柄);
        弹性句柄 = StartCoroutine(弹性动画());
    }

    private IEnumerator 弹性动画()
    {
        var 卡 = 信息卡;
        const float 总时 = 0.22f;
        float t = 0f;
        while (t < 总时)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 总时);
            float s = k < 0.35f ? Mathf.Lerp(1f, 0.88f, k / 0.35f)              // 先 缩
                : k < 0.85f ? Mathf.Lerp(0.88f, 1.08f, (k - 0.35f) / 0.5f)      // 回弹 过冲
                : Mathf.Lerp(1.08f, 1f, (k - 0.85f) / 0.15f);                   // 复位
            卡.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        卡.localScale = Vector3.one;
        弹性句柄 = null;
    }
}
