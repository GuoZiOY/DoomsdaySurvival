using System;
using System.Collections.Generic;

// 战斗单位：战斗中的统一实体（玩家/敌人都用它）。纯 C# 领域模型（零 UnityEngine 依赖）。
// 末日版：无魔力/魔攻/魔防/护盾/五行/弱点/增幅——伤害 = 近战(力量)/远程(武器)，
// 防御 = 百分比减伤（物防 1 点 = 1%，封顶 50%），抗性 = 装备第二层减伤（封顶 50%）。
// 由战斗状态机(BattleService)驱动：状态机算伤害/流程，单位只做状态变更，不发布事件。
public sealed class 战斗单位
{
    // —— 身份 ——
    public string 名称;
    public string 实例标识;   // **一个单位一个唯一标识**（房间层 = 网格实体标识 如「便利店_门厅#敌人0」；其余入口 = 「定义标识#序号」）
                              // 敌人定义标识 可以重复（同种敌人多只），但单位标识绝不重复 —— 战斗里一个标识 = 一个单位
    public bool 是否我方;
    public 敌人数据 源数据;   // 敌人单位的来源数据（AI行动表/目标策略/掉落用）；玩家为 null

    // —— 当前数值（战斗过程） ——
    public int 生命, 最大生命;
    public int 精力, 最大精力;   // 技能统一消耗精力（行动点）
    public int 基础近战, 基础远程, 基础物防, 基础速度, 基础敏捷;
    public float 暴击率, 闪避率;
    public float 命中率 = 0.95f;

    // —— 抗性（装备第二层减伤，0~50，非线性封顶，主力玩家用） ——
    public int 抗性百分比;

    // —— 无敌（天赋触发后：免疫伤害一轮） ——
    public int 无敌回合;   // >0 = 本轮免疫伤害；轮次结束递减
    public bool 无敌 => 无敌回合 > 0;

    // —— 玩家武器（近/远程伤害类型 + 切武器用） ——
    public string 主手武器, 副手武器;    // 玩家装备的主/副手物品标识（敌我通用，敌方恒空）
    public string 当前武器标识 = "";     // 当前持用的武器标识
    public 武器种类 当前武器 = 武器种类.无;   // 当前武器种类

    // —— 战斗沙盒（行动条即时制 + 棋盘） ——
    public float 行动条;          // 0~100，满 100 执行当前模式行动；重置 = 清零重计
    public int 列, 行;            // 棋盘坐标（列 0 基，宽 > 高）
    public int 攻击距离 = 1;      // 当前有效攻击射程（玩家 = 当前武器射程；敌人 = 数据.攻击距离）
    public string 移动AI;         // 敌人自动移动策略："冲锋"/"风筝"/"驻守"（玩家用 运动模式，此字段空）
    public int 身形 = 1;          // 占格宽（1 单格 / 2 横向大体型）
    public int 运动模式;          // 玩家 运动模式：0 自动前进 / 1 原地等待 / 2 自动后退（敌人忽略）
    public int 行动模式;          // 行动模式：0 自动攻击 / 1 自动防御
    public bool 防御姿态;         // 防御姿态生效中（行动模式=防御 且 满条触发后，持续到下次行动；期间受击减半）

    // （行动 频率 改由 意图间隔秒 决定，见 移动/攻击/技能间隔秒；基础速度 仅 用于 逃跑 对比 等）

    // —— 行动意图（读条 内容）：意图 在 读条 开始 时 锁定，满条 才 执行（全 B 承诺制）——
    // 意图 决定 推进 速度 与 行动条 颜色（移动 金 / 攻击 血红 / 技能 紫 / 戒备 灰蓝）
    public enum 意图类型 { 移动, 攻击, 技能, 戒备 }
    public 意图类型 当前意图 = 意图类型.移动;   // 本 轮 读条 对应 的 意图
    public string 意图技能;                     // 技能 意图：锁 的 技能 标识（读条 大招）
    public bool 意图已选;                       // 本 轮 是否 已 锁定 意图（锁定 后 到 满条 不变）

    // —— 行动 间隔（现实 秒 = 一次 行动 用时；无 加成 基准 5 秒）——
    // 玩家：移动/技能/攻击 均 5 秒（装备 移速/武器 攻速 倍率 后续 生效）；敌人：由 敌人数据 天然 设定
    public float 移动间隔秒 = 5f;
    public float 攻击间隔秒 = 5f;
    public float 技能间隔秒 = 5f;
    public float 攻速倍率 = 1f;   // 武器 攻速 加成（预留）
    public float 移速倍率 = 1f;   // 装备 移速 加成（预留）

    // —— Buff 列表（增益/减益/异常；无护盾） ——
    public readonly List<Buff实例> Buffs = new List<Buff实例>();

    // —— 技能冷却：技能标识 → 剩余秒（现实秒，战斗中 每帧 推进；非 轮） ——
    public readonly Dictionary<string, float> 技能冷却 = new Dictionary<string, float>();

    // —— 已学技能（我方投影 / 敌人技能） ——
    public readonly HashSet<string> 已学技能 = new HashSet<string>();
    public readonly Dictionary<string, int> 技能熟练 = new Dictionary<string, int>();

    // 注：原有 `List<string> 消耗道具`（"离场统一回写扣档案"）—— v51 刀15 删了 BattleService 里那份，
    //     v51 刀24 删掉这里最后一份：全仓库**只声明、零写入**（真正的消耗走"扣战斗容器"直接改容器）。
    //     留着会让读者以为"战斗消耗品有统一登记口"，照着它登记还会双重扣减。

    public bool 存活 => 生命 > 0;

    // 当前攻防（基础 × buff修正；增益/减益按属性加减百分比）
    public int 当前近战 => 修正(基础近战, 属性修正("攻击"));
    public int 当前远程 => 修正(基础远程, 属性修正("攻击"));
    public int 当前物防 => 修正(基础物防, 属性修正("防御"));   // 减伤百分比（1 点 = 1%）
    // 敏捷 buff（猎人直觉）→ 速度/暴击/闪避 同步提升
    public int 当前速度 => 修正(基础速度, 属性修正("敏捷"));
    public float 当前暴击率 => Math.Clamp(暴击率 * (1f + 属性修正("敏捷") / 100f), 0f, 0.8f);
    public float 当前闪避率 => Math.Clamp(闪避率 * (1f + 属性修正("敏捷") / 100f), 0f, 0.8f);

    // 防御减伤系数：物防 → 减伤%（1:1，封顶 50%）
    public float 防御减伤 => Math.Min(当前物防, 50) / 100f;

    // 持续伤害量（异常且非控制 buff 的每轮伤害总和）；时机："开始"=轮开始扣血（缺省）/ "结束"=轮结束扣血
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

    // 眩晕（秒制 控制）：此 期间 无法 行动、行动条 不 推进（读条 清零）；0 = 无。与 Buff 控制 共用 无法行动
    public float 眩晕剩余秒;
    public void 施加眩晕(float 秒) { if (秒 > 0f && 秒 > 眩晕剩余秒) 眩晕剩余秒 = 秒; }   // 刷新 取 更长（纯 C#，不 依赖 UnityEngine）

    // 控制类异常（麻痹/眩晕 Buff）或 眩晕秒：该单位 无法 行动 / 读条 冻结
    public bool 无法行动 => Buffs.Exists(b => b.定义.类型枚举 == Buff类型.异常 && b.定义.控制) || 眩晕剩余秒 > 0f;

    // 某属性当前百分比修正（增益为正、减益为负）
    public int 属性修正(string 属性)
    {
        int 总 = 0;
        foreach (var b in Buffs)
            if ((b.定义.类型枚举 == Buff类型.增益 || b.定义.类型枚举 == Buff类型.减益) && b.定义.属性 == 属性)
                总 += b.定义.数值 * b.层数;
        return 总;
    }

    // —— 生命/精力 ——

    public void 受到伤害(int 数值)
    {
        if (数值 <= 0) return;
        生命 = Max(0, 生命 - 数值);
    }

    public void 恢复生命(int 数值) => 生命 = Min(最大生命, 生命 + 数值);
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
                return;
            }
        Buffs.Add(new Buff实例
        {
            定义 = 定义,
            层数 = Min(层, 定义.最大层数),
            剩余回合 = 定义.持续回合
        });
    }

    public void 移除Buff(string 标识) => Buffs.RemoveAll(b => b.定义.标识 == 标识);

    // 轮次结束：buff 计时递减移除；无敌回合递减（技能冷却 改 现实秒，由 推进冷却 逐帧 推进，不走 轮）
    public void 回合结束()
    {
        for (int i = Buffs.Count - 1; i >= 0; i--)
        {
            var b = Buffs[i];
            b.剩余回合--;
            if (b.剩余回合 <= 0) Buffs.RemoveAt(i);
        }
        if (无敌回合 > 0) 无敌回合--;
    }

    // —— 技能 ——

    public bool 掌握(string 标识) => 已学技能.Contains(标识);
    public int 熟练等级(string 标识) => 技能熟练.TryGetValue(标识, out var v) ? v : 0;
    public float 冷却剩余(string 标识) => 技能冷却.TryGetValue(标识, out var c) ? c : 0f;

    public void 开始冷却(string 标识, float 秒数)
    {
        if (秒数 > 0f) 技能冷却[标识] = 秒数;
    }

    // 冷却 推进：现实秒 逐帧 递减（战斗中 每帧 调用）；到 0 移除
    public void 推进冷却(float 现实秒)
    {
        if (技能冷却.Count == 0) return;
        foreach (var 标识 in new List<string>(技能冷却.Keys))   // 快照 key，避免 边遍历边改
        {
            float 剩 = 技能冷却[标识] - 现实秒;
            if (剩 <= 0f) 技能冷却.Remove(标识);
            else 技能冷却[标识] = 剩;
        }
    }

    // —— 工厂（投影/生成） ——

    // 玩家 → 战斗单位（进场投影）
    public static 战斗单位 从玩家投影(玩家档案 玩家)
    {
        var 单位 = new 战斗单位
        {
            名称 = "幸存者",
            是否我方 = true,
            最大生命 = 玩家.最大生命,
            生命 = 玩家.生命,
            最大精力 = 玩家.最大行动点,
            精力 = 玩家.行动点,
            基础近战 = 玩家.近战伤害,
            基础远程 = 玩家.枪械伤害,
            基础物防 = 玩家.总防御,            // 减伤百分比（1 点 = 1%）
            基础速度 = 玩家.速度,              // 速度 = 敏捷×2 + 装备速度词缀
            基础敏捷 = 玩家.敏捷,              // 敏捷 buff 作用于速度/暴击/闪避
            暴击率 = 玩家.暴击概率,
            闪避率 = 玩家.闪避概率,
            命中率 = 0.95f,                   // 原为 0.95f + 玩家.命中加成 —— 那个"命中加成"是奇幻残留且恒 0，v51 已删
            抗性百分比 = 玩家.抗性百分比       // 装备抗性（非线性封顶 50）
        };
        // 玩家技能：战斗内可用 = 战斗技能槽（固定 6 槽）内技能（学更多技能 → 槽满则不进战斗）
        foreach (var 槽标识 in 玩家.战斗技能槽 ?? new List<string>())
        {
            if (string.IsNullOrEmpty(槽标识)) continue;
            单位.已学技能.Add(槽标识);
            var 掌握 = 玩家.已学技能.Find(s => s.标识 == 槽标识);
            if (掌握 != null) 单位.技能熟练[槽标识] = 掌握.熟练等级;
        }
        // 玩家武器：主/副手武器类型（切武器 + 近/远程伤害类型）
        单位.主手武器 = 玩家.装备标识("主手");
        单位.副手武器 = 玩家.装备标识("副手");
        单位.当前武器标识 = string.IsNullOrEmpty(单位.主手武器) ? 单位.副手武器 : 单位.主手武器;
        单位.当前武器 = 玩家.武器种类解析?.Invoke(单位.当前武器标识) ?? 武器种类.无;
        return 单位;
    }

    // 敌人 → 战斗单位（生成）
    public static 战斗单位 从敌人生成(敌人数据 敌人)
    {
        var 单位 = new 战斗单位
        {
            名称 = 敌人.名称,
            实例标识 = 敌人.标识,                 // 默认 = 定义标识；房间层进来时会覆盖成"网格实体标识"（一人一个唯一标识）
            是否我方 = false,
            源数据 = 敌人,
            最大生命 = 敌人.生命,
            生命 = 敌人.生命,
            最大精力 = 100,                        // 敌人精力充足，技能可放
            精力 = 100,
            基础近战 = 敌人.攻击,
            基础远程 = 敌人.攻击,            // 敌人近/远程同攻（伤害类型由 攻击距离 区分）
            基础物防 = 敌人.防御,            // 减伤百分比（1 点 = 1%）
            基础速度 = 敌人.速度,
            暴击率 = 0.05f,
            闪避率 = 0.05f
        };
        if (敌人.行动表 != null)
            foreach (var a in 敌人.行动表)
                if (a.行动 != "普攻" && !string.IsNullOrEmpty(a.行动)) 单位.已学技能.Add(a.行动);
        // 战斗沙盒：攻击距离 / 移动AI / 身形
        单位.攻击距离 = Math.Max(1, 敌人.攻击距离);
        单位.移动AI = 敌人.移动AI;
        单位.身形 = Math.Max(1, 敌人.身形);
        // 行动 间隔（现实 秒）：敌人 天然 设定；缺省 0 → 5 秒
        单位.移动间隔秒 = 敌人.移动间隔秒 > 0f ? 敌人.移动间隔秒 : 5f;
        单位.攻击间隔秒 = 敌人.攻击间隔秒 > 0f ? 敌人.攻击间隔秒 : 5f;
        单位.技能间隔秒 = 敌人.技能间隔秒 > 0f ? 敌人.技能间隔秒 : 5f;
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
}
