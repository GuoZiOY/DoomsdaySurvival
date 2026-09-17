using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 角色面板：全局面板。一屏四栏 = **一块浮在安全屋画面上的"幸存者档案板"**
//   （板内：顶部身份条 + 顶部横排四个大字 Tab + 内容区；栏页内容分批填，本批到栏目标题占位）。
//
// ★ 本文件是**第二次重写**：布局从"代码自建"改成"**用户自己在 Unity 里搭 + Inspector 接线**"。
//   上一版 `Awake` 里用 new GameObject / AddComponent 把整棵节点树建出来（场景里只要一个空物体就够）；
//   用户拍板要手动搭 UI，所以那些建节点的代码**整批删掉**，本组件现在只做两件事：
//     ① 把场景里搭好的节点**引用进来**（[SerializeField]，全部可空、允许只接一部分）；
//     ② 填数据 + 响应点击（身份文本 / 等级 / 经验格 / 四个 Tab 的切换）。
//   ★ 不能破的约定：**本文件不许再出现 `new GameObject` / `AddComponent`**（那正是这一批要拆掉的东西）；
//     要"建"东西 → 加到场景里手动搭，然后在这里加一个引用位。
//   ⚠ 文件名与 `.cs.meta` 没动 —— 场景/预制体引用靠 meta 里的 GUID，改名或删文件会让引用变 Missing Script。
//
// 场景里要搭的层级（括号里 = 拖到哪个引用位；**名字随意，认的是引用不是路径**）：
//   角色面板（挂本组件，满屏 —— `面板管理器` 要求面板根满屏，它会自己摆）
//     ├─ 遮罩            → Image                        遮罩
//     └─ 板子            → Image + RectTransform        板子      ★ Image 挂在板子自己身上（= 板底）
//          ├─ 身份条      → Image                        身份条    ★ 同上（= 身份条底）
//          │    ├─ 身份文本 → TMP_Text                  身份文本
//          │    ├─ 等级文本 → TMP_Text                  等级文本
//          │    └─ 经验格父 → RectTransform             经验格父   ★ 下面摆 N 个各带 Image 的格子（皮肤里的进度格尺寸/间隔）
//          ├─ Tab0..Tab3  → Button + Image（底图）      Tab按钮[0..3]
//          │    ├─ 标签    → TMP_Text                   Tab标签[0..3]
//          │    └─ 下划线  → Image                      Tab下划线[0..3]
//          ├─ 分隔线      → Image                        分隔线
//          └─ 内容区      → Image                        内容区    ★ 同上（= 内容底）
//               └─ 栏目标题 → TMP_Text                  栏目标题
//   ※ 留空 = 那一步跳过。搭到一半（只接了 2 个 Tab、还没摆经验格）面板照样能开、不报错、不崩。
//
// 口径（搭的时候照做，别自己发明）：
//   · 不铺满屏：板子居中、覆盖大半（比例与上限在 `角色面板皮肤`，代码不再算尺寸，照那些数摆）；
//   · 大一号 + 暖调近黑 + 唯一强调色暗锈红；**没有圆角/阴影/发光/渐变/图标/动画**（按钮悬停也 fadeDuration = 0）；
//   · 四个 Tab 里**没有"装备"栏** —— 装备归 `持有面板`，这里不重复一份。
//
// 开合沿用 `面板管理器` 的既有约定，不自造一套：
//   · 打开 = `打开角色面板事件`（或 C 键）→ `面板管理器.显示(角色)`；
//   · 关闭 = HUD 上那颗统一关闭按钮 → 调本面板的 `回退()`（`面板基类.可关闭` 的判据就是"覆写了 取消文本"）。
//   · 项目**没有全局 Esc 回退键**（用户 2026-09-15 拍板），所以这里也不绑 Esc。
public sealed class 角色面板 : 面板基类
{
    // 四个栏目（顺序 = 顶部 Tab 从左到右，也就是下面三个数组的下标 0..3）。
    // **没有"装备"栏** —— 装备归 `持有面板`，这里不重复一份。
    // Tab 标签文字与栏目标题都由代码按这张表写 → 想改字改这里，**别在 Inspector 里改**（开面板时会被覆盖）。
    private static readonly string[] 栏目表 = { "属性", "技能", "知识", "天赋" };

    // ================= Inspector 接线：结构引用（用户手动搭好后拖进来，全部可为空） =================
    // 为什么全是可空 + 每处判空：这批的前提是"用户自己搭"，他随时处于"搭了一半"的状态 ——
    //   这时面板必须能开、能看、不报错（不许空引用崩），只是没接的那部分不显示而已。
    [Header("结构引用（手动搭好后拖进来；留空的字段 = 那一步跳过）")]
    [SerializeField] private Image 遮罩;                // 满屏暗色层（颜色由皮肤写；挡不挡点击看它的 Raycast Target）
    [SerializeField] private RectTransform 板子;         // 档案板根（Image 挂它自己身上 = 板底）
    [SerializeField] private RectTransform 身份条;       // 板内顶部那条（Image 挂它自己身上 = 身份条底）
    [SerializeField] private TMP_Text 身份文本;         // "幸存者 · 职业"
    [SerializeField] private TMP_Text 等级文本;         // "Lv.N"
    [SerializeField] private RectTransform 经验格父;     // 经验格的父节点：下面摆 N 个各带 Image 的格子
    [SerializeField] private Button[] Tab按钮 = new Button[4];     // 顶部四个 Tab 的按钮（下标 = 栏目顺序）
    [SerializeField] private TMP_Text[] Tab标签 = new TMP_Text[4]; // 四个 Tab 的文字（选中/未选中就改它的颜色）
    [SerializeField] private Image[] Tab下划线 = new Image[4];      // 四个 Tab 的下划线（只显示的选中那条）
    [SerializeField] private Image 分隔线;               // Tab 行下方那条 2px 线（整板宽）
    [SerializeField] private TMP_Text 栏目标题;         // 内容区顶部的"属性/技能/知识/天赋"（随 Tab 变）
    [SerializeField] private RectTransform 内容区;       // 分隔线以下的整块（Image 挂它自己身上 = 内容底）

    // ================= Inspector 接线：皮肤参数 =================
    // 每个面板实例一份（类是可序列化的普通类，展开就能改）→ 颜色/字号/尺寸都从它读，本文件不写死任何数字。
    [Header("皮肤参数（颜色/字号/尺寸；默认值 = 幸存者档案板口径）")]
    [SerializeField] private 角色面板皮肤 皮肤 = new 角色面板皮肤();

    private int 当前栏;

    void Awake()
    {
        // 皮肤是普通可序列化类（不是 UnityEngine.Object）：字段初始化器已经给了默认值，
        // 但**旧场景里存过的组件**可能带着一个 null（这个字段是后加的）→ 后面十几处都要读它，
        // 在这里兜一次，比每处都写 `皮肤?.` 干净（也是这批"搭一半不许崩"的一部分）。
        if (皮肤 == null) 皮肤 = new 角色面板皮肤();

        套用皮肤();   // 静态外观（底色/字号/悬停色）只在启动时写一次，之后不再动
        接Tab();

        // 刷新口径：属性变化（加点/装备变化都会发）与经验变化（升级/加经验）各刷一次顶部身份条。
        // 四个栏页的内容各自订阅自己关心的事件（如 `属性子面板`），本面板只负责"框"与顶部那一条。
        var 事件 = ServiceRegistry.Get<EventBus>();
        if (事件 != null)
        {
            事件.订阅<属性变化事件>(_ => 刷新(null));
            事件.订阅<经验变化事件>(_ => 刷新(null));
        }
        切换栏(0);
        刷新(null);   // 先填一次顶部身份条：不依赖"上一次变化事件"（面板可能是在档案早已改变之后才第一次打开）
    }

    protected override void 刷新(object 上下文)
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null) return;
        面板基类.设文本(身份文本, 身份行(玩家));
        面板基类.设文本(等级文本, $"Lv.{玩家.等级}");
        刷经验格(玩家);
    }

    // ================= 皮肤落位（把参数写进场景里的物体） =================

    // 为什么连板底/分隔线这种"用户在 Image 上也能自己填"的颜色也在代码里写一遍：
    //   皮肤要当**唯一口径** —— 否则同一个颜色得在 Inspector 里手填好几个 Image，改一次风格就要逐个找，
    //   漏一个就是一块对不上色的面板（上一版靠"颜色只写在皮肤里"才没散开，这个性质这一版要保住）。
    // 代价（写在明处）：**想改色改 `皮肤`，别在 Image 上改** —— 进 Play 时会被这里覆盖。
    // 反过来：`板子/身份条/内容区` 的底色 Image **要直接挂在这三个物体自己身上**才会被上色；
    //   如果你把底图做成了子物体（上一版的结构就是这样），皮肤就不管它，自己填色即可。
    private void 套用皮肤()
    {
        设图(遮罩, 皮肤.遮罩);        // 65% 黑只负责画面变暗：Raycast Target 留给你决定（要不要挡住背后的界面）
        设图(板子, 皮肤.板底);
        设图(身份条, 皮肤.内容底);
        设图(内容区, 皮肤.内容底);
        设图(分隔线, 皮肤.边框);
        设文本样式(身份文本, 皮肤.字号_标题, 皮肤.正文);       // 身份行：板内最大的一档
        设文本样式(等级文本, 皮肤.字号_等级, 皮肤.次要);       // 等级：同排但不跟身份行抢视线
        设文本样式(栏目标题, 皮肤.字号_栏目标题, 皮肤.正文);
        for (int i = 0; i < 栏目表.Length; i++)
        {
            设文本样式(取(Tab标签, i), 皮肤.字号_Tab, 皮肤.次要);   // 先按"未选中"写；选中的那个由 切换栏 改成 正文
            设图(取(Tab下划线, i), 皮肤.强调);                       // 下划线颜色固定，显隐由 切换栏 管
        }
    }

    // 接 Tab 的点击与悬停色。
    // 为什么悬停色也在代码里写（而不是让人在 Button 的 Colors 里填）：皮肤要当唯一口径，四个 Tab 才不会各写各的。
    // 代价：**别在 Button 的 Colors 上手改**（进 Play 时被这里覆盖）；底图 = Button 所在物体上的 Image
    //   （Unity 加 Button 时会自动把 Target Graphic 指到同一个物体上的 Graphic）。
    private void 接Tab()
    {
        for (int i = 0; i < 栏目表.Length; i++)
        {
            int 序 = i;   // 闭包捕获：循环变量必须另存一份，否则四个按钮全会切到最后一栏
            var 按钮 = 取(Tab按钮, i);
            if (按钮 == null) continue;
            按钮.transition = Selectable.Transition.ColorTint;
            按钮.colors = new ColorBlock
            {
                normalColor = new Color(0f, 0f, 0f, 0f),        // 常态透明：选中不靠底色块区分（靠文字色 + 下划线）
                highlightedColor = 皮肤.悬停底,                 // 悬停 = 只换底，与选中态（本组件直接写文字色）不冲突
                pressedColor = 皮肤.悬停底,
                selectedColor = new Color(0f, 0f, 0f, 0f),
                disabledColor = 皮肤.禁选底,
                colorMultiplier = 1f,
                fadeDuration = 0f,                              // 无过渡动画（用户明令：无任何动画，也**不做缩放反馈**）
            };
            按钮.onClick.AddListener(() => 切换栏(序));
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
            var 数据 = ServiceRegistry.Get<DataService>();
            职业 = 数据 != null && 数据.职业 != null && 数据.职业.TryGetValue(玩家.职业, out var 定义) && 定义 != null
                && !string.IsNullOrEmpty(定义.名称)
                ? 定义.名称
                : 玩家.职业;
        }
        return string.IsNullOrEmpty(职业) ? "幸存者" : $"幸存者 · {职业}";
    }

    // 经验条：**一格一格地点亮**，不做连续填充（口径与上一版一致）。
    // 格子由你在场景里摆好（几个都行），这里只按比例决定"亮到第几格"，空槽用 禁选底 画出来。
    // 为什么不再按条宽现场算格数并建格子（上一版的 建经验格）：布局已经交给你了，摆几格是你的事，
    //   代码只负责"按 经验/升级所需经验 点亮前 N 格"这件事 —— 也就不需要再 Destroy/new 任何节点。
    private void 刷经验格(玩家档案 玩家)
    {
        if (经验格父 == null) return;
        int 需要 = Mathf.Max(1, 玩家.升级所需经验);
        float 比例 = Mathf.Clamp01(玩家.经验 / (float)需要);
        int 总格 = 经验格父.childCount;   // 格数 = 你摆的子物体个数（一格 = 一个有 Image 的直接子物体）
        int 亮 = Mathf.Clamp(Mathf.CeilToInt(比例 * 总格), 0, 总格);
        for (int i = 0; i < 总格; i++)
        {
            var 格 = 经验格父.GetChild(i).GetComponent<Image>();
            if (格 == null) continue;   // 子物体只是个分组/占位（没挂 Image）→ 跳过，不算错
            格.color = i < 亮 ? 皮肤.强调 : 皮肤.禁选底;
        }
    }

    // ================= 栏切换 =================

    // 切栏：只改文字色/下划线显隐与内容区标题（点选，无动画，无缩放反馈）。
    // 每处都判空：四个 Tab 只接了两个、栏目标题还没搭 —— 都属于"搭一半"，不许因此报错。
    private void 切换栏(int 序)
    {
        当前栏 = Mathf.Clamp(序, 0, 栏目表.Length - 1);
        for (int i = 0; i < 栏目表.Length; i++)
        {
            bool 选中 = i == 当前栏;
            var 标签 = 取(Tab标签, i);
            面板基类.设文本(标签, 栏目表[i]);   // Tab 文字由代码写（口径见 栏目表），保证与下标的栏名一致
            if (标签 != null) 标签.color = 选中 ? 皮肤.正文 : 皮肤.次要;
            var 线 = 取(Tab下划线, i);
            if (线 != null) 线.gameObject.SetActive(选中);   // 只有选中那条下划线显示（下划线认得字，宽度在场景里摆好）
        }
        面板基类.设文本(栏目标题, 栏目表[当前栏]);   // 本批内容区只有栏目标题占位，栏页内容分批填
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

    // ================= 接线工具（全是判空小工具，不建任何节点） =================

    // 数组取值：越界或该位没接都返回 null → 调用方一句判空即可，"只接了两个 Tab"是合法状态
    private static T 取<T>(T[] 数组, int 序) where T : class
        => 数组 != null && 序 >= 0 && 序 < 数组.Length ? 数组[序] : null;

    // 给一张图填色（Image 没接就跳过）
    private static void 设图(Image 图, Color 色)
    {
        if (图 != null) 图.color = 色;
    }

    // 给"底色挂在自身"的容器填色（容器没接、或它身上没 Image 都跳过 —— 那说明底图是子物体，归用户自己填）
    private static void 设图(RectTransform 矩形, Color 色)
    {
        if (矩形 == null) return;
        var 图 = 矩形.GetComponent<Image>();
        if (图 != null) 图.color = 色;
    }

    // 写文本的字号与颜色（皮肤里的字号口径就靠它落地；文本没接就跳过）
    private static void 设文本样式(TMP_Text 文本, int 字号, Color 色)
    {
        if (文本 == null) return;
        文本.fontSize = 字号;
        文本.color = 色;
    }
}
