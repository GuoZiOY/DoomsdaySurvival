using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 任务行：任务面板（日常委托栏 / 系统任务面板）的列表专用行（名称 + 状态背景）。
// 任务三状态三色：进行中=琥珀金高亮；可接取=中性暗蓝；已完成=暗灰+灰字。选中态用缩放反馈，不换背景色。
public sealed class 任务行 : MonoBehaviour
{
    [SerializeField] private TMP_Text 名称;   // 任务名/目标名
    [SerializeField] private Image 背景;      // 行背景（按三态着色）

    // 任务状态（供面板传入）
    public const int 进行中 = 0;   // 任务进行中：琥珀金高亮（醒目）
    public const int 可接取 = 1;   // 待玩家行动（未接取/待提交/主线当前）：中性暗蓝
    public const int 已完成 = 2;   // 任务已完成/已结算：暗灰 + 灰字

    private Color 默认名色;   // 模板原始文字色（首次绑定时缓存）
    private bool 已存默认色;

    private static readonly Color 可接取色 = new Color(0.25f, 0.27f, 0.30f, 0.9f);      // 可接取：中性暗蓝
    private static readonly Color 进行中色 = new Color(0.55f, 0.42f, 0.22f, 0.9f);     // 进行中：琥珀金高亮
    private static readonly Color 已完成色 = new Color(0.32f, 0.32f, 0.32f, 0.85f);    // 已完成：暗灰
    private static readonly Color 已完成字色 = new Color(0.62f, 0.62f, 0.62f, 1f);     // 已完成：灰字

    public void 绑定(string 名称文本, int 状态, bool 选中, System.Action 点击)
    {
        if (名称 != null)
        {
            if (!已存默认色) { 默认名色 = 名称.color; 已存默认色 = true; }
            名称.text = 名称文本;
            名称.color = 状态 == 已完成 ? 已完成字色 : 默认名色;   // 已完成灰字，其余模板色
        }
        if (背景 != null)
            背景.color = 状态 == 可接取 ? 可接取色 : (状态 == 已完成 ? 已完成色 : 进行中色);
        var 按钮 = GetComponent<Button>();
        if (按钮 != null)
        {
            面板基类.设选中缩放(按钮, 选中);   // 选中态 = 缩放放大（交互反馈，不换背景色）
            按钮.onClick.AddListener(() => 点击());
            音效管理器.实例?.注册按钮(按钮);
        }
    }
}