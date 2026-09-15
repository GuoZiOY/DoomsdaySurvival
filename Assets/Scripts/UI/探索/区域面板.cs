using System;
using UnityEngine;

// ============================================================
// 区域面板：区域层的宿主面板（对位 房间面板）——只做两件事：
//   ① 管网格（引用位 `网格面板`；场景没接线时自动建一个，零接线可测）；
//   ② 面板自己的出口按钮 = 走出这一片（回上一个面板 = 大地图/荒野节点那一层）。
//
// 信息展示**不在本面板**（与房间层同一条口径）：
//   · 区域名 · 危险 → 发布 地图位置事件，由常驻 HUD 的「地点」位显示；
//   · 区域里的提示（"你推开了便利店门厅的门"）→ 发布 日志事件（探索类），左下角日志播报。
//
// 与房间层的衔接（重点，第 1 刀的"两层"就靠它）：
//   走到某栋楼的入口格上 → 区域探索服务.抵达格 直接调 房间探索服务.进入房间 →
//   服务发 打开房间事件 → 面板管理器 切到 房间面板（**区域面板自动成为"上一个面板"**）；
//   在楼里按 Esc / 走大门 → 房间面板.回退 → 返回上一面板 = 区域面板。**这一路不需要任何新代码。**
// ============================================================
public sealed class 区域面板 : 面板基类
{
    [SerializeField] private 区域网格面板 网格面板;   // 拖：区域网格面板（同物体挂 区域图层）
    [SerializeField] private Vector2 视口 = new Vector2(1720f, 960f);   // **相机窗口大小**（像素）；填 0 = 自动铺满面板

    private bool 已订阅;
    private Action<区域显示事件> 显示回调;
    private Action<离开区域事件> 出街口回调;

    private 区域探索服务 服务 => ServiceRegistry.Get<区域探索服务>();

    public override string 取消文本 => "回大地图";

    // 面板自己的出口按钮：走出这一片并回上一个面板（大地图）
    public override bool 回退(){
        // 用户要求：离开这一层不能瞬移 —— 得自己走出去（路上照常耗时间、会遇敌）。
        // 起步了就返回；已经站在出口上 / 走不到 -> 落到下面的原逻辑（那正是玩家走到门口那一刻的分支）。
        if (ServiceRegistry.Get<区域探索服务>()?.自动走向出口() == true) return true;

        服务?.离开();
        // 用户 2026-09-15：离开这一层要回到**外面那一层**（房间->区域，区域->大世界），
        // 不能靠 返回上一面板() —— 那是只有 1 层深的槽，中途开关过背包就会被覆盖成背包
        // （实测症状：从房间走到门口，回来的却是背包）。
        if (面板管理器.实例 != null) 面板管理器.实例.显示面板类型<大世界面板>();
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
        // 走到街口出去（服务发 离开区域事件）→ 和 Esc 同一条路：清区域态 + 回上一个面板
        // （与房间层的"走大门出楼 → 离开房间事件 → 房间面板.回退"一一对位）
        出街口回调 = _ => 回退();
        事件.订阅(出街口回调);
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
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.取消订阅(显示回调);
        if (出街口回调 != null) 事件.取消订阅(出街口回调);
        已订阅 = false;
    }

    // ===== 零接线兜底 + 图层自检 =====
    // ① 引用位没接 → 面板下自动建"网格容器 + 区域网格面板 + 区域图层"；
    // ② 接了但**漏挂 / 挂错图层** → 这是"点了完全没反应、Console 又没线索"的经典原因，这里必须喊出来：
    //    漏挂 → 自动补上；挂成别的图层（如 房间图层）→ 报错（两个图层会抢同一份网格，不能自动修）。
    private void 确保网格()
    {
        if (网格面板 == null)
        {
            var 物体 = new GameObject("网格容器", typeof(RectTransform));
            var 容器 = (RectTransform)物体.transform;
            容器.SetParent(transform, false);
            UI工具.设锚(容器, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(Mathf.Max(320f, 视口.x), Mathf.Max(240f, 视口.y)));   // 相机窗口大小 = 视口字段
            网格面板 = 物体.AddComponent<区域网格面板>();
            网格面板.绑定网格容器(容器);
            物体.AddComponent<区域图层>();
            Debug.LogWarning("[区域] 场景未接线 区域面板.网格面板 —— 已在面板下自动建一个（要美观请手动搭建并拖引用位）。");
            return;
        }
        if (网格面板.GetComponent<区域图层>() != null) return;
        var 错图层 = 网格面板.GetComponent<探索图层>();
        if (错图层 != null)
            Debug.LogError($"[区域] {网格面板.name} 上挂的是 {错图层.GetType().Name}，不是 区域图层 —— 请把 {错图层.GetType().Name} 删掉改挂 区域图层；"
                         + "（挂错的表现：地表/路径/落点都没有，点击也完全没反应）");
        else
        {
            网格面板.gameObject.AddComponent<区域图层>();
            Debug.LogWarning($"[区域] {网格面板.name} 上漏挂 区域图层 —— 已自动补上（没它：没有地表/路径/落点，点击也没人处理）。");
        }
    }
}
