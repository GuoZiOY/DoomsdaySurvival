using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 战斗沙盒面板：即时制战斗 外壳（面板基类——按钮/结算/目标选择/时间推进），棋盘渲染 由 组合 的 战斗棋盘网格（网格面板基类 子类）承担。
// 组合架构与"安全屋面板 + 家具网格面板"一致：外壳 = 面板语义（面板管理器 显示/回退），网格 = 棋盘语义（底座/分隔线/实体 增量刷新）。
// 逻辑：订阅 战斗开始事件 → 布阵（棋盘网格.布阵 + 技能/道具栏 重建）；Update 每帧 推进战斗 + 同步 单位 到 棋盘网格；
//  目标选择：点技能/道具 → 高亮可选目标 → 点 棋子/格 释放；Esc/点空白 取消。
public sealed class 战斗沙盒面板 : 面板基类
{
    // —— Inspector 拖入 ——
    [SerializeField] private 战斗轨道 棋盘;            // 节点式轨道组件（场景 轨道 子物体 挂 战斗轨道，本字段拖入）
    [SerializeField] private Button 运动按钮;            // 运动模式循环（前进/等待/后退）
    [SerializeField] private Button 行动按钮;            // 行动模式切换（攻击/防御）
    [SerializeField] private Button 切武器按钮;
    [SerializeField] private Button 逃跑按钮;
    [SerializeField] private 技能槽[] 技能槽位;         // 固定 6 技能槽（场景手动搭建，Inspector 拖入）
    [SerializeField] private 战斗道具网格 弹挂网格;       // 弹挂 容器网格（场景侧栏，战斗内只读+单击使用）
    [SerializeField] private 战斗道具网格 腰封网格;       // 腰封 容器网格（场景侧栏，战斗内只读+单击使用）
    [SerializeField] private Button 继续按钮;            // 战斗结束激活，点击结算返回

    // —— 动态渲染状态 ——
    private BattleService 战斗;
    private EventBus 事件;
    private string 待选技能;    // 目标选择中："技能标识" 或 "道具:标识"；null = 未选择
    private readonly List<战斗单位> 当前可选目标 = new List<战斗单位>();

    void Awake()
    {
        战斗 = ServiceRegistry.Get<BattleService>();
        事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<战斗开始事件>(_ => 布阵());
        事件.订阅<战斗结束事件>(e => 显示结算(e));
        事件.订阅<伤害事件>(处理伤害反馈);
        事件.订阅<治疗事件>(处理治疗反馈);
        运动按钮?.onClick.AddListener(() => { 战斗?.切换运动模式(); });
        行动按钮?.onClick.AddListener(() => { 战斗?.切换行动模式(); });
        切武器按钮?.onClick.AddListener(() => { 战斗?.切换武器(); });
        逃跑按钮?.onClick.AddListener(() => { 战斗?.逃跑(); });
        继续按钮?.onClick.AddListener(() => { 战斗?.返回(); });
        if (继续按钮 != null) 继续按钮.gameObject.SetActive(false);
    }

    // 面板由 面板管理器 显示（打开战斗事件）；布阵走 战斗开始事件（棋盘已就绪后触发）
    protected override void 刷新(object 上下文)
    {
        if (战斗 != null && 战斗.战斗中) 布阵();   // 兜底：面板显示时战斗已在进行
    }

    public override bool 回退()
    {
        if (待选技能 != null) { 取消选择(); return true; }
        return false;
    }

    // ===== 布阵 =====

    private void 布阵()
    {
        if (战斗 == null || !战斗.战斗中) return;
        if (继续按钮 != null) 继续按钮.gameObject.SetActive(false);
        待选技能 = null;
        当前可选目标.Clear();
        if (棋盘 != null) 棋盘.布阵(战斗.棋盘宽);   // 节点式轨道：节点数 = 棋盘宽
        绑定道具容器();
        刷新技能槽();
        刷新模式按钮();
    }

    // 弹挂/腰封 容器网格：布阵时 注入 容器视图（穿戴容器视图 → 网格服务），空容器也显示（容器物品 null 自动初始化）；未装备才隐藏
    private void 绑定道具容器()
    {
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        foreach (var (槽位, 网格) in new (string, 战斗道具网格)[] { ("弹挂", 弹挂网格), ("腰封", 腰封网格) })
        {
            if (网格 == null) continue;
            var 记录 = ServiceRegistry.Get<PlayerService>()?.档案?.装备.Find(e => e.槽位 == 槽位);
            if (记录 == null || 记录.容器列 <= 0) { 网格.gameObject.SetActive(false); continue; }   // 未装备 → 隐藏
            网格.数据源 = 容器服务?.穿戴容器视图(记录);   // 装备记录 → 网格服务（容器物品 null 自动初始化空容器）
            网格.gameObject.SetActive(true);
            网格.强制重建();
        }
    }

    // 技能槽：固定 6 槽（场景手动搭建的 技能槽 组件数组），绑定 战斗技能槽 数据
    private void 刷新技能槽()
    {
        if (技能槽位 == null || 战斗?.玩家 == null) return;
        var 数据 = ServiceRegistry.Get<DataService>();
        var 槽 = ServiceRegistry.Get<PlayerService>()?.档案?.战斗技能槽;
        for (int i = 0; i < 技能槽位.Length; i++)
        {
            var 槽组件 = 技能槽位[i];
            if (槽组件 == null) continue;
            string 标识 = 槽 != null && i < 槽.Count ? 槽[i] : null;
            槽组件.绑定(标识, 数据);
        }
    }

    // 技能槽 点击 转发：开始 目标选择
    public void 点击技能槽(string 技能标识)
    {
        if (string.IsNullOrEmpty(技能标识)) return;
        // 位移技（推进/后撤/冲撞）：无目标 自我 瞬发，点击 即 释放（免 目标选择）
        var 数据 = ServiceRegistry.Get<DataService>();
        if (数据 != null && 数据.技能.TryGetValue(技能标识, out var 技能) && !string.IsNullOrEmpty(技能.位移))
        {
            if (战斗 != null && 战斗.玩家 != null) 战斗.玩家主动技能(技能标识, 战斗.玩家);
            return;
        }
        开始选技能(技能标识);
    }

    // ===== 目标选择 =====

    private void 开始选技能(string 标识)
    {
        if (战斗 == null) return;
        if (待选技能 == 标识) { 取消选择(); return; }
        待选技能 = 标识;
        当前可选目标.Clear();
        foreach (var 目标 in 战斗.技能可选目标(标识))
            if (目标.存活) 当前可选目标.Add(目标);
    }

    private void 开始选道具(string 标识)
    {
        if (战斗 == null) return;
        if (待选技能 == "道具:" + 标识) { 取消选择(); return; }
        待选技能 = "道具:" + 标识;
        当前可选目标.Clear();
        foreach (var 目标 in 战斗.道具可选目标(标识))
            if (目标.存活) 当前可选目标.Add(目标);
    }

    private void 取消选择()
    {
        待选技能 = null;
        当前可选目标.Clear();
    }

    // 棋盘网格 转发：点击 棋子 → 释放 待选技能/道具
    public void 点击单位(战斗单位 单位)
    {
        if (待选技能 == null || 战斗 == null || 单位 == null) return;
        if (!当前可选目标.Contains(单位)) return;
        if (待选技能.StartsWith("道具:"))
        {
            string 标识 = 待选技能.Substring("道具:".Length);
            战斗.玩家主动道具(标识, 单位);
        }
        else
        {
            战斗.玩家主动技能(待选技能, 单位);
        }
        取消选择();
    }

    // 战斗道具网格 转发：点击 弹挂/腰封 里 物品 → 进入 目标选择
    public void 点击道具(string 物品标识) => 开始选道具(物品标识);

    // 棋盘网格 转发：点击 空白格 → 取消
    public void 点击空白() => 取消选择();

    // 棋盘网格 查询：该单位 是否为 当前可选目标（更新实体框 高亮 用）
    public bool 是可选目标(战斗单位 单位) => 待选技能 != null && 单位 != null && 当前可选目标.Contains(单位);

    // ===== 战斗 反馈：伤害/治疗 → 飘字 + 相机抖动（配色 在此 集中） =====
    private static readonly Color 飘字近战 = new Color(1f, 0.95f, 0.9f, 1f);
    private static readonly Color 飘字远程 = new Color(0.45f, 0.62f, 1f, 1f);
    private static readonly Color 飘字真实 = new Color(0.9f, 0.5f, 0.95f, 1f);
    private static readonly Color 飘字暴击 = new Color(1f, 0.82f, 0.25f, 1f);
    private static readonly Color 飘字闪避 = new Color(0.75f, 0.78f, 0.82f, 1f);
    private static readonly Color 飘字治疗 = new Color(0.35f, 0.85f, 0.45f, 1f);

    private void 处理伤害反馈(伤害事件 e)
    {
        if (棋盘 == null || e.目标 == null) return;
        if (e.闪避) { 棋盘.显示飘字(e.目标, "MISS", 飘字闪避, 16f); return; }
        if (e.数值 <= 0) return;
        Color 色 = e.暴击 ? 飘字暴击
            : e.类型 == 伤害类型.远程 ? 飘字远程
            : e.类型 == 伤害类型.真实 ? 飘字真实 : 飘字近战;
        棋盘.显示飘字(e.目标, e.数值.ToString(), 色, e.暴击 ? 26f : 20f);
        棋盘.受击抖动(e.暴击 ? 0.45f : 0.14f);   // 暴击 抖 更 强
    }

    private void 处理治疗反馈(治疗事件 e)
    {
        if (棋盘 == null || e.目标 == null || e.数值 <= 0) return;
        棋盘.显示飘字(e.目标, "+" + e.数值, 飘字治疗, 20f);
    }

    // ===== 结算 =====

    private void 显示结算(战斗结束事件 e)
    {
        if (!gameObject.activeInHierarchy) return;
        待选技能 = null;
        当前可选目标.Clear();
        if (继续按钮 != null) 继续按钮.gameObject.SetActive(true);   // 战斗结束 → 激活继续，点击返回
    }

    // ===== 每帧驱动 + 渲染 =====

    void Update()
    {
        if (战斗 == null || !战斗.战斗中) return;
        战斗.推进战斗(Time.deltaTime);
        if (棋盘 != null) 棋盘.同步单位(战斗.全部单位());
        刷新模式按钮();
        // 技能冷却（固定 6 槽 轮询）
        if (技能槽位 != null && 战斗?.玩家 != null)
            foreach (var 槽组件 in 技能槽位)
            {
                if (槽组件 == null) continue;
                if (槽组件.当前技能标识 != null)
                {
                    槽组件.刷新冷却(战斗.玩家.冷却剩余(槽组件.当前技能标识));
                    槽组件.刷新蓄力(战斗.蓄力中技能 == 槽组件.当前技能标识);   // 预约/蓄力 中 标记
                }
            }
        // 弹挂/腰封 网格：物品使用 扣量 由 背包变化事件 自动刷新（物品网格面板 机制），无需此处轮询
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) 取消选择();
    }

    private void 刷新模式按钮()
    {
        if (运动按钮 != null)
        {
            var 文本 = 运动按钮.GetComponentInChildren<TMP_Text>();
            if (文本 != null) 文本.text = $"运动：{运动模式名(战斗?.玩家?.运动模式 ?? 0)}";
        }
        if (行动按钮 != null)
        {
            var 文本 = 行动按钮.GetComponentInChildren<TMP_Text>();
            if (文本 != null) 文本.text = $"行动：{行动模式名(战斗?.玩家?.行动模式 ?? 0)}";
        }
    }

    private static string 运动模式名(int 模式) => 模式 switch
    {
        0 => "前进",
        1 => "等待",
        _ => "后退",
    };

    private static string 行动模式名(int 模式) => 模式 == 0 ? "攻击" : "防御";
}
