using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// 知识子面板 —— 角色面板「知识」页（三页里的第三页）。
//   左 = 知识列表（`知识行`：`《名称》` + `· 罗马等级` + 选中高亮 + 未掌握置灰），13 本全列出、已掌握排前面
//   右 = 详情区，**一个 `TMP_Text`**（`《名称》`/家族·领域/等级/当前经验 空行 逐级表，见 `刷详情`）
// 显隐归壳（`角色面板`）：本类不继承 `面板基类`，`OnEnable` 全量重算一次（切页 = 壳 SetActive）。
// 滚动 / 遮罩 / 布局组件全归场景；本类只往 `列表内容` 里克隆重排行。
public sealed class 知识子面板 : MonoBehaviour
{
    private const string 家族精通 = "精通";
    private const string 家族制作 = "制作";

    [SerializeField] private RectTransform 列表内容;      // 左列表容器（行克隆到这里）
    [SerializeField] private GameObject 知识行模板;        // 左列表行模板（挂 `知识行`；场景里默认 SetActive(false)）
    [SerializeField] private TMP_Text 详情文本;            // 右详情：整块细节都拼进这一个文本

    // 逐级表两态色（Inspector 可调）：已解锁 = 白、未解锁 = 灰。代码只把 Color 转成 `#RRGGBB`，不写死色值。
    // ⚠ 灰那个值比 hex 的精确商略高一丁点：`ToHtmlStringRGB` 内部 `Color → Color32` 是**截断**，
    //   写精确商（`138/255`）会因浮点误差落到 137、输出 `898378` 而不是 `8A8378`。
    [SerializeField] private Color 已获得色 = new Color(1f, 1f, 1f);              // → #FFFFFF 白
    [SerializeField] private Color 未获得色 = new Color(0.542f, 0.514f, 0.471f);   // → #8A8378 灰

    // 领域 = 标识白名单（猜前缀一定会把新书归错类；表里没有的就留空）。
    private static readonly Dictionary<string, string> 领域表 = new Dictionary<string, string>
    {
        { "近战精通", "近战" },
        { "枪械精通", "枪械" },     { "弓弩精通", "弓弩" },   { "医学精通", "医学" },
        { "盾精通", "盾" },         { "观察", "观察" },
        { "初级制作", "通用材料" },  { "中级制作", "通用材料" },  { "高级制作", "通用材料" },
        { "刀具制作", "武器制作" },  { "弓制作", "武器制作" },    { "枪械制作", "武器制作" },
        { "防具制作", "防具" },
    };

    // 按百分点显示的加成属性（暴击/命中/闪避/医疗 在项目里 1 点 = 1%），其余按点。
    private static readonly HashSet<string> 按百分点 = new HashSet<string> { "暴击", "命中", "闪避", "医疗" };

    private static readonly string[] 罗马十以内 = { "—", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };

    private int 选中序号 = -1;                                        // `列表` 里的下标；-1 = 没选中
    private readonly List<string> 列表 = new List<string>();           // 排序后的书标识（已掌握在前、家族序、标识序）
    private readonly List<知识行> 行池 = new List<知识行>();           // 只增不减（重建只是重新绑定 + 切显隐）

    private DataService 数据;
    private DataService 取数据() => 数据 != null ? 数据 : (数据 = ServiceRegistry.已注册<DataService>() ? ServiceRegistry.Get<DataService>() : null);

    private static 玩家档案 档案
        => ServiceRegistry.已注册<PlayerService>() ? ServiceRegistry.Get<PlayerService>()?.档案 : null;

    void OnEnable() => 全量刷新();

    // ================= 刷新 =================

    private void 全量刷新()
    {
        var 玩家 = 档案;
        var 库 = 取数据();
        if (玩家 == null || 库 == null) return;   // 未开局 / 服务没起来

        收书单(库);
        建列表(玩家);
        if (选中序号 >= 0 && (选中序号 >= 列表.Count || 查书(库, 列表[选中序号]) == null)) 选中序号 = -1;
        刷选中态();
        刷详情(玩家, 库);
    }

    // 书单：`类型=书籍 且 书籍种类=知识`；排序 = 已掌握在前 → 家族（精通 → 制作）→ 标识（序号比较，结果稳定）。
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
            if (甲会 != 乙会) return 甲会 ? -1 : 1;
            int 家 = 家族序(查书(库, 甲)).CompareTo(家族序(查书(库, 乙)));
            if (家 != 0) return 家;
            return string.CompareOrdinal(甲, 乙);
        });
    }

    // 家族按**级数**判（不自作主张信标识尾巴）：盾精通(10 级)/观察(5 级) 都跟"精通"二字对不上。
    private static string 家族(物品数据 书) => 书 != null && 级数(书) > 5 ? 家族精通 : 家族制作;
    private static int 家族序(物品数据 书) => 家族(书) == 家族精通 ? 0 : 1;

    // ================= 左列表 =================

    private void 建列表(玩家档案 玩家)
    {
        if (列表内容 == null || 知识行模板 == null) return;

        if (列表.Count == 0)
        {
            收行(0);
            return;
        }
        for (int i = 0; i < 列表.Count; i++)
        {
            var 标识 = 列表[i];
            var 行 = 取行(i);
            if (行 == null) return;                            // 模板没挂 `知识行`：停手
            行.transform.SetSiblingIndex(i);                   // 顺序 = 书单顺序
            int 序 = i;                                        // 闭包捕获：循环变量必须另存一份
            绑行(行, 玩家, 标识, () => 点击行(序));
        }
        收行(列表.Count);
    }

    // 把池子里第 `起` 条之后的行收起来（不 Destroy：下次书变多还要用）。
    private void 收行(int 起)
    {
        for (int i = 起; i < 行池.Count; i++) if (行池[i] != null) 行池[i].gameObject.SetActive(false);
    }

    private 知识行 取行(int 序)
    {
        while (行池.Count <= 序)
        {
            var 组件 = 面板基类.创建模板<知识行>(列表内容, 知识行模板);
            if (组件 == null) return null;
            行池.Add(组件);
        }
        return 行池[序];
    }

    // 一行读作 `《近战精通》· II`；未掌握的行照常显示，只是灰着（`未掌握` 那个预置节点归 `知识行` 切）。
    private void 绑行(知识行 行, 玩家档案 玩家, string 标识, UnityEngine.Events.UnityAction 点击)
    {
        var 书 = 查书(取数据(), 标识);
        if (书 == null) { 行.gameObject.SetActive(false); return; }

        int 级 = 玩家.知识等级(标识);                          // 未掌握 = 0
        行.gameObject.SetActive(true);
        行.绑定(
            名称文本: 品质工具.名称着色(书.品质档, "《" + 标识 + "》"),
            等级文本: "· " + 罗马(级),
            选中: 标识 == 当前选中标识(),
            未掌握: 级 <= 0,
            点击: 点击);
    }

    // 1..10 → I..X；0（未掌握）→ "—"；>10 兜底阿拉伯数字（数据里最多 10 级，这一支只是不崩）。
    public static string 罗马(int 级)
    {
        if (级 <= 0) return 罗马十以内[0];
        if (级 <= 10) return 罗马十以内[级];
        return 级.ToString();
    }

    // 选中态只切各行的高亮节点（不重建列表本体，滚动位置与点击态都留着）。
    private void 刷选中态()
    {
        string 选 = 当前选中标识();
        for (int i = 0; i < 行池.Count && i < 列表.Count; i++)
        {
            if (行池[i] == null) continue;
            行池[i].绑定选中态(列表[i] == 选);
        }
    }

    private string 当前选中标识()
        => 选中序号 >= 0 && 选中序号 < 列表.Count ? 列表[选中序号] : null;

    private void 点击行(int 序)
    {
        if (序 < 0 || 序 >= 列表.Count) return;
        bool 换了 = 选中序号 != 序;
        选中序号 = 序;
        var 玩家 = 档案;
        var 库 = 取数据();
        if (玩家 == null || 库 == null) return;
        if (换了) 音效管理器.实例?.播放成功();   // 连点同一行不重复响
        刷选中态();
        刷详情(玩家, 库);
    }

    // ================= 右详情 =================

    // 头部（`《名称》` / 家族·领域 / `等级 3/ 10` / `当前经验 53 / 100`） 空行 逐级表。
    private void 刷详情(玩家档案 玩家, DataService 库)
    {
        if (详情文本 == null) return;
        var 书 = 选中序号 >= 0 && 选中序号 < 列表.Count ? 查书(库, 列表[选中序号]) : null;
        if (书 == null) { 面板基类.设文本(详情文本, ""); return; }

        int 级 = 玩家.知识等级(书.标识);
        int 累计经验 = 玩家.查知识(书.标识)?.经验 ?? 0;

        var 头 = new StringBuilder();
        加行(头, 品质工具.名称着色(书.品质档, "《" + 书.标识 + "》"));
        加行(头, 家族领域(书));
        加行(头, "等级 " + 等级文本(书, 级));
        加行(头, 经验文本(书, 级, 累计经验));

        var 文本 = new StringBuilder();
        加块(文本, 头.ToString());
        加块(文本, 逐级行文本(库, 玩家, 书, 级));
        面板基类.设文本(详情文本, 文本.ToString());
    }

    private static void 加行(StringBuilder 块, string 内容)
    {
        if (string.IsNullOrEmpty(内容)) return;
        if (块.Length > 0) 块.Append('\n');
        块.Append(内容);
    }

    private static void 加块(StringBuilder 全文, string 内容)
    {
        if (string.IsNullOrEmpty(内容)) return;
        if (全文.Length > 0) 全文.Append('\n').Append('\n');
        全文.Append(内容);
    }

    private static string 家族领域(物品数据 书)
    {
        string 领域 = 领域表.TryGetValue(书.标识, out var 词) ? 词 : null;
        return string.IsNullOrEmpty(领域) ? 家族(书) : 家族(书) + " · " + 领域;
    }

    private static string 等级文本(物品数据 书, int 级) => $"{级}/ {级数(书)}";

    // `当前经验: 级内 / 本级所需`。
    //   基准 = 这一级之前已用掉的累计门槛（当前级 ≥ 2 → 门槛[当前级-1]；1 级与未掌握 → 0）：
    //   `门槛[0]` 那档在代码路径上取不到（`成长管理器` 首次读满直接下发 1 级、经验从 0 起算），
    //   所以 1 级的分母是 `门槛[1]`，不是 `门槛[1] - 门槛[0]`。存储口径一个字没动，这里只是显示换算。
    private static string 经验文本(物品数据 书, int 级, int 累计经验)
    {
        int 上限 = 级数(书);
        if (上限 > 0 && 级 >= 上限) return "已满级";
        int 基准 = 级 >= 2 ? 门槛取(书, 级 - 1) : 0;
        int 级内 = Math.Max(0, 累计经验 - 基准);
        int 所需 = Math.Max(0, 门槛取(书, 级) - 基准);
        return $"当前经验: {级内} / {所需}";
    }

    // ================= 逐级表（拼进详情文本） =================

    // 每级一行 `{级}级：{奖励}`，已解锁 = 白 / 未解锁 = 灰；门槛不在逐级行里出现（只在头部那一行）。
    private string 逐级行文本(DataService 库, 玩家档案 玩家, 物品数据 书, int 当前级)
    {
        var 文本 = new StringBuilder();
        int 满级 = 级数(书);
        for (int i = 1; i <= 满级; i++)
        {
            string 内容 = 解锁文本(库, 玩家, 找等级行(书.知识等级, i));
            加行(文本, 着色(内容.Length == 0 ? $"{i}级" : $"{i}级：{内容}", 逐级色(i, 当前级)));
        }
        return 文本.ToString();
    }

    private Color 逐级色(int 级, int 当前级) => 级 <= 当前级 ? 已获得色 : 未获得色;

    // 色值取自上面两个 `[SerializeField] Color`；前提是详情那个 `TMP_Text` 开着 richText（TMP 默认开）。
    private static string 着色(string 文本, Color 色)
        => $"<color=#{ColorUtility.ToHtmlStringRGB(色)}>{文本}</color>";

    // 一级"解锁了什么"：技能 `解锁[名]`（多个用 `、`）、配方 `配方：名`（已习得的加"（已习得）"）、
    // 加成 `暴击 +2%`；异类之间用全角空格，空项一个字都不写。
    private static string 解锁文本(DataService 库, 玩家档案 玩家, 知识等级数据 行)
    {
        if (行 == null) return "";
        var 段 = new List<string>();

        string 技能 = 同类(行.解锁技能, 标识 => "[" + 名称或(库?.技能, 标识) + "]");
        if (技能.Length > 0) 段.Add("解锁" + 技能);

        string 配方 = 同类(行.解锁配方, 标识 => 名称或(库?.配方, 标识) + (玩家 != null && 玩家.配方已习得(标识) ? "（已习得）" : ""));
        if (配方.Length > 0) 段.Add("配方：" + 配方);

        if (!string.IsNullOrEmpty(行.加成属性) && 行.加成值 != 0f)
            段.Add($"{行.加成属性} +{(int)行.加成值}{(按百分点.Contains(行.加成属性) ? "%" : "")}");

        return string.Join("　", 段);
    }

    private static string 同类(string[] 标识表, Func<string, string> 写法)
    {
        if (标识表 == null) return "";
        var 项 = new List<string>();
        foreach (var 标识 in 标识表)
            if (!string.IsNullOrEmpty(标识)) 项.Add(写法(标识));
        return string.Join("、", 项);
    }

    // 配方/技能名取不到就退回标识（宁可显示标识，也不要一行空白）。
    private static string 名称或<T>(Dictionary<string, T> 表, string 标识) where T : class
    {
        if (表 != null && 表.TryGetValue(标识, out var 项) && 项 != null)
        {
            var 名 = 取名(项);
            if (!string.IsNullOrEmpty(名)) return 名;
        }
        return 标识;
    }

    // `配方数据` / `技能数据` 都有 `名称` 字段但没有共同基类 → 反射取一次（取不到走标识兜底，不抛）。
    private static string 取名(object 项)
    {
        var 字段 = 项.GetType().GetField("名称");
        return 字段 != null ? 字段.GetValue(项) as string : null;
    }

    private static 知识等级数据 找等级行(知识等级数据[] 表, int 级)
    {
        if (表 == null) return null;
        foreach (var 行 in 表) if (行 != null && 行.级 == 级) return 行;
        return null;
    }

    // ================= 数值口径 =================

    // `知识门槛` 下标 = 级 - 1（累计口径）。传进来的 `级` 是**当前已到达的等级** → 返回"下档门槛"。
    private static int 门槛取(物品数据 书, int 级)
    {
        var 门槛 = 书.知识门槛;
        if (门槛 == null || 门槛.Length == 0) return 0;
        int 下标 = Math.Max(0, Math.Min(级, 门槛.Length - 1));
        return 门槛[下标];
    }

    private static int 级数(物品数据 书) => 书.知识等级?.Length ?? 0;

    private static 物品数据 查书(DataService 库, string 标识)
        => 库 != null && 库.物品 != null && 库.物品.TryGetValue(标识, out var 书) ? 书 : null;
}
