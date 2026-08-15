using System;

    // 战斗数值规则：纯函数，无状态。伤害公式 max(1, 攻 - 防/2 + 随机掷)。
    public static class 战斗规则
    {
        // 普通攻击伤害
        public static int 普通伤害(int 攻方攻击, int 守方防御, int 随机掷)
            => Math.Max(1, 攻方攻击 - 守方防御 / 2 + 随机掷);

        // 技能（重击类）伤害：总攻击 * 倍率
        public static int 技能伤害(int 总攻击, int 倍率, int 守方防御, int 随机掷)
            => Math.Max(1, 总攻击 * 倍率 - 守方防御 / 2 + 随机掷);

        // 防御状态减半（至少 1）
        public static int 防御后伤害(int 原伤害) => Math.Max(1, 原伤害 / 2);
    }
