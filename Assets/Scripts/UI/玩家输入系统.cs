using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 玩家输入系统：全局统一输入检测与调控（操作减负）。
// 鼠标左键 = 确认（UI 按钮原生左键即确认，不拦截，避免与按钮双击）；
// 鼠标右键 = 物品操作菜单（见 右键菜单/网格背包面板.物品右键）；全局回退 由 侧边栏取消按钮 触发（右键回退已取消）。
// 测试热键：F1 = 打开网格背包面板；F2 = 背包加入 1 个随机物品（测试场景验证网格背包用）。
// 兼容新旧两套输入系统：按项目激活的输入处理自动选用 Keyboard/Mouse.current 或 Input.GetKeyDown。
// 挂载：与 面板管理器 同物体（UI管理器），或任意常驻物体。
public sealed class 玩家输入系统 : MonoBehaviour
{
    private 面板管理器 面板;

    void Awake()
    {
        面板 = GetComponent<面板管理器>();
        if (面板 == null) 面板 = Object.FindFirstObjectByType<面板管理器>();
    }

    void Update()
    {
        if (检测按下(KeyCode.F1)) 打开背包面板();
        if (检测按下(KeyCode.F2)) 测试加随机物品();
        if (检测按下(KeyCode.F3)) 测试加容器();
        if (检测按下(KeyCode.F4)) 测试清空背包();
        if (检测按下(KeyCode.F5)) 测试随机穿戴容器();
        // 左键 = 确认：由各 UI 按钮的 onClick 原生处理，这里不拦截
        // 右键 = 物品操作菜单：由 网格背包面板 的物品点击组件处理（此处不再做全局回退）
    }

    // 按键检测：兼容新(InputSystem)/旧(Input Manager)/Both
    private bool 检测按下(KeyCode 键)
    {
        bool 按下 = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (键 == KeyCode.F1 && Keyboard.current.f1Key.wasPressedThisFrame) 按下 = true;
            if (键 == KeyCode.F2 && Keyboard.current.f2Key.wasPressedThisFrame) 按下 = true;
            if (键 == KeyCode.F3 && Keyboard.current.f3Key.wasPressedThisFrame) 按下 = true;
            if (键 == KeyCode.F4 && Keyboard.current.f4Key.wasPressedThisFrame) 按下 = true;
            if (键 == KeyCode.F5 && Keyboard.current.f5Key.wasPressedThisFrame) 按下 = true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(键)) 按下 = true;
#endif
        return 按下;
    }

    // F4：一键清空背包网格物品（测试用；装备槽保留），发布事件刷新所有面板
    private void 测试清空背包()
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        var 事件 = ServiceRegistry.Get<EventBus>();
        if (玩家 == null) return;
        int 数量 = 玩家.背包.Count;
        if (数量 <= 0) { 事件?.发布(new 日志事件(日志类型.反馈, "[测试] 背包已空，无需清空。")); return; }
        玩家.背包.Clear();
        事件?.发布(new 背包变化事件("", 0, 变化原因.失去));   // 触发网格/面板刷新
        事件?.发布(new 日志事件(日志类型.反馈, $"[测试] 已清空背包（{数量} 件）。"));
    }

    // F1：打开/关闭 装备与背包面板（含 装备区 + 背包区 子面板；需在 面板管理器 的「背包」引用位接线 装备背包面板）
    private void 打开背包面板()
    {
        if (面板 == null)
        {
            ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.反馈坏, "[测试] 场景缺少 面板管理器。"));
            return;
        }
        if (面板.当前显示面板 is 装备背包面板) { 面板.返回上一面板(); return; }   // 再按 F1 → 关闭（返回上一面板）
        面板.显示面板类型<装备背包面板>();
        if (面板.当前显示面板 is not 装备背包面板)
            ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.反馈坏, "[测试] 装备与背包面板未接线到 面板管理器.背包 引用位。"));
    }

    // F2：背包加入随机物品（数量 1~3，自动找空位；放不下提示）
    private void 测试加随机物品()
    {
        var 数据 = ServiceRegistry.Get<DataService>();
        var 玩家 = ServiceRegistry.Get<PlayerService>();
        var 事件 = ServiceRegistry.Get<EventBus>();
        if (数据 == null || 玩家 == null || 事件 == null) return;
        if (数据.物品.Count == 0)
        {
            事件.发布(new 日志事件(日志类型.反馈坏, "[测试] 无物品数据（items.json 缺失）。"));
            return;
        }
        var 表 = new List<string>(数据.物品.Keys);
        var 标识 = 表[Random.Range(0, 表.Count)];
        int 数量 = Random.Range(1, 4);
        var 目标 = 第一个穿戴容器();   // 塔科夫式：物品放穿戴容器（弹挂/腰封/背包），无独立主背包
        if (目标 == null)
        {
            事件.发布(new 日志事件(日志类型.反馈坏, "[测试] 未穿戴任何容器（弹挂/腰封/背包），无处放置。"));
            return;
        }
        int 实际 = 目标.放入网格(标识, 数量);
        事件.发布(new 背包变化事件(标识, 实际, 变化原因.获得));
        string 名称 = 数据.物品.TryGetValue(标识, out var 物) ? 物.名称 : 标识;
        if (实际 <= 0) 事件.发布(new 日志事件(日志类型.反馈坏, $"[测试] 容器已满，{名称} 放不下了！"));
        else if (实际 < 数量) 事件.发布(new 日志事件(日志类型.反馈坏, $"[测试] 容器空间不足，只放入了 {名称} ×{实际}/{数量}。"));
        else 事件.发布(new 日志事件(日志类型.反馈, $"[测试] 获得 {名称} ×{实际}"));
    }

    // 第一个穿戴容器（弹挂/腰封/背包 按序）；没穿返回 null
    private 背包服务 第一个穿戴容器()
    {
        var 档案 = ServiceRegistry.Get<PlayerService>()?.档案;
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        if (档案 == null || 容器服务 == null) return null;
        foreach (var 槽 in new[] { "弹挂", "腰封", "背包" })
        {
            var 记录 = 档案.装备.Find(e => e.槽位 == 槽);
            if (记录 != null && !string.IsNullOrEmpty(记录.标识)) return 容器服务.穿戴容器视图(记录);
        }
        return null;
    }

    // F5：随机装备 3 个容器槽（弹挂/腰封/背包）——测试穿戴容器用（凭空穿戴，不占背包物品）
    private void 测试随机穿戴容器()
    {
        var 数据 = ServiceRegistry.Get<DataService>();
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        var 事件 = ServiceRegistry.Get<EventBus>();
        if (数据 == null || 玩家 == null || 事件 == null) return;
        foreach (var 槽 in new[] { "弹挂", "腰封", "背包" })
        {
            var 候选 = new List<物品数据>();
            foreach (var 物 in 数据.物品.Values)
                if (物.槽位 == 槽 && 物.是容器) 候选.Add(物);
            if (候选.Count == 0) continue;
            玩家.装备到槽(槽, 候选[Random.Range(0, 候选.Count)].标识);   // 装备到槽 自动初始化容器
        }
        事件.发布(new 属性变化事件(玩家.体质, 玩家.力量, 玩家.智慧, 玩家.敏捷, 玩家.意志, 玩家.自由属性点));   // 触发 中区/装备面板 刷新
        事件.发布(new 日志事件(日志类型.反馈, "[测试] 已随机穿戴 弹挂/腰封/背包。"));
    }

    // F3：一键添加容器 + 配套物品（测试容器系统用）
    // 放入：弹药箱 + 子弹、医疗箱 + 绷带/医疗包、战术背包 + 胶带/零件（并初始化容器内部网格）
    private void 测试加容器()
    {
        var 数据 = ServiceRegistry.Get<DataService>();
        var 玩家 = ServiceRegistry.Get<PlayerService>();
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        var 事件 = ServiceRegistry.Get<EventBus>();
        if (数据 == null || 玩家 == null || 容器服务 == null || 事件 == null) return;
        var 档案 = 玩家.档案;
        bool 放(string 标识, int 数量 = 1)
        {
            if (!数据.物品.ContainsKey(标识)) { 事件.发布(new 日志事件(日志类型.反馈坏, $"[测试] 物品 {标识} 不存在。")); return false; }
            int 实际 = 档案.放入网格(标识, 数量);
            if (实际 <= 0) { 事件.发布(new 日志事件(日志类型.反馈坏, $"[测试] 背包已满，{标识} 放不下！")); return false; }
            return true;
        }
        // ① 弹药箱 + 子弹（放容器前先初始化容器，再把子弹塞进容器内部）
        if (放("弹药箱", 1))
        {
            var 弹药箱 = 档案.背包服务.背包.Find(s => s != null && s.标识 == "弹药箱" && s.列 >= 0 && s.容器物品 == null);
            if (弹药箱 != null)
            {
                容器服务.初始化容器(弹药箱);
                var 视图 = 容器服务.打开(弹药箱);
                视图.放入网格("弹药", 50);
            }
        }
        // ② 医疗箱 + 医疗品
        if (放("医疗箱", 1))
        {
            var 医疗箱 = 档案.背包服务.背包.Find(s => s != null && s.标识 == "医疗箱" && s.列 >= 0 && s.容器物品 == null);
            if (医疗箱 != null)
            {
                容器服务.初始化容器(医疗箱);
                var 视图 = 容器服务.打开(医疗箱);
                视图.放入网格("绷带", 5);
                视图.放入网格("医疗包", 2);
            }
        }
        // ③ 战术背包 + 材料
        if (放("战术背包", 1))
        {
            var 背包 = 档案.背包服务.背包.Find(s => s != null && s.标识 == "战术背包" && s.列 >= 0 && s.容器物品 == null);
            if (背包 != null)
            {
                容器服务.初始化容器(背包);
                var 视图 = 容器服务.打开(背包);
                视图.放入网格("胶带", 10);
                视图.放入网格("零件", 10);
            }
        }
        事件.发布(new 背包变化事件("弹药箱", 1, 变化原因.获得));
        事件.发布(new 日志事件(日志类型.反馈, "[测试] 已添加 弹药箱/医疗箱/战术背包（含配套物品）。双击容器物品打开。"));
    }
}
