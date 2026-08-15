using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 主视窗面板：剧情/战斗/探索/结局 四种模式的呈现（共用 标题/正文/选项区）。
// 打开由 面板管理器 路由（刷新 按事件类型渲染）；本面板只订阅战斗中的实时内容事件。
public sealed class 主视窗面板 : 面板基类
{
    [SerializeField] private TMP_Text 标题;
    [SerializeField] private TMP_Text 正文;
    [SerializeField] private RectTransform 选项区;
    [SerializeField] private Button 返回主菜单;   // 结局时显示的返回按钮
    [SerializeField] private 主菜单面板 主菜单;    // 返回主菜单 的跳转目标

    void Awake()
    {
        if (返回主菜单 != null) 返回主菜单.onClick.AddListener(() => 面板管理器.实例.显示(主菜单));
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<战斗消息事件>(e => 设文本(正文, e.文本));
        事件.订阅<战斗结束事件>(处理战斗结束);
    }

    // 面板管理器 路由后调用：按事件类型渲染对应模式
    protected override void 刷新(object 上下文)
    {
        switch (上下文)
        {
            case 显示剧情事件 剧情: 渲染剧情(剧情); break;
            case 打开战斗事件: 渲染战斗(); break;
            case 探索显示事件 探索: 渲染探索(探索); break;
            case 打开结局事件: 渲染结局(); break;
        }
    }

    // ===== 剧情（选项只服务剧情分支）=====

    private void 渲染剧情(显示剧情事件 e)
    {
        设标题("剧情", 游戏主题.金色);
        设文本(正文, e.文本);
        返回主菜单?.gameObject.SetActive(false);
        清空(选项区);
        if (e.选项 == null) return;
        foreach (var 选项 in e.选项)
        {
            var 目标 = 选项.目标;
            创建行(选项区, 选项.文本, () => ServiceRegistry.Get<DialogueService>().处理选项(目标));
        }
    }

    // ===== 战斗 =====

    private void 渲染战斗()
    {
        设标题("战斗", 游戏主题.危险);
        设文本(正文, ServiceRegistry.Get<BattleService>().当前消息);
        返回主菜单?.gameObject.SetActive(false);
        显示主行动();
    }

    private void 显示主行动()
    {
        清空(选项区);
        创建行(选项区, "攻击", () => ServiceRegistry.Get<BattleService>().玩家攻击());
        创建行(选项区, "技能", 显示技能页);
        创建行(选项区, "道具", 显示道具页);
        创建行(选项区, "逃跑", () => ServiceRegistry.Get<BattleService>().逃跑());
    }

    private void 显示技能页()
    {
        清空(选项区);
        var 玩家 = ServiceRegistry.Get<PlayerService>().档案;
        var 数据 = ServiceRegistry.Get<DataService>();
        foreach (var 掌握 in 玩家.已学技能)
        {
            if (!数据.技能.TryGetValue(掌握.标识, out var 技能)) continue;
            if (玩家.魔力 < 技能.消耗魔力) continue;
            var 标识 = 掌握.标识;
            创建行(选项区, $"{品质工具.标签(技能.品质)}{技能.名称}（{技能.消耗魔力}MP 熟练{玩家.技能熟练等级(标识)}）", () => ServiceRegistry.Get<BattleService>().玩家技能(标识));
        }
        创建行(选项区, "返回", 显示主行动);
    }

    private void 显示道具页()
    {
        清空(选项区);
        var 玩家 = ServiceRegistry.Get<PlayerService>().档案;
        var 数据 = ServiceRegistry.Get<DataService>();
        foreach (var 堆叠 in 玩家.背包)
        {
            if (堆叠.数量 <= 0) continue;
            if (!数据.物品.TryGetValue(堆叠.标识, out var 物品) || 物品.类型 != "恢复") continue;
            var 标识 = 堆叠.标识;
            创建行(选项区, $"使用{物品.名称}（×{堆叠.数量}）", () => ServiceRegistry.Get<BattleService>().玩家道具(标识));
        }
        创建行(选项区, "返回", 显示主行动);
    }

    private void 处理战斗结束(战斗结束事件 e)
    {
        var 服务 = ServiceRegistry.Get<BattleService>();
        var 节点 = e.胜利 ? 服务.胜利节点 : 服务.返回节点;
        if (节点 == "__探索胜利") { ServiceRegistry.Get<探索服务>().战斗胜利(); return; }
        if (节点 == "__探索返回") { ServiceRegistry.Get<探索服务>().战斗逃跑(); return; }
        ServiceRegistry.Get<DialogueService>().进入节点(节点);
    }

    // ===== 探索 =====

    private void 渲染探索(探索显示事件 e)
    {
        设标题("探索", 游戏主题.金色);
        设文本(正文, e.文本);
        返回主菜单?.gameObject.SetActive(false);
        清空(选项区);
        if (e.选项 == null) return;
        foreach (var 选项 in e.选项)
        {
            var 动作 = 选项.动作;
            创建行(选项区, 选项.文本, () => 处理探索动作(动作));
        }
    }

    private void 处理探索动作(string 动作)
    {
        var 探索 = ServiceRegistry.Get<探索服务>();
        switch (动作)
        {
            case "深入": 探索.深入探索(); break;
            case "战斗": 显示主行动(); break;
            case "返回": ServiceRegistry.Get<DialogueService>().进入节点(探索.返回节点); break;
        }
    }

    // ===== 结局 =====

    private void 渲染结局()
    {
        设标题("—— 序章 · 完 ——", 游戏主题.金色);
        设文本(正文, "灰烬镇的余烬还在燃烧。龙在山的深处沉睡。\n\n你的故事，才刚刚开始。");
        清空(选项区);
        创建行(选项区, "踏入灰烬镇（开放大地图）", () => ServiceRegistry.Get<地图服务>().打开大地图("灰烬镇"));
        返回主菜单?.gameObject.SetActive(true);
    }

    private void 设标题(string 文本, Color 颜色)
    {
        if (标题 == null) return;
        标题.text = 文本;
        标题.color = 颜色;
    }
}
