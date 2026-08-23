using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 日常任务面板（各设施委托栏功能面板）：展示本设施范围（装备/食物/药剂/通用）当天随机日常。
// 布局同 制作面板：任务列表（行）+ 详情区（选中任务的完整描述/进度/奖励 + [提交] 按钮）。
// 击杀/收集均需回 本设施委托栏 手动提交结算；返回统一走 侧边栏取消 = 返回设施内部。
public sealed class 日常任务面板 : 设施功能面板基类
{
    [SerializeField] private RectTransform 详情区;   // 选中任务详情（常显/初始不隐藏，无任务时清空）
    [SerializeField] private TMP_Text 详情文本;
    [SerializeField] private Button 行动按钮;   // 行动：接取任务 / 提交任务；已结算隐藏
    [SerializeField] private GameObject 任务行模板;   // 任务行模板（挂 任务行）

    private DataService 数据服务 => ServiceRegistry.Get<DataService>();
    private 日常任务服务 日常服务 => ServiceRegistry.Get<日常任务服务>();
    private string 选中标识;

    protected override void Awake()
    {
        base.Awake();
        // 日常进度/结算变化即时刷新（击杀在战斗外发生后回到面板需同步进度）
        ServiceRegistry.Get<EventBus>()?.订阅<日常任务变更事件>(_ => 刷新(null));
    }

    // 标题统一为「设施名 · 日常委托」（与其他功能面板「设施名·功能」风格一致）
    protected override string 标题文字()
    {
        string 名 = 逻辑 != null && !string.IsNullOrEmpty(逻辑.名称) ? 逻辑.名称 : 范围内() switch
        {
            "装备" => "铁匠铺", "食物" => "厨房", "药剂" => "药房", _ => "公榜"
        };
        return $"{名} · 日常委托";
    }

    // 当前设施范围：制作设施按 类型（装备/食物/药剂），其余（任务板/镇长府）为 通用
    private string 范围内()
    {
        if (逻辑 is 制作设施 制作 && !string.IsNullOrEmpty(制作.类型)) return 制作.类型;
        return "通用";
    }

    protected override void 渲染内容()
    {
        清空(内容区);
        var 名单 = 日常服务?.当天日常(范围内());
        if (名单 == null || 名单.Length == 0)
        {
            创建标签(内容区, "暂无任务，明日再来看看。");
            选中标识 = null;
            刷新详情();
            return;
        }
        // 选中态：若要点已在当天列表里则保留，否则清空
        if (选中标识 != null && 查找(名单, 选中标识) == null) 选中标识 = null;
        foreach (var 日常 in 名单)
        {
            var 标识 = 日常.标识;
            string 行名 = 目标名(日常);
            int 状态 = 日常.已领取 ? 任务行.已完成 : (!日常.已接取 || 日常服务.可提交(日常) ? 任务行.可接取 : 任务行.进行中);
            var 行 = 面板基类.创建模板<任务行>(内容区, 任务行模板);
            if (行 == null) continue;   // 模板未接线则跳过该行
            行.绑定(行名, 状态, 标识 == 选中标识, () => 选中任务(标识));
        }
        刷新详情();
    }

    private void 选中任务(string 标识)
    {
        选中标识 = 标识;
        // 详情区未接线时退化为日志展示描述（不崩坏）
        if (详情区 == null)
        {
            var 日常 = 当日().FirstOrDefault(条 => 条.标识 == 标识);
            if (日常 != null) 发布日志($"{标题文字()}\n{描述(日常)}");
            return;
        }
        刷新详情();
    }

    // 详情区：目标名 / 类型·进度 / 奖励 / 描述 / [提交]（已结算则隐藏提交）
    private void 刷新详情()
    {
        var 日常 = 当日().FirstOrDefault(条 => 条.标识 == 选中标识);
        bool 有 = 日常 != null;
        if (!有)
        {
            设文本(详情文本, "");
            if (行动按钮 != null) { 行动按钮.onClick.RemoveAllListeners(); 行动按钮.gameObject.SetActive(false); }
            return;
        }
        设文本(详情文本, 描述(日常));
        if (行动按钮 != null)
        {
            行动按钮.onClick.RemoveAllListeners();
            if (日常.已领取)
            {
                // 已结算：隐藏提交
                行动按钮.gameObject.SetActive(false);
            }
            else if (!日常.已接取)
            {
                // 未接取：认领委托
                行动按钮.gameObject.SetActive(true);
                行动按钮.interactable = true;
                设按钮文本("接取任务");
                var 标识 = 日常.标识;
                行动按钮.onClick.AddListener(() => { if (日常服务.接取(标识)) 刷新(null); });
            }
            else
            {
                // 已接取未结算：回板提交（进度满则可提交，未达则置灰）
                bool 可提交 = 日常服务.可提交(日常);
                行动按钮.gameObject.SetActive(true);
                行动按钮.interactable = 可提交;
                设按钮文本("提交任务");
                var 标识 = 日常.标识;
                行动按钮.onClick.AddListener(() => { if (日常服务.提交(标识)) 选中标识 = null; });
            }
        }
    }

    private string 描述(日常任务 日常)
    {
        string s = $"{类型前缀(日常)}{目标名(日常)}\n{进度文本(日常)}   {奖励文本(日常)}";
        if (日常.已领取) s = $"（已结算）\n{s}";
        return $"{s}\n\n{日常.描述}";
    }

    // 设本面板行动按钮的标签文字（找其「文字」子物体；防空）
    private void 设按钮文本(string 文字)
    {
        if (行动按钮 == null) return;
        var 文本 = 行动按钮.transform.Find("文字")?.GetComponent<TMP_Text>();
        if (文本 != null) 文本.text = 文字;
    }

    private 日常任务[] 当日() => 日常服务?.当天日常(范围内()) ?? new 日常任务[0];
    private 日常任务 查找(日常任务[] 名单, string 标识) => System.Array.Find(名单, 条 => 条.标识 == 标识);

    private void 发布日志(string 正文) => ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.任务, 正文));

    private static string 类型前缀(日常任务 日常) => 日常.目标类型 == "击杀" ? "【击杀】" : "【收集】";

    private string 进度文本(日常任务 日常)
        => $"{(日常.目标类型 == "击杀" ? "已击杀" : "持有")} {日常服务.进度(日常)}/{日常.目标数量}";

    private string 奖励文本(日常任务 日常)
        => $"+{货币工具.文本(日常.奖励金币)} · {日常.奖励经验}经验";

    private string 目标名(日常任务 日常)
    {
        if (日常.目标类型 == "击杀")
            return 数据服务.敌人.TryGetValue(日常.目标标识, out var 敌) ? 敌.名称 : 日常.目标标识;
        return 数据服务.物品.TryGetValue(日常.目标标识, out var 物) ? 物.名称 : 日常.目标标识;
    }
}