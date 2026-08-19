using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 探索面板：深度分层区域探索的交互表现（主视窗内容面板）。
// 布局（静态引用模式）：信息条（区域名·危险度·层进度·精力）+ 正文（层描述/事件文本）+ 动作区。
// 动作区：搜索/返回 固定按钮 + 岔路（安全/危险）按钮，按服务发布的动作集显隐；
//   遭遇/资源/偷袭/通路/选择类等动态选项用 创建行 渲染到 选择区。
// 取消（侧边栏）= 返回（撤离回大地图）。
public sealed class 探索面板 : 面板基类
{
    [SerializeField] private TMP_Text 信息条;
    [SerializeField] private TMP_Text 正文;
    [SerializeField] private Button 搜索按钮, 返回按钮;   // 固定动作（深入由通路事件接管）
    [SerializeField] private Button 安全按钮, 危险按钮;   // 岔路
    [SerializeField] private RectTransform 选择区;       // 动态选项（遭遇/资源/偷袭/通路/选择类）

    private 探索服务 服务 => ServiceRegistry.Get<探索服务>();

    void Awake()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<探索显示事件>(渲染);
        搜索按钮?.onClick.AddListener(() => 服务.搜索());
        返回按钮?.onClick.AddListener(() => 服务.返回());
        安全按钮?.onClick.AddListener(() => 服务.选岔路("安全"));
        危险按钮?.onClick.AddListener(() => 服务.选岔路("危险"));
    }

    // 路由进入时拉取当前显示（打开探索/野外面板 事件可能早于 探索显示事件）
    protected override void 刷新(object 上下文)
    {
        var 服务 = this.服务;
        if (服务.当前显示.选项 != null) 渲染(服务.当前显示);
        else if (上下文 is 探索显示事件 e) 渲染(e);
    }

    private void 渲染(探索显示事件 e)
    {
        设文本(信息条, e.信息条);
        设文本(正文, e.正文);
        // 固定动作按动作集显隐；其余（战斗/逃跑/偷袭/绕开/资源/通路/选择:i）进选择区动态行
        bool 有搜索 = false, 有返回 = false, 岔路 = false;
        清空(选择区);
        if (e.选项 != null)
            foreach (var 选项 in e.选项)
            {
                switch (选项.动作)
                {
                    case "搜索": 有搜索 = true; break;
                    case "返回": 有返回 = true; break;
                    case "岔路:安全":
                    case "岔路:危险": 岔路 = true; break;
                    default:   // 战斗 / 逃跑 / 偷袭 / 绕开 / 资源 / 强行突破 / 悄悄绕行 / 选择:i
                        var 动作 = 选项.动作;
                        创建行(选择区, 选项.文本, () => 回调(动作), true, true);
                        break;
                }
            }
        if (搜索按钮 != null) 搜索按钮.gameObject.SetActive(有搜索);
        if (返回按钮 != null) 返回按钮.gameObject.SetActive(有返回);
        if (安全按钮 != null) 安全按钮.gameObject.SetActive(岔路);
        if (危险按钮 != null) 危险按钮.gameObject.SetActive(岔路);
    }

    private void 回调(string 动作)
    {
        switch (动作)
        {
            case "战斗": 服务.战斗遭遇(); break;
            case "逃跑": 服务.逃跑遭遇(); break;
            case "偷袭": 服务.战斗遭遇(); break;          // 我方先手
            case "绕开": 服务.偷袭绕开(); break;
            case "资源:是": 服务.资源是(); break;
            case "资源:否": 服务.资源否(); break;
            case "强行突破": 服务.强行突破(); break;
            case "悄悄绕行": 服务.悄悄绕行(); break;
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
