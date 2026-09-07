using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 战斗棋盘网格：战斗沙盒 的 棋盘渲染（网格面板基类 子类——复用 网格基类 底座/分隔线/实体框 增量刷新 全链路）。
// 实体语义：棋子 = 网格实体（色块 + 名称 + 血条 + 行动条）；单击 = 目标选择（转发 外壳 战斗沙盒面板）。
// 数据源 = 棋盘服务（new 网格服务：宽高 = 棋盘，网格物品 = 单位伪堆叠）——注入 数据源 即驱动 网格基类 渲染。
// 不可拖拽：允许跨面板=false / 允许开始拖拽=false / 完成拖拽=false。
// 位置刷新：单位 每帧 由 外壳 同步（同步单位），实体框 定位 直接 用 单位 实时 坐标（伪堆叠 列/行 保持 出生位，避免 布局变化 触发 分隔线 重画）。
public sealed class 战斗棋盘网格 : 网格面板基类
{
    private readonly Dictionary<战斗单位, 物品堆叠> 单位伪堆叠 = new Dictionary<战斗单位, 物品堆叠>();
    private readonly Dictionary<物品堆叠, 战斗单位> 伪堆叠单位 = new Dictionary<物品堆叠, 战斗单位>();
    private readonly Dictionary<物品框, (Image 血条, Image 行动条)> 框表附加 = new Dictionary<物品框, (Image, Image)>();
    private RectTransform 空白层;   // 点击 非实体 区域 → 取消目标选择

    private 战斗沙盒面板 外壳 => GetComponentInParent<战斗沙盒面板>();

    private static readonly Color 我方色 = new Color(0.30f, 0.55f, 0.36f, 1f);
    private static readonly Color 敌色 = new Color(0.62f, 0.30f, 0.28f, 1f);
    private static readonly Color 可选色 = new Color(0.95f, 0.85f, 0.3f, 1f);

    // 布阵：注入 棋盘 数据源（网格基类 渲染底座），每场战斗重建
    public void 布阵(int 宽, int 高)
    {
        单位伪堆叠.Clear(); 伪堆叠单位.Clear(); 框表附加.Clear();
        数据源 = new 网格服务 { 网格列 = 宽, 网格行 = 高, 网格物品 = new List<物品堆叠>() };
        确保空白点击层();
        请求刷新();
    }

    // 每帧：同步 单位 → 伪堆叠（增删）+ 请求刷新（增量渲染 位置/血条/行动条/可选高亮）
    public void 同步单位(List<战斗单位> 单位组)
    {
        if (数据源 == null) return;
        if (单位伪堆叠.Count != 单位组.Count)   // 数量变化（死亡/助战进场）→ 同步 增删
        {
            var 移除 = new List<战斗单位>();
            foreach (var kv in 单位伪堆叠) if (!单位组.Contains(kv.Key)) 移除.Add(kv.Key);
            foreach (var 单位 in 移除)
            {
                服务.网格物品.Remove(单位伪堆叠[单位]);
                伪堆叠单位.Remove(单位伪堆叠[单位]);
                单位伪堆叠.Remove(单位);
            }
        }
        foreach (var 单位 in 单位组)
        {
            if (单位伪堆叠.ContainsKey(单位)) continue;
            var 伪 = new 物品堆叠("单位:" + 单位.名称, 1) { 列 = 单位.列, 行 = 单位.行 };
            单位伪堆叠[单位] = 伪;
            伪堆叠单位[伪] = 单位;
            服务.网格物品.Add(伪);
        }
        请求刷新();
    }

    // ===== 网格基类 实体钩子 =====

    protected override 物品框 创建实体框(物品堆叠 堆叠)
    {
        if (!伪堆叠单位.TryGetValue(堆叠, out var 单位)) return null;
        var 框 = new 物品框();
        var 根 = new GameObject(单位.名称, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        根.SetParent(物品层, false);
        var 图 = 根.GetComponent<Image>();
        图.color = 单位.是否我方 ? 我方色 : 敌色;
        图.raycastTarget = true;
        框.根 = 根; 框.框图 = 图;
        var 内容 = new GameObject("内容", typeof(RectTransform)).GetComponent<RectTransform>();
        内容.SetParent(根, false);
        框.内容层 = 内容;
        框.名称 = 创建文本(根, "名称", 单位.名称, 16f, TextAlignmentOptions.TopLeft);
        var 名称矩 = (RectTransform)框.名称.transform;
        名称矩.anchorMin = Vector2.zero; 名称矩.anchorMax = Vector2.one;
        名称矩.offsetMin = new Vector2(4f, 6f); 名称矩.offsetMax = new Vector2(-4f, -2f);
        var 血条 = 创建条(内容, "血条", new Color(0.85f, 0.22f, 0.18f, 1f), 0.04f);
        var 行动条 = 创建条(内容, "行动条", new Color(1f, 0.78f, 0.28f, 1f), 0.12f);
        框.创建标识 = 单位.名称;
        框表附加[框] = (血条, 行动条);
        挂接交互(框, 堆叠, 内容);
        定位(根, 堆叠.列, 堆叠.行, 单位.身形, 1);
        return 框;
    }

    protected override void 更新实体框(物品框 框, 物品堆叠 堆叠)
    {
        if (!伪堆叠单位.TryGetValue(堆叠, out var 单位)) return;
        if (!单位.存活) { 框.根.gameObject.SetActive(false); return; }
        框.根.gameObject.SetActive(true);
        定位(框.根, 单位.列, 单位.行, 单位.身形, 1);   // 直接用 单位 实时 坐标（不 改 伪堆叠 列/行，防 布局变化 重画线）
        if (框.名称 != null) 框.名称.text = 单位.名称;
        bool 可选 = 外壳 != null && 外壳.是可选目标(单位);
        框.框图.color = 可选 ? 可选色 : (单位.是否我方 ? 我方色 : 敌色);
        if (框表附加.TryGetValue(框, out var 条))
        {
            条.血条.fillAmount = 单位.最大生命 > 0 ? (float)单位.生命 / 单位.最大生命 : 0f;
            条.行动条.fillAmount = Mathf.Clamp01(单位.行动条 / 100f);
        }
    }

    protected override void 单击实体(物品堆叠 堆叠, int 点击次数)
    {
        if (伪堆叠单位.TryGetValue(堆叠, out var 单位)) 外壳?.点击单位(单位);
    }

    protected override void 右键实体(物品堆叠 堆叠, RectTransform 框) { }   // 棋盘实体 无 右键菜单
    protected override bool 完成拖拽(PointerEventData 事件, 物品堆叠 堆叠) => false;
    protected override bool 允许跨面板() => false;
    protected override bool 允许开始拖拽(物品堆叠 堆叠) => false;
    protected override bool 目标允许放入(string 标识) => false;

    // 空白点击层：点击 非实体 区域 → 取消目标选择（底格/线/底盘 raycastTarget 均 false，穿透到 本层）
    private void 确保空白点击层()
    {
        if (空白层 != null || 网格容器 == null) return;
        var 物体 = new GameObject("空白点击", typeof(RectTransform), typeof(Image), typeof(Button));
        物体.transform.SetParent(网格容器, false);
        物体.transform.SetAsFirstSibling();
        物体.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        物体.GetComponent<Button>().onClick.AddListener(() => 外壳?.点击空白());
        var 矩 = 物体.GetComponent<RectTransform>();
        矩.anchorMin = Vector2.zero; 矩.anchorMax = Vector2.one;
        矩.offsetMin = Vector2.zero; 矩.offsetMax = Vector2.zero;
        空白层 = 矩;
    }

    private Image 创建条(RectTransform 父, string 名, Color 色, float 顶偏移)
    {
        var 物体 = new GameObject(名, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        物体.SetParent(父, false);
        物体.anchorMin = new Vector2(0.02f, 1f - 顶偏移 - 0.06f);
        物体.anchorMax = new Vector2(0.98f, 1f - 顶偏移);
        物体.offsetMin = Vector2.zero; 物体.offsetMax = Vector2.zero;
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
