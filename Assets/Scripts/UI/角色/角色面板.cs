using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 角色面板：全局面板。一屏四栏 = **一块浮在安全屋画面上的"幸存者档案板"**
//   （板内：顶部身份条 + 顶部横排四个大字 Tab + 内容区；栏页内容分批填，本批到栏目标题占位）。
//
// ★ 本文件是**整体重写**（原版是奇幻 RPG 遗留：顶部两枚 Tab + 属性页/装备背包页 两个 Inspector 接线子面板）。
//   重写的两条理由：
//     ① 旧版把"装备/背包"当角色面板的一个 Tab —— 末日版装备归 `持有面板`，两边功能重复；
//     ② 用户不手动搭 Unity → 布局必须**代码自建**：本组件 `Awake` 里把整棵节点树建出来，场景里只需要
//        有一个挂了本组件的空物体（连子物体都不用摆）。旧版那一堆 `[SerializeField]` 引用位已全部删除。
//   ⚠ 文件名与 `.cs.meta` 没动 —— 场景/预制体引用靠 meta 里的 GUID，改名或删文件会让引用变 Missing Script。
//
// ★ 本版是**布局改版**（用户对第一版的差评："覆盖全屏、组件太小、AI 风味太重"）。改的是三件事，逻辑一行没动：
//     ① 不铺满屏：面板根保持满屏（`面板管理器` 要求），但里面再分三层 ——
//        满屏 **点击遮罩**(全透明，只负责挡住背后的界面) → 满屏 **65% 黑遮罩层**(纯视觉) → **板子**(居中定尺)。
//        板子宽高 = min(屏宽/屏高 × 比例, 上限)，见 板子尺寸()。
//     ② 组件放大：字号 19/24/30、行高 48、身份条 88（口径全在 `角色面板皮肤`，本文件不写死数字）。
//     ③ 去仪表盘味：**去掉左侧竖排导航**（那是文档站布局）→ 顶部横排四个大字 Tab；
//        列表行改"行内文本左对齐"（名称 + 附属信息），不再用"品质色条 + 数值右对齐到大边"。
//
// 开合沿用 `面板管理器` 的既有约定，不自造一套：
//   · 打开 = `打开角色面板事件` → `面板管理器.显示(角色)`；
//   · 关闭 = HUD 上那颗统一关闭按钮 → 调本面板的 `回退()`（`面板基类.可关闭` 的判据就是"覆写了 取消文本"）。
//   · 项目**没有全局 Esc 回退键**（用户 2026-09-15 拍板），所以这里也不绑 Esc。
public sealed class 角色面板 : 面板基类
{
    // 四个栏目（顺序 = 顶部 Tab 从左到右）。**没有"装备"栏** —— 装备归 `持有面板`，这里不重复一份。
    private static readonly string[] 栏目表 = { "属性", "技能", "知识", "天赋" };

    // 建出来的节点引用（全部由本组件代码创建，不需要 Inspector 拖任何东西）
    private TMP_Text 身份文本;
    private TMP_Text 等级文本;
    private RectTransform 身份条;
    private RectTransform 经验条;
    private RectTransform 板子;
    private readonly List<Image> 经验格 = new List<Image>();   // 经验条的格子（从左到右；按比例点亮前 N 格）
    private readonly List<Button> 导航按钮 = new List<Button>();
    private readonly List<RectTransform> 导航标签 = new List<RectTransform>();
    private readonly List<RectTransform> 导航下划线 = new List<RectTransform>();
    private TMP_Text 内容标题;
    private RectTransform 内容标题下划线;

    private int 当前栏;
    private bool 已建;   // 节点树建完前不响应"根矩形尺寸变化"（那时连板子都还没有）

    void Awake()
    {
        建节点树();

        // 刷新口径：属性变化（加点/装备变化都会发）与经验变化（升级/加经验）各刷一次顶部身份条。
        // 四个栏页的内容各自订阅自己关心的事件，本面板只负责"框"与顶部那一条。
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

    // 经验条：**一格一格地填**，不做连续填充。
    // 为什么按"点亮前 N 格"而不是改某一张图的宽度：规格要的是分段格子（每格 10 宽、间隔 2），
    //   格数由板宽算出来（见 建经验格）；这里只按比例决定亮到第几格 —— 空槽也要看得见（格子的底色就是槽底）。
    private void 刷经验格(玩家档案 玩家)
    {
        int 需要 = Mathf.Max(1, 玩家.升级所需经验);
        float 比例 = Mathf.Clamp01(玩家.经验 / (float)需要);
        int 亮 = 经验格.Count == 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(比例 * 经验格.Count), 0, 经验格.Count);
        for (int i = 0; i < 经验格.Count; i++)
            经验格[i].color = i < 亮 ? 角色面板皮肤.强调 : 角色面板皮肤.边框;
    }

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

    // ================= 节点树（全部代码创建） =================
    // 层级：根（满屏，透明）
    //         ├─ 点击遮罩（满屏，透明但接点击：挡住背后界面的鼠标事件）
    //         ├─ 遮罩层  （满屏，65% 黑；raycastTarget = false → 板子外面看得见安全屋，但点不穿）
    //         └─ 板子    （居中定尺）── 板底 + 四边 2px 边框 + 身份条 + Tab 行 + 2px 分隔线 + 内容区
    private void 建节点树()
    {
        var 根 = (RectTransform)transform;

        // 面板根保持满屏（`面板管理器` 建/摆面板的前提），但**根上不画任何东西** —— 视觉全在板子里，
        // 所以"覆盖全屏"这件事从"一整块实心底"变成了"一层半透明遮罩"。
        // 为什么"不让点到板子背后"不靠那层暗色遮罩（它的 raycastTarget = false，点了会穿过去）：
        //   `GraphicRaycaster` 只认 Graphic，`Button` 只认 targetGraphic —— 所以挡点击得有一张**全透明的图**。
        //   它守在最底层，板子里的按钮在它上面，先被命中；**板子外面**的点击才落到它身上被吃掉。
        遮罩图("点击遮罩", 根, new Color(0f, 0f, 0f, 0f), true);
        遮罩图("遮罩层", 根, 角色面板皮肤.板底遮罩, false);   // 65% 黑只负责画面变暗

        // 板子：不挂 LayoutGroup —— 里面三块区域各自锚定到 页边距 算出的位置。
        // 为什么手算锚点而不用布局组件：板子尺寸是"按屏算 + 有上限"的，布局组能做的（拉伸/换行）
        //   恰好是这里不想要的，反而要额外挂一堆 LayoutElement 去拧。
        板子 = 新矩形("板子", 根);
        板子.anchorMin = new Vector2(0.5f, 0.5f);
        板子.anchorMax = new Vector2(0.5f, 0.5f);
        板子.pivot = new Vector2(0.5f, 0.5f);
        板子.anchoredPosition = Vector2.zero;
        底(板子, 角色面板皮肤.面板底).name = "板底";

        // 四边 2px 边框：用四条实心矩形画，**不用 Outline 组件**
        //   （Outline 是按 1px 偏移画四份，线宽随缩放糊，也没法保证恒为 2px）。
        //   板底 面板底(#12100E) 与 身份条/内容区 内容底(#1A1815) 不同色 → 边框落在两者之间正好看得见。
        float 框 = 角色面板皮肤.板边框;
        边框条(板子, "上边框", new Vector2(0f, -框), new Vector2(0f, 框));      // 左上角起、整板宽、2 高
        边框条(板子, "下边框", new Vector2(0f, -框), new Vector2(0f, 0f));      // 左下角起、整板宽、2 高
        边框条(板子, "左边框", new Vector2(0f, 0f), new Vector2(框, 0f));       // 左上角起、2 宽、整板高
        边框条(板子, "右边框", new Vector2(-框, 0f), new Vector2(0f, 0f));      // 右上角起、2 宽、整板高

        // —— 顶部身份条 ——
        身份条 = 新矩形("身份条", 板子);
        身份条.anchorMin = new Vector2(0f, 1f);
        身份条.anchorMax = new Vector2(1f, 1f);
        身份条.pivot = new Vector2(0.5f, 1f);
        身份条.offsetMin = new Vector2(角色面板皮肤.页边距, -角色面板皮肤.页边距 - 角色面板皮肤.身份条高);
        身份条.offsetMax = new Vector2(-角色面板皮肤.页边距, -角色面板皮肤.页边距);
        底(身份条, 角色面板皮肤.内容底);

        // 标题与等级都贴身份条**上沿**（锚点固定，不参与板子尺寸计算）：
        //   标题 30 号在屏幕尺度上会明显大于"字号数字"，居中摆反而容易跟进度条打架。
        //   高度给"字号 + 8"：TMP 的行高略大于字号，框贴着字号高容易把字底切掉一点。
        float 身份内边 = 角色面板皮肤.段距;
        身份文本 = 文本("身份文本", 身份条, 角色面板皮肤.字号_标题, 角色面板皮肤.正文, TextAlignmentOptions.Left);
        顶内(身份文本.rectTransform, 身份内边, 身份内边, 角色面板皮肤.字号_标题 + 8f);

        // 等级用 20 号（不是正文 19）：它能跟标题同排但不抢视线。
        等级文本 = 文本("等级文本", 身份条, 角色面板皮肤.字号_等级, 角色面板皮肤.次要, TextAlignmentOptions.Right);
        顶内(等级文本.rectTransform, 身份内边, 身份内边, 角色面板皮肤.字号_等级 + 8f);

        // 经验条：贴身份条**下沿**，左右各留 身份内边（宽度随板宽变 → 格数在 顶层尺寸变化 里重算）
        经验条 = 新矩形("经验条", 身份条);
        经验条.anchorMin = new Vector2(0f, 0f);
        经验条.anchorMax = new Vector2(1f, 0f);
        经验条.pivot = new Vector2(0.5f, 0f);
        经验条.offsetMin = new Vector2(身份内边, 身份内边);
        经验条.offsetMax = new Vector2(-身份内边, 身份内边 + 角色面板皮肤.进度格高);

        // —— 顶部横排四个大字 Tab（**取代原来的左侧竖排导航**）——
        // 为什么不用 HorizontalLayoutGroup：Tab 宽度要等于各自标签的宽度（下划线才贴得住字），
        //   而面板是 Awake 里代码建的，"布局组第一次算出来的宽度"不一定就是最终宽度；
        //   这里直接读 TMP 的 preferredWidth 摆位置，摆完立刻能看见正确结果，不留一帧的错位。
        float Tab行顶 = 角色面板皮肤.页边距 + 角色面板皮肤.身份条高 + 角色面板皮肤.段距;
        for (int i = 0; i < 栏目表.Length; i++)
        {
            int 序 = i;   // 闭包捕获：循环变量必须另存一份，否则四个按钮全会切到最后一栏
            导航按钮.Add(建导航按钮(板子, 栏目表[i], 序, Tab行顶));
        }

        // —— Tab 行下方的 2px 分隔线（**整板宽**，不随 Tab 文字长度）——
        var 分隔 = 新矩形("分隔线", 板子);
        定锚(分隔, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        分隔.offsetMin = new Vector2(角色面板皮肤.页边距, -Tab行顶 - 角色面板皮肤.字号_Tab - 角色面板皮肤.段距 - 角色面板皮肤.板边框);
        分隔.offsetMax = new Vector2(-角色面板皮肤.页边距, -Tab行顶 - 角色面板皮肤.字号_Tab - 角色面板皮肤.段距);
        底(分隔, 角色面板皮肤.边框);

        // —— 内容区：**占满分隔线以下的剩余高度**（"内容区占满剩余高度"就落在这里）——
        //   上面锚在分隔线下方、下面锚在板子下沿的页边距上 → 板子多高它多高，不用另算一个"可用高度"。
        var 内容区 = 新矩形("内容区", 板子);
        内容区.anchorMin = new Vector2(0f, 0f);
        内容区.anchorMax = new Vector2(1f, 1f);
        内容区.pivot = new Vector2(0.5f, 0.5f);
        内容区.offsetMin = new Vector2(角色面板皮肤.页边距, 角色面板皮肤.页边距);
        内容区.offsetMax = new Vector2(-角色面板皮肤.页边距, -(Tab行顶 + 角色面板皮肤.字号_Tab + 角色面板皮肤.段距 + 角色面板皮肤.板边框));
        底(内容区, 角色面板皮肤.内容底).name = "底";

        内容标题 = 文本("栏目标题", 内容区, 角色面板皮肤.字号_栏目标题, 角色面板皮肤.正文, TextAlignmentOptions.Left);
        顶内(内容标题.rectTransform, 角色面板皮肤.页边距, 角色面板皮肤.页边距, 角色面板皮肤.字号_栏目标题 + 8f);
        面板基类.设文本(内容标题, 栏目表[0]);   // 与本批"栏目标题占位"的口径一致（切栏时再改）

        // 标题下的 2px 分隔线：靠"留白 + 一条线"分层（不靠卡片/阴影）；下划线尺寸随标题走，在 摆标题 里算
        内容标题下划线 = 底(内容区, 角色面板皮肤.强调);
        内容标题下划线.name = "标题下划线";

        已建 = true;
        顶层尺寸变化();   // 建完再校正一次：Awake 跑在第一帧布局前，那一刻根矩形的宽高可能还是 0
    }

    // 建一个 Tab：底图 + 标签 + 下方 2px 下划线（选中才显）。尺寸在 摆Tab 里按标签宽度定。
    private Button 建导航按钮(RectTransform 父, string 栏目, int 序, float 行顶)
    {
        var 物体 = new GameObject(栏目, typeof(RectTransform), typeof(Image), typeof(Button));
        物体.transform.SetParent(父, false);
        var 矩形 = 物体.GetComponent<RectTransform>();
        定锚(矩形, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        矩形.anchoredPosition = new Vector2(角色面板皮肤.页边距, -行顶);

        var 底图 = 物体.GetComponent<Image>();
        底图.color = new Color(0f, 0f, 0f, 0f);   // 未选中 = 透明（未选中不靠底色块区分，靠文字色）

        var 按钮 = 物体.GetComponent<Button>();
        按钮.targetGraphic = 底图;   // 不指定的话按钮点不到（Button 必须有一个可命中的图形）
        // 悬停/按下走 Button 自带的换色（**不缩放**：缩放反馈是明令禁止的）；
        // 悬停**只换底**（#262220），选中态由本组件在 切换栏 里直接写 底图.color，两者不冲突。
        按钮.transition = Selectable.Transition.ColorTint;
        按钮.colors = new ColorBlock
        {
            normalColor = new Color(0f, 0f, 0f, 0f),
            highlightedColor = 角色面板皮肤.行悬停或选中底,
            pressedColor = 角色面板皮肤.行悬停或选中底,
            selectedColor = new Color(0f, 0f, 0f, 0f),
            disabledColor = new Color(0f, 0f, 0f, 0f),
            colorMultiplier = 1f,
            fadeDuration = 0f,   // 无过渡动画（用户明令：无任何动画）
        };
        按钮.onClick.AddListener(() => 切换栏(序));

        var 标签 = 文本("标签", 矩形, 角色面板皮肤.字号_Tab, 角色面板皮肤.次要, TextAlignmentOptions.Left);
        标签.text = 栏目;   // 先填上：preferredWidth 要靠它算（见 摆Tab）
        定锚(标签.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        标签.rectTransform.sizeDelta = new Vector2(10f, 0f);
        导航标签.Add(标签.rectTransform);

        // 选中下划线：**2px 强调色**，宽度 = 标签宽（所以"下划线认得字"而不是跨满整个 Tab 按钮）
        var 线 = 底(矩形, 角色面板皮肤.强调);
        线.name = "下划线";
        定锚(线, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
        线.sizeDelta = new Vector2(10f, 角色面板皮肤.板边框);
        导航下划线.Add(线);
        return 按钮;
    }

    // 切栏：只改底色/文字色/下划线显隐与内容区标题（点选，无动画，无缩放反馈）
    private void 切换栏(int 序)
    {
        当前栏 = Mathf.Clamp(序, 0, 栏目表.Length - 1);
        for (int i = 0; i < 导航按钮.Count; i++)
        {
            bool 选中 = i == 当前栏;
            var 图 = 导航按钮[i].targetGraphic as Image;
            if (图 != null) 图.color = new Color(0f, 0f, 0f, 0f);   // 底色始终透明：悬停色由 Button 的 transition 管
            var 标签 = 导航标签[i].GetComponent<TMP_Text>();
            面板基类.设文本(标签, 栏目表[i]);
            if (标签 != null) 标签.color = 选中 ? 角色面板皮肤.正文 : 角色面板皮肤.次要;            if (i < 导航下划线.Count) 导航下划线[i].gameObject.SetActive(选中);
        }
        面板基类.设文本(内容标题, 栏目表[当前栏]);   // 本批内容区只有栏目标题占位，内容分批填
        摆标题();   // 标题文字可能换长度（"属性"→"知识"）→ 下划线要跟着重摆
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

    // ================= 尺寸计算（板子不铺满屏 / Tab 与进度格随板宽重算） =================

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
        建经验格();
        刷新(null);   // 格数/条宽都变了 → 重新点亮一次（读当前档案；还没开档就什么都不做）
    }

    // 板子尺寸：宽 = min(屏宽 × 板宽比例, 板宽上限)，高 = min(屏高 × 板高比例, 板高上限)，屏幕居中。
    // 为什么取 min 而不是单纯按比例：比例是"覆盖大半"的观感，上限是"大屏上别拉成一张网页"的硬边界。
    private void 板子尺寸()
    {
        var 父 = 板子 != null ? 板子.parent as RectTransform : null;
        if (父 == null) return;
        float 屏宽 = 父.rect.width;
        float 屏高 = 父.rect.height;
        if (屏宽 <= 1f || 屏高 <= 1f) return;   // 还没量出屏幕尺寸（第一帧布局前）→ 保持原样，等下一次回调
        板子.sizeDelta = new Vector2(
            Mathf.Min(屏宽 * 角色面板皮肤.板宽比例, 角色面板皮肤.板宽上限),
            Mathf.Min(屏高 * 角色面板皮肤.板高比例, 角色面板皮肤.板高上限));
    }

    // Tab 行：从左到右顺着摆，间距 **Tab 间距(=段距 20)**。宽度取标签的 preferredWidth
    //   （所以下划线正好压在字下面）；每次重摆都先按当前字号重算一遍 preferredWidth，缩放/换分辨率后不会用旧值。
    private void 摆Tab()
    {
        if (板子 == null) return;
        float 行顶 = 角色面板皮肤.页边距 + 角色面板皮肤.身份条高 + 角色面板皮肤.段距;
        float x = 角色面板皮肤.页边距;
        for (int i = 0; i < 导航按钮.Count; i++)
        {
            var 标签 = 导航标签[i].GetComponent<TMP_Text>();
            if (标签 != null) 标签.fontSize = 角色面板皮肤.字号_Tab;   // 触发 preferredWidth 按当前字号重算
            float 文字宽 = 标签 != null ? 标签.preferredWidth : 0f;
            if (文字宽 <= 1f) 文字宽 = 40f;   // TMP 还没量出文字（极端情况）→ 给一个能点得到的宽度
            var 矩形 = (RectTransform)导航按钮[i].transform;
            矩形.sizeDelta = new Vector2(文字宽, 角色面板皮肤.字号_Tab);
            矩形.anchoredPosition = new Vector2(x, -行顶);
            导航标签[i].sizeDelta = new Vector2(文字宽, 0f);
            导航下划线[i].sizeDelta = new Vector2(文字宽, 角色面板皮肤.板边框);
            x += 文字宽 + 角色面板皮肤.段距;
        }
    }

    // 内容区标题的下划线：宽度 = 标题文字宽（与 Tab 下划线同一口径：线认得字，不跨满整行）。
    private void 摆标题()
    {
        if (内容标题 == null || 内容标题下划线 == null) return;
        内容标题.fontSize = 角色面板皮肤.字号_栏目标题;
        float 宽 = 内容标题.preferredWidth;
        if (宽 <= 1f) 宽 = 40f;
        定锚(内容标题下划线, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        内容标题下划线.anchoredPosition = new Vector2(角色面板皮肤.页边距, -(角色面板皮肤.页边距 + 角色面板皮肤.字号_栏目标题 + 8f + 12f));
        内容标题下划线.sizeDelta = new Vector2(宽, 角色面板皮肤.板边框);
    }

    // 经验条的分段格子：条宽一变，格数就变 → 清掉重铺。
    // 为什么清掉重建而不是只改填充宽度：规格要的是**分段格子**（每格 10 宽、间隔 2），
    //   格数是"条能塞下几格"的整数，不是连续比例；条变宽时多出来的格必须真的存在。
    private void 建经验格()
    {
        if (经验条 == null) return;
        for (int i = 0; i < 经验格.Count; i++)
            if (经验格[i] != null) Destroy(经验格[i].gameObject);
        经验格.Clear();

        float 条宽 = 经验条.rect.width;
        if (条宽 <= 1f) return;
        int 格数 = Mathf.FloorToInt((条宽 + 角色面板皮肤.进度格间隔) / (角色面板皮肤.进度格宽 + 角色面板皮肤.进度格间隔));
        if (格数 <= 0) return;
        for (int i = 0; i < 格数; i++)
        {
            var 格 = 新矩形("经验格", 经验条);
            定锚(格, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f));
            格.anchoredPosition = new Vector2(i * (角色面板皮肤.进度格宽 + 角色面板皮肤.进度格间隔), 0f);
            格.sizeDelta = new Vector2(角色面板皮肤.进度格宽, 0f);
            var 图 = 格.gameObject.AddComponent<Image>();
            图.color = 角色面板皮肤.边框;   // 先按"空槽"画；点亮比例由 刷经验格 写
            图.raycastTarget = false;
            经验格.Add(图);
        }
    }

    // ================= 建节点的几个小工具 =================

    // 建一个只有 RectTransform 的节点
    private static RectTransform 新矩形(string 名, Transform 父)
    {
        var 物体 = new GameObject(名, typeof(RectTransform));
        物体.transform.SetParent(父, false);
        return (RectTransform)物体.transform;
    }

    // 挂一张纯色实心矩形并铺满父级（Image 不给 sprite = 实心方块，没有圆角）
    private static RectTransform 底(Transform 父, Color 色)
    {
        var 物体 = new GameObject("底", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(父, false);
        var 矩形 = (RectTransform)物体.transform;
        贴合父级(矩形);
        var 图 = 物体.GetComponent<Image>();
        图.color = 色;
        图.raycastTarget = false;
        return 矩形;
    }

    // 铺满父级的实心矩形（遮罩用）：`接点击` 决定它是"吃掉板子外面的点击"（点击遮罩）
    // 还是"只负责变暗"（暗色遮罩层，点了要能穿到下面那层去）。
    private static RectTransform 遮罩图(string 名, Transform 父, Color 色, bool 接点击)
    {
        var 物体 = new GameObject(名, typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(父, false);
        var 矩形 = (RectTransform)物体.transform;
        贴合父级(矩形);
        var 图 = 物体.GetComponent<Image>();
        图.color = 色;
        图.raycastTarget = 接点击;
        return 矩形;
    }

    // 板子的一条边框：角锚点 + offsetMin/offsetMax = 矩形在板内的绝对范围（左上角为原点，Y 向下）。
    // 为什么把范围直接写成 offset：角锚点（anchorMin == anchorMax）时 offsetMin/offsetMax 就是矩形相对锚点的
    //   左下/右上，用它表达"整板宽、2 高"这类跨边矩形最直观，不用再倒推 pivot 与 sizeDelta 的组合。
    private static void 边框条(RectTransform 板子, string 名, Vector2 左下, Vector2 右上)
    {
        var 矩形 = 新矩形(名, 板子);
        定锚(矩形, Vector2.zero, Vector2.zero, Vector2.zero);   // 锚 + 轴心都取板内左下角 = 坐标系原点
        矩形.offsetMin = 左下;
        矩形.offsetMax = 右上;
        var 图 = 矩形.gameObject.AddComponent<Image>();
        图.color = 角色面板皮肤.边框;
        图.raycastTarget = false;
    }

    // 建一段文本。字体**不硬编码资源路径**：项目已有 TMP 设置，取它的默认字体资产
    // （换字体只改 TMP Settings 一处，不用回来改这个文件）。
    private static TMP_Text 文本(string 名, Transform 父, float 字号, Color 色, TextAlignmentOptions 对齐)
    {
        var 物体 = new GameObject(名, typeof(RectTransform), typeof(TextMeshProUGUI));
        物体.transform.SetParent(父, false);
        var 文本组件 = 物体.GetComponent<TextMeshProUGUI>();
        文本组件.font = TMP_Settings.defaultFontAsset;
        文本组件.fontSize = 字号;
        文本组件.color = 色;
        文本组件.alignment = 对齐;
        文本组件.textWrappingMode = TextWrappingModes.NoWrap;   // 档案板靠对齐分层：标题/标签一律不折行
        文本组件.overflowMode = TextOverflowModes.Ellipsis;
        文本组件.raycastTarget = false;   // 文本不拦点击（点击一律落到按钮上）
        return 文本组件;
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
