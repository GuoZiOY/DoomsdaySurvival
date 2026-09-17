using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 技能子面板：角色面板 [技能] 栏页的内容（由 `角色面板` 注入 `内容区`；本组件**不被壳读任何字段**）。
//
// 上半 = **6 个技能槽**（96×150，横排；空槽画 2px 描边 + "空"），下半 = **已学技能列表**。
// 交互（用户点名"点选交换"）：
//   · 待装：点列表里的一条 → 它变成"待装"（该行底色 = 强调色，且置顶显示当前选择）；
//   · 装入：有待装时点某个技能槽 → **调 `成长管理器.切换入槽(槽索引, 标识)`**；
//     技能本来就在别的槽里 → 那个方法内部**两槽对调**（原先那格换到本格，不会白丢一个槽）；
//   · 卸下：**没有**待装时点一个已装槽 → 调 `成长管理器.卸下槽(槽索引)`；
//   · 再点一次同一条列表 = 取消待装。
//
// ★ 写入路径一律走 `成长管理器` 那两个方法，**本组件自己一个字都不写档案**：
//   槽位长度必须恒为 6、同名技能不能占两格、旧档要补齐 —— 这些约束全在那边收口
//   （见 `成长管理器.切换入槽 / 卸下槽` 的注释）；这里若自己 `战斗技能槽[i] = …`，上面三条就会各漏一处。
//
// 配色/字号/间距**全部内联在本文件自己的 [SerializeField] 字段里**（没有"皮肤"这个中间层）。
public sealed class 技能子面板 : MonoBehaviour
{
    // ================= 注入：`内容区` 由 角色面板 给 =================
    [SerializeField] private RectTransform 内容区;

    // ================= 颜色（与壳同一套十六进制值；没有共用参数类，同一色值各文件各写一份是有意的） =================
    [SerializeField] private Color 正文色 = new Color(0.8941f, 0.8745f, 0.8392f, 1f);   // #E4DFD6
    [SerializeField] private Color 次要色 = new Color(0.5412f, 0.5137f, 0.4706f, 1f);   // #8A8378
    [SerializeField] private Color 强调色 = new Color(0.7059f, 0.3333f, 0.2353f, 1f);   // #B4553C 唯一强调色
    [SerializeField] private Color 恢复色 = new Color(0.4314f, 0.5608f, 0.3843f, 1f);   // #6E8F62 增益/治疗语义色
    [SerializeField] private Color 边框色 = new Color(0.2275f, 0.2118f, 0.1882f, 1f);   // #3A3630 空槽描边
    [SerializeField] private Color 内容底色 = new Color(0.1020f, 0.0941f, 0.0824f, 1f); // #1A1815 槽/行的底
    [SerializeField] private Color 悬停底 = new Color(0.1490f, 0.1333f, 0.1255f, 1f);   // #262220 悬停底
    [SerializeField] private Color 选中底 = new Color(0.1490f, 0.1333f, 0.1255f, 1f);   // #262220 选中/待装底

    // ================= 字号 / 间距 / 尺寸 =================
    [SerializeField] private int 字号_正文 = 19;
    [SerializeField] private int 字号_小字 = 16;
    [SerializeField] private float 槽宽 = 96f;
    [SerializeField] private float 槽高 = 150f;
    [SerializeField] private float 槽间距 = 12f;
    [SerializeField] private float 行高 = 50f;   // 两行式列表行：上一行名称+主动/被动，下一行数值/精力/冷却/熟练
    [SerializeField] private float 段距 = 20f;
    [SerializeField] private float 段间隔 = 12f;

    // ================= 本组件自建的节点账本（幂等重建用） =================
    [SerializeField] private List<GameObject> 自建节点 = new List<GameObject>();

    // 槽数固定 6 —— 与 `成长管理器.技能槽数` 是同一个数（那边是权威，这里只是排版需要；
    //   改槽数要改那边，这里会跟着 `成长管理器.技能槽数` 自动对齐，见 建槽）
    private const string 空槽文字 = "空";
    private const string 提示文字 = "点列表选技能 → 点槽装入　·　无待装时点已装槽 = 卸下";

    private RectTransform 画布;
    private readonly List<槽件> 槽表 = new List<槽件>();
    private readonly List<行件> 行表 = new List<行件>();
    private readonly List<RectTransform> 列表列 = new List<RectTransform>();   // 列表的两列（行轮流落进这两列）
    private readonly List<string> 已学标识 = new List<string>();   // 缓存"列表当前显示的是哪几条" → 只在真的变了才重建行
    private string 待装标识;                                        // 点列表选中、还没落槽的那条（空 = 无）
    private bool 已建;

    // 一个技能槽：底 + 四边 2px 描边 + 名称 + 主动/被动标记 + 点击按钮
    private sealed class 槽件
    {
        public RectTransform 矩形;
        public Image 底;
        public TMP_Text 名称;
        public TMP_Text 标记;
    }

    // 列表的一行（两行文字）：上行 = 名称（品质色）+ 主动/被动；下行（小字）= 数值/精力/冷却/熟练等级
    private sealed class 行件
    {
        public RectTransform 矩形;
        public Image 底;
        public TMP_Text 名称;
        public TMP_Text 明细;
    }

    // ================= 注入入口 + 建树 =================

    public void 设内容区(RectTransform 区)
    {
        内容区 = 区;
        重建();
    }

    void Awake()
    {
        // 注入是唯一入口：壳还没给 `内容区` 时什么都不建（等 `设内容区` 那次调用）
        if (内容区 != null) 重建();
    }

    private void OnEnable()
    {
        if (已建) 刷新();
    }

    private void 重建()
    {
        if (内容区 == null) return;
        清掉自建();

        画布 = 新矩形("技能页内容", 内容区);
        定锚(画布, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f));
        画布.offsetMin = new Vector2(内边, 内边);
        画布.offsetMax = new Vector2(-内边, -栏目标题带高);

        建槽();
        建提示();
        建列表骨架();

        已建 = true;
        刷新();
    }

    private float 栏目标题带高 => 24f + 2f + 段距;
    private float 内边 => 段距 / 2f;

    private float 槽顶 => 0f;                                        // 技能槽从页面最上沿起
    private float 提示顶 => 槽高 + 段间隔;                            // 提示行在槽下面
    private float 列表标题顶 => 提示顶 + 字号_小字 + 8f + 段间隔;      // 列表小标题
    private float 列表行顶 => 列表标题顶 + 字号_小字 + 8f + 段间隔;    // 第一行

    // ================= 技能槽 =================

    // 6 个槽横排。槽数 = `成长管理器.技能槽数`（不写死 6：那边的常量是权威）。
    private void 建槽()
    {
        for (int i = 0; i < 成长管理器.技能槽数; i++)
        {
            var 矩形 = 新矩形("技能槽" + (i + 1), 画布);
            定锚(矩形, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            矩形.sizeDelta = new Vector2(槽宽, 槽高);
            矩形.anchoredPosition = new Vector2(i * (槽宽 + 槽间距), -槽顶);

            var 底 = 矩形.gameObject.AddComponent<Image>();
            底.color = 内容底色;
            底.raycastTarget = true;   // Button 需要一个可命中的图形，否则点不到（见 接按钮）
            卡边框(矩形, "槽");

            var 名称 = 文本("名称", 矩形, 字号_正文, 正文色, TextAlignmentOptions.Top, false);
            定锚(名称.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            名称.rectTransform.offsetMin = new Vector2(6f, -(6f + 槽高 * 0.45f));
            名称.rectTransform.offsetMax = new Vector2(-6f, -6f);

            var 标记 = 文本("标记", 矩形, 字号_小字, 次要色, TextAlignmentOptions.Top, false);
            定锚(标记.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            标记.rectTransform.offsetMin = new Vector2(6f, -(6f + 槽高 * 0.45f + 字号_小字 + 6f + 字号_正文 * 1.6f));
            标记.rectTransform.offsetMax = new Vector2(-6f, -(6f + 槽高 * 0.45f + 字号_小字 + 6f));

            var 钮 = 矩形.gameObject.AddComponent<Button>();
            钮.targetGraphic = 底;
            int 序 = i;   // 闭包捕获
            钮.onClick.AddListener(() => 点技能槽(序));
            接按钮(钮);

            槽表.Add(new 槽件 { 矩形 = 矩形, 底 = 底, 名称 = 名称, 标记 = 标记 });
        }
    }

    // ================= 列表 =================

    private void 建提示()
    {
        var 提示 = 文本("操作提示", 画布, 字号_小字, 次要色, TextAlignmentOptions.Right);
        定锚(提示.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        提示.rectTransform.offsetMin = new Vector2(0f, -提示顶 - 字号_小字 - 8f);
        提示.rectTransform.offsetMax = new Vector2(0f, -提示顶);
        面板基类.设文本(提示, 提示文字);
    }

    // 列表骨架 = 小标题 + 两条空槽（行本身在 刷列表 里按数据补建）。
    // 两列并排：一行 4 个字段横排的话会拖到 700px 宽，一列放 6 条就顶到板底 —— 两列各 6 条 = 12 条，
    //   比"单列 + 滚动"更符合这块板子"不滚动、一眼看完"的口径（真超过 12 条也只是往下排，不裁剪）。
    private void 建列表骨架()
    {
        var 小标题 = 文本("已学技能", 画布, 字号_小字, 次要色, TextAlignmentOptions.Left);
        定锚(小标题.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        小标题.rectTransform.offsetMin = new Vector2(0f, -列表标题顶 - 字号_小字 - 8f);
        小标题.rectTransform.offsetMax = new Vector2(0f, -列表标题顶);

        for (int i = 0; i < 2; i++)
        {
            var 容器 = 新矩形("列表列", 画布);
            // 左右按比例各占一半、上下按像素定（上沿 = 第一行的位置，高度 = 每列 6 条）
            定锚(容器, new Vector2(i * 0.5f, 1f), new Vector2((i + 1) * 0.5f, 1f), new Vector2(0f, 1f));
            容器.offsetMin = new Vector2(0f, -列表行顶 - 列表高);
            容器.offsetMax = new Vector2(0f, -列表行顶);
            列表列.Add(容器);
        }
    }

    private float 列表高 => 6f * 行高;   // 每列 6 条

    // ================= 刷新 =================

    public void 刷新()
    {
        if (!已建) return;
        var 玩家 = 当前档案();
        if (玩家 == null) return;

        刷槽(玩家);
        刷列表(玩家);
    }

    private static 玩家档案 当前档案()
        => ServiceRegistry.已注册<PlayerService>() ? ServiceRegistry.Get<PlayerService>()?.档案 : null;

    private static bool 取技能(DataService 数据, string 标识, out 技能数据 技能)
    {
        技能 = null;
        return 数据 != null && 数据.技能 != null && !string.IsNullOrEmpty(标识) && 数据.技能.TryGetValue(标识, out 技能);
    }

    private void 刷槽(玩家档案 玩家)
    {
        var 数据 = ServiceRegistry.已注册<DataService>() ? ServiceRegistry.Get<DataService>() : null;
        var 槽位 = 玩家.战斗技能槽;
        for (int i = 0; i < 槽表.Count; i++)
        {
            var 槽 = 槽表[i];
            if (槽.矩形 == null) continue;
            string 标识 = 槽位 != null && i < 槽位.Count ? 槽位[i] : null;
            取技能(数据, 标识, out var 技能);
            bool 有 = 技能 != null;
            // 空槽：写一个"空"；有待装时那个"空"用强调色，提示"点这里就装进来"
            面板基类.设文本(槽.名称, 有 ? 技能.名称 : 空槽文字);
            槽.名称.color = 有 ? 品质工具.颜色(技能.品质档) : (待装标识 != null ? 强调色 : 次要色);
            面板基类.设文本(槽.标记, 有 ? 主被动标记(技能) : "");
        }
    }

    private void 刷列表(玩家档案 玩家)
    {
        var 数据 = ServiceRegistry.已注册<DataService>() ? ServiceRegistry.Get<DataService>() : null;
        var 列表 = new List<string>();
        foreach (var 掌握 in 玩家.已学技能)
            if (掌握 != null && !string.IsNullOrEmpty(掌握.标识) && 取技能(数据, 掌握.标识, out _))
                列表.Add(掌握.标识);
        列表.Sort(string.CompareOrdinal);

        // 行只在"显示哪几条"真的变了才重建 —— 属性/生命变化事件也会把本页刷一遍，不该每次都销毁重造
        if (!同序(列表, 已学标识))
        {
            已学标识.Clear();
            已学标识.AddRange(列表);
            备列表行(列表);
        }

        // 贴文本 + 选中态（每次刷新都写，因为 待装标识 会变）
        for (int i = 0; i < 行表.Count; i++)
        {
            var 行 = 行表[i];
            if (行.矩形 == null) continue;
            bool 有 = i < 已学标识.Count;
            行.矩形.gameObject.SetActive(有);
            if (!有) continue;
            string 标识 = 已学标识[i];
            取技能(数据, 标识, out var 技能);
            var 掌握 = 玩家.已学技能.Find(掌 => 掌 != null && 掌.标识 == 标识);
            面板基类.设文本(行.名称, 列表行名称(技能));
            面板基类.设文本(行.明细, 列表行明细(技能, 掌握 != null ? 掌握.熟练等级 : 0));
            行.底.color = 标识 == 待装标识 ? 选中底 : 内容底色;
        }
        摆行();
    }

    private static bool 同序(List<string> 甲, List<string> 乙)
    {
        if (甲.Count != 乙.Count) return false;
        for (int i = 0; i < 甲.Count; i++) if (甲[i] != 乙[i]) return false;
        return true;
    }

    // 列表行：上面一行 = 名称（品质色）· 主动/被动，下面一行（小字）= 数值 · 精力 · 冷却 · 熟练等级。
    // 为什么拆两行：六个字段挤一行要 ~460px，而每列只有 550/2 = 275px 的一半——挤在一行会被
    //   `overflowMode = Ellipsis` 截掉尾巴，冷却和熟练度永远看不见（"名字后面一串省略号"那种坏观感）。
    //   ⚠ 仍然**不右对齐**：小字行是"名 + 值"连着读的行内文本（用户明令：数值不右对齐到天边）。
    private string 列表行名称(技能数据 技能)
    {
        // 这里不判空：`已学标识` 只收"在 数据.技能 里查得到"的那些（见 刷列表），所以传进来必非 null
        string 色 = ColorUtility.ToHtmlStringRGB(品质工具.颜色(技能.品质档));
        return $"<color=#{色}>{技能.名称}</color>　{(技能.被动 ? "被动" : "主动")}";
    }

    private string 列表行明细(技能数据 技能, int 熟练等级)
    {
        string 数 = 数值文本(技能);
        string 冷 = 技能.冷却 > 0 ? $"冷却 {技能.冷却}秒" : "无冷却";
        return $"{数}　精力 {技能.消耗精力}　{冷}　熟练 {熟练等级}/{玩家档案.熟练等级上限}";
    }

    // 数值那一列：攻击技能是伤害方式 + 数值（倍率/固定/附加），治疗是恢复量，被动没有数值。
    private static string 数值文本(技能数据 技能)
    {
        if (技能.被动)
        {
            if (技能.被动类型 == "增幅" && !string.IsNullOrEmpty(技能.增幅属性))
                return $"{技能.增幅属性} {(技能.增幅单位 == "%" ? $"+{技能.增幅值}%" : $"+{技能.增幅值}")}";
            return "被动机制";
        }
        if (技能.类别 == "治疗") return $"恢复 {Mathf.RoundToInt(技能.数值)}";
        switch (技能.伤害方式)
        {
            case "固定": return $"固定 {Mathf.RoundToInt(技能.数值)}";
            case "附加": return $"附加 {Mathf.RoundToInt(技能.数值)}";
            default: return $"倍率 {技能.数值:0.##}";
        }
    }

    // 槽里的主动/被动标记（槽只放得下一行）：主动 = 类别；被动 = 增幅/机制
    private static string 主被动标记(技能数据 技能)
    {
        if (!技能.被动) return 技能.类别;
        return 技能.被动类型 == "增幅" ? "被动 · 增幅" : "被动 · 机制";
    }

    // 按需要的条数把列表行补够（**只增不减**：多余的由 刷列表 用 SetActive(false) 收起）。
    // 为什么只增不减：这套行会被反复复用（刷新可能被属性/生命变化事件频繁触发），
    //   每次销毁重建是白扔垃圾；条数变了也只补差额。
    private void 备列表行(List<string> 列表)
    {
        while (行表.Count < 列表.Count)
        {
            var 容器 = 列表列[行表.Count % 列表列.Count];
            var 矩形 = 新矩形("技能行", 容器);
            var 底 = 矩形.gameObject.AddComponent<Image>();
            底.color = 内容底色;
            底.raycastTarget = true;
            float 内 = 6f;
            var 名称 = 文本("名称", 矩形, 字号_正文, 正文色, TextAlignmentOptions.MidlineLeft, false);
            定锚(名称.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            名称.rectTransform.offsetMin = new Vector2(内, -(内 + 字号_正文 + 2f));
            名称.rectTransform.offsetMax = new Vector2(-内, -内);
            var 明细 = 文本("明细", 矩形, 字号_小字, 次要色, TextAlignmentOptions.MidlineLeft, false);
            定锚(明细.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            明细.rectTransform.offsetMin = new Vector2(内, -(行高 - 2f));
            明细.rectTransform.offsetMax = new Vector2(-内, -(内 + 字号_正文 + 2f));

            int 序 = 行表.Count;
            var 钮 = 矩形.gameObject.AddComponent<Button>();
            钮.targetGraphic = 底;
            钮.onClick.AddListener(() => 点列表行(序));
            接按钮(钮);

            行表.Add(new 行件 { 矩形 = 矩形, 底 = 底, 名称 = 名称, 明细 = 明细 });
        }
    }

    // 摆行：两列各自自上而下按 行高 排（列内顺序 = 行表里的先后；索引 i → 列 i%2、行 i/2）
    private void 摆行()
    {
        for (int i = 0; i < 行表.Count; i++)
        {
            var 行 = 行表[i];
            if (行.矩形 == null) continue;
            float 顶 = (i / 列表列.Count) * 行高;
            设行带(行.矩形, 顶, 行高);
        }
    }

    // ================= 交互（点选交换） =================

    // 点列表一条：设为/取消"待装"。装的动作**不在这里**（等玩家点某个槽）——
    //   这是用户点名的"点列表选技能 → 点槽装入"两步式，避免"点一下就顶掉某个槽"的误操作。
    private void 点列表行(int 序)
    {
        if (序 < 0 || 序 >= 已学标识.Count) return;
        string 标识 = 已学标识[序];
        待装标识 = 待装标识 == 标识 ? null : 标识;
        刷新();
    }

    // 点技能槽：
    //   有待装 → 装入（**唯一写入口：`成长管理器.切换入槽`**，它内部处理"技能已在别的槽 → 两槽对调"）；
    //   无待装 → 卸下（**`成长管理器.卸下槽`**，写空串而不是 RemoveAt —— 槽位必须恒为 6）。
    //   两条路径都先判 `成长管理器` 在不在（编辑器非运行态取不到 PlayerService），不在就什么都不做。
    private void 点技能槽(int 槽索引)
    {
        var 玩家 = 当前档案();
        if (玩家?.成长管理 == null) return;
        if (!string.IsNullOrEmpty(待装标识))
        {
            玩家.成长管理.切换入槽(槽索引, 待装标识);
            待装标识 = null;
        }
        else
        {
            玩家.成长管理.卸下槽(槽索引);
        }
        刷新();
    }

    // 按钮的悬停/按下底色（与壳里三个 Tab 同一套：无动画、无缩放）
    private void 接按钮(Button 钮)
    {
        钮.transition = Selectable.Transition.ColorTint;
        钮.colors = new ColorBlock
        {
            normalColor = new Color(1f, 1f, 1f, 1f),
            highlightedColor = 悬停底,
            pressedColor = 悬停底,
            selectedColor = new Color(1f, 1f, 1f, 1f),
            disabledColor = 边框色,
            colorMultiplier = 1f,
            fadeDuration = 0f,
        };
    }

    // ================= 底色 / 边框 / 建节点的小工具 =================

    // 四边 2px 描边（拉伸锚 + sizeDelta，不用 Outline）
    private void 卡边框(RectTransform 父, string 前)
    {
        const float 框 = 2f;
        边框条(父, 前 + "上", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 框));
        边框条(父, 前 + "下", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 框));
        边框条(父, 前 + "左", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(框, 0f));
        边框条(父, 前 + "右", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(框, 0f));
    }

    private void 边框条(RectTransform 父, string 名, Vector2 锚最小, Vector2 锚最大, Vector2 轴心, Vector2 尺寸)
    {
        var 矩形 = 新矩形(名, 父);
        定锚(矩形, 锚最小, 锚最大, 轴心);
        矩形.anchoredPosition = Vector2.zero;
        矩形.sizeDelta = 尺寸;
        var 图 = 矩形.gameObject.AddComponent<Image>();
        图.color = 边框色;
        图.raycastTarget = false;
    }

    private void 清掉自建()
    {
        if (自建节点 == null) 自建节点 = new List<GameObject>();
        foreach (var 物体 in 自建节点)
        {
            if (物体 == null) continue;
            销毁(物体);
        }
        自建节点.Clear();
        槽表.Clear();
        行表.Clear();
        列表列.Clear();
        已学标识.Clear();
        待装标识 = null;
        画布 = null;
        已建 = false;
    }

    private RectTransform 新矩形(string 名, Transform 父, bool 记账 = true)
    {
        var 物体 = new GameObject(名, typeof(RectTransform));
        物体.transform.SetParent(父, false);
        if (记账) 自建节点.Add(物体);
        return (RectTransform)物体.transform;
    }

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

    private static void 设行带(RectTransform 行, float 顶, float 高)
    {
        定锚(行, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        行.offsetMin = new Vector2(0f, -顶 - 高);
        行.offsetMax = new Vector2(0f, -顶);
    }

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
