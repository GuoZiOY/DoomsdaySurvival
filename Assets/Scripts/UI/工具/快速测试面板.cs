using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

// 快速测试面板：代码动态搭建的开发者测试面板（编辑器与正式包均可用——打包后 F1/测试按钮 继续调试）。
// 屏幕顶部边缘常驻「测试」开关按钮 → 展开/收起 测试按钮列表，点击即执行对应测试操作
// （装备/背包相关：开背包面板、加随机物品、加容器套装、清空背包、随机穿戴容器、加食物、加医疗品、随机装备武器防具、恢复生存状态）。
// 由 玩家输入系统.Awake 调用 确保存在() 自动创建；画布置顶，独立于场景 UI 走线。
public sealed class 快速测试面板 : MonoBehaviour
{
    public static 快速测试面板 实例 { get; private set; }

    private RectTransform 画布;
    private RectTransform 测试组;   // 按钮+面板 的公共父级：拖拽整体移动；面板与按钮同级，按钮悬停缩放不波及面板
    private RectTransform 面板根;

    // 创建唯一实例（挂 DontDestroyOnLoad：跨场景常驻）
    public static void 确保存在()
    {
        if (实例 != null) return;
        var 物体 = new GameObject("快速测试面板");
        Object.DontDestroyOnLoad(物体);
        物体.AddComponent<快速测试面板>();
    }

    void Awake()
    {
        if (实例 != null && 实例 != this) { Destroy(gameObject); return; }
        实例 = this;

        // 画布：ScreenSpaceOverlay + 高排序，盖在所有场景面板之上
        var 画布物体 = new GameObject("画布", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        画布物体.transform.SetParent(transform, false);
        var 画布组件 = 画布物体.GetComponent<Canvas>();
        画布组件.renderMode = RenderMode.ScreenSpaceOverlay;
        画布组件.sortingOrder = 32000;
        画布 = 画布物体.GetComponent<RectTransform>();
        // 场景无 EventSystem 时补一个（保证按钮可点击）
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var 事件物体 = new GameObject("事件系统", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(事件物体);
        }

        创建开关按钮();
        创建面板();
    }

    // ===== UI 搭建 =====

    // 测试组（按钮+面板 公共父级）+ 顶部居中开关按钮：点击展开/收起；按钮/面板均可拖拽整体平滑移动
    private void 创建开关按钮()
    {
        // 测试组：锚点屏幕中心（拖拽绝对跟踪用）；按钮与面板为同级子节点
        测试组 = new GameObject("测试组", typeof(RectTransform)).GetComponent<RectTransform>();
        测试组.SetParent(画布, false);
        测试组.anchorMin = new Vector2(0.5f, 0.5f);
        测试组.anchorMax = new Vector2(0.5f, 0.5f);
        测试组.pivot = new Vector2(0.5f, 0.5f);
        测试组.sizeDelta = new Vector2(340f, 620f);
        float 屏幕高 = 画布.rect.height > 0 ? 画布.rect.height : Screen.height;
        测试组.anchoredPosition = new Vector2(0f, 屏幕高 / 2f - 186f);   // 初始：按钮贴近顶部居中

        var 按钮 = 创建按钮(测试组, "测试开关", "测试", () => 面板根.gameObject.SetActive(!面板根.gameObject.activeSelf));
        var 矩形 = 按钮.GetComponent<RectTransform>();
        矩形.anchorMin = new Vector2(0.5f, 0.5f);
        矩形.anchorMax = new Vector2(0.5f, 0.5f);
        矩形.pivot = new Vector2(0.5f, 0.5f);
        矩形.anchoredPosition = new Vector2(0f, 150f);   // 面板上方
        矩形.sizeDelta = new Vector2(110f, 48f);
        // 拖拽按钮 → 测试组整体移动（绝对跟踪鼠标，1:1 平滑跟手）
        var 拖拽 = 按钮.gameObject.AddComponent<测试组拖拽>();
        拖拽.初始化(测试组, 画布);
    }

    // 面板：标题 + 可滚动测试按钮列表（默认收起）。测试组子节点（与按钮同级，不受按钮悬停缩放影响）
    private void 创建面板()
    {
        面板根 = new GameObject("面板", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        面板根.SetParent(测试组, false);
        面板根.anchorMin = new Vector2(0.5f, 0.5f);
        面板根.anchorMax = new Vector2(0.5f, 0.5f);
        面板根.pivot = new Vector2(0.5f, 0.5f);
        面板根.anchoredPosition = new Vector2(0f, -150f);   // 按钮下方
        面板根.sizeDelta = new Vector2(340f, 560f);
        面板根.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.09f, 0.96f);
        // 拖拽面板背景/标题 → 测试组整体移动
        var 拖拽 = 面板根.gameObject.AddComponent<测试组拖拽>();
        拖拽.初始化(测试组, 画布);

        // 标题
        var 标题 = 创建文本(面板根, "标题", "快速测试", 30f, TextAlignmentOptions.Center);
        标题.rectTransform.anchorMin = new Vector2(0, 1);
        标题.rectTransform.anchorMax = new Vector2(1, 1);
        标题.rectTransform.pivot = new Vector2(0.5f, 1);
        标题.rectTransform.offsetMin = new Vector2(0, -44f);
        标题.rectTransform.offsetMax = new Vector2(0, 0);

        // 滚动视图
        var 滚动物体 = new GameObject("滚动", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        滚动物体.transform.SetParent(面板根, false);
        var 滚动矩形 = 滚动物体.GetComponent<RectTransform>();
        滚动矩形.anchorMin = Vector2.zero;
        滚动矩形.anchorMax = new Vector2(1, 1);
        滚动矩形.offsetMin = new Vector2(8f, 8f);
        滚动矩形.offsetMax = new Vector2(-8f, -52f);   // 顶部留标题空间
        滚动物体.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        var 滚动 = 滚动物体.GetComponent<ScrollRect>();
        滚动.horizontal = false;
        滚动.scrollSensitivity = 30f;

        // 视口（裁剪）
        var 视口物体 = new GameObject("视口", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        视口物体.transform.SetParent(滚动物体.transform, false);
        var 视口矩形 = 视口物体.GetComponent<RectTransform>();
        视口矩形.anchorMin = Vector2.zero;
        视口矩形.anchorMax = Vector2.one;
        视口矩形.offsetMin = Vector2.zero;
        视口矩形.offsetMax = Vector2.zero;
        视口物体.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        滚动.viewport = 视口矩形;

        // 内容（竖直排列 + 自适应高度）
        var 内容物体 = new GameObject("内容", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        内容物体.transform.SetParent(视口物体.transform, false);
        var 内容矩形 = 内容物体.GetComponent<RectTransform>();
        内容矩形.anchorMin = new Vector2(0, 1);
        内容矩形.anchorMax = new Vector2(1, 1);
        内容矩形.pivot = new Vector2(0.5f, 1);
        内容矩形.sizeDelta = new Vector2(0, 0);
        var 布局 = 内容物体.GetComponent<VerticalLayoutGroup>();
        布局.spacing = 6f;
        var 自适应 = 内容物体.GetComponent<ContentSizeFitter>();
        自适应.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        滚动.content = 内容矩形;

        // 测试按钮
        (string, UnityAction)[] 测试项 =
        {
            ("打开背包面板", 打开背包面板),
            ("打开安全屋面板", () => 面板管理器.实例?.显示面板类型<安全屋面板>()),
            ("生成新户型", 生成新户型),
            ("加随机物品", 加随机物品),
            ("加容器套装", 加容器套装),
            ("清空背包", 清空背包),
            ("随机穿戴容器", 随机穿戴容器),
            ("仓库加物品", 仓库加物品),
            ("加建材（安全屋）", 加建材),
            ("加食物×5", 加食物),
            ("加医疗品×5", 加医疗品),
            ("随机装备武器防具", 随机装备武器防具),
            ("恢复生存状态", 恢复生存状态),
            ("打开搜索容器·鞋柜", () => 搜索面板.打开搜索("鞋柜")),
            ("打开搜索容器·冰箱", () => 搜索面板.打开搜索("家用冰箱")),
            ("打开搜索容器·工具柜", () => 搜索面板.打开搜索("工具柜")),
            ("清空搜索状态", () => ServiceRegistry.Get<搜索服务>()?.清空战局()),
        };
        foreach (var (文本, 回调) in 测试项)
        {
            var 按钮 = 创建按钮(内容物体.transform, "测试按钮", 文本, 回调);
            var 布局元素 = 按钮.gameObject.AddComponent<LayoutElement>();
            布局元素.preferredHeight = 56f;
        }

        面板根.gameObject.SetActive(false);   // 默认收起
    }

    // 通用按钮：Image 底 + 居中 TMP 文本 + 点击回调
    private Button 创建按钮(Transform 父, string 名, string 文本, UnityAction 回调)
    {
        var 物体 = new GameObject(名, typeof(RectTransform), typeof(Image), typeof(Button));
        物体.transform.SetParent(父, false);
        物体.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.22f, 1f);
        var 按钮 = 物体.GetComponent<Button>();
        按钮.onClick.AddListener(回调);

        var 文本物体 = new GameObject("文本", typeof(RectTransform), typeof(TextMeshProUGUI));
        文本物体.transform.SetParent(物体.transform, false);
        var 文本矩形 = 文本物体.GetComponent<RectTransform>();
        文本矩形.anchorMin = Vector2.zero;
        文本矩形.anchorMax = Vector2.one;
        文本矩形.offsetMin = Vector2.zero;
        文本矩形.offsetMax = Vector2.zero;
        var 文本组件 = 文本物体.GetComponent<TextMeshProUGUI>();
        文本组件.text = 文本;
        文本组件.fontSize = 30f;
        文本组件.alignment = TextAlignmentOptions.Center;
        文本组件.color = Color.white;
        文本组件.raycastTarget = false;   // 不拦截点击（点击在父按钮上）
        return 按钮;
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

    // ===== 测试操作 =====

    private void 日志(string 内容, bool 坏 = false)
        => ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(坏 ? 日志类型.反馈坏 : 日志类型.反馈, 内容));

    private 玩家档案 档案() => ServiceRegistry.Get<PlayerService>()?.档案;
    private DataService 数据() => ServiceRegistry.Get<DataService>();
    private EventBus 事件() => ServiceRegistry.Get<EventBus>();

    // 加建材：一键补齐 安全屋 家具 建造/升级 材料（测试用）
    private void 加建材()
    {
        string[] 建材 = { "木板", "钉子", "布料", "铁皮", "砖块", "绳索", "水泥袋", "电线", "零件", "煤油" };
        string[] 电器 = { "电池", "充电宝", "收音机" };
        if (数据() == null || 档案() == null) return;
        foreach (var 标识 in 建材)
            if (数据().物品.ContainsKey(标识)) 档案().放入物品(标识, 20);
        foreach (var 标识 in 电器)
            if (数据().物品.ContainsKey(标识)) 档案().放入物品(标识, 3);
        事件()?.发布(new 背包变化事件("", 0, 变化原因.获得));
        日志("[测试] 已加建材 ×20 + 电器 ×3（安全屋 建造/升级 用）。");
    }

    // 通知 UI 刷新：背包变化事件（装备区/装具区/网格）+ 属性变化事件（属性/装具区/网格）
    private void 通知刷新(玩家档案 玩家)
    {
        var 总线 = 事件();
        if (总线 == null || 玩家 == null) return;
        总线.发布(new 背包变化事件("", 0, 变化原因.获得));
        总线.发布(new 属性变化事件(玩家.体质, 玩家.力量, 玩家.智慧, 玩家.敏捷, 玩家.意志, 玩家.自由属性点));
    }

    // 重新 生成 随机 户型（安全屋 房间 布局；清空 已 建 家具——旧 布局 在 新 户型 可能 非法）
    private void 生成新户型()
    {
        var 管理器 = ServiceRegistry.Get<安全屋管理器>();
        if (管理器 == null || 档案() == null) { 日志("[测试] 安全屋管理器 未装配。", true); return; }
        管理器.重新生成户型();
        // 若 安全屋面板 当前 打开 → 强制 重建 网格（新 户型 立即 生效；未 打开 则 下次 打开 时 自动 刷新）
        var 营地 = FindFirstObjectByType<安全屋面板>();
        if (营地 != null && 营地.gameObject.activeInHierarchy) 营地.强制重建网格();
        日志($"[测试] 已生成新户型（种子 {档案().户型种子}），已建 家具 已清空。");
    }

    // 打开/关闭 持有面板
    private void 打开背包面板()
    {
        var 面板 = FindFirstObjectByType<面板管理器>();
        if (面板 == null) { 日志("[测试] 场景缺少 面板管理器。", true); return; }
        if (面板.当前显示面板 is 持有面板) { 面板.返回上一面板(); return; }
        面板.显示面板类型<持有面板>();
        if (面板.当前显示面板 is not 持有面板) 日志("[测试] 持有面板未接线到 面板管理器。", true);
    }

    // 加 1 个随机物品：统一放入（穿戴容器优先 → 仓库兜底）
    private void 加随机物品()
    {
        if (数据() == null || 档案() == null || 事件() == null) return;
        if (数据().物品.Count == 0) { 日志("[测试] 无物品数据（items.json 缺失）。", true); return; }
        var 表 = new List<string>(数据().物品.Keys);
        var 标识 = 表[Random.Range(0, 表.Count)];
        int 实际 = 档案().放入物品(标识, 1);   // 统一入口：穿戴容器 → 仓库
        事件().发布(new 背包变化事件(标识, 实际, 变化原因.获得));
        string 名称 = 数据().物品.TryGetValue(标识, out var 物) ? 物.名称 : 标识;
        if (实际 <= 0) 日志($"[测试] 穿戴容器与仓库均无空位，{名称} 放不下了！", true);
        else 日志($"[测试] 获得 {名称} ×{实际}");
    }

    // 扫描 弹挂/腰封/背包 三个穿戴容器：逐个尝试放入，返回实际放入数量（容器套装测试专用：只放穿戴容器）
    private int 放入任一穿戴容器(string 标识, int 数量)
    {
        var 玩家 = 档案();
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        if (玩家 == null || 容器服务 == null) return 0;
        int 剩余 = 数量;
        foreach (var 槽 in new[] { "弹挂", "腰封", "背包" })
        {
            if (剩余 <= 0) break;
            var 记录 = 玩家.装备.Find(e => e.槽位 == 槽);
            if (记录 == null || string.IsNullOrEmpty(记录.标识)) continue;
            var 视图 = 容器服务.穿戴容器视图(记录);
            if (视图 == null) continue;
            剩余 -= 视图.放入网格(标识, 剩余);
        }
        return 数量 - 剩余;
    }

    // 一键清空 弹挂/腰封/背包 三个穿戴容器（装备槽本身保留；仓库独立按钮）
    private void 清空背包()
    {
        var 玩家 = 档案();
        if (玩家 == null) return;
        int 容器数 = 0;
        foreach (var 槽 in new[] { "弹挂", "腰封", "背包" })
        {
            var 记录 = 玩家.装备.Find(e => e.槽位 == 槽);
            if (记录?.容器物品 == null) continue;
            容器数 += 记录.容器物品.Count;
            记录.容器物品.Clear();
        }
        int 总数 = 容器数;
        if (总数 <= 0) { 日志("[测试] 穿戴容器均为空，无需清空。"); return; }
        事件()?.发布(new 背包变化事件("", 0, 变化原因.失去));   // 触发 网格/装具区 刷新
        日志($"[测试] 已清空穿戴容器（{容器数} 件）。");
    }

    // 随机穿戴 弹挂/腰封/背包（凭空穿戴，不占背包物品）
    private void 随机穿戴容器()
    {
        if (数据() == null || 档案() == null || 事件() == null) return;
        var 玩家 = 档案();
        foreach (var 槽 in new[] { "弹挂", "腰封", "背包" })
        {
            var 候选 = new List<物品数据>();
            foreach (var 物 in 数据().物品.Values)
                if (物.槽位 == 槽 && 物.是容器) 候选.Add(物);
            if (候选.Count == 0) continue;
            玩家.装备到槽(槽, 候选[Random.Range(0, 候选.Count)].标识);
        }
        通知刷新(玩家);
        日志("[测试] 已随机穿戴 弹挂/腰封/背包。");
    }

    // 在穿戴容器（弹挂/腰封/背包）内查找指定物品堆叠（用于定位刚放入的容器并初始化）
    private 物品堆叠 在穿戴容器找(string 标识)
    {
        var 玩家 = 档案();
        if (玩家 == null) return null;
        foreach (var 槽 in new[] { "弹挂", "腰封", "背包" })
        {
            var 记录 = 玩家.装备.Find(e => e.槽位 == 槽);
            if (记录?.容器物品 == null) continue;
            var 堆叠 = 记录.容器物品.Find(s => s != null && s.标识 == 标识 && s.列 >= 0 && s.容器物品 == null);
            if (堆叠 != null) return 堆叠;
        }
        return null;
    }

    // 一键添加 弹药箱/医疗箱/战术背包（含配套物品，容器自动初始化）→ 进穿戴容器
    private void 加容器套装()
    {
        if (数据() == null || 档案() == null || ServiceRegistry.Get<容器服务>() == null || 事件() == null) return;
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        int 已加 = 0;
        // ① 弹药箱 + 子弹
        int 弹药箱实得 = 放入任一穿戴容器("弹药箱", 1);
        已加 += 弹药箱实得;
        if (弹药箱实得 > 0)
        {
            var 弹药箱 = 在穿戴容器找("弹药箱");
            if (弹药箱 != null)
            {
                容器服务.初始化容器(弹药箱);
                容器服务.打开(弹药箱).放入网格("弹药", 50);
            }
        }
        // ② 医疗箱 + 医疗品
        int 医疗箱实得 = 放入任一穿戴容器("医疗箱", 1);
        已加 += 医疗箱实得;
        if (医疗箱实得 > 0)
        {
            var 医疗箱 = 在穿戴容器找("医疗箱");
            if (医疗箱 != null)
            {
                容器服务.初始化容器(医疗箱);
                var 视图 = 容器服务.打开(医疗箱);
                视图.放入网格("绷带", 5);
                视图.放入网格("医疗包", 2);
            }
        }
        // ③ 战术背包 + 材料
        int 背包实得 = 放入任一穿戴容器("战术背包", 1);
        已加 += 背包实得;
        if (背包实得 > 0)
        {
            var 背包 = 在穿戴容器找("战术背包");
            if (背包 != null)
            {
                容器服务.初始化容器(背包);
                var 视图 = 容器服务.打开(背包);
                视图.放入网格("胶带", 10);
                视图.放入网格("零件", 10);
            }
        }
        if (已加 <= 0) { 日志("[测试] 弹挂/腰封/背包 均无空位，容器放不下！", true); return; }
        事件().发布(new 背包变化事件("弹药箱", 已加, 变化原因.获得));
        日志($"[测试] 已添加 弹药箱/医疗箱/战术背包（{已加} 件，含配套物品）。双击容器物品打开。");
    }

    // 仓库加入随机物品（测试 跨网格转移/仓库面板）
    private void 仓库加物品()
    {
        if (数据() == null || 档案() == null || 事件() == null) return;
        if (数据().物品.Count == 0) { 日志("[测试] 无物品数据。", true); return; }
        var 表 = new List<string>(数据().物品.Keys);
        var 标识 = 表[Random.Range(0, 表.Count)];
        int 实际 = 档案().仓库视图().放入网格(标识, Random.Range(1, 4));   // 仓库视图 背包绑定 仓库物品 同一引用
        事件().发布(new 背包变化事件(标识, 实际, 变化原因.获得));
        string 名称 = 数据().物品.TryGetValue(标识, out var 物) ? 物.名称 : 标识;
        if (实际 <= 0) 日志("[测试] 仓库已满，放不下了！", true);
        else 日志($"[测试] 仓库获得 {名称} ×{实际}");
    }

    // 加食物（每种 ×5，进穿戴容器）
    private void 加食物()
    {
        if (数据() == null || 档案() == null || 事件() == null) return;
        int 已加 = 0;
        foreach (var 物 in 数据().物品.Values)
            if (物.类型 == "食物")
            {
                已加 += 放入任一穿戴容器(物.标识, 5);
                if (已加 >= 30) break;
            }
        事件().发布(new 背包变化事件("面包", 已加, 变化原因.获得));
        if (已加 <= 0) 日志("[测试] 弹挂/腰封/背包 均无空位，食物放不下！", true);
        else 日志($"[测试] 已加入食物（{已加} 件）。");
    }

    // 加医疗品（每种 ×5，进穿戴容器）
    private void 加医疗品()
    {
        if (数据() == null || 档案() == null || 事件() == null) return;
        int 已加 = 0;
        foreach (var 物 in 数据().物品.Values)
            if (物.类型 == "医疗品")
            {
                已加 += 放入任一穿戴容器(物.标识, 5);
                if (已加 >= 30) break;
            }
        事件().发布(new 背包变化事件("绷带", 已加, 变化原因.获得));
        if (已加 <= 0) 日志("[测试] 弹挂/腰封/背包 均无空位，医疗品放不下！", true);
        else 日志($"[测试] 已加入医疗品（{已加} 件）。");
    }

    // 随机穿戴 主手/副手/头部/胸部/腿部/脚部/手部 各一件
    private void 随机装备武器防具()
    {
        if (数据() == null || 档案() == null || 事件() == null) return;
        var 玩家 = 档案();
        int 已穿 = 0;
        foreach (var 槽 in new[] { "主手", "副手", "头部", "胸部", "腿部", "脚部", "手部" })
        {
            var 候选 = new List<物品数据>();
            foreach (var 物 in 数据().物品.Values)
                if (物.槽位 == 槽 && 物.类型 is "武器" or "防具") 候选.Add(物);
            if (候选.Count == 0) continue;
            玩家.装备到槽(槽, 候选[Random.Range(0, 候选.Count)].标识);
            已穿++;
        }
        if (已穿 > 0)
        {
            通知刷新(玩家);
            日志($"[测试] 已随机穿戴 {已穿} 件装备。");
        }
        else 日志("[测试] 无可用装备数据。", true);
    }

    // 恢复生命 / 饱食 / 水分 / 伤病
    private void 恢复生存状态()
    {
        var 玩家 = 档案();
        if (玩家 == null) return;
        玩家.恢复生命(999);
        玩家.饱食度 = 100;
        玩家.水分度 = 100;
        玩家.疲劳 = 0;
        玩家.中毒 = 0;
        玩家.感冒 = 0;
        玩家.流血 = 0;
        玩家.骨折 = 0;
        玩家.发烧 = 0;
        事件()?.发布(new 生命变化事件(玩家.生命, 玩家.最大生命, 999));
        事件()?.发布(new 生存状态变化事件(生存状态类型.饱食度, 玩家.饱食度, 100));
        事件()?.发布(new 生存状态变化事件(生存状态类型.水分度, 玩家.水分度, 100));
        事件()?.发布(new 伤病变化事件(伤病类型.疲劳, 玩家.疲劳, 0));
        事件()?.发布(new 伤病变化事件(伤病类型.中毒, 玩家.中毒, 0));
        事件()?.发布(new 伤病变化事件(伤病类型.感冒, 玩家.感冒, 0));
        事件()?.发布(new 伤病变化事件(伤病类型.流血, 玩家.流血, 0));
        事件()?.发布(new 伤病变化事件(伤病类型.骨折, 玩家.骨折, 0));
        事件()?.发布(new 伤病变化事件(伤病类型.发烧, 玩家.发烧, 0));
        日志("[测试] 已恢复生命与生存状态。");
    }

    // 测试组拖拽：拖动 按钮/面板 → 测试组 整体平滑跟随鼠标（画布局部坐标绝对跟踪，1:1 跟手、无抓取跳变）
    private sealed class 测试组拖拽 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform 组;
        private RectTransform 画布;
        private RectTransform 面板;   // 钳制基准（懒查：面板由 创建面板 挂到组下）
        private Vector2 拖拽偏移;

        public void 初始化(RectTransform 组, RectTransform 画布)
        {
            this.组 = 组;
            this.画布 = 画布;
        }

        public void OnBeginDrag(PointerEventData 事件)
        {
            if (面板 == null && 组 != null) 面板 = 组.Find("面板") as RectTransform;
            // 绝对跟踪：拖拽偏移 = 组中心相对画布中心的偏移 - 按下点在画布局部的坐标
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(画布, 事件.position, 事件.pressEventCamera, out var 屏幕))
                拖拽偏移 = 组.anchoredPosition - 屏幕;
        }

        public void OnDrag(PointerEventData 事件)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(画布, 事件.position, 事件.pressEventCamera, out var 屏幕))
                组.anchoredPosition = 屏幕 + 拖拽偏移;
            限制在屏幕内();
        }

        public void OnEndDrag(PointerEventData 事件) { }

        // 以 面板 为基准钳制：面板整体保持屏幕内（边缘处停在屏幕边，回拖即恢复跟手）
        private void 限制在屏幕内()
        {
            if (组 == null) return;
            Vector2 尺寸 = 画布 != null ? 画布.rect.size : new Vector2(Screen.width, Screen.height);
            float 半宽 = 面板 != null && 面板.rect.width > 0 ? 面板.rect.width / 2f : 170f;
            float 半高 = 面板 != null && 面板.rect.height > 0 ? 面板.rect.height / 2f : 280f;
            Vector2 中心偏移 = 面板 != null ? 面板.anchoredPosition : new Vector2(0f, -150f);
            float 半屏宽 = 尺寸.x / 2f, 半屏高 = 尺寸.y / 2f;
            float x = Mathf.Clamp(组.anchoredPosition.x, -半屏宽 + 半宽, 半屏宽 - 半宽);
            float yMin = -半屏高 - 中心偏移.y + 半高;
            float yMax = 半屏高 - 中心偏移.y - 半高;
            float y = Mathf.Clamp(组.anchoredPosition.y, Mathf.Min(yMin, yMax), Mathf.Max(yMin, yMax));
            组.anchoredPosition = new Vector2(x, y);
        }
    }
}
