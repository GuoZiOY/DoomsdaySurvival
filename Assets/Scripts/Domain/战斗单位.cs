using System;
using System.Collections.Generic;

// 战斗单位：战斗中的统一实体（玩家/敌人都用它）。纯 C# 领域模型（零 UnityEngine 依赖）。
// 由战斗状态机(BattleService)驱动：状态机算伤害/流程，单位只做状态变更，不发布事件。
// 生命周期：进场投影(从玩家档案/敌人数据生成) → 战斗过程 → 离场回写(状态机把 生命/魔力/消耗 回写档案)。
public sealed class 战斗单位
{
    // —— 身份 ——
    public string 名称;
    public bool 是否我方;
    public 敌人数据 源数据;   // 敌人单位的来源数据（AI行动表/目标策略/掉落用）；玩家为 null

    // —— 当前数值（战斗过程） ——
    public int 生命, 最大生命;
    public int 魔力, 最大魔力;
    public int 精力, 最大精力;   // 物理技能消耗（敌人充足）
    public int 基础物攻, 基础魔攻, 基础物防, 基础魔防, 基础速度, 基础敏捷;
    public float 暴击率, 闪避率;
    public float 命中率 = 0.95f;

    // —— 防御/血条 ——
    public int 护盾, 护盾上限;
    public bool 破防;
    public bool 有护盾 => 护盾上限 > 0;

    // —— 增幅点 BP（八方旅人式：蓄力增幅攻击；仅我方玩家用） ——
    public int BP;
    public const int BP上限 = 3;

    // —— 弱点（我方命中敌方弱点用） ——
    public 五行 五行属性;                        // 敌方五行（魔法弱点判定）
    public readonly List<武器种类> 物理弱点 = new List<武器种类>();   // 敌方物理弱点武器种类

    // —— 抗性（受伤害衰减，装备来源，主力玩家用） ——
    public int 抗性百分比;                       // 0~50，血量的非线性减伤比例

    // —— 玩家武器（物理弱点 + 切武器用） ——
    public string 主手武器, 副手武器;    // 玩家装备的主/副手物品标识（敌我通用，敌方恒空）
    public string 当前武器标识 = "";     // 当前持用的武器标识（物理弱点判定）
    public 武器种类 当前武器 = 武器种类.无;   // 当前武器种类

    // —— 抗性表：伤害类型 → 倍率（缺省1.0） ——
    public readonly Dictionary<伤害类型, float> 抗性 = new Dictionary<伤害类型, float>();

    // —— Buff 列表 ——
    public readonly List<Buff实例> Buffs = new List<Buff实例>();

    // —— 技能冷却：技能标识 → 剩余回合 ——
    public readonly Dictionary<string, int> 技能冷却 = new Dictionary<string, int>();

    // —— 已学技能（我方投影 / 敌人技能） ——
    public readonly HashSet<string> 已学技能 = new HashSet<string>();
    public readonly Dictionary<string, int> 技能熟练 = new Dictionary<string, int>();

    // —— 战斗消耗道具清单（离场统一回写扣档案，D14-B） ——
    public readonly List<string> 消耗道具 = new List<string>();

    public bool 存活 => 生命 > 0;

    // 当前攻防（基础 × buff修正；buff=增益/减益按属性加减百分比）
    public int 当前物攻 => 修正(基础物攻, 属性修正("攻击"));
    public int 当前魔攻 => 修正(基础魔攻, 属性修正("魔攻"));
    public int 当前物防 => 修正(基础物防, 属性修正("防御"));
    public int 当前魔防 => 修正(基础魔防, 属性修正("魔防"));
    // 敏捷 buff（猎人直觉）→ 速度/暴击/闪避 同步提升（速度=敏捷×2，等比修正）
    public int 当前速度 => 修正(基础速度, 属性修正("敏捷"));
    // 暴击/闪避硬上限 80%：防大量词缀堆叠逼近/超 100%（Random.value < 1 恒真）导致必暴/必闪，并保证随机波动空间
    public float 当前暴击率 => Math.Clamp(暴击率 * (1f + 属性修正("敏捷") / 100f), 0f, 0.8f);
    public float 当前闪避率 => Math.Clamp(闪避率 * (1f + 属性修正("敏捷") / 100f), 0f, 0.8f);

    // 持续伤害量（异常且非控制 buff 的每回合伤害总和）；时机："开始"=回合开始扣血（缺省）/ "结束"=回合结束扣血
    public int 持续伤害量(string 时机 = "开始")
    {
        int 总 = 0;
        foreach (var b in Buffs)
            if (b.定义.类型枚举 == Buff类型.异常 && !b.定义.控制)
            {
                string 结算 = string.IsNullOrEmpty(b.定义.结算时机) ? "开始" : b.定义.结算时机;
                if (结算 == 时机) 总 += b.定义.数值 * b.层数;
            }
        return 总;
    }

    // 移除一个随机的减益 buff（小净化用）；无减益返回 false
    public bool 移除随机减益()
    {
        var 减益 = Buffs.FindAll(b => b.定义.类型枚举 == Buff类型.减益);
        if (减益.Count == 0) return false;
        var 选中 = 减益[Random(减益.Count)];
        Buffs.Remove(选中);
        return true;
    }

    // 控制类异常（麻痹/眩晕）：轮到该单位时跳过行动
    public bool 无法行动 => Buffs.Exists(b => b.定义.类型枚举 == Buff类型.异常 && b.定义.控制);

    // 某属性当前百分比修正（增益为正、减益为负）
    public int 属性修正(string 属性)
    {
        int 总 = 0;
        foreach (var b in Buffs)
            if ((b.定义.类型枚举 == Buff类型.增益 || b.定义.类型枚举 == Buff类型.减益) && b.定义.属性 == 属性)
                总 += b.定义.数值 * b.层数;
        return 总;
    }

    public float 抗性倍率(伤害类型 类型) => 抗性.TryGetValue(类型, out var 倍) ? 倍 : 1f;

    // —— 生命/魔力 ——

    public void 受到伤害(int 数值)
    {
        if (数值 <= 0) return;
        // 护盾优先抵扣
        for (int i = Buffs.Count - 1; i >= 0 && 数值 > 0; i--)
        {
            var b = Buffs[i];
            if (b.定义.类型枚举 != Buff类型.护盾) continue;
            int 扣 = 数值 < b.剩余护盾 ? 数值 : b.剩余护盾;
            b.剩余护盾 -= 扣;
            数值 -= 扣;
            if (b.剩余护盾 <= 0) Buffs.RemoveAt(i);
        }
        生命 = Max(0, 生命 - 数值);
    }

    public void 恢复生命(int 数值) => 生命 = Min(最大生命, 生命 + 数值);
    public void 恢复魔力(int 数值) => 魔力 = Min(最大魔力, 魔力 + 数值);
    public bool 消耗魔力(int 数值)
    {
        if (魔力 < 数值) return false;
        魔力 -= 数值;
        return true;
    }

    public void 恢复精力(int 数值) => 精力 = Min(最大精力, 精力 + 数值);
    public bool 消耗精力(int 数值)
    {
        if (精力 < 数值) return false;
        精力 -= 数值;
        return true;
    }

    // —— Buff ——

    public void 添加Buff(Buff定义 定义, int 层 = 1)
    {
        foreach (var b in Buffs)
            if (b.定义.标识 == 定义.标识)
            {
                b.层数 = Min(b.层数 + 层, 定义.最大层数);
                b.剩余回合 = 定义.持续回合;                     // 刷新计时
                if (定义.类型枚举 == Buff类型.护盾) b.剩余护盾 += 定义.数值 * 层;  // 护盾累加
                return;
            }
        Buffs.Add(new Buff实例
        {
            定义 = 定义,
            层数 = Min(层, 定义.最大层数),
            剩余回合 = 定义.持续回合,
            剩余护盾 = 定义.类型枚举 == Buff类型.护盾 ? 定义.数值 * 层 : 0
        });
    }

    public void 移除Buff(string 标识) => Buffs.RemoveAll(b => b.定义.标识 == 标识);

    // 回合结束：非护盾 buff 计时递减移除；技能冷却递减
    public void 回合结束()
    {
        for (int i = Buffs.Count - 1; i >= 0; i--)
        {
            var b = Buffs[i];
            if (b.定义.类型枚举 == Buff类型.护盾) continue;
            b.剩余回合--;
            if (b.剩余回合 <= 0) Buffs.RemoveAt(i);
        }
        var 到期 = new List<string>();
        foreach (var kv in 技能冷却)
        {
            技能冷却[kv.Key] = kv.Value - 1;
            if (技能冷却[kv.Key] <= 0) 到期.Add(kv.Key);
        }
        foreach (var k in 到期) 技能冷却.Remove(k);
    }

    // —— 技能 ——

    public bool 掌握(string 标识) => 已学技能.Contains(标识);
    public int 熟练等级(string 标识) => 技能熟练.TryGetValue(标识, out var v) ? v : 0;
    public int 冷却剩余(string 标识) => 技能冷却.TryGetValue(标识, out var c) ? c : 0;
    public void 开始冷却(string 标识, int 回合)
    {
        if (回合 > 0) 技能冷却[标识] = 回合;
    }

    // —— 工厂（投影/生成） ——

    // 玩家 → 战斗单位（进场投影）
    public static 战斗单位 从玩家投影(玩家档案 玩家)
    {
        var 单位 = new 战斗单位
        {
            名称 = "见习守卫",
            是否我方 = true,
            最大生命 = 玩家.最大生命,
            生命 = 玩家.生命,
            最大魔力 = 玩家.最大魔力,
            魔力 = 玩家.魔力,
            最大精力 = 玩家.最大精力,
            精力 = 玩家.精力,
            基础物攻 = 玩家.物理伤害,
            基础魔攻 = 玩家.魔法伤害,
            基础物防 = 玩家.总防御,
            基础魔防 = 玩家.总防御,          // 暂共用总防御，后期拆
            基础速度 = 玩家.敏捷 * 2 + 玩家.速度加成,   // 速度 = 敏捷×2 + 装备速度词缀
            基础敏捷 = 玩家.敏捷,            // 敏捷 buff（猎人直觉）作用于速度/暴击/闪避
            暴击率 = 玩家.暴击概率,
            闪避率 = 玩家.闪避概率,
            命中率 = 0.95f + 玩家.命中加成
        };
        foreach (var s in 玩家.已学技能)
        {
            单位.已学技能.Add(s.标识);
            单位.技能熟练[s.标识] = s.熟练等级;
        }
        // 玩家武器：主/副手武器类型（物理弱点判定 + 切武器）
        单位.主手武器 = 玩家.装备标识("主手");
        单位.副手武器 = 玩家.装备标识("副手");
        单位.当前武器标识 = string.IsNullOrEmpty(单位.主手武器) ? 单位.副手武器 : 单位.主手武器;
        单位.当前武器 = 玩家.武器种类解析?.Invoke(单位.当前武器标识) ?? 武器种类.无;
        单位.抗性百分比 = 玩家.抗性百分比;   // 装备抗性（非线性封顶 50）
        return 单位;
    }

    // 敌人 → 战斗单位（生成）
    public static 战斗单位 从敌人生成(敌人数据 敌人)
    {
        var 单位 = new 战斗单位
        {
            名称 = 敌人.名称,
            是否我方 = false,
            源数据 = 敌人,
            最大生命 = 敌人.生命,
            生命 = 敌人.生命,
            最大魔力 = Max(50, 敌人.生命),   // 敌人魔力充足，保证魔法技能可放
            魔力 = Max(50, 敌人.生命),
            最大精力 = 100,                        // 敌人精力充足，物理技能可放
            精力 = 100,
            基础物攻 = 敌人.攻击,
            基础魔攻 = 敌人.攻击,            // 敌人暂物魔同攻，后期细分
            基础物防 = 敌人.防御,
            基础魔防 = 敌人.魔防,
            基础速度 = 敌人.速度,
            暴击率 = 0.05f,
            闪避率 = 0.05f
        };
        if (敌人.抗性 != null)
            foreach (var r in 敌人.抗性) 单位.抗性[r.类型枚举] = r.倍率;
        if (敌人.行动表 != null)
            foreach (var a in 敌人.行动表)
                if (a.行动 != "普攻" && !string.IsNullOrEmpty(a.行动)) 单位.已学技能.Add(a.行动);
        // 敌人护盾（= 防御 × 系数，系数缺省 2）与 弱点（物理武器种类 + 五行）
        float 系数 = 敌人.护盾系数 > 0 ? 敌人.护盾系数 : 2f;
        单位.护盾上限 = (int)(敌人.防御 * 系数);
        单位.护盾 = 单位.护盾上限;
        单位.五行属性 = 敌人.五行枚举;
        if (敌人.物理弱点 != null)
            foreach (var w in 敌人.物理弱点)
                if (!string.IsNullOrEmpty(w)) 单位.物理弱点.Add(数据解析.枚举<武器种类>(w));
        return 单位;
    }

    // 助战 → 战斗单位（轻量助战：复用敌人数据形态，但归属我方；AI 行动表自动出手）
    public static 战斗单位 从助战生成(敌人数据 助战)
    {
        var 单位 = 从敌人生成(助战);
        单位.是否我方 = true;   // 归我方：敌人 AI 会攻击它，玩家不可手动操控，倒下无碍
        return 单位;
    }

    // —— 纯 C# 数值工具（零 UnityEngine 依赖） ——
    private static int Min(int a, int b) => a < b ? a : b;
    private static int Max(int a, int b) => a > b ? a : b;
    private static int Random(int 上限) => 上限 <= 0 ? 0 : new System.Random().Next(上限);
    private static int Round(float v) => (int)(v + 0.5f);
    private static int 修正(int 基础, int 百分比) => Max(0, Round(基础 * (1f + 百分比 / 100f)));
}

// Buff 实例：战斗中实际挂载的状态
public sealed class Buff实例
{
    public Buff定义 定义;
    public int 剩余回合;
    public int 层数;
    public int 剩余护盾;   // 仅护盾用（随伤害递减）
}
