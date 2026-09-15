using UnityEngine;
using UnityEngine.UI;

// 背景模糊层：塔科夫式 UI 背景模糊（独立 功能组件，可被 任意 面板/系统 复用）。
// 挂 Canvas 根节点（一个 实例），Inspector 只需 拖：
//   背景图：背景 Image（模糊 来源，全屏 拉伸）
//   模糊强度：shader _BlurSize（塔科夫感 5~8）
//   分辨率倍率：抓屏 分辨率（0.5 = 半分辨率：更糊更快；1 = 全分辨率：更清晰）
// 模糊显示 的 全屏 RawImage 由 脚本 动态 生成（生成 到 本节点 下、根节点 的 最上面 = SetAsLastSibling），
// 无需 手动 搭建。使用：打开 面板 时 背景模糊层.显示模糊()；关闭 时 隐藏模糊()。多 面板 共享（幂等）。
public sealed class 背景模糊层 : MonoBehaviour
{
    public static 背景模糊层 实例;   // 静态 缓存（调用 时 懒 查找，含 inactive，不 依赖 Awake）

    [SerializeField] private Image 背景图;                          // Inspector：背景 Image（模糊 来源，全屏 拉伸）
    [SerializeField, Range(0f, 20f)] private float 模糊强度 = 6f;   // 模糊 强度（shader _BlurSize；塔科夫感 5~8）
    [SerializeField, Range(0.1f, 1f)] private float 分辨率倍率 = 0.5f;   // 抓屏 分辨率（0.5 = 半分辨率：更糊更快；1 = 全分辨率：更清晰）

    private GameObject 模糊层物体;      // 动态 生成 的 全屏 RawImage（缓存 复用）
    private RawImage 模糊显示;
    private RenderTexture 模糊RT;
    private static Material 模糊材质;
    private bool 已显示;

    // ★ 刀66：**引用计数**。这张模糊层是**共享**的 —— 角色创建面板 / 持有面板 / 安全屋面板 三方都用它。
    //   原来没有计数：`显示/隐藏` 是"谁最后调谁赢"，于是 A 面板关闭会把 B 面板正在用的模糊一起关掉
    //   （表现：从 持有面板 切到 安全屋 时模糊会闪一下 / 该糊的时候不糊）。
    //   现在：显示 +1、隐藏 -1，**只在 0↔1 的边界**真正开合。
    private int 引用数;

    void OnDestroy()
    {
        if (实例 == this) 实例 = null;
        if (模糊层物体 != null) Destroy(模糊层物体);
        释放RT();
    }

    // 显示 模糊：背景 sprite → 半分辨率 RT → 模糊 → 全屏 RawImage（动态 生成 一次，之后 复用）。幂等。
    public void 显示()
    {
        if (已显示) return;
        if (背景图 == null || 背景图.sprite == null)
        {
            Debug.LogWarning("[背景模糊层] 未配置「背景图」（Inspector 拖 背景 Image，且 需 设置 sprite）");
            return;
        }
        var 材质 = 获取模糊材质();
        if (材质 == null) return;
        // ① 确保 模糊层 物体 存在 且 激活（首次 动态 生成：挂 本节点 下，SetAsFirstSibling = 最先 渲染 = 根节点 渲染 最底层）
        if (模糊层物体 == null)
        {
            模糊层物体 = new GameObject("背景模糊层", typeof(RectTransform), typeof(RawImage));
            模糊层物体.transform.SetParent(transform, false);
            模糊层物体.transform.SetAsFirstSibling();   // 最先 渲染：垫底，不 遮挡 任何 UI
            模糊显示 = 模糊层物体.GetComponent<RawImage>();
            模糊显示.raycastTarget = false;   // 不 拦截 点击/拖拽
            var 矩形 = 模糊层物体.GetComponent<RectTransform>();
            矩形.anchorMin = Vector2.zero;
            矩形.anchorMax = Vector2.one;
            矩形.offsetMin = Vector2.zero;
            矩形.offsetMax = Vector2.zero;
        }
        模糊层物体.SetActive(true);
        // ② 背景 sprite → RT（按 分辨率倍率 降采样：本身 带 模糊感 + 性能）
        int 宽 = Mathf.Max(2, Mathf.RoundToInt(Screen.width * 分辨率倍率));
        int 高 = Mathf.Max(2, Mathf.RoundToInt(Screen.height * 分辨率倍率));
        var 抓屏rt = RenderTexture.GetTemporary(宽, 高, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(背景图.sprite.texture, 抓屏rt);   // sprite 整纹理 → RT（与 背景 Image 全屏拉伸 一致）
        // ③ 模糊
        材质.SetFloat("_BlurSize", 模糊强度);
        var 模糊rt = RenderTexture.GetTemporary(宽, 高, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(抓屏rt, 模糊rt, 材质);
        RenderTexture.ReleaseTemporary(抓屏rt);
        // ④ 赋给 全屏 RawImage
        模糊显示.texture = 模糊rt;
        模糊RT = 模糊rt;
        已显示 = true;
        Debug.Log($"[背景模糊层] 已显示（尺寸 {宽}x{高}，模糊强度 {模糊强度}）");
    }

    // 隐藏/关闭 模糊 显示
    public void 隐藏()
    {
        已显示 = false;
        if (模糊层物体 != null) 模糊层物体.SetActive(false);
        释放RT();
    }

    // —— 静态 便捷（面板 打开/关闭 调用；未 挂 组件 时 静默 跳过） ——
    // ★ 带引用计数：多个面板同时想糊时，最后一个关掉才真正收起（见 引用数 的注释）。
    public static void 显示模糊()
    {
        var 层 = 查找实例();
        if (层 == null) return;
        层.引用数++;
        if (层.引用数 == 1) 层.显示();
    }

    public static void 隐藏模糊()
    {
        var 层 = 查找实例();
        if (层 == null) return;
        层.引用数--;
        if (层.引用数 <= 0) { 层.引用数 = 0; 层.隐藏(); }
    }

    // ★ 整盘重置（回主菜单 / 新游戏 / 读档 时用）：把计数清零并立刻收起。
    //   为什么必须有：计数是"面板配对调用"维持的，一旦有一次没配上（面板被强隐藏、场景重载、
    //   异常路径跳过了 隐藏面板），计数就永久 >0 → 之后每次开面板都糊、关不掉。这是防漏底。
    public static void 强制清零()
    {
        var 层 = 查找实例();
        if (层 == null) return;
        层.引用数 = 0;
        层.隐藏();
    }

    private static 背景模糊层 查找实例()
    {
        if (实例 == null)
            实例 = FindFirstObjectByType<背景模糊层>(FindObjectsInactive.Include);
        return 实例;
    }

    private void 释放RT()
    {
        if (模糊RT != null) { RenderTexture.ReleaseTemporary(模糊RT); 模糊RT = null; }
    }

    private static Material 获取模糊材质()
    {
        if (模糊材质 == null)
        {
            // 打包后 Shader.Find 找不到未引用 shader（会被裁剪）→ 优先 Resources.Load（Resources 内必定进包），
            // 失败再回退 Shader.Find（编辑器/未裁剪环境）
            var shader = Resources.Load<Shader>("Shaders/UI背景模糊");
            if (shader == null) shader = Shader.Find("UI/背景模糊");
            if (shader == null)
            {
                Debug.LogError("[背景模糊层] 找不到 Shader \"UI/背景模糊\"（检查 Assets/Resources/Shaders/UI背景模糊.shader 是否编译成功）");
                return null;
            }
            模糊材质 = new Material(shader);
        }
        return 模糊材质;
    }
}
