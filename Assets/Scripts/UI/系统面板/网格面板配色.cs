using UnityEngine;

// 网格面板配色：网格/装备拖拽 视觉配色统一规范（代码常量，一处定义全局生效）。
// 网格面板（主背包/仓库/容器/穿戴容器）与 装备槽 拖拽高亮 全部从本类读取配色，
// 组件自身不再暴露配色参数（改风格只改这里，全场景同步；改后需 网格重建 或 重新运行 生效）。
public static class 网格面板配色
{
    // 十六进制 RGB + 透明度 0~255
    public static readonly Color 底座色 = new Color(0f, 0f, 0.0784f, 0.3922f);              // 空格底图（#000014 A100）
    public static readonly Color 物品边界色 = new Color(1f, 1f, 1f, 0.2745f);                     // 物品边界线（较亮，#FFFFFF）
    public static readonly Color 线条色 = new Color(1f, 1f, 1f, 0.1176f);                   // 物品内部/空格 线（较淡，#FFFFFF A30）
    public static readonly float 线宽 = 2f;                                                  // 分隔线宽（px）
    public static readonly float 物品边距 = 4f;                                              // 物品块四周内缩（不压网格线/不重叠）
    public static readonly Color 物品底色 = new Color(0.2784f, 0.298f, 0.3608f, 0.7843f);   // 物品内容层底色（#474C5C A200）
    public static readonly Color 高光色 = new Color(1f, 1f, 1f, 0.0392f);                    // 悬停高光层（#FFFFFF A10）
    public static readonly float 品质底色透明 = 0.3f;                                        // 品质底色（物品框层）半透明程度
    public static readonly Color 放置可色 = new Color(0.4941f, 0.7725f, 0.5294f, 0.3922f);  // 拖拽投影：可放（#7EC587 A100；装备槽高亮同款）
    public static readonly Color 放置禁色 = new Color(0.7529f, 0.4078f, 0.4078f, 0.3922f);  // 拖拽投影：不可放（#C06868 A100；装备槽高亮同款）
    public static readonly Color 合并色 = new Color(0.451f, 0.749f, 1f, 0.3922f);            // 拖拽投影：可合并（#73BFFF A100）
}
