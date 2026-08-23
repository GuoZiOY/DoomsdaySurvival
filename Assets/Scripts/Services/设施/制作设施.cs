using System.Collections.Generic;
using UnityEngine;

// 制作功能接口：按配方用材料制作新物品（铁匠铺=装备 / 厨房=食物 / 药房=药剂）
public interface 制作功能
{
    配方数据[] 可用配方();          // 本设施类型 + 图纸已解锁
    bool 尝试制作(string 配方标识);
    bool 材料足够(配方数据 配方);   // 面板显示可制作状态
    string 材料文本(配方数据 配方); // 面板材料摘要
    string 类型提示();               // 设施类型名（面板标题用）
}

// 制作设施：制作功能（图纸解锁 + 材料检查 → 扣材料 → 得产物）+ 买卖功能（收购 本类型相关的 材料/成品/图纸）
// 类型（装备/食物/药剂）由 设施工厂 从 facilities.json 的 数据 字段注入
public sealed class 制作设施 : 设施逻辑, 制作功能, 买卖功能
{
    public string 类型 = "";   // "装备" / "食物" / "药剂"

    private 买卖逻辑 买卖助手;
    private 买卖逻辑 买卖 => 买卖助手 ??= new 买卖逻辑(玩家, 数据, 事件);

    public 配方数据[] 可用配方()
    {
        var 列表 = new List<配方数据>();
        foreach (var 配方 in 数据.配方.Values)
        {
            if (配方.类型 != 类型) continue;
            if (!string.IsNullOrEmpty(配方.图纸) && !玩家.持有物品(配方.图纸)) continue;   // 图纸解锁
            列表.Add(配方);
        }
        return 列表.ToArray();
    }

    public bool 尝试制作(string 配方标识)
    {
        if (!数据.配方.TryGetValue(配方标识, out var 配方) || 配方.类型 != 类型) return false;
        if (!string.IsNullOrEmpty(配方.图纸) && !玩家.持有物品(配方.图纸))
        {
            事件.发布(new 日志事件(日志类型.反馈坏, $"缺少图纸，无法制作 {配方.名称}。"));
            return false;
        }
        foreach (var 材 in 配方.材料)
            if (玩家.物品数量(材.物品) < 材.数量)
            {
                事件.发布(new 日志事件(日志类型.反馈坏, $"材料不足：还缺 {((数据.物品.TryGetValue(材.物品, out var 物)) ? 物.名称 : 材.物品)}×{材.数量 - 玩家.物品数量(材.物品)}。"));
                return false;
            }
        // 扣材料
        foreach (var 材 in 配方.材料)
        {
            玩家.移除物品(材.物品, 材.数量);
            事件.发布(new 背包变化事件(材.物品, -材.数量, 变化原因.消耗));
        }
        // 得产物：装备产物 → 每件随机词缀实例；非装备 → 普通堆叠
        string 产物名 = "产物";
        var 成品 = 数据.物品.TryGetValue(配方.产物, out var 成) ? 成 : null;
        if (成品 != null) 产物名 = 成品.名称;
        for (int 序 = 0; 序 < Mathf.Max(1, 配方.产物数量); 序++)
        {
            var 词缀 = 成品 != null ? ServiceRegistry.Get<词缀服务>()?.生成(成品) : null;
            if (词缀 != null) 玩家.添加堆叠(new 物品堆叠(配方.产物, 1) { 词缀 = 词缀 });
            else 玩家.添加物品(配方.产物, 1);
        }
        事件.发布(new 背包变化事件(配方.产物, 配方.产物数量, 变化原因.获得));
        事件.发布(new 日志事件(日志类型.反馈, $"制作完成：{产物名} ×{配方.产物数量}！"));
        return true;
    }

    public bool 材料足够(配方数据 配方)
    {
        if (配方.材料 == null) return true;
        foreach (var 材 in 配方.材料)
            if (玩家.物品数量(材.物品) < 材.数量) return false;
        return true;
    }

    public string 材料文本(配方数据 配方)
    {
        if (配方.材料 == null) return "";
        var 段 = new List<string>();
        foreach (var 材 in 配方.材料)
        {
            string 名 = 数据.物品.TryGetValue(材.物品, out var 物) ? 物.名称 : 材.物品;
            段.Add($"{名}×{材.数量}");
        }
        return string.Join("  ", 段);
    }

    public string 类型提示() => 类型 switch { "装备" => "打造装备", "食物" => "烹饪食物", _ => "酿制药剂" };

    // —— 买卖功能：出售/收购 本类型配方 相关的 材料/成品/图纸 ——

    // 本类型配方涉及的全部物品（材料/产物/图纸）
    private HashSet<string> 相关物品()
    {
        var 相关 = new HashSet<string>();
        foreach (var 配方 in 数据.配方.Values)
            if (配方.类型 == 类型)
            {
                if (!string.IsNullOrEmpty(配方.产物)) 相关.Add(配方.产物);
                if (!string.IsNullOrEmpty(配方.图纸)) 相关.Add(配方.图纸);
                if (配方.材料 != null)
                    foreach (var 材 in 配方.材料) 相关.Add(材.物品);
            }
        return 相关;
    }

    // 可购：出售 本类型相关的 材料/成品/图纸（价格>0 才可购买）
    public 物品数据[] 可购商品()
    {
        var 相关 = 相关物品();
        return 买卖.可购商品(物品 => 相关.Contains(物品.标识) && 物品.价格 > 0);
    }
    public bool 尝试购买(string 物品标识) => 买卖.尝试购买(物品标识);

    public 物品堆叠[] 可卖物品()
    {
        var 相关 = 相关物品();
        return 买卖.可卖物品(物品 => 相关.Contains(物品.标识));
    }

    public bool 尝试卖出(string 物品标识) => 买卖.尝试卖出(物品标识);
}
