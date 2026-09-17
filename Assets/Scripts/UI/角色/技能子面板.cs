using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 技能槽引用：一个技能槽的引用组（**Inspector 里接**；没接 → 按名字兜底；名字也没有 → 补建空节点）。
//   `按钮`     = 槽自己的 Button（点它 = 装入 / 卸下，见 `点技能槽`）；
//   `名称`     = 第一行（技能名；空槽写「空」）；
//   `标记`     = 第二行（主动 / 被动）；
//   `明细`     = 第三行（精力 / 冷却）；
//   `待装高亮` = **预制体里预置的高亮子物体**：只在"有待装 + 本槽为空"时 `SetActive(true)`。
//                它长什么样（底色 / 描边 / 边框）全在 Unity 里调；本组件只切它的显隐。
[Serializable]
public class 技能槽引用
{
    public Button 按钮;
    public TMP_Text 名称;
    public TMP_Text 标记;
    public TMP_Text 明细;
    public GameObject 待装高亮;
}

// 技能子面板：角色面板 [技能] 栏页的内容（由 `角色面板` 注入 `内容区`）。
//
// 上半 = **6 个技能槽**（横排），下半 = **已学技能列表**（`列表列` 是列容器，行从 `技能行模板` 克隆）。
//   槽里三行：`名称` · `标记`（主动/被动）· `明细`（精力 / 冷却）。
//   列表每行两行：上行 = 名称 + 主动/被动 + 「已装 N」，下行 = 数值 · 精力 · 冷却 · 熟练。
//
// ★ 本批口径（用户点名的两条，都要守）：
//   ① **外观 0 行代码**：颜色、字号、行高、宽度、尺寸一律归预制体（在 Unity 里调）。
//      状态差异只用两样：`SetActive`（预制体里预置的高亮子物体）与文字内容本身。
//      原来的 `正文色/次要色/强调色/悬停底/选中底/待装底` 六个颜色字段、两个字号字段
//      （正文 / 小字）、`行高`、`列表高兜底`、以及运行时行上的 `Selectable` 过渡配色块 **全部删掉** ——
//      代码里再也读不到一个颜色数值、一个字号；品质色也不再由代码写进富文本（那是"代码写外貌"）。
//   ② **引用优先**：6 个槽（含槽内三行）、提示行、列表标题、列容器、行模板都在 Inspector 里接；
//      引用为空才按名字找（名字只是兜底），名字也没有就**补建一个不带样式的空节点**
//      （挂对父、名字对、组件齐）—— 绝不"只报缺引用，让用户自己去搭"。
//
// 交互（用户点名"点选交换"）：
//   · 待装：点列表里的一条 → 它变成"待装"（该行的 `选中高亮` 亮起）**且 6 个空槽的 `待装高亮` 一起亮**，
//     提示行同时写出"待装：X → 点任意技能槽装入"（原来只有行变色，看不出"现在该点哪儿"）；
//   · 装入：有待装时点某个技能槽 → **调 `成长管理器.切换入槽(槽索引, 标识)`**；
//     技能本来就在别的槽里 → 那个方法内部**两槽对调**（原先那格换到本格，不会白丢一个槽）；
//   · 卸下：**没有**待装时点一个已装槽 → 调 `成长管理器.卸下槽(槽索引)`；
//   · 再点一次同一条列表 = 取消待装。
//   · 6 个槽的 `Button.onClick` 在本组件里接（→ `点技能槽(i)`）；原来没接，所以"槽点了没反应"。
//
// ★ 写入路径一律走 `成长管理器` 那两个方法，**本组件自己一个字都不写档案**：
//   槽位长度必须恒为 6、同名技能不能占两格、旧档要补齐 —— 这些约束全在那边收口；
//   这里若自己 `战斗技能槽[i] = …`，上面三条就会各漏一处。
public sealed class 技能子面板 : MonoBehaviour
{
    // ================= 注入：`内容区` 由 角色面板 给（预制体里也烘好了） =================
    [SerializeField] private RectTransform 内容区;

    // ================= 引用（Inspector 里接；空 → 按名字找 → 还没有就补建空节点） =================
    [SerializeField] private RectTransform 技能页内容;      // 本页的根（预制体里：`内容区/技能页内容`）
    // ★ 下标 0..5 ↔ `玩家档案.战斗技能槽` 的 0..5（槽数恒 = `成长管理器.技能槽数` = 6）。
    //   名字兜底 = 预制体里的 `技能槽1`..`技能槽6`（下标 i ↔ 名字里的 i+1）。
    [SerializeField] private 技能槽引用[] 技能槽 = new 技能槽引用[成长管理器.技能槽数];
    [SerializeField] private TMP_Text 操作提示;              // 底部操作提示行（名字兜底：`操作提示`）
    [SerializeField] private TMP_Text 已学技能标题;          // 列表小标题（名字兜底：`已学技能`）
    [SerializeField] private RectTransform[] 列表列;         // 列容器（预制体里 4 个，名字兜底：`列表列`）
    [SerializeField] private RectTransform 技能行模板;        // 列表行模板（运行时行 = Instantiate 它）

    private const string 空槽文字 = "空";
    private const string 待装提示 = "点列表选技能 → 点槽装入　·　无待装时点已装槽 = 卸下";

    // 运行时造出来的东西（列表行 + "预制体里没有"的兜底节点）——**不序列化**：不属于资产
    private readonly List<GameObject> 自建 = new List<GameObject>();

    private RectTransform 画布;
    private readonly List<槽件> 槽表 = new List<槽件>();
    private readonly List<行件> 行表 = new List<行件>();
    private readonly List<string> 已学标识 = new List<string>();   // 列表当前显示的是哪几条 → 只在真的变了才重建行
    private readonly List<string> 缺的节点 = new List<string>();
    private string 待装标识;                                        // 点列表选中、还没落槽的那条（空 = 无）
    private bool 已绑;

    // 一个技能槽：矩形 + 三行文本 + 预制体里预置的 `待装高亮`
    private sealed class 槽件
    {
        public RectTransform 矩形;
        public TMP_Text 名称;
        public TMP_Text 标记;
        public TMP_Text 明细;
        public GameObject 待装高亮;
    }

    // 列表的一行（两行文字）：上行 = 名称 + 主动/被动 + 已装槽号；下行 = 数值/精力/冷却/熟练
    private sealed class 行件
    {
        public RectTransform 矩形;
        public TMP_Text 名称;
        public TMP_Text 明细;
        public GameObject 选中高亮;
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

    // ================= 绑定位（引用优先 → 名字兜底 → 补建空节点） =================

    private void 绑定位()
    {
        if (已绑) return;
        已绑 = true;
        清掉自建();
        槽表.Clear();
        行表.Clear();
        已学标识.Clear();
        缺的节点.Clear();
        待装标识 = null;

        画布 = 技能页内容 != null ? 技能页内容 : 找或建矩形(内容区, "技能页内容");
        if (画布 == null) { 缺("技能页内容"); 报缺(); return; }
        技能页内容 = 画布;

        绑槽();
        操作提示 = 取或建文本(操作提示, 画布, "操作提示");
        已学技能标题 = 取或建文本(已学技能标题, 画布, "已学技能");
        备列表列();
        报缺();
    }

    // 6 个槽：`技能槽[i]` ↔ `战斗技能槽[i]`（槽数 = `成长管理器.技能槽数`，恒为 6）。
    private void 绑槽()
    {
        // 数组长度按槽数补齐（Inspector 里少接几个引用不算错：少的那几个走名字 / 补建）
        if (技能槽 == null || 技能槽.Length != 成长管理器.技能槽数)
        {
            var 新表 = new 技能槽引用[成长管理器.技能槽数];
            if (技能槽 != null) Array.Copy(技能槽, 新表, Mathf.Min(技能槽.Length, 新表.Length));
            技能槽 = 新表;
        }

        bool 缺高亮 = false;
        for (int i = 0; i < 成长管理器.技能槽数; i++)
        {
            var 引 = 技能槽[i] != null ? 技能槽[i] : new 技能槽引用();
            技能槽[i] = 引;
            // 引用优先 → 名字（`技能槽1`..`技能槽6`，下标 i ↔ 名字里的 i+1）→ 补建
            var 矩形 = 矩形of(引);
            if (矩形 == null) 矩形 = 子矩形(画布, "技能槽" + (i + 1));
            if (矩形 == null) { 矩形 = 新矩形("技能槽" + (i + 1), 画布); 缺("技能槽" + (i + 1)); }
            if (矩形 == null) continue;

            var 待装高亮 = 引.待装高亮 != null ? 引.待装高亮 : 子物体(矩形, "待装高亮");
            if (待装高亮 == null) 缺高亮 = true;
            槽表.Add(new 槽件
            {
                矩形 = 矩形,
                名称 = 取或建文本(引.名称, 矩形, "名称"),
                标记 = 取或建文本(引.标记, 矩形, "标记"),
                明细 = 取或建文本(引.明细, 矩形, "明细"),
                待装高亮 = 待装高亮,
            });

            // 槽自己的 Button → `点技能槽(i)`（用户点名的调用链：原来槽点了没反应）
            var 钮 = 引.按钮 != null ? 引.按钮 : 取或建件<Button>(矩形);
            if (钮 != null)
            {
                钮.onClick.RemoveAllListeners();
                int 序 = i;   // 闭包要捕获"本槽的下标"，别用循环变量
                钮.onClick.AddListener(() => 点技能槽(序));
            }
        }
        if (缺高亮) 缺("技能槽/待装高亮（预制体里给每个槽加一个高亮子物体）");
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
            面板基类.设文本(槽.标记, 有 ? 主被动标记(技能) : "");
            面板基类.设文本(槽.明细, 有 ? 明细行(技能) : "");

            // 状态只改**结构**，不改外观：空槽 + 有待装 → 亮出预制体里的 `待装高亮`
            //   （6 个空槽一起亮 = "现在点任何一格都能装"）。没有那个子物体 → 什么都不做
            //   （外观归预制体，代码不代它决定"亮成什么样"）。
            if (槽.待装高亮 != null) 槽.待装高亮.SetActive(!有 && 有待装);
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
            备列表行(列表.Count);
        }

        // ★ 熟练等级一次查成字典：原来是**每行** `已学技能.Find(...)` → n 行 = n × n 次扫描
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
            // 待装那一条 → 亮它的 `选中高亮`（同样是预制体里的子物体，代码不碰颜色）
            if (行.选中高亮 != null) 行.选中高亮.SetActive(标识 == 待装标识);
        }
        摆行();
        刷标题行(列表.Count, 已装表.Count);
    }

    // 列表小标题 = **活的小结**：条数 + 已装槽数 + 待装是哪一条（原来是空文本，什么都没显示）
    private void 刷标题行(int 条数, int 已装数)
    {
        面板基类.设文本(已学技能标题, $"已学技能 {条数} 条　已装 {已装数}/{成长管理器.技能槽数}");
        面板基类.设文本(操作提示, string.IsNullOrEmpty(待装标识)
            ? 待装提示
            : $"待装：{技能名(待装标识)}　→　点任意技能槽装入　·　再点该行取消");
    }

    // 两个标识表是否逐项相同（用来判断"列表该不该重建"）
    private static bool 同序(List<string> 甲, List<string> 乙)
    {
        if (甲.Count != 乙.Count) return false;
        for (int i = 0; i < 甲.Count; i++) if (甲[i] != 乙[i]) return false;
        return true;
    }

    // ================= 列表行文案 =================

    // 列表行名称：名称 + 主动/被动 + （已装 → 「已装 N」）。
    //   ⚠ 品质色不再由代码写进富文本（本批口径①：代码不写外貌）—— 要区分品质请在预制体里做。
    private static string 列表行名称(技能数据 技能, int 槽号)
    {
        // 这里不判空：`已学标识` 只收"在 数据.技能 里查得到"的那些（见 刷列表），所以传进来必非 null
        return $"{技能.名称}　{(技能.被动 ? "被动" : "主动")}{(槽号 > 0 ? $"　已装 {槽号}" : "")}";
    }

    // 列表行明细（小字）：数值 · 精力 · 冷却 · 熟练。
    // 为什么拆两行：六个字段挤一行会被 `overflowMode = Ellipsis` 截掉尾巴，冷却和熟练度永远看不见。
    //   ⚠ 仍然**不右对齐**：它是"名 + 值"连着读的行内文本（用户明令：数值不右对齐到天边）。
    private static string 列表行明细(技能数据 技能, int 熟练等级)
        => $"{数值文本(技能)}　精力 {技能.消耗精力}　{冷却文本(技能)}　熟练 {熟练等级}/{玩家档案.熟练等级上限}";

    // 槽里的第三行：只放"投入成本"（槽宽 205 放不下熟练度那一串）
    private static string 明细行(技能数据 技能) => $"精力 {技能.消耗精力}　{冷却文本(技能)}";

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

    // ================= 布局：列数 / 行高**全从预制体量**（代码不写死尺寸） =================

    private void 摆行()
    {
        布局(已学标识.Count, out int 列, out float 高);
        if (列表列 == null || 列表列.Length == 0) return;
        for (int i = 0; i < 行表.Count; i++)
        {
            var 行 = 行表[i];
            if (行.矩形 == null) continue;
            var 容器 = 列表列[Mathf.Min(i % 列, 列表列.Length - 1)];
            if (容器 != null && 行.矩形.parent != 容器) 行.矩形.SetParent(容器, false);
            // 行优先排：第 i 条落 `i % 列数` 那一列的第 `i / 列数` 行
            设行带(行.矩形, (i / 列) * 高, 高);
        }
    }

    // 列数 / 行高：两个数都从预制体读（`技能行模板` 的行高 + `列表列` 的列高），代码不写任何尺寸字面量。
    private void 布局(int 条数, out int 列数, out float 高)
    {
        var 首列 = 列表列 != null && 列表列.Length > 0 ? 列表列[0] : null;
        float 列高 = 首列 != null ? 首列.rect.height : 0f;
        高 = 行高();
        int 每列 = 高 > 1f && 列高 > 1f ? Mathf.Max(1, Mathf.FloorToInt(列高 / 高)) : Mathf.Max(1, 条数);
        列数 = Mathf.Clamp(Mathf.CeilToInt(条数 / (float)每列), 1, Mathf.Max(1, 列表列 != null ? 列表列.Length : 1));
        // 量不到行高（模板没接、行是补建的空节点）→ 把列高均分给每条：退化，但不至于全叠在顶上
        if (高 <= 1f && 条数 > 0 && 列高 > 1f) 高 = 列高 / 条数;
    }

    // 一行的高度：模板优先，其次已建的行；都量不到 → 0（交给 布局 去均分）
    private float 行高()
    {
        if (技能行模板 != null && 技能行模板.rect.height > 1f) return 技能行模板.rect.height;
        if (行表.Count > 0 && 行表[0].矩形 != null && 行表[0].矩形.rect.height > 1f) return 行表[0].矩形.rect.height;
        return 0f;
    }

    // ================= 行：按条数补（只增不减，多余的 SetActive(false) 收起） =================

    // 行 = **`Instantiate(技能行模板)`**（样式/悬停/尺寸全跟预制体走）。
    //   模板没接、名字也没有 → 报告并退化：补建一个**不带样式**的空行（组件齐，外观留给用户去搭）。
    private void 备列表行(int 条数)
    {
        while (行表.Count < 条数)
        {
            if (列表列 == null || 列表列.Length == 0) return;
            var 容器 = 列表列[行表.Count % 列表列.Length];
            if (容器 == null) return;

            var 模板 = 找行模板();
            RectTransform 形;
            if (模板 != null)
            {
                var 物 = Instantiate(模板.gameObject, 容器, false);
                物.name = "技能行";
                物.SetActive(true);
                自建.Add(物);
                形 = (RectTransform)物.transform;
            }
            else
            {
                缺("技能行模板");
                形 = 新矩形("技能行", 容器);
                if (形 == null) return;
            }
            行表.Add(绑行(形, 行表.Count));
        }
    }

    // 行模板：Inspector 引用优先 → 名字（`技能页内容/技能行` 或某个 `列表列/技能行`）
    private RectTransform 找行模板()
    {
        if (技能行模板 != null) return 技能行模板;
        var 现成 = 子矩形(画布, "技能行");
        if (现成 != null) return 现成;
        if (列表列 != null)
            foreach (var 列 in 列表列)
            {
                var 子 = 子矩形(列, "技能行");
                if (子 != null) return 子;
            }
        return null;
    }

    private 行件 绑行(RectTransform 矩形, int 序)
    {
        // 行得有个能接射线的图形才点得动（模板里本来就该有；没有就补一个，不设颜色）
        var 底 = 矩形.GetComponent<Image>();
        if (底 == null) 底 = 矩形.gameObject.AddComponent<Image>();
        底.raycastTarget = true;

        var 钮 = 矩形.GetComponent<Button>();
        if (钮 == null) 钮 = 矩形.gameObject.AddComponent<Button>();
        // 已有图形就别抢：悬停 / 按下的那套过渡沿用预制体里烘好的，本组件一个颜色都不写
        if (钮.targetGraphic == null) 钮.targetGraphic = 底;
        钮.onClick.RemoveAllListeners();
        钮.onClick.AddListener(() => 点列表行(序));

        return new 行件
        {
            矩形 = 矩形,
            名称 = 取或建文本(子文本(矩形, "名称"), 矩形, "名称"),
            明细 = 取或建文本(子文本(矩形, "明细"), 矩形, "明细"),
            选中高亮 = 子物体(矩形, "选中高亮"),
        };
    }

    // ================= 交互（点选交换） =================

    // 点列表一条：设为/取消"待装"。装的动作**不在这里**（等玩家点某个槽）——
    //   这是用户点名的"点列表选技能 → 点槽装入"两步式，避免"点一下就顶掉某个槽"的误操作。
    private void 点列表行(int 序)
    {
        if (序 < 0 || 序 >= 已学标识.Count) return;
        string 标识 = 已学标识[序];
        待装标识 = 待装标识 == 标识 ? null : 标识;
        // 两步式还要"落地"：切到待装时滚一下列表标题行、把六个空槽的高亮打开 —— 都在 刷新 里做
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

    // ================= 找节点 / 补节点 的小工具 =================

    // 引用组里"哪一个是这几个引用共同的节点"：按钮 / 三行文本里**挂在槽身上的那个**。
    //   ⚠ 不含 `待装高亮`（它是槽的**子物体**，不是槽本身）。
    private static RectTransform 矩形of(技能槽引用 引)
    {
        if (引 == null) return null;
        if (引.按钮 != null) return 引.按钮.transform as RectTransform;
        if (引.名称 != null) return 引.名称.rectTransform;
        if (引.标记 != null) return 引.标记.rectTransform;
        if (引.明细 != null) return 引.明细.rectTransform;
        return null;
    }

    // 列容器：引用里给的先用；没给就按名字 `列表列` 收（预制体里 4 个）；一个都没有 → 补建 1 个
    private void 备列表列()
    {
        var 列 = new List<RectTransform>();
        if (列表列 != null)
            foreach (var 现成 in 列表列)
                if (现成 != null && !列.Contains(现成)) 列.Add(现成);
        if (列.Count == 0 && 画布 != null)
            for (int i = 0; i < 画布.childCount; i++)
            {
                var 子 = 画布.GetChild(i) as RectTransform;
                if (子 != null && 子.name == "列表列") 列.Add(子);
            }
        if (列.Count == 0)
        {
            缺("列表列");
            var 补的 = 新矩形("列表列", 画布);
            if (补的 != null) 列.Add(补的);
        }
        列表列 = 列.ToArray();
    }

    // 取现成引用；没有就按名字找；名字也没有就**补建**（挂对父、名字对、组件齐，不带任何外观）
    private TMP_Text 取或建文本(TMP_Text 现成, RectTransform 父, string 名)
    {
        if (现成 != null) return 现成;
        if (父 == null) return null;
        var 节点 = 父.Find(名) as RectTransform;
        if (节点 == null) { 节点 = 新矩形(名, 父); 缺(名); }
        if (节点 == null) return null;
        var 件 = 节点.GetComponent<TMP_Text>();
        if (件 == null) 件 = 节点.gameObject.AddComponent<TextMeshProUGUI>();
        return 件;
    }

    private T 取或建件<T>(RectTransform 父) where T : Component
    {
        if (父 == null) return null;
        var 件 = 父.GetComponent<T>();
        return 件 != null ? 件 : 父.gameObject.AddComponent<T>();
    }

    private RectTransform 找或建矩形(RectTransform 父, string 名)
    {
        if (父 == null) return null;
        var 现成 = 父.Find(名) as RectTransform;
        if (现成 != null) return 现成;
        缺(名);
        return 新矩形(名, 父);
    }

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

    private static GameObject 子物体(Transform 父, string 名)
    {
        var 子 = 父 != null ? 父.Find(名) : null;
        return 子 != null ? 子.gameObject : null;
    }

    private void 缺(string 名)
    {
        if (!缺的节点.Contains(名)) 缺的节点.Add(名);
    }

    // 缺节点要出声（以前是静默的：少一个节点 = 面板上少一块，一条日志都没有）
    private void 报缺()
    {
        if (缺的节点.Count == 0) return;
        Debug.LogError("[技能子面板] 预制体里缺这些节点/引用：" + string.Join("、", 缺的节点) +
                       "。缺失的引用已按名字兜底、按名字补建（挂对父、名字对、组件齐，**不带任何外观**）；" +
                       "「高亮」这类子物体**不补建**（补出来只会是个白块），只是不改外观。" +
                       "请在 `Assets/Resources/Prefab/角色面板.prefab` 里预置同名节点 / 在 Inspector 上接好引用。");
    }

    private RectTransform 新矩形(string 名, Transform 父)
    {
        if (父 == null) return null;
        var 物体 = new GameObject(名, typeof(RectTransform));
        物体.transform.SetParent(父, false);
        自建.Add(物体);
        return (RectTransform)物体.transform;
    }

    private void 清掉自建()
    {
        foreach (var 物 in 自建)
        {
            if (物 == null) continue;
            if (Application.isPlaying) Destroy(物);
            else DestroyImmediate(物);   // 编辑器里（未运行）：Destroy 要等帧末，紧接着的重建会读到"还没死的旧行"
        }
        自建.Clear();
    }

    // 行带 = 从容器顶往下第 顶 像素、高 高（横向拉满）——只摆位，不管外观
    private static void 设行带(RectTransform 行, float 顶, float 高)
    {
        行.anchorMin = new Vector2(0f, 1f);
        行.anchorMax = new Vector2(1f, 1f);
        行.pivot = new Vector2(0f, 1f);
        行.offsetMin = new Vector2(0f, -顶 - 高);
        行.offsetMax = new Vector2(0f, -顶);
    }
}
