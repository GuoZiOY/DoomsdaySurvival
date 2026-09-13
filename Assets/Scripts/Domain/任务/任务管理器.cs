using System.Collections.Generic;

// 任务管理器：任务/日常 领域逻辑（玩家档案 组合的子管理器——职责分离、调度器模式）。
// 负责：主线任务 添加/进度（击败/获得计数）；日常任务 查找/覆写。
// 数据（任务/日常 列表）由 玩家档案 持有。
public sealed class 任务管理器
{
    public readonly 玩家档案 玩家;
    public 任务管理器(玩家档案 玩家) { this.玩家 = 玩家; }

    public bool 添加任务(string 标识)
    {
        foreach (var 进度 in 玩家.任务) if (进度.标识 == 标识) return false;
        玩家.任务.Add(new 任务进度 { 标识 = 标识, 数量 = 0, 已完成 = false });
        return true;
    }

    public List<任务进度> 记录击败(string 敌人标识, int 目标数量 = 1)
    {
        var 完成 = new List<任务进度>();
        foreach (var 进度 in 玩家.任务)
        {
            if (进度.已完成) continue;
            if (进度.数量 < 目标数量)
            {
                进度.数量++;
                if (进度.数量 >= 目标数量) { 进度.已完成 = true; 完成.Add(进度); }
            }
        }
        return 完成;
    }

    public List<任务进度> 记录获得(string 物品标识, int 目标数量 = 1)
    {
        var 完成 = new List<任务进度>();
        foreach (var 进度 in 玩家.任务)
        {
            if (进度.已完成) continue;
            if (进度.数量 < 目标数量)
            {
                进度.数量++;
                if (进度.数量 >= 目标数量) { 进度.已完成 = true; 完成.Add(进度); }
            }
        }
        return 完成;
    }

    // 按标识查日常任务（空 = 无）
    public 日常任务 查找日常(string 标识)
    {
        foreach (var 条 in 玩家.日常) if (条.标识 == 标识) return 条;
        return null;
    }

    // 替换当天日常批次并记录生成日（跨天刷新用）
    public void 覆写日常(List<日常任务> 新日常)
    {
        玩家.日常 = 新日常 ?? new List<日常任务>();
        玩家.日常生成日 = 玩家.游戏天数;
    }
}
