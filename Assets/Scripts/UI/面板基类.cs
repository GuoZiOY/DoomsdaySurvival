using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 面板基类：每个 UI 面板 = 一个组件（功能/系统/设施的交互表现）。
// 逻辑参数 ↔ UI 组件 通过 Inspector 拖拽绑定（[SerializeField]），不靠路径字符串。
public abstract class 面板基类 : MonoBehaviour
{
    // 共享按钮预制体（由 面板管理器 在 Inspector 设置，面板创建动态按钮用）
    public static GameObject 按钮预制体;

    // 常驻面板（HUD/日志）：不被 面板管理器 隐藏切换
    public bool 常驻;

    // 设施面板认领标识（如 "商店"）；非设施面板留空。路由器靠它做数据路由。
    [SerializeField] private string 设施标识;
    public string 设施标识值 => 设施标识;

    // 面板管理器调用：激活自己 + 刷新内容
    public void 显示面板(object 上下文 = null)
    {
        gameObject.SetActive(true);
        刷新(上下文);
    }

    public void 隐藏面板() => gameObject.SetActive(false);

    // 子类：进入面板时填充内容（上下文 可由打开者传入，如设施返回节点）
    protected abstract void 刷新(object 上下文);

    // ===== 共享工具 =====

    // 在父级创建一行按钮（克隆共享按钮预制体 + 设文字 + 接点击）
    protected void 创建行(RectTransform 父, string 文字, Action 点击)
    {
        if (按钮预制体 == null) { Debug.LogError("[面板基类] 未设置按钮预制体（请在 面板管理器 的 Inspector 拖入）"); return; }
        var 物体 = Instantiate(按钮预制体, 父, false);
        var 文本 = 物体.transform.Find("文字")?.GetComponent<TMP_Text>();
        if (文本 != null) 文本.text = 文字;
        var 按钮 = 物体.GetComponent<Button>();
        if (按钮 != null) 按钮.onClick.AddListener(() => 点击());
    }

    protected void 清空(RectTransform 列表)
    {
        if (列表 == null) return;
        for (int i = 列表.childCount - 1; i >= 0; i--) Destroy(列表.GetChild(i).gameObject);
    }

    // 设文本（空引用安全）
    protected void 设文本(TMP_Text 文本, string 内容)
    {
        if (文本 != null) 文本.text = 内容;
    }

    // 设施返回：从城镇地图进入的设施回城镇地图；从剧情进入的回剧情节点
    protected void 返回设施(string 返回节点)
    {
        if (string.IsNullOrEmpty(返回节点)) return;
        if (返回节点.StartsWith("城镇:")) { ServiceRegistry.Get<地图服务>().回到城镇地图(); return; }
        ServiceRegistry.Get<DialogueService>().进入节点(返回节点);
    }
}
