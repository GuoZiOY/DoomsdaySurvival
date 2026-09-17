using UnityEngine;

// 角色面板皮肤：角色面板**整块**（面板底 + 四个栏页）共用的色板与尺寸常量。
// 为什么要单独一个类：四栏的内容要分几批做，但"底色/字号/间距"必须**一处定义**——
//   散在四五个文件里各写各的十六进制，改一次风格就得全仓库搜 `new Color(`，改漏一处就是一块对不上色的面板。
// 风格口径（用户定的）：像幸存者的档案板/工具面板——平、密、克制，靠**留白与对齐**分层，不靠装饰。
//   所以这里只有实色的方块与文字：**没有**圆角、阴影、发光、渐变、图标、动画。
// 与既有主题的关系：本类只服务角色面板这一块（`游戏主题` 是全局暖暗余烬风、`网格面板配色` 是网格/地图层），
//   三者互不覆盖；品质色**不在这里**——按项目既有口径读 `品质工具.颜色()`，不要另发明一套品质配色。
public static class 角色面板皮肤
{
    // ===== 底色 =====
    public static readonly Color 面板底 = new Color(0.0784f, 0.0863f, 0.1020f, 1f);   // #14161A 整块底
    public static readonly Color 内容底 = new Color(0.1059f, 0.1176f, 0.1373f, 1f);   // #1B1E23 内容区底
    public static readonly Color 分隔线 = new Color(0.1725f, 0.1922f, 0.2196f, 1f);   // #2C3138 1px 线（分隔线 / 进度槽底）
    public static readonly Color 行悬停或选中底 = new Color(0.1373f, 0.1569f, 0.1882f, 1f);   // #232830 悬停 / 选中

    // ===== 文字 =====
    public static readonly Color 正文 = new Color(0.7882f, 0.8039f, 0.8314f, 1f);     // #C9CDD4
    public static readonly Color 次要 = new Color(0.4863f, 0.5137f, 0.5529f, 1f);     // #7C838D
    // 全屏唯一强调色（琥珀）：**一块面板里只有它一个暖色**——选中项、进度条填充、关键数值都用它，
    // 用多了就不叫强调了，所以除它之外本面板不允许再引入高饱和色。
    public static readonly Color 强调 = new Color(0.8471f, 0.6510f, 0.3412f, 1f);     // #D8A657

    // ===== 语义色（都低饱和，只用于"掉血/减益"与"治疗/增益"这类状态） =====
    public static readonly Color 危险 = new Color(0.7686f, 0.3333f, 0.2314f, 1f);     // #C4553B
    public static readonly Color 恢复 = new Color(0.4314f, 0.6196f, 0.4157f, 1f);     // #6E9E6A

    // ===== 字号 =====
    public const float 字号_顶部标题 = 20f;
    public const float 字号_栏目标题 = 15f;
    public const float 字号_正文 = 14f;
    public const float 字号_小字 = 12f;

    // ===== 间距 =====
    public const float 页边距 = 24f;      // 面板四边的留白（顶部身份条 / 左栏 / 内容区 共用同一个数）
    public const float 行高 = 26f;        // 行间距：行悬停底 / 列表行的高
    public const float 段距 = 10f;        // 段与段之间
    public const float 左栏宽 = 132f;     // 左侧竖排导航宽
    public const float 内容区最大宽 = 880f;   // 内容区宽度上限；比这更宽就居中，不让文字被拉成一整行

    // ===== 进度条（经验/生命等）=====
    public const float 进度条高 = 4f;     // 只有 4px：实心矩形，**不做圆角/渐变**，这是本面板唯一允许的"图形"
    public const float 身份条高 = 72f;    // 顶部身份条高（标题 20 + 段距 + 进度条 4 + 上下留白）
}
