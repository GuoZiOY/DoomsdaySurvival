
    using System.Collections.Generic;
using UnityEngine;

    // 玩家服务：持有玩家档案，处理新游戏/读档/开局构筑，注入装备/形状/重量解析，发布初始状态事件
    public sealed class PlayerService
    {
        private readonly EventBus 事件;
        private readonly DataService 数据;
        private readonly SaveService 存档;

        public 玩家档案 档案 { get; private set; }

        // 开局构筑参数（主菜单选职业/分配自由点/选天赋后传入）
        public string 待选职业 = "";
        public string 角色名 = "无名幸存者";
        public int 待分配自由点 = 10;   // 开局额外自由分配 10 点
        public List<string> 待选天赋 = new List<string>();

        // 开局自由点分配结果：属性类型 -> 加点数（开局界面确认时写入）
        public readonly Dictionary<属性类型, int> 开局分配 = new Dictionary<属性类型, int>();

        public PlayerService(EventBus 事件, DataService 数据, SaveService 存档)
        {
            this.事件 = 事件;
            this.数据 = 数据;
            this.存档 = 存档;
        }

        // 创建并装配玩家档案（解析器接 DataService）；满状态开始
        public void 初始化()
        {
            档案 = new 玩家档案();
            接线解析器(档案);
            档案.户型种子 = UnityEngine.Random.Range(1, int.MaxValue);   // 新游戏：随机 户型 种子（读档 恢复 不 覆盖）
            档案.生命 = 档案.最大生命;
            档案.行动点 = 档案.最大行动点;
            发布初始状态();
        }

        // 装备/形状/重量 解析器：标识 -> 数值（派生数值遍历已装备求和用）
        private void 接线解析器(玩家档案 档案)
        {
            档案.攻击加成解析 = 标识 => 数据.物品.TryGetValue(标识, out var 物品) ? 物品.攻击加成 : 0;
            档案.防御加成解析 = 标识 => 数据.物品.TryGetValue(标识, out var 物品) ? 物品.防御加成 : 0;
            档案.生命加成解析 = 标识 => 数据.物品.TryGetValue(标识, out var 物品) ? 物品.生命加成 : 0;
            档案.负重加成解析 = 标识 => 数据.物品.TryGetValue(标识, out var 物品) ? (物品.负重加成 > 0 ? 物品.负重加成 : 0) : 0;
            档案.武器种类解析 = 标识 => 数据.物品.TryGetValue(标识, out var 物品) ? 物品.武器种类枚举 : 武器种类.无;
            档案.抗性加成解析 = 标识 => 数据.物品.TryGetValue(标识, out var 物品) ? 物品.抗性 : 0;
            档案.形状解析 = 标识 => 数据.物品.TryGetValue(标识, out var 物品) ? new 物品形状(物品.形状宽, 物品.形状高) : new 物品形状(1, 1);
            档案.重量解析 = 标识 => 数据.物品.TryGetValue(标识, out var 物品) ? 物品.重量 : 1;
            档案.堆叠上限解析 = 标识 => 数据.物品.TryGetValue(标识, out var 物品) ? 物品.堆叠上限 : 0;
            档案.最大耐久解析 = 标识 => 数据.物品.TryGetValue(标识, out var 物品) ? 物品.最大耐久 : 0;
            档案.容器尺寸解析 = 标识 => 数据.物品.TryGetValue(标识, out var 物品) && 物品.容器列 > 0 ? (物品.容器列, 物品.容器行) : (0, 0);   // 弹挂/腰封/背包 穿戴时初始化容器网格；非容器 (0,0)
            档案.家具定义解析 = 标识 => 数据.家具.TryGetValue(标识, out var 家) ? 家 : null;   // 安全屋 家具 效果 查询
            档案.词缀定义表 = 数据.词缀;
            档案.装配管理器();   // 装配子管理器（装备/持有/生存/成长/任务——职责分离·调度器模式；读档后同样重装）
            接线网格服务(档案);
        }

        // 把背包解析器接到 网格服务（形状/堆叠上限/有效最大耐久/重量）
        private void 接线网格服务(玩家档案 档案)
        {
            var 背 = 档案.网格服务;
            背.形状解析 = 档案.形状解析;
            背.堆叠上限解析 = 档案.堆叠上限解析;
            背.有效最大耐久解析 = 标识 => 档案.有效最大耐久(标识);
            背.重量解析 = 档案.重量解析;
            // 容器服务 的解析器也同步（打开容器视图时注入）
            if (ServiceRegistry.已注册<容器服务>())
                ServiceRegistry.Get<容器服务>().接线解析器(档案);
        }

        // 新游戏：先做开局构筑（职业/自由点/天赋），再进入游戏
        public void 新游戏()
        {
            初始化();
            // ① 应用职业（属性分布 + 初始技能 + 初始装备 + 职业天赋）
            应用职业(待选职业);
            // ② 应用自由点（开局额外 10 点，按 开局分配 落五维）
            foreach (var (类型, 点数) in 开局分配)
                if (点数 > 0) 档案.训练属性(类型, 点数);
            // ③ 应用正负天赋
            应用天赋(待选天赋);
            // ④ 角色名 + 初始物资与进入（先应用背包装备确定网格尺寸，再放入初始物资）
            档案.角色名 = string.IsNullOrEmpty(角色名) ? "无名幸存者" : 角色名;
            档案.应用背包装备();
            档案.放入网格("面包", 2);
            档案.放入网格("水", 1);
            档案.放入网格("绷带", 1);
            // ⑤ 安全屋：开局 自带 一张 1 级 床（房间网格 0,0，占 2×3）
            档案.家具.Add(new 物品堆叠("床", 1) { 列 = 0, 行 = 0, 旋转 = false });
            档案.当前节点 = "开局_醒来";
            事件.发布(new 日志事件(日志类型.系统, $"末日第 1 天。{档案.角色名}还活着。"));
            发布初始状态();
        }

        // 应用职业：属性分布（直接给定五维，覆盖初始 5）+ 初始技能 + 初始装备 + 职业天赋
        private void 应用职业(string 职业标识)
        {
            if (string.IsNullOrEmpty(职业标识)) return;
            if (!数据.职业.TryGetValue(职业标识, out var 职业)) return;
            档案.职业 = 职业标识;
            // 属性分布：职业直接给定五维（总和 25，区分职业），覆盖 初始 5
            if (职业.属性分布 != null)
                foreach (var 项 in 职业.属性分布)
                    档案.设置属性(解析属性(项.属性), 项.点数);
            if (!string.IsNullOrEmpty(职业.初始技能) && 数据.技能.TryGetValue(职业.初始技能, out var 技能))
                档案.学习技能(技能);
            if (职业.初始装备 != null)
                foreach (var 项 in 职业.初始装备)
                {
                    if (string.IsNullOrEmpty(项.标识)) continue;
                    var 物品 = 数据.物品.TryGetValue(项.标识, out var 物) ? 物 : null;
                    if (物品 != null && (物品.类型 == "武器" || 物品.类型 == "防具"))
                    {
                        string 槽位 = string.IsNullOrEmpty(物品.槽位) ? "主手" : 物品.槽位;
                        档案.装备到槽(槽位, 项.标识);
                    }
                    else 档案.添加物品(项.标识, 项.数量 > 0 ? 项.数量 : 1);
                }
            if (!string.IsNullOrEmpty(职业.天赋) && !档案.天赋.Contains(职业.天赋))
                档案.天赋.Add(职业.天赋);
        }

        // 应用正负天赋：属性类效果直接落五维；状态类（饱食/水分/经验/医疗/制作/夜晚）只作标记，由档案派生读取
        private void 应用天赋(List<string> 天赋标识列表)
        {
            if (天赋标识列表 == null) return;
            foreach (var 标识 in 天赋标识列表)
            {
                if (string.IsNullOrEmpty(标识)) continue;
                if (档案.天赋.Contains(标识)) continue;
                if (!数据.天赋.TryGetValue(标识, out var 天赋)) continue;
                档案.天赋.Add(标识);
                if (天赋.效果 != null)
                    foreach (var 效果 in 天赋.效果)
                        if (效果 != null && !string.IsNullOrEmpty(效果.目标))
                        {
                            // 仅五维目标落属性；状态类（饱食/水分/经验/医疗/制作/夜晚/睡觉/士气/感冒）由档案派生属性按标识读取
                            if (效果.目标 == "体质" || 效果.目标 == "力量" || 效果.目标 == "智慧"
                                || 效果.目标 == "敏捷" || 效果.目标 == "意志")
                                档案.训练属性(解析属性(效果.目标), (int)效果.数值);
                        }
            }
        }

        // 读档：成功则用存档档案，否则新游戏
        public void 读档()
        {
            var 存档数据 = 存档.读取();
            if (存档.有存档() && 存档数据?.玩家 != null)
            {
                档案 = 存档数据.玩家;
                接线解析器(档案);
                清理非法值(档案);
                事件.发布(new 日志事件(日志类型.系统, "读取存档，继续挣扎。"));
            }
            else { 新游戏(); }
            发布初始状态();
        }

        // 发布当前状态，让 HUD 与角色面板初始化
        private void 发布初始状态()
        {
            事件.发布(new 生命变化事件(档案.生命, 档案.最大生命, 0));
            事件.发布(new 精力变化事件(档案.行动点, 档案.最大行动点, 0));
            事件.发布(new 金币变化事件(档案.铜币, 0));
            事件.发布(new 属性变化事件(档案.体质, 档案.力量, 档案.智慧, 档案.敏捷, 档案.意志, 档案.自由属性点));
            事件.发布(new 生存状态变化事件(生存状态类型.饱食度, 档案.饱食度, 0));
            事件.发布(new 生存状态变化事件(生存状态类型.水分度, 档案.水分度, 0));
            foreach (伤病类型 类型 in System.Enum.GetValues(typeof(伤病类型)))
                事件.发布(new 伤病变化事件(类型, 档案.伤病值(类型), 0));
        }

        // 读档后非法值清理
        private static void 清理非法值(玩家档案 档案)
        {
            if (档案 == null) return;
            if (档案.等级 < 1) 档案.等级 = 1;
            if (档案.经验 < 0) 档案.经验 = 0;
            if (档案.铜币 < 0) 档案.铜币 = 0;
            if (档案.体质 < 0) 档案.体质 = 0;
            if (档案.力量 < 0) 档案.力量 = 0;
            if (档案.智慧 < 0) 档案.智慧 = 0;
            if (档案.敏捷 < 0) 档案.敏捷 = 0;
            if (档案.意志 < 0) 档案.意志 = 0;
            if (档案.自由属性点 < 0) 档案.自由属性点 = 0;
            if (档案.游戏分钟数 < 0) 档案.游戏分钟数 = 0;
            档案.饱食度 = Mathf.Clamp(档案.饱食度, 0f, 100f);
            档案.水分度 = Mathf.Clamp(档案.水分度, 0f, 100f);
            档案.疲劳 = Mathf.Clamp(档案.疲劳, 0, 100);
            档案.中毒 = Mathf.Clamp(档案.中毒, 0, 100);
            档案.感冒 = Mathf.Clamp(档案.感冒, 0, 100);
            档案.流血 = Mathf.Clamp(档案.流血, 0, 100);
            档案.骨折 = Mathf.Clamp(档案.骨折, 0, 100);
            档案.发烧 = Mathf.Clamp(档案.发烧, 0, 100);
            档案.生命 = Mathf.Clamp(档案.生命, 0, 档案.最大生命);
            档案.行动点 = Mathf.Clamp(档案.行动点, 0, 档案.最大行动点);
            档案.网格服务.网格物品 ??= new List<物品堆叠>();   // 背包列表由 网格服务 持有
            档案.已学技能 ??= new List<技能掌握>();
            档案.任务 ??= new List<任务进度>();
            档案.装备 ??= new List<装备记录>();
            档案.已清空地点 ??= new List<string>();
            档案.日常 ??= new List<日常任务>();
            档案.幸存者 ??= new List<string>();
            档案.抉择记录 ??= new List<string>();
            档案.天赋 ??= new List<string>();
            档案.天赋冷却 ??= new Dictionary<string, float>();
            档案.家具 ??= new List<物品堆叠>();   // 安全屋家具（物品堆叠 承载）
            // 迁移：旧档 战斗技能槽 空 → 用 已学技能 前 6 自动填充（固定 6 槽）
            档案.战斗技能槽 ??= new List<string>();
            if (档案.战斗技能槽.Count == 0)
                foreach (var s in 档案.已学技能)
                {
                    if (档案.战斗技能槽.Count >= 6) break;
                    if (!档案.战斗技能槽.Contains(s.标识)) 档案.战斗技能槽.Add(s.标识);
                }
            // 清理 旧主背包（档案.背包）非法堆叠
            for (int i = 档案.背包.Count - 1; i >= 0; i--)
                if (档案.背包[i] == null || 档案.背包[i].数量 <= 0 || string.IsNullOrEmpty(档案.背包[i].标识)) 档案.背包.RemoveAt(i);
            // 迁移：旧档 档案.背包（旧"主背包"5×10）物品 → 统一放入 穿戴容器/仓库（主背包 废弃；容器类先初始化内部网格）
            for (int i = 档案.背包.Count - 1; i >= 0; i--)
            {
                var 堆叠 = 档案.背包[i];
                if (堆叠 == null) continue;
                档案.背包.RemoveAt(i);
                if (ServiceRegistry.已注册<容器服务>())
                    ServiceRegistry.Get<容器服务>().初始化容器(堆叠);
                堆叠.列 = -1; 堆叠.行 = -1;   // 重置为未入格（旧档列/行是旧主背包坐标，须重新找空位）
                档案.放入堆叠(堆叠);   // 统一放入：穿戴容器优先 → 仓库兜底
            }
            // 迁移：容器类物品初始化内部网格（穿在身上的 弹挂/腰封/背包 旧档 容器物品=null → 建空容器）
            if (ServiceRegistry.已注册<容器服务>())
            {
                var 容器 = ServiceRegistry.Get<容器服务>();
                foreach (var 记录 in 档案.装备)
                    if (记录 != null && 记录.容器物品 == null && 记录.容器列 > 0) 记录.容器物品 = new List<物品堆叠>();
                foreach (var 堆叠 in 档案.所有持有物品())
                    if (堆叠 != null) 容器.初始化容器(堆叠);
            }
            for (int i = 档案.装备.Count - 1; i >= 0; i--)
                if (档案.装备[i] == null || string.IsNullOrEmpty(档案.装备[i].槽位) || string.IsNullOrEmpty(档案.装备[i].标识)) 档案.装备.RemoveAt(i);
            // 旧存档无耐久字段：有最大耐久的装备若当前耐久<=0（未记录），视为全新回满，避免旧档案全部损坏
            foreach (var 堆叠 in 档案.所有持有物品())
                if (堆叠 != null && 档案.有效最大耐久(堆叠.标识) > 0 && 堆叠.当前耐久 <= 0)
                    堆叠.当前耐久 = 档案.有效最大耐久(堆叠.标识);
            foreach (var e in 档案.装备)
                if (e != null && !string.IsNullOrEmpty(e.标识) && 档案.有效最大耐久(e.标识) > 0 && e.当前耐久 <= 0)
                    e.当前耐久 = 档案.有效最大耐久(e.标识);
            for (int i = 档案.已学技能.Count - 1; i >= 0; i--)
                if (档案.已学技能[i] == null || string.IsNullOrEmpty(档案.已学技能[i].标识)) 档案.已学技能.RemoveAt(i);
            for (int i = 档案.任务.Count - 1; i >= 0; i--)
                if (档案.任务[i] == null || string.IsNullOrEmpty(档案.任务[i].标识)) 档案.任务.RemoveAt(i);
            for (int i = 档案.日常.Count - 1; i >= 0; i--)
                if (档案.日常[i] == null || string.IsNullOrEmpty(档案.日常[i].标识)) 档案.日常.RemoveAt(i);
            for (int i = 档案.已清空地点.Count - 1; i >= 0; i--)
                if (string.IsNullOrEmpty(档案.已清空地点[i])) 档案.已清空地点.RemoveAt(i);
            for (int i = 档案.幸存者.Count - 1; i >= 0; i--)
                if (string.IsNullOrEmpty(档案.幸存者[i])) 档案.幸存者.RemoveAt(i);
            for (int i = 档案.天赋.Count - 1; i >= 0; i--)
                if (string.IsNullOrEmpty(档案.天赋[i])) 档案.天赋.RemoveAt(i);
        }

        // 属性名 -> 属性类型（职业/天赋效果用）
        private static 属性类型 解析属性(string 名)
        {
            switch (名)
            {
                case "体质": return 属性类型.体质;
                case "力量": return 属性类型.力量;
                case "智慧": return 属性类型.智慧;
                case "敏捷": return 属性类型.敏捷;
                case "意志": return 属性类型.意志;
                default: return 属性类型.体质;
            }
        }
    }
