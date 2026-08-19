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

    // 内容区：面板动态内容（列表行/选项/正文等）的渲染容器。面板在 Inspector 拖入这一个内容锚点即可。
    // 子类可覆盖显示/隐藏只切换内容区（容器型面板，如 主视窗=中央视窗框架）保持框架常驻。
    [SerializeField] protected RectTransform 内容区;

    // 面板管理器调用：激活自己 + 刷新内容
    // virtual：容器类面板（如 主视窗=中央视窗框架）可只切换自身内容区，保持框架本体常驻
    public virtual void 显示面板(object 上下文 = null)
    {
        gameObject.SetActive(true);
        刷新(上下文);
    }

    public virtual void 隐藏面板() => gameObject.SetActive(false);

    // 全局"取消/回退"的统一入口：子类按各自语义覆盖（如 战斗面板=取消选目标 / 功能面板=返回设施内部）。
    // 由 玩家输入系统（右键）与 侧边栏取消按钮 调用当前显示面板的 回退()。
    // 返回 true = 本次取消被消费（有动作）；false = 无可取消（调用方决定是否播错误音效）。
    public virtual bool 回退() => false;

    // 取消按钮的文案：随当前面板动态显示（默认"取消"；各面板覆写为 返回/离开城镇/关闭/回主菜单 等）
    public virtual string 取消文本 => "取消";

    // 子类：进入面板时填充内容（上下文 可由打开者传入，如设施返回节点）
    protected abstract void 刷新(object 上下文);

    // ===== 共享工具 =====

    // 在父级创建一行按钮（克隆共享按钮预制体 + 设文字 + 接点击）
    // 移除布局=true 去掉克隆体 LayoutElement（对话选项不需要固定高度）；文字居中=true 覆盖对齐为居中（对话选项）
    // static：子面板组件（非 面板基类 子类）也能调用
    public static void 创建行(RectTransform 父, string 文字, Action 点击, bool 移除布局 = false, bool 文字居中 = false)
    {
        if (按钮预制体 == null) { Debug.LogError("[面板基类] 未设置按钮预制体（请在 面板管理器 的 Inspector 拖入）"); return; }
        if (父 == null) { Debug.LogWarning("[面板基类] 创建行：内容区未接线"); return; }
        var 物体 = Instantiate(按钮预制体, 父, false);
        if (移除布局)
        {
            var 布局 = 物体.GetComponent<LayoutElement>();
            if (布局 != null) Destroy(布局);
        }
        var 文本 = 物体.transform.Find("文字")?.GetComponent<TMP_Text>();
        if (文本 != null)
        {
            文本.text = 文字;
            if (文字居中) 文本.alignment = TextAlignmentOptions.Center;
        }
        var 按钮 = 物体.GetComponent<Button>();
        if (按钮 != null)
        {
            按钮.onClick.AddListener(() => 点击());
            音效管理器.实例?.注册按钮(按钮);   // 动态按钮成功音效（场景静态按钮由管理器自动扫描）
        }
    }

    // 克隆一个「行模板」到父容器并激活；返回指定组件（专用行组件用，如 背包行）。
    // static：子面板组件（非 面板基类 子类）也能调用
    public static T 创建模板<T>(RectTransform 父, GameObject 模板) where T : Component
    {
        if (父 == null || 模板 == null) return null;
        var 物体 = Instantiate(模板, 父, false);
        物体.SetActive(true);
        return 物体.GetComponent<T>();
    }

    public static void 清空(RectTransform 列表)
    {
        if (列表 == null) return;
        for (int i = 列表.childCount - 1; i >= 0; i--) Destroy(列表.GetChild(i).gameObject);
    }

    // 创建一行标签（非按钮，纯文字；分区标题等），用 TMP 默认字体
    public static void 创建标签(RectTransform 父, string 文字)
    {
        if (父 == null) return;
        var 物体 = new GameObject("标签", typeof(RectTransform), typeof(TextMeshProUGUI));
        物体.transform.SetParent(父, false);
        var 文本 = 物体.GetComponent<TextMeshProUGUI>();
        文本.text = 文字;
        文本.color = 游戏主题.暗淡;
        文本.fontSize = 20;
        文本.enableWordWrapping = false;
    }

    // 设文本（空引用安全）
    public static void 设文本(TMP_Text 文本, string 内容)
    {
        if (文本 != null) 文本.text = 内容;
    }

    // 选中缩放：Tab/排序 等切换按钮的选中态——固定放大一点（替代颜色高亮），悬停反馈叠在其上
    public const float 选中缩放 = 1.08f;

    public static void 设选中缩放(Button 按钮, bool 选中)
    {
        if (按钮 == null) return;
        float 目标 = 选中 ? 选中缩放 : 1f;
        var 反馈 = 按钮.GetComponent<悬停反馈>();
        if (反馈 != null) 反馈.设基座(目标);
        else 按钮.transform.localScale = Vector3.one * 目标;
    }

    // 设施返回：从城镇地图进入的设施回城镇地图；从剧情进入的回剧情节点
    protected void 返回设施(string 返回节点)
    {
        if (string.IsNullOrEmpty(返回节点)) return;
        if (返回节点.StartsWith("城镇:")) { ServiceRegistry.Get<地图服务>().回到城镇地图(); return; }
        ServiceRegistry.Get<DialogueService>().进入节点(返回节点);
    }
}
