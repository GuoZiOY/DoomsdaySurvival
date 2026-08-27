using TMPro;
using UnityEditor;
using UnityEngine;

// UI 缩放工具：参考分辨率 1080p → 1440p（2K）时，把选中 UI 树的 RectTransform 与 TMP 字体 统一 ×4/3。
// 用法：场景里选中 Canvas（或任意父节点）→ 菜单 [工具/UI 缩放 ×4/3 (1K→2K)]（含字体）。
// 若已跑过 RectTransform 缩放：用 [工具/仅字体 ×4/3] 只缩 TMP 字号，避免 RectTransform 双倍。
// 单点锚（anchorMin==anchorMax）：anchoredPosition 与 sizeDelta ×倍率；拉伸锚：offsetMin/offsetMax ×倍率。
// 倍率可在工具里改（4f/3f = 1080→1440；1440→1080 用 3f/4f）。
public static class UI缩放工具
{
    private const float 倍率 = 4f / 3f;   // 1080p → 1440p；反方向用 3f/4f

    [MenuItem("工具/UI 缩放 ×4/3 (1K→2K)")]
    public static void 缩放选中()
    {
        int 计数 = 0, 字体数 = 0;
        foreach (var 选中 in 选中物体())
        {
            if (选中 == null) continue;
            foreach (var rt in 选中.GetComponentsInChildren<RectTransform>(true))
                if (缩放一个(rt)) 计数++;
            foreach (var 文本 in 选中.GetComponentsInChildren<TMP_Text>(true))
            {
                Undo.RecordObject(文本, "UI 缩放");
                文本.fontSize *= 倍率;
                EditorUtility.SetDirty(文本);
                字体数++;
            }
        }
        EditorUtility.DisplayDialog("UI 缩放", $"已缩放 {计数} 个 RectTransform、{字体数} 个 TMP 文本（×{倍率:0.###}）。", "好");
    }

    [MenuItem("工具/仅字体 ×4/3")]
    public static void 仅缩放字体()
    {
        int 字体数 = 0;
        foreach (var 选中 in 选中物体())
        {
            if (选中 == null) continue;
            foreach (var 文本 in 选中.GetComponentsInChildren<TMP_Text>(true))
            {
                Undo.RecordObject(文本, "UI 字体缩放");
                文本.fontSize *= 倍率;
                EditorUtility.SetDirty(文本);
                字体数++;
            }
        }
        EditorUtility.DisplayDialog("UI 缩放", $"已缩放 {字体数} 个 TMP 文本字号（×{倍率:0.###}）。", "好");
    }

    private static GameObject[] 选中物体()
    {
        var 物体们 = Selection.gameObjects;
        if (物体们 == null || 物体们.Length == 0)
        {
            EditorUtility.DisplayDialog("UI 缩放", "请先在场景里选中要缩放的父节点（如 Canvas）。", "好");
            return new GameObject[0];
        }
        return 物体们;
    }

    // 返回 true = 实际缩放
    private static bool 缩放一个(RectTransform rt)
    {
        if (rt == null) return false;
        Undo.RecordObject(rt, "UI 缩放");
        bool 拉伸 = rt.anchorMin != rt.anchorMax;
        if (拉伸)
        {
            rt.offsetMin *= 倍率;
            rt.offsetMax *= 倍率;
        }
        else
        {
            rt.sizeDelta *= 倍率;
            rt.anchoredPosition *= 倍率;
        }
        EditorUtility.SetDirty(rt);
        return true;
    }
}
