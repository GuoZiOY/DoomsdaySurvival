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

            // 主线阶段更新（剧情推进标记；阶段前进后检查「主线阶段」类任务）
            if (!string.IsNullOrEmpty(节点.主线阶段) && 玩家.档案.主线阶段 != 节点.主线阶段)
            {
                玩家.档案.主线阶段 = 节点.主线阶段;
                ServiceRegistry.Get<QuestService>().检查阶段任务();
                ServiceRegistry.Get<自动存档器>()?.保存(true);   // 主线阶段推进 = 关键存档点
            }

            // 进入效果（生命/金币/物品等；另含 学习技能/接取任务 扩展）
            if (节点.效果 != null)
            {
                效果结算.应用(事件, 玩家.档案, 节点.效果);
                if (!string.IsNullOrEmpty(节点.效果.学习技能))
                    ServiceRegistry.Get<技能服务>().尝试学习(节点.效果.学习技能);
                if (!string.IsNullOrEmpty(节点.效果.接取任务))
                    ServiceRegistry.Get<QuestService>().接取(节点.效果.接取任务);
            }

            // 构建选项（数据载体，不含委托）
            var 选项列表 = new List<剧情选项数据>();
            if (节点.选项 != null)
                foreach (var 选项 in 节点.选项)
                {
                    // 条件分支：需持有物品才显示
                    if (!string.IsNullOrEmpty(选项.需要物品) && !玩家.档案.持有物品(选项.需要物品)) continue;
                    选项列表.Add(new 剧情选项数据(选项.文本, 选项.目标));
                }

            // 缓存并发布显示事件，StoryController 订阅渲染；无选项时自动进入 下一节点（剧情链连续播放）
            当前显示 = new 显示剧情事件(节点.文本 ?? "", 选项列表.ToArray(), 选项列表.Count == 0 ? (节点.下一节点 ?? "") : "");
            事件.发布(当前显示);
        }

        // 处理选项目标：节点跳转 / __结束 / 战斗 / 设施 / 探索 等（只发事件，UI 管理器订阅切换面板）
        public void 处理选项(string 目标)
        {
            if (目标 == "__结束") { 事件.发布(new 日志事件(日志类型.系统, "序章结束。")); 事件.发布(new 打开结局事件()); return; }
            if (目标.StartsWith("战斗:"))
            {
                // 战斗:敌人组标识:胜利节点[:助战组] —— 开始战斗 内部会先激活战斗面板
                var 部分 = 目标.Split(':');
                if (部分.Length >= 3)
                {
                    string 助战组 = 部分.Length >= 4 ? 部分[3] : "";
                    ServiceRegistry.Get<BattleService>().开始战斗(部分[1], 部分[2], 玩家.档案.当前节点, 0, 助战组);
                }
                return;
            }
            if (目标.StartsWith("设施:"))
            {
                // 设施:标识 —— 让 UI 打开对应设施面板
                事件.发布(new 打开设施事件(目标.Substring(3), 玩家.档案.当前节点));
                return;
            }
            // 旧的「探索:区域:返回节点」「区域:标识」两条指令随 文本探索（探索服务 / 探索面板）一起移除：
            // 荒野节点以后交给 **区域网格**（区域探索服务）接管，剧情里不要再写这两条。
            if (目标.StartsWith("地图:"))
            {
                // 地图:大地图[:世界标识] —— 打开大世界（100×100 格子网格）。
                // v51 起**没有"节点"这个概念了**：方括号位置给的是**世界标识**（Data/世界.json），
                // 留空就用默认世界。「地图:小地图」随 城镇小地图 一起删掉了，剧情里不要再写。
                var 部分 = 目标.Split(':');
                string 世界标识 = 部分.Length >= 3 ? 部分[2] : "";
                ServiceRegistry.Get<大世界探索服务>()?.打开默认世界(世界标识);
                return;
            }
            进入节点(目标);
        }
    }
