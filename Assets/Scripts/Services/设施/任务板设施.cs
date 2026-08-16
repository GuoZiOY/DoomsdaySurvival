using System.Collections.Generic;

// 任务板设施：任务接取与进度展示（任务功能；任务栏共用任务面板）
public sealed class 任务板设施 : 设施逻辑, 任务功能
{
    // 可接任务：全部任务
    public 任务数据[] 可接任务()
    {
        var 列表 = new List<任务数据>();
        foreach (var 任务 in 数据.任务.Values) 列表.Add(任务);
        return 列表.ToArray();
    }

    // 尝试接取：交给 QuestService（已完成自动发奖，交付改动在任务模型讨论中）
    public bool 尝试接取(string 任务标识) => ServiceRegistry.Get<QuestService>().接取(任务标识);

    // 某任务的玩家进度文案
    public string 进度文本(string 任务标识)
    {
        if (!数据.任务.TryGetValue(任务标识, out var 任务)) return "";
        foreach (var p in 玩家.任务)
            if (p.标识 == 任务标识) return p.已完成 ? "已完成" : $"{p.数量}/{任务.目标数量}";
        return "未接取";
    }
}
