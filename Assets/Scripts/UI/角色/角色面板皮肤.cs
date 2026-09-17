using UnityEngine;

// 角色面板皮肤：角色面板**整块**（板底 + 遮罩 + 顶部身份条 + 四个栏页）共用的色板与尺寸常量。
// 为什么要单独一个类：四栏的内容要分几批做，但"底色/字号/间距"必须**一处定义**——
//   散在四五个文件里各写各的十六进制，改一次风格就得全仓库搜 `new Color(`，改漏一处就是一块对不上色的面板。
//
// ★ 本版口径（用户 2026-09 拍板）：**"幸存者档案板"** —— 一块浮在安全屋画面上的档案板，
//   不是网页，也不是仪表盘。三条硬口径写在这里，后面几批填内容时照它做：
//     ① **不铺满屏**：板子按屏幕比例居中、有上限（见 板宽上限/板高上限），底下压一层 65% 黑遮罩；
//     ② **大一号**：正文 19 / 小字 16，行高 48（上一版 12~15 号 + 26 行高是"后台仪表盘"的密度）；
//     ③ **暖调近黑 + 唯一强调色暗锈红**：**没有琥珀**（炭黑配琥珀是 AI 深色仪表盘的标准配色，明令去掉）。
//   所以这里只有实色方块与文字：**没有**圆角、阴影、发光、渐变、图标、动画。
// 与既有主题的关系：本类只服务角色面板这一块（`游戏主题` 是全局暖暗余烬风、`网格面板配色` 是网格/地图层），
//   三者互不覆盖；品质色**不在这里**——按项目既有口径读 `品质工具.颜色()`，不要另发明一套品质配色。
public static class 角色面板皮肤
{
    // ===== 底色（暖调近黑：R > G > B，不是蓝灰炭黑） =====
    public static readonly Color 面板底 = new Color(0.0706f, 0.0627f, 0.0549f, 1f);   // #12100E 板底（板子最外层，露出来的部分就是 2px 边框）
    public static readonly Color 内容底 = new Color(0.1020f, 0.0941f, 0.0824f, 1f);   // #1A1815 身份条 / 内容区底
    public static readonly Color 边框 = new Color(0.2275f, 0.2118f, 0.1882f, 1f);     // #3A3630 板子边框与分隔线（**按 2px 画**，不是 1px）
    public static readonly Color 行悬停或选中底 = new Color(0.1490f, 0.1333f, 0.1255f, 1f);   // #262220 悬停 / 选中底

    // 板底下的全屏遮罩色：65% 黑。它**不参与点击**（板子外面看得见安全屋，但不让点到背后的界面）。
    public static readonly Color 板底遮罩 = new Color(0f, 0f, 0f, 0.65f);

    // ===== 文字 =====
    public static readonly Color 正文 = new Color(0.8941f, 0.8745f, 0.8392f, 1f);     // #E4DFD6 骨白
    public static readonly Color 次要 = new Color(0.5412f, 0.5137f, 0.4706f, 1f);     // #8A8378
    // 全屏唯一强调色（暗锈红）：**一块面板里只有它一个高饱和色**——Tab 选中下划线、进度格、危险语义都用它，
    // 用多了就不叫强调了，所以除它之外本面板不允许再引入高饱和色（上一版的琥珀就是被这条否掉的）。
    public static readonly Color 强调 = new Color(0.7059f, 0.3333f, 0.2353f, 1f);     // #B4553C

    // ===== 语义色（都低饱和，只用于"掉血/减益"与"治疗/增益"这类状态） =====
    public static readonly Color 危险 = new Color(0.7059f, 0.3333f, 0.2353f, 1f);     // #B4553C（与 强调 同值：危险就是本板的强调色）
    public static readonly Color 恢复 = new Color(0.4314f, 0.5608f, 0.3843f, 1f);     // #6E8F62

    // ===== 字号（比上一版整体大一档半） =====
    public const float 字号_标题 = 30f;      // 板顶"幸存者 · 职业"、"幸存者档案"这类大标题
    public const float 字号_栏目标题 = 24f;  // 栏页里的分区标题
    public const float 字号_Tab = 24f;       // 顶部横排四个大字 Tab
    public const float 字号_等级 = 20f;      // 身份条右侧等级（比标题小一档，不与标题抢视线）
    public const float 字号_正文 = 19f;      // 列表行名称、正文
    public const float 字号_小字 = 16f;      // 行附属信息、次要说明

    // ===== 间距 =====
    public const float 页边距 = 40f;   // 板内四边留白（身份条 / Tab 行 / 内容区 共用同一个数）
    public const float 段距 = 20f;     // 段与段之间
    public const float 行高 = 48f;     // 列表行高：**行与行之间不画线**，靠这个留白分层
    public const float 身份条高 = 88f; // 顶部身份条高（标题 30 + 段距 + 进度格 10 + 上下留白）

    // ===== 板子尺寸（"覆盖大半即可"，不铺满屏） =====
    // 算法：宽 = min(屏宽 × 板宽比例, 板宽上限)，高 = min(屏高 × 板高比例, 板高上限)，屏幕居中。
    // 两个上限的作用：大屏上不让板子跟着一起变得又宽又高（一行字横跨 1900px 就不是档案板了）。
    public const float 板宽比例 = 0.84f;
    public const float 板高比例 = 0.88f;
    public const float 板宽上限 = 1180f;
    public const float 板高上限 = 760f;
    public const float 板边框 = 2f;    // 板子四边边框**线宽**（用四条实心矩形画，见 角色面板.建节点树）

    // ===== 进度条（经验/生命等）：10px 高的**分段格子**，不是连续细条 =====
    public const float 进度格高 = 10f;
    public const float 进度格宽 = 10f;
    public const float 进度格间隔 = 2f;

    // ===== 技能槽（本批只落尺寸，内容下批填） =====
    public const float 技能槽高 = 96f;
    public const float 技能槽宽 = 150f;
}
