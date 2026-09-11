using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// 房间面板搭建（Editor 菜单）：一键在场景里搭出房间层的接线，并接好全部引用位。
//   对象：房间面板（含深色背景）
//         ├─ 信息条（TMP_Text，顶部一条）
//         ├─ 提示行（TMP_Text，底部一条）
//         └─ 网格容器（挂 房间网格面板 + 房间图层）
//   引用位：房间面板.网格面板 + 房间网格面板.网格容器 + 面板管理器.房间（信息条/提示行 已取消：走 HUD 地点位与日志）
//
// 为什么不直接改 SampleScene.unity：场景 984 KB / 308 个对象，而且 Unity 正开着它——
// 手改 YAML 既容易出错，也会被 Unity 回写覆盖。交给 Editor 脚本用 Unity 自己的序列化来建与接：
// 安全、可 Undo、可重复执行（幂等：已有对象就补全，不重复创建）。
// 所有接线一律走 SerializedObject（面板字段是 private [SerializeField]，本来也只该由 Inspector 接）。
// ============================================================
public static class 房间面板搭建
{
    private const string 房间面板名 = "房间面板";
    private const string 网格容器名 = "网格容器";
    private static readonly Vector2 网格尺寸 = new Vector2(1440f, 900f);

    [MenuItem("末日/房间/一键搭建面板（场景接线）", false, 10)]
    public static void 搭建()
    {
        var 管理器 = Object.FindAnyObjectByType<面板管理器>(FindObjectsInactive.Include);
        if (管理器 == null)
        {
            EditorUtility.DisplayDialog("房间面板搭建", "场景里找不到 面板管理器（UI管理器）——先确认它在场景里。", "好");
            return;
        }

        var 面板 = Object.FindAnyObjectByType<房间面板>(FindObjectsInactive.Include);
        bool 新建 = 面板 == null;
        if (新建)
        {
            var 面板物体 = 新建UI(房间面板名, 找面板父级(管理器));
            拉满((RectTransform)面板物体.transform);
            var 背景 = 面板物体.AddComponent<Image>();
            背景.color = 网格面板配色.底盘色;   // 与网格底盘同色（全屏面板的底）
            背景.raycastTarget = true;          // 全屏面板：挡住下层点击
            面板 = 面板物体.AddComponent<房间面板>();
            Undo.RegisterCreatedObjectUndo(面板物体, "搭建房间面板");
        }

        建或补子物体(面板);
        接引用(面板, 管理器);

        面板.gameObject.SetActive(false);   // 与其它面板一致：由 面板管理器 显示时激活
        EditorSceneManager.MarkSceneDirty(面板.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = 面板.gameObject;
        EditorGUIUtility.PingObject(面板.gameObject);
        Debug.Log($"{(新建 ? "[房间] 已新建并接线" : "[房间] 已补全接线")}：{房间面板名}" +
                  "（引用位：网格面板 + 面板管理器.房间；房间状态走 HUD 地点位、提示走日志）｜场景已保存。回 Play → 测试 → 房间（第 1 刀）。");
    }

    [MenuItem("末日/房间/检查接线（列出未接的引用位）", false, 11)]
    public static void 检查()
    {
        var sb = new StringBuilder("[房间] 接线检查：\n");
        int 缺 = 0;
        var 面板 = Object.FindAnyObjectByType<房间面板>(FindObjectsInactive.Include);
        var 管理器 = Object.FindAnyObjectByType<面板管理器>(FindObjectsInactive.Include);

        if (面板 == null)
        {
            sb.AppendLine("  ✗ 场景里没有 房间面板（跑一次「一键搭建面板」即可）");
            缺++;
        }
        else
        {
            缺 += 查引用(面板, "网格面板", sb);
            缺 += 查引用(面板, "信息条", sb);
            缺 += 查引用(面板, "提示行", sb);
            var 网格 = 面板.GetComponentInChildren<房间网格面板>(true);
            if (网格 == null) { sb.AppendLine("  ✗ 房间面板下没有 房间网格面板"); 缺++; }
            else
            {
                缺 += 查引用(网格, "网格容器", sb);
                if (网格.GetComponent<房间图层>() == null) { sb.AppendLine("  ✗ 房间网格面板 同物体缺 房间图层"); 缺++; }
            }
        }

        if (管理器 == null) { sb.AppendLine("  ✗ 场景里没有 面板管理器"); 缺++; }
        else 缺 += 查引用(管理器, "房间", sb);

        sb.Append(缺 == 0 ? "  ✓ 全部引用位已接好" : $"  共 {缺} 处要处理");
        Debug.Log(sb.ToString());
    }

    // ================= 建对象 =================

    private static void 建或补子物体(房间面板 面板)
    {
        var 根 = 面板.transform;

        if (读引用(面板, "网格面板") == null)
        {
            var 网格 = 根.GetComponentInChildren<房间网格面板>(true);
            if (网格 == null)
            {
                var 网格物体 = 新建UI(网格容器名, 根);
                var 网格矩形 = (RectTransform)网格物体.transform;
                网格矩形.anchorMin = 网格矩形.anchorMax = new Vector2(0.5f, 0.5f);
                网格矩形.pivot = new Vector2(0.5f, 0.5f);
                网格矩形.anchoredPosition = new Vector2(0f, 8f);
                网格矩形.sizeDelta = 网格尺寸;
                网格 = 网格物体.AddComponent<房间网格面板>();
                网格物体.AddComponent<房间图层>();
                Undo.RegisterCreatedObjectUndo(网格物体, "搭建房间网格");
                // 网格容器 引用位（基类 protected 字段，走序列化写）
                写引用(new SerializedObject(网格), "网格容器", 网格矩形);
            }
            写引用(new SerializedObject(面板), "网格面板", 网格);
        }
    }

    private static void 接引用(房间面板 面板, 面板管理器 管理器)
    {
        Undo.RecordObject(面板, "接线 房间面板");
        var 面板串 = new SerializedObject(面板);
        var 网格 = 面板串.FindProperty("网格面板")?.objectReferenceValue as 房间网格面板;
        面板串.ApplyModifiedProperties();
        if (网格 != null)
        {
            Undo.RecordObject(网格, "接线 房间网格面板");
            写引用(new SerializedObject(网格), "网格容器", 网格.transform as RectTransform);
        }

        Undo.RecordObject(管理器, "接线 面板管理器.房间");
        var 管理器串 = new SerializedObject(管理器);
        写引用(管理器串, "房间", 面板);
        管理器串.ApplyModifiedProperties();
        EditorUtility.SetDirty(面板);
        EditorUtility.SetDirty(管理器);
    }

    // ================= 引用位读写（一律走 SerializedObject） =================

    private static Object 读引用(Object 目标, string 字段名)
    {
        if (目标 == null) return null;
        var 属性 = new SerializedObject(目标).FindProperty(字段名);
        return 属性?.objectReferenceValue;
    }

    private static void 写引用(SerializedObject 串, string 字段名, Object 值)
    {
        var 属性 = 串?.FindProperty(字段名);
        if (属性 == null)
        {
            Debug.LogWarning($"[房间] 找不到序列化字段 {串?.targetObject?.GetType().Name}.{字段名}（字段改名了？）");
            return;
        }
        属性.objectReferenceValue = 值;
        串.ApplyModifiedProperties();
    }

    private static int 查引用(Object 目标, string 字段名, StringBuilder sb)
    {
        if (读引用(目标, 字段名) != null) return 0;
        sb.AppendLine($"  ✗ {目标.GetType().Name}.{字段名} 未接线");
        return 1;
    }

    // ================= 层级与几何 =================

    private static Transform 找面板父级(面板管理器 管理器)
    {
        Component 参考 = Object.FindAnyObjectByType<房间面板>(FindObjectsInactive.Include);
        if (参考 == null) 参考 = Object.FindAnyObjectByType<安全屋面板>(FindObjectsInactive.Include);
        if (参考 != null && 参考.transform.parent != null && 参考.transform.parent.GetComponent<Canvas>() != null)
            return 参考.transform.parent;
        if (管理器.transform.parent != null && 管理器.transform.parent.GetComponent<Canvas>() != null)
            return 管理器.transform.parent;
        var 画布 = 管理器.GetComponentInParent<Canvas>();
        return 画布 != null ? 画布.transform : 管理器.transform;
    }

    private static GameObject 新建UI(string 名, Transform 父)
    {
        var 物 = new GameObject(名, typeof(RectTransform));
        物.transform.SetParent(父, false);
        return 物;
    }

    private static TMP_Text 新建文本(string 名, Transform 父, float 字号, TextAlignmentOptions 对齐)
    {
        var 物 = new GameObject(名, typeof(RectTransform), typeof(TextMeshProUGUI));
        物.transform.SetParent(父, false);
        var 文本 = 物.GetComponent<TextMeshProUGUI>();
        文本.font = 找场景字体();   // 复用场景里已入库的中文 SDF 字体
        文本.fontSize = 字号;
        文本.color = 游戏主题.文字;
        文本.alignment = 对齐;
        文本.raycastTarget = false;
        文本.text = 名;
        return 文本;
    }

    private static TMP_FontAsset 找场景字体()
    {
        foreach (var 文本 in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (文本 != null && 文本.font != null) return 文本.font;
        return null;
    }

    private static void 拉满(RectTransform 矩形)
    {
        矩形.anchorMin = Vector2.zero;
        矩形.anchorMax = Vector2.one;
        矩形.pivot = new Vector2(0.5f, 0.5f);
        矩形.offsetMin = Vector2.zero;
        矩形.offsetMax = Vector2.zero;
    }

    private static void 顶到底(RectTransform 矩形, float 高, float 边距)
    {
        矩形.anchorMin = new Vector2(0f, 1f);
        矩形.anchorMax = new Vector2(1f, 1f);
        矩形.pivot = new Vector2(0.5f, 1f);
        矩形.offsetMin = new Vector2(边距, -高 - 边距);
        矩形.offsetMax = new Vector2(-边距, -边距);
    }

    private static void 底到顶(RectTransform 矩形, float 高, float 边距)
    {
        矩形.anchorMin = new Vector2(0f, 0f);
        矩形.anchorMax = new Vector2(1f, 0f);
        矩形.pivot = new Vector2(0.5f, 0f);
        矩形.offsetMin = new Vector2(边距, 边距);
        矩形.offsetMax = new Vector2(-边距, 高 + 边距);
    }
}
