using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 玩家输入系统：全局统一输入检测与调控（操作减负）。
// 鼠标左键 = 确认（UI 按钮原生左键即确认，不拦截，避免与按钮双击）；
// 鼠标右键 = 取消/回退（触发当前显示面板的 回退()，替代到处找返回按钮）。
// 兼容新旧两套输入系统：按项目激活的输入处理自动选用 Mouse.current 或 Input.GetMouseButtonDown。
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
        // 左键 = 确认：由各 UI 按钮的 onClick 原生处理，这里不拦截
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
}
