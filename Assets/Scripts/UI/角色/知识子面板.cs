using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 知识子面板：角色面板 [知识] 栏页的内容（由 `角色面板` 注入 `内容区`；本组件**不被壳读任何字段**）。
//
// 左 = **11 条知识**（一本书 = 一条知识，标识 = 书籍标识；数据源 = `DataService.物品` 里 `书籍种类=="知识"` 的那些）：
//   每条两行 —— 上行 = 名称（品质色）；下行（小字）= `3 / 10 级　还差 2 本《X》`。
//   未掌握的**也列**（"未入门　共 10 级　怎么获得：搜刮 / 图书馆"）—— 那是"还没解锁的东西"的说明书，藏起来反而看不见目标。
// 右 = 点某一行之后显示那条知识的**逐级表**（√ 已解锁 / → 下一级 / · 未解锁），每级一行。
//
// ★ 本批（刀82）口径：**骨架 100% 读预制体，本组件只建"条数随数据变的东西"**。
//   现成的（只按名字找出来用，不新建、不改位置/尺寸/字号）：
//     · `知识页内容`（本页的根）
//     · `知识列`（左列容器）+ **11 条 `知识` 行**（每行 `名称` + `说明`，行本身就带 Image + Button）
//     · `逐级表` + `逐级表/小标题` + **10 条 `逐级` 行**（每行 `符` + `文本`）
//   运行时只补"数据比烘好的多"的那种行 —— 而且是 `Instantiate` **预制体里那一行**，
//   所以样式/悬停色/尺寸自动跟预制体走，不是又一份"代码里的样式"。
//   ⚠ 名字就是契约：路径写死在 `绑定位()` 里；在 Unity 里重命名节点要同步改那里（缺了会打 LogError，不静默）。
//
// 经验口径（读 `玩家档案` + 书的 `知识门槛` / `知识等级` / `知识经验`）：
//   · `知识门槛[级-1]` = **升到该级所需的累计经验**（不是每级增量，见 `物品数据.知识门槛` 的注释）；
//   · 每次读满一本给 `物品数据.知识经验`（缺省 100）；天赋「学霸」×1.15 —— 这个乘数在
//     `成长管理器.加知识经验` 里算，本组件只把同一个乘数取出来用于**预告**（两边必须一致，见 每本经验）。
public sealed class 知识子面板 : MonoBehaviour
{
    // 没有"这本书从哪来"的数据字段（`物品数据` 里没有来源/出处那类东西）→ 未掌握那条统一写这句静态提示。
    //   口径 = 用户点名的"怎么获得：搜刮 / 图书馆"。
    private const string 获得提示 = "怎么获得：搜刮 / 图书馆";

    // 逐级表的三态符号。**不用 emoji**（用户明令），也不用 TMP 里可能缺字形的箭头/勾形图标 ——
    //   这三个都取自 GB2312 符号区（中文字体一定有），且 `→` 在本项目别处的界面文案里已经在用。
    private const string 符已解锁 = "√";
    private const string 符下一级 = "→";
    private const string 符未解锁 = "·";

    // ================= 注入：`内容区` 由 角色面板 给（预制体里已接好） =================
    [SerializeField] private RectTransform 内容区;

    // ================= 颜色（**只有"随状态变"的才在代码里**） =================
    //   行底（常态）/ 悬停底 / 选中态之外的静态色一律**归预制体**（代码不碰 → 在 Unity 里调得动）。
    [SerializeField] private Color 正文色 = new Color(0.8941f, 0.8745f, 0.8392f, 1f);   // #E4DFD6 名称兜底色（无品质档时）
    [SerializeField] private Color 次要色 = new Color(0.5412f, 0.5137f, 0.4706f, 1f);   // #8A8378 行下行 / 未解锁
    [SerializeField] private Color 强调色 = new Color(0.7059f, 0.3333f, 0.2353f, 1f);   // #B4553C 下一级（唯一强调色）
    [SerializeField] private Color 恢复色 = new Color(0.4314f, 0.5608f, 0.3843f, 1f);   // #6E8F62 已解锁 / 已满级
    [SerializeField] private Color 选中底 = new Color(0.2118f, 0.1529f, 0.1137f, 1f);   // #36271D 选中/待看的那一行

    private readonly List<知识行件> 左行 = new List<知识行件>();
    private readonly List<逐级行件> 右行 = new List<逐级行件>();
    private readonly List<string> 已列标识 = new List<string>();   // 左列当前显示的是哪几条（点第 N 行 → 反查标识）
    private readonly List<string> 缺的节点 = new List<string>();
    private readonly List<GameObject> 补的行 = new List<GameObject>();   // 只有"数据比烘好的多"时才用得上

    private RectTransform 画布, 左列, 右表;
    private TMP_Text 右标题;
    private string 选中标识;
    private bool 已绑;

    // 左列一行：行底（挂在行自己身上）+ 名称 + 说明
    private sealed class 知识行件
    {
        public RectTransform 矩形;
        public Image 底;
        public Color 常态底;      // 预制体里烘的那个行底（取消选中要还原成它）
        public TMP_Text 名称;
        public TMP_Text 说明;
    }

    // 右列一行：三态符号 + `N 级　<该级内容>`
    private sealed class 逐级行件
    {
        public RectTransform 矩形;
        public TMP_Text 符;
        public TMP_Text 文本;
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

    // ================= 绑定位（只找、不建骨架） =================

    private void 绑定位()
    {
        if (已绑) return;
        已绑 = true;
        收掉补的行();
        左行.Clear();
        右行.Clear();
        已列标识.Clear();
        缺的节点.Clear();
        选中标识 = null;

        画布 = 子矩形(内容区, "知识页内容");
        if (画布 == null) { 缺("知识页内容"); 报缺(); return; }
        左列 = 子矩形(画布, "知识列");
        右表 = 子矩形(画布, "逐级表");
        if (左列 == null) 缺("知识列");
        if (右表 == null) { 缺("逐级表"); 报缺(); return; }
        右标题 = 子文本(右表, "小标题");
        if (右标题 == null) 缺("小标题");

        // 左列：所有叫 `知识` 的子节点，**按它们在预制体里的先后**（= 烘好的行序）当第 0..N 行
        if (左列 != null)
            for (int i = 0; i < 左列.childCount; i++)
            {
                var 子 = 左列.GetChild(i) as RectTransform;
                if (子 != null && 子.name == "知识") 左行.Add(绑左行(子));
            }
        if (左行.Count == 0) 缺("知识列/知识");
        // 右列：`小标题` 之后的所有 `逐级` 行
        for (int i = 0; i < 右表.childCount; i++)
        {
            var 子 = 右表.GetChild(i) as RectTransform;
            if (子 != null && 子.name == "逐级") 右行.Add(绑右行(子));
        }
        if (右行.Count == 0) 缺("逐级表/逐级");
        报缺();
    }

    private 知识行件 绑左行(RectTransform 矩形)
    {
        var 底 = 矩形.GetComponent<Image>();
        var 件 = new 知识行件
        {
            矩形 = 矩形,
            底 = 底,
            常态底 = 底 != null ? 底.color : 正文色,   // 记住**预制体里调的**行底，取消选中时还原它
            名称 = 子文本(矩形, "名称"),
            说明 = 子文本(矩形, "说明"),
        };
        if (件.说明 == null) 缺("知识/说明");
        // 点击：只接事件（悬停色/过渡沿用预制体里烘好的 ColorBlock）
        int 序 = 左行.Count;
        var 钮 = 矩形.GetComponent<Button>();
        if (钮 != null)
        {
            钮.onClick.RemoveAllListeners();
            钮.onClick.AddListener(() => 点知识(序));
        }
        return 件;
    }

    private 逐级行件 绑右行(RectTransform 矩形)
        => new 逐级行件 { 矩形 = 矩形, 符 = 子文本(矩形, "符"), 文本 = 子文本(矩形, "文本") };

    // ================= 刷新 =================

    public void 刷新()
    {
        绑定位();
        if (画布 == null || 左列 == null || 右表 == null) return;
        var 玩家 = 当前档案();
        if (玩家 == null) return;
        var 数据 = ServiceRegistry.已注册<DataService>() ? ServiceRegistry.Get<DataService>() : null;
        if (数据 == null) return;

        var 书表 = 知识书(数据);
        记标识(书表);              // 点知识(序) 要按序号反查标识 → 先把这次的标识表存下来（必须在刷行之前）
        备左行(书表.Count);

        for (int i = 0; i < 左行.Count; i++)
        {
            var 行 = 左行[i];
            if (行.矩形 == null) continue;
            bool 有 = i < 书表.Count;
            if (行.矩形.gameObject.activeSelf != 有) 行.矩形.gameObject.SetActive(有);
            if (有) 刷左行(行, 书表[i], 玩家);
        }

        // 选中那条已经不在表里了（数据改了）→ 清掉，免得右列指着一条不存在的东西
        if (!string.IsNullOrEmpty(选中标识) && !书表.Exists(b => b.标识 == 选中标识)) 选中标识 = null;
        刷右列(玩家, 数据);
    }

    private static 玩家档案 当前档案()
        => ServiceRegistry.已注册<PlayerService>() ? ServiceRegistry.Get<PlayerService>()?.档案 : null;

    // 全部知识书，按标识排序（顺序稳定，换存档/换机器都一样）
    private static List<物品数据> 知识书(DataService 数据)
    {
        var 表 = new List<物品数据>();
        if (数据?.物品 == null) return 表;
        foreach (var 对 in 数据.物品)
            if (对.Value != null && 对.Value.书籍种类 == "知识"
                && 对.Value.知识等级 != null && 对.Value.知识等级.Length > 0)
                表.Add(对.Value);
        表.Sort((a, b) => string.CompareOrdinal(a.标识, b.标识));
        return 表;
    }

    // 左列的行要按序号知道"我是哪一条"——刷新时把标识表存下来（点知识 读它）
    private void 记标识(List<物品数据> 书表)
    {
        已列标识.Clear();
        foreach (var 书 in 书表) 已列标识.Add(书.标识);
    }

    // ================= 左列：11 条知识 =================

    // 行数不够才补 —— **补出来的行是 `Instantiate` 预制体里最后那一行**（样式/悬停色全跟着预制体走），
    //   位置就排在它下面一行（行高 = 那一行的 rect.height，不在这里写死任何数字）。
    //   ⚠ 正常情况下 11 条数据 ↔ 11 条烘好的行，这里一次都不会进。
    private void 备左行(int 需要)
    {
        while (左行.Count < 需要 && 左行.Count > 0)
        {
            var 源 = 左行[左行.Count - 1].矩形;
            if (源 == null) return;
            var 物 = Instantiate(源.gameObject, 左列, false);
            物.name = "知识";
            补的行.Add(物);
            var 矩形 = (RectTransform)物.transform;
            矩形.anchoredPosition = new Vector2(源.anchoredPosition.x, 源.anchoredPosition.y - 源.rect.height);
            左行.Add(绑左行(矩形));
        }
    }

    private void 刷左行(知识行件 行, 物品数据 书, 玩家档案 玩家)
    {
        int 总级 = 书.知识等级.Length;
        int 级 = 玩家.知识等级(书.标识);            // 0 = 还没掌握
        int 级内经验 = 玩家.查知识(书.标识)?.经验 ?? 0;
        bool 已掌握 = 级 > 0;

        面板基类.设文本(行.名称, $"<color=#{ColorUtility.ToHtmlStringRGB(品质工具.颜色(书.品质档))}>{书.标识}</color>");
        if (行.底 != null) 行.底.color = 书.标识 == 选中标识 ? 选中底 : 常态行底(行);
        if (行.说明 == null) return;

        if (!已掌握)
        {
            面板基类.设文本(行.说明, $"未入门　共 {总级} 级　{获得提示}");
            行.说明.color = 次要色;
            return;
        }

        int 下一门槛 = 下一级门槛(书, 玩家, 级);
        if (下一门槛 < 0)
        {
            面板基类.设文本(行.说明, $"{级} / {总级} 级　已满级");
            行.说明.color = 恢复色;
            return;
        }

        int 还差经验 = Mathf.Max(0, 下一门槛 - 级内经验);
        int 每本 = 每本经验(书, 玩家);
        int 本数 = Mathf.CeilToInt(还差经验 / (float)Mathf.Max(1, 每本));
        面板基类.设文本(行.说明, 本数 <= 0
            ? $"{级} / {总级} 级　再读一本即可升级"
            : $"{级} / {总级} 级　还差 {本数} 本《{书.标识}》");
        行.说明.color = 次要色;
    }

    private static Color 常态行底(知识行件 行) => 行.常态底;

    // 升到"下一级"所需的**累计**经验；-1 = 已经满级（没有下一级）
    private static int 下一级门槛(物品数据 书, 玩家档案 玩家, int 级)
    {
        if (级 >= 书.知识等级.Length) return -1;
        if (书.知识门槛 != null && 级 < 书.知识门槛.Length) return 书.知识门槛[级];   // 门槛[级] = 升到 级+1 的累计经验
        return 玩家档案.知识每级经验 * (级 + 1);                                     // 数据没给门槛 → 退回"每级 +100"
    }

    // 每次读满一本给的**有效**经验：书的 `知识经验`（缺省 100）× 天赋「学霸」的 1.15。
    //   ⚠ 这个 1.15 与 `成长管理器.加知识经验` 里那一句必须同源 —— 那边才是真正的入账口径，
    //     这里只是为了把"还差 N 本"预告准（不用整数除法，否则学霸会看到"还差 2 本"却读 1 本就升了）。
    private static int 每本经验(物品数据 书, 玩家档案 玩家)
    {
        int 基 = 书.知识经验 > 0 ? 书.知识经验 : 100;
        if (玩家.天赋 != null && 玩家.天赋.Contains("学霸")) 基 = (int)(基 * 1.15f);
        return Mathf.Max(1, 基);
    }

    // 点左边一条：选中/取消。选中之后右列显示它的逐级表。
    private void 点知识(int 序)
    {
        if (序 < 0 || 序 >= 已列标识.Count) return;
        string 标识 = 已列标识[序];
        选中标识 = 选中标识 == 标识 ? null : 标识;
        刷新();
    }

    // ================= 右列：逐级表 =================

    private void 刷右列(玩家档案 玩家, DataService 数据)
    {
        物品数据 书 = null;
        if (!string.IsNullOrEmpty(选中标识) && 数据.物品 != null) 数据.物品.TryGetValue(选中标识, out 书);

        if (书 == null)
        {
            面板基类.设文本(右标题, "逐级表");
            备右行(1);
            刷右行(0, "", "点左边一条知识，这里显示它每一级解锁什么。", 次要色, true);
            for (int i = 1; i < 右行.Count; i++) 显右行(i, false);
            return;
        }

        int 总级 = 书.知识等级.Length;
        int 当前 = 玩家.知识等级(书.标识);
        面板基类.设文本(右标题, $"《{书.标识}》逐级表　{当前} / {总级}");
        备右行(总级);

        for (int i = 0; i < 右行.Count; i++)
        {
            if (i >= 总级) { 显右行(i, false); continue; }
            var 等级行 = 书.知识等级[i];
            int 级 = 等级行 != null && 等级行.级 > 0 ? 等级行.级 : i + 1;

            // √ 已解锁（级 <= 当前） / → 下一级（级 == 当前 + 1） / · 未解锁
            string 符;
            Color 色;
            if (级 <= 当前) { 符 = 符已解锁; 色 = 恢复色; }
            else if (级 == 当前 + 1) { 符 = 符下一级; 色 = 强调色; }
            else { 符 = 符未解锁; 色 = 次要色; }
            刷右行(i, 符, $"{级} 级　{等级内容(数据, 等级行)}", 色, true);
        }
    }

    private void 刷右行(int 序, string 符, string 文本, Color 色, bool 显示)
    {
        if (序 < 0 || 序 >= 右行.Count) return;
        var 行 = 右行[序];
        if (行.矩形 == null) return;
        if (行.矩形.gameObject.activeSelf != 显示) 行.矩形.gameObject.SetActive(显示);
        if (!显示) return;
        面板基类.设文本(行.符, 符);
        if (行.符 != null) 行.符.color = 色;
        面板基类.设文本(行.文本, 文本);
        if (行.文本 != null) 行.文本.color = 色;
    }

    private void 显右行(int 序, bool 显示)
    {
        if (序 < 0 || 序 >= 右行.Count) return;
        var 矩形 = 右行[序].矩形;
        if (矩形 != null && 矩形.gameObject.activeSelf != 显示) 矩形.gameObject.SetActive(显示);
    }

    // 逐级表按数据条数补行（同 备左行：复制预制体里的最后一行，位置排它下面）
    private void 备右行(int 需要)
    {
        while (右行.Count < 需要 && 右行.Count > 0)
        {
            var 源 = 右行[右行.Count - 1].矩形;
            if (源 == null) return;
            var 物 = Instantiate(源.gameObject, 右表, false);
            物.name = "逐级";
            补的行.Add(物);
            var 矩形 = (RectTransform)物.transform;
            矩形.anchoredPosition = new Vector2(源.anchoredPosition.x, 源.anchoredPosition.y - 源.rect.height);
            右行.Add(绑右行(矩形));
        }
    }

    // 一级的内容：优先用数据里的 `描述`（那是这本书作者写的那一句）；没写就按"加成 / 解锁了什么"拼一条。
    private string 等级内容(DataService 数据, 知识等级数据 级)
    {
        if (级 == null) return "（本级无内容）";
        if (!string.IsNullOrEmpty(级.描述)) return 级.描述;
        var 串 = new List<string>();
        if (!string.IsNullOrEmpty(级.加成属性) && 级.加成值 != 0f)
            串.Add($"{级.加成属性} +{级.加成值:0.##}");
        if (级.解锁技能 != null && 级.解锁技能.Length > 0)
            串.Add("解锁技能 " + 技能名(数据, 级.解锁技能));
        if (级.解锁配方 != null && 级.解锁配方.Length > 0)
            串.Add($"解锁配方 {级.解锁配方.Length} 条");
        return 串.Count > 0 ? string.Join("　", 串) : "（本级无内容）";
    }

    private static string 技能名(DataService 数据, string[] 标识表)
    {
        var 名 = new List<string>();
        foreach (var 标识 in 标识表)
        {
            if (string.IsNullOrEmpty(标识)) continue;
            技能数据 技能 = null;
            if (数据?.技能 != null) 数据.技能.TryGetValue(标识, out 技能);
            名.Add(技能 != null && !string.IsNullOrEmpty(技能.名称) ? 技能.名称 : 标识);
        }
        return 名.Count > 0 ? string.Join("、", 名) : "—";
    }

    // ================= 找节点 / 补行的小工具 =================

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

    // 缺节点要出声（以前是静默的：少一个节点 = 面板上少一块，一条日志都没有）
    private void 报缺()
    {
        if (缺的节点.Count == 0) return;
        Debug.LogError("[知识子面板] 预制体里缺这些节点：" + string.Join("、", 缺的节点) +
                       "。本组件**不再自建骨架** —— 请补进 `Assets/Resources/Prefab/角色面板.prefab`" +
                       "（整备脚本：`.workbuddy/分析/角色面板-prefab整备.py`）。");
    }

    private void 收掉补的行()
    {
        foreach (var 物 in 补的行)
        {
            if (物 == null) continue;
            if (Application.isPlaying) Destroy(物);
            else DestroyImmediate(物);   // 编辑器里（未运行）：Destroy 要等帧末，紧接着的重绑会读到"还没死的旧行"
        }
        补的行.Clear();
    }
}
