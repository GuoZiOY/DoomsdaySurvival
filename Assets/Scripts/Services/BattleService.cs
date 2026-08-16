using System.Collections.Generic;
using UnityEngine;

// 战斗服务：回合制战斗状态机。战斗单位统一抽象（玩家/敌人都是它），速度队列行动序，
// 伤害管线/状态(buff)/多敌人/敌人AI/结算回写 全在此驱动。UI 只订阅事件 + 调用行动入口，不直接改战斗数据。
// 进场投影(从玩家档案/敌人数据生成战斗单位) → 战斗 → 离场回写(生命/魔力/道具消耗/奖励)。
public sealed class BattleService
{
    private readonly EventBus 事件;
    private readonly DataService 数据;
    private readonly PlayerService 玩家服务;
    private 玩家档案 档案 => 玩家服务.档案;   // 动态取当前档案（新游戏/读档会替换实例，不能缓存旧引用）

    // —— 战斗状态 ——
    public bool 战斗中 { get; private set; }
    public int 回合数 { get; private set; }
    public 战斗单位 玩家 { get; private set; }
    public readonly List<战斗单位> 我方 = new List<战斗单位>();
    public readonly List<战斗单位> 敌方 = new List<战斗单位>();

    private readonly Queue<战斗单位> 行动队列 = new Queue<战斗单位>();
    private readonly List<战斗单位> 本回合行动列表 = new List<战斗单位>();   // 本回合完整速度序（信息条显示全轮）
    public 战斗单位 当前行动单位 { get; private set; }
    public int 当前行动索引 { get; private set; } = -1;
    public bool 玩家回合 => 战斗中 && 当前行动单位 != null && 当前行动单位.是否我方;

    // 本回合完整行动序（存活，速度序）：已行动在前、当前行动、后续排队 全显示
    public List<战斗单位> 本回合行动显示()
    {
        var 列表 = new List<战斗单位>();
        foreach (var u in 本回合行动列表) if (u.存活) 列表.Add(u);
        return 列表;
    }

    // 战斗结束衔接
    private string 胜利节点;
    private string 返回节点;
    private string 结果节点;
    private readonly List<string> 消耗道具 = new List<string>();
    private int 累计金币, 累计经验;

    public string 当前消息 { get; private set; }

    private const string 危险色 = "#c7473d";

    public BattleService(EventBus 事件, DataService 数据, PlayerService 玩家服务)
    { this.事件 = 事件; this.数据 = 数据; this.玩家服务 = 玩家服务; }

    // ===== 开始战斗 =====
    public void 开始战斗(string 敌人组标识, string 胜利后节点, string 返回节点)
    {
        if (!数据.敌人组.TryGetValue(敌人组标识, out var 组))
        {
            Debug.LogError($"[战斗] 敌人组不存在: {敌人组标识}");
            return;
        }
        this.胜利节点 = 胜利后节点; this.返回节点 = 返回节点;
        战斗中 = true; 回合数 = 0;
        我方.Clear(); 敌方.Clear(); 消耗道具.Clear(); 行动队列.Clear();
        累计金币 = 0; 累计经验 = 0; 当前行动单位 = null;
        var 玩家单位 = 战斗单位.从玩家投影(档案);
        玩家 = 玩家单位; 我方.Add(玩家单位);
        if (组.敌人 != null)
            foreach (var 项 in 组.敌人)
            {
                if (!数据.敌人.TryGetValue(项.敌人, out var 敌)) continue;
                for (int i = 0; i < Mathf.Max(1, 项.数量); i++) 敌方.Add(战斗单位.从敌人生成(敌));
            }
        事件.发布(new 战斗开始事件(我方.ToArray(), 敌方.ToArray()));
        回合开始();
    }

    // ===== 回合循环 =====
    private void 回合开始()
    {
        回合数++;
        // 回合开始：持续伤害结算（中毒/灼烧）
        foreach (var 单位 in 全部单位())
        {
            int 毒伤 = 单位.持续伤害量();
            if (毒伤 <= 0) continue;
            单位.受到伤害(毒伤);
            事件.发布(new 伤害事件(单位, 毒伤, 伤害类型.真实, false, false));
            发消息($"{单位.名称} 受到 <color={危险色}>{毒伤}</color> 点持续伤害。");
            检查单位死亡(单位);
        }
        if (检查战斗结束()) return;
        // 按速度降序生成本回合完整行动序（队列执行 + 信息条显示全轮）
        行动队列.Clear();
        本回合行动列表.Clear();
        本回合行动列表.AddRange(全部单位());
        本回合行动列表.Sort((a, b) => b.基础速度.CompareTo(a.基础速度));
        当前行动索引 = -1;
        foreach (var u in 本回合行动列表) 行动队列.Enqueue(u);
        事件.发布(new 回合开始事件(回合数));
        推进();
    }

    // 弹出下一个可行动单位；玩家回合停下等操作，敌人回合跑AI
    private void 推进()
    {
        while (行动队列.Count > 0)
        {
            当前行动单位 = 行动队列.Dequeue();
            当前行动索引 = 本回合行动列表.IndexOf(当前行动单位);
            if (!当前行动单位.存活) continue;
            if (当前行动单位.无法行动)
            {
                发消息($"{当前行动单位.名称} 被眩晕，无法行动！");
                continue;
            }
            事件.发布(new 行动轮换事件(当前行动单位, 当前行动单位.是否我方));
            return;   // 玩家回合等玩家输入；敌方回合等面板停顿后调用 继续()
        }
        回合结束();
    }

    // 面板在敌方回合停顿后调用：执行当前敌方行动，再推进（让玩家看清敌方行动，而非瞬发）
    public void 继续()
    {
        if (!战斗中 || 当前行动单位 == null) return;
        if (当前行动单位.是否我方) return;
        if (!当前行动单位.存活) { 行动完成(); return; }
        敌人行动(当前行动单位);
        行动完成();
    }

    private void 回合结束()
    {
        foreach (var u in 全部单位()) u.回合结束();
        回合开始();
    }

    // ===== 玩家行动（面板调用入口） =====
    public void 玩家普攻(战斗单位 目标)
    {
        if (!玩家回合 || 目标 == null || !目标.存活) return;
        执行攻击(玩家, 目标);
        行动完成();
    }

    public void 玩家技能(string 技能标识, 战斗单位 目标)
    {
        if (!玩家回合 || 目标 == null || !目标.存活) return;
        执行技能(玩家, 技能标识, 目标);
        行动完成();
    }

    public void 玩家道具(string 物品标识, 战斗单位 目标)
    {
        if (!玩家回合 || 目标 == null) return;
        执行道具(玩家, 物品标识, 目标);
        行动完成();
    }

    public void 逃跑()
    {
        if (!玩家回合) return;
        int 敌速 = 0;
        foreach (var e in 敌方) if (e.存活 && e.基础速度 > 敌速) 敌速 = e.基础速度;
        float 概率 = Mathf.Clamp(0.5f + (玩家.基础速度 - 敌速) * 0.02f, 0.2f, 0.95f);
        if (Random.value < 概率) { 结束战斗(false, "你成功逃离了战斗。", 返回节点); return; }
        音效管理器.实例?.播放失败();
        发消息("逃跑失败！");
        行动完成();
    }

    // ===== 敌人AI（P6） =====
    private void 敌人行动(战斗单位 敌人)
    {
        var 敌数据 = 敌人.源数据;
        string 行动 = 敌数据?.行动表 != null && 敌数据.行动表.Length > 0 ? AI抽行动(敌数据) : "普攻";
        var 目标 = AI选目标(敌人, 敌数据);
        if (行动 == "普攻") 执行攻击(敌人, 目标);
        else 执行技能(敌人, 行动, 目标);
    }

    private string AI抽行动(敌人数据 敌数据)
    {
        int 总 = 0; foreach (var a in 敌数据.行动表) 总 += Mathf.Max(1, a.权重);
        int 掷 = Random.Range(0, 总);
        foreach (var a in 敌数据.行动表)
        {
            掷 -= Mathf.Max(1, a.权重);
            if (掷 < 0) return a.行动;
        }
        return "普攻";
    }

    private 战斗单位 AI选目标(战斗单位 敌人, 敌人数据 敌数据)
    {
        var 存活 = 我方.FindAll(u => u.存活);
        if (存活.Count == 0) return null;
        if (敌数据?.目标策略 == "残血")
        {
            存活.Sort((a, b) => a.生命.CompareTo(b.生命));
            return 存活[0];
        }
        return 存活[Random.Range(0, 存活.Count)];
    }

    // ===== 统一行动执行 =====
    private void 执行攻击(战斗单位 攻击者, 战斗单位 目标)
    {
        if (目标 == null || !目标.存活) return;
        造成伤害(攻击者, 目标, 伤害类型.物理, 100);
    }

    private void 执行技能(战斗单位 施法者, string 技能标识, 战斗单位 目标)
    {
        if (!数据.技能.TryGetValue(技能标识, out var 技能)) return;
        if (施法者.冷却剩余(技能标识) > 0) return;
        if (!施法者.消耗魔力(技能.消耗魔力)) { if (施法者.是否我方) { 音效管理器.实例?.播放失败(); 发消息("魔力不足！"); } return; }
        int 熟练 = 施法者.熟练等级(技能标识);
        float 品质倍 = 品质工具.倍率(技能.品质档);
        switch (技能.类别枚举)
        {
            case 技能类别.攻击:
                {
                    int 倍率 = Mathf.RoundToInt(技能.数值 * 品质倍 * (1f + 熟练 * 技能.熟练伤害加成 / 100f));
                    造成伤害(施法者, 目标, 技能.伤害类型枚举, 倍率);
                    break;
                }
            case 技能类别.治疗:
                {
                    int 恢复 = Mathf.Max(1, Mathf.RoundToInt(技能.数值 * 品质倍 * (1f + 熟练 * 技能.熟练伤害加成 / 100f)));
                    目标.恢复生命(恢复);
                    事件.发布(new 治疗事件(目标, 恢复));
                    发消息($"{施法者.名称} 施展{技能.名称}，{目标.名称}恢复 {恢复} 点生命。");
                    break;
                }
            default:   // 增益 / 减益 / 控制：挂载 buff
                if (!string.IsNullOrEmpty(技能.挂载Buff) && 数据.Buffs.TryGetValue(技能.挂载Buff, out var buff))
                {
                    目标.添加Buff(buff);
                    var 实例 = 目标.Buffs.Find(b => b.定义.标识 == buff.标识);
                    事件.发布(new 状态变化事件(目标, buff.标识, 实例?.层数 ?? 1, buff.持续回合, false));
                    发消息($"{目标.名称} 获得「{buff.名称}」！");
                }
                break;
        }
        施法者.开始冷却(技能标识, 技能.冷却);
        // 玩家使用技能累积熟练度
        if (施法者.是否我方 && 熟练 < 玩家档案.熟练等级上限)
            ServiceRegistry.Get<技能服务>().记录熟练度(技能标识, 1);
    }

    private void 执行道具(战斗单位 使用者, string 物品标识, 战斗单位 目标)
    {
        if (!数据.物品.TryGetValue(物品标识, out var 物品) || !物品.战斗内使用) return;
        消耗道具.Add(物品标识);   // 记入消耗清单，结束统一扣档案（D14-B）
        switch (物品.使用效果枚举)
        {
            case 效果类型.恢复:
                {
                    int 恢复 = Mathf.Max(1, Mathf.RoundToInt(物品.恢复量 * 品质工具.倍率(物品.品质档)));
                    目标.恢复生命(恢复);
                    事件.发布(new 治疗事件(目标, 恢复));
                    发消息($"{使用者.名称} 使用{物品.名称}，恢复 {恢复} 点生命。");
                    break;
                }
            default:   // 增益 / 减益
                if (!string.IsNullOrEmpty(物品.挂载Buff) && 数据.Buffs.TryGetValue(物品.挂载Buff, out var buff))
                {
                    目标.添加Buff(buff);
                    事件.发布(new 状态变化事件(目标, buff.标识, 1, buff.持续回合, false));
                    发消息($"{目标.名称} 获得「{buff.名称}」！");
                }
                break;
        }
    }

    // ===== 统一伤害管线 =====
    private void 造成伤害(战斗单位 攻击者, 战斗单位 目标, 伤害类型 类型, int 倍率100)
    {
        if (目标 == null || !目标.存活) return;
        // 命中判定：基础命中率 vs 目标闪避
        if (Random.value >= 攻击者.命中率 - 目标.闪避率)
        {
            事件.发布(new 伤害事件(目标, 0, 类型, false, true));
            发消息($"{目标.名称} 闪避了{攻击者.名称}的攻击！");
            return;
        }
        bool 暴击 = Random.value < 攻击者.暴击率;
        int 伤害;
        if (类型 == 伤害类型.真实)
            伤害 = Mathf.Max(1, 倍率100);
        else
        {
            int 基础攻 = 类型 == 伤害类型.物理 ? 攻击者.当前物攻 : 攻击者.当前魔攻;
            int 基础防 = 类型 == 伤害类型.物理 ? 目标.当前物防 : 目标.当前魔防;
            伤害 = Mathf.Max(1, Mathf.RoundToInt(基础攻 * 倍率100 / 100f) - 基础防);
        }
        if (暴击) 伤害 = Mathf.RoundToInt(伤害 * 1.5f);
        伤害 = Mathf.Max(1, Mathf.RoundToInt(伤害 * 目标.抗性倍率(类型)));
        目标.受到伤害(伤害);
        事件.发布(new 伤害事件(目标, 伤害, 类型, 暴击, false));
        发消息($"{(暴击 ? "<color=#e0b84a>暴击！</color>" : "")}{攻击者.名称}对{目标.名称}造成 {伤害} 点伤害。");
        检查单位死亡(目标);
    }

    private void 检查单位死亡(战斗单位 单位)
    {
        if (单位.存活) return;
        if (单位.是否我方) 我方.Remove(单位);
        else { 敌方.Remove(单位); 结算单个敌人(单位); }
        事件.发布(new 目标变化事件(单位));
        发消息($"{单位.名称} 倒下了！");
    }

    // ===== 胜利结算（每死一个敌人即时结算） =====
    private void 结算单个敌人(战斗单位 敌人)
    {
        var 敌 = 敌人.源数据;
        if (敌 == null) return;
        档案.金币 += 敌.金币奖励; 累计金币 += 敌.金币奖励;
        bool 升级 = 档案.获得经验(敌.经验奖励); 累计经验 += 敌.经验奖励;
        事件.发布(new 金币变化事件(档案.金币, 敌.金币奖励));
        事件.发布(new 经验变化事件(档案.等级, 档案.经验, 档案.升级所需经验, 升级));
        if (升级) 事件.发布(new 属性变化事件(档案.体力, 档案.力量, 档案.智力, 档案.敏捷, 档案.自由属性点));
        if (!string.IsNullOrEmpty(敌.掉落物品) && Random.value < 敌.掉落概率)
        {
            档案.添加物品(敌.掉落物品);
            事件.发布(new 背包变化事件(敌.掉落物品, 1, 变化原因.获得));
        }
        ServiceRegistry.Get<QuestService>().记录击败(敌.标识);
        发消息($"击败{敌.名称}！获得 {敌.金币奖励} 金币、{敌.经验奖励} 经验。");
    }

    // ===== 行动完成 → 推进/结束 =====
    private void 行动完成()
    {
        if (检查战斗结束()) return;
        推进();
    }

    private bool 检查战斗结束()
    {
        if (!战斗中) return true;
        if (敌方.Count == 0) { 结束战斗(true, $"战斗胜利！共获得 {累计金币} 金币、{累计经验} 经验。", 胜利节点); return true; }
        if (我方.Count == 0) { 结束战斗(false, "你被击败了……", 返回节点); return true; }
        return false;
    }

    // ===== 战斗结束 + 离场回写 =====
    private void 结束战斗(bool 胜利, string 文本, string 进入节点)
    {
        战斗中 = false;
        结果节点 = 进入节点;
        if (胜利) { }
        else if (结果节点 == 返回节点) 结算失败惩罚();
        结算回写();
        事件.发布(new 战斗结束事件(胜利, 文本));
    }

    // 失败惩罚：掉 10% 金币 + 回城满状态（D15-A）
    private void 结算失败惩罚()
    {
        int 损失 = 档案.金币 * 10 / 100;
        if (损失 > 0)
        {
            档案.金币 -= 损失;
            事件.发布(new 金币变化事件(档案.金币, -损失));
        }
        档案.生命 = 档案.最大生命;
        档案.魔力 = 档案.最大魔力;
        发消息($"你损失了 {损失} 金币，被送回安全处。");
    }

    // 离场回写：生命/魔力 + 道具消耗统一扣；Buff/临时状态丢弃（战斗外无临时buff）
    private void 结算回写()
    {
        档案.生命 = 玩家.生命;
        档案.魔力 = 玩家.魔力;
        foreach (var 标识 in 消耗道具) 档案.移除物品(标识);
        消耗道具.Clear();
    }

    // 战斗结束后面板 [继续]：进入结果节点
    public void 返回()
    {
        if (结果节点 == "__探索胜利") { ServiceRegistry.Get<探索服务>().战斗胜利(); return; }
        if (结果节点 == "__探索返回") { ServiceRegistry.Get<探索服务>().战斗逃跑(); return; }
        if (!string.IsNullOrEmpty(结果节点)) ServiceRegistry.Get<DialogueService>().进入节点(结果节点);
    }

    private List<战斗单位> 全部单位()
    {
        var 列表 = new List<战斗单位>();
        列表.AddRange(我方); 列表.AddRange(敌方);
        return 列表;
    }

    private void 发消息(string 文本)
    {
        当前消息 = 文本;
        事件.发布(new 日志事件(日志类型.操作, 文本));
    }
}
