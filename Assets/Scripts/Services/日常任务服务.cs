using System.Collections.Generic;
using UnityEngine;

// 日常任务服务：按游戏内天数生成一批随机日常（悬赏），与系统任务面板「日常」同批共用。
// 目标类型两类（均需回悬赏板互动「提交」才结算发奖）：
//   · 击杀 —— 在悬赏板接取后，击杀指定敌人累计进度，达到数量回悬赏板提交；
//   · 收集 —— 攒够指定道具后回悬赏板提交，扣除道具再结算。
public sealed class 日常任务服务
{
    private readonly EventBus 事件;
    private readonly DataService 数据;
    private readonly PlayerService 玩家服务;
    private 玩家档案 档案 => 玩家服务.档案;

    public 日常任务服务(EventBus 事件, DataService 数据, PlayerService 玩家服务)
    { this.事件 = 事件; this.数据 = 数据; this.玩家服务 = 玩家服务; }

    // 当天日常列表；范围为空返回全部（系统任务面板），否则只返回该设施范围（设施委托栏）
    public 日常任务[] 当天日常(string 范围 = null)
    {
        检查刷新();
        if (string.IsNullOrEmpty(范围)) return 档案.日常.ToArray();
        var 列表 = new List<日常任务>();
        foreach (var 条 in 档案.日常) if (条.范围 == 范围) 列表.Add(条);
        return 列表.ToArray();
    }

    // 击杀项展示进度（击杀=累计数；收集=背包持有数封顶目标数）
    public int 进度(日常任务 日常)
    {
        if (日常.目标类型 == "击杀") return 日常.进度;
        return Mathf.Min(档案.物品数量(日常.目标标识), 日常.目标数量);
    }

    // 是否已达提交条件（未结算 + 击杀进度满 / 收集背包已够）
    public bool 可提交(日常任务 日常)
    {
        if (日常.已领取) return false;
        if (日常.目标类型 == "击杀") return 日常.进度 >= 日常.目标数量;
        return 档案.物品数量(日常.目标标识) >= 日常.目标数量;
    }

    // 击杀联动：指定敌人被击败时推进该类型日常的进度（不自动结算，需回板提交）
    public void 记录击杀(string 敌人标识)
    {
        bool 有变化 = false;
        foreach (var 日常 in 档案.日常)
        {
            if (日常.已领取 || 日常.目标类型 != "击杀" || 日常.目标标识 != 敌人标识) continue;
            if (日常.进度 < 日常.目标数量)
            {
                日常.进度++;
                有变化 = true;
            }
        }
        if (有变化) 事件.发布(new 日常任务变更事件());
    }

    // 是否可接取（未接取且未结算）
    public bool 可接取(日常任务 日常) => 日常 != null && !日常.已接取 && !日常.已领取;

    // 接取（悬赏板互动认领）：接取后才在系统任务面板「日常」栏可见；返回是否成功
    public bool 接取(string 标识)
    {
        var 日常 = 档案.查找日常(标识);
        if (日常 == null || 日常.已接取 || 日常.已领取) return false;
        日常.已接取 = true;
        事件.发布(new 日志事件(日志类型.任务, $"接取了委托：{日常.名称}"));
        事件.发布(new 日常任务变更事件());
        return true;
    }

    // 提交日常（击杀/收集 通用）：回悬赏板互动结算发奖；返回是否成功
    public bool 提交(string 标识)
    {
        var 日常 = 档案.查找日常(标识);
        if (日常 == null || 日常.已领取) return false;
        if (日常.目标类型 == "收集")
        {
            var 物品 = 目标物品(日常);
            string 名 = 物品 != null ? 物品.名称 : 日常.目标标识;
            int 缺 = 日常.目标数量 - 档案.物品数量(日常.目标标识);
            if (缺 > 0)
            {
                音效管理器.实例?.播放失败();
                事件.发布(new 日志事件(日志类型.反馈坏, $"还缺 {名}×{缺}，先凑齐再提交。"));
                return false;
            }
            档案.移除物品(日常.目标标识, 日常.目标数量);
            事件.发布(new 背包变化事件(日常.目标标识, -日常.目标数量, 变化原因.消耗));
        }
        else if (日常.进度 < 日常.目标数量)
        {
            音效管理器.实例?.播放失败();
            事件.发布(new 日志事件(日志类型.反馈坏, $"日常任务尚未完成：还差 {日常.目标数量 - 日常.进度} 个目标。"));
            return false;
        }
        结算(日常);
        return true;
    }

    // 结算：发奖并派发事件
    private void 结算(日常任务 日常)
    {
        日常.进度 = 日常.目标数量;
        日常.已领取 = true;
        档案.铜币 += 日常.奖励金币;
        事件.发布(new 金币变化事件(档案.铜币, 日常.奖励金币));
        bool 升级了 = 档案.获得经验(日常.奖励经验);
        事件.发布(new 任务完成事件(日常.标识, $"{日常.名称} 完成！+{货币工具.文本(日常.奖励金币)}、{日常.奖励经验}经验"));
        事件.发布(new 日志事件(日志类型.任务, $"{日常.名称} 完成！获得 {货币工具.文本(日常.奖励金币)}、{日常.奖励经验} 经验"));
        if (升级了) 事件.发布(new 经验变化事件(档案.等级, 档案.经验, 档案.升级所需经验, true));
        事件.发布(new 日常任务变更事件());
    }

    // —— 生成 ——

    // 跨天（或首日）重新生成一批：各行各业各出一件委托（按设施范围），通用板（任务板/镇长府）出击杀+收集
    private void 检查刷新()
    {
        if (档案.日常生成日 == 档案.游戏天数) return;   // 当天已生成，不重复刷新
        var 列表 = new List<日常任务>();
        var 装备 = 生成收集("装备", "铁匠铺"); if (装备 != null) 列表.Add(装备);
        var 食物 = 生成收集("食物", "厨房"); if (食物 != null) 列表.Add(食物);
        var 药剂 = 生成收集("药剂", "药房"); if (药剂 != null) 列表.Add(药剂);
        var 击杀 = 生成通用击杀(); if (击杀 != null) 列表.Add(击杀);
        var 收集 = 生成通用收集(); if (收集 != null) 列表.Add(收集);
        档案.覆写日常(列表);
        事件.发布(new 日志事件(日志类型.任务, "新的一天，各行各业的委托栏都贴出了新的活儿。"));
        事件.发布(new 日常任务变更事件());
    }

    // 按设施范围生成收集悬赏：目标取自该范围配方涉及的材料/成品（服务设施的内容定义）
    private 日常任务 生成收集(string 范围, string 出处)
    {
        var 相关 = 相关物品(范围);
        if (相关.Count == 0) return null;
        var 键 = new List<string>(相关);
        var 标识 = 键[UnityEngine.Random.Range(0, 键.Count)];
        if (!数据.物品.TryGetValue(标识, out var 物)) return null;
        int 数量 = UnityEngine.Random.Range(2, 5);
        int 金币 = Mathf.Max(物.价格 * 数量, 40);
        return new 日常任务
        {
            标识 = $"日常_{范围}_{档案.游戏天数}",
            名称 = $"{出处}的委托：{物.名称}",
            描述 = $"缴付 {物.名称} ×{数量}，凑齐后回 {出处} 委托栏提交，一手交货一手拿钱。",
            范围 = 范围, 目标类型 = "收集", 目标标识 = 物.标识, 目标数量 = 数量,
            奖励金币 = 金币, 奖励经验 = 3 * 数量
        };
    }

    // 通用板·击杀悬赏
    private 日常任务 生成通用击杀()
    {
        var 敌人标识 = 随机键(数据.敌人);
        if (敌人标识 == null) return null;
        var 敌 = 数据.敌人[敌人标识];
        int 数量 = Mathf.Clamp(档案.等级 / 3 + 2, 2, 6);
        数量 = UnityEngine.Random.Range(2, 数量 + 1);
        int 金币 = Mathf.Max(敌.货币奖励, 20) * 数量;
        int 经验 = Mathf.Max(敌.经验奖励, 2) * 数量;
        return new 日常任务
        {
            标识 = $"日常_通用_击杀_{档案.游戏天数}",
            名称 = $"悬赏·猎杀 {敌.名称}",
            描述 = $"击杀 {敌.名称} ×{数量}，见血见银，回委托栏提交结算。",
            范围 = "通用", 目标类型 = "击杀", 目标标识 = 敌.标识, 目标数量 = 数量,
            奖励金币 = 金币, 奖励经验 = 经验
        };
    }

    // 通用板·收集悬赏（任何可得的食物/材料）
    private 日常任务 生成通用收集()
    {
        var 可采 = new List<物品数据>();
        foreach (var 物 in 数据.物品.Values)
            if (物.价格 > 0 && (物.类型 == "食物" || 物.类型 == "材料"))
                可采.Add(物);
        if (可采.Count == 0) return null;
        int 数量 = UnityEngine.Random.Range(2, 5);
        var 选中 = 可采[UnityEngine.Random.Range(0, 可采.Count)];
        int 金币 = Mathf.Max(选中.价格 * 数量, 40);
        return new 日常任务
        {
            标识 = $"日常_通用_收集_{档案.游戏天数}",
            名称 = $"收购 {选中.名称}",
            描述 = $"缴付 {选中.名称} ×{数量}，凑齐后在委托栏提交。",
            范围 = "通用", 目标类型 = "收集", 目标标识 = 选中.标识, 目标数量 = 数量,
            奖励金币 = 金币, 奖励经验 = 3 * 数量
        };
    }

    // 该范围配方涉及的全部物品（材料/产物）——即该设施会打交道的货
    private HashSet<string> 相关物品(string 范围)
    {
        var 相关 = new HashSet<string>();
        foreach (var 配方 in 数据.配方.Values)
            if (配方.类型 == 范围)
            {
                if (!string.IsNullOrEmpty(配方.产物)) 相关.Add(配方.产物);
                if (配方.材料 != null)
                    foreach (var 材 in 配方.材料) 相关.Add(材.物品);
            }
        return 相关;
    }

    private 物品数据 目标物品(日常任务 日常)
    {
        数据.物品.TryGetValue(日常.目标标识, out var 物);
        return 物;
    }

    // 从字典随机取一个键（空返回 null）
    private static string 随机键<T>(Dictionary<string, T> 表)
    {
        if (表 == null || 表.Count == 0) return null;
        var 键 = new List<string>(表.Keys);
        return 键[UnityEngine.Random.Range(0, 键.Count)];
    }
}