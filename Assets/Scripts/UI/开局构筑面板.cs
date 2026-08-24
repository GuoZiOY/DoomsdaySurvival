using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 开局构筑面板（末日《最后87天》开局流程核心）：
// ①选职业 → ②分配 10 自由点 → ③选正负天赋（预算 10）→ 预览最终属性 → 确认进入游戏。
// 动态构建：不依赖场景专属接线，全部用 按钮预制体 + 创建行 生成。
// 显示方式：作为独立面板由 面板管理器 显示（主菜单"新游戏"→ 本面板）。
public sealed class 开局构筑面板 : 面板基类
{
    [SerializeField] private TMP_Text 标题;   // 可选（未接线则隐藏）

    private int 阶段 = 1;   // 1=选职业 2=分配自由点 3=选天赋 4=预览
    private string 选中职业 = "";
    private readonly Dictionary<属性类型, int> 分配 = new Dictionary<属性类型, int>();
    private readonly List<string> 已选天赋 = new List<string>();
    private int 剩余自由点 = 10;
    private int 天赋预算 = 10;
    private TMP_Text 阶段标题;

    private DataService 数据 => ServiceRegistry.Get<DataService>();
    private PlayerService 玩家服务 => ServiceRegistry.Get<PlayerService>();

    protected override void 刷新(object 上下文)
    {
        // 进入面板时重置构筑状态（每次新游戏都是全新构筑）
        阶段 = 1; 选中职业 = ""; 分配.Clear(); 已选天赋.Clear();
        剩余自由点 = 10; 天赋预算 = 10;
        渲染();
    }

    public override bool 回退()
    {
        // 构筑中：返回主菜单
        if (面板管理器.实例 != null) 面板管理器.实例.回主菜单();
        return true;
    }

    public override string 取消文本 => "返回主菜单";

    private void 渲染()
    {
        if (内容区 == null) return;
        清空(内容区);
        // 阶段标题
        创建标签(内容区, 阶段标题文本());
        switch (阶段)
        {
            case 1: 渲染选职业(); break;
            case 2: 渲染分配自由点(); break;
            case 3: 渲染选天赋(); break;
            case 4: 渲染预览(); break;
        }
    }

    private string 阶段标题文本()
    {
        switch (阶段)
        {
            case 1: return "—— 选择你的身份 ——";
            case 2: return "—— 分配自由点数（剩余 " + 剩余自由点 + "）——";
            case 3: return "—— 选择天赋（预算 " + 天赋预算 + "）——";
            case 4: return "—— 确认你的生存者 ——";
            default: return "";
        }
    }

    // ===== 阶段1：选职业 =====
    private void 渲染选职业()
    {
        创建标签(内容区, "职业决定初始属性、技能、天赋与装备。");
        foreach (var 职业 in 数据.职业.Values)
        {
            string 标识 = 职业.标识;
            bool 选中 = 标识 == 选中职业;
            面板基类.创建行(内容区, (选中 ? "▶ " : "") + 职业.名称 + "：" + 职业.描述,
                () => { 选中职业 = 标识; 渲染(); }, false, false);
        }
        if (string.IsNullOrEmpty(选中职业))
            创建标签(内容区, "（选择一个职业后进入下一步）");
        else
        {
            创建行(内容区, "下一步 →", () => { 阶段 = 2; 渲染(); });
        }
    }

    // ===== 阶段2：分配 10 自由点 =====
    private void 渲染分配自由点()
    {
        创建标签(内容区, $"职业【{职业名(选中职业)}】 基础 5 + 职业加成 + 自由点。");
        创建标签(内容区, $"剩余自由点：{剩余自由点}");
        foreach (属性类型 类型 in System.Enum.GetValues(typeof(属性类型)))
        {
            string 名 = 类型名(类型);
            int 已加 = 分配.TryGetValue(类型, out var v) ? v : 0;
            创建行(内容区, $"{名}  +{已加}   [ + ]",
                () =>
                {
                    if (剩余自由点 > 0)
                    {
                        分配[类型] = 已加 + 1;
                        剩余自由点--;
                        渲染();
                    }
                }, false, false);
        }
        if (剩余自由点 == 0)
            创建行(内容区, "下一步 →", () => { 阶段 = 3; 渲染(); });
    }

    // ===== 阶段3：选正负天赋（预算制） =====
    private void 渲染选天赋()
    {
        创建标签(内容区, $"正面天赋消耗预算，负面天赋返还预算。当前预算：{天赋预算}");
        // 正面
        创建标签(内容区, "—— 正面天赋（花预算）——");
        foreach (var 天赋 in 数据.天赋.Values)
        {
            if (天赋.点数 <= 0) continue;
            string 标识 = 天赋.标识;
            bool 已选 = 已选天赋.Contains(标识);
            创建行(内容区, (已选 ? "✓ " : "") + $"{天赋.名称}（{天赋.点数}点）{天赋.描述}",
                () =>
                {
                    if (已选) { 已选天赋.Remove(标识); 天赋预算 += 天赋.点数; }
                    else if (天赋预算 >= 天赋.点数) { 已选天赋.Add(标识); 天赋预算 -= 天赋.点数; }
                    else { 音效管理器.实例?.播放失败(); 事件日志("预算不足"); }
                    渲染();
                }, false, false);
        }
        // 负面
        创建标签(内容区, "—— 负面天赋（返预算）——");
        foreach (var 天赋 in 数据.天赋.Values)
        {
            if (天赋.点数 >= 0) continue;
            string 标识 = 天赋.标识;
            bool 已选 = 已选天赋.Contains(标识);
            创建行(内容区, (已选 ? "✓ " : "") + $"{天赋.名称}（返还 {-天赋.点数}点）{天赋.描述}",
                () =>
                {
                    if (已选) { 已选天赋.Remove(标识); 天赋预算 += 天赋.点数; }   // 返还的负点，移除时扣回
                    else { 已选天赋.Add(标识); 天赋预算 -= 天赋.点数; }           // 负点 → 预算增加
                    渲染();
                }, false, false);
        }
        if (天赋预算 >= 0)
            创建行(内容区, "下一步 → 确认生存者", () => { 阶段 = 4; 渲染(); });
    }

    // ===== 阶段4：预览并确认 =====
    private void 渲染预览()
    {
        var 玩家 = 玩家服务.档案;
        创建标签(内容区, $"职业：{职业名(选中职业)}");
        string 属性行 = "属性：";
        foreach (属性类型 类型 in System.Enum.GetValues(typeof(属性类型)))
        {
            int 基础 = 5;
            int 职业加 = 职业加成(类型);
            int 自由 = 分配.TryGetValue(类型, out var v) ? v : 0;
            属性行 += $"{类型名(类型)} {基础 + 职业加 + 自由}  ";
        }
        创建标签(内容区, 属性行);
        string 天赋行 = "天赋：";
        foreach (var 标识 in 已选天赋) 天赋行 += 数据.天赋.TryGetValue(标识, out var t) ? t.名称 + " " : 标识 + " ";
        foreach (var 标识 in new[] { 职业天赋(选中职业) })
            if (!string.IsNullOrEmpty(标识) && 数据.天赋.ContainsKey(标识))
                天赋行 += 数据.天赋[标识].名称 + "（职业） ";
        创建标签(内容区, 天赋行);
        创建标签(内容区, "确认后将进入末日第 1 天。");

        创建行(内容区, "✓ 确认开始", () => 确认开始());
        创建行(内容区, "← 返回选天赋", () => { 阶段 = 3; 渲染(); });
    }

    private void 确认开始()
    {
        // 写入构筑参数 → 新游戏
        玩家服务.待选职业 = 选中职业;
        玩家服务.待选天赋.Clear();
        玩家服务.待选天赋.AddRange(已选天赋);
        // 应用职业天赋（职业数据的天赋也加入待选）
        if (数据.职业.TryGetValue(选中职业, out var 职业) && !string.IsNullOrEmpty(职业.天赋))
            if (!玩家服务.待选天赋.Contains(职业.天赋)) 玩家服务.待选天赋.Add(职业.天赋);
        // 自由点分配
        foreach (var (类型, 点数) in 分配) 玩家服务.开局分配[类型] = 点数;
        玩家服务.新游戏();
        // 进入地图（末日：直接打开城市地图；开局文本走日志）
        ServiceRegistry.Get<地图服务>().打开大地图("营地");
    }

    // ===== 工具 =====

    private string 职业名(string 标识) => 数据.职业.TryGetValue(标识, out var 职业) ? 职业.名称 : 标识;
    private string 职业天赋(string 标识) => 数据.职业.TryGetValue(标识, out var 职业) ? (职业.天赋 ?? "") : "";
    private int 职业加成(属性类型 类型)
    {
        if (!数据.职业.TryGetValue(选中职业, out var 职业) || 职业.属性加成 == null) return 0;
        foreach (var 项 in 职业.属性加成)
            if (项.属性 == 类型名(类型)) return 项.点数;
        return 0;
    }

    private static string 类型名(属性类型 类型)
    {
        switch (类型)
        {
            case 属性类型.体质: return "体质";
            case 属性类型.力量: return "力量";
            case 属性类型.智慧: return "智慧";
            case 属性类型.敏捷: return "敏捷";
            case 属性类型.意志: return "意志";
            default: return "";
        }
    }

    private void 事件日志(string 文本) =>
        ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.反馈坏, 文本));
}
