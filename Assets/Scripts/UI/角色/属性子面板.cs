using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 属性子面板：角色面板 [属性] 栏页的内容（由 `角色面板` 注入 `内容区`；本组件**不被壳读任何字段**）。
//
// 本页四个小段（自上而下；小标题 + 行内文本 + 留白分层，**行间不画线**）：
//   ① 五维      力量 / 体质 / 敏捷 / 智慧 / 意志（显示名 = `玩家档案` 的字段名，逐条点明是哪个字段）
//   ② 派生      近战伤害 / 枪械伤害 / 总防御 / 暴击概率 / 命中率 / 闪避概率 / 最大生命 / 最大行动点 / 负重上限
//   ③ 生存状态  生命 / 行动点 / 饱食度 / 水分度 / 疲劳 + 伤病（仅 >0 的占行，全无则"伤病 无"）
//   ④ 天赋      职业天赋大卡（`职业.json` 的 天赋 → `天赋.json` 里那条）+ 通用天赋列表（每条只显示机制一行）
//
// ★ 为什么天赋在这一页而不是自己一个栏：用户 2026-09-17 当场拍板 —— "这部分内容很少，不需要单独写一个子面板"。
//   于是 Tab 从四个压成三个（属性 / 技能 / 知识），天赋降级成本页的第 4 个小段。
//
// ★ 本批（刀82）口径：**结构 100% 读预制体，本组件只建"随数据条数变的东西"**。
//   哪些是"预制体里现成的"（本组件只按名字找出来、只改文字/颜色/显隐/位置，**不新建**）：
//     · 页面根 `属性页内容`（负责让开栏目标题那条）
//     · 三列 `列·五维 / 列·派生 / 列·生存状态` + 各自的 `行` 容器 + **那 24 行静态行**（力量…发烧）
//     · 天赋段 `天赋`（小标题 / 职业天赋卡 / 卡里的 `标题` `机制` / **3 个 `天赋行` 容器**）
//   哪些是"运行时按数据补的"（条数不固定，烘不进预制体）：
//     · 通用天赋列表的行（`天赋.json` 里 通用天赋 的条数会变）
//   ⚠ 名字就是契约：路径写死在 `绑定位()` 里；在 Unity 里重命名节点要同步改那里（缺了会打 LogError，不静默）。
//
// 布局：上面三列（五维 | 派生 | 生存状态）共用一条像素高度带，第 4 段（天赋）横跨整行落在它下面。
//   为什么天赋不挤进第 4 列：通用天赋有 17 条，一列塞不下（塞得下也会把 5 个汉字的名挤到折行）。
//   为什么现在是 3 列：17 条 ÷ 3 列 = 6 行 × 40px = 240，加上小标题+大卡正好落在 980 高的板子里（见 天赋段高）。
//
// 配色/字号/间距：**随状态变的**（行名/行值的颜色、天赋正负面色）在本文件自己的 [SerializeField] 里；
//   **静态的**（分节小标题的字号色、行高、卡片尺寸）在预制体里（代码不碰 → 在 Unity 里调得动）。
public sealed class 属性子面板 : MonoBehaviour
{
    // ================= 注入：`内容区` 由 角色面板 给（也有预制体里烘好的引用位） =================
    [SerializeField] private RectTransform 内容区;

    // ================= 颜色（只有"随状态变"的才在代码里） =================
    [SerializeField] private Color 正文色 = new Color(0.8941f, 0.8745f, 0.8392f, 1f);   // #E4DFD6 行值（正常）
    [SerializeField] private Color 次要色 = new Color(0.5412f, 0.5137f, 0.4706f, 1f);   // #8A8378 行名
    [SerializeField] private Color 增益色 = new Color(0.4314f, 0.5608f, 0.3843f, 1f);   // #6E8F62 正面天赋的机制值
    [SerializeField] private Color 减益色 = new Color(0.7059f, 0.3333f, 0.2353f, 1f);   // #B4553C 负面天赋的机制值（= 全屏唯一强调色）

    // ================= 字号 / 间距（**布局常量**：行是按数据显隐/排序的，位置只能运行时算） =================
    [SerializeField] private int 字号_正文 = 19;        // 行内文字（与预制体里烘的一致）
    [SerializeField] private int 字号_小字 = 16;        // 分节小标题（= 段标题带高 的基数）
    [SerializeField] private float 行高 = 48f;          // 上三列的行高
    [SerializeField] private float 段距 = 20f;          // 名 → 值 之间的那道竖线距离的一半
    [SerializeField] private float 段间隔 = 12f;        // 小标题 → 第一行
    [SerializeField] private float 段行高_天赋 = 40f;   // 天赋行比正文行密一档（17 条要落在一屏里）
    [SerializeField] private float 天赋名宽 = 104f;     // 天赋行里"名称"那一列的固定宽（机制从同一条竖线起）
    [SerializeField] private float 职业卡高 = 84f;      // 预制体里 `职业天赋卡` 的高度（天赋列表从它下面起）

    // 运行时补的行（天赋行）：**不序列化** —— 它们不属于资产，随组件实例生死
    private readonly List<GameObject> 自建行 = new List<GameObject>();

    private RectTransform 画布;                       // `属性页内容`：本页真正的根
    private TMP_Text 天赋卡标题, 天赋卡机制;
    private float 天赋列表顶;                         // 天赋列表各槽的起点（从段顶往下量）
    private readonly List<行件> 全部行 = new List<行件>();      // 所有静态行（天赋行不在此列）
    private readonly List<列槽> 上三列槽 = new List<列槽>();    // 上三列的行槽
    private readonly List<列槽> 天赋行槽 = new List<列槽>();    // 天赋列表的 3 个槽（行是刷的时候按数据补的）
    private readonly List<行件> 天赋行件 = new List<行件>();    // 天赋行件（与 上三列槽 里的行分开管）
    private readonly List<string> 缺的节点 = new List<string>();
    private bool 已绑;

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
    // 行高/字号**挂在槽上**而不是从外层读常量：天赋那三槽比上三列密（见 段行高_天赋），
    //   建行/摆槽 都只认自己这一槽的数 —— 少一处"这几个槽要特殊对待"的判断。
    private sealed class 列槽
    {
        public RectTransform 容器;
        public float 行高 = 48f;
        public int 字号 = 19;
        public readonly List<行件> 行 = new List<行件>();
    }

    // ================= 注入入口 =================

    // 壳在装配时调一次（预制体里 内容区 本来就接好了 → 这条只是"手搭一半"的兜底）。
    public void 设内容区(RectTransform 区)
    {
        if (区 == null || 区 == 内容区) return;
        内容区 = 区;
        已绑 = false;
        if (isActiveAndEnabled) 刷新();
    }

    void Awake() { 刷新(); }

    // 切栏把它点亮 → 自己刷一次（这样"切过去看到旧数据"这件事不可能发生）
    private void OnEnable() { 刷新(); }

    // ================= 绑定位（只找、不建） =================

    private void 绑定位()
    {
        if (已绑) return;
        已绑 = true;
        清掉自建行();   // 重新绑定时先把上一轮造的天赋行收掉（否则会在容器里堆第二套）
        全部行.Clear();
        上三列槽.Clear();
        天赋行槽.Clear();
        天赋行件.Clear();
        缺的节点.Clear();
        画布 = 子矩形(内容区, "属性页内容");
        if (画布 == null) { 缺("属性页内容"); 报缺(); return; }

        建五维段();
        建派生段();
        建生存段();
        绑天赋段();
        报缺();
    }

    // ================= ① 五维 =================

    // 显示名 = `玩家档案` 的字段名，一一对应（用户要核属性名，所以每行都点明是哪个字段）
    private void 建五维段()
    {
        var 槽 = 绑列("五维", 1);
        绑行(槽, 0, "力量", 玩家 => $"{玩家.力量}");   // 玩家档案.力量
        绑行(槽, 0, "体质", 玩家 => $"{玩家.体质}");   // 玩家档案.体质
        绑行(槽, 0, "敏捷", 玩家 => $"{玩家.敏捷}");   // 玩家档案.敏捷
        绑行(槽, 0, "智慧", 玩家 => $"{玩家.智慧}");   // 玩家档案.智慧
        绑行(槽, 0, "意志", 玩家 => $"{玩家.意志}");   // 玩家档案.意志
    }

    // ================= ② 派生（9 项，对半分成两列） =================

    private void 建派生段()
    {
        var 槽 = 绑列("派生", 2);
        绑行(槽, 0, "近战伤害", 玩家 => $"{玩家.近战伤害}");        // 玩家档案.近战伤害
        绑行(槽, 0, "枪械伤害", 玩家 => $"{玩家.枪械伤害}");        // 玩家档案.枪械伤害
        绑行(槽, 0, "总防御", 玩家 => $"{玩家.总防御}");           // 玩家档案.总防御
        绑行(槽, 0, "暴击概率", 玩家 => $"{百分(玩家.暴击概率)}");  // 玩家档案.暴击概率（0~1）
        绑行(槽, 0, "命中率", 玩家 => $"{百分(命中率(玩家))}");     // 战斗单位.命中率（**玩家档案上没有这一项**，见 命中率）
        绑行(槽, 1, "闪避概率", 玩家 => $"{百分(玩家.闪避概率)}");  // 玩家档案.闪避概率（0~1）
        绑行(槽, 1, "最大生命", 玩家 => $"{玩家.最大生命}");        // 玩家档案.最大生命
        绑行(槽, 1, "最大行动点", 玩家 => $"{玩家.最大行动点}");    // 玩家档案.最大行动点
        绑行(槽, 1, "负重上限", 玩家 => $"{玩家.负重上限}");        // 玩家档案.负重上限
    }

    // 命中率：`玩家档案` 上**没有**这一项 —— 它只活在战斗单位上（基础 95% + 命中副属性，上限 99%）。
    // 这里**不照抄那条公式**（副本一定会漂移），而是取战斗投影里那一份权威值：`从玩家投影` 是公开的静态工厂。
    private static float 命中率(玩家档案 玩家) => 战斗单位.从玩家投影(玩家).命中率;

    private static string 百分(float 比例) => $"{Mathf.RoundToInt(比例 * 100f)}%";

    // ================= ③ 生存状态 =================

    private void 建生存段()
    {
        var 槽 = 绑列("生存状态", 2);
        绑行(槽, 0, "生命", 玩家 => $"{玩家.生命} / {玩家.最大生命}");        // 玩家档案.生命 / 最大生命
        绑行(槽, 0, "行动点", 玩家 => $"{玩家.行动点} / {玩家.最大行动点}");   // 玩家档案.行动点 / 最大行动点
        绑行(槽, 0, "饱食度", 玩家 => $"{Mathf.RoundToInt(玩家.饱食度)}");    // 玩家档案.饱食度（0~100，float）
        绑行(槽, 0, "水分度", 玩家 => $"{Mathf.RoundToInt(玩家.水分度)}");    // 玩家档案.水分度（0~100，float）
        绑行(槽, 0, "疲劳", 玩家 => $"{玩家.疲劳}");                         // 玩家档案.疲劳

        // 伤病：`伤病类型` 共 6 种（中毒/感冒/流血/骨折/发烧 + 疲劳），疲劳 已经在上面单独占了一行 →
        //   这里列**其余 5 种**，各自只在真的 >0 时才占一行。行名直接用枚举名（`伤病类型` 的成员名就是中文显示名），
        //   数值走 `玩家档案.伤病值(类型)` 这条既有入口。
        伤病行(槽, 1, 伤病类型.中毒);
        伤病行(槽, 1, 伤病类型.感冒);
        伤病行(槽, 1, 伤病类型.流血);
        伤病行(槽, 1, 伤病类型.骨折);
        伤病行(槽, 1, 伤病类型.发烧);
        // 一条病都没有 → 显示一行"伤病 无"（否则这一列空着，看着像坏了）。
        //   ⚠ 上面 5 行 + 这一行**最多同时出现 5 行**（有任意一条病时"无"那行就藏起来）——
        //     正好卡在 5 行 × 行高 = 上三列高 的预算里，不会溢出到板子外面。
        绑行(槽, 1, "伤病", 玩家 => "无", 玩家 => !闹病(玩家));
    }

    private void 伤病行(List<列槽> 槽, int 子列, 伤病类型 类型)
    {
        var 病 = 类型;   // 闭包捕获（lambda 里直接用 类型 也行，显式存一份更不容易看错）
        绑行(槽, 子列, 病.ToString(), 玩家 => $"{玩家.伤病值(病)}", 玩家 => 玩家.伤病值(病) > 0);
    }

    private static bool 闹病(玩家档案 玩家)
        => 玩家.伤病值(伤病类型.中毒) > 0 || 玩家.伤病值(伤病类型.感冒) > 0 || 玩家.伤病值(伤病类型.流血) > 0
        || 玩家.伤病值(伤病类型.骨折) > 0 || 玩家.伤病值(伤病类型.发烧) > 0;

    // ================= ④ 天赋（职业大卡 + 通用列表） =================

    // 只绑**不随数据变的骨架**：小标题 + 职业大卡（两行文本）+ 列表的三个空槽（预制体里现成的）。
    // 天赋行本身在 刷天赋 里按数据补建（条数取决于 天赋.json，不是固定值）。
    private void 绑天赋段()
    {
        var 段 = 子矩形(画布, "天赋");
        if (段 == null) { 缺("天赋"); return; }
        面板基类.设文本(子文本(段, "小标题"), "天赋");

        var 卡 = 子矩形(段, "职业天赋卡");
        if (卡 != null)
        {
            天赋卡标题 = 子文本(卡, "标题");
            天赋卡机制 = 子文本(卡, "机制");
        }
        天赋列表顶 = 段标题带高 + 段间隔 + 职业卡高 + 段间隔;
        for (int i = 0; i < 段.childCount; i++)
        {
            var 子 = (RectTransform)段.GetChild(i);
            if (子.name != "天赋行") continue;
            天赋行槽.Add(new 列槽 { 容器 = 子, 行高 = 段行高_天赋, 字号 = 字号_正文 });
        }
        if (天赋行槽.Count == 0) 缺("天赋/天赋行");
    }

    // ================= 段落尺寸 =================

    private float 段标题带高 => 字号_小字 + 8f;    // 与预制体里 `五维` 那行文字的高度同源
    // 上三列的行预算 = 段标题带高 + 段间隔 + 5 × 行高（预制体里那三列的高度就是照这个烘的，`行` 容器正好铺满）。
    //   最深的一列是 `生存状态`（生命…疲劳 占 5 行）；伤病那 5 行**只在真闹病时才占位**，
    //   而且"伤病 无"那行此时会藏起来 → 那一列最多同时 5 行，正好卡住预算，不会溢出到板子外面。

    // ================= 绑定：列 / 槽 / 行 =================

    // 绑一列：列节点 `列·段名` 下依次是 `段名`（分节小标题）+ 若干 `行` 容器。
    // 返回的列表就是这一段的槽（下标 = 子列序号）。
    private List<列槽> 绑列(string 段名, int 子列数)
    {
        var 列 = 子矩形(画布, "列·" + 段名);
        if (列 == null) { 缺("列·" + 段名); return new List<列槽>(); }
        // 分节小标题：预制体里这几个是**空文本**（烘的时候那一步没写文字）→ 在这里补上，否则整页没有分节名。
        //   写的是节点名本身（五维/派生/生存状态），所以"改标题"= 改预制体里的节点名 + 改这里的 `段名` 实参。
        面板基类.设文本(子文本(列, 段名), 段名);

        var 槽 = new List<列槽>();
        var 行父 = new List<RectTransform>();
        for (int i = 0; i < 列.childCount; i++)
        {
            var 子 = (RectTransform)列.GetChild(i);
            if (子.name == "行") 行父.Add(子);
        }
        for (int i = 0; i < 子列数; i++)
        {
            if (i >= 行父.Count) { 缺($"列·{段名} 的第 {i + 1} 个 行 容器"); continue; }
            var s = new 列槽 { 容器 = 行父[i], 行高 = 行高, 字号 = 字号_正文 };
            槽.Add(s);
            上三列槽.Add(s);
        }
        return 槽;
    }

    // 绑一行"名 + 值"：值是紧跟名字的文本（**不右对齐到天边**）；行与行靠行高留白分层，**行间不画线**。
    // 名用 次要色（暗）、值用 正文色（亮）—— 一行读下来就是"标签 → 数"，不靠色条/图标区分。
    private 行件 绑行(List<列槽> 槽, int 子列, string 名, Func<玩家档案, string> 取值, Func<玩家档案, bool> 显示 = null)
    {
        if (子列 < 0 || 子列 >= 槽.Count) return null;
        var 矩形 = 子矩形(槽[子列].容器, 名);
        if (矩形 == null) { 缺(名); return null; }
        var 行件 = new 行件
        {
            矩形 = 矩形,
            名称 = 子文本(矩形, "名"),
            值 = 子文本(矩形, "值"),
            取值 = 取值,
            显示 = 显示,
        };
        槽[子列].行.Add(行件);
        全部行.Add(行件);
        return 行件;
    }

    // ================= 刷新 =================

    public void 刷新()
    {
        绑定位();
        if (画布 == null) return;
        var 玩家 = 当前档案();
        if (玩家 == null) return;

        foreach (var 行件 in 全部行)
        {
            if (行件.矩形 == null || 行件.取值 == null) continue;
            bool 显 = 行件.显示 == null || 行件.显示(玩家);
            if (行件.矩形.gameObject.activeSelf != 显) 行件.矩形.gameObject.SetActive(显);
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
        // 行按"第几条"轮流落到三个槽（0,3,6… 进左槽；1,4,7… 进中槽；2,5,8… 进右槽）——
        //   这样三列的条数最多差 1，短的那列不会把最后一行甩到板外。
        for (int i = 0; i < 天赋行件.Count; i++)
        {
            var 行件 = 天赋行件[i];
            if (行件.矩形 == null) continue;
            if (i >= 通用.Count) { 行件.矩形.gameObject.SetActive(false); continue; }
            var 条 = 通用[i];
            行件.矩形.gameObject.SetActive(true);
            面板基类.设文本(行件.名称, 条.名称);
            面板基类.设文本(行件.值, 机制行(条));
            // 正负一眼可辨（刀82 视觉优化）：正面 = 增益色（草绿），负面 = 减益色（砖红），机制类不染色。
            //   判据用 `点数`（正数 = 花费 = 正面；负数 = 返还 = 负面），比去解析效果正负更贴数据口径。
            行件.值.color = 条.点数 > 0 ? 增益色 : 条.点数 < 0 ? 减益色 : 正文色;
        }
    }

    // 按需要的条数补/减天赋行。行只加不减地复用：数据条数变了（换职业/换存档）才动，
    //   而且**只增不减**（余下的行 SetActive(false)）—— 减行会让每次刷新都在销毁/新建，白扔垃圾。
    private void 备天赋行(int 需要)
    {
        if (天赋行槽.Count == 0) return;
        while (天赋行件.Count < 需要)
        {
            var 槽 = 天赋行槽[天赋行件.Count % 天赋行槽.Count];
            var 行件 = 造行(槽);
            if (行件 == null) return;
            天赋行件.Add(行件);
        }
    }

    // 造一行天赋（唯一"运行时新建节点"的地方：条数随 `天赋.json` 变，没法烘进预制体）。
    // 造出来的是"名 + 值"两段文本，样式取本文件的字段（字号_正文 / 次要色 / 正文色）。
    private 行件 造行(列槽 槽)
    {
        var 矩形 = 新矩形("天赋行", 槽.容器);
        var 名称 = 造文本("名", 矩形, 槽.字号, 次要色, TextAlignmentOptions.MidlineLeft);
        定锚(名称.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        名称.rectTransform.sizeDelta = new Vector2(天赋名宽, 0f);   // 天赋名宽固定：机制从同一条竖线起
        名称.rectTransform.anchoredPosition = Vector2.zero;
        var 值 = 造文本("值", 矩形, 槽.字号, 正文色, TextAlignmentOptions.MidlineLeft);
        定锚(值.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f));
        值.rectTransform.offsetMin = new Vector2(天赋名宽 + 段距 * 0.5f, 0f);
        值.rectTransform.offsetMax = Vector2.zero;

        var 行件 = new 行件 { 矩形 = 矩形, 名称 = 名称, 值 = 值 };
        // ⚠ 天赋行**不进 `全部行`**：它们没有 取值/显示（刷的时候直接贴文本），进那张表会被 刷新 的空判据漏掉、
        //   也会把 摆行 里"上三列的名宽"算错（天赋行用的是固定宽）。
        return 行件;
    }

    // 一条天赋的"机制一行"。取法：`效果[]` 有内容 → 逐项写"目标 ±数值"；没有效果 → 退回 `描述`。
    //   为什么效果为空时不能留空：**职业专属那 6 条就是 `效果: []`**（它们的效果写在代码钩子里），
    //   机制只能从 `描述` 来 —— 留空会让那张大卡看起来是坏的。
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

    // ================= 摆行（**位置是运行时算的**：哪些行显示随数据变，位置就随数据变） =================

    // 每槽自成一条竖带，槽顶 = 小标题 + 段间隔，往下按 行高 依次排**当前显示的行**（藏起来的不占位）。
    // 名宽取上三列所有行里的最大值（最宽的是 5 个汉字的"最大行动点"）→ 值从同一条竖线起：
    //   像一张小表，又不至于把数值甩到整块板的右边缘（用户明令去掉的那种"仪表盘"味）。
    //   天赋行不参与这个名宽（它们各自固定 天赋名宽）。
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
            // 主动算一次文字宽：`preferredWidth` 平时要等布局系统调用才算得出来，而"面板刚被激活"的那一帧
            //   还没有第二次布局回调 —— 不补这一步，名宽会按兜底值摆（值那一列就对不齐）。
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

    // ================= 找节点的小工具（只找、只报，不建） =================

    private static RectTransform 子矩形(Transform 父, string 名)
    {
        if (父 == null) return null;
        var 子 = 父.Find(名);
        return 子 as RectTransform;
    }

    private static TMP_Text 子文本(Transform 父, string 名)
    {
        var 子 = 父 != null ? 父.Find(名) : null;
        return 子 != null ? 子.GetComponent<TMP_Text>() : null;
    }

    private void 缺(string 名)
    {
        if (!缺的节点.Contains(名)) 缺的节点.Add(名);
    }

    // 缺节点要出声（以前是静默的：少一个节点 = 面板上少一块，没有任何日志）
    private void 报缺()
    {
        if (缺的节点.Count == 0) return;
        Debug.LogError("[属性子面板] 预制体里缺这些节点：" + string.Join("、", 缺的节点) +
                       "。本组件**不再自建骨架** —— 请补进 `Assets/Resources/Prefab/角色面板.prefab`" +
                       "（整备脚本：`.workbuddy/分析/角色面板-prefab整备.py`）。");
    }

    // ================= 运行时造行的小工具（只有"条数随数据变"的行才走这里） =================

    private RectTransform 新矩形(string 名, Transform 父)
    {
        var 物体 = new GameObject(名, typeof(RectTransform));
        物体.transform.SetParent(父, false);
        自建行.Add(物体);
        return (RectTransform)物体.transform;
    }

    // 收掉"运行时造的那些行"（只收自己造的；预制体里的节点一个都不动）
    private void 清掉自建行()
    {
        foreach (var 物 in 自建行)
        {
            if (物 == null) continue;
            if (Application.isPlaying) Destroy(物);
            else DestroyImmediate(物);   // 编辑器里（未运行）：Destroy 要等帧末，紧接着的重建会读到"还没死的旧行"
        }
        自建行.Clear();
    }

    // 造一段文本（字体取项目 TMP 默认字体资产，不硬编码资源路径）
    private TMP_Text 造文本(string 名, Transform 父, int 字号, Color 色, TextAlignmentOptions 对齐)
    {
        var 矩形 = 新矩形(名, 父);
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
}
