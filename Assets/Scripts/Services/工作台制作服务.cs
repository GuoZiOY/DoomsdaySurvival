using System.Collections.Generic;

// 工作台制作服务：安全屋 制作 家具（工作台/灶台/医疗站）按配方制作物品——末日生存 手工制作 体系。
// 校验：家具 等级 ≥ 配方.需要等级 + 图纸 持有（如有）+ 材料 足够（跨 穿戴容器/仓库）+ 精力（行动点）足够 + 饱食/水分 达标。
// 消耗：扣 材料（跨容器）→ 扣 行动点（精力）→ 推进 游戏时间（游戏分钟数 += 配方.制作时间分）→
//       饱食/水分 按 2 倍速率 随 推进时间 即时扣（饱食 -= 4×(T/60)、水分 -= 6×(T/60)，四舍五入 至少 1）。
// 产物：跨容器放入（优先 穿戴容器 → 仓库 兜底）；背包 满 → 原子回滚（材料/行动点/时间/饱水 全部 返还）。
// 事件：背包变化（材料- / 产物+）、日志反馈、制作完成事件（天赋服务·美食家 = 灶台 饮食 双倍产出）。
public sealed class 工作台制作服务
{
    private readonly EventBus 事件;
    private readonly DataService 数据;
    private 玩家档案 玩家 => ServiceRegistry.Get<PlayerService>().档案;

    // 饱食/水分 阈值：低于 此值 禁止 制作（太饿/太渴 干不动活）
    private const int 最低饱食 = 10;
    private const int 最低水分 = 10;

    public 工作台制作服务(EventBus 事件, DataService 数据)
    {
        this.事件 = 事件;
        this.数据 = 数据;
    }

    // ===== 配方 查询（面板 用） =====

    // 该 制作 家具 的 全部 配方（含 未解锁：等级 未达 / 图纸 缺失——面板 灰显 标注 原因）
    public 配方数据[] 配方列表(string 家具标识)
    {
        var 列表 = new List<配方数据>();
        foreach (var 配方 in 数据.配方.Values)
            if (配方.类型 == 家具标识)
                列表.Add(配方);
        return 列表.ToArray();
    }

    // 等级 是否 解锁（家具 等级 ≥ 配方.需要等级；未建 = 0 级 恒 不解锁）
    public bool 等级解锁(string 家具标识, 配方数据 配方) => 玩家.家具等级(家具标识) >= 配方.需要等级;

    // 图纸 是否 持有（无 图纸 需求 = true）
    public bool 图纸解锁(配方数据 配方) => string.IsNullOrEmpty(配方.解锁图纸) || 玩家.持有物品(配方.解锁图纸);

    // 材料 是否 足够（跨 穿戴容器/仓库）
    public bool 材料足够(配方数据 配方)
    {
        if (配方.材料 == null) return true;
        foreach (var 材 in 配方.材料)
            if (玩家.持有管理.物品数量(材.物品) < 材.数量) return false;
        return true;
    }

    // 面板 状态 文本：未满足 的 首要 原因（"" = 可制作）——顺序：等级 / 图纸 / 材料 / 精力 / 饱食 / 水分
    public string 未满足原因(string 家具标识, 配方数据 配方)
    {
        if (!等级解锁(家具标识, 配方)) return $"需 {家具名(家具标识)} {配方.需要等级} 级";
        if (!图纸解锁(配方)) return $"需图纸：{物品名(配方.解锁图纸)}";
        if (!材料足够(配方)) return "材料不足";
        if (玩家.行动点 < 配方.消耗精力) return "精力不足";
        if (玩家.饱食度 < 最低饱食) return "太饿了";
        if (玩家.水分度 < 最低水分) return "太渴了";
        return "";
    }

    // 材料 摘要（面板 显示）
    public string 材料文本(配方数据 配方)
    {
        if (配方.材料 == null || 配方.材料.Length == 0) return "";
        var 段 = new List<string>();
        foreach (var 材 in 配方.材料)
            段.Add($"{物品名(材.物品)}×{材.数量}");
        return string.Join(" ", 段);
    }

    // ===== 制作 =====

    public bool 尝试制作(string 配方标识)
    {
        if (!数据.配方.TryGetValue(配方标识, out var 配方)) { 提示("未知配方。"); return false; }
        string 家具 = 配方.类型;
        if (玩家.家具等级(家具) <= 0) { 提示($"还没有 {家具名(家具)}。"); return false; }
        string 原因 = 未满足原因(家具, 配方);
        if (原因.Length > 0) { 提示($"{原因}（{配方.名称}）。"); return false; }
        // 校验 通过：扣 材料（跨 穿戴容器/仓库）
        if (配方.材料 != null)
            foreach (var 材 in 配方.材料)
            {
                玩家.持有管理.移除物品(材.物品, 材.数量);
                事件.发布(new 背包变化事件(材.物品, -材.数量, 变化原因.消耗));
            }
        // 扣 精力（行动点）
        玩家.生存管理.消耗行动点(配方.消耗精力);
        // 推进 游戏时间（分钟）
        玩家.游戏分钟数 += 配方.制作时间分;
        // 饱食/水分 2 倍速率 随 推进时间 即时扣（四舍五入 至少 1；下限 0）
        int 扣饱食 = System.Math.Max(1, (int)System.Math.Round(4.0 * 配方.制作时间分 / 60.0));
        int 扣水分 = System.Math.Max(1, (int)System.Math.Round(6.0 * 配方.制作时间分 / 60.0));
        玩家.饱食度 = System.Math.Max(0, 玩家.饱食度 - 扣饱食);
        玩家.水分度 = System.Math.Max(0, 玩家.水分度 - 扣水分);
        // 产物：跨容器放入（优先 穿戴容器 → 仓库 兜底）；背包 满 → 回滚
        int 放入 = 玩家.持有管理.放入物品(配方.产物, 配方.产物数量);
        if (放入 < 配方.产物数量)
        {
            回滚(配方, 扣饱食, 扣水分);
            提示("背包满了，制作失败。");
            return false;
        }
        事件.发布(new 背包变化事件(配方.产物, 放入, 变化原因.获得));
        事件.发布(new 制作完成事件(配方.产物, 放入, 配方.类型));   // 美食家（灶台 饮食 双倍）
        事件.发布(new 日志事件(日志类型.反馈, $"制作完成：{物品名(配方.产物)} ×{放入}！"));
        音效管理器.实例?.播放家具放下();
        return true;
    }

    // 背包满 回滚：材料/行动点/时间/饱水 全 返还（材料 已 腾出 空间，放回 必然 成功）
    private void 回滚(配方数据 配方, int 扣饱食, int 扣水分)
    {
        if (配方.材料 != null)
            foreach (var 材 in 配方.材料)
            {
                玩家.持有管理.放入物品(材.物品, 材.数量);
                事件.发布(new 背包变化事件(材.物品, 材.数量, 变化原因.获得));
            }
        玩家.生存管理.恢复行动点(配方.消耗精力);
        玩家.游戏分钟数 -= 配方.制作时间分;
        玩家.饱食度 = System.Math.Min(100, 玩家.饱食度 + 扣饱食);
        玩家.水分度 = System.Math.Min(100, 玩家.水分度 + 扣水分);
    }

    private string 物品名(string 标识) => 数据.物品.TryGetValue(标识, out var 物) ? 物.名称 : 标识;
    private string 家具名(string 标识) => 数据.家具.TryGetValue(标识, out var 家具) ? 家具.名称 : 标识;
    private void 提示(string 文本) => 事件.发布(new 日志事件(日志类型.反馈坏, 文本));
}
