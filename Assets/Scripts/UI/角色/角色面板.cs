using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 角色面板：全局面板。一屏**三栏** = 一块浮在安全屋画面上的"幸存者档案板"
//   （板内：顶部身份条 + 顶部横排三个大字 Tab + 内容区；三个栏页的内容各自在子面板里）。
//
// ★ 本文件是**第七次改写**。前六代的路子记在这里，别再翻 git：
//     ① 刀78  代码自建 + "不铺满屏 / 组件放大 / 暖调近黑 + 唯一暗锈红" —— 用户认了这个观感；
//     ② 刀79  整批拆掉自建，改成"用户自己搭 + Inspector 接线"；用户随后改主意；
//     ③ 刀80  自建回来，12 个结构引用保留为"可选覆盖"；
//     ④ 刀81  **"皮肤"这个概念整个删掉**（`角色面板皮肤.cs` 连 meta 一起没了）+ 四栏压成三栏
//             （属性 / 技能 / 知识 —— 天赋内容并入 属性页 当第 4 个小段）；
//     ⑤ 刀82  **运行时不再建节点**，结构 100% 以 `Assets/Resources/Prefab/角色面板.prefab` 为准。
//     ⑥ 刀86  **外观 100% 归预制体**（用户 2026-09-17 定调："那些色号、字号不要 —— 我在 Unity 里自己调外观。"）：
//             · 代码里不出现任何色值 / 字号 / 尺寸字面量，也不写任何颜色（自查：0 处）；
//             · 状态只准用**结构手段**表达：Tab 选中 → 切 `Tab选中高亮` 那一项的 `SetActive`；
//               经验格进度 → `Image.fillAmount`（预制体里把每格的 Image 设成 `Type = Filled`）；
//             · 行底 / 悬停 / 选中底色 / 按钮过渡设置一律**在预制体里调**，代码不碰。
//     ⑦ 本批  **"TAB 下划线"整个删掉**（用户："那个 TAB 下划线不要了"）。替代物是一个**可空**的
//             `GameObject[] Tab选中高亮`：切栏时只 `SetActive` 当前那一项，**没接就什么都不做**
//             （不报错、不写颜色、不补建）。选中态长什么样（高亮块 / 描边 / 底图）全归预制体。
//
// 分工因此变成一条硬边界：
//   **预制体管**：结构 / 尺寸 / 锚点 / 字号 / 颜色 / 按钮过渡设置 / 静态文案（= 你在 Unity 里随便调）；
//   **本组件管**：只写"随状态变的东西" —— 身份行与等级的文字、经验格的进度数值、Tab 的标签文字与
//                选中高亮的显隐、栏目标题的切换，以及**切到哪一页让哪一页自己刷**。
//
// 本批口径② —— **"引用/绑定"参数全部摊到 Inspector 上**（用户："不用暴露参数来引用吗？"）：
//   · 结构引用：身份文本 / 等级文本 / 经验格父 / Tab按钮[3] / **Tab标签[3]** / **Tab选中高亮[3]** /
//     栏目标题 / 属性页 / 技能页 / 知识页 —— 留着并继续用。后两个数组都是后补的：`Tab标签` 让 Tab 上
//     那三个字跟着 `栏目表` 走（不必去预制体里手改字），`Tab选中高亮` 接替被删掉的下划线。
//   · `栏目表`（属性/技能/知识）也从代码里挪到 Inspector —— 填几个字就有几栏，不再写死。
//   ⚠ 引用留空 = 仍按名字兜底找回来（`自找()` 的路径与老版一致），名字也没有才补建空节点。
//   ⚠ 唯一的例外是 `Tab选中高亮`：它**可空** —— 没接、也没有同名节点 = 这个面板不做选中态，
//     既不补建也不报缺（"选中态可见"是加分项，不是开工前提）。
//
// ⚠ 由此带来的三条约定（改这个面板前先读）：
//   ① **要加/改外观，去 Unity 里改预制体**。少一个节点**不会**让面板整块消失：`找或建()` 会按名字
//      补建一个**不带样式的空节点**（挂在对的父下、名字对、组件对），并打一条列清名字的 LogWarning。
//      补建的节点没有任何外观 —— 想要好看，就要在预制体里预置同名节点（预置的那个优先被找到）。
//   ② **节点名就是契约**：`自找()` 按 `板子/身份条/身份文本`、`板子/{栏}/标签` 这类路径把引用兜底找回来。
//      重命名节点要么同步改 `自找()`，要么把 Inspector 引用位接上（接上就与名字无关了）。
//   ③ 预制体的整备脚本在 `.workbuddy/分析/角色面板-prefab整备.py`（补节点 / 改尺寸都走它）。
//
// 现成的节点树（Play 里对着 Hierarchy 核；缩进 = 父子）：
//   角色面板（满屏 —— `面板管理器` 建/摆面板的前提；预制体已烘成拉伸锚）
//     ├─ 点击遮罩            满屏透明 Image（吃掉板子外面的点击；垫在最底层）
//     ├─ 遮罩层              满屏 Image（65% 黑，不挡点击：只负责把画面压暗）
//     └─ 板子                1400×980（居中定尺；四边 2px 边框）
//          ├─ 身份条         内容底 + 下沿 2px 强调线
//          │    ├─ 身份文本   "幸存者 · 职业"
//          │    ├─ 等级文本   "Lv.N"
//          │    └─ 经验格父   下面铺着 N 个经验格（铺几格就有几段进度；格宽/间隔/颜色全归预制体）
//          ├─ 属性 / 技能 / 知识    三个 Tab（底图 + 标签；每个 Tab 下的 `选中高亮` 是可选的选中态子节点）
//          ├─ 分隔线         边框色 2px
//          └─ 内容区         内容底
//               ├─ 栏目标题  随 Tab 变
//               ├─ 标题下划线
//               └─ 属性页 / 技能页 / 知识页   三个子面板（切栏 = SetActive）
//
// 开合沿用 `面板管理器` 的既有约定，不自造一套：
//   · 打开 = `打开角色面板事件`（或 C 键）→ `面板管理器.显示(角色)`；
//   · 关闭 = HUD 上那颗统一关闭按钮 → 调本面板的 `回退()`；
//   · 项目**没有全局 Esc 回退键**，所以这里也不绑 Esc；
//   · 板外那层透明图**只吃点击、不关面板**（关闭只有一个入口 = HUD 那颗按钮）。
public sealed class 角色面板 : 面板基类
{
    // 三个栏目（顺序 = 顶部 Tab 从左到右，也就是下面数组与 `子面板` 的下标 0..2）。
    // ★ 本批改成**可编辑字段**（原来写死在代码里）：填几个字就有几栏 —— 用户口径"不要写死在代码里"。
    //   这三个字同时是**节点名**（`板子/属性`、`板子/属性/标签`、`板子/属性/选中高亮`…）→ 改字要连预制体一起改，
    //   或者干脆把下面那些引用位接上（接上之后节点随便改名）。
    //   ⚠ 表清空（Inspector 里把 Size 调成 0）会让 `栏目表[当前栏]` 越界 → `备栏目表()` 会退回默认三栏并报一声。
    [SerializeField] private string[] 栏目表 = { "属性", "技能", "知识" };

    // ================= Inspector：结构引用（预制体里已全部接好） =================
    [Header("结构引用（预制体已接好；留空 = 按名字兜底找回来，见 自找）")]
    [SerializeField] private TMP_Text 身份文本;           // "幸存者 · 职业"（板子/身份条/身份文本）
    [SerializeField] private TMP_Text 等级文本;           // "Lv.N"（板子/身份条/等级文本）
    [SerializeField] private RectTransform 经验格父;       // 板子/身份条/经验格父：子节点就是那一排经验格
    [SerializeField] private Button[] Tab按钮 = new Button[3];      // 板子/{栏}（下标 = 栏目顺序）
    [SerializeField] private TMP_Text[] Tab标签 = new TMP_Text[3];  // 板子/{栏}/标签（标签文字 = 栏目表[i]，由代码写）
    // ★ 选中态 = **可空**引用（口径②）：切栏时只动当前那一项的 `SetActive`，不写任何颜色。
    //   三个位全空（也没同名节点）= 这个面板就不做选中态：不报错、不补建、不写颜色。
    [SerializeField] private GameObject[] Tab选中高亮 = new GameObject[3];  // 板子/{栏}/选中高亮（预制体里接，默认留空）
    [SerializeField] private TMP_Text 栏目标题;           // 板子/内容区/栏目标题（随 Tab 变）
    [SerializeField] private 属性子面板 属性页;             // 板子/内容区/属性页
    [SerializeField] private 技能子面板 技能页;             // 板子/内容区/技能页
    [SerializeField] private 知识子面板 知识页;             // 板子/内容区/知识页

    private readonly List<RectTransform> 经验格 = new List<RectTransform>();   // 经验格父 的子节点（第一次刷新时收一次）
    private readonly List<string> 补建的 = new List<string>();                 // 预制体里没有、由代码补建的节点/组件
    private int 当前栏;
    private bool 报过缺;

    void Awake()
    {
        备栏目表();
        备数组(ref Tab按钮);
        备数组(ref Tab标签);
        备数组(ref Tab选中高亮);
        自找();
        接Tab();
        订阅事件();
        切换栏(0);   // 建完先落在第 0 栏（此时还没开档 → 三页各自刷新会自己 return，不报错）
    }

    // ================= 事件订阅 =================
    // 刷新口径：属性变化（加点/装备变化都会发）+ 生命/精力变化（身份条与属性页的生存段）+ 经验变化（身份条的经验格）。
    private void 订阅事件()
    {
        // `已注册` 再取：`ServiceRegistry.Get` 在未注册时是**抛异常**的，而本组件的 Awake 有可能早于
        //   面板管理器.Awake（脚本执行顺序不保证）→ 直接取会让整个组件炸在这里。
        var 事件 = ServiceRegistry.已注册<EventBus>() ? ServiceRegistry.Get<EventBus>() : null;
        if (事件 == null) return;
        事件.订阅<属性变化事件>(_ => 刷新(null));
        事件.订阅<经验变化事件>(_ => 刷新(null));
        事件.订阅<生命变化事件>(_ => 刷新(null));
        事件.订阅<精力变化事件>(_ => 刷新(null));
    }

    protected override void 刷新(object 上下文)
    {
        var 玩家 = 当前档案();
        if (玩家 == null) return;
        面板基类.设文本(身份文本, 身份行(玩家));
        面板基类.设文本(等级文本, $"Lv.{玩家.等级}");
        收经验格();
        刷经验格(玩家);
        // ★ **只刷当前那一页**：另外两页在 `切换栏` 时会被 SetActive(true) → 各自的 `OnEnable`
        //   会刷一次，所以它们不会"看到旧数据"。原来这里三页各刷一遍，而每次 生命/精力 变化都会走到这
        //   —— 翻三倍的无用功，且另两页的刷新全打在隐藏节点上（白算）。
        刷当前页();
    }

    // ================= 引用兜底：按名字找回来；找不到就按名字**补建一个不带样式的空节点** =================
    // 预制体里已经接好了，这里是"手搭一半 / 场景里现搭 / 改了引用位"的兜底。
    // ⚠ 名字写死在这（与 `栏目表` 同源）—— 重命名节点要么同步改这里，要么把引用位接上。
    private void 自找()
    {
        var 根 = transform as RectTransform;
        var 板子 = 找或建(根, "板子");
        var 身份条 = 找或建(板子, "身份条");
        var 内容区 = 找或建(板子, "内容区");

        身份文本 = 取或建文本(身份文本, 身份条, "身份文本");
        等级文本 = 取或建文本(等级文本, 身份条, "等级文本");
        经验格父 = 取或建矩形(经验格父, 身份条, "经验格父");
        栏目标题 = 取或建文本(栏目标题, 内容区, "栏目标题");

        for (int i = 0; i < 栏目表.Length; i++)
        {
            // 只有"这一栏确实缺按钮/标签"时才按名字去找（找不到才补建）—— 引用位都接好的时候，
            //   不该凭空多出 `板子/属性` 这种空节点（补建是兜底，不是常规路径）。
            if (取(Tab按钮, i) == null || 取(Tab标签, i) == null)
            {
                var 栏 = 找或建(板子, 栏目表[i]);
                if (取(Tab按钮, i) == null) Tab按钮[i] = 取或建件<Button>(栏, null);
                // Tab 上那三个字由**栏目表**写（不再只能去预制体里改）—— 这里只填字，字号/颜色仍归预制体
                if (取(Tab标签, i) == null) Tab标签[i] = 取或建文本(null, 栏, "标签");
            }
            面板基类.设文本(取(Tab标签, i), 栏目表[i]);
            // 选中高亮**只找不建**（可空引用）：凭空补建一个空节点既看不见，又会把"没接"伪装成"接了"。
            //   找不到就让它留空 → 切栏时那一项自然什么都不做。
            if (取(Tab选中高亮, i) == null && 板子 != null)
            {
                var 高亮 = 板子.Find(栏目表[i] + "/选中高亮");
                if (高亮 != null) Tab选中高亮[i] = 高亮.gameObject;
            }
        }

        if (属性页 == null) 属性页 = 取或建件<属性子面板>(找或建(内容区, "属性页"), null);
        if (技能页 == null) 技能页 = 取或建件<技能子面板>(找或建(内容区, "技能页"), null);
        if (知识页 == null) 知识页 = 取或建件<知识子面板>(找或建(内容区, "知识页"), null);
        // 三个子面板的"内容区"注入（预制体里本来就接好了 → 这句是空操作；只在兜底补建时才有用）
        if (属性页 != null) 属性页.设内容区(内容区);
        if (技能页 != null) 技能页.设内容区(内容区);
        if (知识页 != null) 知识页.设内容区(内容区);

        报缺引用();
    }

    // 找子节点；**预制体里没有就补建一个**（挂在对的父下、名字对、不带任何样式）。
    // 为什么要补建而不是只报错：用户明令"不许要求我手搭" —— 少一个节点不该让整块面板消失。
    // 补建的节点**没有外观**：要好看请在预制体里预置同名节点（预置的那个优先被找到）。
    private RectTransform 找或建(RectTransform 父, string 名)
    {
        if (父 == null) return null;
        var 子 = 父.Find(名) as RectTransform;
        if (子 != null) return 子;
        var 物 = new GameObject(名, typeof(RectTransform));
        物.transform.SetParent(父, false);
        补建(名);
        return (RectTransform)物.transform;
    }

    private RectTransform 取或建矩形(RectTransform 现成, RectTransform 父, string 名)
        => 现成 != null ? 现成 : 找或建(父, 名);

    // 取 父 下名为 名 的子节点上的组件 T；节点没有就补建节点，组件没有就 AddComponent。
    // 名 传 null = 直接用 父 自己（页面组件那种"节点即组件"的情况）。
    private T 取或建件<T>(RectTransform 父, string 名) where T : Component
    {
        var 节点 = 名 == null ? 父 : 找或建(父, 名);
        if (节点 == null) return null;
        var 件 = 节点.GetComponent<T>();
        if (件 == null)
        {
            件 = 节点.gameObject.AddComponent<T>();
            补建(节点.name + " + " + typeof(T).Name);
        }
        return 件;
    }

    private TMP_Text 取或建文本(TMP_Text 现成, RectTransform 父, string 名)
    {
        if (现成 != null) return 现成;
        var 节点 = 找或建(父, 名);
        if (节点 == null) return null;
        var 文本 = 节点.GetComponent<TMP_Text>();
        if (文本 == null)
        {
            文本 = 节点.gameObject.AddComponent<TextMeshProUGUI>();
            补建(名 + " + TextMeshProUGUI");
        }
        return 文本;
    }

    private void 补建(string 名)
    {
        if (!补建的.Contains(名)) 补建的.Add(名);
    }

    // 缺引用**要出声**：以前这条路是静默的（Inspector 少接一个位 → 表现只是"面板上少一块"，
    //   一条日志都没有，只能靠肉眼比对）。只在第一次报，免得每帧刷屏。
    private void 报缺引用()
    {
        if (报过缺) return;
        报过缺 = true;
        if (补建的.Count > 0)
            Debug.LogWarning("[角色面板] 预制体里没有这些节点/组件，本组件已按名字**补建**（不带任何样式）：" +
                             string.Join("、", 补建的) +
                             "。外观请去 `Assets/Resources/Prefab/角色面板.prefab` 里预置同名节点（预置的优先被找到）。");
        if (经验格父 != null && 经验格父.childCount == 0)
            Debug.LogWarning("[角色面板] `板子/身份条/经验格父` 下面没有经验格 —— 经验条画不出来。" +
                             "请在预制体里铺上格子，并把每格的 Image 设成 `Type = Filled`（本组件只写 fillAmount）。");
        var 缺 = new List<string>();
        if (身份文本 == null) 缺.Add("身份文本");
        if (等级文本 == null) 缺.Add("等级文本");
        if (经验格父 == null) 缺.Add("经验格父");
        if (栏目标题 == null) 缺.Add("栏目标题");
        for (int i = 0; i < 栏目表.Length; i++)
        {
            if (取(Tab按钮, i) == null) 缺.Add($"Tab按钮[{i}]({栏目表[i]})");
            if (取(Tab标签, i) == null) 缺.Add($"Tab标签[{i}]({栏目表[i]})");
            // `Tab选中高亮` **不进这份名单**：它是可空引用，没接是本面板允许的一种配置（只是没有选中态）。
        }
        if (属性页 == null) 缺.Add("属性页");
        if (技能页 == null) 缺.Add("技能页");
        if (知识页 == null) 缺.Add("知识页");
        if (缺.Count == 0) return;
        Debug.LogError("[角色面板] 结构不完整，缺引用：" + string.Join("、", 缺) +
                       "。请把这些节点补进 `Assets/Resources/Prefab/角色面板.prefab`" +
                       "（整备脚本：`.workbuddy/分析/角色面板-prefab整备.py`），或把 Inspector 引用位接上。");
    }

    // ================= Tab =================
    // 只接点击：**底色 / 悬停 / 按下 / 过渡一律沿用预制体里调好的按钮设置**（在 Unity 里改得动，也能整批换观感）。
    private void 接Tab()
    {
        for (int i = 0; i < 栏目表.Length; i++)
        {
            int 序 = i;   // 闭包捕获：循环变量必须另存一份，否则三个按钮全会切到最后一栏
            var 钮 = 取(Tab按钮, i);
            if (钮 == null) continue;
            // 先清旧监听：本组件接管这三个 Tab 的点击（`Awake` 只跑一次，但"场景里被重新激活"时可能重进）
            钮.onClick.RemoveAllListeners();
            钮.onClick.AddListener(() => 切换栏(序));
        }
    }

    // ================= 切栏 =================
    // 切栏 = 改 Tab 选中态（**只切选中高亮的显隐**，不碰颜色/字号）+ 换栏目标题 + 三个子面板显隐。
    // **不在这里刷页**：被点亮的那一页会收到 `OnEnable` → 自己刷一次；而"已经亮着的那一页"说明数据没变过
    //   （真变了会走本面板的事件订阅 → `刷新` → 只刷当前页）。两条路各管一头，不重叠。
    private void 切换栏(int 序)
    {
        当前栏 = Mathf.Clamp(序, 0, 栏目表.Length - 1);
        for (int i = 0; i < 栏目表.Length; i++)
        {
            // 选中态 = 结构：只有当前那一栏的高亮亮着。**没接的位直接跳过** —— 不报错、也不写颜色。
            var 高亮 = 取(Tab选中高亮, i);
            if (高亮 != null && 高亮.activeSelf != (i == 当前栏)) 高亮.SetActive(i == 当前栏);
        }
        面板基类.设文本(栏目标题, 栏目表[当前栏]);
        只亮(属性页, 当前栏 == 0);
        只亮(技能页, 当前栏 == 1);
        只亮(知识页, 当前栏 == 2);
    }

    // 只刷"当前亮着的那一栏"的子面板。
    // ⚠ 三个子面板是 **`MonoBehaviour`（不是 `面板基类`）** —— 它们没有 `回退/取消文本` 那套出口协议，
    //   只是"内容块"，所以这里**不能**抽成"返回 面板基类 再调 `.刷新()`"（那样类型对不上、编不过）。
    //   逐个点名调最直白：加第 4 栏时，这里和 `切换栏` 各补一行。
    private void 刷当前页()
    {
        switch (当前栏)
        {
            case 0: if (属性页 != null) 属性页.刷新(); break;
            case 1: if (技能页 != null) 技能页.刷新(); break;
            case 2: if (知识页 != null) 知识页.刷新(); break;
        }
    }

    private static void 只亮(Component 目标, bool 亮)
    {
        if (目标 != null && 目标.gameObject.activeSelf != 亮) 目标.gameObject.SetActive(亮);
    }

    // ================= 顶部身份条 =================
    // "幸存者 · 职业"。职业名从数据表取（`职业数据.名称`）；取不到就退回职业标识，
    // 连标识都空（还没开档）就只显示"幸存者"。
    private static string 身份行(玩家档案 玩家)
    {
        string 职业 = "";
        if (!string.IsNullOrEmpty(玩家.职业))
        {
            var 数据 = ServiceRegistry.已注册<DataService>() ? ServiceRegistry.Get<DataService>() : null;
            职业 = 数据 != null && 数据.职业 != null && 数据.职业.TryGetValue(玩家.职业, out var 定义) && 定义 != null
                   && !string.IsNullOrEmpty(定义.名称)
                ? 定义.名称
                : 玩家.职业;
        }
        return string.IsNullOrEmpty(职业) ? "幸存者" : $"幸存者 · {职业}";
    }

    // 经验条：**一格一格地推进**。格子**由预制体铺**（`板子/身份条/经验格父` 下的那排 `经验格`），
    //   格数/格宽/间隔/颜色全归预制体；本组件只写**进度数值**这一件事：`Image.fillAmount`。
    //   ⚠ 所以每格的 Image 必须在预制体里设成 `Type = Filled`（否则写 fillAmount 看不出变化 ——
    //     "把格子做成一格一格的亮/暗"那种做法要写颜色或额外子物体，本组件按口径一律不做）。
    //   ⚠ 代码**不写任何颜色/尺寸**，也不补建格子。
    private void 收经验格()
    {
        if (经验格.Count > 0 || 经验格父 == null) return;
        for (int i = 0; i < 经验格父.childCount; i++)
        {
            var 格 = 经验格父.GetChild(i) as RectTransform;
            if (格 != null) 经验格.Add(格);
        }
    }

    private void 刷经验格(玩家档案 玩家)
    {
        int 总格 = 经验格.Count;
        if (总格 == 0) return;   // 预制体还没铺格子 → 什么都不画（缺引用已经报过了）
        int 需要 = Mathf.Max(1, 玩家.升级所需经验);
        float 进度 = Mathf.Clamp01(玩家.经验 / (float)需要) * 总格;   // 单位 = "格"
        for (int i = 0; i < 总格; i++)
        {
            var 格 = 经验格[i];
            if (格 == null) continue;
            var 图 = 格.GetComponent<Image>();   // 每格自己就是一小段进度条（Image Type = Filled）
            if (图 != null) 图.fillAmount = Mathf.Clamp01(进度 - i);   // 只写数值：颜色/尺寸仍归预制体
        }
    }

    // ================= 出口协议（沿用 面板管理器 / HUD 关闭按钮 的既有约定） =================

    // 全局关闭按钮 = 返回上一面板（无上一面板则不可取消）——与原实现一致
    public override bool 回退()
    {
        if (面板管理器.实例?.上一个面板 == null) return false;
        面板管理器.实例.返回上一面板();
        return true;
    }

    // HUD 关闭按钮按这个文案选图标（"关闭" = 把这一页收掉，不是"离开某个地方"）
    public override string 取消文本 => "关闭";

    // ================= 小工具 =================

    // 取按钮/高亮数组的第 序 个（越界或没接都返回 null → 调用方一句判空即可）
    private static T 取<T>(T[] 数组, int 序) where T : class
        => 数组 != null && 序 >= 0 && 序 < 数组.Length ? 数组[序] : null;

    // `栏目表` 现在是**可编辑字段** → 空表会让 `栏目表[当前栏]` 越界（面板直接炸）。空表退回默认三栏并报一声。
    private void 备栏目表()
    {
        if (栏目表 != null && 栏目表.Length > 0) return;
        栏目表 = new[] { "属性", "技能", "知识" };
        Debug.LogWarning("[角色面板] `栏目表` 是空的 —— 已退回默认三栏（属性/技能/知识）。要改栏目请在 Inspector 里填。");
    }

    // 旧场景可能把数组存成空数组/短数组 → 先补到 栏目表.Length 个（否则按下标赋值就崩）。
    // ⚠ 短数组里**已有的引用要带过来**：直接 `数组 = new T[n]` 会把"只拖了两个 Tab"的那两个引用丢掉。
    private void 备数组<T>(ref T[] 数组) where T : class
    {
        if (数组 != null && 数组.Length == 栏目表.Length) return;
        var 旧 = 数组;
        var 新 = new T[栏目表.Length];
        if (旧 != null)
            for (int i = 0; i < 旧.Length && i < 新.Length; i++) 新[i] = 旧[i];
        数组 = 新;
    }

    // 取当前档案。**先问"注册了没有"再取**：`ServiceRegistry.Get` 未注册时是抛异常的。
    private static 玩家档案 当前档案()
        => ServiceRegistry.已注册<PlayerService>() ? ServiceRegistry.Get<PlayerService>()?.档案 : null;
}
