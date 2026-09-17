using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 技能子面板：角色面板 [技能] 栏页的内容（由 `角色面板` 注入 `内容区`；本组件**不被壳读任何字段**）。
//
// 上半 = **6 个技能槽**（205×118，横排；空槽画 2px 描边 + "空"），下半 = **已学技能列表**（4 列，每列 9 行）。
//   槽里三行：`名称`（品质色）· `标记`（主动/被动）· `明细`（精力 / 冷却）。
//   列表每行两行：上行 = 名称 + 主动/被动 + 「已装 N」，下行（小字）= 数值 · 精力 · 冷却 · 熟练。
//
// 交互（用户点名"点选交换"）：
//   · 待装：点列表里的一条 → 它变成"待装"（该行底色 = 选中底）**且 6 个空槽一起亮成"待装底"**，
//     提示行同时写出"待装：X → 点任意技能槽装入"（原来只有行变色，看不出"现在该点哪儿"）；
//   · 装入：有待装时点某个技能槽 → **调 `成长管理器.切换入槽(槽索引, 标识)`**；
//     技能本来就在别的槽里 → 那个方法内部**两槽对调**（原先那格换到本格，不会白丢一个槽）；
//   · 卸下：**没有**待装时点一个已装槽 → 调 `成长管理器.卸下槽(槽索引)`；
//   · 再点一次同一条列表 = 取消待装。
//
// ★ 写入路径一律走 `成长管理器` 那两个方法，**本组件自己一个字都不写档案**：
//   槽位长度必须恒为 6、同名技能不能占两格、旧档要补齐 —— 这些约束全在那边收口；
//   这里若自己 `战斗技能槽[i] = …`，上面三条就会各漏一处。
//
// ★ 本批（刀82）口径：**骨架 100% 读预制体，本组件只建"条数随数据变"的列表行**。
//   现成的（只按名字找出来用）：`技能页内容` / `技能槽1..6`（含 `名称` `标记` `明细` 三行）/ `操作提示` /
//   `已学技能`（列表小标题）/ `列表列` ×4。运行时造的：**列表行**（已学技能条数不固定）。
//   ⚠ 名字就是契约：路径写死在 `绑定位()` 里；重命名节点要同步改那里（缺了会打 LogError，不静默）。
public sealed class 技能子面板 : MonoBehaviour
{
    // ================= 注入：`内容区` 由 角色面板 给（预制体里也烘好了） =================
    [SerializeField] private RectTransform 内容区;

    // ================= 颜色（只有"随状态变"的才在代码里） =================
    [SerializeField] private Color 正文色 = new Color(0.8941f, 0.8745f, 0.8392f, 1f);   // #E4DFD6
    [SerializeField] private Color 次要色 = new Color(0.5412f, 0.5137f, 0.4706f, 1f);   // #8A8378 行明细 / 空槽字
    [SerializeField] private Color 强调色 = new Color(0.7059f, 0.3333f, 0.2353f, 1f);   // #B4553C 正负标记没有，这里只给"待装"用
    [SerializeField] private Color 悬停底 = new Color(0.0863f, 0.0784f, 0.0706f, 1f);   // #161412 列表行悬停
    [SerializeField] private Color 选中底 = new Color(0.2118f, 0.1529f, 0.1137f, 1f);   // #36271D 选中/待装（暖，与悬停的冷灰分得开）
    [SerializeField] private Color 待装底 = new Color(0.1647f, 0.1294f, 0.1020f, 1f);   // #2A211A 有待装时**空槽**的底（"点这里"）

    // ================= 字号 / 间距 =================
    [SerializeField] private int 字号_正文 = 19;
    [SerializeField] private int 字号_小字 = 16;
    [SerializeField] private float 行高 = 48f;              // 列表行两行式（名称行 + 明细行）
    [SerializeField] private float 列表高兜底 = 468f;        // 量不到 `列表列` 高度时用它（= 预制体里烘的值）

    private const string 空槽文字 = "空";
    private const string 待装提示 = "点列表选技能 → 点槽装入　·　无待装时点已装槽 = 卸下";

    // 运行时造的列表行（**不序列化**：不属于资产）
    private readonly List<GameObject> 自建行 = new List<GameObject>();

    private RectTransform 画布;
    private TMP_Text 提示行, 列表标题;
    private readonly List<槽件> 槽表 = new List<槽件>();
    private readonly List<行件> 行表 = new List<行件>();
    private readonly List<RectTransform> 列表列 = new List<RectTransform>();
    private readonly List<string> 已学标识 = new List<string>();   // 列表当前显示的是哪几条 → 只在真的变了才重建行
    private readonly List<string> 缺的节点 = new List<string>();
    private string 待装标识;                                        // 点列表选中、还没落槽的那条（空 = 无）
    private int 当前列数 = 1;                                        // 这次刷新用了几列（列数变了要把行挪容器）
    private bool 已绑;

    // 一个技能槽：底（挂在槽自己身上）+ 名称 + 主动/被动标记 + 明细；`常态底` = 预制体里烘的那个底（收起高亮要还原成它）
    private sealed class 槽件
    {
        public RectTransform 矩形;
        public Image 底;
        public Color 常态底;
        public TMP_Text 名称;
        public TMP_Text 标记;
        public TMP_Text 明细;
    }

    // 列表的一行（两行文字）：上行 = 名称（品质色）+ 主动/被动 + 已装槽号；下行（小字）= 数值/精力/冷却/熟练
    private sealed class 行件
    {
        public RectTransform 矩形;
        public Image 底;
        public Color 常态底;      // 行底常态色（选中/取消选中都要还原成它）
        public TMP_Text 名称;
        public TMP_Text 明细;
    }

    // ================= 注入入口 =================

    public void 设内容区(RectTransform 区)
    {
        if (区 == null || 区 == 内容区) return;
        内容区 = 区;
        已绑 = false;
        if (isActiveAndEnabled) 刷新();
    }

    void Awake() { 刷新(); }

    private void OnEnable() { 刷新(); }   // 切栏点亮 → 自己刷一次

    // ================= 绑定位（只找、不建骨架） =================

    private void 绑定位()
    {
        if (已绑) return;
        已绑 = true;
        清掉自建行();
        槽表.Clear();
        行表.Clear();
        列表列.Clear();
        已学标识.Clear();
        缺的节点.Clear();
        待装标识 = null;

        画布 = 子矩形(内容区, "技能页内容");
        if (画布 == null) { 缺("技能页内容"); 报缺(); return; }

        绑槽();
        提示行 = 子文本(画布, "操作提示");
        列表标题 = 子文本(画布, "已学技能");
        if (提示行 == null) 缺("操作提示");
        if (列表标题 == null) 缺("已学技能");
        for (int i = 0; i < 画布.childCount; i++)
        {
            var 子 = (RectTransform)画布.GetChild(i);
            if (子.name == "列表列") 列表列.Add(子);
        }
        if (列表列.Count == 0) 缺("列表列");
        报缺();
    }

    // 6 个槽：节点名 `技能槽1..技能槽6`（槽数 = `成长管理器.技能槽数`；预制体里就是 6 个）。
    private void 绑槽()
    {
        for (int i = 1; i <= 成长管理器.技能槽数; i++)
        {
            var 矩形 = 子矩形(画布, "技能槽" + i);
            if (矩形 == null) { 缺("技能槽" + i); continue; }
            var 底 = 矩形.GetComponent<Image>();
            槽表.Add(new 槽件
            {
                矩形 = 矩形,
                底 = 底,
                常态底 = 底 != null ? 底.color : Color.clear,   // 记住**预制体里调的**底色，收起高亮时还原它
                名称 = 子文本(矩形, "名称"),
                标记 = 子文本(矩形, "标记"),
                明细 = 子文本(矩形, "明细"),
            });
        }
    }

    // ================= 刷新 =================

    public void 刷新()
    {
        绑定位();
        if (画布 == null) return;
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
        bool 有待装 = !string.IsNullOrEmpty(待装标识);
        for (int i = 0; i < 槽表.Count; i++)
        {
            var 槽 = 槽表[i];
            if (槽.矩形 == null) continue;
            string 标识 = 槽位 != null && i < 槽位.Count ? 槽位[i] : null;
            取技能(数据, 标识, out var 技能);
            bool 有 = 技能 != null;

            面板基类.设文本(槽.名称, 有 ? 技能.名称 : 空槽文字);
            // 空槽的字：有待装 → 强调色（"就往这儿放"）；没待装 → 次要色。已装 → 品质色。
            槽.名称.color = 有 ? 品质工具.颜色(技能.品质档) : (有待装 ? 强调色 : 次要色);
            面板基类.设文本(槽.标记, 有 ? 主被动标记(技能) : "");
            面板基类.设文本(槽.明细, 有 ? 明细行(技能) : "");

            // 底：**空槽 + 有待装** 才亮成待装底（6 个槽一起亮 → 一眼看出"现在可以点任何一格"）；
            //     其余一律还原成预制体里调的常态底（不自己在代码里定"槽底色"）。
            if (槽.底 != null) 槽.底.color = (!有 && 有待装) ? 待装底 : 槽.常态底;
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
            当前列数 = 列数(列表.Count);
            备列表行(列表, 数据);
        }

        // ★ 熟练等级一次查成字典（刀82 优化）：原来是**每行** `已学技能.Find(...)` → n 行 = n × n 次扫描。
        var 熟练表 = new Dictionary<string, int>();
        foreach (var 掌握 in 玩家.已学技能)
            if (掌握 != null && !string.IsNullOrEmpty(掌握.标识)) 熟练表[掌握.标识] = 掌握.熟练等级;

        // ★ 已装槽号表：列表行要显示「已装 3」（原来只有"点列表→点槽"才知道装没装）
        var 已装表 = new Dictionary<string, int>();
        var 槽位 = 玩家.战斗技能槽;
        if (槽位 != null)
            for (int i = 0; i < 槽位.Count; i++)
                if (!string.IsNullOrEmpty(槽位[i]) && !已装表.ContainsKey(槽位[i])) 已装表[槽位[i]] = i + 1;

        for (int i = 0; i < 行表.Count; i++)
        {
            var 行 = 行表[i];
            if (行.矩形 == null) continue;
            bool 有 = i < 已学标识.Count;
            if (行.矩形.gameObject.activeSelf != 有) 行.矩形.gameObject.SetActive(有);
            if (!有) continue;
            string 标识 = 已学标识[i];
            取技能(数据, 标识, out var 技能);
            已装表.TryGetValue(标识, out int 槽号);
            熟练表.TryGetValue(标识, out int 熟练);
            面板基类.设文本(行.名称, 列表行名称(技能, 槽号));
            面板基类.设文本(行.明细, 列表行明细(技能, 熟练));
            行.底.color = 标识 == 待装标识 ? 选中底 : 常态行底(行);
        }
        摆行();
        刷标题行(玩家, 列表.Count, 已装表.Count);
    }

    // 列表小标题 = **活的小结**（刀82）：条数 + 已装槽数 + 待装是哪一条（原来是空文本，什么都没显示）
    private void 刷标题行(玩家档案 玩家, int 条数, int 已装数)
    {
        面板基类.设文本(列表标题, $"已学技能 {条数} 条　已装 {已装数}/{成长管理器.技能槽数}");
        面板基类.设文本(提示行, string.IsNullOrEmpty(待装标识)
            ? 待装提示
            : $"待装：{技能名(待装标识)}　→　点任意技能槽装入　·　再点该行取消");
        if (提示行 != null) 提示行.color = string.IsNullOrEmpty(待装标识) ? 次要色 : 强调色;
    }

    // 两个标识表是否逐项相同（用来判断"列表该不该重建"）
    private static bool 同序(List<string> 甲, List<string> 乙)
    {
        if (甲.Count != 乙.Count) return false;
        for (int i = 0; i < 甲.Count; i++) if (甲[i] != 乙[i]) return false;
        return true;
    }

    // ================= 列表行文案 =================

    // 列表行名称：品质色名 + 主动/被动 + （已装 → 「已装 N」）。
    private string 列表行名称(技能数据 技能, int 槽号)
    {
        // 这里不判空：`已学标识` 只收"在 数据.技能 里查得到"的那些（见 刷列表），所以传进来必非 null
        string 色 = ColorUtility.ToHtmlStringRGB(品质工具.颜色(技能.品质档));
        string 装 = 槽号 > 0 ? $"　<color=#{ColorUtility.ToHtmlStringRGB(强调色)}>已装 {槽号}</color>" : "";
        return $"<color=#{色}>{技能.名称}</color>　{(技能.被动 ? "被动" : "主动")}{装}";
    }

    // 列表行明细（小字）：数值 · 精力 · 冷却 · 熟练。
    // 为什么拆两行：六个字段挤一行要 ~460px，而每列只有 320px 左右——挤在一行会被
    //   `overflowMode = Ellipsis` 截掉尾巴，冷却和熟练度永远看不见（"名字后面一串省略号"那种坏观感）。
    //   ⚠ 仍然**不右对齐**：它是"名 + 值"连着读的行内文本（用户明令：数值不右对齐到天边）。
    private string 列表行明细(技能数据 技能, int 熟练等级)
    {
        return $"{数值文本(技能)}　精力 {技能.消耗精力}　{冷却文本(技能)}　熟练 {熟练等级}/{玩家档案.熟练等级上限}";
    }

    // 槽里的第三行：只放"投入成本"（槽宽 205 放不下熟练度那一串）
    private static string 明细行(技能数据 技能)
    {
        return $"精力 {技能.消耗精力}　{冷却文本(技能)}";
    }

    private static string 冷却文本(技能数据 技能)
        => 技能.冷却 > 0 ? $"冷却 {技能.冷却}秒" : "无冷却";

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

    private string 技能名(string 标识)
    {
        var 数据 = ServiceRegistry.已注册<DataService>() ? ServiceRegistry.Get<DataService>() : null;
        return 取技能(数据, 标识, out var 技能) ? 技能.名称 : 标识;
    }

    private static Color 常态行底(行件 行) => 行.常态底;

    // ================= 行：按条数补（只增不减，多余的 SetActive(false) 收起） =================

    private int 列数(int 条数) => Mathf.Clamp(Mathf.CeilToInt(条数 / (float)每列行数()), 1, Mathf.Max(1, 列表列.Count));

    private int 每列行数()
    {
        float 高 = 列表列.Count > 0 && 列表列[0] != null ? 列表列[0].rect.height : 0f;
        if (高 < 1f) 高 = 列表高兜底;   // 还没量出来（面板刚实例化）→ 用预制体里烘的值
        return Mathf.Max(1, Mathf.FloorToInt(高 / 行高));
    }

    private void 备列表行(List<string> 列表, DataService 数据)
    {
        while (行表.Count < 列表.Count)
        {
            var 容器 = 列表列[行表.Count % Mathf.Max(1, 列表列.Count)];
            var 矩形 = 新矩形("技能行", 容器);
            var 底 = 矩形.gameObject.AddComponent<Image>();
            Color 行底 = 内容底蕴色(容器);   // 常态底：取预制体里那个容器的底色（没图形就退回内容底色）
            底.color = 行底;
            底.raycastTarget = true;
            float 内 = 6f;
            var 名称 = 造文本("名称", 矩形, 字号_正文, 正文色, TextAlignmentOptions.MidlineLeft);
            定锚(名称.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            名称.rectTransform.offsetMin = new Vector2(内, -(内 + 字号_正文 + 2f));
            名称.rectTransform.offsetMax = new Vector2(-内, -内);
            var 明细 = 造文本("明细", 矩形, 字号_小字, 次要色, TextAlignmentOptions.MidlineLeft);
            定锚(明细.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            明细.rectTransform.offsetMin = new Vector2(内, -(行高 - 2f));
            明细.rectTransform.offsetMax = new Vector2(-内, -(内 + 字号_正文 + 2f));

            int 序 = 行表.Count;
            var 钮 = 矩形.gameObject.AddComponent<Button>();
            钮.targetGraphic = 底;
            钮.onClick.AddListener(() => 点列表行(序));
            接按钮(钮);

            行表.Add(new 行件 { 矩形 = 矩形, 底 = 底, 常态底 = 行底, 名称 = 名称, 明细 = 明细 });
        }
    }

    // 行底常态色：优先取"预制体里 `列表列` 的底"（容器通常没挂 Image → 退回内容底色）
    private static Color 内容底蕴色(RectTransform 容器)
    {
        var 图 = 容器 != null ? 容器.GetComponent<Image>() : null;
        return 图 != null ? 图.color : new Color(0.1020f, 0.0941f, 0.0824f, 1f);
    }

    // 摆行：**按行优先**排 —— 第 i 条落 `i % 列数` 那一列的第 `i / 列数` 行。
    //   列数 = ceil(条数 / 每列行数)，所以"12 条 → 2 列"、"24 条 → 3 列"，列数变了会把行挪到新容器。
    private void 摆行()
    {
        int 列 = Mathf.Max(1, 当前列数);
        for (int i = 0; i < 行表.Count; i++)
        {
            var 行 = 行表[i];
            if (行.矩形 == null) continue;
            if (i < 列 && 列 <= 列表列.Count && 行.矩形.parent != 列表列[i % 列])
                行.矩形.SetParent(列表列[i % 列], false);
            float 顶 = (i / 列) * 行高;
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
        // 两步式还要"落地"：切到待装时滚一下列表标题行、把六个空槽点亮 —— 都在 刷新 里做
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

    // 按钮的悬停/按下底色（只给**运行时造的列表行**用；三个 Tab 与 6 个槽的 ColorBlock 归预制体）
    private void 接按钮(Button 钮)
    {
        钮.transition = Selectable.Transition.ColorTint;
        钮.colors = new ColorBlock
        {
            normalColor = new Color(1f, 1f, 1f, 1f),
            highlightedColor = 悬停底,
            pressedColor = 悬停底,
            selectedColor = new Color(1f, 1f, 1f, 1f),
            disabledColor = 悬停底,
            colorMultiplier = 1f,
            fadeDuration = 0f,   // 无过渡动画（用户明令：无任何动画，也不做缩放反馈）
        };
    }

    // ================= 找节点 / 造行的工具 =================

    private static RectTransform 子矩形(Transform 父, string 名)
    {
        if (父 == null) return null;
        return 父.Find(名) as RectTransform;
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

    private void 报缺()
    {
        if (缺的节点.Count == 0) return;
        Debug.LogError("[技能子面板] 预制体里缺这些节点：" + string.Join("、", 缺的节点) +
                       "。本组件**不再自建骨架** —— 请补进 `Assets/Resources/Prefab/角色面板.prefab`" +
                       "（整备脚本：`.workbuddy/分析/角色面板-prefab整备.py`）。");
    }

    private RectTransform 新矩形(string 名, Transform 父)
    {
        var 物体 = new GameObject(名, typeof(RectTransform));
        物体.transform.SetParent(父, false);
        自建行.Add(物体);
        return (RectTransform)物体.transform;
    }

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
}
