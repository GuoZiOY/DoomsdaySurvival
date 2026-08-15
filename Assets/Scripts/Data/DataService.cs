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
            加载("items", 物品, (物品根 根) => 根.物品);
            加载("skills", 技能, (技能根 根) => 根.技能);
            加载("quests", 任务, (任务根 根) => 根.任务);
            加载("map", 地图, (地图根 根) => 根.地点);
            加载("regions", 区域, (区域根 根) => 根.区域);
            加载("facilities", 设施, (设施根 根) => 根.设施);   // 允许缺失（M4 才有）
            加载("training", 训练项目, (训练项目根 根) => 根.训练项目);
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

            // —— 剧情：选项目标 / 强制战斗敌人 ——
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
                    }
                // 强制战斗
                if (!string.IsNullOrEmpty(节点.战斗))
                {
                    var 部分 = 节点.战斗.Split(':');
                    if (部分.Length >= 2 && !敌人.ContainsKey(部分[1]))
                        校验错误.Add($"剧情[{标识}] 战斗敌人[{部分[1]}] 不存在");
                }
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

            // —— 区域：遭遇敌人 / 发现节点 ——
            foreach (var (标识, 区) in 区域)
            {
                if (区.遭遇 != null)
                    foreach (var 遭遇 in 区.遭遇)
                        if (!string.IsNullOrEmpty(遭遇.敌人) && !敌人.ContainsKey(遭遇.敌人))
                            校验错误.Add($"区域[{标识}] → 敌人[{遭遇.敌人}] 不存在");
                if (区.发现 != null)
                    foreach (var 发现 in 区.发现)
                        if (!string.IsNullOrEmpty(发现.节点) && !剧情.ContainsKey(发现.节点))
                            校验错误.Add($"区域[{标识}] → 节点[{发现.节点}] 不存在");
            }
        }
    }
