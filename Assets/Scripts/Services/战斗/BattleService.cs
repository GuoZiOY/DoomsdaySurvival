using System.Collections.Generic;
using UnityEngine;

// BattleService · （主） 分部 —— 基础设施与入口（字段/构造/开始战斗/棋盘与落位）
public sealed partial class BattleService
{
    private readonly EventBus 事件;
    private readonly DataService 数据;
    private readonly PlayerService 玩家服务;
    private 玩家档案 档案 => 玩家服务.档案;   // 动态取当前档案（新游戏/读档会替换实例，不能缓存旧引用）

    // —— 战斗状态 ——
    public bool 战斗中 { get; private set; }
    public int 回合数 { get; private set; }
    public 战斗单位 玩家 { get; private set; }
    public readonly List<战斗单位> 我方 = new List<战斗单位>();
    public readonly List<战斗单位> 敌方 = new List<战斗单位>();

    // —— 战斗沙盒（即时制 + 节点式轨道） ——
    public int 棋盘宽 { get; private set; } = 12;   // 轨道节点数（横向战线：0 ~ 棋盘宽-1）；节点间前后移动，无行维度
    public float 战斗分钟 { get; private set; }   // 本回合已累计分钟（满 60 结算一轮）
    public string 当前战斗棋盘 => 当前棋盘标识;      // 本次战斗实际使用的棋盘（遭遇来源传入后可用于自检/调试）
    public const float 每现实秒游戏分钟 = 1f;      // 战斗时间流速：1 现实秒 = 1 游戏分钟（2 倍正常流速，可调）

    // 战斗结束衔接
    private string 胜利节点;
    private string 返回节点;
    private string 结果节点;
    // ★ v51 刀17：**自动回派待办**。结束战斗 只登记它，由驱动方（世界时间管理器.驱动）在**下一帧**落地 ——
    //   本帧 战斗结束事件 的订阅者（战斗面板/侧边栏/HUD/格子探索服务）要先把结算表现跑完，再切层。
    private bool 自动回派待办;
    private bool 回派中;   // 回派重入锁：自动回派与面板 [继续] 按钮可能同帧都触发（幂等，见 回派）
    private int 先手模式;   // 0=速度序 1=敌方先手 2=我方先手
    private string 当前棋盘标识 = "街头";   // 本次战斗使用的 战斗棋盘.标识（遭遇来源按所在场景给：房间=室内 / 街道=街头 / 工厂=工厂）
    private int 累计经验;   // 原为 `累计金币, 累计经验` —— 累计金币 全仓库只在这里赋值、从不读取（货币已废），v51 刀7 删
    // 注：原有一个 `List<string> 消耗道具`（离场统一回写扣档案）—— v51 刀15 删：全仓库 0 写入，
    //     真正的消耗是 `扣战斗容器` 直接改容器本身（见 结算回写 上面的注释）。
    private readonly List<物品堆叠> 尸体战利品 = new List<物品堆叠>();   // 胜利 战利品（进 尸体 供 搜索，不 直接 入包）

    public string 当前消息 { get; private set; }

    private const string 危险色 = "#c7473d";
    // 上一次"发过 时间变化事件"的游戏分钟整数值（v51 刀15：把每帧事件降成每分钟一次；-1 = 还没发过）
    private int 上次发事件的分 = -1;

    public BattleService(EventBus 事件, DataService 数据, PlayerService 玩家服务)
    { this.事件 = 事件; this.数据 = 数据; this.玩家服务 = 玩家服务; }

    // ===== 开始战斗（即时制：棋盘 + 行动条） =====
    // 先手：0=速度序（遇见） 1=敌方绝对先手（被偷袭） 2=我方绝对先手（偷袭）
    // 助战组：encounters 助战组标识（可选）；助战单位以 是否我方=true 加入我方，自动 AI 行动（轻量助战）
    // 指定敌人：**一人一项的具体名单**（房间层用）——列表里有几项就打几只，
    //          传了名单就**不再按 敌人组 的 数量 重新展开**（否则"房间里 2 只、打起来 4 只"）；
    //          每项的 实例标识 会写进 战斗单位.实例标识（网格实体标识），保证"一个标识 = 一个单位"。
    //          不传 = 旧语义（探索/剧情：按 敌人组 展开，单位标识 = 定义标识#序号）
    public void 开始战斗(string 敌人组标识, string 胜利后节点, string 返回节点, int 先手 = 0,
        string 助战组标识 = "", string 棋盘标识 = "", IList<(string 定义标识, string 实例标识)> 指定敌人 = null)
    {
        bool 有组 = 数据.敌人组.TryGetValue(敌人组标识, out var 组);
        if (!有组 && 指定敌人 == null)
        {
            Debug.LogError($"[战斗] 敌人组不存在: {敌人组标识}");
            return;
        }
        // 先激活战斗面板再开始布置（面板需处于激活态才能渲染布阵）；剧情/探索 所有入口统一切入。
        事件.发布(new 打开战斗事件(返回节点));
        this.胜利节点 = 胜利后节点; this.返回节点 = 返回节点;
        this.先手模式 = 先手;
        战斗中 = true; 回合数 = 1; 战斗分钟 = 0f;
        我方.Clear(); 敌方.Clear();
        累计经验 = 0;
        尸体战利品.Clear();
        取消待行动作();   // 新 战斗 清 残留 蓄力
        var 玩家单位 = 战斗单位.从玩家投影(档案);
        玩家 = 玩家单位; 我方.Add(玩家单位);
        // 轻量助战：加入我方，AI 行动表自动出手（玩家不可手动操控；倒下无碍，玩家倒下=败）
        if (!string.IsNullOrEmpty(助战组标识) && 数据.助战组.TryGetValue(助战组标识, out var 助战组) && 助战组.成员 != null)
            foreach (var 项 in 助战组.成员)
            {
                if (!数据.敌人.TryGetValue(项.标识, out var 助)) continue;
                for (int i = 0; i < Mathf.Max(1, 项.数量); i++) 我方.Add(战斗单位.从助战生成(助));
            }
        if (指定敌人 != null)
        {
            // 一人一项：列表几项就几只，**一项一个单位**（不再乘 敌人组.数量）
            int 序 = 0;
            foreach (var (定义标识, 实例标识) in 指定敌人)
            {
                if (string.IsNullOrEmpty(定义标识) || !数据.敌人.TryGetValue(定义标识, out var 敌)) continue;
                var 单位 = 战斗单位.从敌人生成(敌);
                单位.实例标识 = string.IsNullOrEmpty(实例标识) ? $"{定义标识}#{++序}" : 实例标识;
                敌方.Add(单位);
            }
        }
        else if (组?.敌人 != null)
        {
            int 序 = 0;
            foreach (var 项 in 组.敌人)
            {
                if (!数据.敌人.TryGetValue(项.敌人, out var 敌)) continue;
                for (int i = 0; i < Mathf.Max(1, 项.数量); i++)
                {
                    var 单位 = 战斗单位.从敌人生成(敌);
                    单位.实例标识 = $"{项.敌人}#{++序}";   // 一个单位一个标识（同种多只也不重复）
                    敌方.Add(单位);
                }
            }
        }
        // 玩家攻击距离按当前武器射程；棋盘与落位先于 战斗开始事件（面板订阅事件布阵时数据已就绪）
        玩家.攻击距离 = 武器攻击距离(玩家.当前武器标识);
        刷新玩家速率();   // 敏捷 + 武器攻速/装备移速（含 重型 负面）→ 攻速/移速倍率
        初始化棋盘(棋盘标识);   // 遭遇场景决定棋盘（街头/室内/工厂…；空 = 街头）
        落位单位(先手);
        事件.发布(new 战斗开始事件(我方.ToArray(), 敌方.ToArray()));
    }

    // 轨道生成：节点数 = 战斗棋盘.宽（默认 12）；无行维度、无障碍物——单位沿节点前后移动
    // 棋盘标识：由遭遇来源给（房间层 = 室内 / 街道 = 街头 …）；空或不存在 → 回落 "街头"
    private void 初始化棋盘(string 棋盘标识)
    {
        当前棋盘标识 = string.IsNullOrEmpty(棋盘标识) ? "街头" : 棋盘标识;
        if (!数据.战斗棋盘.TryGetValue(当前棋盘标识, out var 棋盘))
        {
            if (数据.战斗棋盘.TryGetValue("街头", out var 兜底)) 棋盘 = 兜底;
            当前棋盘标识 = 棋盘 != null ? 棋盘.标识 : "";
        }
        if (棋盘 != null) 棋盘宽 = Mathf.Max(4, 棋盘.宽);
    }

    // 落位：我方节点 0，敌方节点 棋盘宽-1（同节点多单位重叠，视觉错位由轨道面板处理）
    private void 落位单位(int 先手)
    {
        int 我方列 = 0, 敌方列 = 棋盘宽 - 1;
        if (先手 == 1)   // 被偷袭：敌方先手（行动条领先）
        {
            foreach (var 敌 in 敌方) 敌.行动条 = 60f;
            foreach (var 我 in 我方) 我.行动条 = 0f;
        }
        else if (先手 == 2)  // 偷袭：我方先手
        {
            foreach (var 敌 in 敌方) 敌.行动条 = 0f;
            foreach (var 我 in 我方) 我.行动条 = 60f;
        }
        落位一侧(我方, 我方列);
        落位一侧(敌方, 敌方列);
    }

    private void 落位一侧(List<战斗单位> 单位组, int 列)
    {
        foreach (var 单位 in 单位组) 单位.列 = 列;   // 同节点重叠（视觉错位 面板 处理）
    }

}
