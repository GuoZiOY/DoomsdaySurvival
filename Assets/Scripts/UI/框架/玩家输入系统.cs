using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 玩家输入系统：全局统一输入检测与调控（操作减负）。
// 鼠标左键 = 确认（UI 按钮原生左键即确认，不拦截，避免与按钮双击）；
// 鼠标右键 = 物品操作菜单（见 右键菜单/物品网格面板.物品右键）；全局回退 由 侧边栏取消按钮 触发（右键回退已取消）。
// 测试功能（加物品/加容器/清空/随机穿戴/仓库）由 快速测试面板 按钮提供（F 键测试已移除）。
// F1 = 打开/关闭 持有面板（保留为常用快捷键）。
// 兼容新旧两套输入系统：按项目激活的输入处理自动选用 Keyboard/Mouse.current 或 Input.GetKeyDown。
// 挂载：与 面板管理器 同物体（UI管理器），或任意常驻物体。
public sealed class 玩家输入系统 : MonoBehaviour
{
    private 面板管理器 面板;

    void Awake()
    {
        面板 = GetComponent<面板管理器>();
        if (面板 == null) 面板 = Object.FindFirstObjectByType<面板管理器>();
        快速测试面板.确保存在();   // 自动创建 快速测试面板（编辑器与打包程序均可用，便于打包后继续调试）
    }

    void Update()
    {
        if (检测按下(KeyCode.F1)) 打开背包面板();
        // 左键 = 确认：由各 UI 按钮的 onClick 原生处理，这里不拦截
        // 右键 = 物品操作菜单：由 物品网格面板 的物品点击组件处理（此处不再做全局回退）
    }

    // 按键检测：兼容新(InputSystem)/旧(Input Manager)/Both
    private bool 检测按下(KeyCode 键)
    {
        bool 按下 = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (键 == KeyCode.F1 && Keyboard.current.f1Key.wasPressedThisFrame) 按下 = true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(键)) 按下 = true;
#endif
        return 按下;
    }

    // F1：打开/关闭 持有面板（含 装备区 + 中区穿戴容器 + 右区仓库；需在 面板管理器 的「背包」引用位接线 持有面板）
    private void 打开背包面板()
    {
        if (面板 == null)
        {
            ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.反馈坏, "[测试] 场景缺少 面板管理器。"));
            return;
        }
        if (面板.当前显示面板 is 持有面板) { 面板.返回上一面板(); return; }   // 再按 F1 → 关闭（返回上一面板）
        面板.显示面板类型<持有面板>();
        if (面板.当前显示面板 is not 持有面板)
            ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.反馈坏, "[测试] 持有面板未接线到 面板管理器.背包 引用位。"));
    }
}
