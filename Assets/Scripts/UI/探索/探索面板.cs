using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 探索面板：深度分层区域探索的交互表现（主视窗内容面板）。
// 布局（固定结构）：信息条（区域名·危险度·层进度·精力）+ 正文（层描述/事件文本）+ 固定三按钮（探索/是/否）。
// 按钮功能固定：探索=搜索（仅可搜索情景显示）；是=肯定类（战斗/偷袭/强行突破/安全小路/资源是/选项1）；
//   否=否定类（逃跑/绕开/悄悄绕行/危险捷径/资源否/选项2）。按钮文本随情景由 探索服务 填充。
// 返回（撤离回大地图）统一走侧边栏取消按钮。
public sealed class 探索面板 : 面板基类
{
    [SerializeField] private TMP_Text 信息条;
    [SerializeField] private TMP_Text 正文;
    [SerializeField] private Button 探索按钮, 是按钮, 否按钮;   // 固定三按钮（按情景改文本/显隐）

    private 探索服务 服务 => ServiceRegistry.Get<探索服务>();

    void Awake()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<探索显示事件>(渲染);
        // 按钮监听恒定：点击时取 当前显示 的对应按钮动作（渲染只改文本/显隐）
        探索按钮?.onClick.AddListener(() => 执行(服务.当前显示.探索.动作));
        是按钮?.onClick.AddListener(() => 执行(服务.当前显示.是.动作));
        否按钮?.onClick.AddListener(() => 执行(服务.当前显示.否.动作));
    }

    // 生命周期：面板销毁时解绑事件订阅，避免悬挂引用/重复消费
    void OnDestroy()
    {
        if (ServiceRegistry.已注册<EventBus>())
            ServiceRegistry.Get<EventBus>().取消订阅<探索显示事件>(渲染);
    }

    // 进入面板时渲染：优先本次路由携带的探索显示事件（精确快照）；否则回退到服务缓存的最新显示
    protected override void 刷新(object 上下文)
    {
        if (上下文 is 探索显示事件 e) 渲染(e);
        else if (this.服务.当前显示.正文 != null) 渲染(this.服务.当前显示);
    }

    private void 渲染(探索显示事件 e)
    {
        设文本(信息条, e.信息条);
        设文本(正文, e.正文);
        绑定按钮(探索按钮, e.探索);
        绑定按钮(是按钮, e.是);
        绑定按钮(否按钮, e.否);
    }

    // 三按钮统一渲染：文本 + 显隐（空数据=隐藏）
    private void 绑定按钮(Button 按钮, 探索按钮数据 数据)
    {
        if (按钮 == null) return;
        按钮.gameObject.SetActive(数据.有效);
        if (!数据.有效) return;
        var 文本 = 按钮.GetComponentInChildren<TMP_Text>();
        if (文本 != null) 文本.text = 数据.文本;
    }

    // 动作路由到探索服务
    private void 执行(string 动作)
    {
        if (string.IsNullOrEmpty(动作)) return;
        switch (动作)
        {
            case "搜索": 服务.搜索(); break;
            case "战斗":
            case "偷袭": 服务.战斗遭遇(); break;
            case "逃跑": 服务.逃跑遭遇(); break;
            case "绕开": 服务.偷袭绕开(); break;
            case "资源:是": 服务.资源是(); break;
            case "资源:否": 服务.资源否(); break;
            case "强行突破": 服务.强行突破(); break;
            case "悄悄绕行": 服务.悄悄绕行(); break;
            case "岔路:安全": 服务.选岔路("安全"); break;
            case "岔路:危险": 服务.选岔路("危险"); break;
            default:
                if (动作.StartsWith("选择:"))
                {
                    if (int.TryParse(动作.Substring(3), out var 索引)) 服务.选择(索引);
                }
                break;
        }
    }

    // 全局取消 = 撤离回大地图（仅探索中）
    public override bool 回退()
    {
        if (服务.当前区域标识 == null) return false;
        服务.返回();
        return true;
    }

    public override string 取消文本 => "返回";
}
