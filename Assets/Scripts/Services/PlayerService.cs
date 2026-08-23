
    using System.Collections.Generic;
using UnityEngine;

    // 玩家服务：持有玩家档案，处理新游戏/读档，注入装备解析，发布初始状态事件
    public sealed class PlayerService
    {
        private readonly EventBus 事件;
        private readonly DataService 数据;
        private readonly SaveService 存档;

        public 玩家档案 档案 { get; private set; }

        public PlayerService(EventBus 事件, DataService 数据, SaveService 存档)
        {
            this.事件 = 事件;
            this.数据 = 数据;
            this.存档 = 存档;
        }

        // 创建并装配玩家档案（装备加成解析器接 DataService）；满状态开始
        public void 初始化()
        {
            档案 = new 玩家档案();
            接线装备解析(档案);
            档案.装备到槽("主手", "生锈短剑");   // 初始装备：主手短剑
            档案.生命 = 档案.最大生命;
            档案.魔力 = 档案.最大魔力;
            发布初始状态();
        }

        // 装备数值解析器：标识 -> 加成（派生数值遍历已装备求和用）
        private void 接线装备解析(玩家档案 档案)
        {
            档案.攻击加成解析 = 标识 => 数据.物品.TryGetValue(标识, out var 物品) ? 物品.攻击加成 : 0;
            档案.防御加成解析 = 标识 => 数据.物品.TryGetValue(标识, out var 物品) ? 物品.防御加成 : 0;
            档案.生命加成解析 = 标识 => 数据.物品.TryGetValue(标识, out var 物品) ? 物品.生命加成 : 0;
            档案.武器种类解析 = 标识 => 数据.物品.TryGetValue(标识, out var 物品) ? 物品.武器种类枚举 : 武器种类.无;
            档案.抗性加成解析 = 标识 => 数据.物品.TryGetValue(标识, out var 物品) ? 物品.抗性 : 0;
            档案.词缀定义表 = 数据.词缀;   // 词缀实例->模板 查表（词缀求和/显示用）
        }

        // 新游戏：重置档案 + 初始物品
        public void 新游戏()
        {
            初始化();
            档案.添加物品("面包", 1);
            档案.当前节点 = "序章_醒来";
            事件.发布(new 日志事件(日志类型.系统, "新的旅程开始了。"));
            发布初始状态();
        }

        // 读档：成功则用存档档案，否则新游戏。
        // 开发初期不留旧档：存档无 装备 列表（旧结构）或为空 → 视为无存档（新游戏）。
        public void 读档()
        {
            var 存档数据 = 存档.读取();
            if (存档.有存档() && 存档数据?.玩家 != null)
            {
                档案 = 存档数据.玩家;
                接线装备解析(档案);
                清理非法值(档案);   // 防御坏档/旧档：夹取越界、清负数、去空条目
                事件.发布(new 日志事件(日志类型.系统, "读取存档，继续冒险。"));
            }
            else { 新游戏(); }
            发布初始状态();
        }

        // 发布当前生命/魔力/精力/铜币/属性，让 HUD 与角色面板初始化
        private void 发布初始状态()
        {
            事件.发布(new 生命变化事件(档案.生命, 档案.最大生命, 0));
            事件.发布(new 魔力变化事件(档案.魔力, 档案.最大魔力, 0));
            事件.发布(new 精力变化事件(档案.精力, 档案.最大精力, 0));
            事件.发布(new 金币变化事件(档案.铜币, 0));
            事件.发布(new 属性变化事件(档案.体力, 档案.力量, 档案.智力, 档案.敏捷, 档案.自由属性点));
        }

        // 读档后非法值清理：夹取越界、清负数、去空条目，防空异常（防坏档/旧档）
        private static void 清理非法值(玩家档案 档案)
        {
            if (档案 == null) return;
            if (档案.等级 < 1) 档案.等级 = 1;
            if (档案.经验 < 0) 档案.经验 = 0;
            if (档案.铜币 < 0) 档案.铜币 = 0;
            if (档案.体力 < 0) 档案.体力 = 0;
            if (档案.力量 < 0) 档案.力量 = 0;
            if (档案.智力 < 0) 档案.智力 = 0;
            if (档案.敏捷 < 0) 档案.敏捷 = 0;
            if (档案.自由属性点 < 0) 档案.自由属性点 = 0;
            if (档案.游戏分钟数 < 0) 档案.游戏分钟数 = 0;
            档案.生命 = Mathf.Clamp(档案.生命, 0, 档案.最大生命);
            档案.魔力 = Mathf.Clamp(档案.魔力, 0, 档案.最大魔力);
            档案.精力 = Mathf.Clamp(档案.精力, 0, 档案.最大精力);
            档案.背包 ??= new List<物品堆叠>();
            档案.已学技能 ??= new List<技能掌握>();
            档案.任务 ??= new List<任务进度>();
            档案.装备 ??= new List<装备记录>();
            档案.已通关区域 ??= new List<string>();
            档案.日常 ??= new List<日常任务>();
            for (int i = 档案.背包.Count - 1; i >= 0; i--)
                if (档案.背包[i] == null || 档案.背包[i].数量 <= 0 || string.IsNullOrEmpty(档案.背包[i].标识)) 档案.背包.RemoveAt(i);
            for (int i = 档案.装备.Count - 1; i >= 0; i--)
                if (档案.装备[i] == null || string.IsNullOrEmpty(档案.装备[i].槽位) || string.IsNullOrEmpty(档案.装备[i].标识)) 档案.装备.RemoveAt(i);
            for (int i = 档案.已学技能.Count - 1; i >= 0; i--)
                if (档案.已学技能[i] == null || string.IsNullOrEmpty(档案.已学技能[i].标识)) 档案.已学技能.RemoveAt(i);
            for (int i = 档案.任务.Count - 1; i >= 0; i--)
                if (档案.任务[i] == null || string.IsNullOrEmpty(档案.任务[i].标识)) 档案.任务.RemoveAt(i);
            for (int i = 档案.日常.Count - 1; i >= 0; i--)
                if (档案.日常[i] == null || string.IsNullOrEmpty(档案.日常[i].标识)) 档案.日常.RemoveAt(i);
            for (int i = 档案.已通关区域.Count - 1; i >= 0; i--)
                if (string.IsNullOrEmpty(档案.已通关区域[i])) 档案.已通关区域.RemoveAt(i);
        }
    }
