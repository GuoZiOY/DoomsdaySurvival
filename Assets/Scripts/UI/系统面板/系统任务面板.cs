using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 系统任务面板（侧边栏「任务」按钮呼出）：主线 / 支线 / 日常 三类任务分栏展示。
// 布局同 制作面板：任务列表（行）+ 详情区（选中任务完整信息 + 行动按钮：支线接取 / 日常提交领取）。
// 主线、支线来自 quests.json（主线=「主线_」前缀，支线=「支线_」前缀）；日常=悬赏板生成的日常任务。
// 返回上一面板走侧边栏取消按钮。
public sealed class 系统任务面板 : 面板基类
{
    [SerializeField] private TMP_Text 标题;
    [SerializeField] private Button 主线Tab, 支线Tab, 日常Tab;   // 分栏按钮
    [SerializeField] private RectTransform 详情区;   // 选中任务详情（常显，无任务时保留上一次内容）
    [SerializeField] private TMP_Text 详情文本;
    [SerializeField] private Button 行动按钮;        // 支线->接取；日常->提交领取；主线隐藏
    [SerializeField] private GameObject 任务行模板;   // 任务行模板（挂 任务行）
    [SerializeField] private GameObject 章节标题行模板;   // 章节标题行模板（挂 章节标题行）

    private int 当前栏 = 0;   // 0 主线  1 支线  2 日常
    private string 选中标识;
    private readonly HashSet<string> 折叠章 = new HashSet<string>();   // 已折叠的主线大章（点击章按钮切换）
    private DataService 数据服务 => ServiceRegistry.Get<DataService>();
    private QuestService 任务服务 => ServiceRegistry.Get<QuestService>();
    private 日常任务服务 日常服务 => ServiceRegistry.Get<日常任务服务>();
    private 玩家档案 玩家 => ServiceRegistry.Get<PlayerService>().档案;

    // 任务面板 = 侧边式：从屏右滑入（向左到位），退出向右滑出
    protected override 面板过渡样式 过渡样式 => 面板过渡样式.右侧滑入右滑出;

    void Awake()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件?.订阅<任务进度事件>(_ => 刷新(null));
        事件?.订阅<任务完成事件>(_ => 刷新(null));
        事件?.订阅<日常任务变更事件>(_ => 刷新(null));
        事件?.订阅<金币变化事件>(_ => 刷新(null));
        主线Tab?.onClick.AddListener(() => 切栏(0));
        支线Tab?.onClick.AddListener(() => 切栏(1));
        日常Tab?.onClick.AddListener(() => 切栏(2));
    }

    protected override void 刷新(object 上下文)
    {
        if (标题 != null) 标题.text = $"任务 · {栏名(当前栏)}";
        渲染(当前栏);
    }

    // 全局取消 = 返回上一面板（互切时不覆盖上一个面板，所以总能回到进入侧边前的内容面板）
    public override bool 回退()
    {
        if (面板管理器.实例?.上一个面板 == null) return false;
        面板管理器.实例.返回上一面板();
        return true;
    }

    public override string 取消文本 => "返回";

    private void 切栏(int 栏)
    {
        当前栏 = 栏;
        选中标识 = null;
        渲染(栏);
        if (标题 != null) 标题.text = $"任务 · {栏名(栏)}";
        设选中缩放(主线Tab, 栏 == 0);
        设选中缩放(支线Tab, 栏 == 1);
        设选中缩放(日常Tab, 栏 == 2);
    }

    private string 栏名(int 栏) => 栏 switch { 0 => "主线", 1 => "支线", _ => "日常" };

    private void 渲染(int 栏)
    {
        清空(内容区);
        switch (栏)
        {
            case 0: 渲染主线(); break;
            case 1: 渲染列表(支线任务(), "暂无支线任务。", 1); break;
            default: 渲染日常(); break;
        }
        刷新详情();
    }

    // —— 主线 / 支线 ——

    private List<任务数据> 主线任务()
    {
        var 列表 = new List<任务数据>();
        foreach (var 任务 in 数据服务.任务.Values)
            if (任务.标识.StartsWith("主线_")) 列表.Add(任务);
        // 主线按剧情阶段顺序排列（章节轨道）
        列表.Sort((a, b) => 主线阶段工具.序(a.目标标识).CompareTo(主线阶段工具.序(b.目标标识)));
        return 列表;
    }

    // 主线只显示推进到的部分：章按钮（可折叠展开）+ 章内「已完成/当前」任务；未解锁后续任务随推进显现
    private void 渲染主线()
    {
        var 列表 = 主线任务();
        if (列表.Count == 0) { 创建标签(内容区, "暂无主线任务。"); return; }
        if (选中标识 != null && 列表.Find(任 => 任.标识 == 选中标识) == null) 选中标识 = null;
        // 可见任务 = 已完成 / 当前（第一个未完成的）
        var 可见 = new List<任务数据>();
        for (int i = 0; i < 列表.Count; i++)
        {
            bool 完成 = 主线阶段工具.达到(玩家.主线阶段, 列表[i].目标标识);
            bool 当前 = !完成 && (i == 0 || 主线阶段工具.达到(玩家.主线阶段, 列表[i - 1].目标标识));
            if (完成 || 当前) 可见.Add(列表[i]);
        }
        if (可见.Count == 0) { 创建标签(内容区, "暂无主线任务。"); return; }
        // 逐章渲染：章按钮（可折叠）+ 章内可见任务（章内中文序号 + 名称）
        string 当前章 = null; int 章序号 = 0;
        foreach (var 任务 in 可见)
        {
            string 章 = string.IsNullOrEmpty(任务.章节) ? "主线" : 任务.章节;
            if (章 != 当前章)
            {
                当前章 = 章; 章序号 = 0;
                创建章节按钮(章);
                if (折叠章.Contains(章)) continue;   // 折叠：本组任务不渲染
            }
            if (折叠章.Contains(章)) continue;
            bool 完成 = 主线阶段工具.达到(玩家.主线阶段, 任务.目标标识);
            章序号++;
            var 标识 = 任务.标识;
            var 行 = 面板基类.创建模板<任务行>(内容区, 任务行模板);
            if (行 == null) continue;   // 模板未接线则跳过该行
            行.绑定($"（{中文数(章序号)}）{任务.名称}", 完成 ? 任务行.已完成 : 任务行.进行中, 标识 == 选中标识, () => 选中任务(0, 标识));
        }
    }

    // 章节标题行：专用行组件（黑色加粗标题 + 指示），点击切换该大章内容的展开/收起
    private void 创建章节按钮(string 标题)
    {
        bool 折叠 = 折叠章.Contains(标题);
        var 行 = 面板基类.创建模板<章节标题行>(内容区, 章节标题行模板);
        if (行 != null) 行.绑定(标题, 折叠, () => 切换章折叠(标题));
    }

    private void 切换章折叠(string 标题)
    {
        if (!折叠章.Add(标题)) 折叠章.Remove(标题);   // 已在折叠中则展开
        刷新(null);
    }

    // 中文数字（1~10；其余回退为数字）
    private static string 中文数(int 序号)
    {
        string[] 数 = { "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
        return (序号 >= 1 && 序号 <= 数.Length) ? 数[序号 - 1] : 序号.ToString();
    }

    private List<任务数据> 支线任务()
    {
        var 列表 = new List<任务数据>();
        foreach (var 任务 in 数据服务.任务.Values)
            if (任务.标识.StartsWith("支线_")) 列表.Add(任务);
        return 列表;
    }

    private void 渲染列表(List<任务数据> 列表, string 空文本, int 栏)
    {
        // 支线只显示已接取的任务；主线始终显示（主线自开局起就在进行，无「未接取」态）
        if (栏 == 1) 列表 = 列表.Where(任 => 进度文本(任) != "未接取").ToList();
        if (列表.Count == 0) { 创建标签(内容区, 空文本); return; }
        if (选中标识 != null && 列表.Find(任 => 任.标识 == 选中标识) == null) 选中标识 = null;
        foreach (var 任务 in 列表)
        {
            var 标识 = 任务.标识;
            string 进度 = 进度文本(任务);
            bool 未接取 = 进度 == "未接取";
            bool 已完成 = 进度 == "已完成";
            string 行名 = 任务.名称;
            int 状态 = 已完成 ? 任务行.已完成 : (未接取 ? 任务行.可接取 : 任务行.进行中);
            var 行 = 面板基类.创建模板<任务行>(内容区, 任务行模板);
            if (行 == null) continue;   // 模板未接线则跳过该行
            行.绑定(行名, 状态, 标识 == 选中标识, () => 选中任务(栏, 标识));
        }
    }

    // —— 日常（悬赏）——

    private void 渲染日常()
    {
        var 名单 = 日常服务?.当天日常();
        // 日常需先在悬赏板接取才在此显示
        if (名单 != null) 名单 = 名单.Where(x => x.已接取).ToArray();
        if (名单 == null || 名单.Length == 0)
        {
            创建标签(内容区, "今日尚无已接取的日常任务，先去悬赏板认领。");
            选中标识 = null;
            return;
        }
        if (选中标识 != null && System.Array.Find(名单, 条 => 条.标识 == 选中标识) == null) 选中标识 = null;
        foreach (var 日常 in 名单)
        {
            var 标识 = 日常.标识;
            string 行名 = 目标名(日常);
            int 状态 = 日常.已领取 ? 任务行.已完成 : (日常服务.可提交(日常) ? 任务行.可接取 : 任务行.进行中);
            var 行 = 面板基类.创建模板<任务行>(内容区, 任务行模板);
            if (行 == null) continue;   // 模板未接线则跳过该行
            行.绑定(行名, 状态, 标识 == 选中标识, () => 选中日常(标识));
        }
    }

    // —— 详情区 ——

    private void 选中任务(int 栏, string 标识)
    {
        当前栏 = 栏;
        选中标识 = 标识;
        if (详情区 == null) { 详情日志(); return; }
        刷新详情();
    }

    private void 选中日常(string 标识)
    {
        当前栏 = 2;
        选中标识 = 标识;
        if (详情区 == null) { 详情日志(); return; }
        刷新详情();
    }

    private void 刷新详情()
    {
        bool 有 = false;
        switch (当前栏)
        {
            case 0: 有 = 填充任务(主线任务()); break;
            case 1: 有 = 填充任务(支线任务()); break;
            default: 有 = 填充日常(); break;
        }
        // 详情区常显（初始不隐藏）：无任务时清空文本、收起行动按钮；选中时填充内容
        if (!有)
        {
            设文本(详情文本, "");
            if (行动按钮 != null) { 行动按钮.onClick.RemoveAllListeners(); 行动按钮.gameObject.SetActive(false); }
        }
    }

    // 主线/支线详情；返回该任务是否存在
    private bool 填充任务(List<任务数据> 列表)
    {
        var 任务 = 列表.Find(任 => 任.标识 == 选中标识);
        if (任务 == null)
        {
            if (行动按钮 != null) { 行动按钮.onClick.RemoveAllListeners(); 行动按钮.gameObject.SetActive(false); }
            return false;
        }
        // 主线状态由主线阶段派生；支线读任务进度
        string 进度 = 当前栏 == 0
            ? (主线阶段工具.达到(玩家.主线阶段, 任务.目标标识) ? "已完成" : "进行中")
            : 进度文本(任务);
        string 奖励 = 奖励文本(任务);
        string s = $"{任务.名称}\n状态: {进度}";
        if (!string.IsNullOrEmpty(奖励)) s += $"\n奖励: {奖励}";
        s += $"\n\n{任务.描述}";
        设文本(详情文本, s);
        // 支线可接取、主线只读
        if (行动按钮 != null)
        {
            bool 需接取 = 当前栏 == 1 && 进度 == "未接取";
            行动按钮.onClick.RemoveAllListeners();
            行动按钮.gameObject.SetActive(当前栏 == 1);
            if (当前栏 == 1)
            {
                var 标识 = 任务.标识;
                设按钮文本(需接取 ? "接取任务" : "进行中");
                行动按钮.interactable = 需接取;
                if (需接取) 行动按钮.onClick.AddListener(() => { if (任务服务.接取(标识)) 选中标识 = null; });
            }
        }
        return true;
    }

    // 日常详情；返回是否存在
    private bool 填充日常()
    {
        var 日常 = 日常服务?.当天日常()?.FirstOrDefault(日常 => 日常.标识 == 选中标识);
        if (日常 == null)
        {
            if (行动按钮 != null) { 行动按钮.onClick.RemoveAllListeners(); 行动按钮.gameObject.SetActive(false); }
            return false;
        }
        string s = $"{目标名(日常)}\n{进度文本(日常)}   {奖励文本(日常)}";
        if (日常.已领取) s = $"（已结算）\n{s}";
        s += $"\n\n{日常.描述}";
        设文本(详情文本, s);
        if (行动按钮 != null)
        {
            bool 可提交 = 日常服务.可提交(日常);
            行动按钮.onClick.RemoveAllListeners();
            行动按钮.gameObject.SetActive(!日常.已领取);
            if (!日常.已领取)
            {
                var 标识 = 日常.标识;
                设按钮文本(可提交 ? "提交领取" : "未完成");
                行动按钮.interactable = 可提交;
                行动按钮.onClick.AddListener(() => { if (日常服务.提交(标识)) 选中标识 = null; });
            }
        }
        return true;
    }

    // 详情区未接线时，退化为日志展示
    private void 详情日志()
    {
        switch (当前栏)
        {
            case 0:
            {
                var 任 = 主线任务().Find(x => x.标识 == 选中标识);
                if (任 != null) ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.任务, $"{任.名称}：{任.描述}"));
                break;
            }
            case 1:
            {
                var 任 = 支线任务().Find(x => x.标识 == 选中标识);
                if (任 != null) ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.任务, $"{任.名称}：{任.描述}"));
                break;
            }
            default:
            {
                var 日常 = 日常服务?.当天日常()?.FirstOrDefault(x => x.标识 == 选中标识);
                if (日常 != null) ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.任务, $"{目标名(日常)}：{日常.描述}"));
                break;
            }
        }
    }

    // —— 工具 ——

    // 设本面板行动按钮的标签文字（找其「文字」子物体；防空）
    private void 设按钮文本(string 文字)
    {
        if (行动按钮 == null) return;
        var 文本 = 行动按钮.transform.Find("文字")?.GetComponent<TMP_Text>();
        if (文本 != null) 文本.text = 文字;
    }

    private string 进度文本(任务数据 任务)
    {
        foreach (var 进度 in 玩家.任务)
            if (进度.标识 == 任务.标识)
                return 进度.已完成 ? "已完成" : $"{进度.数量}/{任务.目标数量}";
        return "未接取";
    }

    private string 进度文本(日常任务 日常)
        => $"{(日常.目标类型 == "击杀" ? "已击杀" : "持有")} {日常服务.进度(日常)}/{日常.目标数量}";

    private string 奖励文本(任务数据 任务)
        => (任务.奖励金币 > 0 || 任务.奖励经验 > 0)
            ? $"({货币工具.文本(任务.奖励金币)} · {任务.奖励经验}exp)"
            : "";

    private string 奖励文本(日常任务 日常)
        => $"+{货币工具.文本(日常.奖励金币)} · {日常.奖励经验}经验";

    private string 目标名(日常任务 日常)
    {
        if (日常.目标类型 == "击杀")
            return 数据服务.敌人.TryGetValue(日常.目标标识, out var 敌) ? 敌.名称 : 日常.目标标识;
        return 数据服务.物品.TryGetValue(日常.目标标识, out var 物) ? 物.名称 : 日常.目标标识;
    }
}