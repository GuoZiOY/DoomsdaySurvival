using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 小地图面板：城镇内部设施节点网络的交互表现。单击=沿互通路径移动，双击=进入（入口→离开城镇/有内部→节点内部）；拖拽平移、滚轮缩放。
// 平移缩放/单击移动/双击进入由 地图面板基类 提供；这里负责：城镇数据、剧情/设施节点、位置监听。
// 离开城镇统一走 侧边栏取消按钮（回退 = 离开城镇），面板内不再放置 离开按钮。
public sealed class 小地图面板 : 地图面板基类
{
    [SerializeField] private TMP_Text 标题;
    private string 当前城镇;
    private readonly Dictionary<string, 地图节点> 节点数据 = new Dictionary<string, 地图节点>();

    protected override bool 是当前节点(string 标识) => 标识 == ServiceRegistry.Get<地图服务>().当前小节点;

    // 单击：镇内沿互通路径移动（地图服务 校验相邻）
    protected override void 执行移动(string 标识) => ServiceRegistry.Get<地图服务>().小地图移动(标识);

    // 双击：进入节点（需已站在该节点上；入口→离开城镇 / 有内部→节点内部）
    protected override void 执行进入(string 标识)
    {
        var 服务 = ServiceRegistry.Get<地图服务>();
        if (标识 != 服务.当前小节点)
        {
            // 未到达该节点就试图进入 → 失败反馈（否则静默且按钮成功音效会响）
            音效管理器.实例?.播放失败();
            ServiceRegistry.Get<EventBus>().发布(new 日志事件(日志类型.系统, "那里无法直接到达。"));
            return;
        }
        服务.进入当前小节点();
    }

    protected override void Awake()
    {
        base.Awake();
        // 监听地图位置：镇内移动（普通节点）不触发导航事件，靠它重绘当前节点高亮
        ServiceRegistry.Get<EventBus>().订阅<地图位置事件>(e =>
        {
            if (e.所在模式 == 地图模式.城镇)
            {
                当前城镇 = e.大节点;
                渲染小地图();
            }
        });
    }

    // 全局取消 = 离开城镇回大地图（仅城镇模式）
    public override bool 回退()
    {
        if (ServiceRegistry.Get<地图服务>().所在模式 != 地图模式.城镇) return false;
        ServiceRegistry.Get<地图服务>().离开城镇();
        return true;
    }

    public override string 取消文本 => "离开";

    protected override void 刷新(object 上下文)
    {
        if (上下文 is 打开小地图事件 e) 当前城镇 = e.城镇标识;
        渲染小地图();
    }

    // 先确认城镇数据并设标题，再走公共渲染框架
    private void 渲染小地图()
    {
        var 数据 = ServiceRegistry.Get<DataService>();
        if (!数据.地图.TryGetValue(当前城镇, out var 地点) || 地点.小地图 == null)
        {
            设文本(标题, $"—— {当前城镇} ——");
            return;
        }
        设文本(标题, $"—— {地点.名称} ——");
        渲染框架();
    }

    // 城镇小地图节点：画连线 + 摆节点按钮（当前节点金色高亮）
    protected override void 填充节点()
    {
        var 服务 = ServiceRegistry.Get<地图服务>();
        var 数据 = ServiceRegistry.Get<DataService>();
        if (!数据.地图.TryGetValue(当前城镇, out var 地点) || 地点.小地图 == null) return;
        节点数据.Clear();

        // 画连线（去重：只在标识较大的方向画一次）
        foreach (var 节点 in 地点.小地图)
        {
            if (节点.连接 == null) continue;
            foreach (var 相邻 in 节点.连接)
            {
                var 邻点 = 找节点(地点, 相邻);
                if (邻点 == null || string.CompareOrdinal(节点.标识, 相邻) > 0) continue;
                地图渲染.画线(地图内容, 地图渲染.归一化(内容区, 节点.x, 节点.y), 地图渲染.归一化(内容区, 邻点.x, 邻点.y), new Color(0.35f, 0.32f, 0.28f));
            }
        }

        // 摆节点按钮
        foreach (var 节点 in 地点.小地图)
        {
            节点数据[节点.标识] = 节点;
            创建节点按钮(节点.标识, 节点.名称, 节点.x, 节点.y, 节点.标识 == 服务.当前小节点);
        }
    }

    // 按标识找小地图节点（画线需要邻点坐标）
    private 地图节点 找节点(地图地点 地点, string 标识)
    {
        foreach (var 节点 in 地点.小地图) if (节点.标识 == 标识) return 节点;
        return null;
    }
}
