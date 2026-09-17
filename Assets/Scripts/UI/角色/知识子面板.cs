using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 知识行引用：一条知识的引用组（**Inspector 里接**；没接 → 按名字兜底；名字也没有 → 补建空节点）。
//   `底`       = 行自己的 Image（只当 Button 的 targetGraphic 用，代码不改它的颜色）；
//   `按钮`     = 行自己的 Button（点它 = 选中这条知识）；
//   `名称`     = 上行（书名）；
//   `说明`     = 下行（"未入门 共 10 级…" / "3 / 10 级　还差 2 本《X》"）；
//   `进度`     = 这条知识的**进度子物体**：代码只写 `Image.fillAmount`（= 已解锁级数 / 总级数），
//                不写颜色、不写字号；填充色 / 底色 / 圆角 / 长度全在预制体里调；
//   `选中高亮` = **预制体里预置的高亮子物体**：只在"这一条是当前选中"时 `SetActive(true)`。
[Serializable]
public class 知识行引用
{
    public Image 底;
    public Button 按钮;
    public TMP_Text 名称;
    public TMP_Text 说明;
    public RectTransform 进度;
    public GameObject 选中高亮;
}

// 逐级行引用：逐级表里的一行（只有两个文本）。
//   `符`   = √ 已解锁 / → 下一级 / · 未解锁；
//   `文本` = "N 级　<该级内容>"。
[Serializable]
public class 逐级行引用
{
    public TMP_Text 符;
    public TMP_Text 文本;
}

// 知识子面板：角色面板 [知识] 栏页的内容（由 `角色面板` 注入 `内容区`）。
//
// 左 = **11 条知识**（一本书 = 一条知识，标识 = 书籍标识；数据源 = `DataService.物品` 里 `书籍种类=="知识"` 的那些）：
//   每条两行 —— 上行 = 名称；下行 = `3 / 10 级　还差 2 本《X》`（外加一条 `进度`）。
//   未掌握的**也列**（"未入门　共 10 级　怎么获得：搜刮 / 图书馆"）—— 那是"还没解锁的东西"的说明书，藏起来反而看不见目标。
// 右 = 点某一行之后显示那条知识的**逐级表**（√ 已解锁 / → 下一级 / · 未解锁），每级一行。
//
// ★ 本批口径（用户点名的两条，都要守）：
//   ① **外观 0 行代码**：颜色、字号、尺寸一律归预制体（在 Unity 里调）。
//      状态差异只用两样：文字内容本身 + **结构手段**（`SetActive` 预置高亮子物体 / `Image.fillAmount`）。
//      原来的 `正文色/次要色/强调色/恢复色/选中底` 字段、以及每一处 `Image` / `TMP_Text` 的颜色写入
//      **全部删掉** —— 代码里再也读不到一个颜色数值。品质色也不再由代码写进富文本（那是"代码写外貌"）。
//      逐级行的三态（已解锁 / 下一级 / 未解锁）在预制体里只有一颗 `符` 文本，装不下三种颜色 →
//      按用户给的口径退化成"**只写文字与符（√ / → / ·）、不写颜色**"。
//   ② **引用优先**：11 条知识行（含行内 `名称/说明/进度`）、`知识列` / `逐级表` / `逐级小标题`、
//      10 条逐级行、两个行模板都在 Inspector 里接；引用为空才按名字找（名字只是兜底），
//      名字也没有就**补建一个不带样式的空节点**（挂对父、名字对、组件齐）—— 绝不"只报缺引用让用户去搭"。
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
    //   ★ 三态的差异**只靠这三个符号**：颜色归预制体，代码不写任何颜色数值。
    private const string 符已解锁 = "√";
    private const string 符下一级 = "→";
    private const string 符未解锁 = "·";

    // ================= 注入：`内容区` 由 角色面板 给（预制体里已接好） =================
    [SerializeField] private RectTransform 内容区;

    // ================= 引用（Inspector 里接；空 → 按名字找 → 还没有就补建空节点） =================
    [SerializeField] private RectTransform 知识页内容;      // 本页的根（预制体里：`内容区/知识页内容`）
    // ★ 下标 0..10 ↔ **排序后的知识书列表**的第 0..10 条。
    //   顺序来源 = `知识书()`：`物品` 里 `书籍种类=="知识"` 且 `知识等级` 非空的书，
    //   **按 `标识` 字典序**（`string.CompareOrdinal`）排 —— 顺序稳定，换存档/换机器都一样。
    //   `知识行[0]` 就是排第一的那本书，与它在预制体里的兄弟次序**无关**。
    //   数据比 11 条多 → 多出来的行 `Instantiate(知识行模板)`。
    [SerializeField] private 知识行引用[] 知识行;
    [SerializeField] private RectTransform 知识列;          // 左列容器（名字兜底：`知识列`）
    [SerializeField] private RectTransform 逐级表;          // 右表容器（名字兜底：`逐级表`）
    [SerializeField] private TMP_Text 逐级小标题;           // 右表小标题（名字兜底：`逐级表/小标题`）
    // ★ 下标 0..9 ↔ **第 1..10 级**：`逐级行[0]` = 第 1 级，`逐级行[9]` = 第 10 级。
    //   级号以数据里的 `知识等级[i].级` 为准（缺省 = 下标 + 1），本数组只是"第 i 级显示在哪个节点"。
    [SerializeField] private 逐级行引用[] 逐级行;
    [SerializeField] private RectTransform 逐级行模板;      // 逐级行模板（数据比烘好的多时 Instantiate 它）
    [SerializeField] private RectTransform 知识行模板;      // 知识行模板（数据比烘好的多时 Instantiate 它）

    private readonly List<知识行件> 左行 = new List<知识行件>();
    private readonly List<逐级行件> 右行 = new List<逐级行件>();
    private readonly List<string> 已列标识 = new List<string>();   // 左列当前显示的是哪几条（点第 N 行 → 反查标识）
    private readonly List<string> 缺的节点 = new List<string>();
    private readonly List<GameObject> 补的行 = new List<GameObject>();   // 只有"数据比烘好的多"时才用得上

    private RectTransform 画布;
    private string 选中标识;
    private bool 已绑;

    // 左列一行：矩形 + 按钮 + 名称 + 说明 + 进度图（null = 预制体里还没有"进度"节点 → 那就只写文字）+ 选中高亮
    private sealed class 知识行件
    {
        public RectTransform 矩形;
        public Button 按钮;
        public TMP_Text 名称;
        public TMP_Text 说明;
        public Image 进度图;
        public GameObject 选中高亮;
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

    // ================= 绑位（引用优先 → 名字兜底 → 补建空节点） =================

    private void 绑位()
    {
        if (已绑) return;
        已绑 = true;
        收掉补的行();
        左行.Clear();
        右行.Clear();
        已列标识.Clear();
        缺的节点.Clear();
        选中标识 = null;

        画布 = 知识页内容 != null ? 知识页内容 : 找或建矩形(内容区, "知识页内容");
        if (画布 == null) { 缺("知识页内容"); 报缺(); return; }
        知识页内容 = 画布;

        知识列 = 知识列 != null ? 知识列 : 找或建矩形(画布, "知识列");
        逐级表 = 逐级表 != null ? 逐级表 : 找或建矩形(画布, "逐级表");
        if (知识列 == null) 缺("知识列");
        if (逐级表 == null) { 缺("逐级表"); 报缺(); return; }
        逐级小标题 = 取或建文本(逐级小标题, 逐级表, "小标题");
        报缺();
    }

    // ================= 刷新 =================

    public void 刷新()
    {
        绑位();
        if (画布 == null || 知识列 == null || 逐级表 == null) return;
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

    // 左列要显示 `需要` 行：
    //   · 先把"引用数组里接了的 / 预制体里已经烘好的 `知识` 行"全部绑上（多出来的靠 SetActive 收起）；
    //   · 还不够 → `Instantiate(知识行模板)`；模板没接也没找到 → 克隆最后一个现成行（样式仍跟预制体走）；
    //     一个现成行都没有 → 补建一个不带样式的空行（组件齐）。
    private void 备左行(int 需要)
    {
        int 预置 = 名字行数(知识列, "知识");
        int 引用数 = 知识行 != null ? 知识行.Length : 0;
        int 目标 = Mathf.Max(需要, Mathf.Max(预置, 引用数));
        while (左行.Count < 目标)
        {
            int 序 = 左行.Count;
            var 引 = 知识行 != null && 序 < 知识行.Length ? 知识行[序] : null;
            var 矩形 = 矩形of(引);                                   // ① 引用优先
            if (矩形 == null && 序 < 预置) 矩形 = 名字行(知识列, "知识", 序);   // ② 名字兜底（第 序 个 `知识`）
            if (矩形 == null)                                        // ③ 运行时新增（数据比烘好的多）
            {
                矩形 = 造左行(序);
                if (矩形 == null) return;
                摆下一行(矩形, 序 > 0 ? 左行[序 - 1].矩形 : null);
            }
            左行.Add(绑左行(序, 矩形, 引));
        }
    }

    private RectTransform 造左行(int 序)
    {
        var 模板 = 找知识行模板();
        if (模板 != null) return 克隆(模板, 知识列, "知识");
        缺("知识行模板");
        var 源 = 序 > 0 ? 左行[序 - 1].矩形 : null;   // 退化①：克隆最后一个现成行（样式跟预制体一致）
        if (源 != null) return 克隆(源, 知识列, "知识");
        return 新矩形("知识", 知识列);                 // 退化②：补建不带样式的空行
    }

    // 知识行模板：Inspector 引用优先 → 名字 `知识行`（在 `知识页内容` 下或 `知识列` 下）
    private RectTransform 找知识行模板()
    {
        if (知识行模板 != null) return 知识行模板;
        var 现成 = 子矩形(画布, "知识行");
        if (现成 != null) return 现成;
        return 子矩形(知识列, "知识行");
    }

    private 知识行件 绑左行(int 序, RectTransform 矩形, 知识行引用 引)
    {
        if (引 == null) 引 = new 知识行引用();
        if (知识行 != null && 序 < 知识行.Length && 知识行[序] == null) 知识行[序] = 引;

        // 组件齐：行身上该有 Image（接射线）与 Button（点选）
        if (引.底 == null) 引.底 = 矩形.GetComponent<Image>();
        if (引.按钮 == null) 引.按钮 = 矩形.GetComponent<Button>();

        var 件 = new 知识行件
        {
            矩形 = 矩形,
            按钮 = 引.按钮,
            名称 = 取或建文本(引.名称, 矩形, "名称"),
            说明 = 取或建文本(引.说明, 矩形, "说明"),
            进度图 = 绑进度(引.进度, 矩形),
            选中高亮 = 引.选中高亮 != null ? 引.选中高亮 : 子物体(矩形, "选中高亮"),
        };

        // 点击：只接事件（悬停 / 过渡沿用预制体里烘好的那一套，本组件不碰）
        if (件.按钮 != null)
        {
            if (件.按钮.targetGraphic == null && 引.底 != null) 件.按钮.targetGraphic = 引.底;
            件.按钮.onClick.RemoveAllListeners();
            int 序号 = 序;   // 闭包要捕获"本行的下标"，别用循环变量
            件.按钮.onClick.AddListener(() => 点知识(序号));
        }
        return 件;
    }

    private void 刷左行(知识行件 行, 物品数据 书, 玩家档案 玩家)
    {
        int 总级 = 书.知识等级.Length;
        int 级 = 玩家.知识等级(书.标识);            // 0 = 还没掌握
        int 级内经验 = 玩家.查知识(书.标识)?.经验 ?? 0;
        bool 已掌握 = 级 > 0;

        // 名称：纯文本（品质色不再由代码写进富文本 —— 本批口径①：代码不写外貌）
        面板基类.设文本(行.名称, 书.标识);
        // 选中 → 亮预制体里的 `选中高亮` 子物体（结构手段，代码不碰颜色）
        if (行.选中高亮 != null) 行.选中高亮.SetActive(书.标识 == 选中标识);
        // 进度 → 只写 `fillAmount`（已解锁级数 / 总级数）；预制体里没有 `进度` 节点就只写文字
        if (行.进度图 != null) 行.进度图.fillAmount = 总级 > 0 ? Mathf.Clamp01(级 / (float)总级) : 0f;
        if (行.说明 == null) return;

        if (!已掌握)
        {
            面板基类.设文本(行.说明, $"未入门　共 {总级} 级　{获得提示}");
            return;
        }

        int 下一门槛 = 下一级门槛(书, 玩家, 级);
        if (下一门槛 < 0)
        {
            面板基类.设文本(行.说明, $"{级} / {总级} 级　已满级");
            return;
        }

        int 还差经验 = Mathf.Max(0, 下一门槛 - 级内经验);
        int 每本 = 每本经验(书, 玩家);
        int 本数 = Mathf.CeilToInt(还差经验 / (float)Mathf.Max(1, 每本));
        面板基类.设文本(行.说明, 本数 <= 0
            ? $"{级} / {总级} 级　再读一本即可升级"
            : $"{级} / {总级} 级　还差 {本数} 本《{书.标识}》");
    }

    // 进度：**选的是"写 `Image.fillAmount`"**（不是切"亮/暗"子物体的 SetActive）。
    //   为什么选它：预制体里没有"亮/暗两套子物体"，而 fillAmount 只改一个数值 ——
    //   填充色 / 底色 / 圆角 / 长度全留在预制体里，符合"外观归预制体"。
    //   代价：`进度` 那个节点得是 Image 且 **类型 = Filled、填充方式 = Horizontal**，否则填不动
    //   （补建的兜底节点会顺手把这两项设上；预制体里请自己设，加张精灵图才看得见）。
    private Image 绑进度(RectTransform 现成, RectTransform 行矩形)
    {
        var 节点 = 现成 != null ? 现成 : 子矩形(行矩形, "进度");
        if (节点 == null)
        {
            缺("知识行/进度（预制体里给每条知识行加一个进度子物体）");
            节点 = 新矩形("进度", 行矩形);
            if (节点 == null) return null;
            摆进度(节点);   // 只在"补建"时摆位；预制体里预置的节点一概不碰
        }
        var 图 = 节点.GetComponent<Image>();
        if (图 == null) 图 = 节点.gameObject.AddComponent<Image>();
        // 这两项是"让 fillAmount 生效"的功能参数（不是颜色/字号/尺寸），对外观无话可说
        图.type = Image.Type.Filled;
        图.fillMethod = Image.FillMethod.Horizontal;
        return 图;
    }

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
            面板基类.设文本(逐级小标题, "逐级表");
            备右行(1);
            刷右行(0, "", "点左边一条知识，这里显示它每一级解锁什么。", true);
            for (int i = 1; i < 右行.Count; i++) 显右行(i, false);
            return;
        }

        int 总级 = 书.知识等级.Length;
        int 当前 = 玩家.知识等级(书.标识);
        面板基类.设文本(逐级小标题, $"《{书.标识}》逐级表　{当前} / {总级}");
        备右行(总级);

        for (int i = 0; i < 右行.Count; i++)
        {
            if (i >= 总级) { 显右行(i, false); continue; }
            var 等级行 = 书.知识等级[i];
            int 级 = 等级行 != null && 等级行.级 > 0 ? 等级行.级 : i + 1;

            // 三态 = √ 已解锁（级 <= 当前） / → 下一级（级 == 当前 + 1） / · 未解锁。
            //   ★ 只写字与符、不写颜色：预制体里一行只有一颗 `符` 文本，一个节点装不下三种颜色，
            //     而"加三个高亮子物体"预制体里也没有 —— 按用户口径退化成"符号区分"。
            刷右行(i, 级 <= 当前 ? 符已解锁 : (级 == 当前 + 1 ? 符下一级 : 符未解锁),
                   $"{级} 级　{等级内容(数据, 等级行)}", true);
        }
    }

    // 逐级行要显示 `需要` 行：引用数组里的先绑、预制体里的 `逐级` 行也全绑（多的靠 SetActive 收起），
    //   不够再 `Instantiate(逐级行模板)`（模板没接 → 克隆最后一个现成行 → 补建空节点）。
    //   ★ 下标 0..9 ↔ 第 1..10 级。
    private void 备右行(int 需要)
    {
        int 预置 = 名字行数(逐级表, "逐级");
        int 引用数 = 逐级行 != null ? 逐级行.Length : 0;
        int 目标 = Mathf.Max(需要, Mathf.Max(预置, 引用数));
        while (右行.Count < 目标)
        {
            int 序 = 右行.Count;
            var 引 = 逐级行 != null && 序 < 逐级行.Length ? 逐级行[序] : null;
            var 矩形 = 矩形of(引);
            if (矩形 == null && 序 < 预置) 矩形 = 名字行(逐级表, "逐级", 序);
            if (矩形 == null)
            {
                矩形 = 造右行(序);
                if (矩形 == null) return;
                摆下一行(矩形, 序 > 0 ? 右行[序 - 1].矩形 : null);
            }
            右行.Add(绑右行(矩形, 引));
        }
    }

    private RectTransform 造右行(int 序)
    {
        var 模板 = 找逐级行模板();
        if (模板 != null) return 克隆(模板, 逐级表, "逐级");
        缺("逐级行模板");
        var 源 = 序 > 0 ? 右行[序 - 1].矩形 : null;
        if (源 != null) return 克隆(源, 逐级表, "逐级");
        return 新矩形("逐级", 逐级表);
    }

    // 逐级行模板：Inspector 引用优先 → 名字 `逐级行`（在 `逐级表` 下）
    private RectTransform 找逐级行模板()
    {
        if (逐级行模板 != null) return 逐级行模板;
        return 子矩形(逐级表, "逐级行");
    }

    private 逐级行件 绑右行(RectTransform 矩形, 逐级行引用 引)
    {
        if (引 == null) 引 = new 逐级行引用();
        return new 逐级行件
        {
            矩形 = 矩形,
            符 = 取或建文本(引.符, 矩形, "符"),
            文本 = 取或建文本(引.文本, 矩形, "文本"),
        };
    }

    private void 刷右行(int 序, string 符, string 文本, bool 显示)
    {
        if (序 < 0 || 序 >= 右行.Count) return;
        var 行 = 右行[序];
        if (行.矩形 == null) return;
        if (行.矩形.gameObject.activeSelf != 显示) 行.矩形.gameObject.SetActive(显示);
        if (!显示) return;
        面板基类.设文本(行.符, 符);
        面板基类.设文本(行.文本, 文本);
    }

    private void 显右行(int 序, bool 显示)
    {
        if (序 < 0 || 序 >= 右行.Count) return;
        var 矩形 = 右行[序].矩形;
        if (矩形 != null && 矩形.gameObject.activeSelf != 显示) 矩形.gameObject.SetActive(显示);
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

    // ================= 找节点 / 补节点 的小工具 =================

    // 引用组里"哪一个是这几个引用共同的节点"：行身上的 Image / Button / 名称 / 说明。
    //   ⚠ `进度` 是行的**子物体** → 只有它被接上时，退回它的父节点当这一行。
    private static RectTransform 矩形of(知识行引用 引)
    {
        if (引 == null) return null;
        if (引.底 != null) return 引.底.rectTransform;
        if (引.按钮 != null) return 引.按钮.transform as RectTransform;
        if (引.名称 != null) return 引.名称.rectTransform;
        if (引.说明 != null) return 引.说明.rectTransform;
        if (引.进度 != null) return 引.进度.parent as RectTransform;
        return null;
    }

    // 逐级行引用只有两个文本，行的节点 = 它们的父
    private static RectTransform 矩形of(逐级行引用 引)
    {
        if (引 == null) return null;
        if (引.符 != null) return 引.符.transform.parent as RectTransform;
        if (引.文本 != null) return 引.文本.transform.parent as RectTransform;
        return null;
    }

    // 父下第 i 个叫 `名` 的子节点（预制体里烘好的行序 → 第 i 行）
    private static RectTransform 名字行(RectTransform 父, string 名, int i)
    {
        if (父 == null || i < 0) return null;
        int 计 = 0;
        for (int k = 0; k < 父.childCount; k++)
        {
            var 子 = 父.GetChild(k) as RectTransform;
            if (子 == null || 子.name != 名) continue;
            if (计 == i) return 子;
            计++;
        }
        return null;
    }

    private static int 名字行数(RectTransform 父, string 名)
    {
        if (父 == null) return 0;
        int 计 = 0;
        for (int k = 0; k < 父.childCount; k++)
        {
            var 子 = 父.GetChild(k);
            if (子 != null && 子.name == 名) 计++;
        }
        return 计;
    }

    // 克隆一行：跟预制体走的名字/样式/尺寸；`补的行` 记账，重绑时收掉
    private RectTransform 克隆(RectTransform 模板, RectTransform 父, string 名)
    {
        if (模板 == null || 父 == null) return null;
        var 物 = Instantiate(模板.gameObject, 父, false);
        物.name = 名;
        物.SetActive(true);
        补的行.Add(物);
        return (RectTransform)物.transform;
    }

    // 新行排在上一行下面一行：行高 / 起点都取上一行的 rect（代码不写死任何数字）
    private static void 摆下一行(RectTransform 新行, RectTransform 上一行)
    {
        if (新行 == null) return;
        if (上一行 == null) { 新行.anchoredPosition = Vector2.zero; return; }
        新行.anchoredPosition = new Vector2(上一行.anchoredPosition.x,
                                            上一行.anchoredPosition.y - 上一行.rect.height);
    }

    // 兜底"进度"节点的摆位（只在代码补建时用；预制体里预置的节点一概不碰）
    private static void 摆进度(RectTransform 节点)
    {
        if (节点 == null) return;
        节点.anchorMin = new Vector2(0f, 0f);
        节点.anchorMax = new Vector2(1f, 0f);
        节点.pivot = new Vector2(0.5f, 0f);
        节点.anchoredPosition = new Vector2(0f, 4f);
        节点.sizeDelta = new Vector2(-24f, 4f);
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
        Debug.LogError("[知识子面板] 预制体里缺这些节点/引用：" + string.Join("、", 缺的节点) +
                       "。缺失的引用已按名字兜底、按名字补建（挂对父、名字对、组件齐，**不带任何外观**）；" +
                       "「高亮」这类子物体**不补建**（补出来只会是个白块），只是不改外观。" +
                       "请在 `Assets/Resources/Prefab/角色面板.prefab` 里预置同名节点 / 在 Inspector 上接好引用。");
    }

    private RectTransform 新矩形(string 名, Transform 父)
    {
        if (父 == null) return null;
        var 物体 = new GameObject(名, typeof(RectTransform));
        物体.transform.SetParent(父, false);
        补的行.Add(物体);
        return (RectTransform)物体.transform;
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
