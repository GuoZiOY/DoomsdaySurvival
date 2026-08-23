using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 战斗面板：主视窗下的内容面板。文字血条 + 点卡牌选目标 + 文字状态 + 复用主日志栏(D20-B)。
// 交互状态机: 行动选择 → 技能/道具子菜单 → 目标选择(点卡牌) → 执行 → 结算。
// [攻击]单独按钮 = 内置普攻技能(倍率1.0物理无消耗)。场景手动搭结构，代码克隆卡牌/子菜单项。
// 伪演出：伤害飘字/受击震颤/阵亡淡出/行动强调/布阵入场/回合横幅（DOTween）。
public sealed partial class 战斗面板 : 面板基类
{
    // —— Inspector 拖入 ——
    [SerializeField] private TMP_Text 信息条;             // "回合 N: ▶当前 下一步 排队"（富文本着色，信息条+行动序列合一）
    [SerializeField] private RectTransform 敌方区;        // 敌方卡牌容器
    [SerializeField] private RectTransform 我方区;        // 我方卡牌容器
    [SerializeField] private GameObject 卡牌模板;         // 战斗单位卡牌模板（敌我共用）
    [SerializeField] private Button 攻击按钮, 技能按钮, 道具按钮, 逃跑按钮;
    [SerializeField] private Button 切换武器按钮;   // 切武器（消耗回合=防御，可选；场景需拖入按钮）
    [SerializeField] private Button 增幅按钮;     // 增幅切换（0→1→2→3→0，消耗 BP 提升下次攻击，可选）
    [SerializeField] private RectTransform 主行动区;      // [攻击][技能][道具][逃跑]
    [SerializeField] private RectTransform 子菜单区;      // 技能/道具列表（初始隐藏）
    [SerializeField] private RectTransform 列表容器;      // 子菜单项容器
    [SerializeField] private GameObject 技能行模板;       // 技能子菜单行模板（挂 技能行：技能名/消耗/数值）
    [SerializeField] private GameObject 道具行模板;       // 道具子菜单行模板（挂 道具行：品质/名字/数量）
    [SerializeField] private GameObject 结算覆盖层;       // 结算界面（初始隐藏）
    [SerializeField] private TMP_Text 结算文字;
    [SerializeField] private Button 继续按钮;
    [SerializeField] private RectTransform 飘字层;       // 伤害/治疗飘字容器（可选，不设则用卡牌父容器）
    [SerializeField] private GameObject 飘字模板;         // 飘字文本模板（挂 TMP_Text；可选，不设则运行时创建）
    [SerializeField] private TMP_Text 回合横幅;           // "回合 N" 横幅（演出用，可选）

    // —— 演出参数 ——
    [SerializeField] private float 飘字时长 = 1f;         // 伤害/治疗/技能名 飘字停留时长（统一淡出时长，淡出完销毁）
    [SerializeField] private Color 技能名色 = new Color(1f, 0.8f, 0.42f);   // 技能释放时飘出的技能名颜色（暖金）
    [SerializeField] private float 技能名停顿 = 0.35f;    // 技能名飘出后到真正结算伤害的停顿，让玩家先看到技能名
    [SerializeField] private float 飘字上浮 = 56f;        // 飘字上浮距离（世界像素）
    [SerializeField] private float 结算延迟 = 0.55f;      // 战斗结束到弹结算面板的延迟（等最后的阵亡/倒下动画播完）

    private BattleService 战斗;
    private int 当前回合;
    private string 信息条缓存 = "";   // 内容缓存：序列/回合未变化时不重复重建 TMP 文本（敌方停顿期间避免每帧 settext）
    private string 待选行动;   // null=行动选择；"普攻"/技能标识/道具标识 = 正在选目标
    private List<战斗单位> 当前可选集合;   // 当前选目标的可点集合（点击卡牌时校验）
    [SerializeField] private float 敌方行动停顿 = 0.8f;   // 敌方/助战每次行动的停顿秒数（演出节奏，让玩家看清动作与受击）
    [SerializeField] private float 受击停顿 = 0.3f;       // 受击/治疗演出后的停顿秒数（让伤害数字/震颤可见）
    private readonly Dictionary<战斗单位, 战斗单位卡牌> 卡牌表 = new Dictionary<战斗单位, 战斗单位卡牌>();

    // —— 演出时序：单驱动协程 + 片段队列。
    // 所有时序事件(伤害/治疗/阵亡/行动轮换/回合开始)不再"收到即播"，而是压成可迭代的演出片段入队，
    // 驱动协程逐个 MoveNext 播放；每个片段自带停顿节奏，播放完毕才播下一个。
    // 战斗推进(继续())也作为片段挂到队尾 → 逻辑一帧只推进一格，事件不会同帧挤爆、演出不会被打断。
    private readonly Queue<System.Collections.IEnumerator> 演出片段 = new Queue<System.Collections.IEnumerator>();
    private bool 演出驱动中;

    void Awake()
    {
        战斗 = ServiceRegistry.Get<BattleService>();
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<战斗开始事件>(渲染布阵);
        事件.订阅<行动轮换事件>(e => 处理行动轮换(e));
        事件.订阅<伤害事件>(e => 入队演出(演出_受击(e)));
        事件.订阅<治疗事件>(e => 入队演出(演出_治疗(e)));
        事件.订阅<状态变化事件>(e => 刷新卡牌(e.单位));
        事件.订阅<目标变化事件>(e => 入队演出(演出_阵亡(e.单位)));
        事件.订阅<回合开始事件>(e => { 当前回合 = e.回合数; if (回合横幅 != null) 回合横幅.gameObject.SetActive(false); 刷新信息条(); });
        事件.订阅<战斗结束事件>(e => 入队演出(演出_结束(e)));

        攻击按钮.onClick.AddListener(() => 开始选目标("普攻"));
        技能按钮.onClick.AddListener(() => 显示子菜单(true));
        道具按钮.onClick.AddListener(() => 显示子菜单(false));
        逃跑按钮.onClick.AddListener(() => 战斗.逃跑());
        if (切换武器按钮 != null) 切换武器按钮.onClick.AddListener(() => 战斗.切换武器());
        if (增幅按钮 != null)
        {
            增幅按钮.onClick.AddListener(() => { 战斗.切换增幅(); 刷新增幅按钮(); });
            刷新增幅按钮();
        }
        继续按钮.onClick.AddListener(() => { 结算覆盖层?.SetActive(false); 战斗.返回(); });   // 点继续立即收起结算层，再进结果节点
        结算覆盖层?.SetActive(false);
        子菜单区?.gameObject.SetActive(false);
    }

    // 战斗面板完全由事件驱动，刷新 无上下文路由
    protected override void 刷新(object 上下文) { }

    // 全局取消：子菜单展开 或 选目标中 = 取消回动作栏（右键=反悔）；行动选择/敌方回合/结算中不可取消
    public override bool 回退()
    {
        bool 子菜单开 = 子菜单区 != null && 子菜单区.gameObject.activeSelf;
        if (待选行动 != null || 子菜单开) { 回主行动(); return true; }
        return false;
    }

    // ===== 布阵 =====
    private void 渲染布阵(战斗开始事件 e)
    {
        // 生命周期重置：新战斗开始即清掉上一场的结算覆盖层与交互区状态
        结算覆盖层?.SetActive(false);
        主行动区.gameObject.SetActive(true);
        子菜单区?.gameObject.SetActive(false);
        清空(敌方区); 清空(我方区); 卡牌表.Clear();
        待选行动 = null;
        当前可选集合 = null;
        foreach (var 单位 in e.敌方) 创建卡牌(单位, 敌方区);
        foreach (var 单位 in e.我方) 创建卡牌(单位, 我方区);
        // 战斗面板物体在场景中默认 inactive，激活当下实例化卡牌，容器上的布局组不会自动立即重算，
        // 导致全部卡牌堆在容器左上角——这里强制立即重建两边布局，卡牌才会按 HorizontalLayoutGroup 排布居中。
        重排(敌方区); 重排(我方区);
        当前回合 = 0;
        刷新信息条();
    }

    // 强制父容器立即重算布局组（敌方区/我方区 都是 HorizontalLayoutGroup）
    private static void 重排(RectTransform 容器)
    {
        if (容器 == null) return;
        if (容器.GetComponent<HorizontalLayoutGroup>() == null && 容器.GetComponent<VerticalLayoutGroup>() == null)
            return;   // 无布局组的容器不做处理（保持原有摆放逻辑）
        LayoutRebuilder.ForceRebuildLayoutImmediate(容器);
    }

    // 信息条 = 回合数 + 速度序。视觉层次（富文本）：
    //   当前行动者 = 金色 ▶ 放大（焦点） · 「下一个我方行动者」= 亮青（玩家关心的出手时机） · 其它排队敌方 = 灰 · 已行动 = 淡删除线（队尾）。
    // 逻辑保留": 旋转式"：当前永远在队首，已行动绕到末尾。顺序仍由速度定死，这里只做可读性呈现。
    private void 刷新信息条()
    {
        if (信息条 == null) return;
        string 内容;
        if (待选行动 != null) { 内容 = "请选择目标…"; }
        else
        {
            var 序 = 战斗.本回合行动显示();
            int 当前 = 序.IndexOf(战斗.当前行动单位);
            if (当前 < 0 && 序.Count > 0) 当前 = 0;   // 尚未开始行动时，第一个视为当前
            var 段 = new List<string>();
            bool 标出我方 = false;   // 排队里只标出「第一个」「下方的我方」（玩家/助战的下一次出手时机）
            for (int i = 0; i < 序.Count; i++)
            {
                int 原 = (当前 + i) % 序.Count;        // 旋转：从当前开始，已行动的绕到末尾
                var 单位 = 序[原];
                string 名 = 单位.名称 + (单位.无法行动 ? "(晕)" : "");
                string 一段;
                if (i == 0) 一段 = $"<color={游戏主题.金色色值}>▶ {名}</color>";// 当前行动（焦点，字号保持原样）
                else if (原 < 当前) 一段 = $"<color={游戏主题.已行动灰值}><s>{名}</s></color>";  // 已行动（队尾淡删除线）
                else if (单位.是否我方 && !标出我方)                                            // 排队中「下一个我方」= 出手时机
                {
                    一段 = $"<color={游戏主题.出手青值}>{名}</color>";
                    标出我方 = true;
                }
                else 一段 = $"<color={游戏主题.排队灰值}>{名}</color>";                          // 排队中的敌方/其余
                段.Add(一段);
            }
            内容 = $"回合 {当前回合}  ·  " + string.Join("  ", 段);
        }
        if (内容 == 信息条缓存) return;
        信息条缓存 = 内容;
        信息条.text = 内容;
    }

    // 刷新「增幅」按钮文字：显示当前增幅级与 BP（空安全，无按钮/无子文本则静默）
    private void 刷新增幅按钮()
    {
        if (增幅按钮 == null || 战斗?.玩家 == null) return;
        var 文本 = 增幅按钮.GetComponentInChildren<TMP_Text>();
        if (文本 == null) return;
        int 级 = 战斗.当前增幅;
        文本.text = 级 > 0 ? $"增幅 ×{级}（{战斗.玩家.BP} BP）" : $"增幅（{战斗.玩家.BP} BP）";
    }

    private void 创建卡牌(战斗单位 单位, RectTransform 父)
    {
        if (卡牌模板 == null) return;
        var 物体 = Instantiate(卡牌模板, 父, false);
        物体.SetActive(true);
        var 卡牌 = 物体.GetComponent<战斗单位卡牌>();
        if (卡牌 == null) return;
        // 只复位影响动画的姿态参数；锚点/pivot/尺寸 交给 HorizontalLayoutGroup 用模板原值接管，
        // 擅自改 anchor/sizeDelta 会被布局组 childControl 覆盖，反而造成错位。
        var 矩形 = (RectTransform)物体.transform;
        矩形.localScale = Vector3.one;
        矩形.rotation = Quaternion.identity;
        卡牌.绑定(单位, 点击卡牌);
        音效管理器.实例?.注册按钮(物体.GetComponent<Button>());   // 战斗卡牌动态按钮成功音效
        卡牌.清除高亮();   // 初始干净卡面
        卡牌表[单位] = 卡牌;
    }

    // 选目标结束/取消时：清除所有卡牌蒙版高亮（否则右键返回后仍高亮可点）
    private void 重置卡牌可选()
    {
        foreach (var kv in 卡牌表) kv.Value.清除高亮();
    }

    private void 点击卡牌(战斗单位 目标)
    {
        if (待选行动 == null || 目标 == null) return;
        if (当前可选集合 == null || !当前可选集合.Contains(目标)) return;   // 只允许点可选目标
        string 行动 = 待选行动;
        待选行动 = null;
        当前可选集合 = null;
        重置卡牌可选();
        主行动区.gameObject.SetActive(false);   // 结算期间禁止再操作
        StartCoroutine(行动结算(行动, 目标));
    }

    // 先行动画（玩家卡牌 强调+跳一下），动画结束再结算行动逻辑（先动画后判定）
    private System.Collections.IEnumerator 行动结算(string 行动, 战斗单位 目标)
    {
        if (卡牌表.TryGetValue(战斗.玩家, out var 卡牌)) { 卡牌.强调行动(); 卡牌.行动跳跃(); }
        yield return new WaitForSeconds(战斗单位卡牌.行动跳跃时长);
        if (行动 != "普攻")
        {
            var 数据 = ServiceRegistry.Get<DataService>();
            if (数据.技能.ContainsKey(行动)) 飘技能名(数据.技能[行动].名称);   // 先亮技能名，再结算伤害
            yield return new WaitForSeconds(技能名停顿);
        }
        switch (行动)
        {
            case "普攻": 战斗.玩家普攻(目标); break;
            default:
                var 数据 = ServiceRegistry.Get<DataService>();
                if (数据.技能.ContainsKey(行动)) 战斗.玩家技能(行动, 目标);
                else 战斗.玩家道具(行动, 目标);
                break;
        }
    }

    // ===== 行动轮换 =====
    // 只做即时 UI 刷新与节奏安排；真正的"行动执行+停顿"由入队的"敌方行动演出片段"驱动，
    // 与其它演出片段在单队列里串行，实现"每次行动都停顿、一帧只推进一格"
    private void 处理行动轮换(行动轮换事件 e)
    {
        // 玩家回合判定走 战斗.玩家回合（本人）；助战单位回合不算玩家回合，也走自动推进
        bool 是玩家回合 = 战斗.玩家回合;
        主行动区.gameObject.SetActive(是玩家回合);
        子菜单区?.gameObject.SetActive(false);
        待选行动 = null;
        当前可选集合 = null;
        重置卡牌可选();
        foreach (var kv in 卡牌表) kv.Value.刷新();
        刷新增幅按钮();
        刷新信息条();
        // 敌方/助战回合：把"行动者强调 + 停顿 + 触发执行"作为一个演出片段入队，串行播放
        if (!是玩家回合 && e.行动单位 != null)
        {
            if (gameObject.activeInHierarchy) 入队演出(演出_敌方行动(e.行动单位));
            else 战斗.继续();   // 面板未激活（探索遭遇等）：无停顿直接推进，避免卡住
        }
    }

    // ===== 子菜单（技能/道具 用各自的行模板） =====
    private void 显示子菜单(bool 是技能)
    {
        待选行动 = null;
        清空(列表容器);
        var 数据 = ServiceRegistry.Get<DataService>();
        if (是技能)
        {
            foreach (var 标识 in 战斗.玩家.已学技能)
            {
                if (!数据.技能.TryGetValue(标识, out var 技能) || 战斗.玩家.冷却剩余(标识) > 0) continue;
                var 标识副本 = 标识;
                创建技能项(技能, () => 开始选目标(标识副本));
            }
        }
        else
        {
            foreach (var 堆叠 in ServiceRegistry.Get<PlayerService>().档案.背包)
            {
                if (!数据.物品.TryGetValue(堆叠.标识, out var 物品) || !物品.战斗内使用) continue;
                var 标识 = 堆叠.标识;
                创建道具项(堆叠, 物品, () => 开始选目标(标识));
            }
        }
        主行动区.gameObject.SetActive(false);
        子菜单区.gameObject.SetActive(true);
    }

    // 技能行：技能名 / 消耗 MP / 数值（伤害倍率或恢复量）
    private void 创建技能项(技能数据 技能, System.Action 点击)
    {
        if (技能行模板 == null) { Debug.LogError("[战斗面板] 未设置 技能行模板——技能子菜单无法实例化"); return; }
        var 行 = 面板基类.创建模板<技能行>(列表容器, 技能行模板);
        if (行 == null) return;
        if (行.技能名 != null) 行.技能名.text = 物品工具.品质名称(技能.品质档, 技能.名称);
        if (行.消耗 != null) 行.消耗.text = $"{技能.消耗魔力} MP";
        if (行.数值 != null) 行.数值.text = 技能数值文本(技能);
        挂菜单点击(行.GetComponent<Button>(), 点击);
    }

    // 道具行：名字（含品质标签）/ 数量（选中是下一步选目标的事，子菜单行不设选中态）
    private void 创建道具项(物品堆叠 堆叠, 物品数据 物品, System.Action 点击)
    {
        if (道具行模板 == null) { Debug.LogError("[战斗面板] 未设置 道具行模板——道具子菜单无法实例化"); return; }
        var 行 = 面板基类.创建模板<道具行>(列表容器, 道具行模板);
        if (行 == null) return;
        if (行.名字 != null) 行.名字.text = 物品工具.品质名称(物品.品质档, 物品.名称);
        if (行.数量 != null) 行.数量.text = 堆叠.数量 > 1 ? $"×{堆叠.数量}" : "";
        挂菜单点击(行.GetComponent<Button>(), 点击);
    }

    private static void 挂菜单点击(Button 按钮, System.Action 点击)
    {
        if (按钮 == null) return;
        按钮.onClick.AddListener(() => 点击());
        音效管理器.实例?.注册按钮(按钮);   // 子菜单项动态按钮成功音效
    }

    // 技能数值文本（简洁）：攻击按伤害方式 ×N / +N / N，并按物理/魔法/真实分色；治疗=+N HP 绿色
    private static string 技能数值文本(技能数据 技能)
    {
        switch (技能.类别枚举)
        {
            case 技能类别.攻击:
            {
                string 色 = 技能.伤害类型枚举 switch
                {
                    伤害类型.物理 => 游戏主题.物攻色值,
                    伤害类型.魔法 => 游戏主题.魔攻色值,
                    _ => 游戏主题.高亮色值
                };
                string 量 = 技能.伤害方式枚举 switch
                {
                    技能伤害方式.固定 => 技能.数值.ToString("0.#"),
                    技能伤害方式.附加 => "+" + 技能.数值.ToString("0.#"),
                    _ => "x" + 技能.数值.ToString("0.#")
                };
                return "<color=" + 色 + ">" + 量 + "</color>";
            }
            case 技能类别.治疗: return $"<color={游戏主题.成功色值}>+{技能.数值.ToString("0.#")} HP</color>";
            default: return 技能.描述;
        }
    }

    private void 回主行动()
    {
        子菜单区?.gameObject.SetActive(false);
        // 只在仍是玩家回合时显示动作栏（行动后可能已轮到敌人，不能强行亮出来）
        主行动区.gameObject.SetActive(战斗.玩家回合);
        待选行动 = null;
        当前可选集合 = null;
        重置卡牌可选();
        刷新信息条();
    }

    // ===== 目标选择 =====
    private void 开始选目标(string 行动)
    {
        待选行动 = 行动;
        当前可选集合 = 可选中集合(行动);
        var 期望 = 行动的期望目标(行动);
        // 减负：目标为自己 的技能/道具 免选，直接对玩家执行
        if (行动 != "普攻" && 期望 == 目标类型.自己)
        {
            if (战斗.玩家.存活 && 当前可选集合.Contains(战斗.玩家)) 点击卡牌(战斗.玩家);
            else 待选行动 = null;
            return;   // 点击卡牌 内部已 待选行动=null + 回主行动
        }
        // 多目标技能：存活目标数 <= 所需目标数时自动判定执行（像防御一样免选）
        // 敌方两名=2 / 敌方全体=1（只剩 1 个时全打它）；其余情况需手动选目标
        if (行动 != "普攻" && (期望 == 目标类型.敌方两名 || 期望 == 目标类型.敌方全体))
        {
            int 所需 = 期望 == 目标类型.敌方两名 ? 2 : 1;
            if (当前可选集合.Count <= 所需)
            {
                if (当前可选集合.Count > 0) 点击卡牌(当前可选集合[0]);   // 自动执行（执行层自动补足目标）
                else 待选行动 = null;
                return;
            }
        }
        // 收起子菜单/动作栏，露出卡牌选目标（否则会盖住己方卡牌，无法选治疗/增益目标）
        子菜单区?.gameObject.SetActive(false);
        主行动区.gameObject.SetActive(false);
        foreach (var kv in 卡牌表)
            kv.Value.设可选(当前可选集合.Contains(kv.Key));
        刷新信息条();
    }

    // 行动期望的目标类型（普攻=敌方单体；技能/道具按数据）
    private 目标类型 行动的期望目标(string 行动)
    {
        if (行动 == "普攻") return 目标类型.敌方单体;
        var 数据 = ServiceRegistry.Get<DataService>();
        if (数据.技能.TryGetValue(行动, out var 技能)) return 技能.目标枚举;
        if (数据.物品.TryGetValue(行动, out var 物品)) return 物品.使用目标枚举;
        return 目标类型.敌方单体;
    }

    private List<战斗单位> 可选中集合(string 行动)
    {
        var 数据 = ServiceRegistry.Get<DataService>();
        var 列表 = new List<战斗单位>();
        if (行动 == "普攻") { 列表.AddRange(战斗.敌方); return 存活(列表); }
        if (数据.技能.TryGetValue(行动, out var 技能))
            return 目标集合(技能.目标枚举);
        if (数据.物品.TryGetValue(行动, out var 物品))
            return 目标集合(物品.使用目标枚举);
        return 列表;
    }

    private List<战斗单位> 目标集合(目标类型 类型)
    {
        var 源 = 类型 == 目标类型.敌方单体 || 类型 == 目标类型.敌方全体 || 类型 == 目标类型.敌方两名 ? 战斗.敌方 : 战斗.我方;
        var 列表 = new List<战斗单位>(源);
        return 存活(列表);
    }

    private static List<战斗单位> 存活(List<战斗单位> 列表)
    {
        列表.RemoveAll(u => !u.存活);
        return 列表;
    }

    // ===== 刷新/移除卡牌 =====
    private void 刷新卡牌(战斗单位 单位)
    {
        if (卡牌表.TryGetValue(单位, out var 卡牌)) 卡牌.刷新();
    }

    // —— 演出队列核心 ——
    // 所有受影响事件的演出压成可迭代片段入队，由单一驱动协程串行播放。
    // 队列空时驱动协程自然结束；新事件入队时若有演出驱动中则自动追加同队列播放。
    private void 入队演出(System.Collections.IEnumerator 片段)
    {
        演出片段.Enqueue(片段);
        if (!演出驱动中 && gameObject.activeInHierarchy) StartCoroutine(演出驱动());
    }

    private System.Collections.IEnumerator 演出驱动()
    {
        演出驱动中 = true;
        while (演出片段.Count > 0)
        {
            var 片段 = 演出片段.Dequeue();
            if (片段 != null) while (片段.MoveNext()) yield return 片段.Current;
        }
        演出驱动中 = false;
    }

    // ===== 演出片段 / 飘字 =====
    // 战斗演出（受击/治疗/阵亡/敌方行动/结束片段、飘字、飘技能名）已抽取至 战斗面板.演出.cs（partial），
    // 本文件只保留：交互状态机（行动选择/子菜单/目标选择）与布局（布阵/信息条/卡牌创建）+ 演出队列核心。
    // 队列核心仍在此处，供 处理行动轮换 与 事件订阅 调用 入队演出(...)。
}
