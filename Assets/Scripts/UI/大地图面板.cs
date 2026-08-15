using UnityEngine;

// 大地图面板：大世界节点图的交互表现。单击节点=选中（钢蓝高亮），双击=移动/进入；拖拽平移、滚轮缩放。
// 平移缩放/选中/双击由 地图面板基类 提供，这里只负责：填充大地图节点 + 定义当前/双击动作。
public sealed class 大地图面板 : 地图面板基类
{
    protected override bool 是当前节点(string 标识) => 标识 == ServiceRegistry.Get<地图服务>().当前大节点;
    protected override void 执行进入(string 标识) => ServiceRegistry.Get<地图服务>().移动(标识);

    protected override void 刷新(object 上下文)
    {
        // 打开面板时清空选中，避免上次选中残留
        选中节点 = "";
        渲染框架();
    }

    // 大地图节点：画连线 + 摆节点按钮（当前节点金色高亮）
    protected override void 填充节点()
    {
        var 服务 = ServiceRegistry.Get<地图服务>();
        var 数据 = ServiceRegistry.Get<DataService>();

        foreach (var 地点 in 数据.地图.Values)
        {
            if (地点.连接 == null) continue;
            foreach (var 相邻 in 地点.连接)
            {
                if (!数据.地图.TryGetValue(相邻, out var 邻点)) continue;
                if (string.CompareOrdinal(地点.标识, 相邻) > 0) continue;
                地图渲染.画线(地图内容, 地图渲染.归一化(地图区, 地点.x, 地点.y), 地图渲染.归一化(地图区, 邻点.x, 邻点.y), new Color(0.35f, 0.32f, 0.28f));
            }
        }

        foreach (var 地点 in 数据.地图.Values)
            创建节点按钮(地点.标识, 地点.名称, 地点.x, 地点.y, 地点.标识 == 服务.当前大节点);
    }
}
