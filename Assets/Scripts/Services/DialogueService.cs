using System.Collections.Generic;
using UnityEngine;

    // 对话引擎：数据驱动推进剧情。进入节点 → 应用效果 → 发布 显示剧情事件；
    // 用户点选项 → StoryController 回调 处理选项(目标) → 推进。
    public sealed class DialogueService
    {
        private readonly EventBus 事件;
        private readonly DataService 数据;
        private readonly PlayerService 玩家;

        private 剧情节点 当前节点;

        // 当前显示缓存：StoryController 绑定时先拉取，避免错过装配期的初始事件
        public 显示剧情事件 当前显示 { get; private set; }

        public DialogueService(EventBus 事件, DataService 数据, PlayerService 玩家)
        {
            this.事件 = 事件;
            this.数据 = 数据;
            this.玩家 = 玩家;
        }

        public void 进入节点(string 节点标识)
        {
            if (!数据.剧情.TryGetValue(节点标识, out var 节点))
            {
                Debug.LogError($"[DialogueService] 节点不存在: {节点标识}");
                return;
            }
            当前节点 = 节点;
            玩家.档案.当前节点 = 节点标识;

            // 进入效果（生命/金币/物品等）
            if (节点.效果 != null)
                效果结算.应用(事件, 玩家.档案, 节点.效果);

            // 构建选项（数据载体，不含委托）
            var 选项列表 = new List<剧情选项数据>();
            if (节点.选项 != null)
                foreach (var 选项 in 节点.选项)
                {
                    // 条件分支：需持有物品才显示
                    if (!string.IsNullOrEmpty(选项.需要物品) && !玩家.档案.持有物品(选项.需要物品)) continue;
                    选项列表.Add(new 剧情选项数据(选项.文本, 选项.目标));
                }

            // 缓存并发布显示事件，StoryController 订阅渲染
            当前显示 = new 显示剧情事件(节点.文本 ?? "", 选项列表.ToArray());
            事件.发布(当前显示);
        }

        // 处理选项目标：节点跳转 / __结束 / 战斗 / 设施 / 探索 等（只发事件，UI 管理器订阅切换面板）
        public void 处理选项(string 目标)
        {
            if (目标 == "__结束") { 事件.发布(new 日志事件(日志类型.系统, "序章结束。")); 事件.发布(new 打开结局事件()); return; }
            if (目标.StartsWith("战斗:"))
            {
                // 战斗:敌人标识:胜利节点 —— 开始战斗并让 UI 打开战斗面板
                var 部分 = 目标.Split(':');
                if (部分.Length >= 3)
                {
                    ServiceRegistry.Get<BattleService>().开始战斗(部分[1], 部分[2], 玩家.档案.当前节点);
                    事件.发布(new 打开战斗事件(玩家.档案.当前节点));
                }
                return;
            }
            if (目标.StartsWith("设施:"))
            {
                // 设施:标识 —— 让 UI 打开对应设施面板
                事件.发布(new 打开设施事件(目标.Substring(3), 玩家.档案.当前节点));
                return;
            }
            if (目标.StartsWith("探索:"))
            {
                // 探索:区域标识:返回节点 —— 进入区域并让 UI 打开探索面板
                var 部分 = 目标.Split(':');
                if (部分.Length >= 3)
                {
                    ServiceRegistry.Get<探索服务>().进入区域(部分[1], 部分[2]);
                    事件.发布(new 打开探索事件(部分[2]));
                }
                return;
            }
            if (目标.StartsWith("区域:"))
            {
                // 区域:区域标识 —— 回到区域继续探索
                ServiceRegistry.Get<探索服务>().回到区域();
                事件.发布(new 打开探索事件(ServiceRegistry.Get<探索服务>().返回节点));
                return;
            }
            if (目标.StartsWith("地图:"))
            {
                // 地图:大地图 —— 打开大地图（世界导航）；地图:小地图 —— 返回当前城镇的小地图
                var 地图目标 = 目标.Length > 3 ? 目标.Substring(3) : "";
                if (地图目标 == "小地图") ServiceRegistry.Get<地图服务>().回到城镇地图();
                else ServiceRegistry.Get<地图服务>().打开大地图(string.IsNullOrEmpty(地图目标) ? "灰烬镇" : 地图目标);
                return;
            }
            进入节点(目标);
        }
    }
