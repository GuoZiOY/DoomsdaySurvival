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
    private float 面板高缓存;        // 面板 尺寸（按 屏幕 高 动态；测试组/面板 布局 用）
    private float 面板宽缓存;        // 面板 宽度（按 屏幕 宽 动态；更 宽 双 列）
    private string 上次房间模板;      // 「重进上一次房间（同种子）」用：同一布局再进一次，验"翻过的柜子还是翻过的样子"
    private int 上次地点种子;

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
        float 屏高 = 画布.rect.height > 0 ? 画布.rect.height : Screen.height;
        float 屏宽 = 画布.rect.width > 0 ? 画布.rect.width : Screen.width;
        面板高缓存 = Mathf.Clamp(屏高 - 105f, 400f, 960f);   // 面板 高：按钮 下方 到 屏 底（留 边距）
        面板宽缓存 = Mathf.Clamp(屏宽 - 40f, 700f, 800f);   // 面板 宽：更 宽（双 列 布局）
        测试组.sizeDelta = new Vector2(面板宽缓存, 面板高缓存 + 65f);
        测试组.anchoredPosition = new Vector2(0f, 面板高缓存 + 5f - 屏高 / 2f);   // 面板 底 距 屏 底 10

        var 按钮 = 创建按钮(测试组, "测试开关", "测试", () => 面板根.gameObject.SetActive(!面板根.gameObject.activeSelf));
        var 矩形 = 按钮.GetComponent<RectTransform>();
        矩形.anchorMin = new Vector2(0.5f, 0.5f);
        矩形.anchorMax = new Vector2(0.5f, 0.5f);
        矩形.pivot = new Vector2(0.5f, 0.5f);
        矩形.anchoredPosition = new Vector2(0f, 40f);   // 固定：面板 顶 上方 10（不 随 面板 高）
        矩形.sizeDelta = new Vector2(130f, 50f);
        // 拖拽按钮 → 测试组整体移动（绝对跟踪鼠标，1:1 平滑跟手）
        var 拖拽 = 按钮.gameObject.AddComponent<测试组拖拽>();
        拖拽.初始化(测试组, 画布);
    }

    // 面板：标题 + 大尺寸 滚动 测试 按钮 列表（无 折叠，全部 按钮 直接 列出）。测试组子节点（与按钮同级）
    private void 创建面板()
    {
        面板根 = new GameObject("面板", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        面板根.SetParent(测试组, false);
        面板根.anchorMin = new Vector2(0.5f, 0.5f);
        面板根.anchorMax = new Vector2(0.5f, 0.5f);
        面板根.pivot = new Vector2(0.5f, 0.5f);
        面板根.anchoredPosition = new Vector2(0f, -面板高缓存 / 2f + 5f);   // 面板 底部 距 测试组 底 5
        面板根.sizeDelta = new Vector2(面板宽缓存, 面板高缓存);
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

        // 内容（横向：两 大 列；每 列 纵向 排列 分区——更 宽 面板 双 列 展示）
        var 内容物体 = new GameObject("内容", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        内容物体.transform.SetParent(视口物体.transform, false);
        var 内容矩形 = 内容物体.GetComponent<RectTransform>();
        内容矩形.anchorMin = new Vector2(0, 1);
        内容矩形.anchorMax = new Vector2(1, 1);
        内容矩形.pivot = new Vector2(0.5f, 1);
        内容矩形.sizeDelta = new Vector2(0, 0);
        var 内容布局 = 内容物体.GetComponent<HorizontalLayoutGroup>();
        内容布局.spacing = 10f;   // 两 列 间 间隔
        内容布局.childControlWidth = true;
        内容布局.childControlHeight = false;   // 列 高度 由 自身 内容 决定
        内容物体.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        滚动.content = 内容矩形;

        // 两 大 列（左：基础/制作/搜索；右：物资/时间）
        var 左列 = 创建列(内容物体.transform);
        var 右列 = 创建列(内容物体.transform);
        (string, (string, UnityAction)[])[] 左分区 =
        {
            ("大世界（100×100 网格）", new (string, UnityAction)[]
            {
                ("进入 废城", 进入大世界),
                ("在附近搜索一次", 搜索一次),
                ("显示区域解锁状态", 打印区域解锁状态),
                ("打印大世界结构（控制台）", 打印大世界结构),
            }),
            ("建筑（第 3 层）", new (string, UnityAction)[]
            {
                ("进入 便利店（西街店）", () => 进入建筑("西街便利店")),
                ("进入 公寓（西区A栋）", () => 进入建筑("西区公寓A")),
            }),
            ("区域（第 1 刀）", new (string, UnityAction)[]
            {
                ("进入 西区", () => 进入区域("西区")),
                ("进入 东区", () => 进入区域("东区")),
                ("打印区域结构（控制台）", 打印区域结构),
            }),
            ("房间（第 1 刀）", new (string, UnityAction)[]
            {
                ("进入 便利店门厅", () => 进入房间("便利店_门厅")),
                ("进入 药店后间", () => 进入房间("药店_后间")),
                ("进入 居民房客厅", () => 进入房间("居民房_客厅")),
                ("进入 便利店库房", () => 进入房间("便利店_库房")),
                ("进入 药店柜台", () => 进入房间("药店_柜台")),
                ("进入 居民房厨房", () => 进入房间("居民房_厨房")),
                ("进入 居民房杂物间", () => 进入房间("居民房_杂物间")),
                ("重进上一次房间（同种子）", 重进上一次房间),
                ("跳到白天 07:00", () => 跳到时段(true)),
                ("跳到夜晚 19:00", () => 跳到时段(false)),
                ("打印房间结构（控制台）", 打印房间结构),
            }),
            ("基础", new (string, UnityAction)[]
            {
                ("打开背包面板", 打开背包面板),
                ("回安全屋（结束这一趟）", 回安全屋),
                ("发一套开锁家当", 发开锁家当),
                ("生成新户型", 生成新户型),
                ("恢复生存状态", 恢复生存状态),
                ("清空背包", 清空背包),
            }),
            ("制作面板", new (string, UnityAction)[]
            {
                ("工作台", () => 打开制作测试("工作台")),
                ("灶台", () => 打开制作测试("灶台")),
                ("医疗站", () => 打开制作测试("医疗站")),
            }),
            // 弹匣 / 敌人词缀（v51 刀46）：这两条也是"离线测不了"的链，各给一个一键入口
            ("弹匣 / 敌人词缀", new (string, UnityAction)[]
            {
                ("装满主手弹匣", 装满弹匣),
                ("打空主手弹匣（看自动换弹）", 打空弹匣),
                ("打印弹匣状态", 打印弹匣),
                ("下一场敌人必带词缀", () => 下一场敌人必带词缀(true)),
            }),
            // 配件 / 改装（v51 刀38）：把"离线测不了的那条链"变成点一下就能看
            //   ① 造数据（配件、带配件的枪）② 直装/直卸（绕过 UI 测 Domain）③ 打印副属性（测数值链）
            ("配件 / 改装", new (string, UnityAction)[]
            {
                ("加配件×7（各槽一件）", 加配件),
                ("加改装枪（步枪+3配件）", 加改装枪),
                ("加改装护甲（胸甲+插板）", 加改装护甲),
                ("主手直装：消音器", () => 主手直装("消音器")),
                ("主手直装：红点镜", () => 主手直装("红点镜")),
                ("卸下主手全部配件", 卸主手配件),
                ("打印副属性（控制台）", 打印副属性),
            }),
            ("搜索容器", new (string, UnityAction)[]
            {
                ("鞋柜", () => 搜索面板.打开搜索("鞋柜")),
                ("家用冰箱", () => 搜索面板.打开搜索("家用冰箱")),
                ("工具柜", () => 搜索面板.打开搜索("工具柜")),
                ("清空搜索状态", () => ServiceRegistry.Get<搜索服务>()?.清空战局()),
            }),
            ("战斗沙盒", new (string, UnityAction)[]
            {
                ("街头遭遇战", () => 测试战斗("街头遭遇")),
                ("感染潮", () => 测试战斗("感染潮")),
                ("重装护卫", () => 测试战斗("重装护卫")),
                ("变异体Boss", () => 测试战斗("变异体Boss")),
            }),
            // 存档（v52 刀64）：JsonUtility 是 Unity 专属的 —— 往返这半边只能在这里验
            ("存档", new (string, UnityAction)[]
            {
                ("★ 存档往返自检", 存档往返自检),
                ("打印槽位摘要", 打印存档槽位),
                ("打印存档目录", 打印存档目录),
            }),
        };
        (string, (string, UnityAction)[])[] 右分区 =
        {
            ("物资", new (string, UnityAction)[]
            {
                ("加随机物品", 加随机物品),
                ("加容器套装", 加容器套装),
                ("随机穿戴容器", 随机穿戴容器),
                ("仓库加物品", 仓库加物品),
                ("加建材（安全屋）", 加建材),
                ("加食物×5", 加食物),
                ("加医疗品×5", 加医疗品),
                ("随机装备武器防具", 随机装备武器防具),
                ("加制作材料+图纸", 加制作材料),
                ("加生鲜食品（腐坏测试）", 加生鲜食品),
                ("加脏水+木炭（净化测试）", 加脏水木炭),
                ("加种子（种植测试）", 加种子),
                ("加配方书（阅读测试）", 加配方书),
                ("加烹饪食材（制作测试）", 加烹饪食材),
            }),
            ("时间", new (string, UnityAction)[]
            {
                ("推进24游戏时（腐坏测试）", 推进测试时间),
            }),
        };
        foreach (var (组名, 项) in 左分区)
            创建分区(左列, 组名, 项);
        foreach (var (组名, 项) in 右分区)
            创建分区(右列, 组名, 项);

        // 启动诊断：把实际建出来的分区打到 Console——用来判定"看不到某节"是代码没编到还是没滚动
        var 左名 = new List<string>(); var 右名 = new List<string>();
        foreach (var (组名, _) in 左分区) 左名.Add(组名);
        foreach (var (组名, _) in 右分区) 右名.Add(组名);
        Debug.Log($"[测试] 快速测试面板 分区 左列[{string.Join(" / ", 左名)}]  右列[{string.Join(" / ", 右名)}]（左列较长，需在面板里滚动）");

        面板根.gameObject.SetActive(false);   // 默认收起
    }

    // 创建 一 列（横向 内容 的 子列：纵向 排列 分区；flexibleWidth 平分 两 列）
    private RectTransform 创建列(Transform 父)
    {
        var 列 = new GameObject("列", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        列.transform.SetParent(父, false);
        var 矩形 = 列.GetComponent<RectTransform>();
        矩形.anchorMin = new Vector2(0, 1);
        矩形.anchorMax = new Vector2(1, 1);
        矩形.pivot = new Vector2(0.5f, 1);
        矩形.sizeDelta = Vector2.zero;
        var 布局 = 列.GetComponent<VerticalLayoutGroup>();
        布局.spacing = 10f;
        列.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var 元素 = 列.AddComponent<LayoutElement>();
        元素.flexibleWidth = 1f;   // 两 列 平分 宽度
        return 矩形;
    }

    // 创建 一个 功能 分区：半透明 底 块（组标题 + 组内 按钮）——大 面板 按 功能 分区，区域 间 留 间隔
    private void 创建分区(Transform 父, string 组名, (string, UnityAction)[] 项)
    {
        var 分区 = new GameObject($"分区_{组名}", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        分区.transform.SetParent(父, false);
        var 分区矩形 = 分区.GetComponent<RectTransform>();
        分区矩形.anchorMin = new Vector2(0, 1);
        分区矩形.anchorMax = new Vector2(1, 1);
        分区矩形.pivot = new Vector2(0.5f, 1);
        分区矩形.sizeDelta = Vector2.zero;
        分区.GetComponent<Image>().color = new Color(0.13f, 0.14f, 0.18f, 0.95f);   // 分区 底色（视觉 分 区）
        var 分区布局 = 分区.GetComponent<VerticalLayoutGroup>();
        分区布局.spacing = 4f;
        分区布局.padding = new RectOffset(8, 8, 8, 8);
        分区布局.childAlignment = TextAnchor.UpperCenter;   // 按钮 固定 窄宽 → 水平 居中
        分区.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 组标题（静态 文本 行）
        var 标题 = 创建文本(分区矩形, "组标题", $"—— {组名} ——", 24f, TextAlignmentOptions.Center);
        标题.rectTransform.anchorMin = new Vector2(0, 1);
        标题.rectTransform.anchorMax = new Vector2(1, 1);
        标题.rectTransform.pivot = new Vector2(0.5f, 1);
        标题.rectTransform.sizeDelta = new Vector2(0, 0);
        标题.color = new Color(0.72f, 0.78f, 0.92f, 1f);   // 组标题 高亮 色
        var 标题布局 = 标题.gameObject.AddComponent<LayoutElement>();
        标题布局.preferredHeight = 36f;

        foreach (var (文本, 回调) in 项)
        {
            var 按钮 = 创建按钮(分区.transform, "测试按钮", 文本, 回调);
            var 布局元素 = 按钮.gameObject.AddComponent<LayoutElement>();
            布局元素.preferredHeight = 44f;
            布局元素.preferredWidth = 200f;   // 按钮 不 需 全 列 宽（窄 些，居中）
            布局元素.flexibleWidth = 0f;
        }
    }

    // 通用按钮：Image 底 + 居中 TMP 文本 + 点击回调（回调 可空 = 调用方 后续 单独 挂）
    private Button 创建按钮(Transform 父, string 名, string 文本, UnityAction 回调)
    {
        var 物体 = new GameObject(名, typeof(RectTransform), typeof(Image), typeof(Button));
        物体.transform.SetParent(父, false);
        物体.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.22f, 1f);
        var 按钮 = 物体.GetComponent<Button>();
        if (回调 != null) 按钮.onClick.AddListener(回调);

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

    // 测试反馈通道：v39 起 [测试] 消息走 Console（不再污染玩家日志流——玩家日志只承载游戏内叙事/反馈）。
    // 面板内即时反馈：挂 快速测试面板 组件的物体上 可搭 反馈文本 引用位（可选，null = 仅 Console）。
    [SerializeField] private TMP_Text 反馈文本;   // 可选：代码动态搭建面板底部文本行后赋值，最新反馈面板内可见（null = 仅 Console）

    private void 日志(string 内容, bool 坏 = false)
    {
        if (坏) Debug.LogWarning(内容); else Debug.Log(内容);
        if (反馈文本 != null) 反馈文本.text = 内容;   // 面板内可见（可选：场景在面板底部搭一行文本拖入）
    }

    private 玩家档案 档案() => ServiceRegistry.Get<PlayerService>()?.档案;
    private DataService 数据() => ServiceRegistry.Get<DataService>();
    private EventBus 事件() => ServiceRegistry.Get<EventBus>();

    // 加建材：一键补齐 安全屋 家具 建造/升级 材料（测试用）
    private void 加建材()
    {
        string[] 建材 = { "木板", "钉子", "铁皮", "绳索", "钢筋", "螺钉", "螺栓", "螺母", "金属零件", "波纹软管", "管道胶带", "硅胶管" };
        string[] 电器 = { "电池", "充电宝", "收音机" };
        if (数据() == null || 档案() == null) return;
        foreach (var 标识 in 建材)
            if (数据().物品.ContainsKey(标识)) 档案().放入物品(标识, 20);
        foreach (var 标识 in 电器)
            if (数据().物品.ContainsKey(标识)) 档案().放入物品(标识, 3);
        事件()?.发布(new 背包变化事件("", 0, 变化原因.获得));
        日志("[测试] 已加建材 ×20 + 电器 ×3（安全屋 建造/升级 用）。");
    }

    // 打开 制作面板（测试）：找 安全屋 房间 里 的 制作 家具 实例 → 浮动 制作面板（若 未建 家具 → 提示）
    private void 打开制作测试(string 家具标识)
    {
        var 营地 = FindFirstObjectByType<安全屋面板>();
        if (营地 == null) { 日志("[测试] 场景缺少 安全屋面板。", true); return; }
        if (档案() == null) return;
        var 家具实例 = 档案().家具实例(家具标识);
        if (家具实例 == null) { 日志($"[测试] 安全屋 还没建 {家具标识}，先建造 再 打开。", true); return; }
        var 挂载父 = (RectTransform)营地.transform;   // 挂载父 只 用于 定位（浮动 面板 挂 Canvas 顶层）
        制作面板.创建(挂载父, 家具实例);
    }

    // 加制作材料+图纸：一键补齐 工作台/灶台/医疗站 制作 材料 与 图纸（测试用）
    private void 加制作材料()
    {
        string[] 常用 = { "木板", "钉子", "铁皮", "布料", "绳索", "管道胶带", "金属零件", "空瓶子", "火柴", "纱布", "碘伏", "医药棉花", "注射器", "医药酒精", "电池", "水", "医药箱" };
        string[] 生食材 = { "土豆", "鸡蛋", "盐", "香料", "生肉" };
        string[] 图纸 = { "军刀图纸", "消防斧图纸", "撬棍图纸", "猎弓图纸", "手弩图纸" };
        string[] 药品 = { "家用绷带", "医用绷带", "军用绷带", "家用止血带", "医用止血带", "军用止血带",
                          "家用夹板", "医用夹板", "军用夹板", "家用医疗包", "医用医疗包", "军用医疗包",
                          "创伤药", "吗啡", "肾上腺素", "抗生素", "解毒血清", "退烧药", "感冒药" };
        if (数据() == null || 档案() == null) return;
        foreach (var 标识 in 常用)
            if (数据().物品.ContainsKey(标识)) 档案().放入物品(标识, 20);
        foreach (var 标识 in 生食材)
            if (数据().物品.ContainsKey(标识)) 档案().放入物品(标识, 20);
        foreach (var 标识 in 药品)
            if (数据().物品.ContainsKey(标识)) 档案().放入物品(标识, 5);
        foreach (var 标识 in 图纸)
            if (数据().物品.ContainsKey(标识)) 档案().放入物品(标识, 1);
        事件()?.发布(new 背包变化事件("", 0, 变化原因.获得));
        日志("[测试] 已加制作材料 ×20 + 生食材 ×20 + 药品 ×5 + 图纸（工作台/灶台/医疗站 制作 用）。");
    }

    // 加生鲜食品（腐坏 测试：生肉 24h / 鸡蛋 48h / 牛奶 24h / 面包 48h——常温 很快 变质）
    private void 加生鲜食品()
    {
        string[] 生鲜 = { "生肉", "鸡蛋", "土豆", "面包", "牛奶", "果汁" };
        if (数据() == null || 档案() == null) return;
        foreach (var 标识 in 生鲜)
            if (数据().物品.ContainsKey(标识)) 档案().放入物品(标识, 5);
        事件()?.发布(new 背包变化事件("", 0, 变化原因.获得));
        日志("[测试] 已加生鲜食品 ×5（生肉/鸡蛋/土豆/面包/牛奶/果汁）——常温 24~72 游戏时 变质。");
    }

    // 加脏水+木炭（净化 测试：灶台 净化水 配方 = 脏水+木炭 → 水）
    private void 加脏水木炭()
    {
        if (数据() == null || 档案() == null) return;
        if (数据().物品.ContainsKey("脏水")) 档案().放入物品("脏水", 5);
        if (数据().物品.ContainsKey("木炭")) 档案().放入物品("木炭", 5);
        事件()?.发布(new 背包变化事件("", 0, 变化原因.获得));
        日志("[测试] 已加 脏水×5 + 木炭×5（灶台净化用）。");
    }

    // 加种子（种植 测试：土豆种子 24h / 蔬菜种子 48h 成熟）
    private void 加种子()
    {
        if (数据() == null || 档案() == null) return;
        foreach (var 标识 in new[] { "土豆种子", "蔬菜种子" })
            if (数据().物品.ContainsKey(标识)) 档案().放入物品(标识, 3);
        事件()?.发布(new 背包变化事件("", 0, 变化原因.获得));
        日志("[测试] 已加 土豆种子×3 + 蔬菜种子×3（种植箱 用）。");
    }

    // 加配方书（书籍 阅读 测试：初级/中级 美食制作——读满 成功率 判定 → 习得 灶台 配方）
    private void 加配方书()
    {
        if (数据() == null || 档案() == null || 事件() == null) return;
        int 已加 = 0;
        foreach (var 标识 in new[] { "初级美食制作", "中级美食制作" })
            if (数据().物品.ContainsKey(标识))
                已加 += 档案().放入物品(标识, 1);
        事件().发布(new 背包变化事件("", 0, 变化原因.获得));
        if (已加 <= 0) 日志("[测试] 无书籍数据（items_书籍.json 缺失）或 背包已满。", true);
        else 日志($"[测试] 已加 {已加} 本配方书——右键「阅读」→ 读满 判定 习得 灶台配方。");
    }

    // ================= 弹匣 / 敌人词缀（v51 刀46）=================
    // 这三条链离线证明不了（弹匣容量→战斗、词缀→敌人属性），所以调试面板必须能一键看到结果。

    private void 装满弹匣()
    {
        var 战斗 = ServiceRegistry.Get<BattleService>();
        if (战斗 == null || !战斗.战斗中) { 日志("[测试] 不在战斗中 —— 弹匣只在战斗里起作用（先开一场）。", true); return; }
        战斗.调试装满弹匣();
        打印弹匣();
    }

    private void 打空弹匣()
    {
        var 战斗 = ServiceRegistry.Get<BattleService>();
        if (战斗 == null || !战斗.战斗中) { 日志("[测试] 不在战斗中。", true); return; }
        战斗.调试打空弹匣();
        日志("[测试] 已把主手弹匣清空 —— 下一次射击会**自动换弹**（1.5 秒，行动条冻结）。");
    }

    private void 打印弹匣()
    {
        var 玩家 = 档案();
        if (玩家 == null) { 日志("[测试] 没有档案。", true); return; }
        foreach (var 槽 in new[] { "主手", "副手" })
        {
            var 记录 = 玩家.装备.Find(e => e != null && e.槽位 == 槽);
            if (记录 == null || string.IsNullOrEmpty(记录.标识)) continue;
            int 容量 = 玩家.装备管理.有效弹匣容量(记录);
            if (容量 <= 0) { Debug.Log($"[测试·弹匣] {槽} = {记录.标识}：无弹匣（弓弩/近战/未配容量）"); continue; }
            int 本体 = 数据()?.物品.TryGetValue(记录.标识, out var 物) == true ? 物.弹匣容量 : 0;
            int 加成 = 容量 - 本体;
            Debug.Log($"[测试·弹匣] {槽} = {记录.标识}：{记录.已装填}/{容量}（本体 {本体} + 弹匣配件 {加成}）" +
                      (记录.配件 != null && 记录.配件.Count > 0 ? "  配件：" + string.Join("、", 记录.配件.ConvertAll(c => c?.标识)) : ""));
        }
        日志("[测试] 弹匣状态已打印到 Console（含 本体/配件 拆分）。");
    }

    private void 下一场敌人必带词缀(bool 开)
    {
        var 战斗 = ServiceRegistry.Get<BattleService>();
        if (战斗 == null) { 日志("[测试] 没有战斗服务。", true); return; }
        战斗.指定下一场必带词缀(开);
        日志($"[测试] 下一场敌人**必带词缀**（一次性）；想复现同一批词缀请配合「指定下一场种子」。");
    }

    // ================= 配件 / 改装（v51 刀38） =================
    // 这一组按钮是给"离线测不了的那条链"用的：`配件槽 → 装配 → 装备管理器.副属性 → 战斗单位`。
    // 三个层次各给一个入口，出问题能立刻定位到是哪一段：
    //   · 造数据（加配件 / 加带配件的枪）→ 验 物品表/堆叠序列化
    //   · 直装直卸（绕过工作台 UI 调 Domain API）→ 验 装配规则与槽位占用
    //   · 打印副属性 → 验 数值聚合（这里是唯一能把"装了配件到底有没有加成"看出来的地方）

    // 各槽一件（覆盖 枪口/瞄具/弹匣/枪托/握把/刃口/插板 七类，且都带取舍）
    // 刃口那件原本是 磨刀石 —— 2026-09-14 磨刀石 已按拍板改成 材料/工具（不再是配件），换成 精钢刃口
    private static readonly string[] 测试配件 = { "消音器", "红点镜", "扩容弹匣", "轻量骨架托", "战术握把", "精钢刃口", "陶瓷插板" };

    private void 加配件()
    {
        if (数据() == null || 档案() == null) return;
        int 已加 = 0;
        foreach (var 标识 in 测试配件)
            if (数据().物品.ContainsKey(标识)) 已加 += 档案().放入物品(标识, 1);
        事件()?.发布(new 背包变化事件("", 0, 变化原因.获得));
        if (已加 <= 0) 日志("[测试] 没加成配件：items_配件.json 没加载 或 背包已满。", true);
        else 日志($"[测试] 已加 {已加} 件配件（各槽一件）—— 到【工作台】放材料区 + 装备一起改装。");
    }

    // 加一把**已经装好配件**的枪（用来测"拆解"方向，以及"装备后副属性是否生效"）
    private void 加改装枪()
    {
        if (数据() == null || 档案() == null) return;
        const string 枪 = "突击步枪";
        if (!数据().物品.ContainsKey(枪)) { 日志($"[测试] 物品表里没有「{枪}」——换一把枪再试。", true); return; }
        var 堆叠 = new 物品堆叠(枪, 1)
        {
            当前耐久 = 档案().有效最大耐久(枪),
            配件 = new List<配件条>
            {
                new 配件条(配件槽.枪口, "消音器"),
                new 配件条(配件槽.瞄具, "红点镜"),
                new 配件条(配件槽.枪托, "轻量骨架托"),
            },
        };
        int 实放 = 档案().持有管理.放入堆叠(堆叠);
        事件()?.发布(new 背包变化事件("", 0, 变化原因.获得));
        if (实放 <= 0) 日志("[测试] 背包放不下这把枪（先清背包）。", true);
        else 日志($"[测试] 已加「{枪}」（枪口=消音器 / 瞄具=红点镜 / 枪托=轻量骨架托）—— 装备后看 副属性；到工作台可拆解。");
    }

    private void 加改装护甲()
    {
        if (数据() == null || 档案() == null) return;
        const string 甲 = "战术背心";
        if (!数据().物品.ContainsKey(甲)) { 日志($"[测试] 物品表里没有「{甲}」。", true); return; }
        var 堆叠 = new 物品堆叠(甲, 1)
        {
            当前耐久 = 档案().有效最大耐久(甲),
            配件 = new List<配件条> { new 配件条(配件槽.插板, "陶瓷插板") },
        };
        int 实放 = 档案().持有管理.放入堆叠(堆叠);
        事件()?.发布(new 背包变化事件("", 0, 变化原因.获得));
        if (实放 <= 0) 日志("[测试] 背包放不下这件护甲。", true);
        else 日志($"[测试] 已加「{甲}」（插板=陶瓷插板）—— 装到胸部后看 防御/负重。");
    }

    // 直装：绕过工作台 UI，直接调 Domain 的 装备管理器.装配件（先判槽位是否匹配）
    private void 主手直装(string 配件标识)
    {
        var 玩家 = 档案();
        if (玩家 == null || 数据() == null) return;
        var 主手 = 玩家.装备.Find(e => e.槽位 == "主手");
        if (主手 == null || string.IsNullOrEmpty(主手.标识)) { 日志("[测试] 主手没装备武器——先装一把枪。", true); return; }
        if (!数据().物品.TryGetValue(配件标识, out var 件)) { 日志($"[测试] 没有配件「{配件标识}」。", true); return; }
        if (!数据().物品.TryGetValue(主手.标识, out var 武器)) { 日志($"[测试] 物品表里没有主手「{主手.标识}」。", true); return; }
        if (!配件槽.允许(武器, 件)) { 日志($"[测试] {配件标识}（{件.槽位}）装不到 {主手.标识} 上——槽位不匹配。", true); return; }
        if (!玩家.装备管理.装配件("主手", new 配件条(件.槽位, 配件标识)))
        { 日志($"[测试] {主手.标识} 的〔{件.槽位}〕已占用——先点「卸下主手全部配件」。", true); return; }
        事件()?.发布(new 背包变化事件("", 0, 变化原因.获得));
        事件()?.发布(new 属性变化事件(玩家.体质, 玩家.力量, 玩家.智慧, 玩家.敏捷, 玩家.意志, 玩家.自由属性点));
        日志($"[测试] 已把 {配件标识} 装到主手 {主手.标识} 的〔{件.槽位}〕——点「打印副属性」看有没有生效。");
        打印副属性();
    }

    private void 卸主手配件()
    {
        var 玩家 = 档案();
        if (玩家 == null) return;
        var 主手 = 玩家.装备.Find(e => e.槽位 == "主手");
        if (主手?.配件 == null || 主手.配件.Count == 0) { 日志("[测试] 主手上没有配件。", true); return; }
        // 先复制一份槽位清单再拆：拆的过程中 主手.配件 在变，直接遍历会漏（同一只手上多个配件时）
        var 槽们 = new List<string>();
        foreach (var c in 主手.配件) if (c != null) 槽们.Add(c.槽位);
        int 拆了 = 0; string 拒绝 = "";
        foreach (var 槽 in 槽们)
        {
            var 件 = 玩家.装备管理.卸配件("主手", 槽);
            if (件 == null) continue;
            if (档案().放入物品(件.标识, 1) < 1)
            {
                // 背包放不下 → **装回去**（调试按钮也不能把玩家的东西变没：与工作台"拆解"同一条纪律）
                玩家.装备管理.装配件("主手", 件);
                拒绝 = $"背包放不下 {件.标识}，这件没拆";
                continue;
            }
            拆了++;
        }
        事件()?.发布(new 背包变化事件("", 0, 变化原因.获得));
        事件()?.发布(new 属性变化事件(玩家.体质, 玩家.力量, 玩家.智慧, 玩家.敏捷, 玩家.意志, 玩家.自由属性点));
        日志($"[测试] 已从主手拆下 {拆了} 个配件（已放回背包）{(拒绝.Length > 0 ? "；" + 拒绝 : "")}。", 拒绝.Length > 0);
        打印副属性();
    }

    // 打印副属性：**两段都打** ——
    //   ① 装备管理器 的拆分（本体 X + 配件 Y），能看出聚合对不对；
    //   ② 直接 `战斗单位.从玩家投影` 的**实际战斗数值**，能看出"进战斗到底有没有吃上"
    //      （这条链离线只能证明编译过，所以调试面板必须能一眼看到）。
    private void 打印副属性()
    {
        var 玩家 = 档案();
        if (玩家 == null || 数据() == null) { 日志("[测试] 没有档案。", true); return; }
        var 行 = new List<string>();
        foreach (加成类型 类 in System.Enum.GetValues(typeof(加成类型)))
        {
            int 本体 = 玩家.装备管理.本体副属性(类);
            int 配件 = 玩家.装备管理.配件加成(类);
            if (本体 == 0 && 配件 == 0) continue;
            string 尾 = 配件加成.是百分比(类) ? "%" : "";
            行.Add($"{配件加成.名(类)} {本体 + 配件}{尾}（本体 {本体} + 配件 {配件}）");
        }
        Debug.Log($"[测试·副属性] 装备提供：{(行.Count > 0 ? string.Join("　", 行) : "（无）")}");
        Debug.Log($"[测试·副属性] 装备管理器：近战 {玩家.近战伤害} / 枪械 {玩家.枪械伤害} / 防御 {玩家.总防御} / 暴击 {玩家.暴击概率:0.00} / 闪避 {玩家.闪避概率:0.00} / 负重上限 {玩家.负重上限}");
        var 单位 = 战斗单位.从玩家投影(玩家);
        Debug.Log($"[测试·副属性] 进战斗后（从玩家投影）：生命 {单位.生命}/{单位.最大生命} · 近战 {单位.当前近战} · 远程 {单位.当前远程} · 物防 {单位.基础物防} · 速度 {单位.基础速度} · 命中 {单位.命中率:0.00} · 暴击 {单位.暴击率:0.00} · 闪避 {单位.闪避率:0.00} · 抗性 {单位.抗性百分比}");
        foreach (var e in 玩家.装备)
        {
            if (e == null || string.IsNullOrEmpty(e.标识)) continue;
            var 槽 = new List<string>();
            if (e.配件 != null)
                foreach (var c in e.配件)
                    if (c != null) { 数据().物品.TryGetValue(c.标识, out var 件); 槽.Add($"〔{c.槽位}〕{c.标识}{物品工具.配件加成文本(件)}"); }
            Debug.Log($"[测试·副属性] {e.槽位} = {e.标识}（耐久 {e.当前耐久}）{(槽.Count > 0 ? "  " + string.Join("  ", 槽) : "  （无配件）")}");
        }
        日志("[测试] 副属性已打印到 Console（装备拆分 + 进战斗后的实际数值）。");
    }

    // 加烹饪食材（读书 习得 后 制作 测试 用：灶台 配方 的 材料）
    private void 加烹饪食材()
    {
        if (数据() == null || 档案() == null || 事件() == null) return;
        string[] 食材 = { "土豆", "鸡蛋", "火柴", "生肉", "盐", "面包", "速溶咖啡", "脏水", "木炭" };
        int 已加 = 0;
        foreach (var 标识 in 食材)
            if (数据().物品.ContainsKey(标识))
                已加 += 放入任一穿戴容器(标识, 3);
        事件().发布(new 背包变化事件("", 0, 变化原因.获得));
        if (已加 <= 0) 日志("[测试] 穿戴容器均无空位，烹饪食材放不下！", true);
        else 日志($"[测试] 已加烹饪食材 ×3（土豆/鸡蛋/火柴/生肉/盐/面包/速溶咖啡/脏水/木炭）。");
    }

    // 推进 24 游戏时（腐坏 测试：生肉 24h 常温 → 变质；冰箱 内 倍率 3 → 不坏）
    private void 推进测试时间()
    {
        if (档案() == null) return;
        档案().游戏分钟数 += 24 * 60f;   // 24 游戏时
        ServiceRegistry.Get<世界时间管理器>()?.结算时间段(24 * 60, false);   // 腐坏/副作用 结算（不含 饱水）
        ServiceRegistry.Get<世界时间管理器>()?.同步整点基准();   // 防 挂机 驱动 重复 结算
        ServiceRegistry.Get<EventBus>()?.发布(new 背包变化事件("", 0, 变化原因.获得));
        ServiceRegistry.Get<EventBus>()?.发布(new 时间变化事件(档案().游戏分钟数));
        日志("[测试] 已推进 24 游戏时（腐坏/伤病/疲劳 结算）。");
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

    // 回安全屋：**必须走 打开营地事件**（= 地图服务.返回营地 的同一条路）——房间层的"这一趟结束"就挂在这个事件上
    //（容器战局临时数据与门锁状态在这一刻清空；直接 显示面板类型<安全屋面板>() 会绕过它，测不出清空时机）
    private void 回安全屋()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        if (事件 == null) { 日志("[测试] 事件总线未装配。", true); return; }
        事件.发布(new 打开营地事件());
        日志("[测试] 回安全屋：这一趟结束（房间容器的战局临时数据 + 门锁状态已清空）。");
    }

    // 发一套开锁家当：三把门钥匙（每把只开它那扇门，用掉就没了）+ 一件撬锁工具
    private void 发开锁家当()
    {
        var 档 = 档案();
        if (档 == null || 数据() == null) { 日志("[测试] 档案未装配。", true); return; }
        var 给 = new List<string> { "库房钥匙", "柜台钥匙", "杂物间钥匙", "撬棍" };
        var 实得 = new List<string>();
        foreach (var 标识 in 给)
        {
            if (!数据().物品.ContainsKey(标识)) continue;
            if (档.放入物品(标识, 1) > 0) 实得.Add(标识);
        }
        事件()?.发布(new 背包变化事件("", 0, 变化原因.获得));
        日志(实得.Count > 0 ? $"[测试] 发了：{string.Join(" / ", 实得)}（钥匙开一次就消耗；撬锁要看成功率，失败扣工具耐久）"
                            : "[测试] 一个都没发出去——背包与仓库都满了。", 实得.Count == 0);
    }

    // 测试战斗：先随机装备武器防具（保证有武器/攻击距离），补基础技能，再开即时制战斗沙盒
    private void 测试战斗(string 敌人组)
    {
        var 战斗服务 = ServiceRegistry.Get<BattleService>();
        if (战斗服务 == null || 数据() == null || 档案() == null) { 日志("[测试] 战斗服务未装配。", true); return; }
        if (!数据().敌人组.ContainsKey(敌人组)) { 日志($"[测试] 敌人组 {敌人组} 不存在。", true); return; }
        随机装备武器防具();
        // 补 测试 技能包：学习 + 强制 上槽（槽满 顶 末位；想换 组合 改 此 数组 即可）
        var 测试技能包 = new[] { "重击", "蓄力重斩", "砸晕", "破甲斩", "绷带包扎", "推进" };
        var 测试槽 = 档案().战斗技能槽;
        if (测试槽 == null) 测试槽 = 档案().战斗技能槽 = new System.Collections.Generic.List<string>();
        foreach (var 标识 in 测试技能包)
        {
            if (!数据().技能.TryGetValue(标识, out var 技能)) continue;
            if (!档案().已学技能.Exists(s => s.标识 == 标识)) 档案().成长管理.学习技能(技能);
            if (测试槽.Contains(标识)) continue;
            int 空位 = 测试槽.FindIndex(s => string.IsNullOrEmpty(s));
            if (空位 >= 0) 测试槽[空位] = 标识;
            else if (测试槽.Count < 6) 测试槽.Add(标识);
            else 测试槽[测试槽.Count - 1] = 标识;   // 顶掉 末位，保 测试 可用
        }
        // 弹挂/腰封 若未穿戴则随机穿戴（战斗道具来源）
        if (!档案().装备.Exists(e => e.槽位 == "弹挂" && !string.IsNullOrEmpty(e.标识))) 随机穿戴容器();
        放入任一穿戴容器("自制弹药", 24);   // 测 弹药/远程 消耗（通用 弹药 兜底）
        放入任一穿戴容器("家用绷带", 4);       // 测 消耗 型 技能（绷带包扎 需 家用绷带）
        战斗服务.开始战斗(敌人组, "", "");
        日志($"[测试] 开始战斗：{敌人组}。");
    }

    // 建筑（第 3 层）：进一栋楼（落在有大门那层），走到楼梯格右键 上楼/下楼
    private void 进入建筑(string 建筑标识)
    {
        var 服务 = ServiceRegistry.Get<房间探索服务>();
        if (服务 == null) { 日志("[测试] 房间探索服务未装配。", true); return; }
        if (数据() == null || !数据().建筑模板.ContainsKey(建筑标识)) { 日志($"[测试] 建筑模板 {建筑标识} 不存在。", true); return; }
        int 种子 = Random.Range(1, int.MaxValue);
        if (!服务.进入建筑(建筑标识, 种子)) { 日志($"[测试] 进入 {建筑标识} 失败（看 Console）。", true); return; }
        日志($"[测试] 进入建筑：{建筑标识}（种子 {种子}）——走到楼梯格（外墙上凸出来的那一格）右键 上楼/下楼。");
    }

    // ================= 大世界（100×100 格子网格） =================
    // 说明：大世界面板（UI）是下一刀（刀4）。在那之前**用这里**验证大世界逻辑：
    //   服务（大世界探索服务）与数据（世界.json / 大世界生成器）都已就位，缺的只是那一层画面 + 图层虚拟化。

    // 进入大世界：种子取自 玩家档案.世界种子 → 同一存档每次进来都是**同一座废城**
    private void 进入大世界()
    {
        var 服务 = ServiceRegistry.Get<大世界探索服务>();
        if (服务 == null) { 日志("[测试] 大世界探索服务未装配。", true); return; }
        if (!服务.打开默认世界()) { 日志("[测试] 进入大世界失败（看 Console：Data/世界.json 没读到？）。", true); return; }
        日志($"[测试] 已进入大世界「{服务.当前世界标识}」（种子 {服务.当前种子}）"
           + "——走到区域门口那一格进副本、走到安全屋门口那一格回营地；走一格 5 游戏分钟。");
    }

    // 在附近搜索一次（有代价：行动点 + 游戏分钟；有概率翻不出来 —— 那也算正常结果）
    private void 搜索一次()
    {
        var 服务 = ServiceRegistry.Get<大世界探索服务>();
        if (服务 == null) { 日志("[测试] 大世界探索服务未装配。", true); return; }
        if (!服务.探索中) { 日志("[测试] 还没进大世界（先点「进入 废城」）。", true); return; }
        bool 成 = 服务.搜索();
        日志(成 ? "[测试] 翻出一间临时建筑了 —— 走到它门口那一格进去看看（进去一趟就塌）。"
                : "[测试] 这次没翻出东西（正常：有概率，且已扣行动点/时间）。");
    }

    // 各区域的解锁状态（一眼看出"哪片进得去、为什么进不去"）
    private void 打印区域解锁状态()    {
        var 服务 = ServiceRegistry.Get<大世界探索服务>();
        var 世 = 服务?.当前大世界;
        if (世 == null) { 日志("[测试] 当前不在大世界里。", true); return; }
        foreach (var 区 in 大世界生成器.区域列表(世))
        {
            var 门 = 大世界生成器.区域门口格(区);
            string 缘由 = 服务.未解锁缘由(区);
            日志($"[测试] {区.名称} {区.宽}×{区.高}@({区.列},{区.行}) 门口({门.列},{门.行}) → "
               + (string.IsNullOrEmpty(缘由) ? "可进" : $"锁着：{缘由}"));
        }
    }

    // 打印大世界结构：100×100 太大 → 按 4 格降采样（与 Tools/大世界验证 同一套符号，方便对照）
    private void 打印大世界结构()
    {
        var 服务 = ServiceRegistry.Get<大世界探索服务>();
        var 世 = 服务?.当前大世界;
        if (世 == null) { 日志("[测试] 当前不在大世界里。", true); return; }

        const int 步 = 4;
        var 缓冲 = new System.Text.StringBuilder();
        缓冲.AppendLine($"[大世界] {服务.信息条()}");
        缓冲.AppendLine($"[视野] 时段 {视野规则.时段名(服务.当前时段)} · 视野边长 {世.视野边长} · "
                      + $"玩家({服务.玩家列},{服务.玩家行}) · 每格 {服务.移动游戏分钟} 游戏分钟");

        var 区们 = 大世界生成器.区域列表(世);
        var 门集 = new System.Collections.Generic.HashSet<int>();
        foreach (var 区 in 区们) { var m = 大世界生成器.区域门口格(区); 门集.Add(网格数据.编码(m.列, m.行)); }
        var 营 = 大世界生成器.营地实体(世);
        int 营门码 = 营 != null ? 营.门口格 : -1;
        var 你 = 世.玩家();

        缓冲.AppendLine($"  降采样 1/{步}（每格 = 世界上 {步}×{步} 格）：# 边界　C 营地　B 区域　+ 区域门　o 障碍　@ 你　· 空地");
        for (int br = 0; br < 世.行; br += 步)
        {
            var 行 = new System.Text.StringBuilder("  ");
            for (int bc = 0; bc < 世.列; bc += 步)
            {
                char 符 = '·';
                for (int r = br; r < System.Math.Min(br + 步, 世.行); r++)
                    for (int c = bc; c < System.Math.Min(bc + 步, 世.列); c++)
                    {
                        int 码 = 网格数据.编码(c, r);
                        var e = 世.格上实体(c, r);
                        char 本 = '·';
                        if (e != null)
                            本 = e.是玩家 ? '@'
                                : e.类型 == 网格实体类型.墙 ? '#'
                                : e.类型 == 网格实体类型.区域 ? 'B'
                                : e.类型 == 网格实体类型.障碍 ? 'o'
                                : e.类型 == 网格实体类型.建筑 ? 'C' : '·';
                        if (码 == 营门码) 本 = 'C';
                        if (门集.Contains(码)) 本 = '+';
                        if (大世界符号优先级(本) > 大世界符号优先级(符)) 符 = 本;
                    }
                if (你 != null && bc <= 你.列 && 你.列 < bc + 步 && br <= 你.行 && 你.行 < br + 步) 符 = '@';
                行.Append(符);
            }
            缓冲.AppendLine(行.ToString());
        }
        foreach (var 区 in 区们)
        {
            var m = 大世界生成器.区域门口格(区);
            string 缘由 = 服务.未解锁缘由(区);
            缓冲.AppendLine($"[区域] {区.名称} {区.宽}×{区.高}@({区.列},{区.行}) → 门口({m.列},{m.行})　"
                          + (string.IsNullOrEmpty(缘由) ? "可进" : $"锁着：{缘由}"));
        }
        Debug.Log(缓冲.ToString());
        日志("[测试] 大世界结构已打印到 Console。");
    }

    private static int 大世界符号优先级(char 符)
    {
        switch (符)
        {
            case '@': return 6;
            case '+': return 5;
            case 'C': return 4;
            case 'B': return 3;
            case 'o': return 2;
            case '#': return 1;
            default: return 0;
        }
    }

    // 区域（第 1 刀）：进一片区域（种子随机 → 每次进去楼的位置/种类不同）；走到楼门口的格子上就进楼
    private void 进入区域(string 区域标识)
    {
        var 服务 = ServiceRegistry.Get<区域探索服务>();
        if (服务 == null) { 日志("[测试] 区域探索服务未装配。", true); return; }
        if (数据() == null || !数据().区域模板.ContainsKey(区域标识)) { 日志($"[测试] 区域模板 {区域标识} 不存在。", true); return; }
        int 种子 = Random.Range(1, int.MaxValue);
        if (!服务.进入区域(区域标识, 种子)) { 日志($"[测试] 进入 {区域标识} 失败（看 Console）。", true); return; }
        日志($"[测试] 进入区域：{区域标识}（种子 {种子}）——走到楼门口那一格就进楼。");
    }

    // 打印区域结构：两张图 —— ① 实体：@ 你 / # 墙 / B 建筑 / o 障碍 / + 楼门（入口格）/ · 空街
    //                             ② 迷雾：· 可见 / , 已探索（白天记忆） / 空格 全黑（读法与房间层一致）
    private void 打印区域结构()
    {
        var 服务 = ServiceRegistry.Get<区域探索服务>();
        var 区 = 服务?.当前区域;
        if (区 == null) { 日志("[测试] 当前不在区域里。", true); return; }
        var 缓冲 = new System.Text.StringBuilder();
        缓冲.AppendLine($"[区域] {服务.信息条()}");
        // 视野现状：一眼看出"雾是不是真的在跟着时段切"
        缓冲.AppendLine($"[视野] 时段 {视野规则.时段名(服务.当前时段)} · 视野边长 {区.视野边长} · 玩家({服务.玩家列},{服务.玩家行})");
        var 门格 = new System.Collections.Generic.HashSet<int>();
        foreach (var 楼 in 区.取类型(网格实体类型.建筑))
        {
            var 门 = 区域生成器.建筑入口格(楼);
            门格.Add(网格数据.编码(门.列, 门.行));
            缓冲.AppendLine($"[楼] {楼.标识}「{楼.名称}」{楼.宽}×{楼.高}@{楼.列},{楼.行} → 入口格({门.列},{门.行})");
        }
        for (int r = 0; r < 区.行; r++)
        {
            var 行 = new System.Text.StringBuilder("  ");
            for (int c = 0; c < 区.列; c++)
            {
                var e = 区.格上实体(c, r);
                char 符 = '·';
                if (e != null)
                    符 = e.是玩家 ? '@'
                        : e.类型 == 网格实体类型.墙 ? '#'
                        : e.类型 == 网格实体类型.建筑 ? 'B'
                        : e.类型 == 网格实体类型.障碍 ? 'o' : '?';
                if (门格.Contains(网格数据.编码(c, r))) 符 = '+';
                行.Append(符);
            }
            缓冲.AppendLine(行.ToString());
        }
        缓冲.AppendLine("  [迷雾] · 可见 / , 已探索 / 空格 全黑：");
        for (int r = 0; r < 区.行; r++)
        {
            var 行 = new System.Text.StringBuilder("  ");
            for (int c = 0; c < 区.列; c++)
            {
                if (c == 服务.玩家列 && r == 服务.玩家行) { 行.Append('@'); continue; }
                行.Append(服务.迷雾态(c, r) switch
                {
                    网格视野.可见 => '·',
                    网格视野.已探索 => ',',
                    _ => ' ',
                });
            }
            缓冲.AppendLine(行.ToString());
        }
        Debug.Log(缓冲.ToString());
        日志("[测试] 区域结构（含迷雾）已打到 Console。");
    }

    // 房间（第 1 刀）：按模板进入一个中空大房间（种子随机 → 每次进去布局不同；同种子可复现）
    private void 进入房间(string 模板标识)
    {
        var 服务 = ServiceRegistry.Get<房间探索服务>();
        if (服务 == null) { 日志("[测试] 房间探索服务未装配。", true); return; }
        if (数据() == null || !数据().房间模板.ContainsKey(模板标识)) { 日志($"[测试] 房间模板 {模板标识} 不存在。", true); return; }
        int 种子 = Random.Range(1, int.MaxValue);
        if (!服务.进入房间(模板标识, 种子)) { 日志($"[测试] 进入 {模板标识} 失败（看 Console）。", true); return; }
        上次房间模板 = 模板标识;
        上次地点种子 = 种子;
        日志($"[测试] 进入房间：{模板标识}（种子 {种子}）。");
    }

    // 用**同一个种子**再进一次：布局与柜子位置一模一样 —— 用来验"翻过的柜子回来还是翻过的样子"（战局数据不随进出房间清）
    private void 重进上一次房间()
    {
        if (string.IsNullOrEmpty(上次房间模板)) { 日志("[测试] 还没进过房间。", true); return; }
        var 服务 = ServiceRegistry.Get<房间探索服务>();
        if (服务 == null) { 日志("[测试] 房间探索服务未装配。", true); return; }
        if (!服务.进入房间(上次房间模板, 上次地点种子)) { 日志("[测试] 重进失败（看 Console）。", true); return; }
        日志($"[测试] 重进房间：{上次房间模板}（种子 {上次地点种子}）——柜子该还是你翻过的样子。");
    }

    // 跳到"白天 07:00 / 夜晚 19:00"：只推时间（走 世界时间管理器.跳时），用来测视野与记忆的分档
    private void 跳到时段(bool 白天)
    {
        var 时钟 = ServiceRegistry.Get<世界时间管理器>();
        var 档 = 档案();
        if (时钟 == null || 档 == null) { 日志("[测试] 时间管理器或档案未装配。", true); return; }
        float 目标 = (int)(档.游戏分钟数 / 1440f) * 1440f + (白天 ? 7f : 19f) * 60f;
        float 差 = 目标 - 档.游戏分钟数;
        if (差 <= 0f) 差 += 1440f;   // 当天已过该点 → 跳次日
        时钟.跳时(差);
        float 现在 = 档.游戏分钟数 % 1440f;
        日志($"[测试] 时间跳到 {((int)(现在 / 60f)):00}:{((int)(现在 % 60f)):00}（{(白天 ? "白天" : "夜晚")}）。");
    }

    // 打印房间结构：把当前房间以文本图打到 Console（对照生成结果用；· 可见 / , 已探索 / 空格 未探索）
    // 符号：@ 你 / # 墙 / C 未搜容器 / x 已搜容器 / y 尸体 / ! 敌人 / + 内门 / D 大门（通向外面）
    //       l 内门（锁着）/ L 大门（锁着）
    private void 打印房间结构()
    {
        var 服务 = ServiceRegistry.Get<房间探索服务>();
        var 房 = 服务?.当前房间;
        if (房 == null) { 日志("[测试] 当前不在房间里。", true); return; }
        var 缓冲 = new System.Text.StringBuilder();
        缓冲.AppendLine($"[房间] {服务.信息条()}");
        foreach (var 门 in 房.门列表())
            缓冲.AppendLine($"[门] {门.标识} @{门.列},{门.行} 朝向{门.朝向} {(门.锁着 ? $"锁着（需要 {门.锁钥匙}）" : "没锁")} 通向" +
                            $"{(门.是大门 || string.IsNullOrEmpty(门.通向) ? "外面" : 服务.门去向文本(门))}");
        // 调试用：钥匙此刻在谁身上（玩家看不到这条，Console 里方便你测）
        string 持钥 = 服务.敌人持钥摘要();
        if (!string.IsNullOrEmpty(持钥))
            缓冲.AppendLine($"[钥匙] {持钥} —— 杀了**那一只**才掉（它会掉在自己的尸体战利品里）");
        else
        {
            var 锁门 = 房.门列表().Find(m => m.锁着);
            if (锁门 != null) 缓冲.AppendLine($"[钥匙] {锁门.锁钥匙} 在本房**某个可搜索容器**里（按种子挑的那个柜子）");
        }
        // 敌人名单：一只一个标识，**遭遇哪一只就打哪一只**（一场遭遇 = 一个敌人）
        var 敌人们 = 房.取类型(网格实体类型.敌人);
        if (敌人们.Count > 0)
        {
            var 名 = new List<string>();
            foreach (var 敌 in 敌人们) 名.Add($"{敌.名称}[{敌.标识}]");
            缓冲.AppendLine($"[敌人] 本房 {敌人们.Count} 只：{string.Join(" / ", 名)} —— 一场遭遇只打你碰上的那一只");
        }
        for (int 行 = 0; 行 < 房.行; 行++)
        {
            for (int 列 = 0; 列 < 房.列; 列++)
            {
                if (列 == 服务.玩家列 && 行 == 服务.玩家行) { 缓冲.Append('@'); continue; }
                var 格 = 房.格上实体(列, 行);
                if (格 != null)
                {
                    缓冲.Append(格.类型 switch
                    {
                        网格实体类型.墙 => '#',
                        网格实体类型.容器 => 格.已搜 ? 'x' : 'C',
                        网格实体类型.门 => 格.锁着 ? (格.是大门 ? 'L' : 'l') : (格.是大门 ? 'D' : '+'),
                        网格实体类型.尸体 => 'y',
                        网格实体类型.敌人 => '!',
                        _ => '?',
                    });
                    continue;
                }
                缓冲.Append(服务.迷雾态(列, 行) switch
                {
                    网格视野.可见 => '·',
                    网格视野.已探索 => ',',
                    _ => ' ',
                });
            }
            缓冲.AppendLine();
        }
        Debug.Log(缓冲.ToString());
        日志("[测试] 房间结构已打印到 Console。");
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
        string 名称 = 数据().物品.TryGetValue(标识, out var 物) ? 物.标识 : 标识;
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
        // ② 医药箱 + 医疗品
        int 医药箱实得 = 放入任一穿戴容器("医药箱", 1);
        已加 += 医药箱实得;
        if (医药箱实得 > 0)
        {
            var 医药箱 = 在穿戴容器找("医药箱");
            if (医药箱 != null)
            {
                容器服务.初始化容器(医药箱);
                var 视图 = 容器服务.打开(医药箱);
                视图.放入网格("家用绷带", 5);
                视图.放入网格("家用医疗包", 2);
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
                视图.放入网格("管道胶带", 10);
                视图.放入网格("金属零件", 10);
            }
        }
        if (已加 <= 0) { 日志("[测试] 弹挂/腰封/背包 均无空位，容器放不下！", true); return; }
        事件().发布(new 背包变化事件("弹药箱", 已加, 变化原因.获得));
        日志($"[测试] 已添加 弹药箱/医药箱/战术背包（{已加} 件，含配套物品）。双击容器物品打开。");
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
        string 名称 = 数据().物品.TryGetValue(标识, out var 物) ? 物.标识 : 标识;
        if (实际 <= 0) 日志("[测试] 仓库已满，放不下了！", true);
        else 日志($"[测试] 仓库获得 {名称} ×{实际}");
    }

    // 加食物（每种 ×5，进穿戴容器）
    private void 加食物()
    {
        if (数据() == null || 档案() == null || 事件() == null) return;
        int 已加 = 0;
        foreach (var 物 in 数据().物品.Values)
            if (物.类型 == "饮食")
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
            if (物.类型 == "医疗")
            {
                已加 += 放入任一穿戴容器(物.标识, 5);
                if (已加 >= 30) break;
            }
        事件().发布(new 背包变化事件("家用绷带", 已加, 变化原因.获得));
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

    // ================= 存档（v52 刀64） =================
    // 为什么这两颗按钮必须在游戏里：`JsonUtility` 是 Unity 专属的 —— 离线验证器（Tools/存档验证）
    // 编译不到它，所以"序列化出来的东西真能原样读回来"这半边**只能在这里验**。
    // 按钮里跑的是真的 SaveService + 真的 JsonUtility + 真的原子写盘（不碰 0~3 号槽，用 _自检.json）。

    // 存档往返自检：存 → 读 → 比指纹（内存 + 磁盘两段）
    private void 存档往返自检()
    {
        var 存档 = ServiceRegistry.Get<SaveService>();
        if (存档 == null) { 日志("[存档] 没有 SaveService（装配失败？）"); return; }
        string 结论 = 存档.往返自检();
        日志("[存档自检] " + 结论);
        if (!结论.StartsWith("通过")) Debug.LogError("[存档自检] " + 结论);
    }

    // 打印槽位摘要（槽、有没有、角色、天数、位置、时间、版本、损坏原因）+ 存档目录路径
    private void 打印存档槽位()
    {
        var 存档 = ServiceRegistry.Get<SaveService>();
        if (存档 == null) { 日志("[存档] 没有 SaveService（装配失败？）"); return; }
        Debug.Log($"[存档] 目录：{存档文件.根目录}");
        foreach (var 摘 in 存档.列出())
        {
            string 行 = 摘.有档
                ? $"{存档规格.槽名(摘.槽)}｜{摘.角色名}·{摘.职业}·Lv{摘.等级}·第{摘.游戏天数}天·{摘.位置}" +
                  $"｜{存档文件.现实时间文本(摘.保存时间)}｜v{摘.版本}" + (摘.自动 ? "｜自动" : "")
                : $"{存档规格.槽名(摘.槽)}｜空";
            if (!string.IsNullOrEmpty(摘.损坏原因)) 行 += $"｜⚠ {摘.损坏原因}";
            Debug.Log("[存档] " + 行);
        }
        日志($"[存档] 已打印 {存档规格.手动槽数 + 1} 个槽位摘要到控制台。");
    }

    // 存档目录里有什么（含 .bak / .tmp —— 排查"存了却没生效"时先看这个）
    private void 打印存档目录()
    {
        存档文件.确保目录();
        Debug.Log($"[存档] 目录：{存档文件.根目录}");
        try
        {
            var 文件们 = new System.IO.DirectoryInfo(存档文件.根目录).GetFiles();
            if (文件们.Length == 0) Debug.Log("[存档] （目录是空的）");
            foreach (var f in 文件们) Debug.Log($"[存档]   {f.Name}  {f.Length} 字节  {f.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
        }
        catch (System.Exception 异常) { Debug.LogError($"[存档] 列目录失败：{异常.Message}"); }
        日志("[存档] 已把存档目录内容打印到控制台。");
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
