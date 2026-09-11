using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 面板基类：每个 UI 面板 = 一个组件（全静态场景搭建）。
// 显示/隐藏 = 纯 SetActive（无 DOTween 过渡动画）。
// 提供：回退()/取消文本 全局返回协议 + 行构建工具（运行时生成战斗/地图/对话内容用）。
// 逻辑参数 ↔ UI 组件 通过 Inspector 拖拽绑定（[SerializeField]），不靠路径字符串。
// 注意：基类不含「内容区」——需要动态内容容器的面板（地图/战斗/制作等）自行声明各自的内容区。
public abstract class 面板基类 : MonoBehaviour
{
    // 共享按钮预制体（由 面板管理器 在 Inspector 设置，运行时生成按钮用）
    public static GameObject 按钮预制体;

    // 面板管理器调用：激活 + 刷新内容
    public virtual void 显示面板(object 上下文 = null, bool 上下互切 = false, bool 返回方向 = false)
    {
        gameObject.SetActive(true);
        刷新(上下文);
    }

    // 隐藏：直接失活（无动画）
    public virtual void 隐藏面板(bool 上下互切 = false, bool 返回方向 = false)
    {
        gameObject.SetActive(false);
    }

    // 是否为「侧边式」面板：末日全屏面板默认 false（保留字段供子类覆写，如 设置覆盖层）
    public virtual bool 侧边式面板 => false;

    // 是否为「瞬时面板」（临时插进来的一层，如 战斗面板）：显示它**不覆盖**"上一个面板"，
    // 这样它结束后回到原面板（房间 / 探索）时，那个面板的"返回上一面板"还指向真正的前一层（地图等），
    // 不会一点取消就跳回已经打完的战斗结算面板。
    public virtual bool 瞬时面板 => false;

    // 全局"取消/回退"统一入口：子类按各自语义覆盖（战斗=取消选目标 / 探索=撤离 / 角色创建=回主菜单）。
    // 由 玩家输入系统（右键）与 侧边栏取消按钮 调用当前显示面板的 回退()。
    // 返回 true = 本次取消被消费；false = 无可取消（调用方决定是否播错误音效）。
    public virtual bool 回退() => false;

    // 取消按钮文案：随当前面板动态显示（默认"取消"；各面板覆写）
    public virtual string 取消文本 => "取消";

    // 子类：进入面板时填充内容（上下文由打开者传入）
    protected abstract void 刷新(object 上下文);

    // ===== 共享工具（运行时生成内容用；不依赖动画） =====

    // 在父级创建一行按钮（克隆共享按钮预制体 + 设文字 + 接点击）
    public static void 创建行(RectTransform 父, string 文字, Action 点击, bool 移除布局 = false, bool 文字居中 = false)
    {
        if (按钮预制体 == null) { Debug.LogError("[面板基类] 未设置按钮预制体（请在 面板管理器 的 Inspector 拖入）"); return; }
        if (父 == null) { Debug.LogWarning("[面板基类] 创建行：容器未接线"); return; }
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
            音效管理器.实例?.注册按钮(按钮);
        }
    }

    // 克隆一个「行模板」到父容器并激活；返回指定组件（专用行组件用）
    public static T 创建模板<T>(RectTransform 父, GameObject 模板) where T : Component
    {
        if (父 == null || 模板 == null) return null;
        var 物体 = Instantiate(模板, 父, false);
        物体.SetActive(true);
        return 物体.GetComponent<T>();
    }

    // 清空容器子对象（运行时生成内容前清旧）
    public static void 清空(RectTransform 列表)
    {
        if (列表 == null) return;
        for (int i = 列表.childCount - 1; i >= 0; i--) Destroy(列表.GetChild(i).gameObject);
    }

    // 创建一行标签（纯文字，分区标题等）
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

    // 设选中缩放：Tab/排序等切换按钮的选中态——固定放大一点（无动画，直接改缩放）
    public const float 选中缩放 = 1.08f;

    public static void 设选中缩放(Button 按钮, bool 选中)
    {
        if (按钮 == null) return;
        按钮.transform.localScale = Vector3.one * (选中 ? 选中缩放 : 1f);
    }
}
