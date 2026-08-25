using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 网格背包面板：动态构建网格底座 + 物品层（纯 图像+文本，非按钮）。
// 底座：按 玩家档案.网格列×网格行 动态生成的格子底图（背包装备决定网格尺寸，换包自动重建）；每格 Outline 描边线形成视觉网格。
// 物品：按 (列,行,宽,高,旋转) 跨格铺放的 图像+文本；点击（IPointerClickHandler）选中/双击快捷操作；拖拽（IDragHandler）移动/换位。
// 拖拽交互：按住物品拖起（半透明代理跟随鼠标）→ 放空格=移动（拖拽中按 R 旋转）→ 放被占格=换位（互换位置与旋转）→ 拖出网格=取消。
// 操作：[使用]（食物/水/医疗品 按恢复目标结算）[装备]（武器/防具/背包）。
public sealed class 网格背包面板 : 面板基类
{
    [SerializeField] private RectTransform 网格容器;   // 网格区域（左上锚定；代码动态生成底座与物品）
    [SerializeField] private float 格尺寸 = 150f;      // 单格像素（Inspector 可调）
    [SerializeField] private Color 底座色 = new Color(0f, 0f, 0f, 0.35f);          // 空格底图
    [SerializeField] private Color 线色 = new Color(1f, 1f, 1f, 0.55f);            // 网格分隔线
    [SerializeField] private float 线宽 = 4f;                                      // 分隔线宽（px）
    [SerializeField] private float 物品边距 = 6f;                                  // 物品块四周内缩（不压网格线/不重叠）
    [SerializeField] private Color 物品底色 = new Color(0.28f, 0.3f, 0.36f, 0.95f);
    [SerializeField] private Color 选中底色 = new Color(0.45f, 0.38f, 0.18f, 0.95f);
    [SerializeField, Range(0f, 1f)] private float 品质底色透明 = 0.3f;   // 品质底色（物品框层）半透明程度
    [SerializeField] private Color 放置可色 = new Color(0.45f, 1f, 0.5f, 0.35f);    // 拖拽投影：可放（淡绿半透明）
    [SerializeField] private Color 放置禁色 = new Color(1f, 0.4f, 0.4f, 0.35f);      // 拖拽投影：不可放（淡红半透明）
    [SerializeField] private Color 合并色 = new Color(0.45f, 0.75f, 1f, 0.4f);       // 拖拽投影：可合并（淡蓝半透明）
    [SerializeField] private TMP_Text 信息条;          // 已用 X/Y 格 · 负重 A/B
    [SerializeField] private TMP_Text 详情文本;        // 选中物品详情
    [SerializeField] private Button 使用按钮, 装备按钮;

    private 玩家档案 档案 => ServiceRegistry.Get<PlayerService>().档案;
    private DataService 数据 => ServiceRegistry.Get<DataService>();
    private 物品堆叠 选中;

    // —— 拖拽状态 ——
    private 物品堆叠 拖拽源;
    private RectTransform 拖拽代理;   // 跟手物品图片（吸附格子）
    private Image 落点投影;           // 网格上的绿/红落点指示
    private GameObject 原位置影子;    // 原位置的半透明虚影
    private bool 拖拽旋转;

    // ===== 生命周期 =====

    void Awake()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<背包变化事件>(背包变化响应);
        事件.订阅<属性变化事件>(属性变化响应);
        if (使用按钮 != null) 使用按钮.onClick.AddListener(使用选中);
        if (装备按钮 != null) 装备按钮.onClick.AddListener(装备选中);
    }

    void OnDestroy()
    {
        if (ServiceRegistry.已注册<EventBus>())
        {
            var 事件 = ServiceRegistry.Get<EventBus>();
            事件.取消订阅<背包变化事件>(背包变化响应);
            事件.取消订阅<属性变化事件>(属性变化响应);
        }
    }

    private void 背包变化响应(背包变化事件 _) => 刷新网格();
    private void 属性变化响应(属性变化事件 _) => 刷新信息();

    protected override void 刷新(object 上下文)
    {
        选中 = null;
        刷新网格();
    }

    public override bool 回退()
    {
        if (面板管理器.实例 != null) 面板管理器.实例.返回上一面板();
        return true;
    }

    public override string 取消文本 => "返回";

    // ===== 渲染 =====

    // 重建整个网格：动态底座（网格列×行 个底格）→ 物品层
    private void 刷新网格()
    {
        if (网格容器 == null) return;
        清空(网格容器);
        网格容器.sizeDelta = new Vector2(档案.网格列 * 格尺寸, 档案.网格行 * 格尺寸);
        for (int 行 = 0; 行 < 档案.网格行; 行++)
            for (int 列 = 0; 列 < 档案.网格列; 列++)
                创建底格(列, 行);
        画分隔线();   // 线在格子之间（格子在线内）
        foreach (var 堆叠 in 档案.背包)
            if (堆叠 != null && 堆叠.列 >= 0)
                创建物品(堆叠);
        刷新信息();
    }

    // 底座一格（底图 + 粗描边线形成网格；无交互）
    private void 创建底格(int 列, int 行)
    {
        var 物体 = new GameObject($"底格_{行}_{列}", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(网格容器, false);
        var 图 = 物体.GetComponent<Image>();
        图.color = 底座色;
        图.raycastTarget = false;   // 纯底图（不描边——分隔线由 画分隔线 统一绘制，格子在线内）
        定位(物体.GetComponent<RectTransform>(), 列, 行, 1, 1);
    }

    // 画网格分隔线：横线（行+1 条）+ 竖线（列+1 条），线中心对齐格子边界——线只用于分离格子，格子底图在线内
    private void 画分隔线()
    {
        float 宽 = 档案.网格列 * 格尺寸;
        float 高 = 档案.网格行 * 格尺寸;
        for (int i = 0; i <= 档案.网格列; i++)   // 竖线（含左右边界）
        {
            var 物体 = new GameObject($"竖线_{i}", typeof(RectTransform), typeof(Image));
            物体.transform.SetParent(网格容器, false);
            var 图 = 物体.GetComponent<Image>();
            图.color = 线色;
            图.raycastTarget = false;
            var 矩形 = 物体.GetComponent<RectTransform>();
            矩形.anchorMin = new Vector2(0, 1);
            矩形.anchorMax = new Vector2(0, 1);
            矩形.pivot = new Vector2(0.5f, 0.5f);
            矩形.anchoredPosition = new Vector2(i * 格尺寸, -高 / 2f);
            矩形.sizeDelta = new Vector2(线宽, 高);
        }
        for (int j = 0; j <= 档案.网格行; j++)   // 横线（含上下边界）
        {
            var 物体 = new GameObject($"横线_{j}", typeof(RectTransform), typeof(Image));
            物体.transform.SetParent(网格容器, false);
            var 图 = 物体.GetComponent<Image>();
            图.color = 线色;
            图.raycastTarget = false;
            var 矩形 = 物体.GetComponent<RectTransform>();
            矩形.anchorMin = new Vector2(0, 1);
            矩形.anchorMax = new Vector2(0, 1);
            矩形.pivot = new Vector2(0.5f, 0.5f);
            矩形.anchoredPosition = new Vector2(宽 / 2f, -j * 格尺寸);
            矩形.sizeDelta = new Vector2(宽, 线宽);
        }
    }

    // 物品：两层结构 —— ① 物品框（全尺寸 Image = 品质底层色 + 黑描边，点击/拖拽挂这里）→ ② 内容层（内缩 Image = 深色占位块，将来贴美术图）。
    // 品质色永远在框层：内容层内缩 物品边距，无论现在是色块还是将来的美术图，四周都会露出品质色环。
    private void 创建物品(物品堆叠 堆叠)
    {
        if (!数据.物品.TryGetValue(堆叠.标识, out var 物品)) return;
        var 形状 = 档案.形状解析?.Invoke(堆叠.标识) ?? new 物品形状(1, 1);
        int 宽 = 堆叠.旋转 ? 形状.高 : 形状.宽;
        int 高 = 堆叠.旋转 ? 形状.宽 : 形状.高;
        // ① 物品框：全尺寸贴格（品质底层色；选中 = 选中底色）
        var 物体 = new GameObject($"物品_{物品.名称}", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(网格容器, false);
        var 框图 = 物体.GetComponent<Image>();
        框图.color = 选中 == 堆叠 ? 选中底色 : 品质底层色(堆叠);
        var 边框 = 物体.AddComponent<Outline>();
        边框.effectColor = new Color(0f, 0f, 0f, 0.6f);
        边框.effectDistance = new Vector2(3f, -3f);
        var 矩形 = 物体.GetComponent<RectTransform>();
        矩形.anchorMin = new Vector2(0, 1);
        矩形.anchorMax = new Vector2(0, 1);
        矩形.pivot = new Vector2(0, 1);
        矩形.anchoredPosition = new Vector2(堆叠.列 * 格尺寸, -堆叠.行 * 格尺寸);
        矩形.sizeDelta = new Vector2(宽 * 格尺寸, 高 * 格尺寸);
        // ② 内容层（内缩：尺寸少 2×边距，向格内偏移——不压网格线、不叠品质环；将来替换为美术图 sprite）
        var 内容物体 = new GameObject("内容", typeof(RectTransform), typeof(Image));
        内容物体.transform.SetParent(物体.transform, false);
        var 内容图 = 内容物体.GetComponent<Image>();
        内容图.color = 物品底色;
        内容图.raycastTarget = false;   // 不挡底层交互（点击/拖拽挂在物品框上）
        // 手动挂图：items.json 的 "图片" 引用 → 内容层显示精灵；无图/未挂 = 保持色块（品质色环在物品框层不受影响）
        var 图标 = 物品图标服务.获取(物品.图片);
        if (图标 != null)
        {
            内容图.sprite = 图标;
            内容图.color = Color.white;          // 有图时不再用色底染色
            内容图.preserveAspect = true;        // 按比例居中，避免拉伸（武器横向图标在格内等比缩放）
        }
        var 内容矩形 = 内容物体.GetComponent<RectTransform>();
        内容矩形.anchorMin = new Vector2(0.5f, 0.5f);
        内容矩形.anchorMax = new Vector2(0.5f, 0.5f);
        内容矩形.pivot = new Vector2(0.5f, 0.5f);
        内容矩形.anchoredPosition = Vector2.zero;   // 居中于物品框
        内容矩形.sizeDelta = new Vector2(形状.宽 * 格尺寸 - 物品边距 * 2f, 形状.高 * 格尺寸 - 物品边距 * 2f);   // 未旋转宽高（旋转由 rotation 承担）
        内容矩形.localRotation = Quaternion.Euler(0f, 0f, 堆叠.旋转 ? 90f : 0f);   // 图标跟随物品旋转 90°
        // ③ 标签（名称×数量）：挂在物品框下，始终水平居中（不随内容层旋转）
        var 标签物体 = new GameObject("标签", typeof(RectTransform), typeof(TextMeshProUGUI));
        标签物体.transform.SetParent(物体.transform, false);
        var 标签 = 标签物体.GetComponent<TextMeshProUGUI>();
        标签.text = 堆叠.数量 > 1 ? $"{物品.名称}×{堆叠.数量}" : 物品.名称;
        标签.fontSize = 宽 >= 2 ? 32f : 24f;
        标签.alignment = TextAlignmentOptions.Center;
        标签.color = 选中 == 堆叠 ? new Color(1f, 0.9f, 0.55f) : Color.white;
        标签.raycastTarget = false;   // 不挡物品框的点击/拖拽
        标签.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        标签.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        标签.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        标签.rectTransform.anchoredPosition = Vector2.zero;
        标签.rectTransform.sizeDelta = new Vector2(宽 * 格尺寸 - 物品边距 * 2f, 高 * 格尺寸 - 物品边距 * 2f);
        // ④ 点击（非按钮） + 拖拽（挂在物品框上）
        var 点击 = 物体.AddComponent<物品点击>();
        点击.堆叠 = 堆叠;
        点击.面板 = this;
        var 拖拽 = 物体.AddComponent<物品拖拽>();
        拖拽.堆叠 = 堆叠;
        拖拽.面板 = this;
    }

    // 左上锚定定位：列/行 起点 + 宽×高 跨格
    private void 定位(RectTransform 矩形, int 列, int 行, int 宽, int 高)
    {
        矩形.anchorMin = new Vector2(0, 1);
        矩形.anchorMax = new Vector2(0, 1);
        矩形.pivot = new Vector2(0, 1);
        矩形.anchoredPosition = new Vector2(列 * 格尺寸, -行 * 格尺寸);
        矩形.sizeDelta = new Vector2(宽 * 格尺寸, 高 * 格尺寸);
    }

    // 品质底层色（物品框）：全部物品按有效品质整块着色（深底与品质色混合，半透明）；普通 = 透明（无品质染色）。
    // 架构约定：品质色永远属于"物品框层"（全尺寸底层）——将来内容层换成美术图片后，四周仍露出品质色环。
    private Color 品质底层色(物品堆叠 堆叠)
    {
        if (堆叠 == null || !数据.物品.TryGetValue(堆叠.标识, out var 物品)) return new Color(0f, 0f, 0f, 0f);
        品质 档 = 有效品质(堆叠, 物品);
        if (档 == 品质.普通) return new Color(0f, 0f, 0f, 0f);
        var 色 = Color.Lerp(物品底色, 品质工具.颜色(档), 0.55f);
        色.a = 品质底色透明;   // 半透明（能看到底座格/分隔线，品质色仍是区分度）
        return 色;
    }

    // 拖拽代理底色：非普通 = 品质底层色；普通 = 内容层色（不透明，跟手可见）
    private Color 物品品质底(物品堆叠 堆叠)
    {
        var 层色 = 品质底层色(堆叠);
        return 层色.a <= 0f ? 物品底色 : 层色;
    }

    // 有效品质：堆叠品质覆盖（合成提升）优先，否则取物品模板品质
    private static 品质 有效品质(物品堆叠 堆叠, 物品数据 模板)
        => !string.IsNullOrEmpty(堆叠.品质) ? 数据解析.枚举<品质>(堆叠.品质) : 模板.品质档;

    // ===== 点击交互 =====

    private void 物品被点击(物品堆叠 堆叠, int 点击次数)
    {
        选中 = 堆叠;
        if (点击次数 >= 2) { 快捷操作(堆叠); 选中 = null; }
        刷新网格();
    }

    // 双击快捷操作：恢复品=使用；装备类=换装；技能书=学习
    private void 快捷操作(物品堆叠 堆叠)
    {
        if (!数据.物品.TryGetValue(堆叠.标识, out var 物品)) return;
        if (物品.恢复量 > 0) { 使用选中(); return; }
        if (!string.IsNullOrEmpty(物品.槽位)) { 装备选中(); return; }
        if (物品.类型 == "技能书") { 面板操作.学习技能书(档案, 物品); return; }
        音效管理器.实例?.播放失败();
    }

    // ===== 拖拽交互 =====

    // 按下进入拖拽：记录源物品 + 创建视觉（代理/投影/影子），立即开始
    private void 开始拖拽(物品堆叠 堆叠, PointerEventData 事件)
    {
        拖拽源 = 堆叠;
        拖拽旋转 = 堆叠.旋转;
        创建拖拽视觉(堆叠);
        拖拽移动(事件);
    }

    // 首次超过启动阈值：创建 跟手代理 + 落点投影 + 原位置影子
    private void 创建拖拽视觉(物品堆叠 堆叠)
    {
        // ① 跟手代理
        var 物体 = new GameObject("拖拽代理", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(网格容器, false);
        var 图 = 物体.GetComponent<Image>();
        图.raycastTarget = false;
        // 代理优先显示挂载图标；无图则用品质底色块（半透明跟手）
        var 代理物品 = 数据.物品.TryGetValue(堆叠.标识, out var 代理数据) ? 代理数据 : null;
        var 代理图标 = 代理物品 != null ? 物品图标服务.获取(代理物品.图片) : null;
        if (代理图标 != null)
        {
            图.sprite = 代理图标;
            图.color = new Color(1f, 1f, 1f, 0.85f);
            图.preserveAspect = true;
        }
        else
        {
            var 代理底 = 物品品质底(堆叠);
            图.color = new Color(代理底.r, 代理底.g, 代理底.b, 0.85f);
        }
        拖拽代理 = 物体.GetComponent<RectTransform>();
        拖拽代理.anchorMin = new Vector2(0, 1);
        拖拽代理.anchorMax = new Vector2(0, 1);
        拖拽代理.pivot = new Vector2(0.5f, 0.5f);   // 中心跟随鼠标
        var 标签物体 = new GameObject("标签", typeof(RectTransform), typeof(TextMeshProUGUI));
        标签物体.transform.SetParent(物体.transform, false);
        var 标签 = 标签物体.GetComponent<TextMeshProUGUI>();
        标签.text = 数据.物品.TryGetValue(堆叠.标识, out var 物) ? (堆叠.数量 > 1 ? $"{物.名称}×{堆叠.数量}" : 物.名称) : 堆叠.标识;
        标签.fontSize = 26f;
        标签.alignment = TextAlignmentOptions.Center;
        标签.rectTransform.anchorMin = Vector2.zero;
        标签.rectTransform.anchorMax = Vector2.one;
        标签.rectTransform.offsetMin = Vector2.zero;
        标签.rectTransform.offsetMax = Vector2.zero;
        // ② 落点投影（绿/红，贴格）
        var 投体 = new GameObject("落点投影", typeof(RectTransform), typeof(Image));
        投体.transform.SetParent(网格容器, false);
        落点投影 = 投体.GetComponent<Image>();
        落点投影.color = 放置可色;
        落点投影.raycastTarget = false;
        var 投影矩形 = 投体.GetComponent<RectTransform>();
        投影矩形.anchorMin = new Vector2(0, 1);
        投影矩形.anchorMax = new Vector2(0, 1);
        投影矩形.pivot = new Vector2(0, 1);   // 左上贴格（吸附网格，不跟手）
        落点投影.gameObject.SetActive(false);
        更新代理尺寸();
        // ③ 原位置半透明影子（虚影：物品将离开的位置；内缩尺寸与物品一致）
        var 影体 = new GameObject("原位置影子", typeof(RectTransform), typeof(Image));
        影体.transform.SetParent(网格容器, false);
        var 影图 = 影体.GetComponent<Image>();
        影图.color = new Color(0.65f, 0.65f, 0.7f, 0.3f);   // 半透明灰
        影图.raycastTarget = false;
        var 影矩形 = 影体.GetComponent<RectTransform>();
        影矩形.anchorMin = new Vector2(0, 1);
        影矩形.anchorMax = new Vector2(0, 1);
        影矩形.pivot = new Vector2(0, 1);
        影矩形.anchoredPosition = new Vector2(堆叠.列 * 格尺寸 + 物品边距, -堆叠.行 * 格尺寸 - 物品边距);
        var 影形状 = 档案.形状解析?.Invoke(堆叠.标识) ?? new 物品形状(1, 1);
        int 影宽 = 堆叠.旋转 ? 影形状.高 : 影形状.宽;
        int 影高 = 堆叠.旋转 ? 影形状.宽 : 影形状.高;
        影矩形.sizeDelta = new Vector2(影宽 * 格尺寸 - 物品边距 * 2f, 影高 * 格尺寸 - 物品边距 * 2f);   // 影子=物品实际占用的内缩块（旋转后）
        原位置影子 = 影体;
        拖拽代理.SetAsLastSibling();   // 代理置顶渲染——否则同格的落点投影（后创建）会盖住它
    }

    // 屏幕点 → 相对容器左下（**逻辑单位**，除以 Canvas 缩放——与 格尺寸 同基准，任何分辨率/缩放下都准）
    private bool 屏幕到容器相对(PointerEventData 事件, out Vector2 相对, out Vector2 容器尺寸)
    {
        容器尺寸 = 网格容器.rect.size;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(网格容器, 事件.position, 事件.pressEventCamera, out var 世界点))
        {
            相对 = Vector2.zero;
            return false;
        }
        var 原点 = 网格容器.TransformPoint(new Vector3(-网格容器.rect.width * 网格容器.pivot.x, -网格容器.rect.height * 网格容器.pivot.y, 0f));
        var 缩放 = 网格容器.lossyScale;
        相对 = new Vector2((世界点.x - 原点.x) / 缩放.x, (世界点.y - 原点.y) / 缩放.y);   // 逻辑单位（x 向右、y 向上）
        return true;
    }

    // 拖拽中：物品图片吸附鼠标所在格（格内锁定不移动，跨格才跳）→ 投影贴格同格；R 键旋转
    private void 拖拽移动(PointerEventData 事件)
    {
        if (拖拽源 == null || 拖拽代理 == null) return;
        if (检测按R()) { 拖拽旋转 = !拖拽旋转; 更新代理尺寸(); }
        if (!屏幕到容器相对(事件, out var 相对, out var 尺寸))
        {
            拖拽代理.gameObject.SetActive(false);
            if (落点投影 != null) 落点投影.gameObject.SetActive(false);
            return;
        }
        // 投影格 = 物品中心对齐（四舍五入：偏差对称 ±半格内，1×1 精确——大物体不错位）
        var 形状 = 档案.形状解析?.Invoke(拖拽源.标识) ?? new 物品形状(1, 1);
        int 物宽 = 拖拽旋转 ? 形状.高 : 形状.宽;
        int 物高 = 拖拽旋转 ? 形状.宽 : 形状.高;
        float 相对顶 = 尺寸.y - 相对.y;
        int 列 = Mathf.RoundToInt((相对.x - 物宽 * 格尺寸 / 2f) / 格尺寸);
        int 行 = Mathf.RoundToInt((相对顶 - 物高 * 格尺寸 / 2f) / 格尺寸);
        bool 网格内 = 列 >= 0 && 行 >= 0 && 列 < 档案.网格列 && 行 < 档案.网格行;
        if (!网格内)
        {
            拖拽代理.gameObject.SetActive(false);
            if (落点投影 != null) 落点投影.gameObject.SetActive(false);
            return;   // 拖出网格：隐藏代理与落点
        }
        // ① 物品图片：自由跟手（中心 = 鼠标位置，随鼠标连续移动）
        拖拽代理.gameObject.SetActive(true);
        拖拽代理.anchorMin = new Vector2(0, 1);
        拖拽代理.anchorMax = new Vector2(0, 1);
        拖拽代理.pivot = new Vector2(0.5f, 0.5f);   // 中心 = 鼠标
        拖拽代理.anchoredPosition = new Vector2(相对.x, 相对.y - 尺寸.y);   // 相对左下 → 左上锚点（y 向下）
        // ② 落点投影：吸附网格贴格（鼠标在格内投影不移动，跨格才跳；绿/红/蓝指示落格合法性：可放/不可放/可合并）
        var 目标 = 该格物品(列, 行);
        bool 可放;
        bool 可合并 = false;
        if (目标 != null && 目标 != 拖拽源)
        {
            if (档案.可合并(目标, 拖拽源)) { 可放 = true; 可合并 = true; }
            else 可放 = 档案.可换位(拖拽源, 目标);
        }
        else 可放 = 档案.可放置(拖拽源.标识, 列, 行, 拖拽旋转, 拖拽源);
        if (落点投影 != null)
        {
            落点投影.gameObject.SetActive(true);
            落点投影.rectTransform.anchoredPosition = new Vector2(列 * 格尺寸, -行 * 格尺寸);   // 精确贴格（吸附网格）
            落点投影.color = 可合并 ? 合并色 : (可放 ? 放置可色 : 放置禁色);
        }
    }

    // 代理尺寸 = 物品内缩（跟手图片）；投影尺寸 = 完整占格（贴格指示，与格子边缘精确对齐）
    private void 更新代理尺寸()
    {
        if (拖拽代理 == null || 拖拽源 == null) return;
        var 形状 = 档案.形状解析?.Invoke(拖拽源.标识) ?? new 物品形状(1, 1);
        int 宽 = 拖拽旋转 ? 形状.高 : 形状.宽;
        int 高 = 拖拽旋转 ? 形状.宽 : 形状.高;
        // 代理：未旋转内缩宽高 + 随 拖拽旋转 转 90°（跟手图片跟随旋转预览）
        拖拽代理.sizeDelta = new Vector2(形状.宽 * 格尺寸 - 物品边距 * 2f, 形状.高 * 格尺寸 - 物品边距 * 2f);
        拖拽代理.localRotation = Quaternion.Euler(0f, 0f, 拖拽旋转 ? 90f : 0f);
        if (落点投影 != null) 落点投影.rectTransform.sizeDelta = new Vector2(宽 * 格尺寸, 高 * 格尺寸);   // 投影贴格（旋转后）
    }

    // R 键检测（拖拽中旋转预览）：兼容新(InputSystem)/旧(Input Manager)
    private bool 检测按R()
    {
        bool 按下 = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) 按下 = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.R)) 按下 = true;
#endif
        return 按下;
    }

    // 结束拖拽：计算落格 → 空格=移动(带旋转) / 被占=换位 / 网格外=取消
    private void 结束拖拽(PointerEventData 事件)
    {
        var 源 = 拖拽源;
        拖拽源 = null;
        if (拖拽代理 != null) { Destroy(拖拽代理.gameObject); 拖拽代理 = null; }
        if (落点投影 != null) { Destroy(落点投影.gameObject); 落点投影 = null; }
        if (原位置影子 != null) { Destroy(原位置影子); 原位置影子 = null; }
        if (源 == null) return;
        if (!屏幕到容器相对(事件, out var 相对, out var 容器尺寸)) return;
        // 与拖拽中一致：物品中心对齐（四舍五入，落格 = 拖拽时看到的投影位置）
        var 形状 = 档案.形状解析?.Invoke(源.标识) ?? new 物品形状(1, 1);
        int 物宽 = 拖拽旋转 ? 形状.高 : 形状.宽;
        int 物高 = 拖拽旋转 ? 形状.宽 : 形状.高;
        float 相对顶 = 容器尺寸.y - 相对.y;
        int 列 = Mathf.RoundToInt((相对.x - 物宽 * 格尺寸 / 2f) / 格尺寸);
        int 行 = Mathf.RoundToInt((相对顶 - 物高 * 格尺寸 / 2f) / 格尺寸);
        if (列 < 0 || 行 < 0 || 列 >= 档案.网格列 || 行 >= 档案.网格行)
        {
            音效管理器.实例?.播放失败();   // 拖出网格外 = 取消
            return;
        }
        var 目标物品 = 该格物品(列, 行);
        if (目标物品 != null && 目标物品 != 源)
        {
            // 同标识可堆叠 → 合并（并入目标，超上限余量留在原格）；否则尝试换位
            if (档案.合并堆叠(目标物品, 源) > 0) 音效管理器.实例?.播放成功();
            else if (档案.换位(源, 目标物品)) 音效管理器.实例?.播放成功();
            else 音效管理器.实例?.播放失败();
        }
        else
        {
            if (档案.移动堆叠(源, 列, 行, 拖拽旋转)) 音效管理器.实例?.播放成功();
            else 音效管理器.实例?.播放失败();
        }
        刷新网格();
    }

    // 该格被哪个物品覆盖（左上角或身体格都算）
    private 物品堆叠 该格物品(int 列, int 行)
    {
        if (档案.形状解析 == null) return null;
        foreach (var 堆叠 in 档案.背包)
        {
            if (堆叠 == null || 堆叠.列 < 0) continue;
            var 形状 = 档案.形状解析(堆叠.标识);
            int 宽 = 堆叠.旋转 ? 形状.高 : 形状.宽;
            int 高 = 堆叠.旋转 ? 形状.宽 : 形状.高;
            if (列 >= 堆叠.列 && 列 < 堆叠.列 + 宽 && 行 >= 堆叠.行 && 行 < 堆叠.行 + 高) return 堆叠;
        }
        return null;
    }

    // ===== 操作按钮 =====

    private void 使用选中()
    {
        if (选中 == null) return;
        if (!数据.物品.TryGetValue(选中.标识, out var 物品) || 物品.恢复量 <= 0) { 音效管理器.实例?.播放失败(); return; }
        面板操作.使用恢复(档案, 数据, 选中.标识);
        选中 = null;
        刷新网格();
    }

    private void 装备选中()
    {
        if (选中 == null) return;
        if (!数据.物品.TryGetValue(选中.标识, out var 物品) || string.IsNullOrEmpty(物品.槽位)) { 音效管理器.实例?.播放失败(); return; }
        if (面板操作.换装(档案, 物品)) 选中 = null;   // 换装成功物品已扣
        刷新网格();
    }

    // ===== 信息 =====

    private void 刷新信息()
    {
        if (信息条 != null)
        {
            int 已用 = 0;
            if (档案.形状解析 != null)
                foreach (var 堆叠 in 档案.背包)
                    if (堆叠 != null && 堆叠.列 >= 0)
                    {
                        var 形状 = 档案.形状解析(堆叠.标识);
                        int 宽 = 堆叠.旋转 ? 形状.高 : 形状.宽;
                        int 高 = 堆叠.旋转 ? 形状.宽 : 形状.高;
                        已用 += 宽 * 高;
                    }
            设文本(信息条, $"已用 {已用}/{档案.网格列 * 档案.网格行} 格 · 负重 {档案.负重占用}/{档案.负重上限}");
        }
        刷新详情();
    }

    private void 刷新详情()
    {
        if (详情文本 == null) return;
        if (选中 == null) { 设文本(详情文本, ""); return; }
        if (!数据.物品.TryGetValue(选中.标识, out var 物品)) { 设文本(详情文本, 选中.标识); return; }
        var 形状 = 档案.形状解析?.Invoke(选中.标识) ?? new 物品形状(1, 1);
        string 数值 = "";
        if (物品.攻击加成 > 0) 数值 += $"攻击 {物品.攻击加成}  ";
        if (物品.防御加成 > 0) 数值 += $"防御 {物品.防御加成}  ";
        if (物品.生命加成 > 0) 数值 += $"生命 {物品.生命加成}  ";
        if (物品.恢复量 > 0) 数值 += $"恢复 {物品.恢复量}（{物品.恢复目标}）";
        string 操作提示 = 物品.恢复量 > 0 ? "双击使用" : (!string.IsNullOrEmpty(物品.槽位) ? "双击装备" : "");
        int 上限 = 档案.堆叠上限(选中.标识);
        string 堆叠文本 = 上限 > 1 ? $"堆叠 {选中.数量}/{上限}" : $"数量 {选中.数量}";
        string 品质名称行 = 物品工具.品质名称(有效品质(选中, 物品), 物品.名称);
        设文本(详情文本,
            $"<b>{品质名称行}</b>（{物品.类型}）\n{物品.描述}\n{数值}\n形状 {形状.宽}×{形状.高} · 重量 {物品.重量} · 价值 {物品.价值} · {堆叠文本}\n{操作提示}");
    }

    // ===== 内部组件：非按钮点击 + 拖拽 =====

    private sealed class 物品点击 : MonoBehaviour, IPointerClickHandler
    {
        public 物品堆叠 堆叠;
        public 网格背包面板 面板;
        public void OnPointerClick(PointerEventData 事件)
        {
            if (堆叠 != null && 面板 != null) 面板.物品被点击(堆叠, 事件.clickCount);
        }
    }

    private sealed class 物品拖拽 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public 物品堆叠 堆叠;
        public 网格背包面板 面板;
        public void OnBeginDrag(PointerEventData 事件) { if (堆叠 != null && 面板 != null) 面板.开始拖拽(堆叠, 事件); }
        public void OnDrag(PointerEventData 事件) { if (面板 != null) 面板.拖拽移动(事件); }
        public void OnEndDrag(PointerEventData 事件) { if (面板 != null) 面板.结束拖拽(事件); }
    }
}
