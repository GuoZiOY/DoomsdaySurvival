using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// 技能子面板 —— 角色面板「技能」页（三页里的第二页）。
//   上 = 6 个技能槽（`技能格`：一张 Image + 一个 Text，只写名字、不接收点击）
//   下 = 已学技能列表（`技能行`：名字 + 置灰；左键选中看详情，**右键弹出换槽小菜单**）
//   右 = 说明区，**一个 `TMP_Text`**（选中哪条显示哪条，见 `说明内容`）
// 显隐归壳（`角色面板`）：本类不继承 `面板基类`；`OnEnable` 全量重算。
// 换槽由 右键菜单 的条目触发，只走 `成长管理器.切换入槽` / `卸下槽`，本类不写档案。
public sealed class 技能子面板 : MonoBehaviour
{
    private sealed class 槽格视图
    {
        public 技能格 格;
        public bool 预搭;   // 场景里摆好的格子（不重排兄弟序号）
    }

    [SerializeField] private RectTransform 槽容器;
    [SerializeField] private GameObject 技能格模板;
    [SerializeField] private RectTransform 列表容器;
    [SerializeField] private GameObject 技能行模板;
    [SerializeField] private TMP_Text 说明文本;

    private const string 名空列表 = "暂无技能";        // 列表为空时用户自己在场景里放的节点（没放就不显示兜底字）
    private const string 空列表文本 = "还没有学到任何技能。";
    private const string 槽格名前缀 = "技能格";        // 预搭槽格的节点名：技能格1..技能格N

    // 当前选中的技能（说明区显示它）。null = 没选中。
    private string 选中标识;

    private readonly List<槽格视图> 槽格池 = new List<槽格视图>();   // 只增不减
    private readonly List<技能行> 行池 = new List<技能行>();
    private TMP_Text 空列表节点;

    private static 玩家档案 档案
        => ServiceRegistry.已注册<PlayerService>() ? ServiceRegistry.Get<PlayerService>()?.档案 : null;

    void OnEnable() => 刷新();

    public void 刷新()
    {
        var 玩家 = 档案;
        if (玩家 == null) return;
        if (!string.IsNullOrEmpty(选中标识) && !已学会(玩家, 选中标识)) 选中标识 = null;   // 选中的那条可能已被清掉
        刷槽(玩家);
        建列表(玩家);
        刷说明(玩家);
    }

    // ================= 上：6 个技能槽 =================

    // 备齐槽位：先认场景里预搭的 `技能格1..6`（下标 = 槽位），不够的按模板克隆。
    private void 备槽格()
    {
        while (槽格池.Count < 成长管理器.技能槽数)
        {
            int 槽索引 = 槽格池.Count;
            var 预搭 = 找节点(transform, 槽格名前缀 + (槽索引 + 1));
            if (预搭 != null)
            {
                槽格池.Add(new 槽格视图 { 格 = 预搭.GetComponent<技能格>(), 预搭 = true });
                continue;
            }
            var 格 = 面板基类.创建模板<技能格>(槽容器, 技能格模板);
            if (格 == null) return;                       // 容器/模板没接：停手
            量尺寸(格.gameObject, 技能格模板);
            槽格池.Add(new 槽格视图 { 格 = 格, 预搭 = false });
        }
    }

    private void 刷槽(玩家档案 玩家)
    {
        备槽格();
        for (int i = 0; i < 成长管理器.技能槽数 && i < 槽格池.Count; i++)
        {
            var 视图 = 槽格池[i];
            if (视图.格 == null) continue;
            if (!视图.预搭) 视图.格.transform.SetSiblingIndex(i);
            视图.格.gameObject.SetActive(true);

            string 标识 = 槽标识(玩家, i);
            var 技能 = 技能数据(玩家, 标识);
            // 名字**不在这里着色**：文本颜色归场景 / 预制体（用户口径 —— 品质色里的"普通"档是浅灰，看不清）。
            视图.格.绑定(技能标识: 标识, 名称文本: 名称文本(标识, 技能));
        }
    }

    private static string 槽标识(玩家档案 玩家, int 槽索引)
    {
        var 槽 = 玩家.战斗技能槽;
        return 槽 != null && 槽索引 < 槽.Count ? 槽[槽索引] : null;   // 短表/空表 = 后面全是空槽
    }

    // ================= 下：已学技能列表 =================

    // 只列 `已学技能`（未学会的不显示），顺序 = 学习先后；行池复用，够用就不新建。
    private void 建列表(玩家档案 玩家)
    {
        if (列表容器 == null || 技能行模板 == null) return;
        int 条数 = 玩家.已学技能?.Count ?? 0;

        if (条数 == 0)
        {
            收行(0);
            备空列表();
            if (空列表节点 != null) 空列表节点.gameObject.SetActive(true);
            return;
        }
        if (空列表节点 != null) 空列表节点.gameObject.SetActive(false);

        for (int i = 0; i < 条数; i++)
        {
            var 标识 = 玩家.已学技能[i]?.标识;
            var 行 = 取行(i);
            if (行 == null) return;                       // 模板没挂 `技能行`：停手
            行.transform.SetSiblingIndex(i);
            if (string.IsNullOrEmpty(标识)) { 行.gameObject.SetActive(false); continue; }   // 坏档项：跳过

            var 技能 = 技能数据(玩家, 标识);
            行.gameObject.SetActive(true);
            // 名字**不在这里着色**（同槽格：品质色的"普通"档是浅灰，看不清）—— 颜色归场景 / 预制体。
            行.绑定(
                名称文本: "《" + 名称文本(标识, 技能) + "》",
                置灰中: 已装格(玩家, 标识) < 0,           // 没装进技能槽的灰着
                左键: () => 点击行(标识),                  // 传标识（选中活过多次刷新，下标会错位）
                右键: () => 右击行(标识, (RectTransform)行.transform));
        }
        收行(条数);
    }

    // 把池子里第 `起` 条之后的行收起来（不 Destroy：下次技能变多还要用）。
    private void 收行(int 起)
    {
        for (int i = 起; i < 行池.Count; i++) if (行池[i] != null) 行池[i].gameObject.SetActive(false);
    }

    private 技能行 取行(int 序)
    {
        while (行池.Count <= 序)
        {
            var 组件 = 面板基类.创建模板<技能行>(列表容器, 技能行模板);
            if (组件 == null) return null;
            量尺寸(组件.gameObject, 技能行模板);
            行池.Add(组件);
        }
        return 行池[序];
    }

    // 列表为空：写场景里那个 `暂无技能` 节点（文本挂在它自己身上）；没搭这个节点就什么都不显示。
    private void 备空列表()
    {
        if (空列表节点 == null) 空列表节点 = 找节点(列表容器, 名空列表)?.GetComponent<TMP_Text>();
        if (空列表节点 != null) 面板基类.设文本(空列表节点, 空列表文本);
    }

    // ================= 说明区（一个文本，按行拼） =================

    private void 刷说明(玩家档案 玩家)
    {
        var 技能 = 技能数据(玩家, 选中标识);
        面板基类.设文本(说明文本, 技能 != null ? 说明内容(玩家, 技能) : "");
    }

    // `《技能名》`（品质色）/ `类别 · 主动|被动` / `熟练 …` / `已装…`，空行分块。
    // 被动不写目标/距离/时机/消耗（对它永不生效），改写 `被动类型` + 增幅。
    private static string 说明内容(玩家档案 玩家, 技能数据 技能)
    {
        var 头 = new StringBuilder();
        加行(头, 品质工具.名称着色(技能.品质档, "《" + 名称文本(技能.标识, 技能) + "》"));
        加行(头, $"{类别文本(技能)} · {(技能.被动 ? "被动" : "主动")}");
        加行(头, $"熟练　{熟练文本(玩家, 技能.标识)}");
        加行(头, 已装文本(玩家, 技能.标识));

        var 身 = new StringBuilder();
        if (技能.被动)
        {
            加行(身, 标签行("被动类型", 技能.被动类型));
            加行(身, 增幅文本(技能));
        }
        else
        {
            加行(身, 标签行("目标", 技能.目标));
            加行(身, $"攻击距离　{攻击距离(技能)} 格");
            加行(身, $"出手时机　{时机文本(技能)}");
            加行(身, $"消耗精力　{技能.消耗精力}　冷却　{冷却(技能)} 秒");
        }

        var 文本 = new StringBuilder();
        加块(文本, 头.ToString());
        加块(文本, 身.ToString());
        return 文本.ToString();
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

    private static string 标签行(string 标签, string 值)
        => string.IsNullOrEmpty(值) ? "" : $"{标签}　{值}";          // 值为空 → 整行不写

    // 增幅单位照抄数据（数据里 `致命点` 是"点"、`狠劲` 是"%"），不按属性名猜。
    private static string 增幅文本(技能数据 技能)
        => string.IsNullOrEmpty(技能.增幅属性)
            ? ""
            : $"增幅　{技能.增幅属性} +{技能.增幅值.ToString("0.##")}{技能.增幅单位}";

    private static int 攻击距离(技能数据 技能) => 技能.攻击距离 > 0 ? 技能.攻击距离 : 1;   // 数据 0 = 按 1
    private static string 冷却(技能数据 技能) => 技能.冷却 > 0 ? 技能.冷却.ToString() : "无";
    private static string 时机文本(技能数据 技能) => string.IsNullOrEmpty(技能.出手时机) ? "蓄力" : 技能.出手时机;

    // ================= 交互：点列表 / 点槽 =================

    // 点列表一条：选中它（说明区显示它，也是"下一步放进槽里的那条"）；再点同一条 = 取消选中。只改视图状态。
    private void 点击行(string 标识)
    {
        var 玩家 = 档案;
        if (玩家?.已学技能 == null || string.IsNullOrEmpty(标识)) return;

        // 数据缺失的已学标识禁止选中：`切换入槽` 只校验"学过没有"，装进去战斗里也是空的。
        if (技能数据(玩家, 标识) == null)
        {
            警告($"skills.json 里没有这个技能，装入也不会生效：{标识}");
            return;
        }

        选中标识 = 标识 == 选中标识 ? null : 标识;
        音效管理器.实例?.播放成功();
        刷新();
    }

    // 右键技能行 → 换槽小菜单：**6 个技能格各一条**（空格 = 装入；这条所在的格 = 卸下；别人的格 = 换过去）。
    //   条目的文本把"会发生什么"写清楚，动作统一落到 `执行换槽`。
    //   为什么改走右键菜单（用户定稿）：技能格不再是按钮（Image + Text），换槽只在这一处入口，
    //   一次右键就能看清 6 格各是谁、以及这条会进哪一格。
    private void 右击行(string 标识, RectTransform 行框)
    {
        var 玩家 = 档案;
        if (玩家 == null || string.IsNullOrEmpty(标识)) return;
        if (技能数据(玩家, 标识) == null)
        {
            警告($"skills.json 里没有这个技能，装入也不会生效：{标识}");
            return;
        }
        if (存档门禁.正在战斗)
        {
            音效管理器.实例?.播放失败();
            警告("战斗中不能调整技能位（技能栏在进场时已锁定）");
            return;
        }

        选中标识 = 标识;      // 详情区先显示"正要装的这条"
        刷说明(玩家);

        int 已装 = 已装格(玩家, 标识);
        var 条目 = new List<(string 文本, Action 动作)>();
        for (int i = 0; i < 成长管理器.技能槽数; i++)
        {
            int 格 = i;
            string 现有 = 槽标识(玩家, i);
            bool 卸下 = i == 已装;
            string 文本 = 卸下 ? $"卸下（第 {i + 1} 格）"
                        : string.IsNullOrEmpty(现有) ? $"装入第 {i + 1} 格"
                        : $"换到第 {i + 1} 格";
            条目.Add((文本, () => 执行换槽(卸下, 格, 标识)));
        }
        右键菜单.实例?.显示动作(条目, 行框);
    }

    // 菜单条目落地：卸下 / 切换入槽（同技能已在别槽会自动对调）。两者都是 `成长管理器` 的写入口。
    private void 执行换槽(bool 卸下, int 槽索引, string 标识)
    {
        var 玩家 = 档案;
        if (玩家?.成长管理 == null) return;
        bool 成败 = 卸下 ? 玩家.成长管理.卸下槽(槽索引) : 玩家.成长管理.切换入槽(槽索引, 标识);
        if (!成败)
        {
            音效管理器.实例?.播放失败();
            警告($"{(卸下 ? "卸下" : "装入")}第 {槽索引 + 1} 格失败（界面已按档案重算）");
            刷新();
            return;
        }
        音效管理器.实例?.播放成功();
        刷新();   // 全量重算：对调会同时改两格
    }

    // ================= 数据取值 =================

    private static 技能数据 技能数据(玩家档案 玩家, string 标识)
        => string.IsNullOrEmpty(标识) ? null : 玩家?.技能解析?.Invoke(标识);

    private static string 名称文本(string 标识, 技能数据 技能)
        => 技能 != null && !string.IsNullOrEmpty(技能.名称) ? 技能.名称 : 标识 + "（数据缺失）";

    private static string 类别文本(技能数据 技能) => 技能?.类别 ?? "";   // JSON 原字符串照抄，不解析枚举

    // 满级只留 `Lv5/5`（满级后熟练度不再累积）。
    private static string 熟练文本(玩家档案 玩家, string 标识)
    {
        int 等级 = 玩家.技能熟练等级(标识);
        int 上限 = 玩家档案.熟练等级上限;
        return 等级 >= 上限 ? $"Lv{上限}/{上限}" : $"Lv{等级}/{上限}　{玩家.技能熟练度(标识)}/{每级熟练度(标识)}";
    }

    private static int 每级熟练度(string 标识)
        => ServiceRegistry.已注册<技能服务>() ? ServiceRegistry.Get<技能服务>()?.每级熟练度(标识) ?? 0 : 0;

    private static int 已装格(玩家档案 玩家, string 标识)
        => 玩家?.战斗技能槽?.FindIndex(s => s == 标识) ?? -1;

    private static string 已装文本(玩家档案 玩家, string 标识)
    {
        int 索引 = 已装格(玩家, 标识);
        return 索引 >= 0 ? $"已装·第{索引 + 1}格" : "未装";
    }

    private static bool 已学会(玩家档案 玩家, string 标识)
        => 玩家?.已学技能 != null && 玩家.已学技能.Exists(s => s != null && s.标识 == 标识);

    private static void 警告(string 文本)
        => ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.警告, 文本));

    // ================= 小工具 =================

    // 让克隆跟模板一样高：行根是 Button/Image（`Image` 的 preferredHeight 是 0），容器挂了
    //   `VerticalLayoutGroup` 时会把没有 `LayoutElement` 的行压成 0 高。
    private static void 量尺寸(GameObject 克隆, GameObject 模板)
    {
        if (克隆 == null || 模板 == null) return;
        var 模板矩形 = 模板.GetComponent<RectTransform>();
        if (模板矩形 == null) return;
        var 模板布局 = 模板.GetComponent<UnityEngine.UI.LayoutElement>();
        float 高 = 模板布局 != null && 模板布局.preferredHeight > 0f ? 模板布局.preferredHeight : 模板矩形.rect.height;
        if (高 <= 0f) return;
        var 布局 = 克隆.GetComponent<UnityEngine.UI.LayoutElement>();
        if (布局 == null) 布局 = 克隆.AddComponent<UnityEngine.UI.LayoutElement>();
        if (布局.preferredHeight <= 0f) 布局.preferredHeight = 高;
    }

    // 按名找节点（预搭的 `技能格1..6`、`暂无技能` 这两处场景内容用它；Inspector 引用位没有兜底）。
    private static Transform 找节点(Transform 根, string 名)
    {
        if (根 == null || string.IsNullOrEmpty(名)) return null;
        for (int i = 0; i < 根.childCount; i++)
        {
            var 子 = 根.GetChild(i);
            if (子.name == 名) return 子;
            var 深 = 找节点(子, 名);
            if (深 != null) return 深;
        }
        return null;
    }
}
