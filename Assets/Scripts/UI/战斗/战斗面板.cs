using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 战斗面板：主视窗下的内容面板。文字血条 + 点卡牌选目标 + 文字状态 + 复用主日志栏(D20-B)。
// 交互状态机: 行动选择 → 技能/道具子菜单 → 目标选择(点卡牌) → 执行 → 结算。
// [攻击]单独按钮 = 内置普攻技能(倍率1.0物理无消耗)。场景手动搭结构，代码克隆卡牌/子菜单项。
public sealed class 战斗面板 : 面板基类
{
    // —— Inspector 拖入 ——
    [SerializeField] private TMP_Text 信息条;             // "回合 N: ▶当前 下一步 排队"（富文本着色，信息条+行动序列合一）
    [SerializeField] private RectTransform 敌方区;        // 敌方卡牌容器
    [SerializeField] private RectTransform 我方区;        // 我方卡牌容器
    [SerializeField] private GameObject 卡牌模板;         // 战斗单位卡牌模板（敌我共用）
    [SerializeField] private Button 攻击按钮, 技能按钮, 道具按钮, 逃跑按钮;
    [SerializeField] private RectTransform 主行动区;      // [攻击][技能][道具][逃跑]
    [SerializeField] private RectTransform 子菜单区;      // 技能/道具列表（初始隐藏）
    [SerializeField] private RectTransform 列表容器;      // 子菜单项容器
    [SerializeField] private GameObject 技能行模板;       // 技能子菜单行模板（挂 技能行：技能名/消耗/数值）
    [SerializeField] private GameObject 道具行模板;       // 道具子菜单行模板（挂 道具行：品质/名字/数量）
    [SerializeField] private GameObject 结算覆盖层;       // 结算界面（初始隐藏）
    [SerializeField] private TMP_Text 结算文字;
    [SerializeField] private Button 继续按钮;

    private BattleService 战斗;
    private int 当前回合;
    private string 待选行动;   // null=行动选择；"普攻"/技能标识/道具标识 = 正在选目标
    private List<战斗单位> 当前可选集合;   // 当前选目标的可点集合（点击卡牌时校验）
    private Coroutine 敌方节奏协程;
    [SerializeField] private float 敌方行动停顿 = 0.8f;   // 敌方回合停顿秒数（让玩家看清敌方行动）
    private readonly Dictionary<战斗单位, 战斗单位卡牌> 卡牌表 = new Dictionary<战斗单位, 战斗单位卡牌>();

    void Awake()
    {
        战斗 = ServiceRegistry.Get<BattleService>();
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<战斗开始事件>(渲染布阵);
        事件.订阅<行动轮换事件>(e => 处理行动轮换(e));
        事件.订阅<伤害事件>(e => 刷新卡牌(e.目标));
        事件.订阅<治疗事件>(e => 刷新卡牌(e.目标));
        事件.订阅<状态变化事件>(e => 刷新卡牌(e.单位));
        事件.订阅<目标变化事件>(e => { 移除死亡卡牌(e.单位); 刷新信息条(); });
        事件.订阅<回合开始事件>(e => { 当前回合 = e.回合数; 刷新信息条(); });
        事件.订阅<战斗结束事件>(显示结算);

        攻击按钮.onClick.AddListener(() => 开始选目标("普攻"));
        技能按钮.onClick.AddListener(() => 显示子菜单(true));
        道具按钮.onClick.AddListener(() => 显示子菜单(false));
        逃跑按钮.onClick.AddListener(() => 战斗.逃跑());
        继续按钮.onClick.AddListener(() => { 战斗.返回(); });
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
        清空(敌方区); 清空(我方区); 卡牌表.Clear();
        待选行动 = null;
        foreach (var 单位 in e.敌方) 创建卡牌(单位, 敌方区);
        foreach (var 单位 in e.我方) 创建卡牌(单位, 我方区);
        当前回合 = 0;
        刷新信息条();
    }

    // 信息条 = 回合数 + 行动序（富文本一行，旋转式：当前永远在队首，已行动排到队尾划掉）
    // 当前=▶金色 · 下一步=钢蓝 · 排队=正文色 · 已行动=队尾删除线暗
    private void 刷新信息条()
    {
        if (信息条 == null) return;
        if (待选行动 != null) { 信息条.text = "选择目标"; return; }
        var 序 = 战斗.本回合行动显示();
        int 当前 = 序.IndexOf(战斗.当前行动单位);
        if (当前 < 0 && 序.Count > 0) 当前 = 0;   // 尚未开始行动时，第一个视为当前
        var 段 = new List<string>();
        for (int i = 0; i < 序.Count; i++)
        {
            int 原 = (当前 + i) % 序.Count;        // 旋转：从当前开始，已行动的绕到末尾
            var 单位 = 序[原];
            string 名 = 单位.名称 + (单位.无法行动 ? "(晕)" : "");
            string 一段;
            if (i == 0) 一段 = $"<color={游戏主题.金色色值}>>{名}</color>";            // 当前行动
            else if (原 < 当前) 一段 = $"<color=#5a5750><s>{名}</s></color>";         // 已行动（队尾删除线）
            else if (i == 1) 一段 = $"<color=#6ba8d1>{名}</color>";                   // 下一步（准备）
            else 一段 = $"<color=#d8d3c8>{名}</color>";                               // 排队（正文色）
            段.Add(一段);
        }
        信息条.text = $"回合 {当前回合}: " + string.Join("  ", 段);
    }

    private void 创建卡牌(战斗单位 单位, RectTransform 父)
    {
        if (卡牌模板 == null) return;
        var 物体 = Instantiate(卡牌模板, 父, false);
        物体.SetActive(true);
        var 卡牌 = 物体.GetComponent<战斗单位卡牌>();
        if (卡牌 == null) return;
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

    // 先行动画（玩家卡牌跳一下），动画结束再结算行动逻辑（先动画后判定）
    private System.Collections.IEnumerator 行动结算(string 行动, 战斗单位 目标)
    {
        if (卡牌表.TryGetValue(战斗.玩家, out var 卡牌)) 卡牌.行动跳跃();
        yield return new WaitForSeconds(战斗单位卡牌.行动跳跃时长);
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
    private void 处理行动轮换(行动轮换事件 e)
    {
        主行动区.gameObject.SetActive(e.是否玩家回合);
        子菜单区?.gameObject.SetActive(false);
        待选行动 = null;
        当前可选集合 = null;
        重置卡牌可选();
        foreach (var kv in 卡牌表) kv.Value.刷新();
        刷新信息条();
        // 敌方回合：停顿让玩家看清轮到谁/当前行动，再触发敌人行动
        if (!e.是否玩家回合 && e.行动单位 != null)
        {
            if (gameObject.activeInHierarchy)
            {
                if (敌方节奏协程 != null) StopCoroutine(敌方节奏协程);
                敌方节奏协程 = StartCoroutine(敌方行动节奏(e.行动单位));
            }
            else 战斗.继续();   // 面板未激活（探索遭遇等）：无停顿直接推进，避免卡住
        }
    }

    // 敌方回合：先行动画（敌人卡牌跳），停顿后结算敌人行动
    private System.Collections.IEnumerator 敌方行动节奏(战斗单位 敌人)
    {
        if (卡牌表.TryGetValue(敌人, out var 卡牌)) 卡牌.行动跳跃();
        yield return new WaitForSeconds(敌方行动停顿);
        战斗.继续();
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

    // 技能数值文本：攻击=伤害倍率；治疗=恢复量；增益/减益/控制=描述
    private static string 技能数值文本(技能数据 技能)
    {
        switch (技能.类别枚举)
        {
            case 技能类别.攻击: return $"伤害 ×{技能.数值}";
            case 技能类别.治疗: return $"恢复 {技能.数值}";
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
        // 减负：目标为自己 的技能/道具 免选，直接对玩家执行
        if (行动 != "普攻" && 行动的期望目标(行动) == 目标类型.自己)
        {
            if (战斗.玩家.存活 && 当前可选集合.Contains(战斗.玩家)) 点击卡牌(战斗.玩家);
            else 待选行动 = null;
            return;   // 点击卡牌 内部已 待选行动=null + 回主行动
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
        var 源 = 类型 == 目标类型.敌方单体 || 类型 == 目标类型.敌方全体 ? 战斗.敌方 : 战斗.我方;
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

    private void 移除死亡卡牌(战斗单位 单位)
    {
        if (!卡牌表.TryGetValue(单位, out var 卡牌)) return;
        卡牌.gameObject.SetActive(false);
        卡牌表.Remove(单位);
    }

    // ===== 结算 =====
    private void 显示结算(战斗结束事件 e)
    {
        主行动区.gameObject.SetActive(false);
        子菜单区?.gameObject.SetActive(false);
        结算覆盖层?.SetActive(true);
        结算文字.text = e.结算文本;
    }
}
