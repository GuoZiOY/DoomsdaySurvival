using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// UI 管理器（uGUI）：挂在场景任意物体上（建议新建「UI管理器」空物体挂此脚本）。
// 职责：① 按固定路径抓取场景中搭建好的 UI 元素 ② 订阅事件总线刷新 ③ 切换各面板 ④ 动态填充日志/选项/列表。
// 用户负责用 uGUI 在场景里搭建静态结构（面板/布局/配色），本脚本只做逻辑与动态内容。
public sealed class UI管理器 : MonoBehaviour
{
    public static UI管理器 实例 { get; private set; }

    // —— 按钮预制体（用户搭建：根含 Image+Button+LayoutElement，子物体「文字」TMP_Text）——
    [SerializeField] private GameObject 按钮预制体;

    // 画布根（查找场景 UI 用）
    private Transform 画布根;

    // —— 场景引用（按 画布 下路径查找，用户在场景中按此搭建）——
    private TMP_Text 生命, 魔力, 金币, 时间, 地点;
    private TMP_Text 主标题, 正文;
    private RectTransform 选项区;
    private RectTransform 日志内容;
    private ScrollRect 日志滚动;
    private GameObject 主菜单面板, 主视窗面板, 商店面板, 训练场面板, 任务板面板;
    private RectTransform 商品列表, 技能列表, 任务列表;
    private Button 主菜单新游戏, 主菜单继续, 商店返回, 训练场返回, 任务板返回, 结局返回;

    // —— 状态 ——
    private readonly List<GameObject> 日志条目表 = new List<GameObject>();
    private const int 日志上限 = 200;
    private string 设施返回节点;      // 从设施返回后的剧情节点
    private string 探索返回节点;      // 探索返回后的剧情节点

    void Awake()
    {
        if (实例 != null && 实例 != this) { Destroy(gameObject); return; }
        实例 = this;

        // 先确保核心服务已装配（幂等）——避免与「框架引导」的 Awake 顺序问题
        GameBootstrap.装配();

        var 画布 = FindFirstObjectByType<Canvas>();
        if (画布 == null) { Debug.LogError("[UI管理器] 场景中未找到 Canvas，请先搭建 UI"); return; }
        画布根 = 画布.transform;

        // —— 抓取引用（按固定路径，场景搭建时保持同名）——
        生命 = 画布根.Find("HUD条/生命")?.GetComponent<TMP_Text>();
        魔力 = 画布根.Find("HUD条/魔力")?.GetComponent<TMP_Text>();
        金币 = 画布根.Find("HUD条/金币")?.GetComponent<TMP_Text>();
        时间 = 画布根.Find("HUD条/时间")?.GetComponent<TMP_Text>();
        地点 = 画布根.Find("HUD条/地点")?.GetComponent<TMP_Text>();

        主标题 = 画布根.Find("主视窗面板/标题")?.GetComponent<TMP_Text>();
        正文 = 画布根.Find("主视窗面板/正文")?.GetComponent<TMP_Text>();
        选项区 = 画布根.Find("主视窗面板/选项区") as RectTransform;
        日志内容 = 画布根.Find("日志面板/日志滚动/日志内容") as RectTransform;
        日志滚动 = 画布根.Find("日志面板/日志滚动")?.GetComponent<ScrollRect>();

        主菜单面板 = 画布根.Find("主菜单面板")?.gameObject;
        主视窗面板 = 画布根.Find("主视窗面板")?.gameObject;
        商店面板 = 画布根.Find("商店面板")?.gameObject;
        训练场面板 = 画布根.Find("训练场面板")?.gameObject;
        任务板面板 = 画布根.Find("任务板面板")?.gameObject;

        商品列表 = 画布根.Find("商店面板/商品列表") as RectTransform;
        技能列表 = 画布根.Find("训练场面板/技能列表") as RectTransform;
        任务列表 = 画布根.Find("任务板面板/任务列表") as RectTransform;

        主菜单新游戏 = 画布根.Find("主菜单面板/新游戏按钮")?.GetComponent<Button>();
        主菜单继续 = 画布根.Find("主菜单面板/继续按钮")?.GetComponent<Button>();
        商店返回 = 画布根.Find("商店面板/返回按钮")?.GetComponent<Button>();
        训练场返回 = 画布根.Find("训练场面板/返回按钮")?.GetComponent<Button>();
        任务板返回 = 画布根.Find("任务板面板/返回按钮")?.GetComponent<Button>();
        结局返回 = 画布根.Find("主视窗面板/返回主菜单")?.GetComponent<Button>();

        // —— 订阅事件 ——
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<日志事件>(追加日志);
        事件.订阅<生命变化事件>(e => 设置文本(生命, $"生命 {e.当前}/{e.最大}", 游戏主题.危险));
        事件.订阅<魔力变化事件>(e => 设置文本(魔力, $"魔 {e.当前}/{e.最大}", null));
        事件.订阅<金币变化事件>(e => 设置文本(金币, $"{e.当前} 金", 游戏主题.金色));
        事件.订阅<显示剧情事件>(显示剧情);
        事件.订阅<战斗消息事件>(e => 正文.text = e.文本);
        事件.订阅<战斗结束事件>(处理战斗结束);
        事件.订阅<打开战斗事件>(打开战斗);
        事件.订阅<探索显示事件>(显示探索);
        事件.订阅<打开设施事件>(打开设施);
        事件.订阅<打开结局事件>(_ => 显示结局());

        // —— 主菜单按钮 ——
        if (主菜单新游戏 != null) 主菜单新游戏.onClick.AddListener(() => 开始游戏(false));
        if (主菜单继续 != null) 主菜单继续.onClick.AddListener(() => 开始游戏(true));
        if (商店返回 != null) 商店返回.onClick.AddListener(返回剧情);
        if (训练场返回 != null) 训练场返回.onClick.AddListener(返回剧情);
        if (任务板返回 != null) 任务板返回.onClick.AddListener(返回剧情);
        if (结局返回 != null) 结局返回.onClick.AddListener(() => { 显示面板(主菜单面板); });

        // —— 初始状态（读玩家档案填充 HUD）——
        var 玩家 = ServiceRegistry.Get<PlayerService>().档案;
        设置文本(生命, $"生命 {玩家.生命}/{玩家.最大生命}", 游戏主题.危险);
        设置文本(魔力, $"魔 {玩家.魔力}/{玩家.最大魔力}", null);
        设置文本(金币, $"{玩家.金币} 金", 游戏主题.金色);
        设置文本(时间, 格式化时间(玩家), null);

        显示面板(主菜单面板);
    }

    // ================= 面板切换 =================

    // 只激活目标面板，其余（主菜单/主视窗/商店/训练场/任务板）隐藏
    private void 显示面板(GameObject 目标)
    {
        var 面板们 = new[] { 主菜单面板, 主视窗面板, 商店面板, 训练场面板, 任务板面板 };
        foreach (var 面板 in 面板们)
            if (面板 != null) 面板.SetActive(面板 == 目标);
    }

    // ================= 剧情 =================

    // 显示剧情：主视窗 标题/正文/选项
    private void 显示剧情(显示剧情事件 e)
    {
        显示面板(主视窗面板);
        if (主标题 != null) { 主标题.text = "剧情"; 主标题.color = 游戏主题.金色; }
        正文.text = e.文本;
        结局返回?.gameObject.SetActive(false);
        清空选项();
        if (e.选项 == null) return;
        foreach (var 选项 in e.选项)
        {
            var 目标 = 选项.目标;
            创建按钮(选项区, 选项.文本, () => ServiceRegistry.Get<DialogueService>().处理选项(目标));
        }
    }

    // ================= 战斗 =================

    // 打开战斗面板（主视窗复用：标题=战斗，行动区=攻击/技能/道具/逃跑）
    private void 打开战斗(打开战斗事件 e)
    {
        探索返回节点 = e.返回节点;
        显示面板(主视窗面板);
        if (主标题 != null) { 主标题.text = "战斗"; 主标题.color = 游戏主题.危险; }
        正文.text = ServiceRegistry.Get<BattleService>().当前消息;
        结局返回?.gameObject.SetActive(false);
        显示主行动();
    }

    // 主行动：攻击 / 技能 / 道具 / 逃跑
    private void 显示主行动()
    {
        清空选项();
        创建按钮(选项区, "攻击", () => ServiceRegistry.Get<BattleService>().玩家攻击());
        创建按钮(选项区, "技能", 显示技能页);
        创建按钮(选项区, "道具", 显示道具页);
        创建按钮(选项区, "逃跑", () => ServiceRegistry.Get<BattleService>().逃跑());
    }

    private void 显示技能页()
    {
        清空选项();
        var 玩家 = ServiceRegistry.Get<PlayerService>().档案;
        var 数据 = ServiceRegistry.Get<DataService>();
        foreach (var 技能标识 in 玩家.已学技能)
        {
            if (!数据.技能.TryGetValue(技能标识, out var 技能)) continue;
            if (玩家.魔力 < 技能.消耗魔力) continue;
            var 标识 = 技能标识;
            创建按钮(选项区, $"{技能.名称}（{技能.消耗魔力}MP）", () => ServiceRegistry.Get<BattleService>().玩家技能(标识));
        }
        创建按钮(选项区, "返回", 显示主行动);
    }

    private void 显示道具页()
    {
        清空选项();
        var 玩家 = ServiceRegistry.Get<PlayerService>().档案;
        var 数据 = ServiceRegistry.Get<DataService>();
        foreach (var 堆叠 in 玩家.背包)
        {
            if (堆叠.数量 <= 0) continue;
            if (!数据.物品.TryGetValue(堆叠.标识, out var 物品) || 物品.类型 != "恢复") continue;
            var 标识 = 堆叠.标识;
            创建按钮(选项区, $"使用{物品.名称}（×{堆叠.数量}）", () => ServiceRegistry.Get<BattleService>().玩家道具(标识));
        }
        创建按钮(选项区, "返回", 显示主行动);
    }

    // 战斗结束：胜利→胜利节点；失败/逃跑→返回节点；探索战斗→回探索
    private void 处理战斗结束(战斗结束事件 e)
    {
        var 服务 = ServiceRegistry.Get<BattleService>();
        var 节点 = e.胜利 ? 服务.胜利节点 : 服务.返回节点;
        if (节点 == "__探索胜利") { ServiceRegistry.Get<探索服务>().战斗胜利(); return; }
        if (节点 == "__探索返回") { ServiceRegistry.Get<探索服务>().战斗逃跑(); return; }
        ServiceRegistry.Get<DialogueService>().进入节点(节点);
    }

    // ================= 探索 =================

    private void 显示探索(探索显示事件 e)
    {
        显示面板(主视窗面板);
        if (主标题 != null) { 主标题.text = "探索"; 主标题.color = 游戏主题.金色; }
        正文.text = e.文本;
        结局返回?.gameObject.SetActive(false);
        清空选项();
        if (e.选项 == null) return;
        foreach (var 选项 in e.选项)
        {
            var 动作 = 选项.动作;
            创建按钮(选项区, 选项.文本, () => 处理探索动作(动作));
        }
    }

    private void 处理探索动作(string 动作)
    {
        var 探索 = ServiceRegistry.Get<探索服务>();
        switch (动作)
        {
            case "深入": 探索.深入探索(); break;
            case "战斗": 打开战斗(new 打开战斗事件(探索.返回节点)); break;
            case "返回": ServiceRegistry.Get<DialogueService>().进入节点(探索.返回节点); break;
        }
    }

    // ================= 结局 =================

    private void 显示结局()
    {
        显示面板(主视窗面板);
        if (主标题 != null) { 主标题.text = "—— 序章 · 完 ——"; 主标题.color = 游戏主题.金色; }
        正文.text = "灰烬镇的余烬还在燃烧。龙在山的深处沉睡。\n\n你的故事，才刚刚开始。";
        清空选项();
        结局返回?.gameObject.SetActive(true);
    }

    // ================= 设施（商店/训练场/任务板）=================

    private void 打开设施(打开设施事件 e)
    {
        设施返回节点 = e.返回节点;
        switch (e.设施标识)
        {
            case "商店": 打开商店(); break;
            case "训练场": 打开训练场(); break;
            case "任务板": 打开任务板(); break;
            default: Debug.LogWarning($"[UI管理器] 未实现的设施: {e.设施标识}"); break;
        }
    }

    private void 打开商店()
    {
        显示面板(商店面板);
        设置文本(画布根.Find("商店面板/标题")?.GetComponent<TMP_Text>(), "—— 酒馆 · 补给 ——", 游戏主题.金色);
        清空列表(商品列表);
        var 玩家 = ServiceRegistry.Get<PlayerService>().档案;
        var 数据 = ServiceRegistry.Get<DataService>();
        foreach (var 物品 in 数据.物品.Values)
        {
            if (物品.价格 <= 0 || 物品.类型 == "任务") continue;
            var 标识 = 物品.标识;
            创建按钮(商品列表, $"{物品.名称}（{物品.描述}）  {物品.价格}金", () => 处理购买(玩家, 数据, 标识));
        }
    }

    private void 处理购买(玩家档案 玩家, DataService 数据, string 物品标识)
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        if (!数据.物品.TryGetValue(物品标识, out var 物品)) return;
        if (玩家.金币 < 物品.价格) { 事件.发布(new 日志事件(日志类型.反馈坏, $"金币不足（需要 {物品.价格}）。")); return; }
        玩家.金币 -= 物品.价格;
        if (物品.类型 == "武器") 玩家.武器标识 = 物品.标识;
        else if (物品.类型 == "防具") 玩家.防具标识 = 物品.标识;
        else 玩家.添加物品(物品.标识);
        事件.发布(new 金币变化事件(玩家.金币, -物品.价格));
        事件.发布(new 日志事件(日志类型.反馈, $"购买 {物品.名称}（-{物品.价格} 金币）"));
    }

    private void 打开训练场()
    {
        显示面板(训练场面板);
        设置文本(画布根.Find("训练场面板/标题")?.GetComponent<TMP_Text>(), "—— 训练场 ——", 游戏主题.金色);
        清空列表(技能列表);
        var 玩家 = ServiceRegistry.Get<PlayerService>().档案;
        var 数据 = ServiceRegistry.Get<DataService>();
        foreach (var 技能 in 数据.技能.Values)
        {
            var 标识 = 技能.标识;
            创建按钮(技能列表, $"{技能.名称}（{技能.描述}）  {技能.价格}金", () => 学习技能(玩家, 数据, 标识));
        }
    }

    private void 学习技能(玩家档案 玩家, DataService 数据, string 技能标识)
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        if (!数据.技能.TryGetValue(技能标识, out var 技能)) return;
        if (玩家.掌握技能(技能标识)) { 事件.发布(new 日志事件(日志类型.系统, "你已经掌握这个技能了。")); return; }
        if (玩家.金币 < 技能.价格) { 事件.发布(new 日志事件(日志类型.反馈坏, $"金币不足（需要 {技能.价格}）。")); return; }
        玩家.金币 -= 技能.价格;
        玩家.学习技能(技能标识);
        事件.发布(new 金币变化事件(玩家.金币, -技能.价格));
        事件.发布(new 日志事件(日志类型.反馈, $"你学会了技能：{技能.名称}（-{技能.价格} 金币）"));
    }

    private void 打开任务板()
    {
        显示面板(任务板面板);
        设置文本(画布根.Find("任务板面板/标题")?.GetComponent<TMP_Text>(), "—— 任务板 ——", 游戏主题.金色);
        清空列表(任务列表);
        var 玩家 = ServiceRegistry.Get<PlayerService>().档案;
        var 数据 = ServiceRegistry.Get<DataService>();
        foreach (var 任务 in 数据.任务.Values)
        {
            var 标识 = 任务.标识;
            string 进度 = "接取";
            foreach (var p in 玩家.任务)
                if (p.标识 == 标识) 进度 = p.已完成 ? "已完成" : $"{p.数量}/{任务.目标数量}";
            string 文字 = $"{任务.名称}（{任务.描述}）  +{任务.奖励金币}金 +{任务.奖励经验}经验  [{进度}]";
            创建按钮(任务列表, 文字, () => ServiceRegistry.Get<QuestService>().接取(标识));
        }
    }

    // 从设施返回：回主视窗并重新显示剧情（进入返回节点会发布 显示剧情事件）
    private void 返回剧情()
    {
        if (string.IsNullOrEmpty(设施返回节点)) { 显示面板(主菜单面板); return; }
        ServiceRegistry.Get<DialogueService>().进入节点(设施返回节点);
    }

    // ================= 主菜单 =================

    private void 开始游戏(bool 读档)
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>();
        var 对话 = ServiceRegistry.Get<DialogueService>();
        if (读档) 玩家.读档(); else 玩家.新游戏();
        对话.进入节点(玩家.档案.当前节点);
    }

    // ================= 日志 =================

    private void 追加日志(日志事件 e)
    {
        if (日志内容 == null) return;
        var 条目 = new GameObject("日志条目", typeof(RectTransform), typeof(TextMeshProUGUI));
        条目.transform.SetParent(日志内容, false);
        var 文本 = 条目.GetComponent<TextMeshProUGUI>();
        文本.fontSize = 20f;
        文本.enableWordWrapping = true;
        文本.raycastTarget = false;
        文本.color = 游戏主题.文字;
        文本.text = $"<color={游戏主题.时间戳色值}>[{格式化时间(ServiceRegistry.Get<PlayerService>().档案)}]</color> <color={游戏主题.日志色(e.类型)}>{e.文本}</color>";
        日志条目表.Add(条目);
        if (日志条目表.Count > 日志上限)
        {
            Destroy(日志条目表[0]);
            日志条目表.RemoveAt(0);
        }
        日志滚动到底部();
    }

    // ================= 工具 =================

    // 实例化按钮预制体到指定父级，设文字、接点击
    private void 创建按钮(Transform 父级, string 文字, Action 点击)
    {
        if (按钮预制体 == null)
        {
            Debug.LogError("[UI管理器] 未设置「按钮预制体」：请在 Inspector 槽位拖入（根含 Image+Button+LayoutElement，子物体「文字」TMP_Text）");
            return;
        }
        var 物体 = Instantiate(按钮预制体, 父级, false);
        var 文本 = 物体.transform.Find("文字")?.GetComponent<TMP_Text>();
        if (文本 != null) 文本.text = 文字;
        var 按钮 = 物体.GetComponent<Button>();
        if (按钮 != null) 按钮.onClick.AddListener(() => 点击());
    }

    private void 清空选项()
    {
        if (选项区 == null) return;
        for (int i = 选项区.childCount - 1; i >= 0; i--) Destroy(选项区.GetChild(i).gameObject);
    }

    private void 清空列表(RectTransform 列表)
    {
        if (列表 == null) return;
        for (int i = 列表.childCount - 1; i >= 0; i--) Destroy(列表.GetChild(i).gameObject);
    }

    private void 设置文本(TMP_Text 文本, string 内容, Color? 颜色)
    {
        if (文本 == null) return;
        文本.text = 内容;
        if (颜色.HasValue) 文本.color = 颜色.Value;
    }

    private void 日志滚动到底部()
    {
        Canvas.ForceUpdateCanvases();
        if (日志滚动 != null) 日志滚动.verticalNormalizedPosition = 0f;
    }

    private static string 格式化时间(玩家档案 玩家)
    {
        int 分钟 = (int)玩家.游戏分钟数;
        return $"{分钟 / 60 % 24:00}:{分钟 % 60:00}";
    }
}
