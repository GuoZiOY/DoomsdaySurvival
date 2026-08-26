using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 玩家输入系统：全局统一输入检测与调控（操作减负）。
// 鼠标左键 = 确认（UI 按钮原生左键即确认，不拦截，避免与按钮双击）；
// 鼠标右键 = 取消/回退（触发当前显示面板的 回退()，替代到处找返回按钮）。
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
        if (检测右键()) 触发回退();
        if (检测按下(KeyCode.F1)) 打开背包面板();
        if (检测按下(KeyCode.F2)) 测试加随机物品();
        if (检测按下(KeyCode.F3)) 测试加容器();
        // 左键 = 确认：由各 UI 按钮的 onClick 原生处理，这里不拦截
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
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(键)) 按下 = true;
#endif
        return 按下;
    }

    // 右键检测：兼容新(InputSystem)/旧(Input Manager)/Both
    private bool 检测右键()
    {
        bool 按下 = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) 按下 = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(1)) 按下 = true;
#endif
        return 按下;
    }

    // 右键 → 当前显示面板的 回退()
    private void 触发回退()
    {
        var 当前 = 面板?.当前显示面板;
        if (当前 != null) 当前.回退();
    }

    // F1：打开网格背包面板（需在 面板管理器 的「背包」引用位接线）
    private void 打开背包面板()
    {
        if (面板 == null)
        {
            ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.反馈坏, "[测试] 场景缺少 面板管理器。"));
            return;
        }
        面板.显示面板类型<网格背包面板>();
        if (面板.当前显示面板 is not 网格背包面板)
            ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.反馈坏, "[测试] 背包面板未接线到 面板管理器.背包 引用位。"));
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
        int 实际 = 玩家.档案.放入网格(标识, 数量);
        事件.发布(new 背包变化事件(标识, 实际, 变化原因.获得));
        string 名称 = 数据.物品.TryGetValue(标识, out var 物) ? 物.名称 : 标识;
        if (实际 <= 0) 事件.发布(new 日志事件(日志类型.反馈坏, $"[测试] 背包已满，{名称} 放不下了！"));
        else if (实际 < 数量) 事件.发布(new 日志事件(日志类型.反馈坏, $"[测试] 背包空间不足，只放入了 {名称} ×{实际}/{数量}。"));
        else 事件.发布(new 日志事件(日志类型.反馈, $"[测试] 获得 {名称} ×{实际}"));
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
