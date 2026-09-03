
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
            事件.发布(new 日志事件(日志类型.系统, $"接取了任务：{任务.名称}"));
            return true;
        }

        // 记录一次击败：推进「击败」类任务 + 悬赏「击杀」类
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

        // 检查「主线阶段」类任务：主线阶段前进后调用；达到目标阶段即完成
        public void 检查阶段任务()
        {
            // 主线任务自动纳入：阶段达到目标即自动接取（无需逐节点剧情指定），随后照常完成发奖
            自动纳入主线();
            foreach (var 进度 in 档案.任务)
            {
                if (进度.已完成 || !数据.任务.TryGetValue(进度.标识, out var 任务)) continue;
                if (任务.目标类型 != "主线阶段" || !主线阶段工具.达到(档案.主线阶段, 任务.目标标识)) continue;
                进度.数量 = 任务.目标数量;
                进度.已完成 = true;
                发放奖励(进度.标识, 任务);
                事件.发布(new 任务进度事件(进度.标识, 进度.数量, 任务.目标数量, true));
            }
        }

        // 主线任务随主线阶段自动接取：所有「主线_」前缀且目标为「主线阶段」的任务，
        // 一旦玩家主线阶段达到其目标阶段即自动纳入任务轨道（幂等，已在轨道则跳过）。
        private void 自动纳入主线()
        {
            foreach (var 任务 in 数据.任务.Values)
            {
                if (任务.标识 == null || !任务.标识.StartsWith("主线_")) continue;
                if (任务.目标类型 != "主线阶段") continue;
                if (!主线阶段工具.达到(档案.主线阶段, 任务.目标标识)) continue;   // 未到目标阶段，暂不纳入
                if (!档案.添加任务(任务.标识)) continue;   // 已在轨道则跳过（幂等）
                事件.发布(new 任务进度事件(任务.标识, 0, 任务.目标数量, false));
            }
        }

        // 任务是否已完成（区域剧情路由 需要任务 条件用）
        public bool 任务已完成(string 任务标识)
        {
            if (string.IsNullOrEmpty(任务标识)) return false;
            foreach (var 进度 in 档案.任务)
                if (进度.标识 == 任务标识) return 进度.已完成;
            return false;
        }

        // 发放任务奖励：物资/经验（末日以物易物：奖励物品 + 价值额度，无金币）
        private void 发放奖励(string 任务标识, 任务数据 任务)
        {
            // 奖励物品入背包（等价值物优先）
            if (!string.IsNullOrEmpty(任务.奖励物品)) 档案.添加物品(任务.奖励物品);
            // 奖励价值 → 换通用补给（面包/水，价值 1 的等价物）
            int 奖励价值 = 任务.奖励金币 > 0 ? 任务.奖励金币 : 0;
            bool 升级了 = 档案.获得经验(任务.奖励经验);
            string 奖励文本 = "";
            if (!string.IsNullOrEmpty(任务.奖励物品))
            {
                string 名 = 数据.物品.TryGetValue(任务.奖励物品, out var 物) ? 物.标识 : 任务.奖励物品;
                奖励文本 += $"+{名}";
            }
            if (奖励价值 > 0)
            {
                int 面包数 = 奖励价值 / 2 + 1;
                档案.添加物品("面包", 面包数);
                奖励文本 += (奖励文本.Length > 0 ? " " : "") + $"+面包×{面包数}";
            }
            if (任务.奖励经验 > 0) 奖励文本 += (奖励文本.Length > 0 ? " " : "") + $"+{任务.奖励经验}经验";
            事件.发布(new 任务完成事件(任务标识, $"{任务.名称} 完成！{奖励文本}".TrimEnd()));
            事件.发布(new 日志事件(日志类型.获得, $"{任务.名称} 完成！{奖励文本}"));
            if (升级了) 事件.发布(new 经验变化事件(档案.等级, 档案.经验, 档案.升级所需经验, true));
        }
    }
