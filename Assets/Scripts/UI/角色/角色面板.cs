using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 角色面板：全局面板（一屏四栏：顶部身份条 + 左侧竖排导航 + 右侧内容区）。
//
// ★ 本文件是**整体重写**（原版是奇幻 RPG 遗留：顶部两枚 Tab + 属性页/装备背包页 两个 Inspector 接线子面板）。
//   重写的两条理由：
//     ① 旧版把"装备/背包"当角色面板的一个 Tab —— 末日版装备归 `持有面板`，两边功能重复（判定表见交付报告）；
//     ② 用户不手动搭 Unity → 布局必须**代码自建**：本组件 `Awake` 里把整棵节点树建出来，场景里只需要
//        有一个挂了本组件的空物体（连子物体都不用摆）。旧版那一堆 `[SerializeField]` 引用位已全部删除。
//   ⚠ 文件名与 `.cs.meta` 没动 —— 场景/预制体引用靠 meta 里的 GUID，改名或删文件会让引用变 Missing Script。
//
// 本批只到"能看见框架"：四个栏页都是**占位标题**，内容留到后面几批填（每个栏页一批）。
//
// 开合沿用 `面板管理器` 的既有约定，不自造一套：
//   · 打开 = `打开角色面板事件` → `面板管理器.显示(角色)`；
//   · 关闭 = HUD 上那颗统一关闭按钮 → 调本面板的 `回退()`（`面板基类.可关闭` 的判据就是"覆写了 取消文本"）。
//   · 项目**没有全局 Esc 回退键**（用户 2026-09-15 拍板），所以这里也不绑 Esc。
public sealed class 角色面板 : 面板基类
{
    // 四个栏目（顺序 = 左栏从上到下）。**没有"装备"栏** —— 装备归 `持有面板`，这里不重复一份。
    private static readonly string[] 栏目表 = { "属性", "技能", "知识", "天赋" };

    // 建出来的节点引用（全部由本组件代码创建，不需要 Inspector 拖任何东西）
    private TMP_Text 身份文本;
    private TMP_Text 等级文本;
    private RectTransform 经验填充;
    private readonly List<Button> 导航按钮 = new List<Button>();
    private TMP_Text 内容标题;

    private int 当前栏;
    private bool 已建;            // 节点树建完前不响应"根矩形尺寸变化"（那时算出来的可用宽度没意义）
    private RectTransform 内容区;
    private float 内容区基准左, 内容区基准右;   // 限宽前的左右偏移（让位从这两个基准推，见 内容区宽限）
    private float 内容区让位 = -1f;             // 已经让出去的量（-1 = 还没算过，第一次一定算）

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
        // 经验条宽度按比例写（父级 RectTransform 的中间锚点已"两头固定 X、上下拉满"）：
        // 右边用正值内缩 → offsetMax.x = -(1 - 比例) * 条宽，右边刚好落在比例位置。
        if (经验填充 != null && 经验填充.parent is RectTransform 条)
        {
            int 需要 = Mathf.Max(1, 玩家.升级所需经验);
            float 比例 = Mathf.Clamp01(玩家.经验 / (float)需要);
            经验填充.offsetMax = new Vector2(-(1f - 比例) * 条.rect.width, 0f);
        }
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

    private void 建节点树()
    {
        var 根 = (RectTransform)transform;

        // 整块底：不挂 LayoutGroup —— 三块区域各自锚定到 页边距 算出的位置。
        // 为什么手算锚点而不用布局组件：四栏是"固定栏宽 + 内容区居中且限宽"，
        // 布局组能做的（拉伸/换行）恰好是这里不想要的，反而要额外挂 LayoutElement 一个个去拧。
        底(根, 角色面板皮肤.面板底).name = "底";

        // —— 顶部身份条 ——
        var 身份条 = 新矩形("身份条", 根);
        顶部固定(身份条, 角色面板皮肤.身份条高, 角色面板皮肤.页边距);
        底(身份条, 角色面板皮肤.内容底);

        float 身份顶 = -角色面板皮肤.页边距 + (角色面板皮肤.页边距 - 角色面板皮肤.身份条高) * 0.5f;   // 身份条自身中心（pivot 0.5,0.5）
        身份文本 = 文本("身份文本", 身份条, 角色面板皮肤.字号_顶部标题, 角色面板皮肤.正文, TextAlignmentOptions.Left);
        定锚(身份文本.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        身份文本.rectTransform.anchoredPosition = new Vector2(角色面板皮肤.段距, 身份顶 + 角色面板皮肤.段距);
        身份文本.rectTransform.sizeDelta = new Vector2(420f, 26f);

        等级文本 = 文本("等级文本", 身份条, 角色面板皮肤.字号_正文, 角色面板皮肤.次要, TextAlignmentOptions.Right);
        定锚(等级文本.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
        等级文本.rectTransform.anchoredPosition = new Vector2(-角色面板皮肤.段距, 身份顶 + 角色面板皮肤.段距);
        等级文本.rectTransform.sizeDelta = new Vector2(160f, 24f);

        var 经验条 = 新矩形("经验条", 身份条);
        定锚(经验条, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));
        经验条.anchoredPosition = new Vector2(0f, 身份顶 - 角色面板皮肤.段距);
        经验条.sizeDelta = new Vector2(-2f * 角色面板皮肤.段距, 角色面板皮肤.进度条高);
        底(经验条, 角色面板皮肤.分隔线);            // 进度槽底 = 分隔线色（空的时候也要看得见条在哪）
        经验填充 = 底(经验条, 角色面板皮肤.强调);   // 填充 = 琥珀（本面板唯一强调色）
        经验填充.name = "填充";
        经验填充.anchorMin = Vector2.zero;          // 填充从左端起，宽度由 刷新 按经验比例写 offsetMax.x
        经验填充.anchorMax = new Vector2(0f, 1f);
        经验填充.offsetMin = Vector2.zero;
        经验填充.offsetMax = new Vector2(-经验条.rect.width, 0f);

        // —— 左侧竖排导航（属性 / 技能 / 知识 / 天赋）——
        var 左栏 = 新矩形("左栏", 根);
        var 左栏高 = 角色面板皮肤.行高 * 栏目表.Length + 角色面板皮肤.段距 * (栏目表.Length - 1);
        var 左栏左 = 角色面板皮肤.页边距;
        var 左栏顶 = -角色面板皮肤.页边距 - 角色面板皮肤.身份条高 - 角色面板皮肤.页边距;
        定锚(左栏, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        左栏.anchoredPosition = new Vector2(左栏左, 左栏顶);
        左栏.sizeDelta = new Vector2(角色面板皮肤.左栏宽, 左栏高);
        var 左栏底 = 底(左栏, 角色面板皮肤.内容底);
        左栏底.name = "底";

        for (int i = 0; i < 栏目表.Length; i++)
        {
            int 序 = i;   // 闭包捕获：循环变量必须另存一份，否则四个按钮全会切到最后一栏
            var 按钮 = 建导航按钮(左栏, 栏目表[i], 序);
            导航按钮.Add(按钮);
        }

        // —— 右侧内容区（本批只有栏目标题；内容留空给后面几批）——
        内容区 = 新矩形("内容区", 根);
        var 右区左 = 角色面板皮肤.页边距 + 角色面板皮肤.左栏宽 + 角色面板皮肤.页边距;
        var 右区顶 = 左栏顶;
        var 右区右 = -角色面板皮肤.页边距;
        定锚(内容区, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        内容区.offsetMin = new Vector2(右区左, 右区顶 - 左栏高);
        内容区.offsetMax = new Vector2(右区右, 右区顶);
        内容区基准左 = 右区左;
        内容区基准右 = 右区右;
        var 内容区底 = 底(内容区, 角色面板皮肤.内容底);
        内容区底.name = "底";

        内容标题 = 文本("栏目标题", 内容区, 角色面板皮肤.字号_栏目标题, 角色面板皮肤.正文, TextAlignmentOptions.Left);
        定锚(内容标题.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        内容标题.rectTransform.anchoredPosition = new Vector2(0f, -角色面板皮肤.页边距);
        内容标题.rectTransform.sizeDelta = new Vector2(0f, 22f);

        // 标题下的 1px 分隔线：靠"留白 + 一条细线"分层（不靠卡片/阴影）
        var 分隔 = 新矩形("分隔线", 内容区);
        定锚(分隔, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        分隔.anchoredPosition = new Vector2(0f, -角色面板皮肤.页边距 - 22f - 角色面板皮肤.段距);
        分隔.sizeDelta = new Vector2(0f, 1f);
        底(分隔, 角色面板皮肤.分隔线);

        已建 = true;
        内容区宽限(内容区);   // 建完再校正一次（建的过程中父矩形可能已经量得出来）
    }

    private Button 建导航按钮(RectTransform 左栏, string 栏目, int 序)
    {
        var 物体 = new GameObject(栏目, typeof(RectTransform), typeof(Image), typeof(Button));
        物体.transform.SetParent(左栏, false);
        var 矩形 = 物体.GetComponent<RectTransform>();
        定锚(矩形, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        矩形.anchoredPosition = new Vector2(0f, -序 * (角色面板皮肤.行高 + 角色面板皮肤.段距));
        矩形.sizeDelta = new Vector2(0f, 角色面板皮肤.行高);

        var 底图 = 物体.GetComponent<Image>();
        底图.color = new Color(0f, 0f, 0f, 0f);   // 未选中 = 透明（本面板不靠底色块区分，靠文字色）

        var 按钮 = 物体.GetComponent<Button>();
        按钮.targetGraphic = 底图;   // 不指定的话按钮点不到（Button 必须有一个可命中的图形）
        // 悬停/按下走 Button 自带的换色（**不缩放**：缩放反馈是明令禁止的）；
        // 选中态由本组件在 切换栏 里直接写 底图.color，两者不冲突（换色只在指针状态变化时应用）。
        按钮.transition = Selectable.Transition.ColorTint;
        按钮.colors = new ColorBlock
        {
            normalColor = new Color(0f, 0f, 0f, 0f),
            highlightedColor = 角色面板皮肤.行悬停或选中底,
            pressedColor = 角色面板皮肤.行悬停或选中底,
            selectedColor = new Color(0f, 0f, 0f, 0f),
            disabledColor = new Color(0f, 0f, 0f, 0f),
            colorMultiplier = 1f,
            fadeDuration = 0f,   // 无过渡动画
        };
        按钮.onClick.AddListener(() => 切换栏(序));

        var 标签 = 文本("标签", 矩形, 角色面板皮肤.字号_正文, 角色面板皮肤.次要, TextAlignmentOptions.Left);
        定锚(标签.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        标签.rectTransform.offsetMin = new Vector2(角色面板皮肤.段距, 0f);
        标签.rectTransform.offsetMax = Vector2.zero;
        return 按钮;
    }

    // 切栏：只改底色/文字色与右栏标题（点选，无动画，无缩放反馈）
    private void 切换栏(int 序)
    {
        当前栏 = Mathf.Clamp(序, 0, 栏目表.Length - 1);
        for (int i = 0; i < 导航按钮.Count; i++)
        {
            bool 选中 = i == 当前栏;
            var 图 = 导航按钮[i].targetGraphic as Image;
            if (图 != null) 图.color = 选中 ? 角色面板皮肤.行悬停或选中底 : new Color(0f, 0f, 0f, 0f);
            var 标签 = 导航按钮[i].GetComponentInChildren<TMP_Text>();
            面板基类.设文本(标签, 栏目表[i]);
            if (标签 != null) 标签.color = 选中 ? 角色面板皮肤.强调 : 角色面板皮肤.次要;
        }
        面板基类.设文本(内容标题, 栏目表[当前栏]);   // 本批右栏只有占位标题，内容分批填
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

    // ================= 建节点的几个小工具 =================

    // 建一个只有 RectTransform 的节点
    private static RectTransform 新矩形(string 名, Transform 父)
    {
        var 物体 = new GameObject(名, typeof(RectTransform));
        物体.transform.SetParent(父, false);
        return (RectTransform)物体.transform;
    }

    // 挂一张纯色实心矩形（Image 不给 sprite = 实心方块，没有圆角）
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
        文本组件.textWrappingMode = TextWrappingModes.NoWrap;   // 一屏四栏靠对齐分层：标题/标签一律不折行
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

    private static void 贴合父级(RectTransform 矩形)
    {
        矩形.anchorMin = Vector2.zero;
        矩形.anchorMax = Vector2.one;
        矩形.pivot = new Vector2(0.5f, 0.5f);
        矩形.offsetMin = Vector2.zero;
        矩形.offsetMax = Vector2.zero;
    }

    // 顶部固定条：左右各留 边距，钉在顶上，高度固定
    private static void 顶部固定(RectTransform 矩形, float 高, float 边距)
    {
        矩形.anchorMin = new Vector2(0f, 1f);
        矩形.anchorMax = new Vector2(1f, 1f);
        矩形.pivot = new Vector2(0.5f, 1f);
        矩形.offsetMin = new Vector2(边距, -高 - 边距);
        矩形.offsetMax = new Vector2(-边距, -边距);
    }

    // 内容区限宽：可用宽度超过 内容区最大宽 → 左右各让出 (可用宽 - 最大宽)/2，把内容挤到中间。
    // 为什么不用 LayoutElement.maxWidth：那需要给父级挂布局组，而本面板三块区域是手算锚点的
    //   （见 建节点树 的说明）；宽度是锚点算出来的，这里就地改一次最省事、也不会跟别的布局系统打架。
    // 为什么"已让出多少"要记在字段里：让位必须从**基准偏移**推出来，不能在上一次结果上再让一次 ——
    //   否则每重算一次就往里缩一圈，越算越窄。
    private void 内容区宽限(RectTransform 内容区)
    {
        var 父 = 内容区.parent as RectTransform;
        if (父 == null) return;
        float 基准可用 = 父.rect.width - (内容区基准左 + 内容区基准右);
        float 让 = 基准可用 > 角色面板皮肤.内容区最大宽 ? (基准可用 - 角色面板皮肤.内容区最大宽) * 0.5f : 0f;
        if (Mathf.Approximately(让, 内容区让位)) return;   // 尺寸没变就别重写（免得每帧白改布局）
        内容区让位 = 让;
        内容区.offsetMin = new Vector2(内容区基准左 + 让, 内容区.offsetMin.y);
        内容区.offsetMax = new Vector2(内容区基准右 - 让, 内容区.offsetMax.y);
    }

    // 根矩形尺寸变化时重算一次限宽（Awake 跑在第一帧布局之前，那一刻根矩形的宽高可能还是 0 → 算出来的"可用宽"是假的）。
    // 不做每帧轮询：只有真的变了才进来。
    private void OnRectTransformDimensionsChange()
    {
        if (!已建 || 内容区 == null) return;
        内容区宽限(内容区);
    }
}
