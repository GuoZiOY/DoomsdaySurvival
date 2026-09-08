using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 战斗轨道：战斗沙盒 的 节点式轨道 渲染（轻量，不用 网格面板基类）。
// 双方抽象为"点"（视觉 = 圆棋子），沿横向轨道节点（0 ~ N-1）前后移动。
// 单位 = Resources/Prefab/战斗单位.prefab（手动搭建，根挂 战斗单位视图）：
//   每帧 只改 根.x（运动跟随）与 信息卡.y（阵营 上2/下3 槽位）；圆色/fill/可选高亮 由 战斗单位视图.刷新外观 负责。
// 交互：点 信息卡 = 选中 对应 单位（战斗单位视图.点卡）；点 重叠 圆 = 同节点 单位 循环 切换（点圆）；点 空白 = 取消。
public sealed class 战斗轨道 : MonoBehaviour
{
    private const string 单位预制体路径 = "Prefab/战斗单位";   // Resources 路径（预制体 手动搭建，沿用 浮动面板 惯例）

    [SerializeField] private RectTransform 轨道容器;   // 轨道区域（线/节点/单位根 动态生成到这里）
    [SerializeField] private float 节点间距 = 130f;     // 相邻 节点 中心 像素 距离（≥ 节点直径，防 圆 重叠）
    [SerializeField] private float 节点直径 = 100f;     // 节点 圆 直径（站位 标记）
    [SerializeField] private float 直线宽度 = 3f;       // 轨道 直线 宽度
    [SerializeField] private float 棋子直径 = 70f;      // 身体 圆 基准 直径（单位，叠 在 节点 上；绑定 时 × 身形 加宽）
    [SerializeField] private float 信息卡间距 = 34f;    // 同阵营 信息卡 垂直 堆叠 间距
    [SerializeField] private float 信息层距 = 22f;      // 信息卡 与 棋子（身体圆）边缘 的 间隔
    [SerializeField] private float 信息卡侧偏 = 100f;   // 信息卡 左右 错开（避 双方 卡 重叠）：我方 → 左移 侧偏，敌方 → 右移 侧偏；0 = 不偏移

    private readonly Dictionary<战斗单位, 战斗单位视图> 单位表 = new Dictionary<战斗单位, 战斗单位视图>();
    private readonly Dictionary<int, int> 节点点击计数 = new Dictionary<int, int>();   // 重叠 圆 点击 循环 计数
    private RectTransform 空白层;
    private int 节点数;
    private static 战斗单位视图 单位预制体缓存;
    private static bool 预制体缺失已警告;

    private 战斗沙盒面板 外壳 => GetComponentInParent<战斗沙盒面板>();

    // 圆形 白 贴图（运行时 生成，零资源依赖——内置 UI/Skin/Knob 在 2022 不存在；仅 节点圆/装饰 用，单位圆 用 预制体 素材）
    private static Sprite 圆形精灵;
    private static Sprite 圆形()
    {
        if (圆形精灵 != null) return 圆形精灵;
        const int 尺寸 = 64;
        var 纹理 = new Texture2D(尺寸, 尺寸, TextureFormat.RGBA32, false);
        float 中心 = (尺寸 - 1) / 2f, 半径 = 中心 - 1f;
        for (int y = 0; y < 尺寸; y++)
            for (int x = 0; x < 尺寸; x++)
            {
                float dx = x - 中心, dy = y - 中心;
                纹理.SetPixel(x, y, Mathf.Sqrt(dx * dx + dy * dy) <= 半径 ? Color.white : Color.clear);
            }
        纹理.Apply();
        圆形精灵 = Sprite.Create(纹理, new Rect(0, 0, 尺寸, 尺寸), new Vector2(0.5f, 0.5f), 100f);
        return 圆形精灵;
    }

    private static readonly Color 节点色 = new Color(0.16f, 0.16f, 0.20f, 0.8f);

    // 布阵：清旧 → 生成 轨道线 + 节点圆 + 空白点击层；节点数 = 轨道宽度
    public void 布阵(int 数量)
    {
        清空();
        节点数 = Mathf.Max(2, 数量);
        if (轨道容器 != null)
            轨道容器.sizeDelta = new Vector2((节点数 - 1) * 节点间距 + 节点直径,
                节点直径 + 信息层距 * 2f + 信息卡间距 * 6f + 40f);   // 上 2 + 下 3 信息卡 空间
        创建轨道();
        确保空白点击层();
    }

    public void 清空()
    {
        foreach (var kv in 单位表) if (kv.Value != null) Destroy(kv.Value.gameObject);
        单位表.Clear();
        节点点击计数.Clear();
    }

    // 每帧：同步 单位（移动 父节点；信息卡 相对 y 按 阵营 名额；外观 由 战斗单位视图 刷新）
    public void 同步单位(List<战斗单位> 单位组)
    {
        if (单位组 == null) return;
        // 移除 离场 单位
        if (单位表.Count != 单位组.Count)
        {
            var 移除 = new List<战斗单位>();
            foreach (var kv in 单位表) if (!单位组.Contains(kv.Key)) 移除.Add(kv.Key);
            foreach (var 单位 in 移除)
            {
                if (单位表[单位] != null) Destroy(单位表[单位].gameObject);
                单位表.Remove(单位);
            }
        }
                // 阵营 名额 分配：友方/敌方 各 前 2 → 圆 上方（优先），其余 → 下方（y 为 信息卡 相对 根 的 local y）
                foreach (var 阵营组 in new[] { 我方组(单位组), 敌方组(单位组) })
                {
                    for (int i = 0; i < 阵营组.Count; i++)
                    {
                        var 单位 = 阵营组[i];
                        if (!单位表.TryGetValue(单位, out var 视图))
                        {
                            视图 = 生成单位视图(单位);
                            if (视图 == null) continue;
                            单位表[单位] = 视图;
                        }
                        if (!单位.存活) { 视图.gameObject.SetActive(false); continue; }
                        视图.gameObject.SetActive(true);
                        var 根 = (RectTransform)视图.transform;
                        根.anchoredPosition = new Vector2(单位.列 * 节点间距, 0f);   // 移动 根 = 圆 + 信息卡 一起 跟随
                        bool 上方 = i < 2;                                          // 上面 优先：每阵营 ≤2 个 都 放 圆 上方
                        int 层序号 = 上方 ? i : i - 2;
                        float 相对y = 上方
                            ? +(棋子直径 / 2f + 信息层距 + 层序号 * 信息卡间距)     // 上方（正 y = 上）：棋子边缘 + 间隔 + 堆叠
                            : -(棋子直径 / 2f + 信息层距 + 层序号 * 信息卡间距);    // 下方
                        if (视图.卡变换 != null) 视图.卡变换.localPosition = new Vector2(
                            单位.是否我方 ? -信息卡侧偏 : 信息卡侧偏, 相对y);   // 我方 左移 / 敌方 右移（避 双方 卡 重叠）
                        bool 可选 = 外壳 != null && 外壳.是可选目标(单位);
                        视图.刷新外观(可选);
                    }
                }
    }

    private static List<战斗单位> 我方组(List<战斗单位> 单位组)
    {
        var 列表 = new List<战斗单位>();
        foreach (var u in 单位组) if (u != null && u.是否我方) 列表.Add(u);
        return 列表;
    }

    private static List<战斗单位> 敌方组(List<战斗单位> 单位组)
    {
        var 列表 = new List<战斗单位>();
        foreach (var u in 单位组) if (u != null && !u.是否我方) 列表.Add(u);
        return 列表;
    }

    // 单位预制体（懒加载一次；缺失 → 报错 并 跳过生成，搭好 Resources/Prefab/战斗单位.prefab 即恢复）
    private static 战斗单位视图 加载单位预制体()
    {
        if (单位预制体缓存 == null && !预制体缺失已警告)
        {
            单位预制体缓存 = Resources.Load<战斗单位视图>(单位预制体路径);
            if (单位预制体缓存 == null)
            {
                预制体缺失已警告 = true;
                Debug.LogError($"[战斗轨道] 找不到 Resources/Prefab/战斗单位.prefab（根需挂 战斗单位视图）——单位不显示。");
            }
        }
        return 单位预制体缓存;
    }

    // 生成 单位 视图：Instantiate 预制体 → 根 归一（锚 左中 / pivot 中 / 尺寸 0，子 自动 居中）→ 绑定
    private 战斗单位视图 生成单位视图(战斗单位 单位)
    {
        if (轨道容器 == null) return null;
        var 预制 = 加载单位预制体();
        if (预制 == null) return null;
        var 视图 = Instantiate(预制, 轨道容器, false);
        var 根 = (RectTransform)视图.transform;
        根.anchorMin = new Vector2(0f, 0.5f);
        根.anchorMax = new Vector2(0f, 0.5f);
        根.pivot = new Vector2(0.5f, 0.5f);
        根.sizeDelta = Vector2.zero;   // 根 不 参与 布局，子 决定 视觉（相对 根 中心 摆放）
        视图.绑定(单位, 棋子直径);
        return 视图;
    }

    // 轨道视觉：一条 直线（宽度 = 直线宽度，贯穿 节点 中心）+ 线上 等距 节点 圆（直径 = 节点直径）
    private void 创建轨道()
    {
        if (轨道容器 == null) return;
        float 轨道宽 = (节点数 - 1) * 节点间距;
        var 线 = new GameObject("轨道线", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        线.SetParent(轨道容器, false);
        线.anchorMin = new Vector2(0f, 0.5f); 线.anchorMax = new Vector2(0f, 0.5f);
        线.pivot = new Vector2(0.5f, 0.5f);
        线.anchoredPosition = new Vector2(轨道宽 / 2f, 0f);
        线.sizeDelta = new Vector2(轨道宽, 直线宽度);
        线.GetComponent<Image>().color = 节点色;
        for (int i = 0; i < 节点数; i++)
        {
            var 点 = new GameObject($"节点_{i}", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            点.SetParent(轨道容器, false);
            点.anchorMin = new Vector2(0f, 0.5f); 点.anchorMax = new Vector2(0f, 0.5f);
            点.pivot = new Vector2(0.5f, 0.5f);
            点.anchoredPosition = new Vector2(i * 节点间距, 0f);
            点.sizeDelta = new Vector2(节点直径, 节点直径);
            var 图 = 点.GetComponent<Image>();
            图.sprite = 圆形(); 图.color = 节点色;
            图.raycastTarget = false;
        }
    }

    // 空白点击层：点击 非 棋子/信息卡 区域 → 取消目标选择
    private void 确保空白点击层()
    {
        if (空白层 != null || 轨道容器 == null) return;
        var 物体 = new GameObject("空白点击", typeof(RectTransform), typeof(Image), typeof(Button));
        物体.transform.SetParent(轨道容器, false);
        物体.transform.SetAsFirstSibling();
        物体.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        物体.GetComponent<Button>().onClick.AddListener(() => 外壳?.点击空白());
        var 矩 = 物体.GetComponent<RectTransform>();
        矩.anchorMin = Vector2.zero; 矩.anchorMax = Vector2.one;
        矩.offsetMin = Vector2.zero; 矩.offsetMax = Vector2.zero;
        空白层 = 矩;
    }

    // —— 由 战斗单位视图 Button 转发 ——

    // 点 信息卡 = 精确 选中
    public void 处理卡点击(战斗单位 单位)
    {
        if (外壳 == null || 单位 == null || !单位.存活) return;
        外壳.点击单位(单位);
    }

    // 点 身体 圆：同节点 多单位 → 循环 切换 选中；单个 → 直接
    public void 处理圆点击(战斗单位 单位)
    {
        if (外壳 == null || 单位 == null || !单位.存活) return;
        var 同节点 = new List<战斗单位>();
        foreach (var 敌 in 单位表.Keys)
            if (敌.存活 && 敌.列 == 单位.列) 同节点.Add(敌);
        if (同节点.Count <= 1) { 外壳.点击单位(单位); return; }
        节点点击计数.TryGetValue(单位.列, out int n);
        节点点击计数[单位.列] = n + 1;
        外壳.点击单位(同节点[n % 同节点.Count]);
    }

    // ===== 战斗反馈（伤害/治疗 飘字 + 受击 相机 抖动；面板 订阅 事件 调用） =====

    // 飘字：挂在 轨道容器（与单位同坐标），列 上 浮起 淡出；数字 用 ASCII（缺省 TMP 字体 保证 显示）
    public void 显示飘字(战斗单位 单位, string 文本, Color 色, float 字号 = 20f)
    {
        if (轨道容器 == null || 单位 == null || !单位表.TryGetValue(单位, out var 视图) || 视图 == null || string.IsNullOrEmpty(文本)) return;
        var 物体 = new GameObject("飘字", typeof(RectTransform), typeof(TextMeshProUGUI));
        物体.transform.SetParent(轨道容器, false);
        var 矩 = (RectTransform)物体.transform;
        矩.anchorMin = new Vector2(0f, 0.5f); 矩.anchorMax = new Vector2(0f, 0.5f);
        矩.pivot = new Vector2(0.5f, 0.5f);
        矩.anchoredPosition = new Vector2(单位.列 * 节点间距 + Random.Range(-26f, 26f), 棋子直径 * 0.45f);
        矩.sizeDelta = new Vector2(150f, 42f);
        var 字 = 物体.GetComponent<TextMeshProUGUI>();
        字.text = 文本;
        字.fontSize = 字号;
        字.color = 色;
        字.alignment = TextAlignmentOptions.Center;
        字.raycastTarget = false;
        StartCoroutine(飘字动画(矩, 字));
    }

    private IEnumerator 飘字动画(RectTransform 矩, TextMeshProUGUI 字)
    {
        const float 时长 = 0.7f;
        var 起点 = 矩.anchoredPosition;
        var 原色 = 字.color;
        float t = 0f;
        while (t < 时长)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 时长);
            if (矩 != null) 矩.anchoredPosition = 起点 + new Vector2(0f, 34f * k);
            if (字 != null) 字.color = new Color(原色.r, 原色.g, 原色.b, Mathf.Lerp(1f, 0f, Mathf.Clamp01(k * 1.5f)));
            yield return null;
        }
        if (矩 != null) Destroy(矩.gameObject);
    }

    private Coroutine 抖动句柄;
    public void 受击抖动(float 幅度)
    {
        if (幅度 <= 0f) return;
        if (抖动句柄 != null) StopCoroutine(抖动句柄);
        抖动句柄 = StartCoroutine(抖动动画(幅度));
    }

    private IEnumerator 抖动动画(float 幅度)
    {
        var 相机 = Camera.main;
        if (相机 == null) { 抖动句柄 = null; yield break; }
        var 原 = 相机.transform.localPosition;
        const float 时长 = 0.16f;
        float t = 0f;
        while (t < 时长)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / 时长);
            相机.transform.localPosition = 原 + (Vector3)Random.insideUnitCircle * 幅度 * k;
            yield return null;
        }
        if (相机 != null) 相机.transform.localPosition = 原;
        抖动句柄 = null;
    }
}
