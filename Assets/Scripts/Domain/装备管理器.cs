using System;
using System.Collections.Generic;

// 装备管理器：装备/穿戴容器 领域逻辑（玩家档案 组合的子管理器——职责分离、调度器模式）。
// 负责：槽位 装备/卸下/换槽/容器应用/来源追溯；装备驱动 派生数值（负重/战斗数值/词缀总值/耐久）。
// 数据（装备列表/解析委托）由 玩家档案 持有，本管理器 操作之；玩家档案 门面转发调用。
public sealed class 装备管理器
{
    public readonly 玩家档案 玩家;
    public 装备管理器(玩家档案 玩家) { this.玩家 = 玩家; }

    // —— 装备 数据 ——
    public List<装备记录> 装备 => 玩家.装备;

    public string 装备标识(string 槽位)
    {
        foreach (var e in 玩家.装备) if (e.槽位 == 槽位) return e.标识;
        return "";
    }

    // 按 装备记录 完整恢复槽位（卸下失败穿回/换装回滚用）：恢复 词缀/耐久/容器尺寸/容器物品/来源，
    // 避免容器位（弹挂/腰封/背包）穿回时 内部物品 被清空。返回 被替换的旧记录（空槽返回 null）。
    public 装备记录 装备到槽(string 槽位, 装备记录 记录)
    {
        if (记录 == null || string.IsNullOrEmpty(记录.标识)) return null;
        foreach (var e in 玩家.装备)
            if (e.槽位 == 槽位)
            {
                var 旧 = new 装备记录(槽位, e.标识) { 词缀 = e.词缀, 当前耐久 = e.当前耐久, 容器列 = e.容器列, 容器行 = e.容器行, 容器物品 = e.容器物品, 来源 = e.来源 };
                e.标识 = 记录.标识;
                e.词缀 = 记录.词缀;
                e.当前耐久 = 记录.当前耐久;
                e.容器列 = 记录.容器列;
                e.容器行 = 记录.容器行;
                e.容器物品 = 记录.容器物品;
                e.来源 = 记录.来源;
                return 旧;
            }
        var 新 = new 装备记录(槽位, 记录.标识)
        {
            词缀 = 记录.词缀,
            当前耐久 = 记录.当前耐久,
            容器列 = 记录.容器列,
            容器行 = 记录.容器行,
            容器物品 = 记录.容器物品,
            来源 = 记录.来源,
        };
        玩家.装备.Add(新);
        return null;
    }

    public 装备记录 装备到槽(string 槽位, string 标识, List<词缀条> 词缀 = null, int? 当前耐久 = null, 物品堆叠 容器源 = null, string 来源 = null)
    {
        bool 容器位 = 槽位 == "弹挂" || 槽位 == "腰封" || 槽位 == "背包";   // 穿戴容器：内部有网格
        foreach (var e in 玩家.装备)
            if (e.槽位 == 槽位)
            {
                var 旧 = new 装备记录(槽位, e.标识) { 词缀 = e.词缀, 当前耐久 = e.当前耐久, 容器列 = e.容器列, 容器行 = e.容器行, 容器物品 = e.容器物品, 来源 = e.来源 };
                e.标识 = 标识; e.词缀 = 词缀;
                e.当前耐久 = 当前耐久 ?? 有效最大耐久(标识);   // 透传实例耐久；无则按模板初始化
                if (来源 != null) e.来源 = 来源;   // 记录装备前所在网格（卸下时"从哪来回哪去"）
                if (容器位) 应用容器(e, 标识, 容器源);   // 容器位：携带容器源数据（内部物品保留）或按模板初始化
                return 旧;
            }
        var 新 = new 装备记录(槽位, 标识) { 词缀 = 词缀, 当前耐久 = 当前耐久 ?? 有效最大耐久(标识), 来源 = 来源 };
        if (容器位) 应用容器(新, 标识, 容器源);
        玩家.装备.Add(新);
        return null;
    }

    // 容器位：容器源 提供容器数据（卸下再穿上时内部物品保留）→ 直接用；否则 新空容器 + 模板尺寸
    private void 应用容器(装备记录 记录, string 标识, 物品堆叠 容器源)
    {
        if (容器源 != null && 容器源.容器列 > 0)
        {
            记录.容器列 = 容器源.容器列;
            记录.容器行 = 容器源.容器行;
            记录.容器物品 = 容器源.容器物品;
            return;
        }
        记录.容器物品 = 记录.容器物品 ?? new List<物品堆叠>();
        应用容器尺寸(记录, 标识);
    }

    // 容器位：按模板（注入的 容器尺寸解析）设置实例网格尺寸
    private void 应用容器尺寸(装备记录 记录, string 标识)
    {
        if (玩家.容器尺寸解析 != null)
        {
            var (列, 行) = 玩家.容器尺寸解析(标识);
            记录.容器列 = 列;
            记录.容器行 = 行;
        }
    }

    public 装备记录 卸下装备(string 槽位)
    {
        for (int i = 玩家.装备.Count - 1; i >= 0; i--)
            if (玩家.装备[i].槽位 == 槽位) { var 记录 = 玩家.装备[i]; 玩家.装备.RemoveAt(i); return 记录; }
        return null;
    }

    public bool 已装备(string 标识)
    {
        foreach (var e in 玩家.装备) if (e.标识 == 标识) return true;
        return false;
    }

    // 兼容旧引用：饰品槽自动分配（末日槽位无饰品，保留方法防旧代码断）
    public string 饰品目标槽()
    {
        if (string.IsNullOrEmpty(装备标识("饰品1"))) return "饰品1";
        if (string.IsNullOrEmpty(装备标识("饰品2"))) return "饰品2";
        return "饰品1";
    }

    // 背包装备 → 网格尺寸（默认背包 50 格 10×5 / 战术背包 5×4 / 登山包 6×5 / 腰包 4×2）
    public (int 列, int 行) 背包网格尺寸(string 包标识 = null)
    {
        if (string.IsNullOrEmpty(包标识)) 包标识 = 装备标识("背包");
        return 玩家.背包服务.背包网格尺寸(包标识);
    }

    public void 应用背包装备()
    {
        玩家.背包服务.应用背包装备(装备标识("背包"));
    }

    // 有效最大耐久：优先 items.json 配置；缺省给"有攻击或防御加成"的装备默认 15（武器/防具）
    public int 有效最大耐久(string 标识)
    {
        int 配 = 玩家.最大耐久解析?.Invoke(标识) ?? 0;
        if (配 > 0) return 配;
        if ((玩家.攻击加成解析?.Invoke(标识) ?? 0) > 0 || (玩家.防御加成解析?.Invoke(标识) ?? 0) > 0) return 15;
        return 0;
    }

    // 指定槽装备当前耐久
    public int 装备当前耐久(string 槽位)
    {
        foreach (var e in 玩家.装备) if (e.槽位 == 槽位) return e.当前耐久;
        return 0;
    }

    // 指定槽装备是否损坏（有装备、有耐久、且 当前耐久<=0）
    public bool 装备已损坏(string 槽位)
    {
        string 标识 = 装备标识(槽位);
        if (string.IsNullOrEmpty(标识)) return false;
        if (有效最大耐久(标识) <= 0) return false;
        foreach (var e in 玩家.装备) if (e.槽位 == 槽位) return e.当前耐久 <= 0;
        return false;
    }

    // 扣指定槽位装备耐久（不掉出负数）
    public void 扣装备耐久(string 槽位, int 量)
    {
        if (量 <= 0) return;
        foreach (var e in 玩家.装备)
            if (e.槽位 == 槽位 && 有效最大耐久(e.标识) > 0)
            {
                e.当前耐久 = Math.Max(0, e.当前耐久 - 量);
                return;
            }
    }

    // 扣指定标识装备耐久（任意槽位，用于按当前武器标识扣）
    public void 扣装备标识耐久(string 标识, int 量)
    {
        if (量 <= 0 || string.IsNullOrEmpty(标识)) return;
        foreach (var e in 玩家.装备)
            if (e.标识 == 标识 && 有效最大耐久(e.标识) > 0)
            {
                e.当前耐久 = Math.Max(0, e.当前耐久 - 量);
                return;
            }
    }

    // 已装备某物品的词缀（详情显示用）
    public List<词缀条> 装备词缀(string 标识)
    {
        foreach (var e in 玩家.装备) if (e.标识 == 标识) return e.词缀;
        return null;
    }

    // 装备加成求和（生存/成长管理器 派生数值用）
    public int 装备总加成(Func<string, int> 加成) => 装备数值(加成);
    public int 词缀总数值(词缀属性 属性) => 词缀总值(属性);

    private int 装备数值(Func<string, int> 加成)
    {
        if (加成 == null) return 0;
        int 总 = 0;
        foreach (var e in 玩家.装备)
        {
            if (string.IsNullOrEmpty(e.标识)) continue;
            if (有效最大耐久(e.标识) > 0 && e.当前耐久 <= 0) continue;   // 耐久归零：该装备加成失效
            总 += 加成(e.标识);
        }
        return 总;
    }

    private int 词缀总值(词缀属性 属性)
    {
        if (玩家.词缀定义表 == null) return 0;
        int 总 = 0;
        foreach (var e in 玩家.装备)
        {
            if (e.词缀 == null) continue;
            if (有效最大耐久(e.标识) > 0 && e.当前耐久 <= 0) continue;   // 损坏装备的词缀失效
            foreach (var c in e.词缀)
                if (玩家.词缀定义表.TryGetValue(c.标识, out var def) && def.属性枚举 == 属性) 总 += c.数值;
        }
        return 总;
    }

    // ================= 装备驱动 派生数值 =================

    // 负重上限：50 + 力量×5 + 体质×2 + 装备/词缀
    public int 负重上限 => 50 + 玩家.力量 * 5 + 玩家.体质 * 2 + 装备数值(玩家.负重加成解析) + 词缀总值(词缀属性.负重);

    // 负重占用（当前总重）：身上 3 个穿戴容器（弹挂/腰封/背包）内物品重量（递归，2 层嵌套）。仓库不占负重（收纳）。
    public int 负重占用
    {
        get
        {
            int 总 = 0;
            foreach (var 槽 in new[] { "弹挂", "腰封", "背包" })
            {
                var 记录 = 玩家.装备.Find(e => e.槽位 == 槽);
                if (记录 == null || 记录.容器物品 == null) continue;
                foreach (var 堆叠 in 记录.容器物品)
                    if (堆叠 != null) 总 += 堆叠负重(堆叠, 0);
            }
            return 总;
        }
    }

    // 单堆叠负重：自身 + 容器内部（深度 < 2 才继续，防套娃）
    private int 堆叠负重(物品堆叠 堆叠, int 深度)
    {
        if (堆叠 == null || string.IsNullOrEmpty(堆叠.标识)) return 0;
        int 总 = 玩家.重量解析 != null ? 玩家.重量解析(堆叠.标识) * 堆叠.数量 : 0;
        if (堆叠.容器物品 != null && 深度 < 2)
            foreach (var 内 in 堆叠.容器物品)
                if (内 != null) 总 += 堆叠负重(内, 深度 + 1);
        return 总;
    }

    public bool 超重 => 负重占用 > 负重上限;

    // 超重惩罚（0~0.5：超重越多越慢）
    public float 超重惩罚
    {
        get
        {
            if (!超重) return 0f;
            int 超出 = 负重占用 - 负重上限;
            return Math.Min(0.5f, 超出 / 100f);
        }
    }

    // 近战伤害：力量 + 武器/词缀（骨折削弱）
    public int 近战伤害 => Math.Max(1, 玩家.力量 + 装备数值(玩家.攻击加成解析) + 词缀总值(词缀属性.攻击) - (玩家.骨折 > 0 ? 玩家.骨折 / 10 : 0));

    // 枪械伤害：武器固定伤害（不吃属性）+ 词缀
    public int 枪械伤害 => 装备数值(玩家.攻击加成解析) + 词缀总值(词缀属性.攻击);

    // 总防御：体质×0.5 + 防具/词缀
    public int 总防御 => 玩家.体质 / 2 + 装备数值(玩家.防御加成解析) + 词缀总值(词缀属性.防御);

    // 暴击率%：敏捷×1 + 意志×0.5（封顶 80）
    public float 暴击概率 => Math.Min(0.8f, (玩家.敏捷 * 1f + 玩家.意志 * 0.5f) / 100f + 词缀总值(词缀属性.暴击) / 100f);

    // 闪避率%：敏捷×1 + 意志×0.3（封顶 60）
    public float 闪避概率 => Math.Min(0.6f, (玩家.敏捷 * 1f + 玩家.意志 * 0.3f) / 100f + 词缀总值(词缀属性.闪避) / 100f);

    // 潜行值：敏捷×2 + 意志×1 + 词缀（躲避丧尸/偷袭判定）
    public int 潜行值 => 玩家.敏捷 * 2 + 玩家.意志 + 词缀总值(词缀属性.潜行);

    // 感知：智慧×1 + 意志×1（发现物资/幸存者/先手判定）
    public int 感知 => 玩家.智慧 + 玩家.意志;

    // 恐惧抗性%：意志×1.5（夜晚/尸群/恐怖事件判定）
    public float 恐惧抗性 => Math.Min(0.8f, 玩家.意志 * 1.5f / 100f);

    // 抗性百分数（装备来源）：非线性收益递减 + 硬上限 50%
    public int 抗性百分比
    {
        get
        {
            if (玩家.抗性加成解析 == null) return 0;
            int 总和 = 0;
            foreach (var e in 玩家.装备)
                if (!string.IsNullOrEmpty(e.标识)) 总和 += 玩家.抗性加成解析(e.标识);
            总和 += 词缀总值(词缀属性.抗性);
            if (总和 <= 30) return 总和;
            return Math.Min(50, 30 + (总和 - 30) / 2);
        }
    }

    // 兼容旧引用：速度加成（词缀）
    public int 速度加成 => 词缀总值(词缀属性.速度);
}
