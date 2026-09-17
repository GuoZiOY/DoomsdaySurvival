using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 属性子面板：角色面板 [属性] 栏页的内容（由 `角色面板` 注入 `内容区`；本组件**不被壳读任何字段**）。
//
// 本页四个小段（自上而下；小标题 + 行内文本 + 留白分层，**行间不画线**）：
//   ① 五维      力量 / 体质 / 敏捷 / 智慧 / 意志（显示名 = `玩家档案` 的字段名，逐条点明是哪个字段，见 建五维段）
//   ② 派生      近战伤害 / 枪械伤害 / 总防御 / 暴击概率 / 命中率 / 闪避概率 / 最大生命 / 最大行动点 / 负重上限
//   ③ 生存状态  生命 / 行动点 / 饱食度 / 水分度 / 疲劳 + 伤病（仅 >0 的占行，全无则"伤病 无"）
//   ④ 天赋      职业天赋大卡（`职业.json` 的 天赋 → `天赋.json` 里那条）+ 通用天赋列表（每条只显示机制一行）
//
// ★ 为什么天赋在这一页而不是自己一个栏：用户 2026-09-17 当场拍板 —— "这部分内容很少，不需要单独写一个子面板"。
//   于是 Tab 从四个压成三个（属性 / 技能 / 知识），天赋降级成本页的第 4 个小段。
//
// 布局：上面三列（五维 | 派生 | 生存状态）共用一条像素高度带，第 4 段（天赋）横跨整行落在它下面。
//   为什么天赋不挤进第 4 列：通用天赋有 17 条，一列 5+ 行塞不下（塞得下也会把 5 个汉字的名挤到折行）。
//
// 配色/字号/间距**全部内联在本文件自己的 [SerializeField] 字段里**（没有"皮肤"这个中间层 —— 用户明确不要）。
public sealed class 属性子面板 : MonoBehaviour
{
    // ================= 注入：`内容区` 由 角色面板 给（本组件不找父级、不猜节点名） =================
    [SerializeField] private RectTransform 内容区;

    // ================= 颜色（与壳同一套十六进制值；同一个颜色在这里再写一遍是有意的取舍：没有共用参数类） =================
    [SerializeField] private Color 正文色 = new Color(0.8941f, 0.8745f, 0.8392f, 1f);   // #E4DFD6
    [SerializeField] private Color 次要色 = new Color(0.5412f, 0.5137f, 0.4706f, 1f);   // #8A8378
    [SerializeField] private Color 强调色 = new Color(0.7059f, 0.3333f, 0.2353f, 1f);   // #B4553C 唯一强调色
    [SerializeField] private Color 内容底色 = new Color(0.1020f, 0.0941f, 0.0824f, 1f); // #1A1815 职业大卡的底
    [SerializeField] private Color 边框色 = new Color(0.2275f, 0.2118f, 0.1882f, 1f);   // #3A3630 大卡的 2px 描边

    // ================= 字号 / 间距 =================
    [SerializeField] private int 字号_正文 = 19;
    [SerializeField] private int 字号_小字 = 16;
    [SerializeField] private int 字号_大卡标题 = 22;
    [SerializeField] private float 段距 = 20f;
    [SerializeField] private float 行高 = 48f;          // 上三列的行高
    [SerializeField] private float 段间隔 = 12f;         // 小标题 → 第一行
    [SerializeField] private float 段间空 = 20f;         // 上三列底 → 天赋段标题
    [SerializeField] private float 职业卡高 = 84f;
    // 天赋段的行高比 行高 密一档：那里每条只有一行短机制（"力量 +1" / "经验 +10%"），
    //   用 48 会让 17 条铺出半屏；用户要的是"内容尽量都在板上"。其余各段照旧 48，留白分层仍然成立。
    [SerializeField] private float 段行高_天赋 = 40f;
    [SerializeField] private float 天赋名宽 = 104f;     // 天赋行里"名称"那一列的固定宽（值从同一条竖线起）

    // ================= 本组件自建的节点账本（幂等重建用；理由与壳里那份同源） =================
    [SerializeField] private List<GameObject> 自建节点 = new List<GameObject>();

    private RectTransform 画布;                       // 本页真正的根（贴 `内容区` 上沿，让开栏目标题那一带）
    private TMP_Text 天赋卡标题, 天赋卡机制;
    private float 天赋列表顶;                         // 天赋列表两槽的起点（从段顶往下量）
    private readonly List<行件> 全部行 = new List<行件>();      // 所有行（含天赋行）：刷值/摆位都靠它
    private readonly List<列槽> 上三列槽 = new List<列槽>();    // 上三列的行槽
    private readonly List<列槽> 天赋行槽 = new List<列槽>();    // 天赋列表的两槽（行是刷的时候按数据补的）
    private readonly List<行件> 天赋行件 = new List<行件>();    // 天赋行（与 上三列槽 里的行分开管）
    private bool 已建;

    // 一行 = 名 + 值（同一行，值是紧跟名字的文本，**不右对齐到天边**），带一条显隐判据（伤病那几行按闹不闹病显隐）
    private sealed class 行件
    {
        public RectTransform 矩形;
        public TMP_Text 名称;
        public TMP_Text 值;
        public Func<玩家档案, string> 取值;
        public Func<玩家档案, bool> 显示;   // null = 恒显示
    }

    // 一列行：行按"第几个显示的行"自上而下摆成 行高 的条带（藏起来的行不占位，下面的行顶上来，不留洞）。
    // 行高/字号**挂在槽上**而不是从外层读常量：天赋那两槽比上三列密（见 段行高_天赋），
    //   建行/摆槽 都只认自己这一槽的数 —— 少一处"这两个槽要特殊对待"的判断。
    private sealed class 列槽
    {
        public RectTransform 容器;
        public float 行高 = 48f;
        public int 字号 = 19;
        public readonly List<行件> 行 = new List<行件>();
    }

    // ================= 注入入口 + 建树 =================

    // 壳在每次建树/重建布局时调；本组件据此**就地重建自己那一套**（幂等）。
    public void 设内容区(RectTransform 区)
    {
        内容区 = 区;
        重建();
    }

    void Awake()
    {
        // 壳可能还没注入（本组件先 Awake），或它被拖进场景手搭 → 内容区 为空时什么都不建，
        //   等 `设内容区` 那次调用再建。这不是防御代码，是"注入是唯一入口"这条约定的直接后果。
        if (内容区 != null) 重建();
    }

    private void OnEnable()
    {
        if (已建) 刷新();
    }

    // 幂等重建：清掉自建 → 重新建固定骨架 → 刷（天赋行按数据补建，见 刷天赋）。
    private void 重建()
    {
        if (内容区 == null) return;
        清掉自建();

        画布 = 新矩形("属性页内容", 内容区);
        定锚(画布, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f));
        // 上沿让开"栏目标题 + 强调短线"那一条，下沿留 内边
        画布.offsetMin = new Vector2(内边, 内边);
        画布.offsetMax = new Vector2(-内边, -栏目标题带高);

        建五维段();
        建派生段();
        建生存段();
        建天赋骨架();

        已建 = true;
        刷新();
    }

    // 本页内容与栏目标题之间的留白：标题(24) + 短线(2) + 段距(20)
    private float 栏目标题带高 => 24f + 2f + 段距;
    private float 内边 => 段距 / 2f;   // 内容再往里缩一点点（不贴内容区的边）

    // ================= ① 五维 =================

    // 显示名 = `玩家档案` 的字段名，一一对应（用户要核属性名，所以每行都点明是哪个字段）
    private void 建五维段()
    {
        var 槽 = 建列("五维", 0f, 0.22f, 1);
        建行(槽[0], "力量", 玩家 => $"{玩家.力量}");   // 玩家档案.力量
        建行(槽[0], "体质", 玩家 => $"{玩家.体质}");   // 玩家档案.体质
        建行(槽[0], "敏捷", 玩家 => $"{玩家.敏捷}");   // 玩家档案.敏捷
        建行(槽[0], "智慧", 玩家 => $"{玩家.智慧}");   // 玩家档案.智慧
        建行(槽[0], "意志", 玩家 => $"{玩家.意志}");   // 玩家档案.意志
    }

    // ================= ② 派生（9 项，对半分成两列） =================

    private void 建派生段()
    {
        var 槽 = 建列("派生", 0.22f, 0.54f, 2);
        建行(槽[0], "近战伤害", 玩家 => $"{玩家.近战伤害}");        // 玩家档案.近战伤害
        建行(槽[0], "枪械伤害", 玩家 => $"{玩家.枪械伤害}");        // 玩家档案.枪械伤害
        建行(槽[0], "总防御", 玩家 => $"{玩家.总防御}");           // 玩家档案.总防御
        建行(槽[0], "暴击概率", 玩家 => $"{百分(玩家.暴击概率)}");  // 玩家档案.暴击概率（0~1）
        建行(槽[0], "命中率", 玩家 => $"{百分(命中率(玩家))}");     // 战斗单位.命中率（**玩家档案上没有这一项**，见 命中率）
        建行(槽[1], "闪避概率", 玩家 => $"{百分(玩家.闪避概率)}");  // 玩家档案.闪避概率（0~1）
        建行(槽[1], "最大生命", 玩家 => $"{玩家.最大生命}");        // 玩家档案.最大生命
        建行(槽[1], "最大行动点", 玩家 => $"{玩家.最大行动点}");    // 玩家档案.最大行动点
        建行(槽[1], "负重上限", 玩家 => $"{玩家.负重上限}");        // 玩家档案.负重上限
    }

    // 命中率：`玩家档案` 上**没有**这一项 —— 它只活在战斗单位上（基础 95% + 命中副属性，上限 99%）。
    // 这里**不照抄那条公式**（副本一定会漂移），而是取战斗投影里那一份权威值：`从玩家投影` 是公开的静态工厂。
    private static float 命中率(玩家档案 玩家) => 战斗单位.从玩家投影(玩家).命中率;

    private static string 百分(float 比例) => $"{Mathf.RoundToInt(比例 * 100f)}%";

    // ================= ③ 生存状态 =================

    private void 建生存段()
    {
        var 槽 = 建列("生存状态", 0.54f, 1f, 2);
        建行(槽[0], "生命", 玩家 => $"{玩家.生命} / {玩家.最大生命}");        // 玩家档案.生命 / 最大生命
        建行(槽[0], "行动点", 玩家 => $"{玩家.行动点} / {玩家.最大行动点}");   // 玩家档案.行动点 / 最大行动点
        建行(槽[0], "饱食度", 玩家 => $"{Mathf.RoundToInt(玩家.饱食度)}");    // 玩家档案.饱食度（0~100，float）
        建行(槽[0], "水分度", 玩家 => $"{Mathf.RoundToInt(玩家.水分度)}");    // 玩家档案.水分度（0~100，float）
        建行(槽[0], "疲劳", 玩家 => $"{玩家.疲劳}");                         // 玩家档案.疲劳

        // 伤病：`伤病类型` 共 6 种（中毒/感冒/流血/骨折/发烧 + 疲劳），疲劳 已经在上面单独占了一行 →
        //   这里列**其余 5 种**，各自只在真的 >0 时才占一行。行名直接用枚举名（`伤病类型` 的成员名就是中文显示名），
        //   数值走 `玩家档案.伤病值(类型)` 这条既有入口。
        伤病行(槽[1], 伤病类型.中毒);
        伤病行(槽[1], 伤病类型.感冒);
        伤病行(槽[1], 伤病类型.流血);
        伤病行(槽[1], 伤病类型.骨折);
        伤病行(槽[1], 伤病类型.发烧);
        // 一条病都没有 → 显示一行"伤病 无"（否则这一列空着，看着像坏了）
        建行(槽[1], "伤病", 玩家 => "无", 玩家 => !闹病(玩家));
    }

    private void 伤病行(列槽 槽, 伤病类型 类型)
    {
        var 病 = 类型;   // 闭包捕获（lambda 里直接用 类型 也行，显式存一份更不容易看错）
        建行(槽, 病.ToString(), 玩家 => $"{玩家.伤病值(病)}", 玩家 => 玩家.伤病值(病) > 0);
    }

    private static bool 闹病(玩家档案 玩家)
        => 玩家.伤病值(伤病类型.中毒) > 0 || 玩家.伤病值(伤病类型.感冒) > 0 || 玩家.伤病值(伤病类型.流血) > 0
        || 玩家.伤病值(伤病类型.骨折) > 0 || 玩家.伤病值(伤病类型.发烧) > 0;

    // ================= ④ 天赋（职业大卡 + 通用列表） =================

    // 只建**不随数据变的骨架**：小标题 + 职业大卡（两行文本）+ 列表的两条空槽。
    // 天赋行本身在 刷天赋 里按数据补建（条数取决于 天赋.json，不是固定值）。
    private void 建天赋骨架()
    {
        float 段顶 = 上三列高 + 段间空;   // 从画布上沿往下量
        var 段 = 新矩形("天赋", 画布);
        定锚(段, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        段.offsetMin = new Vector2(0f, -段顶 - 天赋段高);
        段.offsetMax = new Vector2(0f, -段顶);

        var 小标题 = 文本("小标题", 段, 字号_小字, 次要色, TextAlignmentOptions.Left);
        定锚(小标题.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        小标题.rectTransform.offsetMin = new Vector2(0f, -段标题带高);
        小标题.rectTransform.offsetMax = Vector2.zero;
        面板基类.设文本(小标题, "天赋");

        // 职业天赋大卡：一块内容底 + 四边 2px 边框的实心块（与壳里的板子同一套做法，不用 Outline）。
        //   里面两行：标题（**强调色** —— 用户点名"可以用强调色标题突出"）+ 一条机制行。
        var 卡 = 新矩形("职业天赋卡", 段);
        定锚(卡, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        卡.offsetMin = new Vector2(0f, -段标题带高 - 段间隔 - 职业卡高);
        卡.offsetMax = new Vector2(0f, -段标题带高 - 段间隔);
        var 卡底 = 卡.gameObject.AddComponent<Image>();
        卡底.color = 内容底色;
        卡底.raycastTarget = false;
        卡边框(卡);

        float 卡内 = 内边 + 4f;
        天赋卡标题 = 文本("标题", 卡, 字号_大卡标题, 强调色, TextAlignmentOptions.Left);
        定锚(天赋卡标题.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        天赋卡标题.rectTransform.offsetMin = new Vector2(卡内, -(卡内 + 字号_大卡标题 + 6f));
        天赋卡标题.rectTransform.offsetMax = new Vector2(-卡内, -卡内);
        天赋卡机制 = 文本("机制", 卡, 字号_正文, 正文色, TextAlignmentOptions.Left);
        定锚(天赋卡机制.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        天赋卡机制.rectTransform.offsetMin = new Vector2(卡内, -(卡内 + 字号_大卡标题 + 6f + 行高));
        天赋卡机制.rectTransform.offsetMax = new Vector2(-卡内, -(卡内 + 字号_大卡标题 + 6f));

        // 通用天赋列表：两列并排。17 条 → 每列 9 行 × 段行高_天赋，正好落在大卡下面那段高度里。
        天赋列表顶 = 段标题带高 + 段间隔 + 职业卡高 + 段间隔;
        for (int i = 0; i < 2; i++)
        {
            var 容器 = 新矩形("天赋行", 段);
            定锚(容器, new Vector2(i * 0.5f, 0f), new Vector2((i + 1) * 0.5f, 1f), new Vector2(0f, 0.5f));
            容器.offsetMin = Vector2.zero;
            容器.offsetMax = Vector2.zero;
            var 槽 = new 列槽 { 容器 = 容器, 行高 = 段行高_天赋, 字号 = 字号_正文 };
            天赋行槽.Add(槽);
        }
    }

    // ================= 段落尺寸（一处定义，建列/建天赋 都读它） =================

    private float 段标题带高 => 字号_小字 + 8f;
    // 上三列的高度：最深的列 = 小标题 + 段间隔 + 5×行高（五维 5 行；派生两列各 5 行；生存两列各 5~6 行）
    private float 上三列高 => 段标题带高 + 段间隔 + 5f * 行高;
    // 天赋段高度：小标题 + 大卡 + 两列行（每列最多 9 行）。9 = 通用天赋 17~18 条 / 2 列向上取整
    private float 天赋段高 => 段标题带高 + 段间隔 + 职业卡高 + 段间隔 + 9f * 段行高_天赋;

    // ================= 建列 / 建槽 / 建行 =================

    // 建一列：列宽 = 画布宽的 [左比例, 右比例]，列顶是小标题，下面是 子列数 个行槽（1 = 不再细分）。
    // 上三列共用同一条高度带（上三列高），所以它们的小标题与第一行都在同一条水平线上。
    private List<列槽> 建列(string 段名, float 左比例, float 右比例, int 子列数)
    {
        var 列 = 新矩形("列·" + 段名, 画布);
        定锚(列, new Vector2(左比例, 1f), new Vector2(右比例, 1f), new Vector2(0f, 1f));
        列.offsetMin = new Vector2(0f, -上三列高);
        列.offsetMax = Vector2.zero;

        var 小标题 = 文本(段名, 列, 字号_小字, 次要色, TextAlignmentOptions.Left);
        定锚(小标题.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        小标题.rectTransform.offsetMin = new Vector2(0f, -段标题带高);
        小标题.rectTransform.offsetMax = Vector2.zero;

        var 槽 = new List<列槽>();
        for (int i = 0; i < 子列数; i++) 槽.Add(建列槽(列, i / (float)子列数, (i + 1) / (float)子列数, 行高, 字号_正文));
        return 槽;
    }

    private 列槽 建列槽(RectTransform 父, float 左比例, float 右比例, float 行高, int 字号)
    {
        var 容器 = 新矩形("行", 父);
        定锚(容器, new Vector2(左比例, 0f), new Vector2(右比例, 1f), new Vector2(0f, 0.5f));
        容器.offsetMin = Vector2.zero;
        容器.offsetMax = Vector2.zero;
        var 槽 = new 列槽 { 容器 = 容器, 行高 = 行高, 字号 = 字号 };
        上三列槽.Add(槽);
        return 槽;
    }

    // 建一行"名 + 值"：值是紧跟名字的文本（**不右对齐到天边**）；行与行靠行高留白分层，**行间不画线**。
    // 名用 次要色（暗）、值用 正文色（亮）—— 一行读下来就是"标签 → 数"，不靠色条/图标区分。
    // 每一列/每一槽自带行高（见 列槽.行高），摆位时用它 —— 天赋那一段比别处密，靠的就是这个字段。
    private 行件 建行(列槽 槽, string 名, Func<玩家档案, string> 取值, Func<玩家档案, bool> 显示 = null)
    {
        var 矩形 = 新矩形(名, 槽.容器);
        var 名称 = 文本("名", 矩形, 槽.字号, 次要色, TextAlignmentOptions.MidlineLeft);
        定锚(名称.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        // 先给"最长 5 个汉字"的兜底宽度，摆行 时按 preferredWidth 校正
        名称.rectTransform.sizeDelta = new Vector2(槽.字号 * 5f, 0f);
        名称.rectTransform.anchoredPosition = Vector2.zero;
        var 值 = 文本("值", 矩形, 槽.字号, 正文色, TextAlignmentOptions.MidlineLeft);
        定锚(值.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f));
        值.rectTransform.offsetMin = new Vector2(槽.字号 * 5f + 段距 * 0.5f, 0f);
        值.rectTransform.offsetMax = Vector2.zero;

        var 行件 = new 行件 { 矩形 = 矩形, 名称 = 名称, 值 = 值, 取值 = 取值, 显示 = 显示 };
        槽.行.Add(行件);
        全部行.Add(行件);
        return 行件;
    }

    // ================= 刷新 =================

    public void 刷新()
    {
        if (!已建) return;
        var 玩家 = 当前档案();
        if (玩家 == null) return;

        foreach (var 行件 in 全部行)
        {
            if (行件.矩形 == null || 行件.取值 == null) continue;   // 天赋行没有 取值（它们在 刷天赋 里直接贴文本）
            bool 显 = 行件.显示 == null || 行件.显示(玩家);
            行件.矩形.gameObject.SetActive(显);
            if (显) 面板基类.设文本(行件.值, 行件.取值(玩家));
        }
        刷天赋(玩家);
        摆行();
    }

    private static 玩家档案 当前档案()
        => ServiceRegistry.已注册<PlayerService>() ? ServiceRegistry.Get<PlayerService>()?.档案 : null;

    // 天赋段的填充。
    // 数据来源 = `DataService.天赋`（天赋.json → `天赋数据` 字典）：
    //   职业大卡 = 玩家 `职业` → `职业数据.天赋`（天赋标识）→ 那条记录；
    //   通用列表 = 其余全部（`职业专属` 的排除），按标识排序保证每次刷新顺序一致。
    private void 刷天赋(玩家档案 玩家)
    {
        if (!ServiceRegistry.已注册<DataService>()) return;
        var 数据 = ServiceRegistry.Get<DataService>();
        if (数据?.天赋 == null) return;

        天赋数据 职业天赋 = null;
        if (!string.IsNullOrEmpty(玩家.职业) && 数据.职业 != null
            && 数据.职业.TryGetValue(玩家.职业, out var 职业) && 职业 != null
            && !string.IsNullOrEmpty(职业.天赋))
            数据.天赋.TryGetValue(职业.天赋, out 职业天赋);

        面板基类.设文本(天赋卡标题, 职业天赋 != null ? 职业天赋.名称 : "职业天赋");
        面板基类.设文本(天赋卡机制, 职业天赋 != null ? 机制行(职业天赋) : "—— 未选择职业 ——");

        var 通用 = new List<天赋数据>();
        foreach (var 对 in 数据.天赋)
            if (对.Value != null && !对.Value.职业专属) 通用.Add(对.Value);
        通用.Sort((a, b) => string.CompareOrdinal(a.标识, b.标识));

        备天赋行(通用.Count);
        // 行按"第几条"轮流落到两槽（0,2,4… 进左槽；1,3,5… 进右槽）——
        //   这样两列的条数最多差 1，短的那列不会把最后一行甩到板外。
        for (int i = 0; i < 天赋行件.Count; i++)
        {
            var 行件 = 天赋行件[i];
            if (行件.矩形 == null) continue;
            if (i >= 通用.Count) { 行件.矩形.gameObject.SetActive(false); continue; }
            var 条 = 通用[i];
            行件.矩形.gameObject.SetActive(true);
            面板基类.设文本(行件.名称, 条.名称);
            面板基类.设文本(行件.值, 机制行(条));
        }
    }

    // 按需要的条数补/减天赋行。行只加不减地复用：数据条数变了（换职业/换存档）才动，
    //   而且**只增不减**（余下的行 SetActive(false)）—— 减行会让每帧刷新都在销毁/新建，白扔垃圾。
    private void 备天赋行(int 需要)
    {
        while (天赋行件.Count < 需要)
        {
            var 槽 = 天赋行槽[天赋行件.Count % 天赋行槽.Count];
            var 行件 = 建行(槽, "", null);
            行件.名称.rectTransform.sizeDelta = new Vector2(天赋名宽, 0f);   // 天赋名宽固定：机制从同一条竖线起
            行件.值.rectTransform.offsetMin = new Vector2(天赋名宽 + 段距 * 0.5f, 0f);
            天赋行件.Add(行件);
        }
    }

    // 一条天赋的"机制一行"。取法：`效果[]` 有内容 → 逐项写"目标 ±数值"；没有效果 → 退回 `描述`。
    //   为什么效果为空时不能留空：**职业专属那 6 条就是 `效果: []`**（它们的效果写在代码钩子里，
    //   见 `天赋数据.职业专属` 的注释），机制只能从 `描述` 来 —— 留空会让那张大卡看起来是坏的。
    private static string 机制行(天赋数据 条)
    {
        if (条.效果 != null && 条.效果.Length > 0)
        {
            var 串 = new StringBuilder();
            foreach (var 效 in 条.效果)
            {
                if (效 == null) continue;
                if (串.Length > 0) 串.Append("　");   // 全角空格分隔：多个效果并列时不用标点（标点在这套排版里很吵）
                串.Append(效.目标).Append(' ').Append(数值文本(效));
            }
            if (串.Length > 0) return 串.ToString();
        }
        return 条.描述;
    }

    // 效果数值的写法：五维按"点"写 ±N；其余目标（饱食/水分/经验/医疗/制作/夜晚/睡觉/感冒）是**百分比修正**
    //   （见 `天赋效果项.数值` 的注释）。比例那类写成 ±N% —— 不写百分号会把"饱食 60"读成 60 点。
    private static string 数值文本(天赋效果项 效)
    {
        float 值 = 效.数值;
        int 整 = Mathf.RoundToInt(Mathf.Abs(值));
        switch (效.目标)
        {
            case "体质":
            case "力量":
            case "智慧":
            case "敏捷":
            case "意志":
                return 值 >= 0 ? $"+{整}" : $"-{整}";
            default:
                return 值 >= 0 ? $"+{整}%" : $"-{整}%";
        }
    }

    // ================= 摆行 =================

    // 摆行：每槽自成一条竖带，槽顶 = 小标题 + 段间隔，往下按 行高 依次排**当前显示的行**（藏起来的不占位）。
    // 名宽取上三列所有行里的最大值（最宽的是 5 个汉字的"最大行动点"）→ 值从同一条竖线起：
    //   像一张小表，又不至于把数值甩到整块板的右边缘（用户明令去掉的那种"仪表盘"味）。
    //   天赋行不参与这个名宽（它们各自固定 天赋名宽，见 备天赋行）。
    private void 摆行()
    {
        float 名宽 = 0f;
        foreach (var 槽 in 上三列槽)
            foreach (var 行件 in 槽.行)
                if (行件.名称 != null && 行件.名称.preferredWidth > 名宽) 名宽 = 行件.名称.preferredWidth;
        if (名宽 <= 1f) 名宽 = 字号_正文 * 5f;   // TMP 还没量出文字（极端情况）→ 用"最长 5 个汉字"兜底
        float 值左 = 名宽 + 段距 * 0.5f;
        foreach (var 槽 in 上三列槽)
            foreach (var 行件 in 槽.行)
                if (行件.值 != null) 行件.值.rectTransform.offsetMin = new Vector2(值左, 0f);

        float 各槽顶 = 段标题带高 + 段间隔;
        foreach (var 槽 in 上三列槽) 摆槽(槽, 各槽顶);
        foreach (var 槽 in 天赋行槽) 摆槽(槽, 天赋列表顶);
    }

    // 摆一条槽：槽顶往下按**这一槽自己的行高**依次排当前显示的行（藏起来的不占位，下面的顶上来）
    private void 摆槽(列槽 槽, float 顶)
    {
        foreach (var 行件 in 槽.行)
        {
            if (行件.矩形 == null) continue;
            // 行件被追加的先后顺序 = 想要的显示顺序（备天赋行 按条数补在末尾 → 槽内顺序天然正确）
            if (!行件.矩形.gameObject.activeSelf) continue;
            // 主动算一次文字宽：`preferredWidth` 平时要等布局系统调用才算得出来。
            //   为什么值得主动算：`末日/角色/一键生成面板预制体` 是在编辑器**非运行态**调 重建布局() 的，
            //   那一帧没有第二次布局回调 —— 不补这一步，烘进预制体的名宽/值起点会按兜底值摆。
            行件.名称?.ForceMeshUpdate(true);   // ignoreActiveState：面板可能整棵还没激活
            设行带(行件.矩形, 顶, 槽.行高);
            顶 += 槽.行高;
        }
    }

    // 一行 = 所在槽里的一条横带：左右铺满槽宽，自上而下从 顶 开始、高 高
    private static void 设行带(RectTransform 行, float 顶, float 高)
    {
        定锚(行, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        行.offsetMin = new Vector2(0f, -顶 - 高);
        行.offsetMax = new Vector2(0f, -顶);
    }

    // ================= 底色 / 边框 / 建节点的小工具 =================

    // 四边 2px 边框（与壳里的板子同一套做法：拉伸锚 + sizeDelta，不用 Outline）
    private void 卡边框(RectTransform 父)
    {
        const float 框 = 2f;
        边框条(父, "上", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 框));
        边框条(父, "下", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 框));
        边框条(父, "左", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(框, 0f));
        边框条(父, "右", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(框, 0f));
    }

    private void 边框条(RectTransform 父, string 名, Vector2 锚最小, Vector2 锚最大, Vector2 轴心, Vector2 尺寸)
    {
        var 矩形 = 新矩形("边框" + 名, 父);
        定锚(矩形, 锚最小, 锚最大, 轴心);
        矩形.anchoredPosition = Vector2.zero;
        矩形.sizeDelta = 尺寸;
        var 图 = 矩形.gameObject.AddComponent<Image>();
        图.color = 边框色;
        图.raycastTarget = false;
    }

    // 清掉本组件建过的那一套（**只销毁账本上的**；调用方拖进来的物体一个都不动）
    private void 清掉自建()
    {
        if (自建节点 == null) 自建节点 = new List<GameObject>();
        foreach (var 物体 in 自建节点)
        {
            if (物体 == null) continue;
            销毁(物体);
        }
        自建节点.Clear();
        全部行.Clear();
        上三列槽.Clear();
        天赋行槽.Clear();
        天赋行件.Clear();
        画布 = null;
        天赋卡标题 = null;
        天赋卡机制 = null;
        已建 = false;
    }

    // 建一个只有 RectTransform 的节点。**默认记账**：本组件建的东西都要能被 重建 收回去。
    private RectTransform 新矩形(string 名, Transform 父, bool 记账 = true)
    {
        var 物体 = new GameObject(名, typeof(RectTransform));
        物体.transform.SetParent(父, false);
        if (记账) 自建节点.Add(物体);
        return (RectTransform)物体.transform;
    }

    // 建一段文本（字体取项目 TMP 默认字体资产，不硬编码资源路径）
    private TMP_Text 文本(string 名, Transform 父, int 字号, Color 色, TextAlignmentOptions 对齐, bool 记账 = true)
    {
        var 矩形 = 新矩形(名, 父, 记账);
        var 文本组件 = 矩形.gameObject.AddComponent<TextMeshProUGUI>();
        文本组件.font = TMP_Settings.defaultFontAsset;
        文本组件.fontSize = 字号;
        文本组件.color = 色;
        文本组件.alignment = 对齐;
        文本组件.textWrappingMode = TextWrappingModes.NoWrap;
        文本组件.overflowMode = TextOverflowModes.Ellipsis;
        文本组件.raycastTarget = false;
        return 文本组件;
    }

    private static void 定锚(RectTransform 矩形, Vector2 锚最小, Vector2 锚最大, Vector2 轴心)
    {
        矩形.anchorMin = 锚最小;
        矩形.anchorMax = 锚最大;
        矩形.pivot = 轴心;
    }

    // 销毁：编辑器里（未运行）用 DestroyImmediate（Destroy 在编辑器下不会立刻生效，紧接着的重建会读到旧节点）
    private static void 销毁(GameObject 物体)
    {
        if (物体 == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) { DestroyImmediate(物体); return; }
#endif
        物体.SetActive(false);
        Destroy(物体);
    }
}
