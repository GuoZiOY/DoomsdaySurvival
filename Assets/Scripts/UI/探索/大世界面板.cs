using System;
using UnityEngine;

// ============================================================
// 大世界面板：大世界层（100×100 格子网格）的宿主面板（对位 区域面板 / 房间面板）——只做两件事：
//   ① 管网格（引用位 `网格面板`；场景没接线时自动建一个，**零接线可测**）；
//   ② Esc / 侧边栏取消 = 回安全屋。
//
// 信息展示**不在本面板**（与另外两层同一条口径）：
//   · 世界名 → 发布 地图位置事件，由常驻 HUD 的「地点」位显示；
//   · 世界里的提示（"你从西区的街口拐了进去"）→ 发布 日志事件（探索类），左下角日志播报。
//
// 与下面三层的衔接（不需要任何新代码）：
//   走到**区域门口格** → 大世界探索服务.抵达格 → 区域探索服务.进入区域 →
//     服务发 打开区域事件 → 面板管理器 切到 区域面板（**大世界面板自动成为"上一个面板"**）；
//   在区域里 Esc / 出区域 → 区域探索服务.离开 → 让 大世界探索服务 重发显示 → 回本面板。
//   走到**营地门口格** → 发 打开营地事件 → 切到 安全屋面板。
//
// ⚠ 大世界是 100×100：网格面板基类的底格/格线在这里关掉了，地表/迷雾/交互走 大世界图层 的**格子池**。
//   所以本面板**不要**去覆写 视口 之外的网格参数；要看更多就调大 `视口`（相机窗口）。
// ============================================================
public sealed class 大世界面板 : 面板基类
{
    [SerializeField] private 大世界网格面板 网格面板;   // 拖：大世界网格面板（同物体挂 大世界图层）
    [SerializeField] private Vector2 视口 = new Vector2(1720f, 960f);   // **相机窗口大小**（像素）；填 0 = 自动铺满面板

    private bool 已订阅;
    private Action<大世界显示事件> 显示回调;

    private 大世界探索服务 服务 => ServiceRegistry.Get<大世界探索服务>();

    public override string 取消文本 => "回安全屋";

    // Esc / 侧边栏取消：**大世界是最外层** —— 回安全屋。
    // 注意走的是**事件**（与"走到营地门口那一格"完全同一条路）：面板管理器 切到安全屋面板，
    // 各格子层同时据此清"这一趟出行"的临时数据（容器战局 / 门锁态 / 视野记忆）。
    // 这里**不**调 返回上一面板 —— 大世界是从主菜单/角色创建进来的，上一个面板可能是角色创建。
    public override bool 回退()
    {
        ServiceRegistry.Get<EventBus>()?.发布(new 打开营地事件());
        return true;
    }

    protected override void 刷新(object 上下文)
    {
        确保订阅();
        确保网格();
        var 服 = 服务;
        if (服 == null || !服.探索中) return;
        if (网格面板 == null) return;
        网格面板.数据源 = 服.网格;
        网格面板.视口 = 视口;
        网格面板.请求刷新();
        网格面板.图层()?.刷新();
    }

    // ===== 订阅（面板销毁必须退订） =====

    private void 确保订阅()
    {
        if (已订阅) return;
        var 事件 = ServiceRegistry.Get<EventBus>();
        if (事件 == null) return;
        显示回调 = e => 刷新网格();
        事件.订阅(显示回调);
        已订阅 = true;
    }

    private void 刷新网格()
    {
        if (!gameObject.activeInHierarchy) return;
        if (网格面板 == null) return;
        var 服 = 服务;
        if (服 != null && 服.探索中) 网格面板.数据源 = 服.网格;
        网格面板.视口 = 视口;
        网格面板.请求刷新();
        网格面板.图层()?.刷新();
    }

    void OnDestroy()
    {
        if (!已订阅) return;
        if (!ServiceRegistry.已注册<EventBus>()) return;
        ServiceRegistry.Get<EventBus>().取消订阅(显示回调);
        已订阅 = false;
    }

    // ===== 零接线兜底 + 图层自检（与 区域面板 同一条：漏挂/挂错图层是"点了没反应"的经典原因） =====
    private void 确保网格()
    {
        if (网格面板 == null)
        {
            var 物体 = new GameObject("网格容器", typeof(RectTransform));
            var 容器 = (RectTransform)物体.transform;
            容器.SetParent(transform, false);
            UI工具.设锚(容器, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(Mathf.Max(320f, 视口.x), Mathf.Max(240f, 视口.y)));   // 相机窗口大小 = 视口字段
            网格面板 = 物体.AddComponent<大世界网格面板>();
            网格面板.绑定网格容器(容器);
            物体.AddComponent<大世界图层>();
            Debug.LogWarning("[大世界] 场景未接线 大世界面板.网格面板 —— 已在面板下自动建一个（要美观请手动搭建并拖引用位）。");
            return;
        }
        if (网格面板.GetComponent<大世界图层>() != null) return;
        var 错图层 = 网格面板.GetComponent<探索图层>();
        if (错图层 != null)
            Debug.LogError($"[大世界] {网格面板.name} 上挂的是 {错图层.GetType().Name}，不是 大世界图层 —— 请把 {错图层.GetType().Name} 删掉改挂 大世界图层；"
                         + "（挂错的表现：地表/路径/落点都没有，点击也完全没反应；而且**没有格子池**，100×100 会卡死）");
        else
        {
            网格面板.gameObject.AddComponent<大世界图层>();
            Debug.LogWarning($"[大世界] {网格面板.name} 上漏挂 大世界图层 —— 已自动补上（没它：没有地表/路径/落点，点击也没人处理，且 100×100 会逐格建图卡死）。");
        }
    }
}
