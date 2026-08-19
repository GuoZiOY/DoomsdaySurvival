using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 地图渲染工具：节点图（节点按钮 + 连线）的通用渲染，供 大地图/城镇小地图 面板复用
public static class 地图渲染
{
    // 地图节点专用按钮预制体（由 面板管理器 在 Inspector 设置；为空时退回共享按钮预制体）
    public static GameObject 节点预制体;

    // 0~100 坐标 → 容器内 anchoredPosition
    public static Vector2 归一化(RectTransform 容器, float x, float y)
    {
        var 尺寸 = 容器.rect.size;
        return new Vector2((x / 100f - 0.5f) * 尺寸.x, (y / 100f - 0.5f) * 尺寸.y);
    }

    // 创建/取回「地图内容」容器：节点与连线都放进它，平移缩放只变换它（不重渲染，拖拽/滚轮流畅）
    public static RectTransform 创建内容(RectTransform 内容区)
    {
        var 子 = 内容区.Find("地图内容");
        if (子 != null) return 子 as RectTransform;
        var 物体 = new GameObject("地图内容", typeof(RectTransform));
        物体.transform.SetParent(内容区, false);
        var 矩形 = (RectTransform)物体.transform;
        矩形.anchorMin = Vector2.zero;
        矩形.anchorMax = Vector2.one;
        矩形.offsetMin = Vector2.zero;
        矩形.offsetMax = Vector2.zero;
        return 矩形;
    }

    // 应用平移缩放视图：只动容器变换，节点无需重画
    public static void 应用视图(RectTransform 内容, float 缩放, Vector2 平移)
    {
        if (内容 == null) return;
        内容.localScale = Vector3.one * 缩放;
        内容.anchoredPosition = 平移;
    }

    // 在容器创建地图节点按钮：当前节点金色、选中节点钢蓝、其余正文色。
    // 返回文字组件供面板做「单击选中→改色」；尺寸由地图节点预制体决定。
    public static TMP_Text 创建节点(RectTransform 父, string 名称, Vector2 位置, bool 当前, bool 选中, Action 点击)
    {
        var 预制体 = 节点预制体 != null ? 节点预制体 : 面板基类.按钮预制体;
        if (预制体 == null) { Debug.LogError("[地图渲染] 未设置按钮预制体或地图节点预制体（请在 面板管理器 的 Inspector 拖入）"); return null; }
        var 物体 = UnityEngine.Object.Instantiate(预制体, 父, false);
        var 文本 = 物体.transform.Find("文字")?.GetComponent<TMP_Text>();
        if (文本 != null) { 文本.text = 名称; 文本.color = 当前 ? 游戏主题.金色 : 选中 ? 游戏主题.选中色 : 游戏主题.文字; }
        var 矩形 = (RectTransform)物体.transform;
        矩形.anchoredPosition = 位置;
        var 按钮 = 物体.GetComponent<Button>();
        if (按钮 != null)
        {
            按钮.onClick.AddListener(() => 点击());
            音效管理器.实例?.注册按钮(按钮);   // 地图节点动态按钮成功音效
        }
        return 文本;
    }

    // 两点间画细线（Image 旋转拉伸）
    public static void 画线(RectTransform 父, Vector2 a, Vector2 b, Color 颜色)
    {
        var 物体 = new GameObject("线", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(父, false);
        var 图像 = 物体.GetComponent<Image>();
        图像.color = 颜色;
        图像.raycastTarget = false;
        var 矩形 = (RectTransform)物体.transform;
        Vector2 方向 = b - a;
        float 长度 = 方向.magnitude;
        float 角度 = Mathf.Atan2(方向.y, 方向.x) * Mathf.Rad2Deg;
        矩形.sizeDelta = new Vector2(Mathf.Max(1f, 长度), 2f);
        矩形.anchoredPosition = (a + b) / 2f;
        矩形.localEulerAngles = new Vector3(0, 0, 角度);
    }
}

// 双击检测器：地图节点交互用。单击=选中（返回 false），阈值内同节点二次点击=双击进入（返回 true）
public sealed class 双击检测
{
    private string 上次节点;
    private float 上次时间;
    private const float 间隔 = 0.3f;

    // 每次节点点击都调用；返回 true 表示命中双击
    public bool 点击(string 节点)
    {
        var 现在 = Time.time;
        bool 命中 = 节点 == 上次节点 && 现在 - 上次时间 < 间隔;
        上次节点 = 节点;
        上次时间 = 现在;
        return 命中;
    }
}
