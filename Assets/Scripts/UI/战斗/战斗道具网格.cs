using UnityEngine;
using UnityEngine.EventSystems;

// 战斗道具网格：战斗沙盒 的 弹挂/腰封 容器网格（物品网格面板 子类——复用 物品 渲染/增量刷新/背包变化自动刷新）。
// 战斗内 只读 + 单击 使用：禁 拖拽 / 禁 右键菜单 / 禁 跨面板 / 禁 放入；单击 物品 → 转发 外壳 目标选择。
// 数据源 由 外壳 布阵 时 注入（容器服务.打开(弹挂/腰封 容器实例) → 网格服务）。
public sealed class 战斗道具网格 : 物品网格面板
{
    private 战斗沙盒面板 外壳 => GetComponentInParent<战斗沙盒面板>();

    protected override void 单击实体(物品堆叠 堆叠, int 点击次数)
    {
        // 单击 即 使用：进入 目标选择（恢复/增益 默认 点自己；投掷物 点 敌人）
        if (堆叠 != null && !string.IsNullOrEmpty(堆叠.标识) && 外壳 != null) 外壳.点击道具(堆叠.标识);
    }

    protected override void 右键实体(物品堆叠 堆叠, RectTransform 框) { }   // 战斗内 禁 右键菜单

    protected override bool 完成拖拽(PointerEventData 事件, 物品堆叠 堆叠) => false;
    protected override bool 允许跨面板() => false;
    protected override bool 允许开始拖拽(物品堆叠 堆叠) => false;
    protected override bool 目标允许放入(string 标识) => false;
}
