using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 战斗单位视图：战斗单位（身体圆 + 信息卡 合一）预制体的 手动组件（场景/预制体 搭建，Inspector 接线）。
// 照 技能槽 手动范式：引用位 全部 [SerializeField]；代码只负责 数值/外观/点击转发，尺寸字体素材布局 全在预制体。
//
// 【预制体结构约定】Resources/Prefab/战斗单位.prefab（根 挂 本组件）
//   根（RectTransform：代码 强制 锚(0,0.5) pivot(中) 尺寸 0——移动只改 x，勿依赖根的锚定）
//   ├─ 身体圆  Image + Button   ← 身体圆 引用位；Button.onClick → 点圆()（同节点重叠循环选中）
//   └─ 信息卡  RectTransform(含 Image 底 + Button) ← 信息卡 引用位；Button.onClick → 点卡()（精确选中）
//       ├─ 名字     TMP_Text（中文用 Silver 等 TMP 字体；字号 16~18）
//       ├─ 可选框   GameObject（金色描边/边框，可选 或 留空；代码 SetActive(可选)）
//       ├─ 血条     Slider（0~1 只读显示：背景=暗轨，Fill Area 前景=亮红）← 血条 引用位
//       └─ 行动条   Slider（0~1 只读显示：Fill 前景=橙黄）              ← 行动条 引用位
//
// 【Slider 只读条 搭建要点】（血条/行动条 不用 Scrollbar——Scrollbar 语义是滚动内容；
//   用 Slider：填充区靠锚点拉伸，无 sprite 依赖，不会再出现 裸 Image.Filled 不成条 的坑）
//   ① Slider 保留 Background + Fill Area（Fill 前景上色）；删除或留空 Handle Area；
//   ② Slider 组件：Min=0 Max=1 Whole Numbers 关；勾 取消 interactable（或 组件 上 无 Button）；
//   ③ Background/Fill 的 Image 全部取消 Raycast Target——否则会挡 信息卡 的点击选中。
//
// 观感（字号/卡宽高/配色/素材）以预制体为准；本组件只按 单位 数据覆盖：名字、圆色(阵营/可选)、value、可选框。
public sealed class 战斗单位视图 : MonoBehaviour
{
    [SerializeField] private RectTransform 信息卡;    // 信息卡 根（代码 摆 上下槽位 y；含 Button 与卡底）
    [SerializeField] private Image 身体圆;            // 身体圆（可点：重叠循环；需 sprite 才可见）
    [SerializeField] private TMP_Text 名字;           // 名字（绑定 单位.名称）
    [SerializeField] private Slider 血条;             // 血条（Slider 0~1 只读：背景 暗轨 + Fill Area 前景）
    [SerializeField] private Slider 行动条;           // 行动条（Slider 0~1 只读：Fill 前景）
    [SerializeField] private GameObject 可选框;       // 可选/选中 高亮（金色描边等；可空）

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
        if (名字 != null) 名字.text = 单位.名称;
        if (身体圆 != null)
        {
            float 宽 = 棋子直径 * Mathf.Max(1, 单位.身形);
            身体圆.rectTransform.sizeDelta = new Vector2(宽, 棋子直径);
            身体圆.color = 单位.是否我方 ? 我方色 : 敌色;
        }
        刷新外观(false);
    }

    // 每帧 刷新：可选 高亮（圆/名字 变金 + 可选框）+ 血条/行动条 fill
    public void 刷新外观(bool 可选)
    {
        if (单位 == null) return;
        bool 我方 = 单位.是否我方;
        if (身体圆 != null) 身体圆.color = 可选 ? 可选色 : (我方 ? 我方色 : 敌色);
        if (名字 != null) 名字.color = 可选 ? 名字可选色 : 名字色;
        if (可选框 != null && 可选框.activeSelf != 可选) 可选框.SetActive(可选);
        float 血比 = 单位.最大生命 > 0 ? (float)单位.生命 / 单位.最大生命 : 0f;
        if (血条 != null) 血条.value = Mathf.Clamp01(血比);
        if (行动条 != null) 行动条.value = Mathf.Clamp01(单位.行动条 / 100f);
    }

    // —— Button 回调（预制体里：身体圆.Button → 点圆；信息卡.Button → 点卡）——
    public void 点圆()
    {
        if (单位 != null) 轨道?.处理圆点击(单位);
    }

    public void 点卡()
    {
        if (单位 != null) 轨道?.处理卡点击(单位);
    }
}
