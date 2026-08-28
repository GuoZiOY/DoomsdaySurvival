using UnityEngine;
using UnityEngine.UI;

// 装备面板（左区，场景手动搭建 UI）：7 个装备槽位（主手/副手/头部/胸部/腿部/脚部/手部 + 容器位 弹挂/腰封/背包），槽位式（非网格）。
//   引用方式：直接拖 装备槽预制体组件（装备槽[]）——槽位参数在预制体上配置，改预制体全局生效。
//   刷新：遍历 槽位 → 各槽 设置(玩家, 数据) + 强制 Layout 重建（槽显隐/尺寸变化即时生效）；命中槽位：拖拽穿戴。
public sealed class 装备面板 : MonoBehaviour
{
    public static 装备面板 实例;   // 场景挂载自动登记

    [SerializeField] private 装备槽[] 槽位;   // 装备槽（引用预制体上的 装备槽 组件；顺序 = 视觉顺序）
    [SerializeField] private RectTransform 布局父;   // 装备槽 布局容器（Layout Group 所在）；刷新后强制重排

    void Awake()
    {
        实例 = this;
        ServiceRegistry.Get<EventBus>()?.订阅<背包变化事件>(背包变化响应);   // 装备/卸下/换装 都发该事件 → 自动刷新
        刷新();
    }

    private void OnEnable() => 刷新();

    private void OnDestroy()
    {
        if (ServiceRegistry.已注册<EventBus>())
            ServiceRegistry.Get<EventBus>()?.取消订阅<背包变化事件>(背包变化响应);
    }

    // 背包变化（装备/卸下/丢弃/获得…）→ 脏标记，Update 合并刷新（避免同帧多次重建）
    private bool 待刷新;

    // 外部请求刷新（装备槽/右键菜单 拖拽操作后）：设脏标记，Update 帧末合并刷新（与事件驱动合并，避免重复全量刷新）
    public void 请求刷新() => 待刷新 = true;

    void Update()
    {
        if (!待刷新) return;
        待刷新 = false;
        刷新();
    }

    private void 背包变化响应(背包变化事件 _) => 待刷新 = true;

    // 刷新所有槽位显示（物品名/品质图/物品图/耐久）+ 强制 Layout 重建
    public void 刷新()
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null) return;
        var 数据 = ServiceRegistry.Get<DataService>();
        if (槽位 == null) return;
        foreach (var 槽 in 槽位)
            if (槽 != null) 槽.设置(玩家, 数据);
        if (布局父 != null) LayoutRebuilder.ForceRebuildLayoutImmediate(布局父);   // 槽显隐/尺寸变化即时生效
    }

    // 屏幕点 命中的装备槽（拖拽穿戴用）；未命中返回 false
    public bool 命中槽位(Vector2 屏幕点, out string 槽位名)
    {
        槽位名 = null;
        if (槽位 == null || 槽位.Length == 0) return false;
        var 画布 = 槽位[0]?.框体 != null ? 槽位[0].框体.GetComponentInParent<Canvas>() : null;
        var 相机 = 画布 != null && 画布.renderMode != RenderMode.ScreenSpaceOverlay ? 画布.worldCamera : null;
        foreach (var 槽 in 槽位)
        {
            if (槽 == null || 槽.框体 == null) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(槽.框体, 屏幕点, 相机))
            {
                槽位名 = 槽.槽位;
                return true;
            }
        }
        return false;
    }

    // —— 装备拖拽投影：目标槽位 绿（类型匹配）/ 红（不匹配）高亮 ——
    public void 高亮槽位(string 槽位名, bool 可放)
    {
        foreach (var 槽 in 槽位)
            if (槽 != null && 槽.槽位 == 槽位名) { 槽.显示高亮(可放); return; }
    }

    public void 清除全部高亮()
    {
        if (槽位 == null) return;
        foreach (var 槽 in 槽位)
            if (槽 != null) 槽.清除高亮();
    }
}
