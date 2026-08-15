using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 小地图面板：城镇内部设施节点网络的交互表现。单击节点=选中，双击=移动/进入/离开城镇；拖拽平移、滚轮缩放。
// 平移缩放/选中/双击由 地图面板基类 提供；这里负责：城镇数据、剧情/设施节点、离开按钮、位置监听。
public sealed class 小地图面板 : 地图面板基类
{
    [SerializeField] private TMP_Text 标题;
    [SerializeField] private Button 离开按钮;
    private string 当前城镇;
    private readonly Dictionary<string, 地图节点> 节点数据 = new Dictionary<string, 地图节点>();

    protected override bool 是当前节点(string 标识) => 标识 == ServiceRegistry.Get<地图服务>().当前小节点;

    // 双击动作：入口节点离开城镇；设施/剧情/空地 小地图移动
    protected override void 执行进入(string 标识)
    {
        if (!节点数据.TryGetValue(标识, out var 节点)) return;
        var 服务 = ServiceRegistry.Get<地图服务>();
        if (节点.类型 == "入口") 服务.离开城镇();
        else 服务.小地图移动(标识);
    }

    protected override void Awake()
    {
        base.Awake();
        离开按钮?.onClick.AddListener(() => ServiceRegistry.Get<地图服务>().离开城镇());
        // 监听地图位置：镇内移动（空地节点）不触发导航事件，靠它重绘当前节点高亮
        ServiceRegistry.Get<EventBus>().订阅<地图位置事件>(e =>
        {
            if (e.所在模式 == 地图模式.城镇)
            {
                当前城镇 = e.大节点;
                渲染小地图();
            }
        });
    }

    protected override void 刷新(object 上下文)
    {
        if (上下文 is 打开小地图事件 e) 当前城镇 = e.城镇标识;
        // 打开面板时清空选中，避免上次选中残留
        选中节点 = "";
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
                地图渲染.画线(地图内容, 地图渲染.归一化(地图区, 节点.x, 节点.y), 地图渲染.归一化(地图区, 邻点.x, 邻点.y), new Color(0.35f, 0.32f, 0.28f));
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
