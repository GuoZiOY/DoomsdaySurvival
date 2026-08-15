using UnityEngine;

    // 战斗服务：回合制逻辑（复用 战斗规则 纯函数）。UI 订阅 战斗消息/战斗结束 事件渲染。
    public sealed class BattleService
    {
        private readonly EventBus 事件;
        private readonly DataService 数据;
        private readonly 玩家档案 档案;

        private 敌人数据 敌人;
        private int 敌人生命;
        private bool 防御中;

        // 战斗结束后回到的剧情节点（胜利/逃跑用，UI 读取；"__探索" 前缀为探索战斗标记）
        public string 胜利节点 { get; private set; }
        public string 返回节点 { get; private set; }

        // 当前消息缓存：战斗视图绑定时先拉取（规避装配期消息先发）
        public string 当前消息 { get; private set; }

        public BattleService(EventBus 事件, DataService 数据, 玩家档案 档案) { this.事件 = 事件; this.数据 = 数据; this.档案 = 档案; }

        public bool 战斗中 { get; private set; }

        // 开始战斗：记录敌人/节点，重置状态
        public void 开始战斗(string 敌人标识, string 胜利后节点, string 返回节点)
        {
            if (!数据.敌人.TryGetValue(敌人标识, out 敌人)) { Debug.LogError($"[BattleService] 敌人不存在: {敌人标识}"); return; }
            this.胜利节点 = 胜利后节点; this.返回节点 = 返回节点;
            敌人生命 = 敌人.生命; 防御中 = false; 战斗中 = true;
            发消息($"<color={游戏主题危险}>{敌人.名称}</color> 出现了！生命 {敌人生命}/{敌人.生命}");
        }

        // 玩家普通攻击
        public void 玩家攻击() => 敌人受击(战斗规则.普通伤害(档案.总攻击, 敌人.防御, Random.Range(-1, 2)), "你挥剑攻击！");

        // 玩家技能（重击/防御/回血）
        public void 玩家技能(string 技能标识)
        {
            if (!数据.技能.TryGetValue(技能标识, out var 技能)) return;
            if (!档案.消耗魔力(技能.消耗魔力)) { 事件.发布(new 日志事件(日志类型.反馈坏, "魔力不足。")); return; }
            事件.发布(new 魔力变化事件(档案.魔力, 档案.最大魔力, -技能.消耗魔力));
            switch (技能.类型)
            {
                case "重击": 敌人受击(战斗规则.技能伤害(档案.总攻击, 技能.数值, 敌人.防御, Random.Range(-1, 2)), $"你发动{技能.名称}！"); break;
                case "防御": 防御中 = true; 发消息("你摆出防御架势，全身戒备。"); break;
                case "回血": 档案.恢复生命(技能.数值); 事件.发布(new 生命变化事件(档案.生命, 档案.最大生命, 技能.数值)); 发消息($"你施展{技能.名称}，恢复了 {技能.数值} 点生命。"); break;
            }
            敌人回合();
        }

        // 玩家使用恢复道具
        public void 玩家道具(string 物品标识)
        {
            if (!数据.物品.TryGetValue(物品标识, out var 物品) || 物品.类型 != "恢复") return;
            if (!档案.移除物品(物品标识)) return;
            档案.恢复生命(物品.恢复量);
            事件.发布(new 生命变化事件(档案.生命, 档案.最大生命, 物品.恢复量));
            事件.发布(new 背包变化事件(物品标识, -1, 变化原因.消耗));
            发消息($"你饮下{物品.名称}，恢复了 {物品.恢复量} 点生命。");
            敌人回合();
        }

        // 逃跑：发 战斗结束事件(失败) 由 UI 回返回节点
        public void 逃跑()
        {
            战斗中 = false;
            事件.发布(new 战斗结束事件(false, 敌人?.标识));
        }

        // 敌人受击：扣血，死亡则胜利
        private void 敌人受击(int 伤害, string 前缀)
        {
            敌人生命 = Mathf.Max(0, 敌人生命 - 伤害);
            发消息($"{前缀}对{敌人.名称}造成 {伤害} 点伤害。");
            if (敌人生命 <= 0) { 胜利(); return; }
            敌人回合();
        }

        // 敌人回合：反击（防御减半）
        private void 敌人回合()
        {
            int 伤害 = 战斗规则.普通伤害(敌人.攻击, 档案.总防御, Random.Range(-1, 2));
            if (防御中) 伤害 = 战斗规则.防御后伤害(伤害);
            档案.受到伤害(伤害);
            事件.发布(new 生命变化事件(档案.生命, 档案.最大生命, -伤害));
            发消息($"<color={游戏主题危险}>{敌人.名称}</color> 对你造成 {伤害} 点伤害。");
            if (档案.生命 <= 0) { 战败(); return; }
            防御中 = false;
        }

        // 胜利：奖励 + 掉落 + 事件
        private void 胜利()
        {
            战斗中 = false;
            档案.金币 += 敌人.金币奖励;
            档案.获得经验(敌人.经验奖励);
            事件.发布(new 金币变化事件(档案.金币, 敌人.金币奖励));
            发消息($"你击败了{敌人.名称}！获得 {敌人.金币奖励} 金币、{敌人.经验奖励} 经验。");
            事件.发布(new 战斗结束事件(true, 敌人.标识));
            if (!string.IsNullOrEmpty(敌人.掉落物品) && Random.value < 敌人.掉落概率)
            { 档案.添加物品(敌人.掉落物品); 事件.发布(new 背包变化事件(敌人.掉落物品, 1, 变化原因.获得)); }
        }

        private void 战败() { 战斗中 = false; 发消息("你被击败了……"); 事件.发布(new 战斗结束事件(false, 敌人.标识)); }

        // 战斗消息：日志栏 + 战斗视图正文（富文本）
        private void 发消息(string 文本)
        {
            当前消息 = 文本;
            事件.发布(new 日志事件(日志类型.操作, 文本));
            事件.发布(new 战斗消息事件(文本));
        }

        // 主题危险色值（富文本用）
        private const string 游戏主题危险 = "#c7473d";
    }
