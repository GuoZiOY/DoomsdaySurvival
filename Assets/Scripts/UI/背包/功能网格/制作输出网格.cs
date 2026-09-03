using UnityEngine;

// 制作输出网格：物品网格面板 语义子类——制作面板 的 产物输出区（禁 拖入）。
// 产物 由 制作 结算 直接 入 输出 会话；玩家 不可 把 东西 塞 回 输出 区，
// 但 可 拖走 成品（拿回 背包/仓库）。
public sealed class 制作输出网格 : 物品网格面板
{
    // 输出：禁 拖入（防止 玩家 把 东西 塞 回 输出 区）
    protected override bool 目标允许放入(string 标识) => false;

    // 输出 区 内容 可 拖走（成品 拿走）——默认 允许开始拖拽=true（不 override）
}
