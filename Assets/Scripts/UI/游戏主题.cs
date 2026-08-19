using UnityEngine;

// 游戏主题：统一配色（暖暗余烬风）。供代码生成的动态元素（日志/按钮/行）取色。
public static class 游戏主题
{
    // —— 基础色 ——
    public static readonly Color 背景 = new Color(0.043f, 0.043f, 0.051f);   // 炭黑
    public static readonly Color 面板 = new Color(0.082f, 0.082f, 0.098f);   // 面板底
    public static readonly Color 按钮底 = new Color(0.122f, 0.122f, 0.145f); // 按钮底
    public static readonly Color 文字 = new Color(0.85f, 0.83f, 0.78f);      // 羊皮纸白
    public static readonly Color 暗淡 = new Color(0.54f, 0.52f, 0.47f);      // 系统/辅助
    public static readonly Color 金色 = new Color(0.85f, 0.64f, 0.25f);      // 余烬金
    public static readonly Color 危险 = new Color(0.78f, 0.28f, 0.24f);      // 血烬红
    public static readonly Color 成功 = new Color(0.50f, 0.68f, 0.42f);      // 苔绿
    public static readonly Color 内心 = new Color(0.62f, 0.55f, 0.72f);      // 暮紫
    public static readonly Color 选中色 = new Color(0.42f, 0.66f, 0.82f);    // 钢蓝（地图节点选中高亮，区别于当前节点金色）

    // —— 富文本十六进制 ——
    public static readonly string 金色色值 = "#d9a441";
    public static readonly string 危险色值 = "#c7473d";
    public static readonly string 成功色值 = "#7fae6a";
    public static readonly string 暗淡色值 = "#73706a";
    public static readonly string 内心色值 = "#9e8fb8";
    public static readonly string 时间戳色值 = "#8a8f96";   // 提亮灰蓝（灰半透明底上可读，仍次级于正文）

    // 日志类型 → 富文本颜色（针对日志灰半透明底设计的提亮配色；不动共享主题色）
    public static string 日志色(日志类型 类型)
    {
        switch (类型)
        {
            case 日志类型.系统: return "#a8b0ba";    // 亮灰蓝（避开灰底，与之区分）
            case 日志类型.操作: return "#e8b84c";    // 提亮金
            case 日志类型.反馈: return "#8fcf78";    // 提亮苔绿
            case 日志类型.反馈坏: return "#e05a50";  // 提亮血烬红
            case 日志类型.内心: return "#b09fd0";    // 提亮暮紫
            case 日志类型.战斗: return "#e0826a";    // 提亮余烬橙红（战斗过程）
            case 日志类型.探索: return "#7fc0a8";    // 提亮青绿（探索事件）
            case 日志类型.任务: return "#d9b36a";    // 提亮土金（任务接取/完成）
            default: return "#f0ead9";               // 剧情 = 近白暖色（主叙事最亮）
        }
    }
}
