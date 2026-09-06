using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 战斗沙盒面板：即时制战斗棋盘 UI（场景手动搭建，Inspector 接线；与其它面板同模式）。
// 逻辑：订阅 战斗开始事件 布阵（棋盘/棋子/技能/道具 为动态渲染内容，代码生成到 场景提供的容器）；
//  Update 每帧 推进战斗（战斗时间 → 行动条 → 自动行动）+ 轮询渲染（位置/血条/行动条/按钮状态）；
//  目标选择：点技能/道具 → 高亮可选目标 → 点棋子/格释放；Esc/点空白 取消。
public sealed class 战斗沙盒面板 : 面板基类
{
    // —— Inspector 拖入 ——
    [SerializeField] private RectTransform 棋盘区;      // 棋盘容器（格子/棋子 动态生成到这里）
    [SerializeField] private TMP_Text 状态文本;          // 顶部：游戏时间/轮次/玩家模式
    [SerializeField] private TMP_Text 消息文本;          // 底部消息行
    [SerializeField] private Button 运动按钮;            // 运动模式循环（前进/等待/后退）
    [SerializeField] private Button 行动按钮;            // 行动模式切换（攻击/防御）
    [SerializeField] private Button 切武器按钮;
    [SerializeField] private Button 逃跑按钮;
    [SerializeField] private RectTransform 技能栏;       // 技能按钮容器（动态生成）
    [SerializeField] private RectTransform 道具栏;       // 弹挂/腰封 战斗道具按钮容器（动态生成）
    [SerializeField] private GameObject 结算层;          // 结算覆盖层（含 结算文字 + 继续按钮）
    [SerializeField] private TMP_Text 结算文字;
    [SerializeField] private Button 继续按钮;
    [SerializeField] private GameObject 目标提示层;      // 选目标提示（可选）

    // —— 动态渲染状态 ——
    private BattleService 战斗;
    private EventBus 事件;
    private readonly Dictionary<战斗单位, RectTransform> 棋子表 = new Dictionary<战斗单位, RectTransform>();
    private readonly Dictionary<战斗单位, Image> 血条表 = new Dictionary<战斗单位, Image>();
    private readonly Dictionary<战斗单位, Image> 行动条表 = new Dictionary<战斗单位, Image>();
    private readonly Dictionary<Button, string> 技能标识表 = new Dictionary<Button, string>();
    private readonly Dictionary<Button, TMP_Text> 技能文本表 = new Dictionary<Button, TMP_Text>();
    private readonly List<Button> 技能按钮列表 = new List<Button>();
    private readonly Dictionary<Button, string> 道具标识表 = new Dictionary<Button, string>();
    private readonly List<Button> 道具按钮列表 = new List<Button>();
    private readonly List<RectTransform> 格子表 = new List<RectTransform>();
    private string 待选技能;    // 目标选择中："技能标识" 或 "道具:标识"；null = 未选择
    private readonly List<战斗单位> 当前可选目标 = new List<战斗单位>();
    private readonly Queue<string> 消息队列 = new Queue<string>();
    private float 消息停留;
    private float 格宽 = 76f, 格高 = 76f;
    private readonly Color 我方色 = new Color(0.30f, 0.55f, 0.36f, 1f);
    private readonly Color 敌色 = new Color(0.62f, 0.30f, 0.28f, 1f);
    private readonly Color 可走色 = new Color(0.16f, 0.16f, 0.20f, 1f);
    private readonly Color 障碍色 = new Color(0.32f, 0.28f, 0.24f, 1f);

    void Awake()
    {
        战斗 = ServiceRegistry.Get<BattleService>();
        事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<战斗开始事件>(_ => 布阵());
        事件.订阅<战斗结束事件>(e => 显示结算(e));
        事件.订阅<战斗消息事件>(e => 排队消息(e.文本));
        运动按钮?.onClick.AddListener(() => { 战斗?.切换运动模式(); });
        行动按钮?.onClick.AddListener(() => { 战斗?.切换行动模式(); });
        切武器按钮?.onClick.AddListener(() => { 战斗?.切换武器(); });
        逃跑按钮?.onClick.AddListener(() => { 战斗?.逃跑(); });
        继续按钮?.onClick.AddListener(() => { if (结算层 != null) 结算层.SetActive(false); 战斗?.返回(); });
        if (结算层 != null) 结算层.SetActive(false);
        if (目标提示层 != null) 目标提示层.SetActive(false);
    }

    // 面板由 面板管理器 显示（打开战斗事件）；布阵走 战斗开始事件（棋盘已就绪后触发）
    protected override void 刷新(object 上下文)
    {
        if (战斗 != null && 战斗.战斗中) 布阵();   // 兜底：面板显示时战斗已在进行
    }

    public override bool 回退()
    {
        if (待选技能 != null) { 取消选择(); return true; }
        return false;
    }

    // ===== 布阵 =====

    private void 布阵()
    {
        if (战斗 == null || !战斗.战斗中) return;
        if (结算层 != null) 结算层.SetActive(false);
        待选技能 = null;
        当前可选目标.Clear();
        if (目标提示层 != null) 目标提示层.SetActive(false);
        清空格子();
        清空棋子();
        重建棋盘();
        重建技能栏();
        重建道具栏();
        刷新模式按钮();
    }

    private void 清空格子()
    {
        foreach (var 格 in 格子表) if (格 != null) Destroy(格.gameObject);
        格子表.Clear();
    }

    private void 清空棋子()
    {
        foreach (var kv in 棋子表) if (kv.Value != null) Destroy(kv.Value.gameObject);
        棋子表.Clear(); 血条表.Clear(); 行动条表.Clear();
    }

    private void 重建棋盘()
    {
        if (棋盘区 == null || 战斗 == null) return;
        int 宽 = 战斗.棋盘宽, 高 = 战斗.棋盘高;
        棋盘区.sizeDelta = new Vector2(宽 * 格宽, 高 * 格高);
        for (int 行 = 0; 行 < 高; 行++)
            for (int 列 = 0; 列 < 宽; 列++)
            {
                var 格 = new GameObject($"格_{列}_{行}", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                格.SetParent(棋盘区, false);
                格.anchorMin = new Vector2(0f, 1f);
                格.anchorMax = new Vector2(0f, 1f);
                格.pivot = new Vector2(0f, 1f);
                格.anchoredPosition = new Vector2(列 * 格宽, -行 * 格高);
                格.sizeDelta = new Vector2(格宽, 格高);
                bool 障碍 = 战斗.障碍格.Contains((列, 行));
                格.GetComponent<Image>().color = 障碍 ? 障碍色 : 可走色;
                var 按钮 = 格.gameObject.AddComponent<Button>();
                int 列副本 = 列, 行副本 = 行;
                按钮.onClick.AddListener(() => 点击格(列副本, 行副本));
                格子表.Add(格);
            }
        foreach (var 单位 in 战斗.我方) 创建棋子(单位, true);
        foreach (var 单位 in 战斗.敌方) 创建棋子(单位, false);
    }

    private void 创建棋子(战斗单位 单位, bool 我方)
    {
        var 棋子 = new GameObject(单位.名称, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
        棋子.SetParent(棋盘区, false);
        棋子.anchorMin = new Vector2(0f, 1f);
        棋子.anchorMax = new Vector2(0f, 1f);
        棋子.pivot = new Vector2(0f, 1f);
        var 底色 = 棋子.GetComponent<Image>();
        底色.color = 我方 ? 我方色 : 敌色;
        var 按钮 = 棋子.GetComponent<Button>();
        var 单位副本 = 单位;
        按钮.onClick.AddListener(() => 点击棋子(单位副本));
        棋子表[单位] = 棋子;
        var 名称 = 创建文本(棋子, "名称", 单位.名称, 16f, TextAlignmentOptions.TopLeft);
        ((RectTransform)名称.transform).anchorMin = Vector2.zero;
        ((RectTransform)名称.transform).anchorMax = Vector2.one;
        ((RectTransform)名称.transform).offsetMin = new Vector2(4f, 6f);
        ((RectTransform)名称.transform).offsetMax = new Vector2(-4f, -2f);
        var 血条 = 创建条(棋子, "血条", new Color(0.85f, 0.22f, 0.18f, 1f), 0.04f);
        血条表[单位] = 血条;
        var 行动条 = 创建条(棋子, "行动条", new Color(1f, 0.78f, 0.28f, 1f), 0.12f);
        行动条表[单位] = 行动条;
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

    // ===== 技能栏 / 道具栏（克隆共享按钮预制体到 场景容器） =====

    private void 重建技能栏()
    {
        foreach (var 按钮 in 技能按钮列表) if (按钮 != null) Destroy(按钮.gameObject);
        技能按钮列表.Clear(); 技能标识表.Clear(); 技能文本表.Clear();
        if (技能栏 == null || 战斗?.玩家 == null) return;
        foreach (var 标识 in 战斗.玩家.已学技能)
        {
            if (!ServiceRegistry.Get<DataService>().技能.TryGetValue(标识, out var 技能)) continue;
            var (按钮, 文本) = 创建动态按钮(技能栏, 技能.名称);
            if (按钮 == null) continue;
            技能按钮列表.Add(按钮);
            技能标识表[按钮] = 标识;
            技能文本表[按钮] = 文本;
            var 标识副本 = 标识;
            按钮.onClick.AddListener(() => 开始选技能(标识副本));
        }
    }

    private void 重建道具栏()
    {
        foreach (var 按钮 in 道具按钮列表) if (按钮 != null) Destroy(按钮.gameObject);
        道具按钮列表.Clear(); 道具标识表.Clear();
        if (道具栏 == null || 战斗 == null) return;
        foreach (var (堆叠, 物品) in 战斗.战斗道具清单())
        {
            var (按钮, 文本) = 创建动态按钮(道具栏, $"{物品.标识} ×{堆叠.数量}");
            if (按钮 == null) continue;
            道具按钮列表.Add(按钮);
            道具标识表[按钮] = 物品.标识;
            if (文本 != null) 文本.text = $"{物品.标识} ×{堆叠.数量}";
            var 标识副本 = 物品.标识;
            按钮.onClick.AddListener(() => 开始选道具(标识副本));
        }
    }

    private (Button, TMP_Text) 创建动态按钮(RectTransform 父, string 文字)
    {
        if (面板基类.按钮预制体 == null) { Debug.LogError("[战斗沙盒面板] 未设置 按钮预制体（面板管理器 Inspector）"); return (null, null); }
        var 物体 = Instantiate(面板基类.按钮预制体, 父, false);
        物体.SetActive(true);
        var 按钮 = 物体.GetComponent<Button>();
        if (按钮 == null) return (null, null);
        音效管理器.实例?.注册按钮(按钮);
        var 文本 = 物体.transform.Find("文字")?.GetComponent<TMP_Text>();
        if (文本 != null) 文本.text = 文字;
        return (按钮, 文本);
    }

    // ===== 目标选择 =====

    private void 开始选技能(string 标识)
    {
        if (战斗 == null) return;
        if (待选技能 == 标识) { 取消选择(); return; }
        待选技能 = 标识;
        当前可选目标.Clear();
        foreach (var 目标 in 战斗.技能可选目标(标识))
            if (目标.存活) 当前可选目标.Add(目标);
        if (目标提示层 != null) 目标提示层.SetActive(true);
        刷新可选高亮();
    }

    private void 开始选道具(string 标识)
    {
        if (战斗 == null) return;
        if (待选技能 == "道具:" + 标识) { 取消选择(); return; }
        待选技能 = "道具:" + 标识;
        当前可选目标.Clear();
        foreach (var 目标 in 战斗.道具可选目标(标识))
            if (目标.存活) 当前可选目标.Add(目标);
        if (目标提示层 != null) 目标提示层.SetActive(true);
        刷新可选高亮();
    }

    private void 取消选择()
    {
        待选技能 = null;
        当前可选目标.Clear();
        if (目标提示层 != null) 目标提示层.SetActive(false);
        刷新可选高亮();
    }

    private void 点击棋子(战斗单位 单位)
    {
        if (待选技能 == null || 战斗 == null) return;
        if (!当前可选目标.Contains(单位)) return;
        if (待选技能.StartsWith("道具:"))
        {
            string 标识 = 待选技能.Substring("道具:".Length);
            战斗.玩家主动道具(标识, 单位);
        }
        else
        {
            战斗.玩家主动技能(待选技能, 单位);
        }
        取消选择();
        重建道具栏();
    }

    private void 点击格(int 列, int 行)
    {
        if (待选技能 == null) return;
        foreach (var 单位 in 当前可选目标)
            if (单位.列 == 列 && 单位.行 == 行) { 点击棋子(单位); return; }
        取消选择();   // 点到空白格 → 取消
    }

    private void 刷新可选高亮()
    {
        foreach (var kv in 棋子表)
        {
            var 底色 = kv.Value.GetComponent<Image>();
            bool 可选 = 待选技能 != null && 当前可选目标.Contains(kv.Key);
            底色.color = 可选 ? new Color(0.95f, 0.85f, 0.3f, 1f) : (kv.Key.是否我方 ? 我方色 : 敌色);
        }
    }

    // ===== 结算 / 消息 =====

    private void 显示结算(战斗结束事件 e)
    {
        if (!gameObject.activeInHierarchy) return;
        if (结算文字 != null)
            结算文字.text = (e.胜利 ? "<color=#7fcf8f>战斗胜利</color>" : "<color=#c7473d>战斗失败</color>") + "\n" + e.结算文本;
        if (结算层 != null) 结算层.SetActive(true);
        待选技能 = null;
        当前可选目标.Clear();
        if (目标提示层 != null) 目标提示层.SetActive(false);
    }

    private void 排队消息(string 文本) => 消息队列.Enqueue(文本);

    // ===== 每帧驱动 + 渲染 =====

    void Update()
    {
        if (战斗 == null || !战斗.战斗中) return;
        战斗.推进战斗(Time.deltaTime);
        渲染();
        if (Input.GetKeyDown(KeyCode.Escape)) 取消选择();
    }

    private void 渲染()
    {
        if (战斗 == null) return;
        foreach (var 单位 in 战斗.全部单位())
        {
            if (!棋子表.TryGetValue(单位, out var 棋子)) continue;
            if (!单位.存活) { 棋子.gameObject.SetActive(false); continue; }
            棋子.gameObject.SetActive(true);
            棋子.anchoredPosition = new Vector2(单位.列 * 格宽, -单位.行 * 格高);
            棋子.sizeDelta = new Vector2(单位.身形 * 格宽, 格高);
            if (血条表.TryGetValue(单位, out var 血条)) 血条.fillAmount = 单位.最大生命 > 0 ? (float)单位.生命 / 单位.最大生命 : 0f;
            if (行动条表.TryGetValue(单位, out var 行动条)) 行动条.fillAmount = Mathf.Clamp01(单位.行动条 / 100f);
        }
        if (状态文本 != null)
            状态文本.text = $"游戏时间 {时间文本()}   第 {战斗.回合数} 轮    {运动模式名(战斗?.玩家?.运动模式 ?? 0)} · {行动模式名(战斗?.玩家?.行动模式 ?? 0)}";
        刷新模式按钮();
        // 技能冷却
        foreach (var kv in 技能文本表)
        {
            if (kv.Key == null || kv.Value == null || 战斗?.玩家 == null) continue;
            int 冷却 = 战斗.玩家.冷却剩余(技能标识表[kv.Key]);
            kv.Value.text = 冷却 > 0 ? $"{技能标识表[kv.Key]} {冷却}轮" : 技能标识表[kv.Key];
        }
        // 道具数量（耗尽按钮销毁）
        foreach (var 按钮 in new List<Button>(道具按钮列表))
        {
            if (按钮 == null) continue;
            string 标识 = 道具标识表[按钮];
            int 数量 = 道具数量(标识);
            var 文本 = 按钮.transform.Find("文字")?.GetComponent<TMP_Text>();
            if (文本 != null) 文本.text = $"{标识} ×{数量}";
            if (数量 <= 0) { 道具按钮列表.Remove(按钮); Destroy(按钮.gameObject); }
        }
        // 消息
        if (消息停留 < 3f)
        {
            消息停留 += Time.deltaTime;
            if (消息队列.Count > 0 && 消息文本 != null) 消息文本.text = 消息队列.Peek();
        }
        else if (消息队列.Count > 0)
        {
            消息队列.Dequeue();
            消息停留 = 0f;
            if (消息文本 != null) 消息文本.text = 消息队列.Count > 0 ? 消息队列.Peek() : "";
        }
    }

    private int 道具数量(string 标识)
    {
        if (战斗 == null) return 0;
        foreach (var (堆叠, _) in 战斗.战斗道具清单())
            if (堆叠.标识 == 标识) return 堆叠.数量;
        return 0;
    }

    private void 刷新模式按钮()
    {
        if (运动按钮 != null)
        {
            var 文本 = 运动按钮.GetComponentInChildren<TMP_Text>();
            if (文本 != null) 文本.text = $"运动：{运动模式名(战斗?.玩家?.运动模式 ?? 0)}";
        }
        if (行动按钮 != null)
        {
            var 文本 = 行动按钮.GetComponentInChildren<TMP_Text>();
            if (文本 != null) 文本.text = $"行动：{行动模式名(战斗?.玩家?.行动模式 ?? 0)}";
        }
    }

    private static string 运动模式名(int 模式) => 模式 switch
    {
        0 => "前进",
        1 => "等待",
        _ => "后退",
    };

    private static string 行动模式名(int 模式) => 模式 == 0 ? "攻击" : "防御";

    private string 时间文本()
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null) return "—";
        int 天 = (int)(玩家.游戏分钟数 / 1440f) + 1;
        int 时 = (int)(玩家.游戏分钟数 % 1440f / 60f);
        int 分 = (int)(玩家.游戏分钟数 % 60f);
        return $"第{天}天 {时:00}:{分:00}";
    }
}
