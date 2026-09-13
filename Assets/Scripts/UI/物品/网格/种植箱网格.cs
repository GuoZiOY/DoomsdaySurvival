// 种植箱网格：种植箱 家具 的 种植 网格 语义（继承 物品网格面板）——
// 单 种子 限制（容器 已 有 物品 → 拒绝 再 放入：一次 只 种 一 棵）＋ 生长中 种子 不可 拖拽
// （定根 种植；成熟 作物 可 拖拽 收获 移动；右键 丢弃 始终 可用）。
public sealed class 种植箱网格 : 物品网格面板
{
    // 目标 允许 放入：基类 校验（种类/容器）之上 加 单 种子 限制（容器 非空 → 拒绝）
    protected override bool 目标允许放入(string 标识)
    {
        if (!base.目标允许放入(标识)) return false;
        var 容器 = 所属容器;
        return 容器?.容器物品 == null || 容器.容器物品.Count == 0;   // 已 有 种子/作物 → 先 收获 或 销毁
    }

    // 拖拽：生长中 的 种子 禁止 拖拽（种下 后 定根，不能 拖走/换位/拖回 背包）；
    // 成熟 作物（生长 完成 的 产物）可 拖拽 收获 移动。
    protected override bool 允许开始拖拽(物品堆叠 堆叠)
    {
        if (堆叠 == null || string.IsNullOrEmpty(堆叠.标识)) return false;
        if (!数据.物品.TryGetValue(堆叠.标识, out var 模板)) return false;
        if (模板.生长时间 > 0 && !string.IsNullOrEmpty(模板.成熟产物) && 堆叠.生长分钟 > 0) return false;   // 种子 且 生长中
        return true;   // 成熟 作物 / 其他 物品
    }
}
