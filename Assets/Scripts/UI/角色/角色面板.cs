using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 角色面板：全局面板。一屏**三栏** = 一块浮在安全屋画面上的"幸存者档案板"
//   （板内：顶部身份条 + 顶部横排三个大字 Tab + 内容区；三个栏页的内容各自在子面板里）。
//
// ★ 本文件是**第四次改写**，三代口径别再来回翻 git：
//     ① 刀78：代码自建 + "不铺满屏 / 组件放大 / 暖调近黑 + 唯一暗锈红" —— 用户认了这个观感；
//     ② 刀79：整批拆掉自建，改成"用户自己搭 + Inspector 接线"；用户随后改主意；
//     ③ 刀80：自建回来，但 12 个结构引用保留为可选覆盖；
//     ④ 本批（刀81）：**"皮肤"这个概念整个删掉**（`角色面板皮肤.cs` 连 meta 一起没了）+
//        四栏压成**三栏**（属性 / 技能 / 知识 —— 天赋内容太少，用户当场拍板并进 属性页 当第 4 个小段）。
//        → 配色/字号/间距不再有一个共用参数类，而是**内联在本面板自己的 [SerializeField] 字段里**
//          （子面板各自内联自己那一份；同一个颜色在几个文件里各写一遍是有意的取舍 ——
//           换来的是"没有皮肤这个中间层、每个面板自己说了算"，用户明确不要抽象层）。
//
// 本文件只管三件事：**壳（遮罩/板子/身份条/Tab/分隔线）+ 内容区 + 切栏分发**。
//   三个栏页的真实内容全部在子面板里（`属性子面板` / `技能子面板` / `知识子面板`），
//   它们**各自 MonoBehaviour、互不引用**；本面板与它们之间只有一个约定：
//   **`内容区` 由本面板注入（`设内容区`），子面板拿到之后自己建/刷自己的节点。**
//
// 建出来的节点树（Play 里对着 Hierarchy 核；缩进 = 父子）：
//   角色面板（本组件，满屏 —— `面板管理器` 建/摆面板的前提）
//     ├─ 点击遮罩            满屏透明 Image（raycastTarget = true：**吃掉板子外面的点击**；垫在最底层）
//     ├─ 遮罩层              满屏 Image（65% 黑，raycastTarget = false：只负责把画面压暗）→ 遮罩
//     └─ 板子                Image（板底；居中定尺）→ 板子
//          ├─ 上/下/左/右边框  Image ×4（边框色，2px，实心矩形不是 Outline）
//          ├─ 身份条          Image（内容底）→ 身份条
//          │    ├─ 身份文本    TMP（"幸存者 · 职业"）→ 身份文本
//          │    ├─ 等级文本    TMP（"Lv.N"）→ 等级文本
//          │    ├─ 经验格父    Rect → 经验格父（下面铺 10 个 经验格，10×10、间隔 2）
//          │    └─ 身份条强调线 Image（强调 2px：**全屏第一处强调色**，整块板子靠它立住）
//          ├─ 属性/技能/知识 Button（底图 + 标签 TMP + 下划线 Image）→ Tab按钮/Tab标签/Tab下划线
//          ├─ 分隔线          Image（边框色 2px，左右缩 页边距）→ 分隔线
//          └─ 内容区          Image（内容底）→ 内容区
//               ├─ 栏目标题    TMP（随 Tab 变）→ 栏目标题
//               ├─ 标题下划线  Image（强调 2px，宽 = 标题文字宽）
//               └─ 三个子面板   （`属性子面板` / `技能子面板` / `知识子面板` 各挂一个子物体；切栏 = SetActive）
//
// 口径（外观）：
//   · **不铺满屏**：板子 = min(屏宽×0.84, 1180) × min(屏高×0.88, 760)，屏幕居中；四边 2px 边框；
//     板外 65% 黑遮罩（不挡点击）+ 满屏透明图（挡点击）；
//   · 板内留白：页边距 40 用在外圈，身份条高 88，段距 20 用在"段与段之间"；
//   · Tab：三个横排大字（24），间距 = 段距；选中 = 正文色 + 2px 强调下划线，未选 = 次要色 + 下划线隐藏；
//     悬停只换底（悬停底）；**无动画**（fadeDuration = 0）、无缩放反馈；
//   · 行：行内文本（名 + 值同一行，值紧跟名）+ 靠留白分层 —— **没有**左侧色条、**没有**数值右对齐到天边、**行间不画线**；
//   · 没有圆角 / 阴影 / 发光 / 渐变 / emoji / 入场动画 / 琥珀色。
//
// 开合沿用 `面板管理器` 的既有约定，不自造一套：
//   · 打开 = `打开角色面板事件`（或 C 键）→ `面板管理器.显示(角色)`；
//   · 关闭 = HUD 上那颗统一关闭按钮 → 调本面板的 `回退()`；
//   · 项目**没有全局 Esc 回退键**，所以这里也不绑 Esc；
//   · 板外那层透明图**只吃点击、不关面板**（关闭只有一个入口 = HUD 那颗按钮）。
public sealed class 角色面板 : 面板基类
{
    // 三个栏目（顺序 = 顶部 Tab 从左到右，也就是下面三个数组与 `子面板` 的下标 0..2）。
    // **没有"装备"栏**（装备归 `持有面板`，这里不重复一份）；
    // **没有"天赋"栏**（天赋内容太少，用户拍板并进 属性页 当第 4 个小段，见 `属性子面板`）。
    // Tab 标签文字与栏目标题都由代码按这张表写 → 想改字改这里，**别在 Inspector 里改**（切栏时会被覆盖）。
    private static readonly string[] 栏目表 = { "属性", "技能", "知识" };

    // ================= Inspector：结构引用（**可选覆盖**） =================
    // 口径：**非空 = 用你拖的那个物体；为空 = 自动建一个并回填到这个字段**（Play 里在 Inspector 看得到）。
    //   所以"一个都不接"是默认状态、也是最常用的状态（`面板管理器` 的零接线兜底就走这条路径）。
    // 一条要守住的约定：**摆位/配色永远由本组件按下面那些内联字段写**（拖进来的物体是"换一个物体承载"，不是"位置归你"），
    //   否则同一套口径会因为接没接线裂成两套；你手调的位置会在下一次 顶层尺寸变化（改分辨率/切栏）时被覆盖。
    // 反过来：本组件**只销毁自己建的节点**（`自建节点` 这份账上记着的那些）—— 你的物体一个都不会被干掉。
    [Header("结构引用（可选覆盖：拖了 = 用你的；留空 = 自动建并回填）")]
    [SerializeField] private Image 遮罩;                 // 满屏暗色层（颜色 = 遮罩色；raycastTarget 由本组件写成 false，吃点击的是另一张透明图）
    [SerializeField] private RectTransform 板子;          // 档案板根（板底 Image 挂在它自己身上）
    [SerializeField] private RectTransform 身份条;        // 板内顶部那条（底 = 内容底色）
    [SerializeField] private TMP_Text 身份文本;          // "幸存者 · 职业"
    [SerializeField] private TMP_Text 等级文本;          // "Lv.N"
    [SerializeField] private RectTransform 经验格父;      // 经验格的父节点：留空则自动建；自建时格子由本组件铺（固定 10 格）
    [SerializeField] private Button[] Tab按钮 = new Button[3];     // 顶部三个 Tab 的按钮（下标 = 栏目顺序）
    [SerializeField] private TMP_Text[] Tab标签 = new TMP_Text[3]; // 三个 Tab 的文字（选中/未选中就改它的颜色）
    [SerializeField] private Image[] Tab下划线 = new Image[3];      // 三个 Tab 的下划线（只显示选中那条）
    [SerializeField] private Image 分隔线;               // Tab 行下方那条 2px 线（边框色）
    [SerializeField] private TMP_Text 栏目标题;          // 内容区顶部的"属性/技能/知识"（随 Tab 变）
    [SerializeField] private RectTransform 内容区;        // 分隔线以下的整块（底 = 内容底色；也是三个子面板的父节点）

    // 三个子面板：**可选覆盖** —— 拖了 = 用你挂的那个；留空 = 在 `内容区` 下自动建子物体挂组件并回填。
    // 本组件只做两件事：把 `内容区` 注入进去（`设内容区`）+ 切栏时 SetActive 对应的那个。
    // 子面板内部怎么排、有哪些行，本组件一概不管（互不引用的意思就是：这里连它们的字段都读不到）。
    [Header("子面板（可选覆盖：拖了 = 用你的；留空 = 在 内容区 下自动建）")]
    [SerializeField] private 属性子面板 属性页;
    [SerializeField] private 技能子面板 技能页;
    [SerializeField] private 知识子面板 知识页;

    // 本组件建过的节点**账本**（不是给你调的字段，是"哪些节点是我建的"这份账）：
    //   为什么必须有它：`重建布局()` / `Awake` 要"清掉自己建的那一套、绝不动你拖进来的"，
    //   而上面那些引用位里分不出谁建的 —— 唯一可靠的办法就是建的时候就记下来。
    //   为什么序列化：预制体烘好、读档之后这份账还在。不序列化的话，装了预制体的场景里 `Awake` 会再建一套
    //   → 两套边框、两套内容（第一次踩的就是这个坑）。
    [SerializeField] private List<GameObject> 自建节点 = new List<GameObject>();

    // ================= Inspector：本面板自己的颜色/字号/间距（内联，没有"皮肤"这个中间层） =================
    [Header("颜色（十六进制见注释；强调色只有一个）")]
    [SerializeField] private Color 板底色 = new Color(0.0706f, 0.0627f, 0.0549f, 1f);   // #12100E 板子最外层的底
    [SerializeField] private Color 内容底色 = new Color(0.1020f, 0.0941f, 0.0824f, 1f); // #1A1815 身份条/内容区的底（比板底亮一档，边框线才分得开）
    [SerializeField] private Color 边框色 = new Color(0.2275f, 0.2118f, 0.1882f, 1f);   // #3A3630 四边 2px 边框 + Tab 行下方的分隔线
    [SerializeField] private Color 正文色 = new Color(0.8941f, 0.8745f, 0.8392f, 1f);   // #E4DFD6 主文字 / 选中的 Tab
    [SerializeField] private Color 次要色 = new Color(0.5412f, 0.5137f, 0.4706f, 1f);   // #8A8378 未选中的 Tab / 等级文本
    [SerializeField] private Color 强调色 = new Color(0.7059f, 0.3333f, 0.2353f, 1f);   // #B4553C 全屏唯一强调色：选中 Tab 的下划线 + 已点亮的经验格
    [SerializeField] private Color 悬停底 = new Color(0.1490f, 0.1333f, 0.1255f, 1f);   // #262220 Tab 悬停/按下时的底
    [SerializeField] private Color 禁选底色 = new Color(0.2275f, 0.2118f, 0.1882f, 1f); // #3A3630 未点亮的经验格（= 边框色，空槽也看得见）
    [SerializeField] private Color 遮罩色 = new Color(0f, 0f, 0f, 0.65f);               // #000000 α0.65 压在安全屋画面上的暗色层

    [Header("字号 / 间距 / 尺寸")]
    [SerializeField] private int 字号_标题 = 30;      // 身份行"幸存者 · 职业"
    [SerializeField] private int 字号_栏目标题 = 24;  // 内容区顶部栏目标题
    [SerializeField] private int 字号_Tab = 24;       // 顶部三个大字 Tab
    [SerializeField] private int 字号_等级 = 20;      // 身份条右边 "Lv.N"
    [SerializeField] private float 页边距 = 40f;      // 板内四边留白
    [SerializeField] private float 段距 = 20f;        // 段与段之间（身份条→Tab、Tab→分隔线、Tab 与 Tab 之间）
    [SerializeField] private float 身份条高 = 88f;
    [SerializeField] private float 边框粗 = 2f;
    [SerializeField] private float 板宽比例 = 0.84f, 板宽上限 = 1180f;
    [SerializeField] private float 板高比例 = 0.88f, 板高上限 = 760f;

    [Header("经验格（固定 10 格 · 10×10 · 间隔 2）")]
    [SerializeField] private int 经验格数 = 10;
    [SerializeField] private float 进度格宽 = 10f, 进度格高 = 10f, 进度格间隔 = 2f;

    // ================= 本组件自建的节点（不在覆盖清单里，不给 Inspector 位） =================
    private RectTransform 点击遮罩;      // 满屏透明 + raycastTarget = true：板外点击的兜底
    private Image 身份条强调线;           // 身份条下沿那条 2px 强调线
    private Image 标题下划线;             // 栏目标题下那条 2px 强调短线
    private readonly List<Image> 经验格 = new List<Image>();   // 经验条的格子（从左到右，按比例点亮前 N 格）
    private bool 经验格自建;   // 经验格父 是本组件建的（→ 格子由本组件铺）；你拖进来的那个不铺、子物体一个不动
    private int 当前栏;
    private bool 已建;         // 节点树建完前不响应"根矩形尺寸变化"（那时连板子都还没有）

    void Awake()
    {
        // 旧场景可能把这三个数组存成空数组/短数组 → 按下标回填会越界崩，先补齐长度。
        备数组(ref Tab按钮);
        备数组(ref Tab标签);
        备数组(ref Tab下划线);

        建节点树();
        接Tab();

        // 刷新口径：属性变化（加点/装备变化都会发）+ 生命/精力变化（身份条与属性页的生存段）+ 经验变化（身份条的经验格）。
        // 三个栏页的内容各自订阅自己关心的事件，本面板只负责"框"与顶部那一条。
        // `已注册` 再取：`ServiceRegistry.Get` 在未注册时是**抛异常**的，而本组件的 Awake 有可能早于 面板管理器.Awake
        //   （脚本执行顺序不保证）→ 直接取会让整个组件炸在这里。
        var 事件 = ServiceRegistry.已注册<EventBus>() ? ServiceRegistry.Get<EventBus>() : null;
        if (事件 != null)
        {
            事件.订阅<属性变化事件>(_ => 刷新(null));
            事件.订阅<经验变化事件>(_ => 刷新(null));
            事件.订阅<生命变化事件>(_ => 刷新(null));
            事件.订阅<精力变化事件>(_ => 刷新(null));
        }
        切换栏(0);
        刷新(null);   // 先填一次：不依赖"上一次变化事件"（面板可能是在档案早已改变之后才第一次打开）
    }

    // ================= 公开入口：重建布局（编辑器里与运行时都能调，不依赖 Play 模式） =================
    // 给"一键生成 .prefab"那类编辑器脚本用（`末日/角色/一键生成面板预制体` 只认这个入口）：
    //   清掉本组件建过的节点 → 重新建整棵树 → 把引用回填好 → 强制算一次文字宽再重摆 Tab/标题/经验格。
    // **幂等**：连调两次不会堆出两套。保证方式 = 本组件建的每个节点都记在 `自建节点` 里，
    //   建树的第一步（清掉自建）先把账本上的节点全部销毁，再按"引用位里还剩什么"重建 ——
    //   剩下的只可能是你手动拖进来的，那些一个都不动。
    // 编辑器里调用需要的前置条件（三条都会影响"建出来好不好看"，但一条都不影响"建不建得出来"）：
    //   ① 本组件所在物体**处于激活状态**：TMP 的 `Awake` 没跑过就量不出文字宽（Tab/标题会退回兜底宽度）；
    //   ② **最好挂在 Canvas 下**：量不到屏宽时板子退回"板宽上限 × 板高上限"（1180×760），经验格也按那个宽度铺；
    //      运行时第一次布局回调（`OnRectTransformDimensionsChange`）会按真实屏幕改回来；
    //   ③ 编辑器里没跑 `GameBootstrap.装配()` → 取不到 `PlayerService`，身份行/各栏内容为空（**不报错**）；
    //      Play 里 `Awake` 会刷一遍 → 预制体不需要手填任何数字、也不需要存数据。
    public void 重建布局()
    {
        备数组(ref Tab按钮);
        备数组(ref Tab标签);
        备数组(ref Tab下划线);

        建节点树();      // 内部第一步就是 清掉自建()：自己建的先销毁，你拖进来的原样复用
        接Tab();
        强制排版();      // 编辑器里没有第二帧回调 → TMP 的 preferredWidth 得在这里主动算一次
        切换栏(当前栏);
        刷新(null);
    }

    // 强制让 TMP 算一遍文字宽（`preferredWidth` 平时要等布局系统调用才算得出来，刚建出来的文本那一刻是 0），
    // 再按算出来的宽度重摆 Tab 行与标题下划线 —— 不补这一步，编辑器里建出来的下划线会按兜底宽度摆。
    private void 强制排版()
    {
        for (int i = 0; i < 栏目表.Length; i++)
            if (标签(i) != null) 标签(i).ForceMeshUpdate(true);   // ignoreActiveState：物体没激活也算（否则直接 no-op）
        if (栏目标题 != null) 栏目标题.ForceMeshUpdate(true);
        摆Tab();
        摆标题();
    }

    protected override void 刷新(object 上下文)
    {
        var 玩家 = 当前档案();
        if (玩家 == null) return;
        面板基类.设文本(身份文本, 身份行(玩家));
        面板基类.设文本(等级文本, $"Lv.{玩家.等级}");
        建经验格();      // 板宽变了/第一次进来 → 需要时补格子
        刷经验格(玩家);
        // 再让三个栏页各自刷一遍：它们订阅不到"本面板订阅的那几条事件"里的一部分
        //   （最典型的是 书籍服务 加知识经验 —— 那条路**不发任何事件**，见 `成长管理器.加知识经验`）。
        //   不在这里转一下，就会出现"面板开着读完一本书、知识页的等级/进度还是旧的，切一下栏才变"。
        //   代价：三条栏页各刷一次。都是原地改文本，量级与"切一次栏"相同（见各自 刷新 的注释）。
        刷三个栏页();
    }

    // 让三个栏页各自刷新。判空安全：Tab 只接了俩、子面板还没搭 —— 都属于"搭一半"，不许因此报错。
    private void 刷三个栏页()
    {
        属性页?.刷新();
        技能页?.刷新();
        知识页?.刷新();
    }

    // 取当前档案。**先问"注册了没有"再取**：`ServiceRegistry.Get` 未注册时是抛异常的，
    //   而 `重建布局()` 是要在编辑器里（那时没跑 `GameBootstrap.装配()`）调的 —— 那条路径上不许抛。
    private static 玩家档案 当前档案()
        => ServiceRegistry.已注册<PlayerService>() ? ServiceRegistry.Get<PlayerService>()?.档案 : null;

    // ================= 建节点树（缺哪块建哪块；建出来的回填到对应的 Inspector 字段） =================

    private void 建节点树()
    {
        // 第一步：把本组件上一次建的那一套收回去（自己建的先销毁，你拖进来的原样复用 —— 见 清掉自建）。
        // 这一步让"建树"变成**幂等**的：预制体里已经烘了一套、或者 重建布局() 连调两次，都不会堆出两套边框/两套内容。
        清掉自建();

        var 根 = (RectTransform)transform;

        // ① 满屏、吃点击的透明图。**必须垫在最底下**（`SetAsFirstSibling`）：
        //    它 raycastTarget = true，若盖在板子上面，板里所有按钮的点击都会被它先吃掉（"点了完全没反应"的经典坑）。
        //    这张图不给 Inspector 位：它没有任何可调的东西，留个字段反而让人以为要手搭。
        点击遮罩 = 满屏图("点击遮罩", 根, new Color(0f, 0f, 0f, 0f), true);
        点击遮罩.SetAsFirstSibling();

        // ② 65% 黑：只负责把安全屋画面压暗。点了要能穿到下面那张透明图上 → raycastTarget = false。
        if (遮罩 == null) 遮罩 = 满屏图("遮罩层", 根, 遮罩色, false).GetComponent<Image>();
        遮罩.color = 遮罩色;
        遮罩.raycastTarget = false;

        // ③ 板子：居中定尺（尺寸在 板子尺寸 里按屏算）。不挂 LayoutGroup ——
        //    里面几块各自锚定到按尺寸算出的位置，布局组能做的（拉伸/换行）恰好是这里不想要的。
        if (板子 == null) 板子 = 满屏图("板子", 根, 板底色, false);
        定锚(板子, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        板子.anchoredPosition = Vector2.zero;
        补底(板子, 板底色);
        建边框(板子);

        // ④ 身份条：板内顶部那条带（左右与顶部都缩 页边距）。
        if (身份条 == null) 身份条 = 满屏图("身份条", 板子, 内容底色, false);
        定锚(身份条, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        身份条.offsetMin = new Vector2(页边距, -页边距 - 身份条高);
        身份条.offsetMax = new Vector2(-页边距, -页边距);
        补底(身份条, 内容底色);

        // 身份条**下沿**那条 2px 强调线：这是全屏唯一强调色的第一个落点，整块板子靠它立住。
        身份条强调线 = 贴边条("身份条强调线", 身份条, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 边框粗), 强调色).GetComponent<Image>();

        // 文字：身份行（左）+ 等级（右）都贴身份条**上沿**；高度给"字号 + 8"（TMP 行高略大于字号，贴字号高会切掉字底）。
        if (身份文本 == null) 身份文本 = 文本("身份文本", 身份条, 字号_标题, 正文色, TextAlignmentOptions.Left);
        顶内(身份文本.rectTransform, 内边, 内边, 字号_标题 + 8f);
        设文本样式(身份文本, 字号_标题, 正文色);
        if (等级文本 == null) 等级文本 = 文本("等级文本", 身份条, 字号_等级, 次要色, TextAlignmentOptions.Right);
        顶内(等级文本.rectTransform, 内边, 内边, 字号_等级 + 8f);
        设文本样式(等级文本, 字号_等级, 次要色);

        // ⑤ 经验格父：贴身份条**下沿**（左右各缩 内边），高度 = 进度格高。格子本身在 建经验格 里铺（固定 10 格）。
        if (经验格父 == null)
        {
            经验格父 = 新矩形("经验格父", 身份条);
            经验格自建 = true;
        }
        定锚(经验格父, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        经验格父.offsetMin = new Vector2(内边, 内边);
        经验格父.offsetMax = new Vector2(-内边, 内边 + 进度格高);

        // ⑥ 三个 Tab：底图 + 标签 + 2px 下划线；尺寸与位置在 摆Tab 里按标签宽度算。
        for (int i = 0; i < 栏目表.Length; i++) 备Tab(i);

        // ⑦ Tab 行下方那条 2px 分隔线（**整板宽**、左右缩 页边距，不随 Tab 文字长度）。
        if (分隔线 == null) 分隔线 = 贴边条("分隔线", 板子, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 边框粗), 边框色).GetComponent<Image>();
        定锚(分隔线.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        分隔线.rectTransform.offsetMin = new Vector2(页边距, -内容区顶);
        分隔线.rectTransform.offsetMax = new Vector2(-页边距, -分隔线顶);
        分隔线.color = 边框色;

        // ⑧ 内容区：**占满分隔线以下的剩余高度**（上面锚在分隔线下沿、下面锚在板子下沿的页边距上）。
        if (内容区 == null) 内容区 = 满屏图("内容区", 板子, 内容底色, false);
        定锚(内容区, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        内容区.offsetMin = new Vector2(页边距, 页边距);
        内容区.offsetMax = new Vector2(-页边距, -内容区顶);
        补底(内容区, 内容底色);

        // 内容区顶部：栏目标题 + 它下面那条 2px 强调短线（宽 = 标题文字宽，在 摆标题 里算）。
        if (栏目标题 == null) 栏目标题 = 文本("栏目标题", 内容区, 字号_栏目标题, 正文色, TextAlignmentOptions.Left);
        顶内(栏目标题.rectTransform, 内边, 内边, 标题带高);
        设文本样式(栏目标题, 字号_栏目标题, 正文色);
        面板基类.设文本(栏目标题, 栏目表[0]);
        标题下划线 = 贴边条("标题下划线", 内容区, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, 边框粗), 强调色).GetComponent<Image>();

        // ⑨ 三个栏页的子面板：缺哪个装哪个（`内容区` 交给它们自己用，本面板只做注入与显隐）
        建子面板();

        已建 = true;
        顶层尺寸变化();   // 建完再校正一次：Awake 跑在第一帧布局前，那一刻根矩形的宽高可能还是 0
    }

    // 四边 2px 边框：四条实心矩形，**不用 Outline 组件**
    //   （Outline 是按 1px 偏移画四份，线宽随缩放糊，也保证不了恒为 2px）。
    // 板底 与 身份条/内容区 不同色 → 边框落在两者之间正好看得见。
    //
    // ⚠ 写法是"**拉伸锚 + sizeDelta**"：上/下边框左右拉伸、只给 2px 高；左/右边框上下拉伸、只给 2px 宽。
    //   千万不要改回"角锚点 + offsetMin/offsetMax"那种写法（刀78 与上一个工作区版本就是这么写的，四条框**全是废的**）：
    //   角锚点下 offset 的 Y 轴是**向上**的，而"上边框"脑子里想的是"从板子上沿往下 2px"（Y 向下）——
    //   两套方向一混就会写出 0 宽或 0 高的退化矩形。Image 宽或高为 0 = **什么都不画**，边框静默消失，
    //   编译器和运行时都不会报错（只能靠眼睛或读这段注释发现）。拉伸锚没有这个歧义：方向由锚点定，粗细由 sizeDelta 定。
    private void 建边框(RectTransform 板)
    {
        float 框 = 边框粗;
        边框条(板, "上边框", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 框));
        边框条(板, "下边框", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 框));
        边框条(板, "左边框", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(框, 0f));
        边框条(板, "右边框", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(框, 0f));
    }

    // 一条边框：拉伸锚决定"横跨哪条边"，sizeDelta 决定"那个方向多粗"，anchoredPosition 恒 0（贴齐那条边、往板内长）
    private void 边框条(RectTransform 板, string 名, Vector2 锚最小, Vector2 锚最大, Vector2 轴心, Vector2 尺寸)
    {
        var 矩形 = 新矩形(名, 板);
        定锚(矩形, 锚最小, 锚最大, 轴心);
        矩形.anchoredPosition = Vector2.zero;
        矩形.sizeDelta = 尺寸;
        var 图 = 矩形.gameObject.AddComponent<Image>();
        图.color = 边框色;
        图.raycastTarget = false;
    }

    // 备一个 Tab：底图（Button 所在物体上的 Image）+ 标签 + 2px 下划线。缺哪块补哪块。
    // 按钮是本组件新建的、而标签/下划线你拖了 → 把拖进来的那个**认到新按钮底下**（否则它会飘在别处，看着像没生效）。
    private void 备Tab(int 序)
    {
        bool 新建按钮 = 取(Tab按钮, 序) == null;
        if (新建按钮)
        {
            var 新按钮矩形 = 新矩形(栏目表[序], 板子);
            var 图 = 新按钮矩形.gameObject.AddComponent<Image>();
            // 底图给**白**（不是全透明）：Button 的 ColorTint 是"乘"在底图颜色上的 ——
            //   底图 alpha = 0 的话，悬停色乘上去还是 0 → 悬停看不见（上一版悬停失效的根因）。
            //   常态不可见靠 transition 的 normalColor 取 alpha = 0，见 接Tab。
            图.color = Color.white;
            图.raycastTarget = true;   // Button 必须有一个可命中的图形（targetGraphic），否则点不到
            var 按钮 = 新按钮矩形.gameObject.AddComponent<Button>();
            按钮.targetGraphic = 图;
            Tab按钮[序] = 按钮;
        }

        var 按钮矩形 = (RectTransform)Tab按钮[序].transform;
        if (取(Tab标签, 序) == null) Tab标签[序] = 文本("标签", 按钮矩形, 字号_Tab, 次要色, TextAlignmentOptions.Left);
        else if (新建按钮) 标签(序).transform.SetParent(按钮矩形, false);
        定锚(标签(序).rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        标签(序).rectTransform.anchoredPosition = Vector2.zero;
        标签(序).rectTransform.sizeDelta = new Vector2(10f, 0f);   // 宽度在 摆Tab 里按 preferredWidth 定

        // 选中下划线：**2px 强调色**，宽度 = 标签宽（所以"下划线认得字"，而不是跨满整个 Tab 按钮）
        if (取(Tab下划线, 序) == null) Tab下划线[序] = 贴边条("下划线", 按钮矩形, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 边框粗), 强调色).GetComponent<Image>();
        else if (新建按钮) Tab下划线[序].transform.SetParent(按钮矩形, false);
        定锚(下划线(序).rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
        下划线(序).rectTransform.anchoredPosition = Vector2.zero;
        下划线(序).color = 强调色;
        面板基类.设文本(标签(序), 栏目表[序]);   // 先填上：preferredWidth 要靠它算（见 摆Tab）
    }

    // 接 Tab 的点击与悬停色。悬停色写在这里（而不是让你在 Button 的 Colors 里填）：口径要唯一，三个 Tab 才不会各写各的。
    // 代价：**别在 Button 的 Colors 上手改**（进 Play 会被这里覆盖）。
    private void 接Tab()
    {
        for (int i = 0; i < 栏目表.Length; i++)
        {
            int 序 = i;   // 闭包捕获：循环变量必须另存一份，否则三个按钮全会切到最后一栏
            var 钮 = 按钮(i);
            if (钮 == null) continue;
            // 先清掉旧监听：本组件接管这三个 Tab 的点击；不清的话，对"你拖进来的"按钮再接管一次（重建布局 时）
            //   就会一次点击切两次栏（自建的按钮不存在这个问题 —— 它们每次重建都是新物体）。
            钮.onClick.RemoveAllListeners();
            钮.transition = Selectable.Transition.ColorTint;
            var 透明 = new Color(1f, 1f, 1f, 0f);   // "白但全透明" = 常态/选中态不可见（底图本身是白的，见 备Tab）
            钮.colors = new ColorBlock
            {
                normalColor = 透明,               // 常态：不靠底色块区分（靠文字色 + 下划线）
                highlightedColor = 悬停底,         // 悬停 = 只换底，与选中态（本组件直接写文字色）不冲突
                pressedColor = 悬停底,
                selectedColor = 透明,
                disabledColor = 禁选底色,
                colorMultiplier = 1f,
                fadeDuration = 0f,                // 无过渡动画（用户明令：无任何动画，也**不做缩放反馈**）
            };
            钮.onClick.AddListener(() => 切换栏(序));
        }
    }

    // ================= 三个子面板（`内容区` 的注入 + 切栏时的显隐） =================

    // 缺哪个装哪个。装的动作 = 在 `内容区` 下建一个子物体、挂上那个组件、记进账本、回填字段、注入 内容区。
    // 拖了的那三个（不为 null）**一律不动** —— 它们不在账本上，清掉自建 时不会被销毁。
    private void 建子面板()
    {
        if (内容区 == null) return;
        装子面板(0, ref 属性页);
        装子面板(1, ref 技能页);
        装子面板(2, ref 知识页);
    }

    private void 装子面板<T>(int 序, ref T 字段) where T : Component
    {
        if (字段 == null)
        {
            var 物体 = new GameObject(栏目表[序] + "页", typeof(RectTransform));
            物体.transform.SetParent(内容区, false);
            自建节点.Add(物体);            // 先记账：随后那一步（注入）若抛了，清掉自建 也收得回这个半成品
            字段 = 物体.AddComponent<T>();
            var 矩形 = (RectTransform)物体.transform;
            定锚(矩形, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            矩形.offsetMin = Vector2.zero;
            矩形.offsetMax = Vector2.zero;
        }
        else
        {
            // 你拖进来的物体：只保证它挂在 `内容区` 下（位置归本组件按上面那套锚点写），并确保它在账本上 ——
            //   记账不会让它被误删（清掉自建 只销毁"本组件建的"；你拖的那个由本组件接管摆位，属于"我来管"，
            //   但**不销毁用户资产**这条更硬 → 这里不给它记账，只改父级与锚点）。
            字段.transform.SetParent(内容区, false);
            var 矩形 = (RectTransform)字段.transform;
            定锚(矩形, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            矩形.offsetMin = Vector2.zero;
            矩形.offsetMax = Vector2.zero;
        }
        注入内容区(序, 字段);
    }

    // 把 `内容区` 注入子面板（三者接口同名但类型不同 → 只能按具体类型分发，`where T : Component` 那层拿不到方法）。
    // ⚠ 这里用 `as` 而不是 `(T)` 强转：泛型位传进来的实际类型是确定的（只在这三行调用），
    //   但 `as` 让"以后误传别的组件"变成一次静默跳过，而不是运行期炸在面板打开的那一帧。
    private void 注入内容区<T>(int 序, T 子面板)
    {
        switch (序)
        {
            case 0: (属性页 as 属性子面板)?.设内容区(内容区); break;
            case 1: (技能页 as 技能子面板)?.设内容区(内容区); break;
            case 2: (知识页 as 知识子面板)?.设内容区(内容区); break;
        }
    }

    // ================= 数据填充 =================

    // 顶部身份行："幸存者 · 职业"。职业名从数据表取（`职业数据.名称`）；取不到就退回职业标识，
    // 连标识都空（还没开档）就只显示"幸存者"。
    private static string 身份行(玩家档案 玩家)
    {
        string 职业 = "";
        if (!string.IsNullOrEmpty(玩家.职业))
        {
            var 数据 = ServiceRegistry.已注册<DataService>() ? ServiceRegistry.Get<DataService>() : null;
            职业 = 数据 != null && 数据.职业 != null && 数据.职业.TryGetValue(玩家.职业, out var 定义) && 定义 != null                && !string.IsNullOrEmpty(定义.名称)
                ? 定义.名称
                : 玩家.职业;
        }
        return string.IsNullOrEmpty(职业) ? "幸存者" : $"幸存者 · {职业}";
    }

    // 经验条：**一格一格地点亮**，不做连续填充（比例 = 经验 / 升级所需经验，CeilToInt(比例×格数) 亮前 N 格）。
    // 格数固定 = 经验格数（10）：这是"分段经验格"，不随板宽变（板宽变了格子会被拉宽，段数不变）。
    private void 建经验格()
    {
        if (经验格父 == null) return;
        if (!经验格自建)
        {
            if (经验格父.childCount > 0) { 收你的经验格(); return; }
            经验格自建 = true;   // 你只是拖了个空容器 → 替你把格子铺出来（之后按自建铺）
        }
        if (经验格.Count == 经验格数) return;   // 格数没变 → 位置与尺寸全按这些字段算，还是对的 → 直接复用
        for (int i = 0; i < 经验格.Count; i++)
        {
            var 旧 = 经验格[i];
            if (旧 == null) continue;
            自建节点.Remove(旧.gameObject);   // 先退账再销毁：账本里留着"待销毁的格子"，清掉自建 会去拎它们的（不存在的）子物体
            销毁(旧.gameObject);
        }
        经验格.Clear();
        for (int i = 0; i < 经验格数; i++)
        {
            var 格 = 新矩形("经验格", 经验格父);
            定锚(格, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f));
            格.anchoredPosition = new Vector2(i * (进度格宽 + 进度格间隔), 0f);
            格.sizeDelta = new Vector2(进度格宽, 0f);
            var 图 = 格.gameObject.AddComponent<Image>();
            图.color = 禁选底色;   // 先按"空槽"画；点亮比例由 刷经验格 写
            图.raycastTarget = false;
            经验格.Add(图);
        }
    }

    // 收你摆的格子（只收一次：顶层尺寸变化 会反复调，按 childCount 判重）
    private void 收你的经验格()
    {
        if (经验格.Count == 经验格父.childCount) return;
        经验格.Clear();
        for (int i = 0; i < 经验格父.childCount; i++) 经验格.Add(经验格父.GetChild(i).GetComponent<Image>());
    }

    private void 刷经验格(玩家档案 玩家)
    {
        int 需要 = Mathf.Max(1, 玩家.升级所需经验);
        float 比例 = Mathf.Clamp01(玩家.经验 / (float)需要);
        int 总格 = 经验格.Count;
        int 亮 = 总格 == 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(比例 * 总格), 0, 总格);
        for (int i = 0; i < 总格; i++)
        {
            var 格 = 经验格[i];
            if (格 == null) continue;   // 子物体只是个分组/占位（没挂 Image）→ 跳过，不算错
            格.color = i < 亮 ? 强调色 : 禁选底色;
        }
    }

    // ================= 栏切换（= SetActive + 注入，让子面板自己刷） =================

    // 切栏：只改文字色/下划线显隐 + 内容区标题 + 三个子面板的显隐（点选，无动画，无缩放反馈）。
    // 每处都判空：三个 Tab 只接了俩、栏目标题还没搭 —— 都属于"搭一半"，不许因此报错。
    private void 切换栏(int 序)
    {
        当前栏 = Mathf.Clamp(序, 0, 栏目表.Length - 1);
        for (int i = 0; i < 栏目表.Length; i++)
        {
            bool 选中 = i == 当前栏;
            var 文字 = 标签(i);
            面板基类.设文本(文字, 栏目表[i]);   // Tab 文字由代码写（口径见 栏目表），保证与下标的栏名一致
            if (文字 != null) 文字.color = 选中 ? 正文色 : 次要色;
            var 线 = 下划线(i);
            if (线 != null) 线.gameObject.SetActive(选中);   // 只有选中那条下划线显示
        }
        面板基类.设文本(栏目标题, 栏目表[当前栏]);
        摆标题();   // 标题文字换长度（"属性"→"知识"）→ 那条 2px 短线要跟着重摆

        // 切栏 = 让当前那个子面板显示、另外两个收起，然后**让当前这个自己刷一次**。
        // 为什么刷一次而不是等它自己的事件：切过去的那一刻它可能刚被 SetActive(true)，
        //   而"上次变化事件"已经过去了（事件不会为"你刚打开这一页"重发）。
        显隐子面板(0, 属性页);
        显隐子面板(1, 技能页);
        显隐子面板(2, 知识页);
        switch (当前栏)
        {
            case 0: 属性页?.刷新(); break;
            case 1: 技能页?.刷新(); break;
            case 2: 知识页?.刷新(); break;
        }
    }

    private void 显隐子面板<T>(int 序, T 子面板) where T : Component
    {
        if (子面板 != null) 子面板.gameObject.SetActive(序 == 当前栏);
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

    // ================= 自建清单（"哪些节点是我建的"这份账；建树 / 重建布局 都靠它） =================

    // 清掉"本组件建过的那一套"：**只销毁 `自建节点` 里的东西**（= 我建的），你的物体一个都不动。
    // 顺带把你拖进来的、恰好挂在我建的节点下面的物体先拎出来，免得跟着一起被销毁。
    private void 清掉自建()
    {
        if (自建节点 == null) 自建节点 = new List<GameObject>();

        foreach (var 物体 in 自建节点)
        {
            if (物体 == null) continue;   // 你手动删过的（或已被父节点带走的）→ 跳过，不算错
            拎出你的子物体(物体);
            销毁(物体);
        }

        // 引用位里指向"自建节点"的那些要**显式清空**：运行期 `Destroy` 要等帧末才真的生效，
        //   不清的话紧接着的建树会把这些"待销毁的"当成"已经接好的"直接复用 → 帧末它们真没了，面板就空一块。
        // ⚠ 这一段必须在 自建节点.Clear() **之前**（判据就是这份账）。
        if (是我的(遮罩)) 遮罩 = null;
        if (是我的(板子)) 板子 = null;
        if (是我的(身份条)) 身份条 = null;
        if (是我的(身份文本)) 身份文本 = null;
        if (是我的(等级文本)) 等级文本 = null;
        if (是我的(经验格父)) 经验格父 = null;
        if (是我的(分隔线)) 分隔线 = null;
        if (是我的(栏目标题)) 栏目标题 = null;
        if (是我的(内容区)) 内容区 = null;
        // 三个子面板：只清"我建的那三个"（拖进来的不在账上，原样留着继续用）
        if (是我的(属性页)) 属性页 = null;
        if (是我的(技能页)) 技能页 = null;
        if (是我的(知识页)) 知识页 = null;
        for (int i = 0; i < 栏目表.Length; i++)
        {
            if (是我的(取(Tab按钮, i))) Tab按钮[i] = null;
            if (是我的(取(Tab标签, i))) Tab标签[i] = null;
            if (是我的(取(Tab下划线, i))) Tab下划线[i] = null;
        }

        自建节点.Clear();

        // 缓存跟着清：节点已经销毁，列表里再留着就是一堆假引用（经验格都是挂在那些节点下面的）
        经验格.Clear();
        经验格自建 = false;
        身份条强调线 = null;
        标题下划线 = null;
        点击遮罩 = null;
    }

    private bool 是我的(GameObject 物体) => 物体 != null && 自建节点.Contains(物体);

    private bool 是我的(Component 组件) => 组件 != null && 自建节点.Contains(组件.gameObject);

    // 把"不在自建清单里"的直接子物体拎到**最近一个不是我建的祖先**下（最坏就是面板根）——
    //   那些是你拖进来的，本组件永远不销毁它们。
    // 为什么落点要往上找，而不是像早先那样只跳到"物体的父级"：父级很可能**也是我建的、也在待销毁清单里**，
    //   那样等于把你的物体挪到一个马上要没的节点底下（运行期 Destroy 还要等帧末）→ 它会被连带销毁。
    //   会命中的场景是"半接"：只把 标签 拖进 Tab标签 位、Tab按钮 位留空 —— 本组件会把你的标签认到新建的按钮底下，
    //   重建时就得先把它带出来（见 备Tab）。
    private void 拎出你的子物体(GameObject 物体)
    {
        var 你的 = new List<Transform>();
        for (int i = 0; i < 物体.transform.childCount; i++)
        {
            var 子 = 物体.transform.GetChild(i);
            if (!是我的(子.gameObject)) 你的.Add(子);
        }
        if (你的.Count == 0) return;

        var 落点 = 物体.transform.parent;
        while (落点 != null && 是我的(落点.gameObject)) 落点 = 落点.parent;   // 面板根不在账本上 → 循环必然停

        // 先收集再改父级：边遍历边 SetParent 会把 childCount/顺序搅乱
        foreach (var 子 in 你的) 子.SetParent(落点, false);
    }

    // 销毁：编辑器里（未运行）用 `DestroyImmediate` —— 运行期的 `Destroy` 在编辑器下不会立刻生效，
    //   紧接着的建树会读到"还没死的旧节点"；运行期用 `Destroy`，但**先失活**：它要等帧末，留着会和新树叠一帧。
    private static void 销毁(GameObject 物体)
    {
        if (物体 == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) { DestroyImmediate(物体); return; }
#endif
        物体.SetActive(false);
        Destroy(物体);
    }

    // ================= 尺寸计算（板子不铺满屏 / Tab 随板宽重算） =================

    // 根矩形尺寸变化时重算（Awake 跑在第一帧布局之前，那一刻根矩形的宽高可能还是 0 → 算出来的板子是假的）。
    // 不做每帧轮询：只有真的变了才进来。
    private void OnRectTransformDimensionsChange()
    {
        if (!已建) return;
        顶层尺寸变化();
    }

    private void 顶层尺寸变化()
    {
        板子尺寸();
        摆Tab();
        摆标题();
        刷新(null);   // 条宽/格数都变了 → 重新点亮一次（读当前档案；还没开档就什么都不做）
    }

    // 板子尺寸：宽 = min(屏宽 × 板宽比例, 板宽上限)，高 = min(屏高 × 板高比例, 板高上限)，屏幕居中（锚点/轴心都是 0.5）。
    // 为什么取 min 而不是单纯按比例：比例是"覆盖大半"的观感，上限是"大屏上别拉成一张网页"的硬边界。
    private void 板子尺寸()
    {
        if (板子 == null) return;
        var 父 = 板子.parent as RectTransform;
        if (父 == null) return;
        float 屏宽 = 父.rect.width;
        float 屏高 = 父.rect.height;
        if (屏宽 <= 1f || 屏高 <= 1f)
        {
            // 还没量出屏幕尺寸。两种情形走这里：
            //   ① 运行时第一帧布局前（马上会收到尺寸回调，这里的结果只是过渡值）；
            //   ② 编辑器里（未运行）一键生成预制体 —— 那个时刻根本没有"屏幕"，给上限尺寸才看得见东西。
            // 取上限而不是"保持原样"：早先版本保持原样时，编辑器里建出来的板子是 0×0（保存进预制体就是一块看不见的板）。
            板子.sizeDelta = new Vector2(板宽上限, 板高上限);
            return;
        }
        板子.sizeDelta = new Vector2(
            Mathf.Min(屏宽 * 板宽比例, 板宽上限),
            Mathf.Min(屏高 * 板高比例, 板高上限));
    }

    // Tab 行：从左到右顺着摆，间距 = 段距；宽度取标签的 preferredWidth（所以下划线正好压在字下面）。
    // 每次重摆都先按当前字号重算一遍 preferredWidth：换分辨率/换字号后不会用旧值。
    // 为什么不用 HorizontalLayoutGroup：Tab 宽度要等于各自标签的宽度（下划线才贴得住字），而布局组的宽度
    //   要等一帧才算出来 —— 这里直接读 preferredWidth，摆完立刻能看见正确结果，不留一帧的错位。
    private void 摆Tab()
    {
        if (板子 == null) return;
        float x = 页边距;
        for (int i = 0; i < 栏目表.Length; i++)
        {
            var 钮 = 按钮(i);
            if (钮 == null) continue;
            var 文字 = 标签(i);
            if (文字 != null) 文字.fontSize = 字号_Tab;   // 触发 preferredWidth 按当前字号重算
            float 文字宽 = 文字 != null ? 文字.preferredWidth : 0f;
            if (文字宽 <= 1f) 文字宽 = 字号_Tab * 2f;     // TMP 还没量出文字（极端情况）→ 给一个点得到的宽度
            var 矩形 = (RectTransform)钮.transform;
            定锚(矩形, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            // 高度给 字号 + 8：下划线要落在字的下方（按字号高摆的话会压住字底）
            矩形.sizeDelta = new Vector2(文字宽, 字号_Tab + 8f);
            矩形.anchoredPosition = new Vector2(x, -Tab行顶);
            if (文字 != null)
            {
                文字.rectTransform.sizeDelta = new Vector2(文字宽, 0f);
                文字.rectTransform.anchoredPosition = Vector2.zero;
            }
            // 下划线贴按钮**下沿**（按钮高 = 字号 + 8，所以它落在文字基线之下一点，是"下划线"不是"删除线"）
            var 线 = 下划线(i);
            if (线 != null) 线.rectTransform.sizeDelta = new Vector2(文字宽, 边框粗);
            x += 文字宽 + 段距;
        }
    }

    // 内容区标题下那条 2px 强调短线：宽度 = 标题文字宽（与 Tab 下划线同一口径：线认得字，不跨满整行）。
    private void 摆标题()
    {
        if (栏目标题 == null || 标题下划线 == null) return;
        栏目标题.fontSize = 字号_栏目标题;
        float 宽 = 栏目标题.preferredWidth;
        if (宽 <= 1f) 宽 = 字号_栏目标题 * 2f;
        定锚(标题下划线.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        标题下划线.rectTransform.anchoredPosition = new Vector2(内边, -标题下划线顶);
        标题下划线.rectTransform.sizeDelta = new Vector2(宽, 边框粗);
    }

    // ================= 留白口径（一处定义，摆位处都读它） =================

    private float 内边 => 段距;                       // 板内第二层留白：文字/行 离 身份条/内容区 的边还有 20px（**不贴边**）
    private float 标题带高 => 字号_栏目标题 + 8f;      // 栏目标题那一行的带高（TMP 行高略大于字号，留 8）
    private float 标题下划线顶 => 标题带高 + 12f;       // 标题下 12px 才画那条强调短线（靠留白分层，不贴字）
    private float Tab行顶 => 页边距 + 身份条高 + 段距;   // 从板子上沿往下量
    private float Tab行底 => Tab行顶 + 字号_Tab + 8f;    // 与 摆Tab 里那个"字号 + 8"是同一个数（两边必须一致）
    private float 分隔线顶 => Tab行底 + 段距;           // 分隔线上沿（下沿 = 它 + 边框粗）
    private float 内容区顶 => 分隔线顶 + 边框粗;

    // ================= 建节点的几个小工具（全是判空安全的小工具，只建本组件自己的节点） =================

    // 取按钮/标签/下划线数组的第 序 个（越界或没接都返回 null → 调用方一句判空即可）
    private Button 按钮(int 序) => 取(Tab按钮, 序);
    private TMP_Text 标签(int 序) => 取(Tab标签, 序);
    private Image 下划线(int 序) => 取(Tab下划线, 序);

    // 数组取值：越界或该位没接都返回 null → "只接了两个 Tab"是合法状态
    private static T 取<T>(T[] 数组, int 序) where T : class
        => 数组 != null && 序 >= 0 && 序 < 数组.Length ? 数组[序] : null;

    // 旧场景可能把数组存成空数组/短数组 → 先补到 3 个（否则按下标赋值就崩）。
    // ⚠ 短数组里**已有的引用要带过来**：直接 `数组 = new T[3]` 会把"只拖了两个 Tab"的那两个引用丢掉
    //   （它们在 Inspector 里看着还在，实际已经被本组件换掉了 —— 面板上就是那两个 Tab 消失）。
    private static void 备数组<T>(ref T[] 数组) where T : class
    {
        if (数组 != null && 数组.Length == 栏目表.Length) return;
        var 旧 = 数组;
        var 新 = new T[栏目表.Length];
        if (旧 != null)
            for (int i = 0; i < 旧.Length && i < 新.Length; i++) 新[i] = 旧[i];
        数组 = 新;
    }

    // 建一个只有 RectTransform 的节点。**默认记账**：本组件建的东西都要能被 重建布局 收回去。
    //（唯一需要"先退账再销毁"的是经验格 —— 它由 建经验格 反复铺/收，见那里的注释）
    private RectTransform 新矩形(string 名, Transform 父, bool 记账 = true)
    {
        var 物体 = new GameObject(名, typeof(RectTransform));
        物体.transform.SetParent(父, false);
        if (记账) 自建节点.Add(物体);
        return (RectTransform)物体.transform;
    }

    // 建一张铺满父级的实心矩形（遮罩/板底/内容底用）；`接点击` 决定它吃不吃鼠标事件
    private RectTransform 满屏图(string 名, Transform 父, Color 色, bool 接点击)
    {
        var 矩形 = 新矩形(名, 父);
        贴合父级(矩形);
        var 图 = 矩形.gameObject.AddComponent<Image>();
        图.color = 色;
        图.raycastTarget = 接点击;
        return 矩形;
    }

    // 建一条贴父级某条边的实心矩形（强调线/下划线/分隔线用，都是"一条 2px"）
    private RectTransform 贴边条(string 名, Transform 父, Vector2 锚最小, Vector2 锚最大, Vector2 尺寸, Color 色)
    {
        var 矩形 = 新矩形(名, 父);
        定锚(矩形, 锚最小, 锚最大, 锚最小);
        矩形.anchoredPosition = Vector2.zero;
        矩形.sizeDelta = 尺寸;
        var 图 = 矩形.gameObject.AddComponent<Image>();
        图.color = 色;
        图.raycastTarget = false;
        return 矩形;
    }

    // 建一段文本。字体**不硬编码资源路径**：项目已有 TMP 设置，取它的默认字体资产
    // （换字体只改 TMP Settings 一处，不用回来改这个文件）。
    private TMP_Text 文本(string 名, Transform 父, int 字号, Color 色, TextAlignmentOptions 对齐, bool 记账 = true)
    {
        var 矩形 = 新矩形(名, 父, 记账);
        var 文本组件 = 矩形.gameObject.AddComponent<TextMeshProUGUI>();
        文本组件.font = TMP_Settings.defaultFontAsset;
        文本组件.fontSize = 字号;
        文本组件.color = 色;
        文本组件.alignment = 对齐;
        文本组件.textWrappingMode = TextWrappingModes.NoWrap;   // 档案板靠对齐分层：标题/标签一律不折行
        文本组件.overflowMode = TextOverflowModes.Ellipsis;
        文本组件.raycastTarget = false;   // 文本不拦点击（点击一律落到按钮上）
        return 文本组件;
    }

    // 给容器上底：没接、或它身上没有 Image 就补一张（板底/身份条底/内容底 是这块板子的观感底座，缺了就不成"板"）。
    // 代价：**别在这些物体上放 sprite**（实心方块才是这套视觉的口径）。
    private static void 补底(RectTransform 矩形, Color 色)
    {
        if (矩形 == null) return;
        var 图 = 矩形.GetComponent<Image>();
        if (图 == null) 图 = 矩形.gameObject.AddComponent<Image>();
        图.color = 色;
        图.raycastTarget = false;
    }

    // 写文本的字号与颜色（内联口径就靠它落地；文本没接就跳过）
    private static void 设文本样式(TMP_Text 文本, int 字号, Color 色)
    {
        if (文本 == null) return;
        文本.fontSize = 字号;
        文本.color = 色;
    }

    private static void 定锚(RectTransform 矩形, Vector2 锚最小, Vector2 锚最大, Vector2 轴心)
    {
        矩形.anchorMin = 锚最小;
        矩形.anchorMax = 锚最大;
        矩形.pivot = 轴心;
    }

    // 贴父级上沿：左右各留 边距，高度固定（标题/等级这类"单行文字"用）
    private static void 顶内(RectTransform 矩形, float 左, float 右, float 高)
    {
        定锚(矩形, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        矩形.offsetMin = new Vector2(左, -高);
        矩形.offsetMax = new Vector2(-右, 0f);
    }

    private static void 贴合父级(RectTransform 矩形)
    {
        矩形.anchorMin = Vector2.zero;
        矩形.anchorMax = Vector2.one;
        矩形.pivot = new Vector2(0.5f, 0.5f);
        矩形.offsetMin = Vector2.zero;
        矩形.offsetMax = Vector2.zero;
    }
}
