using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// 知识子面板 —— 角色面板「知识」页的内容（三页里的第三页）。
//
// 分工（硬边界，照 `属性子面板` 那一批定的口径）：
//   · **显隐不由本组件管**：壳（`角色面板`）只做 `SetActive` → 本类**不继承 `面板基类`**，不参与"显示面板/回退"协议。
//   · 自刷入口：`Awake()` 建行 + `OnEnable()` 全量重算（切页 = 壳把这一页 `SetActive(true)` → `OnEnable` 触发）。
//   · **不订阅任何事件**（用户定稿）：`书籍服务` 只发 `日志事件`，知识进度变了没有专门信号 →
//     本页靠 `OnEnable` 全量重算 + 点击行时重算详情，不做"事件驱动的增量刷新"（少一条订阅就少一处会漏的口径）。
//   · **外观 100% 归场景 / 预制体**：本文件不写配色、字号、尺寸常量；状态表达只用 `SetActive`
//     （选中高亮 = 一个预置节点亮了而已，不写颜色）。行高从**模板节点自己的 RectTransform** 量（见 加逐级行池），不写死数字。
//
// 数据口径（全部读 `DataService` / `玩家档案` 上已有的字段，本页**不自己发明数值**）：
//   · 知识书 = `DataService.物品` 里 `类型=="书籍" && 书籍种类=="知识"`。
//     ⚠ 口径（`刀2` 加书后定的）：**13 本 = 4 精通（近战/枪械/弓弩/医学）+ 7 制作（初级/中级/高级/刀具/弓/防具/枪械制作）+ 盾精通 + 观察**。
//       数量不符只报一条 LogError（见 `报书目数()`），**不挡面板**、也不偷偷裁书。
//   · 进度 = `玩家档案.已掌握知识`（`List<知识进度>{标识,等级,经验}`），查询走 `查知识 / 掌握知识 / 知识等级(标识)`。
//   · 门槛 = 书 `知识门槛`（int[]，累计口径，下标 = 级-1）。**下档门槛 = 知识门槛[当前级]**（不是 [当前级-1]）：
//     当前级 = 已经到达的等级，它的门槛是"升到 当前级 那次"用的；要升到 当前级+1 就得攒够 下标=当前级 那一格。
//   · 每次所得 = 书 `知识经验`（缺省 100）+ 天赋「学霸」×1.15（`成长管理器.加知识经验` 的口径）。
//     **唯一例外 = "1 级且经验 == 0"那一档不吃学霸**：首次读满只解锁 1 级、不给经验（`书籍服务.知识书成功` 的
//     "首次不进阶"分支）→ 玩家要"再读满 1 次"才能进第 2 级，那一档的所得就是原始 100（见 每次所得()）。
public sealed class 知识子面板 : MonoBehaviour
{
    // 知识家族：**精通排前面、制作排后面**（用户钦定；两段内部再按 标识 序 → 每次刷新顺序稳定，不会乱跳）。
    private const string 家族精通 = "精通";
    private const string 家族制作 = "制作";

    // 列表每行的视图（模板 → `知识行` 组件；`标识` 只在"池子扩容"时用来找占位行，运行时不改）。
    [Serializable]
    public class 知识行视图
    {
        public string 标识;
        public 知识行 行;
    }

    // —— Inspector 引用位（缺了就按节点名兜底，再缺就一条 LogError 点名，不崩）——
    [SerializeField] private RectTransform 列表区;        // 左列表容器（行克隆到这里）
    [SerializeField] private GameObject 知识行模板;        // 左列表行模板（挂 `知识行`；场景里默认 SetActive(false)）
    [SerializeField] private GameObject 逐级行模板;        // 逐级表行模板（挂 `逐级行`；场景里默认 SetActive(false)）
    [SerializeField] private TMP_Text 详情标题;            // 书名（品质着色后的富文本）
    [SerializeField] private TMP_Text 详情副行;            // "家族 · 领域 · 共 y 级"
    [SerializeField] private TMP_Text 详情状态;            // 未掌握 / "x/y 级　累计经验 E / T" / 已满级
    [SerializeField] private TMP_Text 详情描述;            // `物品数据.描述`
    [SerializeField] private TMP_Text 详情合计加成;        // 本条知识合计加成（多行；没加成时整段可隐藏）
    [SerializeField] private RectTransform 逐级表;        // 逐级表容器（逐级行克隆到这里）

    // 节点名兜底用的名字（Inspector 接上引用位就与名字无关）。
    // 一律用**精确名**："知识行模板" 不会误配到 "逐级行模板"；若还用 Contains 式匹配，"逐级行模板" 也会被知识行抢走。
    private const string 名列表区 = "列表区";
    private const string 名知识行模板 = "知识行模板";
    private const string 名逐级行模板 = "逐级行模板";
    private const string 名逐级表 = "逐级表";
    private const string 名详情标题 = "标题";
    private const string 名详情副行 = "副行";
    private const string 名详情状态 = "状态";
    private const string 名详情描述 = "描述";
    private const string 名详情合计加成 = "合计加成";
    private const string 名空列表 = "暂无知识";   // 列表为空时的兜底文本（内容**完全不随档变**，值得常驻）

    // 空列表兜底节点的常驻引用：随内容建的"暂无知识"在每次重算时会被 `清空` 删掉，
    //   于是每刷一次就 unload/reload 一次字体 —— 常驻节点（默认隐藏）则只写一次文本、之后只切显隐。
    private TMP_Text 空列表文本;

    // 领域 = **标识白名单**（用户定稿）。为什么用白名单而不是猜：领域是给人看的归类词，
    //   猜（比如取"制作"前缀）一定会把新书归错类；白名单里没有的就留空（宁可不写，也不写个错的）。
    private static readonly Dictionary<string, string> 领域表 = new Dictionary<string, string>
    {
        { "近战精通", "刀" },       { "枪械精通", "枪械" },     { "弓弩精通", "弓弩" },   { "医学精通", "医学" },
        { "初级制作", "通用材料" },  { "中级制作", "通用材料" },  { "高级制作", "通用材料" },
        { "刀具制作", "武器制作" },  { "弓制作", "武器制作" },    { "枪械制作", "武器制作" },
        { "防具制作", "防具" },
    };

    // 按百分点显示的加成属性（其余按点）——与 `装备管理器.知识加成` 的存储口径无关，只是**显示**单位。
    //   为什么列出来：暴击/命中/闪避/医疗 这四项在项目里 1 点 = 1%（见 `数据模型.加成类型` 注释），
    //   写成分数会让人以为"暴击 +2"是 2 倍；其余（攻击/防御/生命/速度/负重/潜行/弹匣容量）是原样点数。
    private static readonly HashSet<string> 按百分点 = new HashSet<string> { "暴击", "命中", "闪避", "医疗" };

    // 运行时状态
    private int 选中序号 = -1;                     // `列表` 里的下标；-1 = 没选中（列表为空 / 选中的书已不在数据里）
    private readonly List<string> 列表 = new List<string>();          // 排序后的书标识（已掌握在前、家族序、标识序）
    private readonly List<知识行视图> 行池 = new List<知识行视图>();   // 左列表行池（**只增不减**：重建只是重新绑定+切显隐）
    private readonly List<逐级行> 逐级行池 = new List<逐级行>();        // 逐级表行池（同上，克隆一次就够用了）

    // 服务引用缓存（照 `属性子面板.档案` 那条路：UI 层读 Service 是本项目既有模式）。
    // 为什么缓存：每次刷新要按 13 本书各查一次数据表，缓存省掉每行一次 ServiceRegistry 查找。
    private DataService 数据;
    private DataService 取数据() => 数据 != null ? 数据 : (数据 = ServiceRegistry.已注册<DataService>() ? ServiceRegistry.Get<DataService>() : null);

    private static 玩家档案 档案
        => ServiceRegistry.已注册<PlayerService>() ? ServiceRegistry.Get<PlayerService>()?.档案 : null;

    // ================= 生命周期 =================

    void Awake()
    {
        自找();          // Inspector → 节点名兜底（只找，不建）
        报缺引用();      // 找完再点名：报的是"引用位与节点名两边都没找到"的那些位
        全量刷新();      // 建行 + 刷一遍（`OnEnable` 紧接着还会刷一次，这里只为"先有行、再报错"的顺序）
    }

    // 切页 / 重开面板：全量重算（列表 + 详情）。**选中标识保留**；若选中的书已不在数据里 → 回退到未选中。
    void OnEnable() => 全量刷新();

    // ================= 刷新 =================

    // 全量重算：重算书单 → 重建列表 → 建/刷详情。
    // 幂等：所有值都是从 `玩家档案` / 数据表现算的，不做增量、不做缓存。
    private void 全量刷新()
    {
        var 玩家 = 档案;
        var 库 = 取数据();
        if (玩家 == null || 库 == null) return;   // 未开局 / 服务还没起来：没有可显示的东西（清空会显得像"没数据"，干脆不动）

        收书单(库);
        建列表(玩家);

        // 选中的书还在不在数据里？不在（旧档里的书被改掉标识）→ 回退到未选中，别指向一条不存在的数据。
        if (选中序号 >= 0 && (选中序号 >= 列表.Count || 查书(库, 列表[选中序号]) == null)) 选中序号 = -1;

        刷选中态();
        刷详情(玩家, 库);
    }

    // 书单：`类型=="书籍" && 书籍种类=="知识"` → 排序 = **已掌握在前** → 家族（精通 → 制作）→ 标识（确定性）。
    // 为什么"已掌握在前"要单独一段而不是按等级排：用户要的是"我手上这门书和我还没入门的"两段式，
    //   段内保持家族+标识的稳定序，读者每次打开看到的相对位置都一样。
    private void 收书单(DataService 库)
    {
        列表.Clear();
        var 玩家 = 档案;
        if (库.物品 == null) return;
        foreach (var 对 in 库.物品)
        {
            var 书 = 对.Value;
            if (书 == null || 书.类型 != "书籍" || 书.书籍种类 != "知识") continue;
            列表.Add(书.标识);
        }
        列表.Sort((甲, 乙) =>
        {
            bool 甲会 = 玩家 != null && 玩家.掌握知识(甲), 乙会 = 玩家 != null && 玩家.掌握知识(乙);
            if (甲会 != 乙会) return 甲会 ? -1 : 1;                 // 已掌握在前
            int 家 = 家族序(甲).CompareTo(家族序(乙));               // 精通 → 制作
            if (家 != 0) return 家;
            return string.CompareOrdinal(甲, 乙);                   // 标识序：**序号比较**，不受当前区域语言影响（同一份数据在哪台机器上顺序都一样）
        });
        报书目数();
    }

    // 家族：标识以"精通"结尾 = 精通家族，其余知识书 = 制作家族（数据里就是这两族）。
    private static string 家族(string 标识) => 标识 != null && 标识.EndsWith("精通", StringComparison.Ordinal) ? 家族精通 : 家族制作;
    private static int 家族序(string 标识) => 家族(标识) == 家族精通 ? 0 : 1;

    // 书目数核对：口径 = **13 本 = 4 精通 + 7 制作 + 盾精通 + 观察**（`刀2` 给数据加了 `盾精通` / `观察`
    //   这两本 —— 它们**开局就可能已掌握**，所以原来的"11 本"判据每次刷新都会刷一条 LogError 噪音）。
    // 为什么只报错不裁书：数量对不上要么是口径写旧了、要么是数据多配了，**这两种都不该由 UI 私自决定**——
    //   少显示一本 = 玩家"已掌握却查不到"；所以照全表显示，把差异喊出来让人去改数据或改口径。
    private void 报书目数()
    {
        if (列表.Count == 13) return;   // 口径一致（13 本）→ 不用出声
        Debug.LogError($"[知识子面板] 数据里「类型=书籍 且 书籍种类=知识」的书是 **{列表.Count} 本**，" +
                       "与设计口径的 13 本（4 精通 + 7 制作 + 盾精通 + 观察）不一致：" + string.Join("、", 列表) +
                       "。本页照数据全表显示（不裁书）。请核对 items_书籍.json 或更新设计口径。");
    }

    // ================= 左列表 =================

    // 重建列表：把行池里的行按新顺序**重新绑定 + 切显隐**（不 Destroy / 不 Instantiate 已够用的行）。
    // 为什么不做"先清空再建"：滚动位置与点击态都在这些行对象上，复用对象 = 复用它们的布局结果，
    //   每次刷新重建会让列表在视觉上跳一下（用户明确要避免的那件事）。
    private void 建列表(玩家档案 玩家)
    {
        if (列表区 == null || 知识行模板 == null) return;

        // 列表为空：亮出常驻的兜底文本，并把行全部收起来（不建空行）。
        if (列表.Count == 0)
        {
            备空列表();
            for (int i = 0; i < 行池.Count; i++) 行池[i].行.gameObject.SetActive(false);
            if (空列表文本 != null) 空列表文本.gameObject.SetActive(true);
            return;
        }
        if (空列表文本 != null) 空列表文本.gameObject.SetActive(false);

        for (int i = 0; i < 列表.Count; i++)
        {
            var 标识 = 列表[i];
            var 行 = 取行(i);
            if (行 == null) return;          // 模板丢了：停手（`报缺引用` 已经点过名）
            行.行.gameObject.transform.SetSiblingIndex(i);   // 顺序 = 书单顺序（池子复用时靠它重排）
            int 序 = i;                                       // 闭包捕获：循环变量必须另存一份，否则所有行都会选中最后一本
            绑行(行, 玩家, 标识, () => 点击行(序));
        }
        // 池子里多出来的（书变少了：旧档/改数据）→ 收起来，不删（下次书又多了不用重建）
        for (int i = 列表.Count; i < 行池.Count; i++) 行池[i].行.gameObject.SetActive(false);
    }

    // 池子取第 i 行：不够就地克隆一个（模板行默认隐藏，`创建模板` 已经负责激活）。
    private 知识行视图 取行(int 序)
    {
        while (行池.Count <= 序)
        {
            var 组件 = 面板基类.创建模板<知识行>(列表区, 知识行模板);
            if (组件 == null) return null;
            行池.Add(new 知识行视图 { 标识 = null, 行 = 组件 });
        }
        return 行池[序];
    }

    // 绑定一行：名称（品质着色）· x/y 级 · 累计经验 E/T · 还差 N 本 · 持有 M 本 · 未掌握提示 · 选中高亮。
    private void 绑行(知识行视图 行, 玩家档案 玩家, string 标识, UnityEngine.Events.UnityAction 点击)
    {
        var 库 = 取数据();
        var 书 = 查书(库, 标识);
        if (书 == null) { 行.行.gameObject.SetActive(false); return; }

        行.标识 = 标识;
        int 上限 = 级数(书);
        int 级 = 玩家.知识等级(标识);                                  // 未掌握 = 0
        int 经验 = 玩家.查知识(标识)?.经验 ?? 0;
        bool 掌握 = 级 > 0;
        int 门槛 = 门槛取(书, 级);                                     // 这一档的累计门槛（见文件头"下档门槛"）
        int 还差 = 还差本数(书, 级, 经验);                              // 满级 = 0（不显示）

        // 名称 = **标识**：`物品数据` 上**没有** `名称` 字段（物品的标识本身就是显示名，见 `数据模型` 第 244 行注释
        //   "唯一键 + 显示名 + 图标引用 三合一"）→ 这里直接用标识、按品质着色。
        行.行.gameObject.SetActive(true);
        行.行.绑定(
            名称文本: 品质工具.名称着色(书.品质档, 标识),
            等级文本: $"{级} / {上限} 级",
            进度文本: 掌握 ? $"累计经验 {经验} / {门槛}" : "",      // 未掌握不显示进度（那一行有"首次读满解锁"提示）
            还差文本: 还差 > 0 ? $"还差 {还差} 本" : "",             // 满级 = 空（用户定稿：满级不显示"还差 N 本"）
            持有文本: $"持有 {玩家.物品数量(标识)} 本",
            掌握: 掌握,
            选中: 标识 == 当前选中标识(),
            点击: 点击);
    }

    // 选中态：只切各行的预置高亮节点（**不重建列表本体**，避免滚动位置归零 / 点击态丢失）。
    private void 刷选中态()
    {
        string 选 = 当前选中标识();
        for (int i = 0; i < 行池.Count && i < 列表.Count; i++)
        {
            var 行 = 行池[i];
            if (行?.行 == null) continue;
            行.标识 = 列表[i];
            行.行.绑定选中态(列表[i] == 选);
        }
    }

    private string 当前选中标识()
        => 选中序号 >= 0 && 选中序号 < 列表.Count ? 列表[选中序号] : null;

    // 点行：记选中 → 只重建详情 + 刷新各行选中态。**不重建列表本体**（定稿）。
    private void 点击行(int 序)
    {
        if (序 < 0 || 序 >= 列表.Count) return;
        bool 换了 = 选中序号 != 序;
        选中序号 = 序;
        var 玩家 = 档案;
        var 库 = 取数据();
        if (玩家 == null || 库 == null) return;
        if (换了) 音效管理器.实例?.播放成功();   // 选中音只在真的换了选中项时响（连点同一行不重复响）
        刷选中态();
        刷详情(玩家, 库);
    }

    // 空列表兜底：场景里若有名为"暂无知识"的常驻节点就写它；否则**克隆一次模板行、清掉里面的子节点**当纯文本容器。
    // 为什么不用基类那个"建一行纯文字标签"的工具：它每次刷新新建一个 TMP 对象（字体 unload/reload + 新旧两份并存的闪），
    //   而"暂无知识"这句话**完全不随档变** —— 常驻一份才对。
    private void 备空列表()
    {
        if (空列表文本 != null) return;

        var 现成 = 找节点(列表区, 名空列表);
        if (现成 != null) { 空列表文本 = 现成.GetComponent<TMP_Text>(); if (空列表文本 != null) return; }

        var 克隆 = 面板基类.创建模板<Transform>(列表区, 知识行模板);
        if (克隆 == null) return;
        克隆.name = 名空列表;
        for (int i = 克隆.childCount - 1; i >= 0; i--) Destroy(克隆.GetChild(i).gameObject);   // 清空行内容：只留一个空文本容器
        var 文本 = 克隆.GetComponent<TMP_Text>();
        if (文本 == null) 文本 = 克隆.gameObject.AddComponent<TextMeshProUGUI>();
        文本.text = 名空列表;       // 字号/颜色沿用模板（本类不碰外观）
        空列表文本 = 文本;
    }

    // ================= 右详情 =================

    private void 刷详情(玩家档案 玩家, DataService 库)
    {
        var 书 = 选中序号 >= 0 && 选中序号 < 列表.Count ? 查书(库, 列表[选中序号]) : null;
        bool 有 = 书 != null;

        // 详情区整段显隐：没选中（或选中的书没了）时把整块收起来，别留一堆空标题在右边。
        // ⚠ 只收"**本组件自己下面**的节点"：详情标题若直接挂在本组件下，它的父节点就是根节点 ——
        //   连根一起 SetActive(false) 会把整个知识页连带关掉（壳那边切页就再也亮不起来）。
        if (详情标题 != null) 切父显隐(详情标题.transform, 有, "详情区");

        // 标题与列表行同源：`物品数据` 没有 `名称` 字段，标识即显示名。
        面板基类.设文本(详情标题, 有 ? 品质工具.名称着色(书.品质档, 书.标识) : "");
        面板基类.设文本(详情副行, 有 ? 副行(书) : "");
        面板基类.设文本(详情状态, 有 ? 状态行(玩家, 书) : "");
        面板基类.设文本(详情描述, 有 ? 书.描述 : "");

        string 合计 = 有 ? 合计加成(书, 玩家.知识等级(书.标识)) : "";
        if (详情合计加成 != null)
        {
            // 只给配方的书（数据里制作书都是这种）：**整段隐藏**，不显示一个空标题（用户定稿）。
            详情合计加成.text = 合计;
            切父显隐(详情合计加成.transform, !string.IsNullOrEmpty(合计), "合计加成区");
        }

        建逐级行(玩家, 库, 书);
    }

    // 切"某个文本节点的父节点"显隐（把标题与它那一段内容一起收/放）。
    // 两道保险：父节点必须是**本组件自己下面**的（绝不碰根节点）；且父节点不能**包含着**本组件依赖的引用
    //   —— 否则一收就把列表/逐级表也收掉了（那样"没选中"和"面板坏了"看起来一模一样，极难排查）。
    private void 切父显隐(Transform 节点, bool 显, string 说明)
    {
        var 父 = 节点 != null ? 节点.parent : null;
        if (父 == null || 父 == transform) return;                        // 直挂本组件下 → 只有那个文本自己，不动别的
        if (父.IsChildOf(transform) == false) return;                     // 不知道是什么，别碰
        if (包含关键引用(父)) return;                                      // 父节点里装着列表/模板/逐级表 → 不许整段收
        if (父.gameObject.activeSelf != 显) 父.gameObject.SetActive(显);
    }

    // 这个节点的子树里有没有本组件赖以工作的关键引用（有 → 不能整段 SetActive）
    private bool 包含关键引用(Transform 节点)
    {
        if (节点 == null) return false;
        if (列表区 != null && (节点 == 列表区 || 列表区.IsChildOf(节点))) return true;
        if (逐级表 != null && (节点 == 逐级表 || 逐级表.IsChildOf(节点))) return true;
        if (知识行模板 != null && 知识行模板.transform.IsChildOf(节点)) return true;
        if (逐级行模板 != null && 逐级行模板.transform.IsChildOf(节点)) return true;
        return false;
    }

    // 副行："家族 · 领域 · 共 y 级"。领域查白名单，查不到就**不写这一段**（宁缺勿错）。
    private static string 副行(物品数据 书)
    {
        int 上限 = 级数(书);
        string 领域 = 领域表.TryGetValue(书.标识, out var 词) ? 词 : null;
        return string.IsNullOrEmpty(领域)
            ? $"{家族(书.标识)} · 共 {上限} 级"
            : $"{家族(书.标识)} · {领域} · 共 {上限} 级";
    }

    // 状态行：未掌握 / "x/y 级　累计经验 E / T" / 已满级（三态，用户定稿）。
    private static string 状态行(玩家档案 玩家, 物品数据 书)
    {
        int 上限 = 级数(书);
        int 级 = 玩家.知识等级(书.标识);
        if (级 <= 0) return "未掌握";
        if (级 >= 上限) return "已满级";
        int 经验 = 玩家.查知识(书.标识)?.经验 ?? 0;
        return $"{级}/{上限} 级　累计经验 {经验} / {门槛取(书, 级)}";
    }

    // 本条知识合计加成：**与 `装备管理器.知识加成` 同一口径**单本聚合 ——
    //   遍历该书 `知识等级[]`，`行.级 <= 当前级 && 行.加成属性 非空 && 行.加成值 != 0` 则累加；
    //   输出 `"{属性} +{值}"`（暴击/命中/闪避/医疗 按百分点，其余按点）。
    // 顺序 = `加成类型` 枚举声明顺序（确定性；不按"出现顺序"排，免得改一行数据就换一个排序）。
    private static string 合计加成(物品数据 书, int 当前级)
    {
        if (书.知识等级 == null || 当前级 <= 0) return "";
        var 表 = new Dictionary<加成类型, int>();
        var 序 = new List<加成类型>();
        foreach (var 行 in 书.知识等级)
        {
            if (行 == null || 行.级 > 当前级) continue;
            if (string.IsNullOrEmpty(行.加成属性) || 行.加成值 == 0f) continue;
            if (!Enum.TryParse(行.加成属性, out 加成类型 类)) continue;   // 数据校验（DataService）已保证合法；这里只是不因坏数据崩
            if (!表.ContainsKey(类)) { 表[类] = 0; 序.Add(类); }
            // `(int)` 截断：与 `装备管理器.知识加成` 的 `总 += (int)行.加成值` **必须一致**，
            //   否则本页显示 "+2"、实际生效 "+2.5" 之类，玩家会以为面板在骗人。
            表[类] += (int)行.加成值;
        }
        var 文本 = new StringBuilder();
        foreach (var 类 in 序)
        {
            if (文本.Length > 0) 文本.Append('\n');
            文本.Append(类).Append(" +").Append(表[类]).Append(按百分点.Contains(类.ToString()) ? "%" : "");
        }
        return 文本.ToString();
    }

    // ================= 逐级表 =================

    // 逐级表：1..y 级每级一行（行池**只增不减**，多余的行收起来）。
    private void 建逐级行(玩家档案 玩家, DataService 库, 物品数据 书)
    {
        if (逐级表 == null || 逐级行模板 == null) return;
        if (书 == null)
        {
            for (int i = 0; i < 逐级行池.Count; i++) 逐级行池[i].gameObject.SetActive(false);
            return;
        }

        int 上限 = 级数(书);
        int 当前级 = 玩家.知识等级(书.标识);
        for (int i = 0; i < 上限; i++)
        {
            var 行 = 取逐级行(i);
            if (行 == null) return;                     // 模板丢了：停手（`报缺引用` 已点名）
            var 数据行 = 找等级行(书.知识等级, i + 1);
            行.gameObject.transform.SetSiblingIndex(i);
            行.gameObject.SetActive(true);
            行.绑定(
                等级文本: $"{i + 1} 级 · 累计 {门槛取(书, i + 1)}",
                状态文本: i + 1 <= 当前级 ? 逐级行.已获得 : (i + 1 == 当前级 + 1 ? 逐级行.下一级 : 逐级行.未获得),
                描述文本: 数据行?.描述 ?? "",
                清单文本: 清单(库, 玩家, 数据行));
        }
        for (int i = 上限; i < 逐级行池.Count; i++) 逐级行池[i].gameObject.SetActive(false);
    }

    // 池子取第 i 个逐级行：不够就地克隆；克隆时**从模板量行高**写进自己的 `LayoutElement`。
    // 为什么要写行高：容器挂了 `VerticalLayoutGroup` 时，`LayoutElement.preferredHeight` 会**覆盖** RectTransform 的高度，
    //   用户在模板上量的高度会被布局组吃掉 → 每级行挤成 0 高。取的值就是模板自己的 rect 高度（**不写死数字**）。
    private 逐级行 取逐级行(int 序)
    {
        while (逐级行池.Count <= 序)
        {
            var 行 = 面板基类.创建模板<逐级行>(逐级表, 逐级行模板);
            if (行 == null) return null;
            量行高(行.gameObject, 逐级行模板);
            逐级行池.Add(行);
        }
        return 逐级行池[序];
    }

    // 把模板节点量到的高度落到克隆体的 `LayoutElement` 上（已有 `LayoutElement` 就一并继承它的左右 padding 等设置）。
    private static void 量行高(GameObject 克隆, GameObject 模板)
    {
        var 模板矩形 = 模板.GetComponent<RectTransform>();
        if (模板矩形 == null) return;
        var 模板布局 = 模板.GetComponent<UnityEngine.UI.LayoutElement>();
        float 高 = 模板布局 != null && 模板布局.preferredHeight > 0f ? 模板布局.preferredHeight : 模板矩形.rect.height;
        if (高 <= 0f) return;   // 模板还没被布局过（场景刚搭好）→ 不写，免得写成 0 高
        var 布局 = 克隆.GetComponent<UnityEngine.UI.LayoutElement>();
        bool 新加 = false;
        if (布局 == null) { 布局 = 克隆.AddComponent<UnityEngine.UI.LayoutElement>(); 新加 = true; }
        if (新加 || 布局.preferredHeight <= 0f) 布局.preferredHeight = 高;
    }

    // 一级行的结构化清单：解锁配方（已习得附注）· 解锁技能（没到手就注明前提未满足）· 加成属性 +值。
    // 空项**一行都不写**（没有解锁内容的等级就只有描述，不留空行）。
    private static string 清单(DataService 库, 玩家档案 玩家, 知识等级数据 行)
    {
        if (行 == null) return "";
        var 文本 = new StringBuilder();
        if (行.解锁配方 != null)
            foreach (var 标识 in 行.解锁配方)
            {
                if (string.IsNullOrEmpty(标识)) continue;
                附行(文本, "解锁配方　" + 名称或(库?.配方, 标识) + (玩家.配方已习得(标识) ? "（已习得）" : ""));
            }
        if (行.解锁技能 != null)
            foreach (var 标识 in 行.解锁技能)
            {
                if (string.IsNullOrEmpty(标识)) continue;
                // 为什么要在意"有没有到手"：知识等级表**只在升级那一刻**尝试学技能（`书籍服务.发知识奖励`），
                //   属性前提不满足会被跳过记日志 —— 之后再也不会补发。所以这里如实标出来，免得玩家以为已经会了。
                附行(文本, "解锁技能　" + 名称或(库?.技能, 标识) + (玩家.掌握技能(标识) ? "" : "（未到手——学习前提未满足）"));
            }
        if (!string.IsNullOrEmpty(行.加成属性) && 行.加成值 != 0f)
            附行(文本, $"{行.加成属性} +{(int)行.加成值}{(按百分点.Contains(行.加成属性) ? "%" : "")}");
        return 文本.ToString();
    }

    private static void 附行(StringBuilder 文本, string 内容)
    {
        if (文本.Length > 0) 文本.Append('\n');
        文本.Append(内容);
    }

    // 配方/技能名：取不到名字就退回**标识**（宁可显示标识，也不要一行空白）。
    private static string 名称或<T>(Dictionary<string, T> 表, string 标识) where T : class
    {
        if (表 != null && 表.TryGetValue(标识, out var 项) && 项 != null)
        {
            var 名 = 取名(项);
            if (!string.IsNullOrEmpty(名)) return 名;
        }
        return 标识;
    }

    // 配方/技能都有 `名称` 字段（`配方数据.名称` / `技能数据.名称`），但两者没有共同基类 →
    //   反射取一次即可（本页刷新频次低，代价可忽略；换来的是**字段改名时这里不会静默拿到空**：
    //   取不到就走标识兜底，而不是抛）。
    private static string 取名(object 项)
    {
        var 字段 = 项.GetType().GetField("名称");
        return 字段 != null ? 字段.GetValue(项) as string : null;
    }

    // 等级行：`知识等级[]` 里找 `级` 相等的那一行（表按 级 升序，数据校验保证从 1 连续）。
    private static 知识等级数据 找等级行(知识等级数据[] 表, int 级)
    {
        if (表 == null) return null;
        foreach (var 行 in 表) if (行 != null && 行.级 == 级) return 行;
        return null;
    }

    // ================= 数值口径（照定稿；别在这里"顺手优化"） =================

    // 一次读满能拿多少知识经验（定稿口径）：
    //   · 基准 = 书 `知识经验`，缺省 100（`书籍服务.每次知识经验` 同款）。
    //   · 天赋「学霸」×1.15（`成长管理器.加知识经验` 的口径）。
    //   · **唯一例外：当前是 1 级、且级内经验 == 0** —— 那一档读满只解锁/不进阶（首次读满只到 1 级、
    //     什么经验都不给），所以它的所得就是原始 100，不吃学霸多出来的那 15%。
    //     （口径出处：用户定稿 + `书籍服务.知识书成功` 的"首次不进阶"分支。）
    private static int 每次所得(物品数据 书, int 当前级, int 级内经验)
    {
        int 基准 = 书.知识经验 > 0 ? 书.知识经验 : 100;
        if (当前级 == 1 && 级内经验 == 0) return 100;
        var 玩家 = 档案;
        return 玩家 != null && 玩家.天赋 != null && 玩家.天赋.Contains("学霸") ? (int)(基准 * 1.15f) : 基准;
    }

    // 还差几本读满这一档：N = ceil((T - E) / G)。T = 本档累计门槛，E = 级内累计经验，G = 每次所得（见上）。
    //   · 满级（级 >= 级数）→ 0（调用方据此不显示"还差 N 本"）。
    //   · 未掌握（级 = 0）→ 门槛按"升到 1 级"那一格算（下标 0），提示玩家从零读几本到 1 级。
    //   · 刚好攒够（T - E <= 0）→ 0（下一本读满就会进阶，不显示"还差 0 本"）。
    private static int 还差本数(物品数据 书, int 当前级, int 级内经验)
    {
        if (当前级 >= 级数(书)) return 0;
        int 差 = 门槛取(书, 当前级) - 级内经验;
        if (差 <= 0) return 0;
        int 每次 = 每次所得(书, 当前级, 级内经验);
        if (每次 <= 0) return 0;   // 坏数据兜底（G = 0 会除零）
        return (差 + 每次 - 1) / 每次;   // 整数上取整 = CeilToInt(差 / 每次)，不用浮点免得大数值丢精度
    }

    // 门槛：`知识门槛`（下标 = 级 - 1，累计口径）。
    // `级` 传的是**当前已到达的等级** → 返回的正是"下档门槛" `知识门槛[当前级]`（定稿里反复强调的那个下标）。
    //   级 = 0（未掌握）→ `知识门槛[0]` = 升到 1 级的门槛。
    //   级 = 满级 → 越界，返回**最后一个门槛**（满级的"累计经验"就该显示到顶那一个值）。
    private static int 门槛取(物品数据 书, int 级)
    {
        var 门槛 = 书.知识门槛;
        if (门槛 == null || 门槛.Length == 0) return 0;
        int 下标 = 级;
        if (下标 < 0) 下标 = 0;
        if (下标 >= 门槛.Length) 下标 = 门槛.Length - 1;
        return 门槛[下标];
    }

    private static int 级数(物品数据 书) => 书.知识等级?.Length ?? 0;

    private static 物品数据 查书(DataService 库, string 标识)
        => 库 != null && 库.物品 != null && 库.物品.TryGetValue(标识, out var 书) ? 书 : null;

    // ================= 引用兜底：按节点名找回来（只找，不建） =================

    private void 自找()
    {
        if (列表区 == null) 列表区 = 子矩形(transform, 名列表区);
        if (知识行模板 == null) 知识行模板 = 找节点(transform, 名知识行模板)?.gameObject;
        if (逐级行模板 == null) 逐级行模板 = 找节点(transform, 名逐级行模板)?.gameObject;
        if (逐级表 == null) 逐级表 = 子矩形(transform, 名逐级表);
        if (详情标题 == null) 详情标题 = 子文本(transform, 名详情标题);
        if (详情副行 == null) 详情副行 = 子文本(transform, 名详情副行);
        if (详情状态 == null) 详情状态 = 子文本(transform, 名详情状态);
        if (详情描述 == null) 详情描述 = 子文本(transform, 名详情描述);
        if (详情合计加成 == null) 详情合计加成 = 子文本(transform, 名详情合计加成);
    }

    // 缺引用**要出声**：Inspector 少接一个位 → 表现只是"某一块永远空着 / 点了没反应"，一条日志都没有。
    // 打**一条** LogError 把缺的名字列全（不崩、不中断别的面板）。
    private void 报缺引用()
    {
        var 缺 = new List<string>();
        if (列表区 == null) 缺.Add($"列表区（节点名「{名列表区}」）");
        if (知识行模板 == null) 缺.Add($"知识行模板（节点名「{名知识行模板}」）");
        if (逐级行模板 == null) 缺.Add($"逐级行模板（节点名「{名逐级行模板}」）");
        if (逐级表 == null) 缺.Add($"逐级表（节点名「{名逐级表}」）");
        if (详情标题 == null) 缺.Add($"详情标题（节点名「{名详情标题}」）");
        if (详情副行 == null) 缺.Add($"详情副行（节点名「{名详情副行}」）");
        if (详情状态 == null) 缺.Add($"详情状态（节点名「{名详情状态}」）");
        if (详情描述 == null) 缺.Add($"详情描述（节点名「{名详情描述}」）");
        if (详情合计加成 == null) 缺.Add($"详情合计加成（节点名「{名详情合计加成}」）");
        if (缺.Count == 0) return;
        Debug.LogError("[知识子面板] 缺引用：" + string.Join("、", 缺) +
                       "。请在场景里把这些节点拖到本组件的 Inspector 引用位上" +
                       "（或让节点名与括号里的一致、挂在本组件下面）。" +
                       "外观在场景 / 预制体里调：本组件只克隆「知识行模板 / 逐级行模板」，不建别的节点。");
    }

    // ================= 小工具 =================

    // 按名找节点：本组件下面**任意深度**的第一个同名节点。
    // 为什么不能只用 `Transform.Find`：它只找**直接子节点**，而详情区/列表区通常还在区节点里面一层。
    private static Transform 找节点(Transform 根, string 名)
    {
        if (根 == null) return null;
        for (int i = 0; i < 根.childCount; i++)
        {
            var 子 = 根.GetChild(i);
            if (子.name == 名) return 子;
            var 深 = 找节点(子, 名);
            if (深 != null) return 深;
        }
        return null;
    }

    private static TMP_Text 子文本(Transform 父, string 名) => 找节点(父, 名)?.GetComponent<TMP_Text>();
    private static RectTransform 子矩形(Transform 父, string 名) => 找节点(父, 名)?.GetComponent<RectTransform>();
}
