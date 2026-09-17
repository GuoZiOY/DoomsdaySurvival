using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// 生成角色面板预制体（Editor 菜单）：把 `角色面板.重建布局()` 建出来的整棵节点树**烘成一个 .prefab 资产**。
//
// 为什么要有这一批：`角色面板` 现在是自己建布局（`Awake` 里建树），用户在场景里什么都不用搭；
//   但"能玩"不等于"有个资产"—— 预制体是给人接手改的（改完拖回引用位即可），也是这一批要交的东西。
//
// 为什么必须在**临时 Canvas** 下生成（不是图省事，是正确性前提）：
//   ① 板子尺寸按"父矩形宽高"算（`角色面板.顶层尺寸变化` → `板子尺寸`）：面板物体不挂在 Canvas 下就没有屏幕尺寸，
//      板子只能退回上限 1180×760，经验格也跟着按那个宽度铺；
//   ② 更要命的是 TMP：Tab 行与下划线、栏目标题下划线、行名的横坐标**全都要读 `preferredWidth`**，
//      而 `preferredWidth` 要 TMP 的组件真的初始化过才算得出来 —— 没有 Canvas 量出来是 0，
//      摆出来的 Tab 全挤在左边、下划线宽度退回兜底值（"看着像零宽"就是这么来的）。
//   临时 Canvas 只活在这次方法调用里：生成完立刻 `DestroyImmediate` 整棵临时树，
//   **不往场景里留任何东西、也不 MarkSceneDirty / 不保存场景**（本脚本只产出预制体资产）。
//
// 本脚本只认 `角色面板.重建布局()` 这一个入口（无参、public、编辑器非运行态可调、幂等），
//   **不去碰**角色面板的字段与口径 —— 皮肤/摆位永远由 `角色面板` 自己说了算，这里只负责"给它一个能测量的舞台 + 存盘"。
// ============================================================
public static class 生成角色面板预制体
{
    private const string 预制体路径 = "Assets/Resources/Prefab/角色面板.prefab";

    // 临时画布的参考分辨率：1920×1080。取这个数是因为它让"按屏算出来的板子"正好落在皮肤的上限档
    //（1920×0.84 = 1613 → min(…, 1180) = 1180；1080×0.88 = 950 → min(…, 760) = 760），
    // 也就是 1180×760 —— 与"编辑器里量不到屏宽"时的兜底尺寸一致，生成出来的板子在哪条路径上都是同一个大小。
    private static readonly Vector2 参考分辨率 = new Vector2(1920f, 1080f);

    // 菜单归到项目自己的 `末日/` 根下（与 末日/房间/… 同一套），不要放 Unity 默认的 `工具/`。
    [MenuItem("末日/角色/一键生成面板预制体", false, 10)]
    public static void 生成()
    {
        bool 已有 = AssetDatabase.LoadAssetAtPath<GameObject>(预制体路径) != null;

        // 临时舞台：Canvas 撑起"屏幕"，面板物体挂在它下面（顺序不能反 —— 见文件头）。
        var 临时画布物体 = new GameObject("角色面板预制体_临时画布", typeof(Canvas), typeof(CanvasScaler));
        try
        {
            var 画布 = 临时画布物体.GetComponent<Canvas>();
            画布.renderMode = RenderMode.ScreenSpaceOverlay;   // 量尺寸不需要相机，Overlay 最省事
            var 缩放 = 临时画布物体.GetComponent<CanvasScaler>();
            缩放.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;   // 别让画布缩放参与进"文字宽"的计算
            缩放.referenceResolution = 参考分辨率;                          // 屏幕尺寸的来源（见 参考分辨率）

            // 角色面板物体：满屏的根矩形（与运行时 `面板管理器` 建/摆面板的前提一致）。
            var 面板物体 = new GameObject("角色面板", typeof(RectTransform), typeof(角色面板));
            面板物体.transform.SetParent(临时画布物体.transform, false);   // ★ 先挂进 Canvas，再重建布局
            var 根矩形 = (RectTransform)面板物体.transform;
            根矩形.anchorMin = Vector2.zero;
            根矩形.anchorMax = Vector2.one;
            根矩形.pivot = new Vector2(0.5f, 0.5f);
            根矩形.offsetMin = Vector2.zero;
            根矩形.offsetMax = Vector2.zero;

            量屏幕();

            // 本组件建树 + 回填 12 个引用位 + 强制算一遍文字宽（编辑器里没有第二帧布局回调）。
            面板物体.GetComponent<角色面板>().重建布局();
            量屏幕();   // 刚建出来的节点参与了这一帧布局 → 再量一次，让经验格/内容行拿到的宽高是这一棵树真实算出来的

            PrefabUtility.SaveAsPrefabAsset(面板物体, 预制体路径);
            AssetDatabase.Refresh();

            if (已有) Debug.Log($"[角色面板] 预制体已存在，本次**覆盖**：{预制体路径}");
            Debug.Log($"[角色面板] 已生成预制体：{预制体路径}\n" +
                      "  用法：把它拖到场景里 面板管理器 的「角色」引用位上（拖进去的实例自己带整棵节点树，运行时不用再接任何东西）。");
        }
        finally
        {
            // 临时树一个都不留（编辑器里 `Destroy` 要等帧末，这里必须 `DestroyImmediate`）。
            销毁临时对象(临时画布物体);
        }
    }

    // 让 Canvas 立刻算出根矩形的宽高：不主动算的话 `Rect` 还是上一帧的值（新建画布时就是 0），
    //   板子尺寸与经验格条宽会按 0 走兜底分支。
    // 为什么加 `!Application.isPlaying` 守卫：这个菜单项是**在编辑器里、非运行态**用的工具；
    //   万一有人在 Play 里点了它，`ForceUpdateCanvases` 会把当帧的真实 UI 也算一遍 → 白改一帧画面。
    private static void 量屏幕()
    {
        if (!Application.isPlaying) Canvas.ForceUpdateCanvases();
    }

    private static void 销毁临时对象(GameObject 物体)
    {
        if (物体 != null) Object.DestroyImmediate(物体);
    }
}
