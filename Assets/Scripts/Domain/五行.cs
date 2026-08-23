// 五行（金木水火土）：本作魔法属性的相克体系。
// 相克环（克）：金→木→土→水→火→金（金克木、木克土、土克水、水克火、火克金）。
// 相生暂缓（下一阶段再做）。
public enum 五行 { 无, 金, 木, 水, 火, 土 }

public static class 五行工具
{
    // 攻方五行 所克制的目标五行
    public static 五行 克制目标(五行 攻方)
    {
        switch (攻方)
        {
            case 五行.金: return 五行.木;
            case 五行.木: return 五行.土;
            case 五行.土: return 五行.水;
            case 五行.水: return 五行.火;
            case 五行.火: return 五行.金;
            default: return 五行.无;
        }
    }

    // 攻方五行 是否克制 目标五行（魔法弱点判定：技能五行克制敌人五行 = 弱点命中）
    public static bool 克制(五行 攻方, 五行 目标)
    {
        if (攻方 == 五行.无 || 目标 == 五行.无) return false;
        return 克制目标(攻方) == 目标;
    }
}