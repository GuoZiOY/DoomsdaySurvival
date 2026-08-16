
    // 任务服务：接取 / 记录击败 / 记录获得；完成时发放奖励并发 任务完成事件
    public sealed class QuestService
    {
        private readonly EventBus 事件;
        private readonly DataService 数据;
        private readonly PlayerService 玩家服务;
        private 玩家档案 档案 => 玩家服务.档案;   // 动态取当前档案

        public QuestService(EventBus 事件, DataService 数据, PlayerService 玩家服务) { this.事件 = 事件; this.数据 = 数据; this.玩家服务 = 玩家服务; }

        // 接取任务：重复接取返回 false
        public bool 接取(string 任务标识)
        {
            if (!数据.任务.TryGetValue(任务标识, out var 任务)) return false;
            if (!档案.添加任务(任务标识)) return false;
            事件.发布(new 任务进度事件(任务标识, 0, 任务.目标数量, false));
            return true;
        }

        // 记录一次击败：推进「击败」类任务
        public void 记录击败(string 敌人标识)
        {
            foreach (var 进度 in 档案.任务)
            {
                if (进度.已完成 || !数据.任务.TryGetValue(进度.标识, out var 任务)) continue;
                if (任务.目标类型 != "击败" || 任务.目标标识 != 敌人标识) continue;
                进度.数量++;
                if (进度.数量 >= 任务.目标数量) { 进度.已完成 = true; 发放奖励(进度.标识, 任务); }
                事件.发布(new 任务进度事件(进度.标识, 进度.数量, 任务.目标数量, 进度.已完成));
            }
        }

        // 记录一次获得：推进「获得物品」类任务
        public void 记录获得(string 物品标识)
        {
            foreach (var 进度 in 档案.任务)
            {
                if (进度.已完成 || !数据.任务.TryGetValue(进度.标识, out var 任务)) continue;
                if (任务.目标类型 != "获得物品" || 任务.目标标识 != 物品标识) continue;
                进度.数量++;
                if (进度.数量 >= 任务.目标数量) { 进度.已完成 = true; 发放奖励(进度.标识, 任务); }
                事件.发布(new 任务进度事件(进度.标识, 进度.数量, 任务.目标数量, 进度.已完成));
            }
        }

        // 发放任务奖励：金币/经验/物品
        private void 发放奖励(string 任务标识, 任务数据 任务)
        {
            档案.金币 += 任务.奖励金币;
            if (!string.IsNullOrEmpty(任务.奖励物品)) 档案.添加物品(任务.奖励物品);
            bool 升级了 = 档案.获得经验(任务.奖励经验);
            事件.发布(new 金币变化事件(档案.金币, 任务.奖励金币));
            事件.发布(new 任务完成事件(任务标识, $"{任务.名称} 完成！+{任务.奖励金币}金 +{任务.奖励经验}经验"));
            if (升级了) 事件.发布(new 经验变化事件(档案.等级, 档案.经验, 档案.升级所需经验, true));
        }
    }
