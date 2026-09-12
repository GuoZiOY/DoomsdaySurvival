using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// 格子探索服务（基类）：**格子世界运行器** —— 与"格子上是什么"无关的那一半。
//   令牌 + 点格寻路（A*）→ 沿路径逐格推进（走 1 格 = 1 游戏分钟）→ 三态迷雾（未探索 / 已探索 / 可见）
//   → 把 网格数据 同步成 网格服务（渲染框架吃这个）→ 发「显示 / 地点」事件。
// 谁继承：房间探索服务（门 / 锁 / 搜索 / 遭遇）、区域探索服务（建筑入口 / 街道遭遇）。
//
// 分工纪律：基类只知道"格子、令牌、路径、视野、时间"；
//           派生类填"格子上有什么、点它做什么、走到它上面发生什么"。
//   · 派生可拦：有迷雾 / 视野边长 / 点格拦截 / 抵达后检查 / 抵达格 / 记忆键 / 清战局 / 发布显示事件
//   · 派生专有内容（门锁、搜索、钥匙、遭遇、楼层）**不下沉**到这里
// ============================================================
public abstract class 格子探索服务
{
    // —— 走一格的游戏时间（所有探索层一致）——
    public const int 移动游戏分钟 = 1;
    // —— 走一格的现实节奏（表现层：令牌速度 = 1 格 / 每格现实秒）——
    public const float 每格现实秒 = 0.30f;

    protected readonly EventBus 事件;
    protected readonly DataService 数据;
    protected readonly PlayerService 玩家服务;

    protected readonly Dictionary<网格实体, 物品堆叠> 框映射 = new Dictionary<网格实体, 物品堆叠>();
    protected readonly Dictionary<string, (int 宽, int 高)> 形状表 = new Dictionary<string, (int, int)>();

    protected readonly 视野规则 规则;                    // 角色 + 时段 → 视野边长 / 有没有记忆（数据：视野.json）
    public 时段 当前时段 { get; protected set; } = 时段.白天;
    public bool 有记忆 => 规则.有记忆(当前时段);           // 白天才有记忆（淡阴影），夜晚全黑
    public float 阴影压暗 => 规则.阴影压暗;               // 阴影区（白天记忆 / 夜晚弱视野）压暗程度

    public 网格数据 当前世界 { get; protected set; }
    public 网格服务 网格 { get; protected set; }
    // 当前这个世界实际用的种子（派生类负责算：房间 = 派生(出行种子, 模板标识)）
    public int 当前种子 { get; protected set; }
    public string 提示 { get; protected set; } = "";
    public List<(int 列, int 行)> 当前路径 { get; protected set; } = new List<(int 列, int 行)>();

    protected readonly HashSet<int> 已探索 = new HashSet<int>();                       // 迷雾记忆：进过视野的格（**当前这个世界**的）
    // 迷雾记忆按世界存：走回头路 / 再进同一间房，看过的格还在（不清成全黑）。
    // 键由 记忆键() 决定；与容器战局数据同一生命期：**回安全屋才清**（见 回安全屋清战局）。
    protected readonly Dictionary<string, HashSet<int>> 视野记忆 = new Dictionary<string, HashSet<int>>();
    protected HashSet<int> 当前可见 = new HashSet<int>();
    protected (int 列, int 行) 遭遇前格;
    protected 网格实体 遭遇目标;
    protected string 最近尸体容器 = "";
    private string 上次地点 = "";   // HUD 地点位去重（内容变了才发事件）

    protected int 出行种子;              // 本趟出行的基准种子（从外面进来的那一次给的）
    protected string 出行起点标识 = "";   // 本趟的起点（只有它"从外面进来"，房间层还只有它外墙上有大门）

    public bool 探索中 => 当前世界 != null;
    public bool 遭遇中 { get; protected set; }
    public int 玩家列 => 当前世界?.玩家列() ?? 0;
    public int 玩家行 => 当前世界?.玩家行() ?? 0;

    protected 玩家档案 档案 => 玩家服务?.档案;

    protected 格子探索服务(EventBus 事件, DataService 数据, PlayerService 玩家服务)
    {
        this.事件 = 事件;
        this.数据 = 数据;
        this.玩家服务 = 玩家服务;
        规则 = 视野规则.从数据(数据?.视野);   // 视野：角色 + 时段（数据：Data/视野.json）
        if (事件 != null)
        {
            事件.订阅<时间变化事件>(时间变化响应);            // 站着不动跨过 6:00 / 18:00 也要立刻切换视野与记忆
            事件.订阅<打开营地事件>(_ => 回安全屋清战局());   // 回到安全屋 = 这一趟结束（临时数据作废的**唯一**时机）
        }
    }

    // ================= 派生类可以拦的钩子 =================

    protected virtual bool 有迷雾 => true;                        // 两片格子层（房间 / 区域）都用雾；要"全亮"的层才覆写成 false
    protected virtual int 视野边长(时段 时段) => 规则.核心边长(时段);
    protected virtual bool 点格拦截(int 列, int 行) => false;      // true = 这次点击被派生消费（区域：点建筑 → 进楼）
    protected virtual bool 抵达后检查() => false;                  // 走完一步之后（房间：遭遇）
    protected virtual bool 抵达格() => false;                      // 踩到的那一格上有什么（房间：能过的门 → 转移）
    protected virtual void 刷新视野之后() { }                       // 房间：记"在 ≥3 格看清过的敌人"（先手优势）
    protected virtual string 记忆键(string 标识, int 种子) => $"{种子}#{标识}";
    protected virtual void 清战局() { }                            // 派生：搜索战局 / 门锁态 一起作废
    protected virtual void 发布显示事件(string 信息条, int 列, int 行) { }   // 派生：发自己的"显示"事件（房间显示事件）
    protected virtual void 离开前() { }                            // 派生：离开前存状态 / 清自己那几样

    // ================= 回安全屋 / 记忆 =================

    // 回安全屋：这一趟出行的临时数据整体作废（下一趟出去是全新的一批物资）。
    // **进房间 / 离开房间都不清**：一趟里搜到的进度、看过的路要留到回安全屋为止。
    protected virtual void 回安全屋清战局()
    {
        视野记忆.Clear();
        清战局();
    }

    // 把"当前这个世界看过的格"存回记忆表（换世界 / 离开前调用）
    protected void 存视野记忆()
    {
        if (当前世界 == null) return;
        视野记忆[记忆键(当前世界.模板标识, 当前种子)] = new HashSet<int>(已探索);
    }

    // 离开时清空"世界态"（结构本来就不存档，随用随生成）
    protected void 清空世界态()
    {
        当前世界 = null;
        网格 = null;
        框映射.Clear();
        形状表.Clear();
        已探索.Clear();
        当前可见.Clear();
        当前路径.Clear();
        遭遇中 = false;
        遭遇目标 = null;
        最近尸体容器 = "";
        上次地点 = "";
    }

    // ================= 迷雾 / 视野 =================

    // 重算当前视野，并把可见格记入"已探索"记忆
    public void 刷新视野()
    {
        if (当前世界 == null) return;
        // 边长与"有没有记忆"由角色与时段决定（白天 7 / 夜晚 3 之类，永远是单数）
        当前时段 = 规则.判时段(档案?.游戏分钟数 ?? 480f);
        当前世界.视野边长 = 视野边长(当前时段);
        当前可见 = 网格视野.可见集(当前世界, 玩家列, 玩家行, 当前世界.视野边长);
        foreach (int 码 in 当前可见)
            已探索.Add(码);
        刷新视野之后();
    }

    // 三态：0 未探索 / 1 已探索（此刻不可见） / 2 可见
    public byte 迷雾态(int 列, int 行)
    {
        int 码 = 网格数据.编码(列, 行);
        if (当前可见.Contains(码)) return 网格视野.可见;
        if (已探索.Contains(码)) return 网格视野.已探索;
        return 网格视野.未探索;
    }

    public bool 可见(int 列, int 行) => 当前可见.Contains(网格数据.编码(列, 行));
    public bool 已探索过(int 列, int 行) => 已探索.Contains(网格数据.编码(列, 行));

    // 每格的显示档（渲染面板与图层都照它画）：
    //   亮       = 核心方形内（含敌人）
    //   暗记忆   = 白天：已探索（墙/容器/尸体 可辨，敌人不画）
    //   暗中轮廓 = 夜晚：弱视野圈（只看得见建筑/墙）
    //   全黑     = 什么都不画
    public 视野档 档(int 列, int 行)
    {
        if (当前世界 == null) return 视野档.全黑;
        if (!有迷雾) return 视野档.亮;                    // 不做雾的格子层：一律全亮
        if (当前可见.Contains(网格数据.编码(列, 行))) return 视野档.亮;
        if (当前时段 == 时段.夜晚)
            return 规则.在弱视野圈(列, 行, 玩家列, 玩家行, 当前世界.视野边长 / 2) ? 视野档.暗中轮廓 : 视野档.全黑;
        return 已探索过(列, 行) ? 视野档.暗记忆 : 视野档.全黑;
    }

    // ================= 寻路 / 移动 =================

    // 算路径：占格实体 → 走到其相邻格；空格 → 直接走；不可达 → null
    public List<(int 列, int 行)> 算路径(int 列, int 行)
    {
        if (当前世界 == null) return null;
        var 起 = (列: 玩家列, 行: 玩家行);
        if (列 == 起.列 && 行 == 起.行) return new List<(int 列, int 行)> { 起 };

        var 目标实体 = 当前世界.占位(列, 行);
        if (目标实体 != null)
            return 网格寻路.寻路到相邻(当前世界, 起, 目标实体);

        // 迷雾只影响"看不看得见"，不影响"走不走得过去"：空格一律按 A* 走（墙/容器自然挡路 → 返回 null 并提示）
        return 网格寻路.寻路(当前世界, 起, (列, 行));
    }

    // 点格：算路径 → **当场换路径**（面板立刻改画、令牌立刻拐弯）→ 图层沿路径逐格推进
    public bool 点格(int 列, int 行)
    {
        if (当前世界 == null || 遭遇中) return false;
        if (点格拦截(列, 行)) return true;          // 派生先看一眼（区域：点建筑 = 进楼，不是走过去）
        var 路 = 算路径(列, 行);
        if (路 == null || 路.Count == 0)
        {
            播报("那边过不去。");
            return false;
        }
        // 点自己脚下这格：站着 = 原地（单点路径，面板只高亮这一格）
        // 正在走 = **不打断**：路径照旧走完剩下的（半路把路径缩成单点会掐断脚下这半步，令牌得往回跳）
        if (列 == 玩家列 && 行 == 玩家行)
        {
            if (当前路径.Count > 1) return true;
            当前路径 = 路;
            发布显示();
            return true;
        }
        // 新路径一律从"当前逻辑格"起算；走到一半又点别处就当场换（视觉连续性由 探索图层 保证）
        当前路径 = 路;
        发布显示();
        return true;
    }

    // 方向键：走一格（相邻可站格）；正在走时同样当场改向
    public bool 方向移动(int 偏列, int 偏行)
    {
        if (当前世界 == null || 遭遇中) return false;
        int 列 = 玩家列 + 偏列, 行 = 玩家行 + 偏行;
        if (!当前世界.可通行(列, 行)) { 播报("那边过不去。"); return false; }
        当前路径 = new List<(int 列, int 行)> { (玩家列, 玩家行), (列, 行) };
        发布显示();
        return true;
    }

    // 沿路径走一步：推进游戏时间 → 刷新视野 → 派生检查（遭遇 / 踩到门之类）；返回 true = 还有下一步
    // 由 探索图层 在"令牌真的踩到下一格"的那一刻调用（逻辑格与视觉到位同一时刻换，不会打架）
    public bool 推进一步()
    {
        if (当前世界 == null || 当前路径.Count <= 1)
        {
            当前路径.Clear();
            return false;
        }
        遭遇前格 = (玩家列, 玩家行);
        当前路径.RemoveAt(0);
        var 步 = 当前路径[0];
        当前世界.移动玩家(步.列, 步.行);
        推进游戏分钟(移动游戏分钟);
        刷新视野();
        发布显示();
        if (抵达后检查()) return false;   // 派生：遭遇了 → 停在这里
        if (抵达格()) return false;       // 派生：踩在"能过的门"上 → 直接转移
        if (当前路径.Count <= 1)
        {
            当前路径.Clear();
            发布显示();
            return false;
        }
        return true;
    }

    // 够得着：玩家是否**正交相邻**于该实体占的任意一格（搜索 / 开门 的前置条件——隔着屋子不能操作）
    public bool 够得着(网格实体 实体)
    {
        if (当前世界 == null || 实体 == null) return false;
        for (int r = 实体.行; r < 实体.行 + 实体.高; r++)
            for (int c = 实体.列; c < 实体.列 + 实体.宽; c++)
                if (Math.Abs(c - 玩家列) + Math.Abs(r - 玩家行) == 1) return true;
        return false;
    }

    // ================= 网格同步（渲染用） =================

    // 把 网格数据 同步成一个 网格服务：实体全部当"物品堆叠"承载（基类网格框架吃这个）
    protected void 同步网格()
    {
        if (当前世界 == null) return;
        if (网格 == null)
        {
            网格 = new 网格服务();
            网格.形状解析 = 标识 => 形状表.TryGetValue(标识, out var 形) ? new 物品形状(形.宽, 形.高) : new 物品形状(1, 1);
        }
        网格.网格列 = 当前世界.列;
        网格.网格行 = 当前世界.行;

        var 新列表 = new List<物品堆叠>();
        var 存活 = new HashSet<网格实体>();
        foreach (var e in 当前世界.实体)
        {
            if (e == null) continue;
            存活.Add(e);
            形状表[e.标识] = (e.宽, e.高);
            if (!框映射.TryGetValue(e, out var 堆叠))
            {
                堆叠 = new 物品堆叠(e.标识, 1);
                框映射[e] = 堆叠;
            }
            堆叠.列 = e.列;
            堆叠.行 = e.行;
            新列表.Add(堆叠);
        }
        // 清理已消失实体的映射（敌人被打死等）
        var 待删 = new List<网格实体>();
        foreach (var kv in 框映射)
            if (!存活.Contains(kv.Key)) 待删.Add(kv.Key);
        foreach (var e in 待删)
        {
            框映射.Remove(e);
            if (网格 != null) 网格.网格物品.RemoveAll(堆叠 => 堆叠 != null && 堆叠.标识 == e.标识);
        }
        网格.网格物品 = 新列表;
    }

    // 只更新玩家令牌位置（走一格的最省刷新路径：基类增量刷新只挪这一个框）
    protected void 同步玩家格()
    {
        if (当前世界 == null || 网格 == null) return;
        var 玩家 = 当前世界.玩家();
        if (玩家 == null) return;
        if (!框映射.TryGetValue(玩家, out var 堆叠)) { 同步网格(); return; }
        堆叠.列 = 玩家.列;
        堆叠.行 = 玩家.行;
    }

    // ================= 信息 / 发布 =================

    public virtual string 信息条() => 地点文本();

    public static string 危险度文本(int 危险度)
    {
        string 点 = new string('●', Mathf.Clamp(危险度, 1, 5)) + new string('○', Mathf.Max(0, 5 - 危险度));
        return $"危险 {点}";
    }

    protected string 时间文本()
    {
        if (档案 == null) return "";
        int 总分钟 = (int)档案.游戏分钟数;
        int 天 = 总分钟 / 1440 + 1;
        int 时 = 总分钟 % 1440 / 60;
        int 分 = 总分钟 % 60;
        return $"第{天}天 {时:00}:{分:00}";
    }

    // 播报：一律走日志（探索类）
    protected void 播报(string 文本)
    {
        提示 = 文本;
        事件?.发布(new 日志事件(日志类型.探索, 文本));
    }

    public virtual void 发布显示()
    {
        同步玩家格();
        同步HUD地点();
        发布显示事件(信息条(), 玩家列, 玩家行);
    }

    // HUD 的「地点」位（内容变了才发，避免每走一步都刷 HUD）
    private void 同步HUD地点()
    {
        string 文本 = 地点文本();
        if (文本 == 上次地点) return;
        上次地点 = 文本;
        事件?.发布(new 地图位置事件(地图模式.大地图, 文本));
    }

    // HUD「地点」位：**只显示当前地点名**（房间层 = 房间名）——危险度/进度不进 HUD
    public virtual string 地点文本() => 当前世界?.名称 ?? "";

    // 时间变化（跨过 6:00 / 18:00）：视野边长与"记忆"立刻切换，不用等玩家走一步
    private void 时间变化响应(时间变化事件 e)
    {
        if (当前世界 == null) return;
        时段 之前 = 当前时段;
        刷新视野();
        if (之前 != 当前时段)
        {
            播报(当前时段 == 时段.白天 ? "天亮了。" : "天黑了——看不见的地方就是真的看不见。");
            发布显示();
        }
    }

    // 推进游戏时间：走世界时间管理器（统一时钟，禁自己加时间）
    protected void 推进游戏分钟(int 分钟)
    {
        var 时钟 = ServiceRegistry.Get<世界时间管理器>();
        if (时钟 == null) return;
        时钟.推进(分钟 * 60f / 世界时间管理器.每现实分钟游戏分钟);
    }

    // ================= 离开 =================

    // 派生类离开时调：存记忆 → 清世界态 → 自己那几样
    protected void 离开世界()
    {
        if (当前世界 == null) return;
        存视野记忆();
        离开前();
        清空世界态();
    }
}
