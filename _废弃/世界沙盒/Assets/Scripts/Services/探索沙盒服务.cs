using System.Collections.Generic;
using UnityEngine;

// 探索沙盒服务：世界沙盒第 1 刀的核心逻辑（1 个地区街道网格 + 多栋楼的逐层房间网格）。
// 职责：进入地区（种子展开结构）→ 逐格移动（时间片 + 行动点）→ 迷雾/视野 → 搜刮容器 → 遭遇战 → 楼梯切层 → 撤离。
//
// 分工与约定（评审定案，改前先读）：
//   · 结构 vs 状态：地形/对象/实例 id 由种子确定性展开（片区数据）；迷雾/当前格/警觉 是每局状态，撤离即弃。
//   · 时钟：玩家每动 1 格 = 2 游戏分钟（时间片）。探索期间挂机驱动暂停（时间由时间片统一推进），
//           推进一律经 世界时间管理器.推进（不直接写 游戏分钟数），整点结算/生长/净化 照常走。
//   · 容器：一律按【实例 id】注册到 搜索服务——同一"衣柜"在一栋楼里有多个实例，按定义标识注册会串物资。
//   · 撤离：只回安全屋，不调 地图服务.返回营地（那条会直接睡觉回满状态，会抹平探索张力）。
public sealed class 探索沙盒服务
{
    private readonly EventBus 事件;
    private readonly DataService 数据;
    private readonly PlayerService 玩家服务;
    private readonly BattleService 战斗;
    private readonly 搜索服务 搜索;
    private 玩家档案 档案 => 玩家服务.档案;

    // —— 规则常量（第 1 刀定版；后续可下放到 地区模板 / 职业 / 天赋）——
    public const float 每格游戏分钟 = 2f;     // 时间片：走 1 格 = 2 游戏分钟
    public const int 移动行动点 = 1;          // 走 1 格耗 1 行动点（回程成本 → 贪心惩罚）
    public const int 搜索行动点 = 10;         // 搜一处 = 10（与旧探索一致）
    public const float 搜索游戏分钟 = 5f;     // 搜一处 = 5 游戏分钟
    public const int 视野半径 = 5;            // 基础视野半径（格）；后续接 光照/夜晚

    private const byte 未探索 = 0, 已探索 = 1, 可见 = 2;

    // —— 对外状态 ——
    public 沙盒显示事件 当前显示 { get; private set; }
    public string 当前地区标识 { get; private set; }
    public bool 遭遇中 { get; private set; }               // 战斗中：屏蔽移动/交互
    public bool 本次战斗来自沙盒 { get; private set; }      // 战斗面板据此决定"是否自动弹搜索面板"
    public bool 探索中 => 当前地区标识 != null;             // 挂机时间驱动 据此暂停（时间由时间片推进）
    public int 当前楼层索引 => 楼层索引;                    // -1 = 街道（自检/调试/UI 用）
    public 沙盒层数据 当前层数据 => 当前层();                // 当前层结构（自检/调试用；不要拿去做玩法判断之外的写操作）
    public 片区数据 当前片区 => 片区;
    public (int 列, int 行) 玩家格 => (玩家列, 玩家行);      // 玩家当前格（自检/调试/UI 用；遭遇时显示快照可能滞后一格）

    // —— 内部状态 ——
    private 地区模板 当前地区;
    private 片区数据 片区;
    private 建筑数据 当前建筑;      // null = 在街道
    private int 楼层索引 = -1;       // -1 = 在街道
    private int 玩家列, 玩家行;
    private readonly Dictionary<string, byte[]> 迷雾表 = new Dictionary<string, byte[]>();
    private readonly HashSet<int> 警觉敌格 = new HashSet<int>();   // 逃跑过的敌人：下次遭遇它先动手
    private int 待结算敌列 = -1, 待结算敌行 = -1;

    public 探索沙盒服务(EventBus 事件, DataService 数据, PlayerService 玩家服务, BattleService 战斗, 搜索服务 搜索)
    {
        this.事件 = 事件; this.数据 = 数据; this.玩家服务 = 玩家服务; this.战斗 = 战斗; this.搜索 = 搜索;
        事件.订阅<战斗结束事件>(处理战斗结束);
    }

    // ===== 进入 / 撤离 =====

    public bool 进入地区(string 地区标识)
    {
        if (数据 == null || !数据.地区模板.TryGetValue(地区标识, out var 模板))
        {
            Debug.LogError($"[沙盒] 地区模板不存在: {地区标识}");
            return false;
        }
        if (探索中) 搜索?.清空战局();    // 换区：上一趟的容器实例/已搜索/尸体 一并作废（同一次撤离语义）
        当前地区 = 模板;
        当前地区标识 = 地区标识;
        long 世界 = 取世界种子();
        var 片区结果 = 区域生成器.生成(世界, 模板, 找建筑模板, 找地图类型);
        if (片区结果?.街道 == null) { Debug.LogError($"[沙盒] 地区生成失败: {地区标识}"); return false; }
        片区 = 片区结果;
        注册容器实例();

        迷雾表.Clear(); 警觉敌格.Clear();
        当前建筑 = null; 楼层索引 = -1; 遭遇中 = false; 本次战斗来自沙盒 = false;
        玩家列 = 片区.街道.出生列; 玩家行 = 片区.街道.出生行;
        待结算敌列 = -1; 待结算敌行 = -1;

        刷新视野();
        事件.发布(new 打开世界沙盒事件(地区标识));
        事件.发布(new 日志事件(日志类型.探索, $"你踏入了{模板.名称}——风里带着灰。"));
        发布显示();
        return true;
    }

    // 撤离：只能站在出口格；清空战局态（容器/尸体/迷雾）后回安全屋（不睡觉）
    public bool 撤离()
    {
        var 层 = 当前层();
        if (层 == null) return false;
        if (当前格()?.地形 != 沙盒地形.出口 || 当前建筑 != null)
        {
            提示("要回到街上的撤离点才能离开这片区域。");
            return false;
        }
        string 名 = 当前地区?.名称 ?? "这片区域";
        搜索?.清空战局();     // raid 结束：容器内容/已搜索标记/尸体 全部作废（结构仍由种子重建）
        当前地区 = null; 片区 = null; 当前建筑 = null; 楼层索引 = -1; 当前地区标识 = null;
        迷雾表.Clear(); 警觉敌格.Clear(); 遭遇中 = false;
        当前显示 = default;
        事件.发布(new 日志事件(日志类型.探索, $"你带着这一趟的收获撤出了{名}。"));
        // 清屏事件（格 = null）：沙盒面板据此收摊，不再显示上一张地图
        事件.发布(new 沙盒显示事件("", $"你已撤出{名}。", 0, 0, null, 0, 0, false));
        事件.发布(new 打开营地事件());   // 回安全屋；【不要】走 地图服务.返回营地（会直接睡觉满状态）
        return true;
    }

    // ===== 移动 / 交互 =====

    // ===== 点击物件的动作菜单（UI 只负责把 目标动作 列出来；判定全在服务）=====

    // 这格能做什么：点击世界里的物件（门/楼梯/楼门/家具/出口/空地）→ 返回可执行的动作列表。
    // 空列表 = 太远或无可操作（UI 只给提示）。
    public List<沙盒动作项> 目标动作(int 列, int 行)
    {
        var 结果 = new List<沙盒动作项>();
        if (遭遇中) return 结果;
        var 层 = 当前层();
        if (层 == null) return 结果;
        var 目标 = 层.取(列, 行);
        if (目标 == null) return 结果;
        int 距离 = Mathf.Abs(列 - 玩家列) + Mathf.Abs(行 - 玩家行);
        if (距离 > 1) return 结果;
        bool 同格 = 距离 == 0;

        if (同格)
        {
            if (目标.对象 == 沙盒对象.楼梯) { 加楼梯动作(结果); return 结果; }
            if (目标.对象 == 沙盒对象.建筑入口) { 结果.Add(new 沙盒动作项("出去", "离开")); return 结果; }
            if (目标.是门)
            {
                if (目标.门开) 结果.Add(new 沙盒动作项("带上", "关门"));
                else 结果.Add(new 沙盒动作项("推开", "开门"));
                return 结果;
            }
            if (目标.地形 == 沙盒地形.出口) { 结果.Add(new 沙盒动作项("撤离", "撤离")); return 结果; }
            结果.Add(new 沙盒动作项("看看", "查看"));
            return 结果;
        }

        // 相邻格
        if (目标.对象 == 沙盒对象.建筑入口) { 结果.Add(new 沙盒动作项(当前建筑 == null ? "进去" : "出来", 当前建筑 == null ? "进入" : "离开")); return 结果; }
        if (目标.对象 == 沙盒对象.楼梯) { 加楼梯动作(结果); return 结果; }
        if (沙盒格工具.占位物(目标)) { 结果.Add(new 沙盒动作项("搜刮", "搜刮")); return 结果; }
        if (目标.是门)
        {
            if (!目标.门开) { 结果.Add(new 沙盒动作项("推开", "开门")); return 结果; }   // 关着的门：只能先推开
            结果.Add(new 沙盒动作项("走过去", "移动"));
            return 结果;
        }
        if (沙盒格工具.当前可通行(目标)) { 结果.Add(new 沙盒动作项("走过去", "移动")); return 结果; }
        结果.Add(new 沙盒动作项("看看", "查看"));
        return 结果;
    }

    private void 加楼梯动作(List<沙盒动作项> 结果)
    {
        bool 可上 = 当前建筑 != null && 楼层索引 + 1 < 当前建筑.楼层.Count;
        bool 可下 = 当前建筑 != null && 楼层索引 > 0;
        if (可上) 结果.Add(new 沙盒动作项("上楼", "上楼"));
        if (可下) 结果.Add(new 沙盒动作项("下楼", "下楼"));
        if (!可上 && !可下) 结果.Add(new 沙盒动作项("看看", "查看"));
    }

    // 执行菜单里选的动作（列/行 = 当时点的那格的坐标）
    public bool 执行动作(string 动作, int 列, int 行)
    {
        if (string.IsNullOrEmpty(动作)) return false;
        var 层 = 当前层();
        if (层 == null) return false;
        var 目标 = 层.取(列, 行);
        switch (动作)
        {
            case "开门": if (目标 != null && 目标.是门) { 开合门(目标, 列, 行); return true; } return false;
            case "关门": if (目标 != null && 目标.是门) { 开合门(目标, 列, 行); return true; } return false;
            case "上楼": 上楼(); return true;
            case "下楼": 下楼(); return true;
            case "进入":
                if (目标 != null && 目标.对象 == 沙盒对象.建筑入口) 进入建筑(目标.标识);
                return true;
            case "离开": 离开建筑(); return true;
            case "搜刮":
                if (沙盒格工具.占位物(目标)) 打开容器(目标.标识);
                else { var 邻 = 相邻可搜物(); if (邻 != null) 打开容器(邻.标识); }
                return true;
            case "撤离": return 撤离();
            case "移动": return 移动(列, 行);
            case "查看": 交互(); return true;
            default: return false;
        }
    }

    // 点格 = 直接执行首选动作（没接小菜单时的兜底；接了小菜单就走 目标动作 + 执行动作）
    public void 点格(int 列, int 行)
    {
        if (遭遇中) return;
        var 层 = 当前层();
        if (层 == null) return;
        if (Mathf.Abs(列 - 玩家列) + Mathf.Abs(行 - 玩家行) > 1) { 提示("一次只能点相邻的一格。"); return; }
        var 动作 = 目标动作(列, 行);
        if (动作.Count == 0) { 提示("那里没什么可做的。"); return; }
        执行动作(动作[0].动作, 列, 行);
    }

    public bool 移动(int 列, int 行)
    {
        if (遭遇中) return false;
        var 层 = 当前层();
        if (层 == null) return false;
        if (Mathf.Abs(列 - 玩家列) + Mathf.Abs(行 - 玩家行) != 1) { 提示("一次只能走到相邻的一格。"); return false; }
        var 目标 = 层.取(列, 行);
        if (目标 == null) return false;
        if (沙盒格工具.占位物(目标)) { 提示($"那是{物件名(目标)}，点它搜刮（人进不去）。"); return false; }
        if (目标.是门 && !目标.门开) { 提示("门关着——点它推开。"); return false; }
        if (!沙盒格工具.当前可通行(目标)) { 提示("那边过不去。"); return false; }
        if (!扣行动点(移动行动点)) { 提示("你累得抬不起腿了，先回安全屋吧。"); return false; }

        // 先手判定：移动前就看见它（玩家主动接近）→ 我方先手；贴脸撞见 → 速度序
        bool 先前可见 = 格可见(层, 列, 行);
        推进时间(每格游戏分钟);
        玩家列 = 列; 玩家行 = 行;
        刷新视野();
        if (!检查遭遇(先前可见)) 发布显示();   // 脚下提示 由 发布显示 自动带上
        return true;
    }

    // 与当前格/相邻格交互（面板按钮走这里；点击走 点格）
    public void 交互()
    {
        if (遭遇中) return;
        var 层 = 当前层();
        var 格 = 当前格();
        if (层 == null || 格 == null) return;
        // 脚下优先
        if (格.对象 == 沙盒对象.楼梯) { 用楼梯(); return; }
        if (格.对象 == 沙盒对象.建筑入口) { if (当前建筑 == null) 进入建筑(格.标识); else 离开建筑(); return; }
        if (格.是门) { 开合门(格, 玩家列, 玩家行); return; }
        if (格.地形 == 沙盒地形.出口) { 撤离(); return; }
        // 再看相邻（门/楼梯/家具 都占格或需要靠近，按钮要能顺手用）
        foreach (var (偏列, 偏行) in 沙盒格工具.四方向)
        {
            var 邻 = 层.取(玩家列 + 偏列, 玩家行 + 偏行);
            if (邻 == null) continue;
            if (邻.对象 == 沙盒对象.建筑入口) { if (当前建筑 == null) 进入建筑(邻.标识); else 离开建筑(); return; }
            if (邻.对象 == 沙盒对象.楼梯) { 用楼梯(); return; }
            if (沙盒格工具.占位物(邻)) { 打开容器(邻.标识); return; }
            if (邻.是门 && !邻.门开) { 开合门(邻, 玩家列 + 偏列, 玩家行 + 偏行); return; }
        }
        提示(脚下提示());
    }

    // 开门/关门（真门的开关状态影响通行与视线）
    private void 开合门(沙盒格 门, int 列, int 行)
    {
        if (门 == null) return;
        门.门开 = !门.门开;
        刷新视野();
        发布显示();
        事件.发布(new 日志事件(日志类型.探索, 门.门开 ? "你推开了门。" : "你带上了门。"));
    }

    // 用楼梯：能上就上，否则能下就下（点击 = 切换楼层）
    private void 用楼梯()
    {
        if (遭遇中) return;
        if (当前建筑 == null) { 提示("这层没楼梯。"); return; }
        if (楼层索引 + 1 < 当前建筑.楼层.Count) { 上楼(); return; }
        if (楼层索引 > 0) { 下楼(); return; }
        提示("这里已经是顶层了。");
    }

    // 相邻的家具/尸体（搜刮按钮 与 自动提示 用）
    private 沙盒格 相邻可搜物()
    {
        var 层 = 当前层();
        if (层 == null) return null;
        foreach (var (偏列, 偏行) in 沙盒格工具.四方向)
        {
            var 格 = 层.取(玩家列 + 偏列, 玩家行 + 偏行);
            if (沙盒格工具.占位物(格)) return 格;
        }
        return null;
    }

    private string 物件名(沙盒格 格)
    {
        if (格 == null) return "东西";
        if (格.对象 == 沙盒对象.尸体) return "尸体";
        if (!string.IsNullOrEmpty(格.附加) && 数据?.搜索地图类型 != null)
        {
            var 定义 = 找容器定义(格.附加);
            if (定义 != null && !string.IsNullOrEmpty(定义.名称)) return 定义.名称;
        }
        return "家具";
    }

    // 打开容器：扣行动点 + 走时间 + 交给搜索面板（实例 id 已在进入地区时注册）
    public void 打开容器(string 实例标识)
    {
        if (string.IsNullOrEmpty(实例标识)) return;
        if (!扣行动点(搜索行动点)) { 提示("你太累了，翻不动了。"); return; }
        推进时间(搜索游戏分钟);
        发布显示();
        事件.发布(new 打开沙盒容器事件(实例标识));
    }

    // ===== 建筑 / 楼层 =====

    public void 进入建筑(string 建筑标识)
    {
        if (遭遇中) return;
        var 楼 = 片区?.找建筑(建筑标识);
        if (楼 == null || 楼.楼层.Count == 0) { 提示("门被堵死了，进不去。"); return; }
        var 层 = 楼.取楼层(0);
        if (层 == null || 层.出生列 < 0) { 提示("里面一片漆黑，进不去。"); return; }
        // 顺手把街上那扇楼门推开（你刚从这里进来），出门时它就是开着的
        var 街门 = 片区?.街道?.取(楼.入口列, 楼.入口行);
        if (街门 != null && 街门.是门) 街门.门开 = true;
        当前建筑 = 楼; 楼层索引 = 0;
        玩家列 = 层.出生列; 玩家行 = 层.出生行;
        刷新视野();
        事件.发布(new 日志事件(日志类型.探索, $"你推门进了{楼.名称}（{层.名称}）。"));
        发布显示();
    }

    public void 离开建筑()
    {
        if (当前建筑 == null) { 提示("你已经在街上了。"); return; }
        var 楼 = 当前建筑;
        当前建筑 = null; 楼层索引 = -1;
        var 街 = 片区.街道;
        var 落点 = 门外落脚点(街, 楼);
        玩家列 = 落点.列; 玩家行 = 落点.行;
        刷新视野();
        事件.发布(new 日志事件(日志类型.探索, $"你从{楼.名称}退了出来。"));
        发布显示();
    }

    // 出楼后站哪儿：楼门旁边那个能站的街道格（没有就站在门洞里，并把门打开）
    private (int 列, int 行) 门外落脚点(沙盒层数据 街, 建筑数据 楼)
    {
        if (楼.入口列 >= 0 && 街.取(楼.入口列, 楼.入口行) != null)
        {
            foreach (var (偏列, 偏行) in 沙盒格工具.四方向)
            {
                int 列 = 楼.入口列 + 偏列, 行 = 楼.入口行 + 偏行;
                if (沙盒格工具.当前可通行(街.取(列, 行)) && 街.取(列, 行)?.对象 == 沙盒对象.无) return (列, 行);
            }
            var 门 = 街.取(楼.入口列, 楼.入口行);
            if (门 != null) 门.门开 = true;
            return (楼.入口列, 楼.入口行);
        }
        return (街.出生列, 街.出生行);
    }

    public void 上楼() => 切层(1);
    public void 下楼() => 切层(-1);

    private void 切层(int 方向)
    {
        if (遭遇中) return;
        if (当前建筑 == null) { 提示("街上没有楼梯。"); return; }
        int 目标 = 楼层索引 + 方向;
        if (目标 < 0) { 离开建筑(); return; }
        if (目标 >= 当前建筑.楼层.Count) { 提示("这里已经是顶层了。"); return; }
        var 层 = 当前建筑.取楼层(目标);
        if (层 == null || 层.出生列 < 0) { 提示("楼梯那头塌了。"); return; }
        楼层索引 = 目标;
        玩家列 = 层.出生列; 玩家行 = 层.出生行;
        刷新视野();
        事件.发布(new 日志事件(日志类型.探索, 方向 > 0 ? $"你上了{层.名称}。" : $"你下到了{层.名称}。"));
        发布显示();
    }

    // ===== 遭遇 / 战斗 =====

    private bool 检查遭遇(bool 先前可见)
    {
        var 层 = 当前层();
        if (层 == null) return false;
        // 自己踩上去的敌人（贴身）：速度序
        var 脚下 = 层.取(玩家列, 玩家行);
        if (脚下 != null && 脚下.对象 == 沙盒对象.敌人 && !string.IsNullOrEmpty(脚下.标识))
        {
            遭遇(玩家列, 玩家行, 脚下.标识, 警觉敌格.Contains(格索引(层, 玩家列, 玩家行)) ? 1 : 0);
            return true;
        }
        foreach (var (偏列, 偏行) in 沙盒格工具.四方向)
        {
            int 列 = 玩家列 + 偏列, 行 = 玩家行 + 偏行;
            var 格 = 层.取(列, 行);
            if (格 == null || 格.对象 != 沙盒对象.敌人 || string.IsNullOrEmpty(格.标识)) continue;
            int 先手 = 警觉敌格.Contains(格索引(层, 列, 行)) ? 1 : (先前可见 ? 2 : 0);
            遭遇(列, 行, 格.标识, 先手);
            return true;
        }
        return false;
    }

    private void 遭遇(int 列, int 行, string 敌人组, int 先手)
    {
        待结算敌列 = 列; 待结算敌行 = 行;
        遭遇中 = true; 本次战斗来自沙盒 = true;
        事件.发布(new 日志事件(日志类型.战斗, 先手 == 2 ? "你抢在它察觉之前扑了上去。" : "它已经扑到眼前了！"));
        战斗?.开始战斗(敌人组, "__沙盒胜利", "__沙盒返回", 先手, "", 当前层()?.战斗棋盘);
    }

    // 战斗结束（BattleService 发布）：胜利 → 清格 + 原地留尸；失败/逃跑 → 敌人留在原地
    private void 处理战斗结束(战斗结束事件 e)
    {
        if (!本次战斗来自沙盒) return;
        遭遇中 = false;
        if (!e.胜利) return;
        var 层 = 当前层();
        if (层 == null || 待结算敌列 < 0) return;
        var 格 = 层.取(待结算敌列, 待结算敌行);
        if (格 != null)
        {
            格.对象 = 沙盒对象.无; 格.标识 = null; 格.附加 = null;
            if (!string.IsNullOrEmpty(e.尸体容器)) { 格.对象 = 沙盒对象.尸体; 格.标识 = e.尸体容器; }
            else if (格.地形 != 沙盒地形.地板 && 格.地形 != 沙盒地形.街道) 格.地形 = 沙盒地形.地板;
        }
        待结算敌列 = -1; 待结算敌行 = -1;
        刷新视野(); 发布显示();
    }

    // 战斗面板 [继续] → BattleService.返回() 依 结果节点 回调到这里
    public void 战斗胜利()
    {
        本次战斗来自沙盒 = false; 遭遇中 = false;
        发布显示(); 提示("战场清干净了，尸体留在原地。");
    }

    public void 战斗逃跑()
    {
        var 层 = 当前层();
        if (层 != null && 待结算敌列 >= 0) 警觉敌格.Add(格索引(层, 待结算敌列, 待结算敌行));
        待结算敌列 = -1; 待结算敌行 = -1;
        本次战斗来自沙盒 = false; 遭遇中 = false;
        发布显示(); 提示("你挣脱了——那个东西还在原地，而且已经盯上你了。");
    }

    // ===== 迷雾 / 视野 =====

    private void 刷新视野()
    {
        var 层 = 当前层();
        if (层 == null) return;
        var 雾 = 取雾(层);
        for (int i = 0; i < 雾.Length; i++) if (雾[i] == 可见) 雾[i] = 已探索;   // 上一帧的可见 → 记忆
        int 半径平方 = 视野半径 * 视野半径;
        for (int 行 = 0; 行 < 层.行; 行++)
            for (int 列 = 0; 列 < 层.列; 列++)
            {
                int 差列 = 列 - 玩家列, 差行 = 行 - 玩家行;
                if (差列 * 差列 + 差行 * 差行 > 半径平方) continue;
                if (!有视线(层, 玩家列, 玩家行, 列, 行)) continue;
                雾[行 * 层.列 + 列] = 可见;
            }
    }

    // 视线：Bresenham 直线，途中碰到阻挡视线的地形则看不见（墙/建筑体挡，障碍不挡）
    private static bool 有视线(沙盒层数据 层, int 起列, int 起行, int 止列, int 止行)
    {
        int 差列 = Mathf.Abs(止列 - 起列), 差行 = Mathf.Abs(止行 - 起行);
        int 步列 = 起列 < 止列 ? 1 : -1, 步行 = 起行 < 止行 ? 1 : -1;
        int 误差 = 差列 - 差行;
        int 列 = 起列, 行 = 起行;
        while (true)
        {
            if (列 == 止列 && 行 == 止行) return true;
            if (!(列 == 起列 && 行 == 起行))
            {
                var 格 = 层.取(列, 行);
                if (沙盒格工具.当前阻挡视线(格)) return false;   // 关着的门也挡视线——门后看不见
            }
            int 双误 = 2 * 误差;
            if (双误 > -差行) { 误差 -= 差行; 列 += 步列; }
            if (双误 < 差列) { 误差 += 差列; 行 += 步行; }
        }
    }

    private bool 格可见(沙盒层数据 层, int 列, int 行)
    {
        var 雾 = 取雾(层);
        return 层.在界内(列, 行) && 雾[行 * 层.列 + 列] == 可见;
    }

    private byte[] 取雾(沙盒层数据 层)
    {
        string 键 = 层键();
        if (!迷雾表.TryGetValue(键, out var 雾) || 雾.Length != 层.列 * 层.行)
        {
            雾 = new byte[层.列 * 层.行];
            迷雾表[键] = 雾;
        }
        return 雾;
    }

    // ===== 显示 =====

    private void 发布显示(string 覆盖提示 = null)
    {
        var 层 = 当前层();
        if (层 == null) { 当前显示 = default; return; }
        var 雾 = 取雾(层);
        var 视图 = new 沙盒格视图[层.列 * 层.行];
        for (int 行 = 0; 行 < 层.行; 行++)
            for (int 列 = 0; 列 < 层.列; 列++)
            {
                int 索引 = 行 * 层.列 + 列;
                byte 态 = 雾[索引];
                if (态 == 未探索) { 视图[索引] = 沙盒格视图.未探索; continue; }
                bool 当前可见 = 态 == 可见;
                var 格 = 层.取(列, 行);
                视图[索引] = new 沙盒格视图(显示码(格, 当前可见), true, 当前可见, 格 != null && 格.门开);
            }
        // 脚下/相邻能做什么（按钮启停用）：搜刮 / 进楼门 / 上下楼 / 撤离
        // 家具占格、门/楼梯要靠近，所以「可做」按 脚下 ∪ 四邻 判定
        var 脚下 = 当前格();
        var 邻 = 相邻可搜物();
        bool 搜 = 邻 != null;
        bool 进 = 脚下 != null && 脚下.对象 == 沙盒对象.建筑入口;
        bool 梯 = 脚下 != null && 脚下.对象 == 沙盒对象.楼梯;
        bool 撤 = 脚下 != null && 脚下.地形 == 沙盒地形.出口 && 当前建筑 == null;
        if (!进 || !梯)
            foreach (var (偏列, 偏行) in 沙盒格工具.四方向)
            {
                var 邻格 = 层.取(玩家列 + 偏列, 玩家行 + 偏行);
                if (邻格 == null) continue;
                if (邻格.对象 == 沙盒对象.建筑入口) 进 = true;
                if (邻格.对象 == 沙盒对象.楼梯) 梯 = true;
            }
        当前显示 = new 沙盒显示事件(信息条(), 覆盖提示 ?? 脚下提示(), 层.列, 层.行, 视图,
            玩家列, 玩家行, 当前建筑 != null, 搜, 进, 梯, 撤);
        事件.发布(当前显示);
    }

    // 显示码：地形恒显（记忆）；移动物（敌人）只在当前可见时显示；门/容器/楼门/楼梯/尸体 算记忆
    private static 沙盒格显示 显示码(沙盒格 格, bool 当前可见)
    {
        if (格 == null) return 沙盒格显示.空;
        switch (格.对象)
        {
            case 沙盒对象.搜索点: return 沙盒格显示.搜索点;
            case 沙盒对象.尸体: return 沙盒格显示.尸体;
            case 沙盒对象.建筑入口: return 沙盒格显示.建筑入口;
            case 沙盒对象.楼梯: return 沙盒格显示.楼梯;
            case 沙盒对象.敌人: return 当前可见 ? 沙盒格显示.敌人 : 沙盒格显示.空;
            case 沙盒对象.陷阱: return 当前可见 ? 沙盒格显示.障碍 : 沙盒格显示.空;
        }
        switch (格.地形)
        {
            case 沙盒地形.墙:
            case 沙盒地形.建筑体: return 沙盒格显示.墙;
            case 沙盒地形.门: return 沙盒格显示.门;          // 开/关 由 视图.门开 区分
            case 沙盒地形.地板: return 沙盒格显示.地板;
            case 沙盒地形.街道: return 沙盒格显示.街道;
            case 沙盒地形.障碍: return 沙盒格显示.障碍;
            case 沙盒地形.出口: return 沙盒格显示.出口;
            default: return 沙盒格显示.空;
        }
    }

    private string 信息条()
    {
        if (片区 == null) return "";
        string 位置 = 当前建筑 == null ? "街道" : $"{当前建筑.名称} · {当前层()?.名称}";
        string 危险 = 片区.危险度 <= 1 ? "危险度:低" : (片区.危险度 == 2 ? "危险度:中" : "危险度:高");
        return $"{片区.名称} · {危险}    {位置}    行动 {档案?.行动点}/{档案?.最大行动点}    {时间文本()}";
    }

    private string 时间文本()
    {
        float 分钟 = 档案?.游戏分钟数 ?? 0f;
        int 天 = (int)(分钟 / 1440f) + 1;
        int 时 = (int)(分钟 % 1440f) / 60;
        int 分 = (int)(分钟 % 60f);
        return $"第{天}天 {时:00}:{分:00}";
    }

    // 脚下 & 相邻的可交互提示（信息条下方一行）
    private string 脚下提示()
    {
        var 层 = 当前层();
        var 格 = 当前格();
        if (层 == null || 格 == null) return "";
        if (格.对象 == 沙盒对象.楼梯) return "脚下是楼梯：点它上/下楼。";
        if (格.对象 == 沙盒对象.建筑入口) return 当前建筑 == null ? "这是楼门：点它进去。" : "这是楼门：点它出去。";
        if (格.是门) return 格.门开 ? "门开着（点自己这格可带上）。" : "门关着（点它推开）。";
        if (格.地形 == 沙盒地形.出口) return "撤离点：随时可以带着收获离开。";
        var 邻 = 相邻可搜物();
        if (邻 != null) return $"旁边是{物件名(邻)}：点它搜刮。";
        foreach (var (偏列, 偏行) in 沙盒格工具.四方向)
        {
            int 列 = 玩家列 + 偏列, 行 = 玩家行 + 偏行;
            var 邻居 = 层.取(列, 行);
            if (邻居 == null) continue;
            if (邻居.对象 == 沙盒对象.敌人 && 格可见(层, 列, 行)) return "旁边就是敌人——再靠近一步就会打起来。";
            if (邻居.对象 == 沙盒对象.建筑入口) return "旁边有一扇门，点它进出。";
            if (邻居.是门 && !邻居.门开) return "旁边有扇关着的门，点它可以推开。";
            if (邻居.对象 == 沙盒对象.楼梯) return "旁边是楼梯，点它上/下楼。";
        }
        return "";
    }

    private void 提示(string 文本)
    {
        if (string.IsNullOrEmpty(文本)) return;
        if (探索中 && 当前显示.格 != null) { 发布显示(文本); return; }   // 面板在：整条刷新，提示落在提示行
        事件.发布(new 沙盒提示事件(文本));
    }

    // ===== 内部工具 =====

    private 沙盒层数据 当前层()
    {
        if (片区 == null) return null;
        if (当前建筑 == null) return 片区.街道;
        return 当前建筑.取楼层(楼层索引);
    }

    private string 层键()
    {
        if (当前建筑 == null) return "街";
        return $"{当前建筑.标识}#{楼层索引}";
    }

    private 沙盒格 当前格() => 当前层()?.取(玩家列, 玩家行);

    private static int 格索引(沙盒层数据 层, int 列, int 行) => 行 * 层.列 + 列;

    private long 取世界种子()
    {
        long 种子 = 档案?.世界种子 ?? 0;
        if (种子 == 0)
        {
            // 旧档/未设：用固定兜底种子（不是随机——避免同一存档每次进图都变），并提示升级
            种子 = 沙盒种子.混合("缺省世界");
            if (档案 != null) 档案.世界种子 = 种子;
            Debug.LogWarning($"[沙盒] 世界种子缺失，已回落固定种子 {种子}（建议开新档或触发一次重掷）");
        }
        if (档案 != null)
        {
            if (档案.沙盒生成器版本 != 沙盒种子.生成器版本)
            {
                // 版本不符：明确重掷（不做静默重建——否则玩家记住的路线会悄悄失效）
                Debug.LogWarning($"[沙盒] 生成器版本 {档案.沙盒生成器版本} → {沙盒种子.生成器版本}：结构需重掷");
                档案.沙盒生成器版本 = 沙盒种子.生成器版本;
            }
        }
        return 种子;
    }

    // 容器实例注册：把每层的「搜索点」格按 实例 id 注册到 搜索服务（定义 = 搜索_地图类型 里的容器）
    private void 注册容器实例()
    {
        if (搜索 == null || 片区 == null) return;
        int 注册数 = 0;
        foreach (var 层 in 枚举所有层())
            foreach (var (_, _, 格) in 层.全部())
            {
                if (格 == null || 格.对象 != 沙盒对象.搜索点 || string.IsNullOrEmpty(格.标识)) continue;
                var 定义 = 找容器定义(格.附加);
                if (定义 == null) continue;
                // 实例定义 = 静态定义 + 实例 id（搜索服务按 id 缓存内容；清空战局 时会一起清掉）
                搜索.注册实例容器(new 搜索容器
                {
                    标识 = 格.标识,
                    名称 = 定义.名称,
                    描述 = 定义.描述,
                    容器列 = 定义.容器列,
                    容器行 = 定义.容器行,
                    容器形状 = 定义.容器形状,
                    容器允许类型 = 定义.容器允许类型,
                    搜索时间 = 定义.搜索时间,
                    搜索表 = 定义.搜索表,
                });
                注册数++;
            }
        Debug.Log($"[沙盒] {当前地区?.名称}：容器实例 {注册数} 个，建筑 {片区.建筑.Count} 栋");
    }

    private IEnumerable<沙盒层数据> 枚举所有层()
    {
        if (片区?.街道 != null) yield return 片区.街道;
        if (片区?.建筑 == null) yield break;
        foreach (var 楼 in 片区.建筑)
        {
            if (楼?.楼层 == null) continue;
            foreach (var 层 in 楼.楼层) if (层 != null) yield return 层;
        }
    }

    private 搜索容器 找容器定义(string 容器定义标识)
    {
        if (string.IsNullOrEmpty(容器定义标识) || 数据?.搜索地图类型 == null) return null;
        foreach (var 类型 in 数据.搜索地图类型.Values)
        {
            if (类型?.房间 == null) continue;
            foreach (var 房间 in 类型.房间)
            {
                if (房间?.容器 == null) continue;
                foreach (var 容器 in 房间.容器)
                    if (容器 != null && 容器.标识 == 容器定义标识) return 容器;
            }
        }
        return null;
    }

    private 建筑模板 找建筑模板(string 标识)
        => 数据 != null && 数据.建筑模板.TryGetValue(标识, out var 模板) ? 模板 : null;

    private 搜索地图类型 找地图类型(string 标识)
        => 数据 != null && 数据.搜索地图类型.TryGetValue(标识, out var 类型) ? 类型 : null;

    private bool 扣行动点(int 数额)
    {
        if (档案 == null) return false;
        if (!档案.消耗行动点(数额))
        {
            事件.发布(new 精力变化事件(档案.行动点, 档案.最大行动点, 0));
            return false;
        }
        事件.发布(new 精力变化事件(档案.行动点, 档案.最大行动点, -数额));
        return true;
    }

    // 时间推进：换算成"现实秒"走 世界时间管理器.推进（含整点结算/生长/净化，绝不直接写 游戏分钟数）
    private void 推进时间(float 游戏分钟)
    {
        if (游戏分钟 <= 0f) return;
        var 时间 = ServiceRegistry.Get<世界时间管理器>();
        if (时间 == null) return;
        时间.推进(游戏分钟 / 世界时间管理器.每现实分钟游戏分钟 * 60f);
    }
}
