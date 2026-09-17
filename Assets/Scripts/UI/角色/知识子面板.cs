using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 知识子面板：角色面板 [知识] 栏页的内容（由 `角色面板` 注入 `内容区`；本组件**不被壳读任何字段**）。
//
// 左 = **11 条知识**（一本书 = 一条知识，标识 = 书籍标识；数据源 = `DataService.物品` 里 `书籍种类=="知识"` 的那些）：
//   每条一行，显示 名称（品质色） · `等级/总级数` · 分段进度 · **"还差 N 本《X》"**。
//   未掌握的**也列**（灰 + "怎么获得：搜刮 / 图书馆"）—— 那是"还没解锁的东西"的说明书，藏起来反而看不见目标。
// 右 = 点某一行之后显示那条知识的**逐级表**（✔ 已解锁 / ▶ 下一级 / 灰 未解锁），每级一行 `描述`。
//
// 经验口径（读 `玩家档案.已掌握知识` + 书的 `知识门槛` / `知识等级` / `知识经验`）：
//   · `知识门槛[级-1]` = **升到该级所需的累计经验**（不是每级增量，见 `物品数据.知识门槛` 的注释）；
//   · 每次读满一本给 `物品数据.知识经验`（缺省 100）；天赋「学霸」×1.15 —— 这个乘数在
//     `成长管理器.加知识经验` 里算，本组件只把同一个乘数取出来用于**预告**（两边必须一致，见 每本经验）。
//
// 配色/字号/间距**全部内联在本文件自己的 [SerializeField] 字段里**（没有"皮肤"这个中间层）。
public sealed class 知识子面板 : MonoBehaviour
{
    // 没有"这本书从哪来"的数据字段（`物品数据` 里没有来源/出处那类东西）→ 未掌握那条统一写这句静态提示。
    //   口径 = 用户点名的"怎么获得：搜刮 / 图书馆"。
    private const string 获得提示 = "怎么获得：搜刮 / 图书馆";

    // ================= 注入：`内容区` 由 角色面板 给 =================
    [SerializeField] private RectTransform 内容区;

    // ================= 颜色（与壳同一套十六进制值；没有共用参数类，同一色值各文件各写一份是有意的） =================
    [SerializeField] private Color 正文色 = new Color(0.8941f, 0.8745f, 0.8392f, 1f);   // #E4DFD6
    [SerializeField] private Color 次要色 = new Color(0.5412f, 0.5137f, 0.4706f, 1f);   // #8A8378
    [SerializeField] private Color 强调色 = new Color(0.7059f, 0.3333f, 0.2353f, 1f);   // #B4553C 唯一强调色
    [SerializeField] private Color 恢复色 = new Color(0.4314f, 0.5608f, 0.3843f, 1f);   // #6E8F62 已解锁语义色
    [SerializeField] private Color 边框色 = new Color(0.2275f, 0.2118f, 0.1882f, 1f);   // #3A3630 进度格空槽
    [SerializeField] private Color 内容底色 = new Color(0.1020f, 0.0941f, 0.0824f, 1f); // #1A1815 行底
    [SerializeField] private Color 悬停底 = new Color(0.1490f, 0.1333f, 0.1255f, 1f);   // #262220 悬停底
    [SerializeField] private Color 选中底 = new Color(0.1490f, 0.1333f, 0.1255f, 1f);   // #262220 选中行底

    // ================= 字号 / 间距 / 尺寸 =================
    [SerializeField] private int 字号_正文 = 19;
    [SerializeField] private int 字号_小字 = 16;
    [SerializeField] private float 行高 = 44f;
    [SerializeField] private float 段距 = 20f;
    [SerializeField] private float 段间隔 = 12f;
    [SerializeField] private int 进度格数 = 10;
    [SerializeField] private float 进度格宽 = 10f;
    [SerializeField] private float 进度格高 = 10f;
    [SerializeField] private float 进度格间隔 = 2f;

    // ================= 本组件自建的节点账本（幂等重建用） =================
    [SerializeField] private List<GameObject> 自建节点 = new List<GameObject>();

    private RectTransform 画布;
    private RectTransform 右列;                       // 逐级表的容器
    private TMP_Text 右标题;
    private readonly List<知识行件> 左行表 = new List<知识行件>();
    private readonly List<逐级行件> 右行表 = new List<逐级行件>();
    private readonly List<string> 已列标识 = new List<string>();   // 左列当前显示的是哪几条（只在真的变了才重建行）
    private string 选中标识;
    private bool 已建;

    // 左列一行：名称 + 等级 + 分段进度 + "还差 N 本"
    private sealed class 知识行件
    {
        public RectTransform 矩形;
        public Image 底;          // 行底：选中/常态就改它（挂在行物体自己身上，不是子节点）
        public TMP_Text 名称;
        public TMP_Text 说明;
        public TMP_Text 还差;
        public readonly List<Image> 格 = new List<Image>();
    }

    // 右列一行：✔/▶/灰 + 级数 + 该级描述
    private sealed class 逐级行件
    {
        public RectTransform 矩形;
        public TMP_Text 符号;
        public TMP_Text 文本;
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

        画布 = 新矩形("知识页内容", 内容区);
        定锚(画布, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f));
        画布.offsetMin = new Vector2(内边, 内边);
        画布.offsetMax = new Vector2(-内边, -栏目标题带高);

        建右列();   // 先建右列（左列的行点它）

        已建 = true;
        刷新();
    }

    private float 栏目标题带高 => 24f + 2f + 段距;
    private float 内边 => 段距 / 2f;

    // 右列：小标题 + 逐级表（行按选中那条的级数补建，见 刷右列）
    private void 建右列()
    {
        右列 = 新矩形("逐级表", 画布);
        定锚(右列, new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        右列.offsetMin = Vector2.zero;
        右列.offsetMax = Vector2.zero;

        右标题 = 文本("小标题", 右列, 字号_小字, 次要色, TextAlignmentOptions.Left);
        定锚(右标题.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        右标题.rectTransform.offsetMin = new Vector2(0f, -字号_小字 - 8f);
        右标题.rectTransform.offsetMax = Vector2.zero;
    }

    // ================= 刷新 =================

    public void 刷新()
    {
        if (!已建) return;
        var 玩家 = 当前档案();
        var 数据 = ServiceRegistry.已注册<DataService>() ? ServiceRegistry.Get<DataService>() : null;
        if (玩家 == null || 数据 == null) return;

        var 书表 = 知识书(数据);
        记标识(书表);            // 点知识(序) 要按序号反查标识 → 先把这次的标识表存下来（必须在建/刷行之前）
        建左行(书表.Count);

        for (int i = 0; i < 左行表.Count; i++)
        {
            var 行 = 左行表[i];
            if (行.矩形 == null) continue;
            bool 有 = i < 书表.Count;
            行.矩形.gameObject.SetActive(有);
            if (!有) continue;
            刷左行(行, 书表[i], 玩家);
        }
        摆左行();
        刷右列(玩家, 数据);

        // 选中那条已经不在表里了（数据改了）→ 清掉，免得右列指着一条不存在的东西
        if (!string.IsNullOrEmpty(选中标识) && !书表.Exists(b => b.标识 == 选中标识)) 选中标识 = null;
    }

    private static 玩家档案 当前档案()
        => ServiceRegistry.已注册<PlayerService>() ? ServiceRegistry.Get<PlayerService>()?.档案 : null;

    // 全部知识书，按标识排序（顺序稳定，换存档/换机器都一样）
    private static List<物品数据> 知识书(DataService 数据)
    {
        var 表 = new List<物品数据>();
        if (数据?.物品 == null) return 表;
        foreach (var 对 in 数据.物品)
            if (对.Value != null && 对.Value.书籍种类 == "知识" && 对.Value.知识等级 != null && 对.Value.知识等级.Length > 0)
                表.Add(对.Value);
        表.Sort((a, b) => string.CompareOrdinal(a.标识, b.标识));
        return 表;
    }

    // 左列行数按知识条数补（只增不减 —— 数据条数固定 11，重进本页不会反复销毁重建）
    private void 建左行(int 需要)
    {
        while (左行表.Count < 需要)
        {
            var 矩形 = 新矩形("知识", 画布);
            定锚(矩形, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f));
            // 高度与位置在 摆左行 里按序号定
            var 底 = 矩形.gameObject.AddComponent<Image>();
            底.color = 内容底色;
            底.raycastTarget = true;

            float 内 = 段距 * 0.4f;
            var 名称 = 文本("名称", 矩形, 字号_正文, 正文色, TextAlignmentOptions.MidlineLeft, false);
            定锚(名称.rectTransform, new Vector2(0f, 1f), new Vector2(0.62f, 1f), new Vector2(0f, 1f));
            名称.rectTransform.offsetMin = new Vector2(内, -(内 + 字号_正文 + 6f));
            名称.rectTransform.offsetMax = new Vector2(0f, -内);

            var 说明 = 文本("说明", 矩形, 字号_小字, 次要色, TextAlignmentOptions.MidlineRight, false);
            定锚(说明.rectTransform, new Vector2(0.62f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            说明.rectTransform.offsetMin = new Vector2(0f, -(内 + 字号_正文 + 6f));
            说明.rectTransform.offsetMax = new Vector2(-内, -内);

            // 进度格：10 个 10×10、间隔 2（与身份条那条经验格同一套观感）
            var 格父 = 新矩形("进度", 矩形, false);
            定锚(格父, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            格父.anchoredPosition = new Vector2(内, -(内 + 字号_正文 + 8f));
            格父.sizeDelta = new Vector2(进度格数 * (进度格宽 + 进度格间隔), 进度格高);
            var 行件 = new 知识行件 { 矩形 = 矩形, 底 = 底, 名称 = 名称, 说明 = 说明 };
            for (int i = 0; i < 进度格数; i++)
            {
                var 格 = 新矩形("格", 格父, false);
                定锚(格, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
                格.anchoredPosition = new Vector2(i * (进度格宽 + 进度格间隔), 0f);
                格.sizeDelta = new Vector2(进度格宽, 0f);
                var 图 = 格.gameObject.AddComponent<Image>();
                图.color = 边框色;
                图.raycastTarget = false;
                行件.格.Add(图);
            }

            var 还差 = 文本("还差", 矩形, 字号_小字, 次要色, TextAlignmentOptions.MidlineLeft, false);
            定锚(还差.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            还差.rectTransform.offsetMin = new Vector2(内, -(行高 - 内));
            还差.rectTransform.offsetMax = new Vector2(-内, -(内 + 字号_正文 + 8f + 进度格高 + 2f));
            行件.还差 = 还差;

            int 序 = 左行表.Count;
            var 钮 = 矩形.gameObject.AddComponent<Button>();
            钮.targetGraphic = 底;
            钮.onClick.AddListener(() => 点知识(序));
            接按钮(钮);

            左行表.Add(行件);
        }
    }

    private void 刷左行(知识行件 行, 物品数据 书, 玩家档案 玩家)
    {
        int 总级 = 书.知识等级.Length;
        int 级 = 玩家.知识等级(书.标识);            // 0 = 还没掌握
        int 级内经验 = 玩家.查知识(书.标识)?.经验 ?? 0;
        bool 已掌握 = 级 > 0;

        行.名称.text = $"<color=#{ColorUtility.ToHtmlStringRGB(品质工具.颜色(书.品质档))}>{书.标识}</color>";
        行.说明.text = 已掌握 ? $"{级} / {总级} 级" : $"未入门　共 {总级} 级";
        行.底.color = 书.标识 == 选中标识 ? 选中底 : 内容底色;

        // 进度：升到"下一级"的累计门槛 - 当前级内已攒的经验；满了（或未掌握 = 0 级）另说
        int 下一门槛 = 级 >= 总级 ? -1 : (书.知识门槛 != null && 级 < 书.知识门槛.Length
            ? 书.知识门槛[级]                                            // 门槛[级] = 升到 级+1 所需的累计经验
            : 玩家档案.知识每级经验 * (级 + 1));
        if (!已掌握)
        {
            // 未掌握：进度条不显示进度（0 格），提示改成"怎么获得"
            foreach (var 格 in 行.格) 格.color = 边框色;
            行.还差.text = 获得提示;
            行.还差.color = 次要色;
            return;
        }
        if (下一门槛 < 0)
        {
            // 已满级：格子全亮 + "已满级"
            foreach (var 格 in 行.格) 格.color = 强调色;
            行.还差.text = "已满级";
            行.还差.color = 恢复色;
            return;
        }

        float 比例 = Mathf.Clamp01(级内经验 / (float)Mathf.Max(1, 下一门槛));
        int 亮 = Mathf.Clamp(Mathf.CeilToInt(比例 * 行.格.Count), 0, 行.格.Count);
        for (int i = 0; i < 行.格.Count; i++) 行.格[i].color = i < 亮 ? 强调色 : 边框色;

        int 还差经验 = Mathf.Max(0, 下一门槛 - 级内经验);
        int 每本 = 每本经验(书, 玩家);
        int 本数 = Mathf.CeilToInt(还差经验 / (float)Mathf.Max(1, 每本));
        行.还差.text = 本数 <= 0 ? "再读一本即可升级" : $"还差 {本数} 本《{书.标识}》";
        行.还差.color = 次要色;
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

    // ================= 右列：逐级表 =================

    private void 刷右列(玩家档案 玩家, DataService 数据)
    {
        var 书 = string.IsNullOrEmpty(选中标识) ? null : (数据.物品.TryGetValue(选中标识, out var 找) ? 找 : null);
        if (书 == null)
        {
            面板基类.设文本(右标题, "逐级表");
            建右行(1);
            右行表[0].矩形.gameObject.SetActive(true);
            面板基类.设文本(右行表[0].符号, "");
            面板基类.设文本(右行表[0].文本, "点左边一条知识，这里显示它每一级解锁什么。");
            右行表[0].文本.color = 次要色;
            for (int i = 1; i < 右行表.Count; i++) 右行表[i].矩形.gameObject.SetActive(false);
            摆右行();
            return;
        }

        int 总级 = 书.知识等级.Length;
        int 当前 = 玩家.知识等级(书.标识);
        面板基类.设文本(右标题, $"《{书.标识}》逐级表　{当前} / {总级}");
        建右行(总级);

        for (int i = 0; i < 右行表.Count; i++)
        {
            var 行 = 右行表[i];
            if (行.矩形 == null) continue;
            if (i >= 总级) { 行.矩形.gameObject.SetActive(false); continue; }
            行.矩形.gameObject.SetActive(true);
            var 等级行 = 书.知识等级[i];
            int 级 = 等级行 != null && 等级行.级 > 0 ? 等级行.级 : i + 1;

            // ✔ 已解锁（级 <= 当前） / ▶ 下一级（级 == 当前+1） / 灰 未解锁
            string 符;
            Color 色;
            if (级 <= 当前) { 符 = "✔"; 色 = 恢复色; }
            else if (级 == 当前 + 1) { 符 = "▶"; 色 = 强调色; }
            else { 符 = "·"; 色 = 次要色; }

            面板基类.设文本(行.符号, 符);
            行.符号.color = 色;
            string 描述 = 等级行 != null ? 等级行.描述 : "";
            if (string.IsNullOrEmpty(描述)) 描述 = 等级行 != null && 等级行.加成值 != 0f
                ? $"{等级行.加成属性} +{等级行.加成值}"
                : "（本级无内容）";
            面板基类.设文本(行.文本, $"{级} 级　{描述}");
            行.文本.color = 色;
        }
        摆右行();
    }

    private void 建右行(int 需要)
    {
        while (右行表.Count < 需要)
        {
            var 矩形 = 新矩形("逐级", 右列);
            定锚(矩形, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            var 符号 = 文本("符", 矩形, 字号_正文, 正文色, TextAlignmentOptions.MidlineLeft, false);
            定锚(符号.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
            符号.rectTransform.sizeDelta = new Vector2(段距, 0f);
            符号.rectTransform.anchoredPosition = Vector2.zero;
            var 文本组件 = 文本("文本", 矩形, 字号_小字, 次要色, TextAlignmentOptions.MidlineLeft, false);
            定锚(文本组件.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f));
            文本组件.rectTransform.offsetMin = new Vector2(段距 + 4f, 0f);
            文本组件.rectTransform.offsetMax = Vector2.zero;
            右行表.Add(new 逐级行件 { 矩形 = 矩形, 符号 = 符号, 文本 = 文本组件 });
        }
    }

    // 逐级表从右列小标题之下开始，每行 40（比正文行矮一点：一行只有"级 + 一句"，10 级要塞进一屏）
    private void 摆右行()
    {
        float 顶 = 字号_小字 + 8f + 段间隔;
        foreach (var 行 in 右行表)
        {
            if (行.矩形 == null || !行.矩形.gameObject.activeSelf) continue;
            设行带(行.矩形, 顶, 40f);
            顶 += 40f;
        }
    }

    // 左列自上而下按 行高 排（11 条 × 44 = 484，正好落在内容区那条可用高度里）。
    // 顺带主动算一次名称/说明的文字宽 —— `末日/角色/一键生成面板预制体` 在编辑器非运行态调 重建布局()，
    //   那一帧没有第二次布局回调，不补这一步烘进预制体的就是兜底宽度。
    private void 摆左行()
    {
        for (int i = 0; i < 左行表.Count; i++)
        {
            var 行 = 左行表[i];
            if (行.矩形 == null || !行.矩形.gameObject.activeSelf) continue;
            行.名称?.ForceMeshUpdate(true);
            行.说明?.ForceMeshUpdate(true);
            设行带(行.矩形, i * 行高, 行高);
        }
    }

    // 点左边一条：选中/取消。选中之后右列显示它的逐级表。
    private void 点知识(int 序)
    {
        if (序 < 0 || 序 >= 已列标识.Count) return;
        string 标识 = 已列标识[序];
        选中标识 = 选中标识 == 标识 ? null : 标识;
        刷新();
    }

    // 左列的行要按序号知道"我是哪一条"——刷新时把标识表存下来（建行 与 点知识 都读它）
    private void 记标识(List<物品数据> 书表)
    {
        已列标识.Clear();
        foreach (var 书 in 书表) 已列标识.Add(书.标识);
    }

    // ================= 底色 / 建节点的小工具 =================

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

    private void 清掉自建()
    {
        if (自建节点 == null) 自建节点 = new List<GameObject>();
        foreach (var 物体 in 自建节点)
        {
            if (物体 == null) continue;
            销毁(物体);
        }
        自建节点.Clear();
        左行表.Clear();
        右行表.Clear();
        已列标识.Clear();
        选中标识 = null;
        画布 = null;
        右列 = null;
        右标题 = null;
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
