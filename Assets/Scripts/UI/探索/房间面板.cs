using System;
using UnityEngine;

// ============================================================
// 房间面板：房间层的宿主面板（对位 安全屋面板）——只做两件事：
//   ① 管网格（引用位 `网格面板`；场景没接线时自动建一个，零接线可测）；
//   ② 面板自己的出口按钮 = 离开房间。
//
// 信息展示**不在本面板**（定版口径）：
//   · 房间名 · 危险 · 已搜 · 敌 → 发布 地图位置事件，由常驻 **HUD 的「地点」位**显示；
//   · 房间里的提示（"你拉开了货架" 等）→ 发布 **日志事件（探索类）**，由左下角日志播报。
//
// 引用位（场景手动搭建）：`网格面板`（拖 房间网格面板；同一物体挂 房间图层）。
// 容器不做容器卡：一切交互走 右键菜单（搜索 / 查看），状态直接画在格子上（已搜 = 灰化 + 打勾）。
// ============================================================
public sealed class 房间面板 : 面板基类
{
    [SerializeField] private 房间网格面板 网格面板;   // 拖：房间网格面板（同物体挂 房间图层）
    [SerializeField] private Vector2 视口 = new Vector2(1720f, 960f);   // **相机窗口大小**（像素）；填 0 = 自动铺满面板

    private bool 已订阅;
    private Action<房间显示事件> 显示回调;
    private Action<离开房间事件> 出楼回调;

    private 房间探索服务 服务 => ServiceRegistry.Get<房间探索服务>();

    public override string 取消文本 => "离开";

    // 面板自己的出口按钮：离开房间（清本局状态）并回上一个面板
    public override bool 回退(){
        // 用户要求：离开这一层不能瞬移 —— 得自己走出去（路上照常耗时间、会遇敌）。
        // 起步了就返回；已经站在出口上 / 走不到 -> 落到下面的原逻辑（那正是玩家走到门口那一刻的分支）。
        if (ServiceRegistry.Get<房间探索服务>()?.自动走向出口() == true) return true;

        服务?.离开();
        // 用户 2026-09-15：离开这一层要回到**外面那一层**（房间->区域，区域->大世界），
        // 不能靠 返回上一面板() —— 那是只有 1 层深的槽，中途开关过背包就会被覆盖成背包
        // （实测症状：从房间走到门口，回来的却是背包）。
        if (面板管理器.实例 != null){
            if (ServiceRegistry.Get<区域探索服务>()?.探索中 == true) 面板管理器.实例.显示面板类型<区域面板>();
            else 面板管理器.实例.显示面板类型<大世界面板>();
        }
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
        // 走大门出去（服务发 离开房间事件）→ 和 Esc 同一条路：清房间态 + 回上一个面板
        出楼回调 = _ => 回退();
        事件.订阅(出楼回调);
        已订阅 = true;
    }

    private void 刷新网格()
    {
        if (!gameObject.activeInHierarchy) return;
        if (网格面板 == null) return;
        var 服 = 服务;
        // 走门换房时数据源也换了：这条"显示刷新"路径也要重绑一次（不只是 刷新(上下文) 那条路）
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
        if (出楼回调 != null) 事件.取消订阅(出楼回调);
        已订阅 = false;
    }

    // ===== 零接线兜底 + 图层自检 =====
    // 引用位没接 → 面板下自动建；接了但**漏挂 / 挂错图层** → 喊出来（挂错的表现就是"点了没反应"）
    private void 确保网格()
    {
        if (网格面板 == null)
        {
            var 物体 = new GameObject("网格容器", typeof(RectTransform));
            var 容器 = (RectTransform)物体.transform;
            容器.SetParent(transform, false);
            UI工具.设锚(容器, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(Mathf.Max(320f, 视口.x), Mathf.Max(240f, 视口.y)));   // 相机窗口大小 = 视口字段
            网格面板 = 物体.AddComponent<房间网格面板>();
            网格面板.绑定网格容器(容器);
            物体.AddComponent<房间图层>();
            Debug.LogWarning("[房间] 场景未接线 房间面板.网格面板 —— 已在面板下自动建一个（要美观请手动搭建并拖引用位）。");
            return;
        }
        if (网格面板.GetComponent<房间图层>() != null) return;
        var 错图层 = 网格面板.GetComponent<探索图层>();
        if (错图层 != null)
            Debug.LogError($"[房间] {网格面板.name} 上挂的是 {错图层.GetType().Name}，不是 房间图层 —— 请改挂 房间图层；"
                         + "（挂错的表现：地表/路径/落点都没有，点击也完全没反应）");
        else
        {
            网格面板.gameObject.AddComponent<房间图层>();
            Debug.LogWarning($"[房间] {网格面板.name} 上漏挂 房间图层 —— 已自动补上（没它：没有地表/路径/落点，点击也没人处理）。");
        }
    }
}
