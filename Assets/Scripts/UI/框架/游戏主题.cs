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

    // —— 战斗演出 ——
    public static readonly Color 远程 = new Color(0.45f, 0.62f, 1f);     // 远程伤害飘字（冰川蓝）
    public static readonly Color 治疗 = new Color(0.35f, 0.85f, 0.45f);   // 治疗飘字苔绿（提亮）

    // —— 富文本十六进制 ——
    public static readonly string 金色色值 = "#d9a441";
    public static readonly string 危险色值 = "#c7473d";
    public static readonly string 成功色值 = "#7fae6a";
    public static readonly string 暗淡色值 = "#73706a";
    public static readonly string 内心色值 = "#9e8fb8";
    public static readonly string 时间戳色值 = "#8a8f96";   // 提亮灰蓝（灰半透明底上可读，仍次级于正文）
    public static readonly string 高亮色值 = "#f5d88a";     // 数值/变量强调（亮米金，正文内始终可见）
    public static readonly string 物攻色值 = "#e0826a";     // 近战伤害（战斗面板技能数值）
    public static readonly string 远程色值 = "#7fb2d9";     // 远程伤害（战斗面板技能数值）
    public static readonly string 出手青值 = "#6be0a0";     // 信息条：下一个出手的我方
    public static readonly string 排队灰值 = "#9aa0a6";     // 信息条：排队中的敌方/其余
    public static readonly string 已行动灰值 = "#46433f";   // 信息条：已行动（淡删除线）

    // 日志类型 → 富文本颜色（针对日志灰半透明底设计的提亮配色；不动共享主题色；v39 收敛 + v40 补 角色）
    public static string 日志色(日志类型 类型)
    {
        switch (类型)
        {
            case 日志类型.探索: return "#7fc0a8";    // 青绿（探索事件）
            case 日志类型.战斗: return "#e0826a";    // 橙红（战斗/结果）
            case 日志类型.生存: return "#6fb8d9";    // 提亮水蓝（睡觉/伤病/时间生存）
            case 日志类型.获得: return "#8fcf78";    // 提亮苔绿（获得/成功）
            case 日志类型.警告: return "#e05a50";    // 提亮血烬红（失败/负面）
            case 日志类型.内心: return "#b09fd0";    // 提亮暮紫（心声）
            case 日志类型.角色: return "#e8b84c";    // 提亮金（角色操作：装备/卸下/使用/学技能/训练）
            default: return "#a8b0ba";               // 系统 = 亮灰蓝（中性）
        }
    }

    // 日志类型 → 简短标签（顶在正文前，形成可扫读的彩色徽章）
    public static string 日志标签(日志类型 类型)
    {
        switch (类型)
        {
            case 日志类型.探索: return "探索";
            case 日志类型.战斗: return "战斗";
            case 日志类型.生存: return "生存";
            case 日志类型.获得: return "获得";
            case 日志类型.警告: return "警告";
            case 日志类型.内心: return "心声";
            case 日志类型.角色: return "角色";
            default: return "系统";
        }
    }
}