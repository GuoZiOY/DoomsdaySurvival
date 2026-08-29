using UnityEngine;

// 网格面板配色：网格/装备拖拽 视觉配色统一规范（代码常量，一处定义全局生效）。
// 网格面板（主背包/仓库/容器/穿戴容器）与 装备槽 拖拽高亮 全部从本类读取配色，
// 组件自身不再暴露配色参数（改风格只改这里，全场景同步；改后需 网格重建 或 重新运行 生效）。
public static class 网格面板配色
{
    // 十六进制 RGB + 透明度 0~255
    public static readonly Color 底座色 = new Color(1f, 1f, 1f, 0.95f);                         // 空格底图：纯白 A255（网格底层精灵 原样着色）
    // 三色线条：纯白，仅透明度不同——物品边界 150 / 形状边界 70 / 线条 30（0~255）
    public static readonly Color 物品边界色 = new Color(1f, 1f, 1f, 120f / 255f);   // 物品边界线（最亮 A150）
    public static readonly Color 形状边界色 = new Color(1f, 1f, 1f, 70f / 255f);    // 容器形状边界线（口袋轮廓框，A70）
    public static readonly Color 线条色 = new Color(1f, 1f, 1f, 30f / 255f);        // 物品内部/空格 线（最淡 A30）
    public static readonly float 线宽 = 1f;                                                  // 分隔线宽（px：内部/空格/形状框）
    public static readonly float 物品边界线宽 = 2f;                                           // 物品边界线宽（px：有物品的 网格边界/口袋 边界——比 普通 线 粗）
    public static readonly float 物品边距 = 4f;                                              // 物品块四周内缩（不压网格线/不重叠）
    public static readonly Color 物品底色 = new Color(0.2784f, 0.298f, 0.3608f, 0.7843f);   // 物品内容层底色（#474C5C A200）
    public static readonly Color 高光色 = new Color(1f, 1f, 1f, 0.0392f);                    // 悬停高光层（#FFFFFF A10）
    public static readonly float 品质底色透明 = 0.3f;                                        // 品质底色（物品框层）半透明程度
    public static readonly Color 放置可色 = new Color(0.4941f, 0.7725f, 0.5294f, 0.3922f);  // 拖拽投影：可放（#7EC587 A100；装备槽高亮同款）
    public static readonly Color 放置禁色 = new Color(0.7529f, 0.4078f, 0.4078f, 0.3922f);  // 拖拽投影：不可放（#C06868 A100；装备槽高亮同款）
    public static readonly Color 合并色 = new Color(0.451f, 0.749f, 1f, 0.3922f);            // 拖拽投影：可合并（#73BFFF A100）
    // 容器内部形状（塔科夫式独立口袋）：块间 空隙 = 块偏移 自然 露出（无线/无底图/不填色），
    // 每块 四边 亮轮廓 框（独立 闭合）——本类 无 缝隙 配色
}
