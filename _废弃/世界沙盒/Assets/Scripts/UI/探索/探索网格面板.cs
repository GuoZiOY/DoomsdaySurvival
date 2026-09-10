using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 探索网格面板：世界沙盒的表现层（区域街道网格 / 楼层房间网格 共用）。
//
// 结构（场景手动搭建 + 引用位）：
//   网格容器(RectTransform，必须接；格子由代码动态生成)
//   信息条 TMP / 提示 TMP
//
// 交互（无按钮栏）：**点世界里的物件 → 弹右键小菜单**（推开/带上/上楼/下楼/进去/出来/搜刮/撤离/走过去）。
//   菜单**统一用场景里那个 右键菜单**（物品菜单同一个组件、同一套取消/定位规则），按需实例化条目；
//   菜单内容由 探索沙盒服务.目标动作(列,行) 判定，本面板只负责把条目交给菜单。
//   万一 右键菜单 没挂到场景，退化为「点一下直接执行首选动作」，不会卡住。
public sealed class 探索网格面板 : 面板基类
{
    [SerializeField] private RectTransform 网格容器;      // 格子父级
    [SerializeField] private TMP_Text 信息条文本;
    [SerializeField] private TMP_Text 提示文本;

    [SerializeField] private float 格尺寸 = 44f;          // 固定格边长（铺满容器=false 时使用）
    [SerializeField] private float 格间距 = 2f;
    [SerializeField] private bool 铺满容器 = true;         // 按 网格容器 的实际尺寸铺满（自适应大屏）

    private readonly List<Image> 格子表 = new List<Image>();
    private readonly List<TextMeshProUGUI> 格字表 = new List<TextMeshProUGUI>();
    private int 当前列, 当前行;
    private float 运行时格尺寸;
    private 沙盒显示事件 当前;

    private 探索沙盒服务 服务 => ServiceRegistry.Get<探索沙盒服务>();

    // ===== 零接线入口（调试用；也给 F2 快捷键用）=====
    // 场景里已经搭了面板 → 直接用那个；没搭 → 现建一个（自带 Canvas/网格/信息条/提示）。
    // 这样"进世界沙盒"这件事不依赖任何场景接线。
    public static 探索网格面板 确保存在()
    {
        var 已有 = Object.FindFirstObjectByType<探索网格面板>(FindObjectsInactive.Include);
        if (已有 != null) return 已有;

        var 画布 = Object.FindFirstObjectByType<Canvas>();
        if (画布 == null)
        {
            var 画布物体 = new GameObject("沙盒画布", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(GraphicRaycaster));
            画布 = 画布物体.GetComponent<Canvas>();
            画布.renderMode = RenderMode.ScreenSpaceOverlay;
            画布.sortingOrder = 31000;
        }
        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("事件系统", typeof(EventSystem), typeof(StandaloneInputModule));

        var 物体 = new GameObject("世界沙盒面板", typeof(RectTransform), typeof(Image), typeof(探索网格面板));
        物体.transform.SetParent(画布.transform, false);
        var 矩形 = 物体.GetComponent<RectTransform>();
        矩形.anchorMin = Vector2.zero; 矩形.anchorMax = Vector2.one;
        矩形.offsetMin = Vector2.zero; 矩形.offsetMax = Vector2.zero;
        物体.GetComponent<Image>().color = new Color(0.043f, 0.043f, 0.051f, 0.98f);
        return 物体.GetComponent<探索网格面板>();
    }

    // 引用位没接就现建（网格容器 / 信息条 / 提示）——手动搭建的面板走原样，这里只兜底
    private void 确保UI()
    {
        var 宿主 = transform as RectTransform;
        if (宿主 == null) return;
        if (网格容器 == null)
        {
            var 物体 = new GameObject("网格容器", typeof(RectTransform));
            物体.transform.SetParent(宿主, false);
            网格容器 = 物体.GetComponent<RectTransform>();
            网格容器.anchorMin = new Vector2(0.5f, 0.5f);
            网格容器.anchorMax = new Vector2(0.5f, 0.5f);
            网格容器.pivot = new Vector2(0.5f, 0.5f);
            网格容器.anchoredPosition = new Vector2(0f, -20f);
            if (铺满容器) 网格容器.sizeDelta = new Vector2(Mathf.Min(宿主.rect.width - 60f, 1500f), Mathf.Min(宿主.rect.height - 140f, 900f));
        }
        if (信息条文本 == null)
        {
            信息条文本 = 建文本(宿主, "信息条", new Vector2(0f, 1f), new Vector2(0f, -18f), 26f, TextAlignmentOptions.Left);
            信息条文本.color = 游戏主题.文字;
        }
        if (提示文本 == null)
        {
            提示文本 = 建文本(宿主, "提示", new Vector2(0.5f, 0f), new Vector2(0f, 24f), 24f, TextAlignmentOptions.Center);
            提示文本.color = 游戏主题.暗淡;
        }
    }

    private static TMP_Text 建文本(RectTransform 宿主, string 名, Vector2 锚, Vector2 位置, float 字号, TextAlignmentOptions 对齐)
    {
        var 物体 = new GameObject(名, typeof(RectTransform), typeof(TextMeshProUGUI));
        物体.transform.SetParent(宿主, false);
        var 矩形 = 物体.GetComponent<RectTransform>();
        矩形.anchorMin = 锚; 矩形.anchorMax = 锚;
        矩形.pivot = new Vector2(0.5f, 0.5f);
        矩形.anchoredPosition = 位置;
        矩形.sizeDelta = new Vector2(1400f, 60f);
        var 字 = 物体.GetComponent<TextMeshProUGUI>();
        字.fontSize = 字号;
        字.alignment = 对齐;
        字.raycastTarget = false;
        return 字;
    }

    // ===== 生命周期 =====

    void Awake()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件?.订阅<沙盒显示事件>(渲染);
        事件?.订阅<沙盒提示事件>(设提示);
        事件?.订阅<打开沙盒容器事件>(打开容器);   // 搜刮 → 既有搜索面板接管
    }

    void OnDestroy()
    {
        if (!ServiceRegistry.已注册<EventBus>()) return;
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.取消订阅<沙盒显示事件>(渲染);
        事件.取消订阅<沙盒提示事件>(设提示);
        事件.取消订阅<打开沙盒容器事件>(打开容器);
    }

    // 事件订阅用（与 OnDestroy 的取消订阅必须是同一个方法，不能用两处 lambda）
    private void 设提示(沙盒提示事件 e) => 设置提示文本(e.文本);
    private void 打开容器(打开沙盒容器事件 e) => 搜索面板.打开搜索(e.容器标识);
    private void 设置提示文本(string 文本) => 设文本(提示文本, 文本);

    protected override void 刷新(object 上下文)
    {
        if (上下文 is 沙盒显示事件 e) { 渲染(e); return; }
        var 服务 = this.服务;
        if (服务 != null && 服务.当前显示.格 != null) 渲染(服务.当前显示);
    }

    // 取消（Esc/侧边栏取消）：不直接撤离（避免手滑丢这一趟），只给提示
    public override bool 回退()
    {
        if (服务?.探索中 != true) return false;
        设置提示文本("要离开这片区域，请走到撤离点（出口格）再点它选「撤离」。");
        return true;
    }

    public override string 取消文本 => "返回";

    // ===== 渲染 =====

    private void 渲染(沙盒显示事件 e)
    {
        // 地图一刷新（走一步/开关门/切层）就把小菜单收掉：它指向的格子状态已经变了
        右键菜单.实例?.隐藏();
        if (e.格 == null || e.列 <= 0 || e.行 <= 0)
        {
            // 清屏事件（撤离）：收掉格子，面板自行隐藏，避免残留上一张地图
            设置提示文本(e.提示);
            for (int i = 网格容器 != null ? 网格容器.childCount - 1 : -1; i >= 0; i--) Destroy(网格容器.GetChild(i).gameObject);
            格子表.Clear(); 格字表.Clear(); 当前列 = 0; 当前行 = 0;
            if (服务?.探索中 != true) 隐藏面板();
            return;
        }
        当前 = e;
        确保UI();   // 零接线模式：引用位没接就现建（手动搭建的面板不受影响）
        设文本(信息条文本, e.信息条);
        设文本(提示文本, e.提示);
        铺格子(e.列, e.行);
        for (int 索引 = 0; 索引 < e.格.Length && 索引 < 格子表.Count; 索引++)
        {
            int 列 = 索引 % e.列, 行 = 索引 / e.列;
            var 视图 = e.格[索引];
            var 图 = 格子表[索引];
            if (图 == null) continue;
            bool 是玩家 = 列 == e.玩家列 && 行 == e.玩家行;
            var 色 = 是玩家 ? 游戏主题.金色 : 颜色(视图);
            图.color = 色;
            图.gameObject.SetActive(true);
            var 字 = 索引 < 格字表.Count ? 格字表[索引] : null;
            if (字 != null)
            {
                字.text = 是玩家 ? "@" : 记号(视图);
                字.color = 是玩家 ? 游戏主题.背景 : 记号色(视图);
            }
        }
    }

    // ===== 格子生成与点击 =====

    private void 铺格子(int 列, int 行)
    {
        if (网格容器 == null) 确保UI();
        float 容器宽 = 网格容器 != null && 网格容器.rect.width > 10f ? 网格容器.rect.width : 0f;
        float 容器高 = 网格容器 != null && 网格容器.rect.height > 10f ? 网格容器.rect.height : 0f;
        float 目标格 = 格尺寸;
        if (铺满容器 && 容器宽 > 0f && 容器高 > 0f)
            目标格 = Mathf.Max(8f, Mathf.Min((容器宽 - 格间距 * (列 - 1)) / 列, (容器高 - 格间距 * (行 - 1)) / 行));

        if (列 == 当前列 && 行 == 当前行 && Mathf.Abs(目标格 - 运行时格尺寸) < 0.5f) return;
        当前列 = 列; 当前行 = 行; 运行时格尺寸 = 目标格;

        if (网格容器 == null)
        {
            Debug.LogWarning("[沙盒面板] 网格容器 未接线：请在场景里把格子父物体拖到「网格容器」引用位");
            return;
        }
        for (int i = 网格容器.childCount - 1; i >= 0; i--) Destroy(网格容器.GetChild(i).gameObject);
        格子表.Clear(); 格字表.Clear();

        float 步长 = 运行时格尺寸 + 格间距;
        网格容器.sizeDelta = new Vector2(列 * 步长 - 格间距, 行 * 步长 - 格间距);
        for (int 行号 = 0; 行号 < 行; 行号++)
            for (int 列号 = 0; 列号 < 列; 列号++)
            {
                var 物体 = new GameObject($"格_{列号}_{行号}", typeof(RectTransform), typeof(Image), typeof(沙盒格点击));
                物体.transform.SetParent(网格容器, false);
                var 矩形 = 物体.GetComponent<RectTransform>();
                矩形.anchorMin = new Vector2(0f, 1f);
                矩形.anchorMax = new Vector2(0f, 1f);
                矩形.pivot = new Vector2(0f, 1f);
                矩形.sizeDelta = new Vector2(运行时格尺寸, 运行时格尺寸);
                矩形.anchoredPosition = new Vector2(列号 * 步长, -行号 * 步长);
                var 图 = 物体.GetComponent<Image>();
                图.color = 未探索色;
                格子表.Add(图);

                // 格上记号（ASCII，避免中文字形缺字）：字体缺字时也只是不显示，不影响可玩
                var 字物体 = new GameObject("记号", typeof(RectTransform), typeof(TextMeshProUGUI));
                字物体.transform.SetParent(物体.transform, false);
                var 字矩形 = 字物体.GetComponent<RectTransform>();
                字矩形.anchorMin = Vector2.zero; 字矩形.anchorMax = Vector2.one;
                字矩形.offsetMin = Vector2.zero; 字矩形.offsetMax = Vector2.zero;
                var 字 = 字物体.GetComponent<TextMeshProUGUI>();
                字.alignment = TextAlignmentOptions.Center;
                字.fontSize = Mathf.Max(10f, 运行时格尺寸 * 0.62f);
                字.raycastTarget = false;
                格字表.Add(字);

                // 点击：左键/右键都弹小菜单（真实物件的动作）
                var 点击 = 物体.GetComponent<沙盒格点击>();
                点击.初始化(矩形, 列号, 行号, 左键点格, 右键点格);
            }
    }

    private void 左键点格(int 列, int 行, RectTransform 格)
    {
        if (服务 == null) return;
        var 动作 = 服务.目标动作(列, 行);
        if (动作.Count == 0) { 设置提示文本(远近距离提示(列, 行)); return; }   // 太远/没东西 → 只给一句提示
        弹菜单(列, 行, 格, 动作);
    }

    private void 右键点格(int 列, int 行, RectTransform 格) => 左键点格(列, 行, 格);

    private string 远近距离提示(int 列, int 行)
    {
        var 显示 = 当前;
        int 距离 = Mathf.Abs(列 - 显示.玩家列) + Mathf.Abs(行 - 显示.玩家行);
        if (距离 == 0) return "这里没什么可做的。";
        if (距离 == 1) return "那格走不过去。";
        return "一次只能点相邻的一格。";
    }

    // ===== 小菜单：统一用场景里那个 右键菜单（物品菜单同一个组件；本面板不自建菜单、不显示标题）=====

    private void 弹菜单(int 列, int 行, RectTransform 格, List<沙盒动作项> 动作)
    {
        var 菜单 = 右键菜单.实例;
        if (菜单 == null)
        {
            服务?.执行动作(动作[0].动作, 列, 行);   // 场景里没挂 右键菜单 → 退化：首选动作直接执行
            return;
        }
        var 条目 = new List<(string 文本, System.Action 动作)>(动作.Count);
        foreach (var 项 in 动作)
        {
            if (!项.有效) continue;
            string 动作标识 = 项.动作; int 捕获列 = 列, 捕获行 = 行;
            条目.Add((项.文本, () => 服务?.执行动作(动作标识, 捕获列, 捕获行)));
        }
        菜单.显示动作(条目, 格);   // 菜单自己负责置顶/定位/取消规则/条目实例化
    }

    // ===== 取色 / 记号 =====

    private static readonly Color 未探索色 = new Color(0.035f, 0.035f, 0.045f, 1f);
    private static readonly Color 记忆压暗 = new Color(0.36f, 0.36f, 0.40f, 1f);   // 已探索但当前不可见：整体压暗
    private static readonly Color 墙色 = new Color(0.16f, 0.16f, 0.19f, 1f);
    private static readonly Color 地板色 = new Color(0.30f, 0.29f, 0.27f, 1f);
    private static readonly Color 街道色 = new Color(0.20f, 0.21f, 0.24f, 1f);
    private static readonly Color 障碍色 = new Color(0.34f, 0.26f, 0.18f, 1f);
    private static readonly Color 出口色 = new Color(0.50f, 0.68f, 0.42f, 1f);
    private static readonly Color 门关色 = new Color(0.58f, 0.36f, 0.18f, 1f);   // 关着的门：木色实心
    private static readonly Color 门开色 = new Color(0.26f, 0.20f, 0.14f, 1f);   // 开着的门：暗下去（能过去）
    private static readonly Color 楼门色 = new Color(0.85f, 0.64f, 0.25f, 1f);
    private static readonly Color 楼梯色 = new Color(0.62f, 0.55f, 0.72f, 1f);
    private static readonly Color 容器色 = new Color(0.42f, 0.66f, 0.82f, 1f);
    private static readonly Color 尸体色 = new Color(0.55f, 0.30f, 0.30f, 1f);
    private static readonly Color 敌人色 = new Color(0.88f, 0.16f, 0.12f, 1f);

    private static Color 颜色(沙盒格视图 视图)
    {
        if (!视图.已探索) return 未探索色;
        var 底色 = 底色取(视图.显示, 视图.门开);
        if (!视图.可见) return Color.Lerp(底色, 记忆压暗, 0.45f);   // 记忆态：压暗一档，仍可辨认
        return 底色;
    }

    private static Color 底色取(沙盒格显示 显示, bool 门开)
    {
        switch (显示)
        {
            case 沙盒格显示.墙: return 墙色;
            case 沙盒格显示.地板: return 地板色;
            case 沙盒格显示.街道: return 街道色;
            case 沙盒格显示.障碍: return 障碍色;
            case 沙盒格显示.出口: return 出口色;
            case 沙盒格显示.门: return 门开 ? 门开色 : 门关色;
            case 沙盒格显示.建筑入口: return 楼门色;
            case 沙盒格显示.楼梯: return 楼梯色;
            case 沙盒格显示.搜索点: return 容器色;
            case 沙盒格显示.尸体: return 尸体色;
            case 沙盒格显示.敌人: return 敌人色;
            default: return 未探索色;
        }
    }

    private static readonly Color 记号浅色 = new Color(0.90f, 0.88f, 0.82f, 1f);
    private static readonly Color 记号深色 = new Color(0.10f, 0.10f, 0.12f, 1f);

    private static string 记号(沙盒格视图 视图)
    {
        if (!视图.已探索) return "";
        switch (视图.显示)
        {
            case 沙盒格显示.障碍: return "X";
            case 沙盒格显示.出口: return "E";
            case 沙盒格显示.门: return 视图.门开 ? "/" : "|";   // | 关着 / / 开着
            case 沙盒格显示.建筑入口: return "D";
            case 沙盒格显示.楼梯: return "^";
            case 沙盒格显示.搜索点: return "?";
            case 沙盒格显示.尸体: return "+";
            case 沙盒格显示.敌人: return "!";
            default: return "";
        }
    }

    private static Color 记号色(沙盒格视图 视图) => 视图.可见 ? 记号深色 : 记号浅色;
}

// 单格点击转发：左键/右键都回调（格子上不放 Button——沙盒不用悬停变色，也不想被 Selectable 状态干扰）
public sealed class 沙盒格点击 : MonoBehaviour, IPointerClickHandler
{
    private RectTransform 矩形;
    private int 列, 行;
    private System.Action<int, int, RectTransform> 左键, 右键;

    public void 初始化(RectTransform 矩形, int 列, int 行, System.Action<int, int, RectTransform> 左键, System.Action<int, int, RectTransform> 右键)
    {
        this.矩形 = 矩形; this.列 = 列; this.行 = 行; this.左键 = 左键; this.右键 = 右键;
    }

    public void OnPointerClick(PointerEventData 事件)
    {
        if (事件.button == PointerEventData.InputButton.Right) 右键?.Invoke(列, 行, 矩形);
        else 左键?.Invoke(列, 行, 矩形);
    }
}
