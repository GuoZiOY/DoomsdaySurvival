using System;
using System.Collections.Generic;

// 持有管理器：物品统一持有/放入/移除（塔科夫式——3 穿戴容器 + 仓库 + 嵌套容器（弹药箱等））。
// 负责：所有持有物品 查询（含嵌套容器递归）；物品数量/移除/词缀/耐久；统一放入（穿戴容器优先 → 仓库兜底）；
// 仓库视图/穿戴容器视图 包装（复用 网格服务 网格算法）。
// 数据（装备容器物品/仓库/网格服务/解析委托）由 玩家档案 持有；玩家档案 门面转发调用。
public sealed class 持有管理器
{
    public readonly 玩家档案 玩家;
    public 持有管理器(玩家档案 玩家) { this.玩家 = 玩家; }

    // —— 旧主背包 网格数据（废弃：不再存放游戏物品；保留 网格算法 与 旧引用 兼容——物品网格面板 数据源 null 时显示空网格兜底） ——
    public List<物品堆叠> 背包 => 玩家.网格服务.网格物品;
    public int 网格列 { get => 玩家.网格服务.网格列; set => 玩家.网格服务.网格列 = value; }
    public int 网格行 { get => 玩家.网格服务.网格行; set => 玩家.网格服务.网格行 = value; }

    // —— 统一持有入口 ——

    // 顶层持有（3 穿戴容器 容器物品 + 仓库物品）
    private IEnumerable<物品堆叠> 顶层持有堆叠()
    {
        foreach (var 槽 in new[] { "弹挂", "腰封", "背包" })
        {
            var 记录 = 玩家.装备.Find(e => e.槽位 == 槽);
            if (记录?.容器物品 == null) continue;
            foreach (var 堆叠 in 记录.容器物品)
                if (堆叠 != null) yield return 堆叠;
        }
        foreach (var 堆叠 in 玩家.仓库物品)
            if (堆叠 != null) yield return 堆叠;
    }

    // 展开容器：容器本身 + 内部物品（递归，深度 < 2 防套娃，与 负重占用 同规则——弹药箱等嵌套容器内部也算持有）
    private IEnumerable<物品堆叠> 展开持有(物品堆叠 堆叠, int 深度)
    {
        yield return 堆叠;
        if (堆叠.容器物品 == null || 深度 >= 2) yield break;
        foreach (var 内 in 堆叠.容器物品)
            if (内 != null)
                foreach (var 展开 in 展开持有(内, 深度 + 1))
                    yield return 展开;
    }

    // 所有持有物品（3 穿戴容器 + 仓库 + 嵌套容器内部）——返回快照 List（调用方迭代中可安全 扣数量/移除堆叠，不影响枚举）
    public List<物品堆叠> 所有持有物品()
    {
        var 结果 = new List<物品堆叠>();
        foreach (var 堆叠 in 顶层持有堆叠())
            foreach (var 展开 in 展开持有(堆叠, 0))
                结果.Add(展开);
        return 结果;
    }

    // 统一持有判断（合成/买卖/任务/使用 等系统入口）：跨 穿戴容器/仓库/嵌套容器 查询
    public bool 持有物品(string 标识) => 物品数量(标识) > 0;

    public int 物品数量(string 标识)
    {
        if (string.IsNullOrEmpty(标识)) return 0;
        int 总 = 0;
        foreach (var 堆叠 in 所有持有物品())
            if (堆叠.标识 == 标识) 总 += 堆叠.数量;
        return 总;
    }

    // 统一移除（跨 穿戴容器/仓库/嵌套容器 累扣；耗尽则从所在列表移除）
    public bool 移除物品(string 标识, int 数量 = 1)
    {
        if (string.IsNullOrEmpty(标识) || 数量 <= 0) return false;
        int 剩 = 数量;
        foreach (var 堆叠 in 所有持有物品())
        {
            if (堆叠.标识 != 标识 || 堆叠.数量 <= 0) continue;
            int 扣 = Math.Min(剩, 堆叠.数量);
            堆叠.数量 -= 扣;
            剩 -= 扣;
            if (堆叠.数量 <= 0) 从所在列表移除(堆叠);
            if (剩 <= 0) return true;
        }
        return false;
    }

    // 移除 具体堆叠实例（合成消耗/任务扣除 用）：从所在列表移除（3 穿戴容器 / 仓库 / 嵌套容器内部）
    public void 移除堆叠实例(物品堆叠 堆叠) => 从所在列表移除(堆叠);

    // 从所在列表移除堆叠（3 穿戴容器 / 仓库 / 嵌套容器内部）
    private void 从所在列表移除(物品堆叠 堆叠)
    {
        foreach (var 槽 in new[] { "弹挂", "腰封", "背包" })
        {
            var 记录 = 玩家.装备.Find(e => e.槽位 == 槽);
            if (记录?.容器物品 != null && 记录.容器物品.Remove(堆叠)) return;
        }
        if (玩家.仓库物品.Remove(堆叠)) return;
        foreach (var 堆 in 顶层持有堆叠())
            if (移除于嵌套(堆, 堆叠, 0)) return;
    }

    private bool 移除于嵌套(物品堆叠 容器, 物品堆叠 目标, int 深度)
    {
        if (容器.容器物品 == null || 深度 >= 2) return false;
        foreach (var 内 in 容器.容器物品)
        {
            if (内 == 目标) { 容器.容器物品.Remove(内); return true; }
            if (移除于嵌套(内, 目标, 深度 + 1)) return true;
        }
        return false;
    }

    public List<词缀条> 背包词缀(string 标识)
    {
        foreach (var 堆叠 in 所有持有物品())
            if (堆叠.标识 == 标识 && 堆叠.数量 > 0) return 堆叠.词缀;
        return null;
    }

    public int 背包当前耐久(string 标识)
    {
        foreach (var 堆叠 in 所有持有物品())
            if (堆叠.标识 == 标识 && 堆叠.数量 > 0) return 堆叠.当前耐久;
        return 0;
    }

    // 扣背包里某个物品的耐久（如 撬锁失败 磨损撬棍）；返回 true = 真扣到了（该物品有耐久上限）。
    // 无耐久（有效最大耐久 <= 0，例如钳子这类"材料工具"）→ 返回 false 且不改动，调用方按"用不坏"处理。
    public bool 扣背包耐久(string 标识, int 量, Func<string, int> 有效最大耐久解析)
    {
        if (量 <= 0 || string.IsNullOrEmpty(标识)) return false;
        if ((有效最大耐久解析?.Invoke(标识) ?? 0) <= 0) return false;
        foreach (var 堆叠 in 所有持有物品())
            if (堆叠.标识 == 标识 && 堆叠.数量 > 0)
            {
                堆叠.当前耐久 = Math.Max(0, 堆叠.当前耐久 - 量);
                return true;
            }
        return false;
    }

    // —— 统一放入（获得物品入口） ——

    // 放入物品（按标识，可堆叠）：优先 3 穿戴容器（弹挂→腰封→背包 找空位），满 → 仓库兜底；返回 实际放入数
    public int 放入物品(string 标识, int 数量 = 1)
    {
        if (string.IsNullOrEmpty(标识) || 数量 <= 0) return 0;
        int 原 = 数量;
        foreach (var 槽 in new[] { "弹挂", "腰封", "背包" })
        {
            var 记录 = 玩家.装备.Find(e => e.槽位 == 槽);
            if (记录?.容器物品 == null) continue;
            int 实放 = 穿戴容器服务(记录).放入网格(标识, 数量);
            数量 -= 实放;
            if (数量 <= 0) return 原;
        }
        数量 -= 仓库视图().放入网格(标识, 数量);
        return 原 - 数量;
    }

    // 放入具体堆叠实例（带词缀/容器数据）：穿戴容器优先 → 仓库兜底；已在某网格中 返回 true（不移动）
    public int 放入堆叠(物品堆叠 堆叠)
    {
        if (堆叠 == null || string.IsNullOrEmpty(堆叠.标识) || 堆叠.数量 <= 0) return 0;
        foreach (var 槽 in new[] { "弹挂", "腰封", "背包" })
        {
            var 记录 = 玩家.装备.Find(e => e.槽位 == 槽);
            if (记录?.容器物品 == null) continue;
            if (穿戴容器服务(记录).放入网格堆叠(堆叠)) return 堆叠.数量;
        }
        return 仓库视图().放入网格堆叠(堆叠) ? 堆叠.数量 : 0;
    }

    // 公开：指定 穿戴容器槽位（弹挂/腰封/背包）的 网格服务 视图（卸下回原容器用；槽空/未穿戴 = null）
    public 网格服务 穿戴容器视图(string 槽位)
    {
        var 记录 = 玩家.装备.Find(e => e.槽位 == 槽位);
        if (记录 == null || 记录.容器物品 == null) return null;
        return 穿戴容器服务(记录);
    }

    // 临时 穿戴容器视图（网格服务 包装 容器物品 + 注入解析器）
    private 网格服务 穿戴容器服务(装备记录 记录)
    {
        var 服务 = new 网格服务();
        服务.网格物品 = 记录.容器物品 ?? (记录.容器物品 = new List<物品堆叠>());
        服务.网格列 = 记录.容器列 > 0 ? 记录.容器列 : 1;
        服务.网格行 = 记录.容器行 > 0 ? 记录.容器行 : 1;
        服务.形状解析 = 玩家.形状解析;
        服务.堆叠上限解析 = 玩家.堆叠上限解析;
        服务.有效最大耐久解析 = 标识 => 玩家.装备管理.有效最大耐久(标识);
        服务.重量解析 = 玩家.重量解析;
        return 服务;
    }

    // 仓库视图：把 仓库物品 包成 网格服务（复用全部网格/拖拽/跨网格转移逻辑）
    public 网格服务 仓库视图()
    {
        var 服务 = new 网格服务();
        服务.网格物品 = 玩家.仓库物品;
        服务.网格列 = 玩家.仓库列;
        服务.网格行 = 玩家.仓库行 + 玩家.家具效果("储物箱");   // 储物箱 家具：升级 扩仓库 行数（每级 +10 行：10/20/30；未建=0 → 基础 10×20）
        服务.形状解析 = 玩家.形状解析;
        服务.堆叠上限解析 = 玩家.堆叠上限解析;
        服务.有效最大耐久解析 = 标识 => 玩家.装备管理.有效最大耐久(标识);
        服务.重量解析 = 玩家.重量解析;
        return 服务;
    }

    // —— 旧入口重定向（统一持有：不再往 档案.背包 放物品） ——
    public int 放入网格(string 标识, int 数量 = 1) => 放入物品(标识, 数量);
    public bool 放入网格堆叠(物品堆叠 堆叠) => 放入堆叠(堆叠) > 0;
    public void 从网格移除(string 标识, int 数量 = 1) => 移除物品(标识, 数量);
    public int 添加物品(string 标识, int 数量 = 1) => 放入物品(标识, 数量);
    public void 添加堆叠(物品堆叠 堆叠) => 放入堆叠(堆叠);
}
