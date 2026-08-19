using UnityEngine;

// 大地图面板：大世界节点图的交互表现。单击=沿互通路径移动，双击=进入（城镇→小地图/荒野→野外面板）；拖拽平移、滚轮缩放。
// 平移缩放/单击移动/双击进入由 地图面板基类 提供，这里只负责：填充大地图节点 + 定义当前/移动/进入动作。
public sealed class 大地图面板 : 地图面板基类
{
    protected override bool 是当前节点(string 标识) => 标识 == ServiceRegistry.Get<地图服务>().当前大节点;

    // 单击：沿互通路径移动（地图服务 校验相邻）
    protected override void 执行移动(string 标识) => ServiceRegistry.Get<地图服务>().移动(标识);

    // 双击：进入节点（需已站在该节点上；城镇→小地图 / 荒野→野外面板）
    protected override void 执行进入(string 标识)
    {
        var 服务 = ServiceRegistry.Get<地图服务>();
        if (标识 != 服务.当前大节点)
        {
            // 未到达该节点就试图进入 → 失败反馈（否则静默且按钮成功音效会响）
            音效管理器.实例?.播放失败();
            ServiceRegistry.Get<EventBus>().发布(new 日志事件(日志类型.系统, "那里无法直接到达。"));
            return;
        }
        服务.进入当前();
    }

    protected override void Awake()
    {
        base.Awake();
        // 大地图内移动后重绘当前节点高亮（移动不切面板，靠位置事件刷新）
        ServiceRegistry.Get<EventBus>().订阅<地图位置事件>(e =>
        {
            if (e.所在模式 == 地图模式.大地图 && gameObject.activeInHierarchy) 渲染框架();
        });
    }

    protected override void 刷新(object 上下文)
    {
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
                地图渲染.画线(地图内容, 地图渲染.归一化(内容区, 地点.x, 地点.y), 地图渲染.归一化(内容区, 邻点.x, 邻点.y), new Color(0.35f, 0.32f, 0.28f));
            }
        }

        foreach (var 地点 in 数据.地图.Values)
            创建节点按钮(地点.标识, 地点.名称, 地点.x, 地点.y, 地点.标识 == 服务.当前大节点);
    }
}
