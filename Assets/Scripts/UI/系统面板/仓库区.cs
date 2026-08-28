using UnityEngine;

// 仓库区（右区，塔科夫 stash）：收纳型大网格（档案.仓库物品，10×20）。
//   场景搭建：ScrollRect（Viewport → Content）+ 挂 网格背包面板（Inspector 配 网格容器）；
//   本组件 注入 数据源 = 档案.仓库视图()（跨网格转移/拖拽 与穿戴容器互通）。
//   仓库不占负重（收纳）；背包变化 → 网格自动刷新（网格背包面板 已订阅事件）。
public sealed class 仓库区 : MonoBehaviour
{
    [SerializeField] private 网格背包面板 面板;   // 仓库网格（场景预搭：网格背包面板）

    void Awake()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        if (事件 != null)
        {
            事件.订阅<背包变化事件>(背包变化响应);
            事件.订阅<属性变化事件>(属性变化响应);   // 装备变化不影响仓库，但刷新兜底
        }
        刷新();
    }

    private void OnEnable() => 刷新();   // 面板每次打开时刷新（数据源/格尺寸）

    private void OnDestroy()
    {
        if (ServiceRegistry.已注册<EventBus>())
        {
            var 事件 = ServiceRegistry.Get<EventBus>();
            if (事件 != null)
            {
                事件.取消订阅<背包变化事件>(背包变化响应);
                事件.取消订阅<属性变化事件>(属性变化响应);
            }
        }
    }

    private bool 待刷新;   // 脏标记：事件 → 标记，Update 合并刷新（仓库网格大，避免同帧多次全量重载）

    void Update()
    {
        if (!待刷新) return;
        待刷新 = false;
        刷新();
    }

    private void 背包变化响应(背包变化事件 _) => 待刷新 = true;
    private void 属性变化响应(属性变化事件 _) => 待刷新 = true;

    // 注入 仓库视图 数据源（每次重建视图，背包列表绑定 档案.仓库物品 同一引用）；网格常驻（不管有无物品）
    public void 刷新()
    {
        var 档案 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (面板 == null)
        {
            ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.反馈坏, "[仓库区] 未配置 面板 引用（请拖入 网格背包面板）。"));
            return;
        }
        if (档案 == null) return;
        面板.gameObject.SetActive(true);   // 仓库网格一直显示（空仓库也显示网格框架）
        float 格 = 网格背包面板.主背包面板 != null ? 网格背包面板.主背包面板.格子尺寸 : 133f;
        面板.数据源 = 档案.仓库视图();
        面板.配置容器显示(格);
        面板.重载网格();
    }
}
