using UnityEngine;

// 仓库区（右区，塔科夫 stash）：收纳型大网格（档案.仓库物品，10×20）。
//   场景搭建：ScrollRect（Viewport → Content）+ 挂 网格面板（Inspector 配 网格容器）；
//   本组件 注入 数据源 = 档案.仓库视图()（跨网格转移/拖拽 与穿戴容器互通）。
//   仓库不占负重（收纳）；背包变化 → 网格自动刷新（网格面板 已订阅事件）。
public sealed class 仓库区 : MonoBehaviour
{
    [SerializeField] private 网格面板 面板;   // 仓库网格（场景预搭：网格面板）

    void Awake()
    {
        // 不再订阅 背包/属性变化：网格面板 自身已订阅（事件 → 脏标记 → Update 合并刷新），这里再订阅会造成双重全量重建
        刷新();
    }

    private void OnEnable() => 刷新();   // 面板每次打开时刷新（数据源/格尺寸）

    // 注入 仓库视图 数据源（每次重建视图，背包列表绑定 档案.仓库物品 同一引用）；网格常驻（不管有无物品）
    public void 刷新()
    {
        var 档案 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (面板 == null)
        {
            ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.反馈坏, "[仓库区] 未配置 面板 引用（请拖入 网格面板）。"));
            return;
        }
        if (档案 == null) return;
        面板.gameObject.SetActive(true);   // 仓库网格一直显示（空仓库也显示网格框架）
        面板.数据源 = 档案.仓库视图();
        面板.配置视图显示();   // 格尺寸统一常量（网格面板.格尺寸=100）；尺寸用 服务.网格列/行（10×20）
        面板.请求刷新();   // 脏标记：Update 帧末按 尺寸/增量 判定（避免直接全量重载 + 面板自身刷新 = 双重重建）
    }
}
