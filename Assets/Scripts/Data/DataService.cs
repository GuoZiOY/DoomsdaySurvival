using System;
using System.Collections.Generic;
using UnityEngine;

    // 数据服务：加载 Resources/Data/*.json 并缓存为字典；启动全量跨引用校验。
    // 任何缺失/断裂引用都会写入 校验错误，由装配层阻止进入游戏（不静默崩溃）。
    public sealed class DataService
    {
        private readonly EventBus 事件;

        // 各数据表（可读写，供校验/测试注入）
        public Dictionary<string, 剧情节点> 剧情 { get; private set; } = new Dictionary<string, 剧情节点>();
        public Dictionary<string, 敌人数据> 敌人 { get; private set; } = new Dictionary<string, 敌人数据>();
        public Dictionary<string, 物品数据> 物品 { get; private set; } = new Dictionary<string, 物品数据>();
        public Dictionary<string, 技能数据> 技能 { get; private set; } = new Dictionary<string, 技能数据>();
        public Dictionary<string, 任务数据> 任务 { get; private set; } = new Dictionary<string, 任务数据>();
        public Dictionary<string, 地图地点> 地图 { get; private set; } = new Dictionary<string, 地图地点>();
        public Dictionary<string, 区域数据> 区域 { get; private set; } = new Dictionary<string, 区域数据>();
        public Dictionary<string, 设施定义> 设施 { get; private set; } = new Dictionary<string, 设施定义>();
        public Dictionary<string, 训练项目> 训练项目 { get; private set; } = new Dictionary<string, 训练项目>();
        public Dictionary<string, Buff定义> Buffs { get; private set; } = new Dictionary<string, Buff定义>();
        public Dictionary<string, 敌人组数据> 敌人组 { get; private set; } = new Dictionary<string, 敌人组数据>();
        public Dictionary<string, 助战组数据> 助战组 { get; private set; } = new Dictionary<string, 助战组数据>();
        public List<区域剧情路由> 区域剧情 { get; private set; } = new List<区域剧情路由>();
        public Dictionary<string, 配方数据> 配方 { get; private set; } = new Dictionary<string, 配方数据>();
        public Dictionary<string, 词缀定义> 词缀 { get; private set; } = new Dictionary<string, 词缀定义>();
        public Dictionary<string, 职业数据> 职业 { get; private set; } = new Dictionary<string, 职业数据>();
        public Dictionary<string, 天赋数据> 天赋 { get; private set; } = new Dictionary<string, 天赋数据>();
        public Dictionary<string, 天气数据> 天气 { get; private set; } = new Dictionary<string, 天气数据>();
        public Dictionary<string, 家具数据> 家具 { get; private set; } = new Dictionary<string, 家具数据>();
        public List<情报条目> 情报 { get; private set; } = new List<情报条目>();   // 收音机 情报池（无标识，列表承载）
        public Dictionary<string, 搜索地图类型> 搜索地图类型 { get; private set; } = new Dictionary<string, 搜索地图类型>();

        public List<string> 校验错误 { get; } = new List<string>();

        public DataService(EventBus 事件)
        {
            this.事件 = 事件;
            加载全部();
            重新校验();
        }

        // 统一加载入口：文件名 -> 目标字典 -> 根对象中提取数组
        private void 加载全部()
        {
            加载("story", 剧情, (剧情根 根) => 根.节点);
            加载("enemies", 敌人, (敌人根 根) => 根.敌人);
            加载物品();   // items.json 已按类型拆分多文件（便于查看修改），全部合并进 物品 字典
            加载("skills", 技能, (技能根 根) => 根.技能);
            加载("quests", 任务, (任务根 根) => 根.任务);
            加载("map", 地图, (地图根 根) => 根.地点);
            加载("regions", 区域, (区域根 根) => 根.区域);
            加载("facilities", 设施, (设施根 根) => 根.设施);   // 允许缺失（M4 才有）
            加载("training", 训练项目, (训练项目根 根) => 根.训练项目);
            加载("buffs", Buffs, (Buff根 根) => 根.Buffs);
            加载("encounters", 敌人组, (敌人组根 根) => 根.敌人组);
            加载("recipes", 配方, (配方根 根) => 根.配方);   // 允许缺失（制作系统）
            加载("affixes", 词缀, (词缀根 根) => 根.词缀);   // 允许缺失（词缀系统）
            加载("职业", 职业, (职业根 根) => 根.职业);       // 允许缺失（职业系统）
            加载("天赋", 天赋, (天赋根 根) => 根.天赋);       // 允许缺失（天赋系统）
            加载("天气", 天气, (天气根 根) => 根.天气);       // 允许缺失（天气系统）
            加载("家具", 家具, (家具根 根) => 根.家具);       // 允许缺失（安全屋系统）
            加载("搜索_地图类型", 搜索地图类型, (搜索地图类型根 根) => 根.地图类型);   // 允许缺失（搜索容器系统）
            加载情报();
            加载助战组与区域剧情();
        }

        // 物品数据按类型拆分（Data/items_*.json），逐文件加载合并；同名标识后加载覆盖
        private void 加载物品()
        {
            加载("items_武器", 物品, (物品根 根) => 根.物品);
            加载("items_防具", 物品, (物品根 根) => 根.物品);
            加载("items_消耗品", 物品, (物品根 根) => 根.物品);
            加载("items_材料", 物品, (物品根 根) => 根.物品);
            加载("items_容器", 物品, (物品根 根) => 根.物品);
        }

        // 情报池（收音机 收听 播报内容；无标识，列表承载）
        private void 加载情报()
        {
            var 资产 = Resources.Load<TextAsset>("Data/情报");
            if (资产 == null) { 事件.发布(new 日志事件(日志类型.系统, "[数据] 缺失 Data/情报.json")); return; }
            var 根 = JsonUtility.FromJson<情报根>(资产.text);
            if (根?.情报 != null) 情报.AddRange(根.情报);
        }

        // 附加表：encounters 助战组 + story 区域剧情（同一文件内的第二数组）
        private void 加载助战组与区域剧情()
        {
            var 敌人组资产 = Resources.Load<TextAsset>("Data/encounters");
            if (敌人组资产 != null)
            {
                var 根 = JsonUtility.FromJson<敌人组根>(敌人组资产.text);
                if (根?.助战组 != null)
                    foreach (var 项 in 根.助战组)
                        if (!string.IsNullOrEmpty(项.标识)) 助战组[项.标识] = 项;
            }
            var 剧情资产 = Resources.Load<TextAsset>("Data/story");
            if (剧情资产 != null)
            {
                var 根 = JsonUtility.FromJson<剧情根>(剧情资产.text);
                if (根?.区域剧情 != null) 区域剧情.AddRange(根.区域剧情);
            }
        }

        private void 加载<T, TRoot>(string 文件, Dictionary<string, T> 目标, Func<TRoot, T[]> 提取) where T : class
        {
            var 资产 = Resources.Load<TextAsset>($"Data/{文件}");
            if (资产 == null) { 事件.发布(new 日志事件(日志类型.系统, $"[数据] 缺失 Data/{文件}.json")); return; }
            var 根 = JsonUtility.FromJson<TRoot>(资产.text);
            if (根 == null) return;
            foreach (var 项 in 提取(根))
            {
                var 标识 = 获取标识(项);
                if (!string.IsNullOrEmpty(标识)) 目标[标识] = 项;
            }
        }

        // 反射取各模型的「标识」字段
        private static string 获取标识<T>(T 项)
        {
            var 字段 = typeof(T).GetField("标识");
            return 字段?.GetValue(项) as string;
        }

        // 全量跨引用校验：清空后重查，错误写入 校验错误
        public void 重新校验()
        {
            校验错误.Clear();

            // —— 剧情：选项目标 / 强制战斗敌人 / 下一节点 ——
            foreach (var (标识, 节点) in 剧情)
            {
                if (节点.选项 != null)
                    foreach (var 选项 in 节点.选项)
                    {
                        if (string.IsNullOrEmpty(选项.目标)) continue;
                        // 特殊目标（设施/战斗/探索/区域/任务/购买/学习/以 __ 开头的指令）跳过——
                        // 设施由 UI管理器 的 switch 硬编码处理，其余为剧情引擎指令，普通节点目标必须存在。
                        else if (!选项.目标.StartsWith("设施:") && !选项.目标.StartsWith("战斗:") && !选项.目标.StartsWith("探索:") &&
                                 !选项.目标.StartsWith("区域:") && !选项.目标.StartsWith("任务:") &&
                                 !选项.目标.StartsWith("购买:") && !选项.目标.StartsWith("学习:") && !选项.目标.StartsWith("地图:") &&
                                 !选项.目标.StartsWith("__") &&
                                 !剧情.ContainsKey(选项.目标))
                        {
                            校验错误.Add($"剧情[{标识}] → 节点[{选项.目标}] 不存在");
                        }
                        // 战斗:敌人组:胜利节点[:助战组] —— 敌人组/助战组 存在性
                        if (选项.目标.StartsWith("战斗:"))
                        {
                            var 部分 = 选项.目标.Split(':');
                            if (部分.Length >= 2 && !敌人组.ContainsKey(部分[1]))
                                校验错误.Add($"剧情[{标识}] → 战斗敌人组[{部分[1]}] 不存在");
                            if (部分.Length >= 4 && !string.IsNullOrEmpty(部分[3]) && !助战组.ContainsKey(部分[3]))
                                校验错误.Add($"剧情[{标识}] → 助战组[{部分[3]}] 不存在");
                        }
                    }
                // 强制战斗
                if (!string.IsNullOrEmpty(节点.战斗))
                {
                    var 部分 = 节点.战斗.Split(':');
                    if (部分.Length >= 2 && !敌人.ContainsKey(部分[1]))
                        校验错误.Add($"剧情[{标识}] 战斗敌人[{部分[1]}] 不存在");
                }
                // 下一节点（剧情链自动播放）
                if (!string.IsNullOrEmpty(节点.下一节点) && !剧情.ContainsKey(节点.下一节点))
                    校验错误.Add($"剧情[{标识}] → 下一节点[{节点.下一节点}] 不存在");
            }

            // —— 区域剧情路由：区域/节点/需要物品/需要任务 ——
            foreach (var 路由 in 区域剧情)
            {
                string 路由名 = $"区域剧情[{路由.区域}/{路由.阶段}]";
                if (!地图.ContainsKey(路由.区域))
                    校验错误.Add($"{路由名} → 地点[{路由.区域}] 不存在");
                if (!string.IsNullOrEmpty(路由.节点) && !剧情.ContainsKey(路由.节点))
                    校验错误.Add($"{路由名} → 节点[{路由.节点}] 不存在");
                if (!string.IsNullOrEmpty(路由.需要物品) && !物品.ContainsKey(路由.需要物品))
                    校验错误.Add($"{路由名} → 需要物品[{路由.需要物品}] 不存在");
                if (!string.IsNullOrEmpty(路由.需要任务) && !任务.ContainsKey(路由.需要任务))
                    校验错误.Add($"{路由名} → 需要任务[{路由.需要任务}] 不存在");
            }

            // —— 助战组：引用的伙伴单位存在 ——
            foreach (var (标识, 组) in 助战组)
            {
                if (组.成员 == null) continue;
                foreach (var 项 in 组.成员)
                    if (!敌人.ContainsKey(项.标识))
                        校验错误.Add($"助战组[{标识}] → 伙伴[{项.标识}] 不存在");
            }

            // —— 地图：目标节点 / 连接 ——
            foreach (var (标识, 地点) in 地图)
            {
                if (!string.IsNullOrEmpty(地点.目标) && !地点.目标.StartsWith("探索:") && !剧情.ContainsKey(地点.目标))
                    校验错误.Add($"地图[{标识}] → 节点[{地点.目标}] 不存在");
                if (地点.连接 != null)
                    foreach (var 相邻 in 地点.连接)
                        if (!地图.ContainsKey(相邻)) 校验错误.Add($"地图[{标识}] → 连接[{相邻}] 不存在");
            }

            // —— 物品：装备类必须有 槽位 ——
            foreach (var (标识, 物品) in 物品)
            {
                if ((物品.类型 == "武器" || 物品.类型 == "防具") && string.IsNullOrEmpty(物品.槽位))
                    校验错误.Add($"物品[{标识}] 装备类缺少 槽位（主手/副手/头部/胸部/腿部/脚部/手部/弹挂/腰封/背包）");
            }

            // —— 区域（层结构）：事件表遭遇敌人 / 发现节点 / Boss 组 / 选择事件 ——
            // 区域事件表校验：遭遇敌人组 / 发现节点 / 选择选项
            void 校验事件表(string 层名, 探索事件表 表)
            {
                if (表 == null) return;
                if (表.遭遇 != null)
                    foreach (var 遭遇 in 表.遭遇)
                        if (!string.IsNullOrEmpty(遭遇.敌人) && !敌人组.ContainsKey(遭遇.敌人))
                            校验错误.Add($"{层名} → 敌人组[{遭遇.敌人}] 不存在");
                if (表.发现 != null)
                    foreach (var 发现 in 表.发现)
                        if (!string.IsNullOrEmpty(发现.节点) && !剧情.ContainsKey(发现.节点))
                            校验错误.Add($"{层名} → 节点[{发现.节点}] 不存在");
                if (表.选择 != null)
                    foreach (var 选择 in 表.选择)
                    {
                        if (选择.选项 == null) continue;
                        foreach (var 选项 in 选择.选项)
                        {
                            if (!string.IsNullOrEmpty(选项.战斗) && !敌人组.ContainsKey(选项.战斗))
                                校验错误.Add($"{层名} 选择[{选择.文本}] → 敌人组[{选项.战斗}] 不存在");
                            if (!string.IsNullOrEmpty(选项.节点) && !剧情.ContainsKey(选项.节点))
                                校验错误.Add($"{层名} 选择[{选择.文本}] → 节点[{选项.节点}] 不存在");
                        }
                    }
            }

            foreach (var (标识, 区) in 区域)
            {
                if (区.层 == null) { 校验错误.Add($"区域[{标识}] 缺少 层 定义"); continue; }
                for (int i = 0; i < 区.层.Length; i++)
                {
                    var 层 = 区.层[i];
                    string 层名 = $"区域[{标识}]·层{i + 1}";
                    校验事件表(层名, 层.事件表);
                    校验事件表(层名, 层.通关后);
                    if (层.岔路 != null)
                    {
                        校验事件表($"{层名}·安全", 层.岔路.安全);
                        校验事件表($"{层名}·危险", 层.岔路.危险);
                    }
                    if (!string.IsNullOrEmpty(层.Boss) && !敌人组.ContainsKey(层.Boss))
                        校验错误.Add($"{层名} Boss组[{层.Boss}] 不存在");
                }
            }

            // —— 配方：产物/材料/图纸 物品存在 + 类型合法 ——
            foreach (var (标识, 配方) in 配方)
            {
                if (!物品.ContainsKey(配方.产物))
                    校验错误.Add($"配方[{标识}] → 产物[{配方.产物}] 不存在");
                if (配方.材料 != null)
                    foreach (var 材 in 配方.材料)
                        if (!物品.ContainsKey(材.物品))
                            校验错误.Add($"配方[{标识}] → 材料[{材.物品}] 不存在");
                if (!string.IsNullOrEmpty(配方.图纸) && !物品.ContainsKey(配方.图纸))
                    校验错误.Add($"配方[{标识}] → 图纸[{配方.图纸}] 不存在");
                if (配方.类型 != "装备" && 配方.类型 != "食物" && 配方.类型 != "药剂")
                    校验错误.Add($"配方[{标识}] → 类型[{配方.类型}] 非法（装备/食物/药剂）");
            }

            // —— 搜索容器：地图类型 → 房间 → 容器（尺寸合法 + 搜索表物品存在 + 形状块合法）——
            foreach (var (类型标识, 地图类型) in 搜索地图类型)
            {
                if (地图类型.房间 == null) { 校验错误.Add($"搜索容器[{类型标识}] 缺少 房间 定义"); continue; }
                foreach (var 房间 in 地图类型.房间)
                {
                    string 房名 = $"搜索容器[{类型标识}]·房间[{房间.标识}]";
                    if (房间.容器 == null || 房间.容器.Length == 0)
                    { 校验错误.Add($"{房名} 缺少 容器 定义"); continue; }
                    foreach (var 容器 in 房间.容器)
                    {
                        string 容名 = $"{房名}·容器[{容器.标识}]";
                        if (容器.容器列 <= 0 || 容器.容器行 <= 0)
                        { 校验错误.Add($"{容名} 尺寸非法（列×行）"); continue; }
                        if (容器.容器形状 != null && 容器.容器形状.Length > 0)
                            foreach (var 块 in 容器.容器形状)
                                if (块 == null || 块.宽 <= 0 || 块.高 <= 0 || 块.列 < 0 || 块.行 < 0
                                    || 块.列 + 块.宽 > 容器.容器列 || 块.行 + 块.高 > 容器.容器行)
                                { 校验错误.Add($"{容名} 形状块越界/非法"); break; }
                        if (容器.搜索表 != null)
                            foreach (var 条目 in 容器.搜索表)
                            {
                                if (条目 == null || string.IsNullOrEmpty(条目.物品标识)) continue;
                                if (!物品.ContainsKey(条目.物品标识))
                                    校验错误.Add($"{容名} → 物品[{条目.物品标识}] 不存在");
                                else if (!string.IsNullOrEmpty(容器.容器允许类型))
                                {
                                    var 模板 = 物品[条目.物品标识];
                                    if (模板.类型 != 容器.容器允许类型)
                                        校验错误.Add($"{容名} → 物品[{条目.物品标识}] 类型[{模板.类型}] 与 允许类型[{容器.容器允许类型}] 不符");
                                }
                            }
                    }
                }
            }

            // —— 安全屋家具：材料/升级材料 物品存在 + 形状/效果 合法 ——
            foreach (var (标识, 家具) in 家具)
            {
                if (家具.最大等级 <= 0) 校验错误.Add($"家具[{标识}] 最大等级 非法");
                if (家具.形状宽 <= 0 || 家具.形状高 <= 0) 校验错误.Add($"家具[{标识}] 形状 非法（宽×高）");
                if (家具.效果 == null || 家具.效果.Length < 家具.最大等级)
                    校验错误.Add($"家具[{标识}] 效果 长度不足（需 {家具.最大等级} 级，当前 {(家具.效果?.Length ?? 0)}）");
                if (家具.材料 != null)
                    foreach (var 材 in 家具.材料)
                        if (!物品.ContainsKey(材.物品)) 校验错误.Add($"家具[{标识}] → 材料[{材.物品}] 不存在");
                if (家具.升级 != null)
                    for (int i = 0; i < 家具.升级.Length; i++)
                    {
                        var 需 = 家具.升级[i];
                        if (需 == null) continue;
                        if (需.材料 != null)
                            foreach (var 材 in 需.材料)
                                if (!物品.ContainsKey(材.物品)) 校验错误.Add($"家具[{标识}] 升{i + 2}级 → 材料[{材.物品}] 不存在");
                    }
            }
        }
    }
