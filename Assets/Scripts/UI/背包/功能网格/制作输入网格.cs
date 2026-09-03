using UnityEngine;

// 制作输入网格：物品网格面板 语义子类——制作面板 的 材料输入区（收 当前 配方 材料）。
// 只收 当前 选中 配方 的 材料：跨面板 拖入 背包 材料 → 会话 网格（制作 时 扣）；
// 取消/关闭 时 输入 剩料 由 制作面板 返还 背包。
public sealed class 制作输入网格 : 物品网格面板
{
    // 当前 允许 的 材料 判定（配方 切换 时 由 制作面板 更新；空 = 拒收 一切）
    public System.Func<string, bool> 允许材料判定;

    // 输入：只收 配方 材料（判定 通过 才 放行）
    protected override bool 目标允许放入(string 标识)
    {
        if (允许材料判定 == null) return false;
        return 允许材料判定(标识);
    }

    // 输入 区 内容 可 拖回 背包（取消/关闭 时 剩料 返还）——默认 允许开始拖拽=true（不 override）
}
