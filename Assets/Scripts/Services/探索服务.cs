using System.Collections.Generic;
using UnityEngine;

    // 探索事件类型
    public enum 探索事件类型 { 遭遇敌人, 发现地点, 获得资源, 无事发生 }

    // 探索服务：进入区域反复深入探索，随机触发事件（权重表）
    public sealed class 探索服务
    {
        private readonly EventBus 事件;
        private readonly DataService 数据;
        private readonly PlayerService 玩家;
        private readonly BattleService 战斗;

        private 区域数据 当前区域;
        public string 返回节点 { get; private set; }
        public string 当前区域标识 { get; private set; }

        // 当前显示缓存：探索控制器绑定时先拉取
        public 探索显示事件 当前显示 { get; private set; }

        public 探索服务(EventBus 事件, DataService 数据, PlayerService 玩家, BattleService 战斗)
        {
            this.事件 = 事件; this.数据 = 数据; this.玩家 = 玩家; this.战斗 = 战斗;
        }

        // 进入区域：显示描述 + 危险度 + 深入/返回
        public void 进入区域(string 区域标识, string 返回节点)
        {
            if (!数据.区域.TryGetValue(区域标识, out 当前区域)) { Debug.LogError($"[探索服务] 区域不存在: {区域标识}"); return; }
            this.返回节点 = 返回节点;
            当前区域标识 = 区域标识;
            显示(当前区域.描述 + "\n\n" + 危险度文本(当前区域.危险度));
        }

        // 从地点节点回到区域继续探索（不改变返回节点）
        public void 回到区域()
        {
            if (当前区域 == null) return;
            显示(当前区域.描述);
        }

        // 深入探索一步：抽取事件并执行
        public void 深入探索() => 执行事件(抽取事件());

        // 探索战斗胜利/逃跑后回调：回探索界面继续
        public void 战斗胜利() => 显示("你喘了口气，握紧武器继续深入。");
        public void 战斗逃跑() => 显示("你退回安全处，惊魂未定。");

        // 显示探索界面内容
        private void 显示(string 文本)
        {
            当前显示 = new 探索显示事件(文本, new[] { new 探索选项数据("深入探索", "深入"), new 探索选项数据("返回", "返回") });
            事件.发布(当前显示);
        }

        // 权重随机抽取事件（复用旧 区域探索 逻辑）
        private (探索事件类型, string, string, string, 剧情效果) 抽取事件()
        {
            var 候选 = new List<(探索事件类型, string, string, string, 剧情效果, int)>();
            if (当前区域.遭遇 != null) foreach (var e in 当前区域.遭遇) if (!string.IsNullOrEmpty(e.敌人)) 候选.Add((探索事件类型.遭遇敌人, null, e.敌人, null, null, e.权重));
            if (当前区域.发现 != null) foreach (var d in 当前区域.发现) if (!string.IsNullOrEmpty(d.节点)) 候选.Add((探索事件类型.发现地点, null, null, d.节点, null, d.权重));
            if (当前区域.资源 != null) foreach (var r in 当前区域.资源) if (r.效果 != null) 候选.Add((探索事件类型.获得资源, r.文本, null, null, r.效果, r.权重));
            if (当前区域.无事文本 != null) foreach (var t in 当前区域.无事文本) 候选.Add((探索事件类型.无事发生, t, null, null, null, 1));
            if (候选.Count == 0) return (探索事件类型.无事发生, "这里什么也没有。", null, null, null);

            int 总权重 = 0; foreach (var c in 候选) 总权重 += Mathf.Max(1, c.Item6);
            int 掷点 = Random.Range(0, 总权重);
            foreach (var c in 候选) { 掷点 -= Mathf.Max(1, c.Item6); if (掷点 < 0) return (c.Item1, c.Item2, c.Item3, c.Item4, c.Item5); }
            return (候选[候选.Count - 1].Item1, 候选[候选.Count - 1].Item2, 候选[候选.Count - 1].Item3, 候选[候选.Count - 1].Item4, 候选[候选.Count - 1].Item5);
        }

        // 执行事件：遭遇→开战斗；发现→进剧情节点；资源→应用效果；无事→显示
        private void 执行事件((探索事件类型, string, string, string, 剧情效果) 事件项)
        {
            switch (事件项.Item1)
            {
                case 探索事件类型.遭遇敌人:
                    战斗.开始战斗(事件项.Item3, "__探索胜利", "__探索返回");
                    事件.发布(new 探索显示事件($"你遇上了{数据.敌人[事件项.Item3].名称}！", new[] { new 探索选项数据("进入战斗", "战斗"), new 探索选项数据("落荒而逃", "返回") }));
                    break;
                case 探索事件类型.发现地点:
                    ServiceRegistry.Get<DialogueService>().进入节点(事件项.Item4);
                    break;
                case 探索事件类型.获得资源:
                    效果结算.应用(事件, 玩家.档案, 事件项.Item5);
                    显示(事件项.Item2);
                    break;
                default:
                    显示(事件项.Item2);
                    break;
            }
        }

        private static string 危险度文本(int 危险度)
            => 危险度 switch { 1 => "<color=#7fae6a>危险度：低</color>", 2 => "<color=#d9a441>危险度：中</color>", _ => "<color=#c7473d>危险度：高</color>" };
    }
