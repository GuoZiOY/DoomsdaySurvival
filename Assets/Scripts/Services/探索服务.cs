using System.Collections.Generic;
using UnityEngine;

    // 探索服务：深度分层区域状态机（层[] 数据驱动）+ 行动点系统（末日版）。
    // 核心循环：进入 → 层1 → 反复[搜索]（耗行动点 10）→ 事件（物资/战斗/幸存者/无事）→ 深入 → … → 深处层 → 搜空（可再刷）。
    // 张力：生命/饱食/水分/行动点 带入不恢复 → "再搜 or 见好就收"；行动点 0 只能返回。
    // 天气影响：雨=噪音降低遇敌减少；雾=潜行加成；沙暴=搜索效率↓受伤↑；酷暑=行动点消耗↑。
    public sealed class 探索服务
    {
        private readonly EventBus 事件;
        private readonly DataService 数据;
        private readonly PlayerService 玩家服务;
        private readonly BattleService 战斗;
        private 玩家档案 档案 => 玩家服务.档案;

        // 搜索消耗行动点（定稿：搜刮一处 -10）
        public const int 搜索消耗 = 10;

        // —— 状态 ——
        private 区域数据 当前区域;
        private int 层索引 = -1;
        private 探索事件表 当前事件表;   // 岔路层选定后锁定；null=未定（普通层直接用 层.事件表）
        private int 搜索次数;
        public string 返回节点 { get; private set; }   // 撤离回来源（大地图）
        private string 遭遇组;           // 遭遇待处理；null=无
        private int 遭遇先手;            // 战斗先手（0速度/1敌方/2我方）
        private bool 待遭遇计数;         // 遭遇战后才计数本次搜索
        private 资源项 当前资源;         // 资源事件待确认 [是][否]
        private bool 通路后深入;         // 通路事件触发战斗，胜利后深入
        private 选择事件 当前选择;       // 选择类事件（选择:索引）

        public string 当前区域标识 { get; private set; }
        public 探索显示事件 当前显示 { get; private set; }

        public 探索服务(EventBus 事件, DataService 数据, PlayerService 玩家, BattleService 战斗)
        {
            this.事件 = 事件; this.数据 = 数据; this.玩家服务 = 玩家; this.战斗 = 战斗;
        }

        // 当前天气（探索影响）
        private 天气类型 当前天气 => (天气类型)档案.天气;

        // ===== 进入 / 状态 =====

        public void 进入区域(string 区域标识, string 返回节点)
        {
            if (!数据.区域.TryGetValue(区域标识, out 当前区域)) { Debug.LogError($"[探索服务] 区域不存在: {区域标识}"); return; }
            this.返回节点 = 返回节点;
            当前区域标识 = 区域标识;
            层索引 = 0;
            初始化层();
            显示当前层();
        }

        // 从剧情节点（区域:指令）回到区域，保留层状态
        public void 回到区域()
        {
            if (当前区域 == null) return;
            显示当前层();
        }

        private void 初始化层()
        {
            当前事件表 = null;
            搜索次数 = 0;
            遭遇组 = null;
            遭遇先手 = 0;
            待遭遇计数 = false;
            当前资源 = null;
            通路后深入 = false;
            当前选择 = null;
        }

        private 探索层数据 当前层 => 当前区域?.层[层索引];

        private bool 已通关 => 当前区域 != null && 档案.已通关(当前区域标识);

        // ===== 动作 =====

        // 选岔路：锁定本层事件表
        public void 选岔路(string 路)
        {
            var 岔路 = 当前层?.岔路;
            if (岔路 == null) return;
            当前事件表 = 路 == "危险" ? 岔路.危险 : 岔路.安全;
            显示(当前层.描述 + "\n\n" + (路 == "危险" ? "你选择了危险捷径。" : "你选择了安全小路。"), 探索按钮());
        }

        // 搜索：耗行动点（10）→ 深处层 / 通路判定 → 抽事件
        public void 搜索()
        {
            if (当前层 == null || 遭遇组 != null || 当前资源 != null) return;
            int 消耗 = 搜索消耗;
            // 酷暑：行动点消耗↑
            if (当前天气 == 天气类型.酷暑) 消耗 += 5;
            // 沙暴：消耗↑
            if (当前天气 == 天气类型.沙暴) 消耗 += 5;
            if (!档案.消耗行动点(消耗))
            {
                事件.发布(new 精力变化事件(档案.行动点, 档案.最大行动点, 0));
                显示("你已经筋疲力尽，再也搜不动了。");
                return;
            }
            事件.发布(new 精力变化事件(档案.行动点, 档案.最大行动点, -消耗));
            // 深处层：未搜空 → 遭遇精英/Boss；已搜空 → 普通化
            if (!string.IsNullOrEmpty(当前层.Boss))
            {
                if (已通关) { 抽事件(当前层.通关后 ?? 空表()); return; }
                遭遇(当前层.Boss, 0);
                return;
            }
            // 通路判定（非底层）：搜满阈值后每次掷通路概率 → 通路事件
            if (搜索次数 >= 当前层.搜索阈值 && 层索引 < 当前区域.层.Length - 1 && Random.value < 当前层.通路概率)
            {
                触发通路事件();
                return;
            }
            抽事件(当前事件表 ?? 当前层.事件表);
        }

        // 深入：进入下一层（通路事件解决后调用）
        public void 深入()
        {
            if (当前区域 == null) return;
            if (层索引 + 1 >= 当前区域.层.Length) return;   // 底层无深入
            层索引++;
            初始化层();
            显示当前层();
        }

        // 返回：撤离回来源（大地图），保留所得
        public void 返回()
        {
            if (当前区域 == null) return;
            当前区域 = null;
            当前区域标识 = null;
            层索引 = -1;
            ServiceRegistry.Get<地图服务>().打开大地图(返回节点);
        }

        // ===== 资源事件（发现 → [是]耗行动点+获得 / [否]略过） =====

        public void 资源是()
        {
            if (当前资源 == null) return;
            var 资源 = 当前资源;
            当前资源 = null;
            int 消耗 = Mathf.Max(1, 资源.消耗精力);
            if (!档案.消耗行动点(消耗))
            {
                事件.发布(new 精力变化事件(档案.行动点, 档案.最大行动点, 0));
                显示("你太累了，采集不动。", 探索按钮());
                return;
            }
            事件.发布(new 精力变化事件(档案.行动点, 档案.最大行动点, -消耗));
            效果结算.应用(事件, 档案, 资源.效果);
            显示(资源.文本, 探索按钮());
            事件.发布(new 日志事件(日志类型.探索, 资源.文本));
            var 摘要 = 获得摘要(资源.效果);
            if (!string.IsNullOrEmpty(摘要)) 事件.发布(new 日志事件(日志类型.探索, 摘要));
        }

        public void 资源否()
        {
            当前资源 = null;
            显示当前层();
        }

        // 生成资源/选择效果的获得摘要（物品·经验），用于探索日志播报
        private string 获得摘要(剧情效果 效果)
        {
            if (效果 == null) return null;
            var 段 = new List<string>();
            if (!string.IsNullOrEmpty(效果.获得物品)) 段.Add($"{物品名(效果.获得物品)}×{Mathf.Max(1, 效果.获得数量)}");
            if (效果.获得物品表 != null)
                foreach (var 项 in 效果.获得物品表)
                    if (!string.IsNullOrEmpty(项.标识)) 段.Add($"{物品名(项.标识)}×{Mathf.Max(1, 项.数量)}");
            if (效果.经验 > 0) 段.Add($"经验 {效果.经验}");
            return 段.Count == 0 ? null : "获得 " + string.Join("、", 段);
        }

        private string 物品名(string 标识) => 数据.物品.TryGetValue(标识, out var 物品) ? 物品.名称 : 标识;

        // ===== 遭遇（天气影响） =====

        private void 遭遇(string 敌人组, int 先手)
        {
            遭遇组 = 敌人组;
            遭遇先手 = 先手;
            待遭遇计数 = true;
            string 名称 = 数据.敌人组.TryGetValue(敌人组, out var 组) ? 组.名称 : 敌人组;
            // 雨/雷雨：噪音被浇灭，遭遇时敌先手概率降（先手 0 不变，此处只改文案）
            string 天气前缀 = "";
            if (当前天气 == 天气类型.雨 || 当前天气 == 天气类型.雷雨) 天气前缀 = "（雨声掩盖了你的脚步）";
            if (先手 == 1)   // 被偷袭：敌方先手，强制战斗
            {
                事件.发布(new 日志事件(日志类型.探索, $"你被{名称}偷袭了！"));
                战斗遭遇();
                return;
            }
            if (先手 == 2)   // 偷袭：敌方未察觉，可偷袭（我方先手）或绕开
            {
                // 潜行值决定能否偷袭（敏捷×2+意志）
                if (档案.潜行值 >= 15 || 当前天气 == 天气类型.雾 || 当前天气 == 天气类型.雨)
                    显示($"你发现了{名称}，它没有察觉到你。{天气前缀}", default, new 探索按钮数据("偷袭", "战斗"), new 探索按钮数据("绕开", "绕开"));
                else
                {
                    // 潜行不足 → 被察觉，转为正常遭遇
                    显示($"你发现了{名称}，但它也看见了你！", default, new 探索按钮数据("战斗", "战斗"), new 探索按钮数据("落荒而逃", "逃跑"));
                    遭遇先手 = 0;
                }
                return;
            }
            // 遇见
            显示($"你遇上了{名称}！{天气前缀}", default, new 探索按钮数据("战斗", "战斗"), new 探索按钮数据("落荒而逃", "逃跑"));
        }

        // 战斗/偷袭 → 开战（按遭遇先手）
        public void 战斗遭遇()
        {
            if (遭遇组 == null) return;
            var 组 = 遭遇组;
            var 先手 = 遭遇先手;
            遭遇组 = null;
            战斗.开始战斗(组, "__探索胜利", "__探索返回", 先手);
        }

        public void 逃跑遭遇()
        {
            遭遇组 = null;
            待遭遇计数 = false;
            显示("你退回安全处，惊魂未定。", 探索按钮());
        }

        // 偷袭时绕开
        public void 偷袭绕开()
        {
            遭遇组 = null;
            待遭遇计数 = false;
            显示("你悄悄绕开了它，没有惊动任何东西。", 探索按钮());
        }

        // 战斗胜利回调：计数 + 通路后深入 + Boss 通关
        public void 战斗胜利()
        {
            if (待遭遇计数) { 待遭遇计数 = false; 搜索次数++; }
            if (通路后深入) { 通路后深入 = false; 深入(); return; }
            if (当前层 != null && !string.IsNullOrEmpty(当前层.Boss) && !已通关)
            {
                档案.标记通关(当前区域标识);
                显示(当前区域.通关文案, 探索按钮());
                return;
            }
            显示("你喘了口气，握紧武器继续深入。", 探索按钮());
        }

        public void 战斗逃跑() => 逃跑遭遇();

        // ===== 通路事件 =====

        private void 触发通路事件()
        {
            string 文本 = string.IsNullOrEmpty(当前层.通路文本) ? "你发现了通往下层的道路，但出口有守卫把守。" : 当前层.通路文本;
            显示(文本, default, new 探索按钮数据("强行突破", "强行突破"), new 探索按钮数据("悄悄绕行", "悄悄绕行"));
        }

        // 强行突破：必然战斗，胜后深入
        public void 强行突破()
        {
            通路战斗(1f);
        }

        // 悄悄绕行：70% 无事直接深入；30% 战斗后深入
        public void 悄悄绕行()
        {
            通路战斗(0.3f);
        }

        private void 通路战斗(float 战斗概率)
        {
            var 组 = 随机遭遇组(当前事件表 ?? 当前层?.事件表);
            if (组 == null || Random.value >= 战斗概率)
            {
                // 无事 → 直接深入
                显示("你绕开了守卫，摸到了下一层的入口。", 探索按钮());
                深入();
                return;
            }
            // 战斗 → 胜后深入
            通路后深入 = true;
            战斗.开始战斗(组, "__探索胜利", "__探索返回");
        }

        private string 随机遭遇组(探索事件表 表)
        {
            if (表?.遭遇 == null || 表.遭遇.Length == 0) return null;
            return 表.遭遇[Random.Range(0, 表.遭遇.Length)].敌人;
        }

        // ===== 选择类事件 =====

        public void 选择(int 选项索引)
        {
            if (当前选择?.选项 == null || 选项索引 < 0 || 选项索引 >= 当前选择.选项.Length) return;
            var 选项 = 当前选择.选项[选项索引];
            当前选择 = null;
            if (!string.IsNullOrEmpty(选项.战斗)) { 遭遇(选项.战斗, 0); return; }
            if (选项.效果 != null)
            {
                效果结算.应用(事件, 档案, 选项.效果);
                var 摘要 = 获得摘要(选项.效果);
                if (!string.IsNullOrEmpty(摘要)) 事件.发布(new 日志事件(日志类型.探索, 摘要));
                搜索次数++;
                显示当前层();
                return;
            }
            if (!string.IsNullOrEmpty(选项.节点)) { ServiceRegistry.Get<DialogueService>().进入节点(选项.节点); return; }
            搜索次数++;
            显示当前层();
        }

        // ===== 事件抽取 =====

        private void 抽事件(探索事件表 表)
        {
            if (表 == null) { 无事("这里什么也没有。"); return; }
            // 候选池：0遭遇 1发现 2资源 3无事 4选择
            var 候选 = new List<(int 类型, string 文本, string 标识, 剧情效果 效果, int 权重)>();
            if (表.遭遇 != null) foreach (var e in 表.遭遇) if (!string.IsNullOrEmpty(e.敌人)) 候选.Add((0, e.形态, e.敌人, null, e.权重));
            if (表.发现 != null) foreach (var d in 表.发现) if (!string.IsNullOrEmpty(d.节点)) 候选.Add((1, null, d.节点, null, d.权重));
            if (表.资源 != null) foreach (var r in 表.资源) if (r.效果 != null) 候选.Add((2, r.文本, null, r.效果, r.权重));
            if (表.无事文本 != null) foreach (var t in 表.无事文本) 候选.Add((3, t, null, null, 1));
            if (表.选择 != null) for (int i = 0; i < 表.选择.Length; i++) 候选.Add((4, null, i.ToString(), null, 1));
            if (候选.Count == 0) { 无事("这里什么也没有。"); return; }

            int 总权重 = 0; foreach (var c in 候选) 总权重 += Mathf.Max(1, c.权重);
            int 掷 = Random.Range(0, 总权重);
            var 选中 = 候选[0];
            foreach (var c in 候选) { 掷 -= Mathf.Max(1, c.权重); if (掷 < 0) { 选中 = c; break; } }

            // 派发
            switch (选中.类型)
            {
                case 0: 遭遇(选中.标识, 形态先手(选中.文本)); break;   // 遭遇（文本=形态）
                case 1: ServiceRegistry.Get<DialogueService>().进入节点(选中.标识); break;   // 发现 → 剧情节点（区域:指令回区域）
                case 2: 发现资源(选中.文本, 选中.效果); break;        // 资源 → [是][否] 确认
                case 3: 无事(选中.文本); break;                       // 无事
                case 4: 显示选择(int.Parse(选中.标识)); break;         // 选择类事件
            }
        }

        // 遭遇形态 → 先手：空=遇见(0) "被偷袭"=1 "偷袭"=2
        private static int 形态先手(string 形态)
        {
            if (形态 == "被偷袭") return 1;
            if (形态 == "偷袭") return 2;
            return 0;
        }

        // 资源事件：显示发现文本 + [是][否]（发现即算一次搜索）
        private void 发现资源(string 文本, 剧情效果 效果)
        {
            搜索次数++;
            // 找到对应资源项（记录 消耗精力）
            var 表 = 当前事件表 ?? 当前层?.事件表;
            if (表?.资源 != null)
                foreach (var r in 表.资源)
                    if (r.文本 == 文本 && r.效果 == 效果) { 当前资源 = r; break; }
            if (当前资源 == null) 当前资源 = new 资源项 { 文本 = 文本, 效果 = 效果, 消耗精力 = 1 };
            显示(文本 + "\n\n（获取需消耗 1 点精力）",
                default, new 探索按钮数据("是", "资源:是"), new 探索按钮数据("否", "资源:否"));
        }

        // 选择类事件：正文显示事件文本，选项1~2 → 是/否（肯定/否定；探索按钮此情景隐藏，返回走侧边栏取消）
        private void 显示选择(int 索引)
        {
            var 表 = 当前事件表 ?? 当前层?.事件表;
            var 选择 = 表?.选择;
            if (选择 == null || 索引 < 0 || 索引 >= 选择.Length) return;
            当前选择 = 选择[索引];
            探索按钮数据 是 = default, 否 = default;
            if (当前选择.选项 != null && 当前选择.选项.Length > 0)
            {
                是 = new 探索按钮数据(当前选择.选项[0].文本, "选择:0");
                if (当前选择.选项.Length > 1) 否 = new 探索按钮数据(当前选择.选项[1].文本, "选择:1");
            }
            显示(当前选择.文本, default, 是, 否);
        }

        private void 无事(string 文本)
        {
            搜索次数++;
            显示(文本, 探索按钮());
            事件.发布(new 日志事件(日志类型.探索, 文本));
        }

        // ===== 显示 =====

        private void 显示当前层()
        {
            var 层 = 当前层;
            if (层 == null) return;
            // 岔路层未选路 → 先选（是=安全小路 / 否=危险捷径；返回走侧边栏取消）
            if (层.岔路 != null && 当前事件表 == null)
            {
                显示(层.描述 + "\n\n" + 层.岔路.文本,
                    default, new 探索按钮数据("走安全小路", "岔路:安全"), new 探索按钮数据("走危险捷径", "岔路:危险"));
                return;
            }
            显示(层.描述, 探索按钮());
        }

        // 普通层主按钮：行动点足够才可探索（返回统一走侧边栏取消）
        private 探索按钮数据 探索按钮() => 档案.行动点 >= 搜索消耗 ? new 探索按钮数据("搜索", "搜索") : default;

        private void 显示(string 正文, 探索按钮数据 探索 = default, 探索按钮数据 是 = default, 探索按钮数据 否 = default)
        {
            当前显示 = new 探索显示事件(信息条文本(), 正文, 探索, 是, 否);
            事件.发布(当前显示);
        }

        private string 信息条文本()
        {
            if (当前区域 == null) return "";
            return $"{当前区域.名称} · {危险度文本(当前区域.危险度)}    {天气名(当前天气)}    层 {层索引 + 1}/{当前区域.层.Length}    行动 {档案.行动点}/{档案.最大行动点}";
        }

        private static string 天气名(天气类型 天气)
        {
            switch (天气)
            {
                case 天气类型.晴: return "晴";
                case 天气类型.雨: return "雨";
                case 天气类型.雾: return "雾";
                case 天气类型.雷雨: return "雷雨";
                case 天气类型.寒潮: return "寒潮";
                case 天气类型.沙暴: return "沙暴";
                case 天气类型.酷暑: return "酷暑";
                default: return "";
            }
        }

        private static string 危险度文本(int 危险度)
            => 危险度 switch { 1 => "危险度:低", 2 => "危险度:中", _ => "危险度:高" };

        private static 探索事件表 空表() => new 探索事件表 { 无事文本 = new[] { "这里已经安全了，什么也没有。" } };
    }
