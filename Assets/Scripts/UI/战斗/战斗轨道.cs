using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 战斗轨道：战斗沙盒 的 节点式轨道 渲染（轻量，不用 网格面板基类）。
// 双方抽象为"点"（视觉 = 圆棋子），沿横向轨道节点（0 ~ N-1）前后移动。
// 单位结构：每个 战斗单位 = 一个 父节点（单位根），内含 身体圆 + 信息卡（名字/血条/行动条）——
//   移动 只 改 父节点 x（运动跟随）；信息卡 相对 y 按 阵营 名额 分配（友方/敌方 各 最多 2 个 上方，放不下 下方）。
// 交互：点 信息卡 = 选中 对应 单位；点 重叠 圆 = 同节点 单位 循环 切换；点 空白 = 取消。
public sealed class 战斗轨道 : MonoBehaviour
{
    [SerializeField] private RectTransform 轨道容器;   // 轨道区域（线/节点/单位根 动态生成到这里）
    [SerializeField] private float 节点间距 = 130f;     // 相邻 节点 中心 像素 距离（≥ 节点直径，防 圆 重叠）
    [SerializeField] private float 节点直径 = 100f;     // 节点 圆 直径（站位 标记）
    [SerializeField] private float 直线宽度 = 3f;       // 轨道 直线 宽度
    [SerializeField] private float 棋子直径 = 70f;      // 身体 圆 直径（单位，叠 在 节点 上，同节点 重叠）
    [SerializeField] private float 信息卡间距 = 34f;    // 同阵营 信息卡 垂直 堆叠 间距
    [SerializeField] private float 信息层距 = 22f;      // 信息卡 与 棋子（身体圆）边缘 的 间隔
    [SerializeField] private float 信息卡宽 = 130f;     // 信息卡 宽度

    // 单位 视图：根（移动 单位）= 圆（身体）+ 信息卡（名字/血条/行动条）
    private sealed class 单位视图
    {
        public RectTransform 根;
        public RectTransform 信息卡;
        public Image 圆;
        public TMP_Text 名字;
        public Image 血条;
        public Image 行动条;
    }
    private readonly Dictionary<战斗单位, 单位视图> 单位表 = new Dictionary<战斗单位, 单位视图>();
    private readonly Dictionary<int, int> 节点点击计数 = new Dictionary<int, int>();   // 重叠 圆 点击 循环 计数
    private RectTransform 空白层;
    private int 节点数;

    private 战斗沙盒面板 外壳 => GetComponentInParent<战斗沙盒面板>();

    // 圆形 白 贴图（运行时 生成，零资源依赖——内置 UI/Skin/Knob 在 2022 不存在）
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

    private static readonly Color 我方色 = new Color(0.30f, 0.55f, 0.36f, 1f);
    private static readonly Color 敌色 = new Color(0.62f, 0.30f, 0.28f, 1f);
    private static readonly Color 可选色 = new Color(0.95f, 0.85f, 0.3f, 1f);
    private static readonly Color 节点色 = new Color(0.16f, 0.16f, 0.20f, 0.8f);
    private static readonly Color 卡底色 = new Color(0.08f, 0.08f, 0.10f, 0.9f);
    private static readonly Color 名字色 = Color.white;
    private static readonly Color 名字可选色 = new Color(0.95f, 0.85f, 0.3f, 1f);

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
        foreach (var kv in 单位表) if (kv.Value?.根 != null) Destroy(kv.Value.根.gameObject);
        单位表.Clear();
        节点点击计数.Clear();
    }

    // 每帧：同步 单位（移动 父节点；信息卡 相对 y 按 阵营 名额；fill/高亮）
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
                if (单位表[单位]?.根 != null) Destroy(单位表[单位].根.gameObject);
                单位表.Remove(单位);
            }
        }
        // 阵营 名额 分配：友方/敌方 各 前 2 → 上方，其余 下方（y 为 信息卡 相对 根 的 local y）
        foreach (var 阵营组 in new[] { 我方组(单位组), 敌方组(单位组) })
        {
            for (int i = 0; i < 阵营组.Count; i++)
            {
                var 单位 = 阵营组[i];
                if (!单位表.TryGetValue(单位, out var 视图)) { 视图 = 创建单位根(单位); 单位表[单位] = 视图; }
                if (视图 == null) continue;
                if (!单位.存活) { 视图.根.gameObject.SetActive(false); continue; }
                视图.根.gameObject.SetActive(true);
                视图.根.anchoredPosition = new Vector2(单位.列 * 节点间距, 0f);   // 移动 父节点 = 圆 + 信息卡 一起 跟随
                bool 上方 = i < 2;
                int 层序号 = 上方 ? i : i - 2;
                float 相对y = 上方
                    ? -(棋子直径 / 2f + 信息层距 + 层序号 * 信息卡间距)     // 上方（负 = 上）：棋子边缘 + 间隔 + 堆叠
                    : +(棋子直径 / 2f + 信息层距 + 层序号 * 信息卡间距);    // 下方
                视图.信息卡.localPosition = new Vector2(0f, 相对y);
                bool 可选 = 外壳 != null && 外壳.是可选目标(单位);
                视图.圆.color = 可选 ? 可选色 : (单位.是否我方 ? 我方色 : 敌色);
                视图.名字.text = 单位.名称;
                视图.名字.color = 可选 ? 名字可选色 : 名字色;
                视图.血条.fillAmount = 单位.最大生命 > 0 ? (float)单位.生命 / 单位.最大生命 : 0f;
                视图.行动条.fillAmount = Mathf.Clamp01(单位.行动条 / 100f);
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

    // 单位根：父节点（移动单位）= 身体圆 + 信息卡（子）；点击 圆 = 循环切换，点 信息卡 = 精确 选中
    private 单位视图 创建单位根(战斗单位 单位)
    {
        if (轨道容器 == null) return null;
        var 视图 = new 单位视图();
        var 根 = new GameObject($"单位_{单位.名称}", typeof(RectTransform)).GetComponent<RectTransform>();
        根.SetParent(轨道容器, false);
        根.anchorMin = new Vector2(0f, 0.5f); 根.anchorMax = new Vector2(0f, 0.5f);
        根.pivot = new Vector2(0.5f, 0.5f);
        根.sizeDelta = Vector2.zero;   // 根 不 参与 布局，子 决定 视觉
        // —— 身体 圆（节点 中心）——
        var 圆 = new GameObject("圆", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
        圆.SetParent(根, false);
        圆.anchorMin = new Vector2(0f, 0.5f); 圆.anchorMax = new Vector2(0f, 0.5f);
        圆.pivot = new Vector2(0.5f, 0.5f);
        圆.sizeDelta = new Vector2(棋子直径 * 单位.身形, 棋子直径);   // 大体型 视觉 加宽
        var 圆图 = 圆.GetComponent<Image>();
        圆图.sprite = 圆形();
        圆图.color = 单位.是否我方 ? 我方色 : 敌色;
        圆图.raycastTarget = true;
        var 单位副本 = 单位;
        圆.GetComponent<Button>().onClick.AddListener(() => 点击圆(单位副本));
        视图.圆 = 圆图;
        // —— 信息卡（子，相对 y 分配 上/下）——
        var 卡 = new GameObject("信息卡", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
        卡.SetParent(根, false);
        卡.anchorMin = new Vector2(0f, 0.5f); 卡.anchorMax = new Vector2(0f, 0.5f);
        卡.pivot = new Vector2(0.5f, 0.5f);
        卡.sizeDelta = new Vector2(信息卡宽, 30f);
        卡.GetComponent<Image>().color = 卡底色;
        卡.GetComponent<Image>().raycastTarget = true;
        卡.GetComponent<Button>().onClick.AddListener(() => { if (外壳 != null) 外壳.点击单位(单位副本); });
        // 名字（信息卡 顶部）
        var 名字 = 创建文本(卡, "名字", 单位.名称, 12f, TextAlignmentOptions.Top);
        ((RectTransform)名字.transform).anchorMin = Vector2.zero;
        ((RectTransform)名字.transform).anchorMax = Vector2.one;
        ((RectTransform)名字.transform).offsetMin = new Vector2(4f, 12f);
        ((RectTransform)名字.transform).offsetMax = new Vector2(-4f, -1f);
        var 血条 = 创建条(卡, "血条", new Color(0.85f, 0.22f, 0.18f, 1f), 10f);
        var 行动条 = 创建条(卡, "行动条", new Color(1f, 0.78f, 0.28f, 1f), 3f);
        视图.根 = 根; 视图.信息卡 = 卡;
        视图.名字 = 名字; 视图.血条 = 血条; 视图.行动条 = 行动条;
        return 视图;
    }

    // 重叠 圆 点击：同节点 多单位 → 循环 切换 选中；单个 → 直接
    private void 点击圆(战斗单位 单位)
    {
        if (外壳 == null || !单位.存活) return;
        var 同节点 = new List<战斗单位>();
        foreach (var 敌 in 单位表.Keys)
            if (敌.存活 && 敌.列 == 单位.列) 同节点.Add(敌);
        if (同节点.Count <= 1) { 外壳.点击单位(单位); return; }
        节点点击计数.TryGetValue(单位.列, out int n);
        节点点击计数[单位.列] = n + 1;
        外壳.点击单位(同节点[n % 同节点.Count]);
    }

    // 信息卡 内 细条：锚 底部，y 偏移 指定
    private Image 创建条(RectTransform 父, string 名, Color 色, float y偏移)
    {
        var 物体 = new GameObject(名, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        物体.SetParent(父, false);
        物体.anchorMin = new Vector2(0f, 0f); 物体.anchorMax = new Vector2(1f, 0f);
        物体.pivot = new Vector2(0.5f, 0.5f);
        物体.anchoredPosition = new Vector2(0f, y偏移);
        物体.sizeDelta = new Vector2(-10f, 5f);
        var 图像 = 物体.GetComponent<Image>();
        图像.color = 色;
        图像.type = Image.Type.Filled;
        图像.fillMethod = Image.FillMethod.Horizontal;
        图像.fillAmount = 1f;
        return 图像;
    }

    private TextMeshProUGUI 创建文本(RectTransform 父, string 名, string 内容, float 字号, TextAlignmentOptions 对齐)
    {
        var 物体 = new GameObject(名, typeof(RectTransform), typeof(TextMeshProUGUI));
        物体.transform.SetParent(父, false);
        var 文本 = 物体.GetComponent<TextMeshProUGUI>();
        文本.text = 内容;
        文本.fontSize = 字号;
        文本.alignment = 对齐;
        文本.color = Color.white;
        文本.raycastTarget = false;
        return 文本;
    }
}
