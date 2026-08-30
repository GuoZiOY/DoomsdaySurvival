using TMPro;
using UnityEngine;
using UnityEngine.UI;

// UI 工具：动态创建 uGUI/TMP 组件的静态辅助——收敛各面板的 new GameObject + SetParent + 锚点 + 组件 样板。
// 高频用法：创建图（黑块/底图/投影/图标）、创建文本（名称/数量/角标/提示）、创建物体（纯容器层）。
// 搜索面板 / 网格面板 / 拖拽视觉 等 动态 UI 全部走这里（改风格只动本类）。
public static class UI工具
{
    // 创建 纯 RectTransform 物体（无 附加 组件），设 单点锚 + pivot
    public static RectTransform 创建物体(Transform 父, string 名字, Vector2 锚点, Vector2 pivot)
    {
        var 物体 = new GameObject(名字, typeof(RectTransform));
        物体.transform.SetParent(父, false);
        var 矩形 = 物体.GetComponent<RectTransform>();
        矩形.anchorMin = 锚点;
        矩形.anchorMax = 锚点;
        矩形.pivot = pivot;
        return 矩形;
    }

    // 创建 物体 + 附加 组件（Image/TMP 等），设 单点锚 + pivot；返回 组件
    public static T 创建<T>(Transform 父, string 名字, Vector2 锚点, Vector2 pivot) where T : Component
    {
        var 物体 = new GameObject(名字, typeof(RectTransform));
        物体.transform.SetParent(父, false);
        var 组件 = 物体.AddComponent<T>();
        var 矩形 = 物体.GetComponent<RectTransform>();
        矩形.anchorMin = 锚点;
        矩形.anchorMax = 锚点;
        矩形.pivot = pivot;
        return 组件;
    }

    // 创建 图（Image + 精灵 + 颜色，不挡交互），单点锚
    public static Image 创建图(Transform 父, string 名字, Sprite 精灵, Color 颜色, Vector2 锚点, Vector2 pivot)
    {
        var 图 = 创建<Image>(父, 名字, 锚点, pivot);
        图.sprite = 精灵;
        图.color = 颜色;
        图.raycastTarget = false;
        return 图;
    }

    // 创建 居中 图（锚/pivot 中心 + 尺寸 + 位置），返回 矩形
    public static RectTransform 创建居中图(Transform 父, string 名字, Sprite 精灵, Vector2 尺寸, Vector2 位置)
    {
        var 图 = 创建<Image>(父, 名字, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        图.sprite = 精灵;
        图.raycastTarget = false;
        var 矩形 = 图.rectTransform;
        矩形.anchoredPosition = 位置;
        矩形.sizeDelta = 尺寸;
        return 矩形;
    }

    // 创建 文本（TMP：白字/不挡交互/默认锚调用方设）；返回 文本
    public static TMP_Text 创建文本(Transform 父, string 名字, string 内容, float 字号, TextAlignmentOptions 对齐)
    {
        var 文本 = 创建<TextMeshProUGUI>(父, 名字, Vector2.zero, Vector2.zero);
        文本.text = 内容;
        文本.fontSize = 字号;
        文本.alignment = 对齐;
        文本.color = Color.white;
        文本.raycastTarget = false;
        return 文本;
    }

    // stretch 铺满 父级（offset 0）
    public static void 铺满(RectTransform 矩形)
    {
        矩形.anchorMin = Vector2.zero;
        矩形.anchorMax = Vector2.one;
        矩形.offsetMin = Vector2.zero;
        矩形.offsetMax = Vector2.zero;
    }

    // 设锚/定位 一条龙（单点锚 + pivot + 位置 + 尺寸）
    public static void 设锚(RectTransform 矩形, Vector2 锚点, Vector2 pivot, Vector2 位置, Vector2 尺寸)
    {
        矩形.anchorMin = 锚点;
        矩形.anchorMax = 锚点;
        矩形.pivot = pivot;
        矩形.anchoredPosition = 位置;
        矩形.sizeDelta = 尺寸;
    }

    // 只设 单点锚 + pivot（不动 位置/尺寸——位置 由 调用方 单独 算 的场景）
    public static void 设锚点(RectTransform 矩形, Vector2 锚点, Vector2 pivot)
    {
        矩形.anchorMin = 锚点;
        矩形.anchorMax = 锚点;
        矩形.pivot = pivot;
    }
}
