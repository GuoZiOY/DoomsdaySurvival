using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 安全屋面板（营地）：玩家据点——网格摆放式 家具 建造/升级 + 睡觉/收音机。
// 房间网格 = 网格面板 组件（家具模式：家具宿主 注入）——渲染（色块+名称+等级角标）/拖拽移动换位/R旋转/落点投影
//         全部复用 网格面板（背包）能力；右键 家具 = 右键菜单 家具 模式（升级/拆除/详情）。
// 家具 = 物品堆叠（玩家档案.家具：标识 含 等级后缀"储物箱_2"；数量恒 1；列/行/旋转 = 房间网格位置）。
// 建造 = 摆放模式（跟手预览 + 网格面板 落点投影 绿/红 → 左键 落格 建造）。
// 全动态 UI（UI工具）：状态行 / 房间网格（网格面板 动态 挂载）/ 家具清单 / 操作行。
// 场景搭建：面板 物体 挂 本组件（面板基类），拖入 面板管理器.营地 引用位；无需 手动 搭 子物体。
public sealed class 安全屋面板 : 面板基类
{
    // ===== 常量 =====
    private const float 格 = 90f;                 // 单格像素（与 网格面板.格尺寸 一致）
    private const int 房间列 = 10, 房间行 = 7;     // 房间网格（70 格）
    private static readonly string[] 家具顺序 = { "床", "工作台", "储物箱", "收音机", "灶台" };   // 清单固定顺序

    private 玩家档案 玩家 => ServiceRegistry.Get<PlayerService>().档案;
    private DataService 数据 => ServiceRegistry.Get<DataService>();
    private 安全屋管理器 安全屋 => ServiceRegistry.Get<安全屋管理器>();
    private EventBus 事件 => ServiceRegistry.Get<EventBus>();

    // ===== 房间网格（网格面板 家具 模式） =====
    private 网格面板 房间网格;

    // ===== 摆放模式（建造） =====
    private GameObject 摆放预览;
    private 物品堆叠 摆放堆叠;   // 摆放 中 的 临时 家具堆叠（未 入 网格）
    private Image 摆放框图;
    public bool 摆放中 => 摆放预览 != null;   // 摆放 模式（网格面板 交互 保护 用）

    protected override void 刷新(object 上下文) => 重建();

    public override void 隐藏面板(bool 上下互切 = false, bool 返回方向 = false)
    {
        退出摆放();
        base.隐藏面板(上下互切, 返回方向);
    }

    public override bool 回退()
    {
        if (摆放预览 != null) { 退出摆放(); 重建(); return true; }
        if (面板管理器.实例 != null) 面板管理器.实例.返回上一面板();
        return true;
    }

    public override string 取消文本 => "返回";

    // ===== 重建 =====
    private void 重建()
    {
        退出摆放();
        foreach (Transform 子 in transform)
            if (子 != null && 子.gameObject != null) Destroy(子.gameObject);
        创建状态区();
        创建房间网格();
        创建清单区();
        创建操作区();
    }

    // —— 状态行（第 N 天 / 天气 / 仓库容量 / 收音机电量） ——
    private void 创建状态区()
    {
        var 区 = UI工具.创建物体(transform, "状态", new Vector2(0f, 1f), new Vector2(0f, 1f));
        UI工具.设锚(区, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -20f), new Vector2(1500f, 80f));
        var 文本 = UI工具.创建文本(区, "状态", 状态文本(), 26, TextAlignmentOptions.Left);
        UI工具.铺满(文本.rectTransform);
    }

    private string 状态文本()
    {
        int 仓库格 = 玩家.仓库列 * (玩家.仓库行 + 玩家.家具效果("储物箱"));
        var 收音机 = 玩家.家具实例("收音机");
        int 电量 = 收音机 != null ? 收音机.电池电量 : 0;
        string 天气名 = ((天气类型)玩家.天气).ToString();
        return $"安全屋 · 第 {玩家.游戏天数 + 1} 天 · 天气：{天气名} · 仓库 {仓库格} 格 · 收音机 电量 {电量}/5";
    }

    // —— 房间网格：动态 挂 网格面板（家具 模式）——渲染/拖拽/旋转/投影 全部 复用 ——
    private void 创建房间网格()
    {
        var 物体 = UI工具.创建物体(transform, "房间网格", new Vector2(0f, 1f), new Vector2(0f, 1f));
        UI工具.设锚(物体, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -120f), new Vector2(房间列 * 格, 房间行 * 格));
        房间网格 = 物体.gameObject.AddComponent<网格面板>();
        房间网格.绑定网格容器(物体);
        房间网格.数据源 = 安全屋.网格();   // 家具网格（10×7；形状解析 = 家具形状[按等级]；家具 不 堆叠）
        房间网格.家具宿主 = this;          // 家具 模式：渲染 色块+名称+等级角标；交互 转交（双击 详情 / 右键 家具菜单）
        房间网格.配置视图显示();
        房间网格.立即刷新();
    }

    // 家具 双击（网格面板 转交）：显示 等级/描述 提示
    public void 家具被点击(物品堆叠 堆叠)
    {
        if (堆叠 == null) return;
        var (定义标识, 等级) = 家具工具.解码(堆叠.标识);
        if (数据.家具.TryGetValue(定义标识, out var 定义))
            事件.发布(new 日志事件(日志类型.反馈, $"{定义.名称} {等级}级：{定义.描述}。右键 操作，拖拽 移动，R 旋转。"));
    }

    // —— 摆放模式（建造：跟手预览 + 网格面板 落点投影 绿/红 → 左键 落格） ——
    private void 进入摆放(string 定义标识)
    {
        if (房间网格 == null) return;
        摆放堆叠 = new 物品堆叠(家具工具.编码(定义标识, 1), 1) { 旋转 = false };
        var 画布 = GetComponentInParent<Canvas>() ?? Object.FindFirstObjectByType<Canvas>();
        if (画布 == null) return;
        if (摆放预览 != null) Destroy(摆放预览);
        摆放预览 = new GameObject("摆放预览", typeof(RectTransform));
        摆放预览.transform.SetParent(画布.transform, false);
        摆放预览.transform.SetAsLastSibling();
        摆放框图 = UI工具.创建<Image>(摆放预览.transform, "框", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        摆放框图.raycastTarget = false;
        var (宽, 高) = 安全屋.占格(摆放堆叠.标识, 摆放堆叠.旋转);
        UI工具.设锚(摆放框图.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(宽 * 格, 高 * 格));
        事件.发布(new 日志事件(日志类型.反馈, "摆放模式：移动鼠标选择位置，左键 放置，R 旋转，返回 取消。"));
    }

    private void 退出摆放()
    {
        if (摆放预览 != null) { Destroy(摆放预览); 摆放预览 = null; }
        摆放堆叠 = null;
        摆放框图 = null;
        if (房间网格 != null) 房间网格.隐藏装备拖拽投影();
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy) return;
        if (摆放预览 != null)
        {
            摆放预览.transform.position = 鼠标位置();
            if (R按下()) 摆放堆叠.旋转 = !摆放堆叠.旋转;   // 旋转 预览（下一帧 投影 自动 用 新 旋转）
            if (房间网格 != null)
            {
                房间网格.显示装备拖拽投影(摆放堆叠, 鼠标位置(), false);   // 落点 投影（绿/红，复用 网格面板）
                if (左键按下() && 房间网格.屏幕到格(鼠标位置(), 摆放堆叠, out int 列, out int 行))
                {
                    bool 成功 = 安全屋.建造(家具工具.解码(摆放堆叠.标识).定义, 列, 行, 摆放堆叠.旋转);
                    if (成功) { 退出摆放(); 重建(); }
                }
            }
            return;
        }
    }

    // 鼠标 屏幕 位置（新输入系统优先；旧 Input 兜底——新输入 激活 时 绝不 触碰 Input 类）
    private Vector2 鼠标位置()
    {
        if (UnityEngine.InputSystem.Mouse.current != null) return UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        return Input.mousePosition;
    }

    // 左键 按下 帧（新输入系统优先；旧 Input 兜底）
    private bool 左键按下()
    {
        if (UnityEngine.InputSystem.Mouse.current != null) return UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
        return Input.GetMouseButtonDown(0);
    }

    // R 键 按下（新输入系统优先；旧 Input 兜底）
    private bool R按下()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null) return UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame;
        return Input.GetKeyDown(KeyCode.R);
    }

    // —— 家具清单（固定 5 件：名称/等级/建材/建造或升级按钮） ——
    private void 创建清单区()
    {
        var 区 = UI工具.创建物体(transform, "家具清单", new Vector2(0f, 1f), new Vector2(0f, 1f));
        UI工具.设锚(区, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(980f, -120f), new Vector2(560f, 640f));
        var 标题 = UI工具.创建文本(区, "标题", "—— 家具 ——", 24, TextAlignmentOptions.Center);
        UI工具.设锚(标题.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(560f, 36f));
        for (int 序 = 0; 序 < 家具顺序.Length; 序++)
            if (数据.家具.TryGetValue(家具顺序[序], out var 定义))
                创建家具行(区, 家具顺序[序], 定义, 序);
    }

    private void 创建家具行(RectTransform 父, string 标识, 家具数据 定义, int 序)
    {
        var 行 = UI工具.创建物体(父, "行_" + 标识, new Vector2(0f, 1f), new Vector2(0f, 1f));
        UI工具.设锚(行, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -44f - 序 * 118f), new Vector2(560f, 110f));
        var 已有 = 玩家.家具实例(标识);
        int 等级 = 已有 != null ? 家具工具.解码(已有.标识).等级 : 0;

        var 名 = UI工具.创建文本(行, "名", $"{定义.名称}（{等级}级）", 24, TextAlignmentOptions.Left);
        UI工具.设锚(名.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(300f, 32f));

        var 需求 = 已有 == null ? 定义.材料
            : (定义.升级 != null && 等级 - 1 < 定义.升级.Length && 定义.升级[等级 - 1] != null ? 定义.升级[等级 - 1].材料 : null);
        string 需求文本 = 已有 == null ? "建造：" + 材料文本(定义.材料)
            : (需求 != null ? "升级：" + 材料文本(需求) : "已满级");
        var 材 = UI工具.创建文本(行, "材", 需求文本, 18, TextAlignmentOptions.Left);
        UI工具.设锚(材.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -44f), new Vector2(540f, 26f));
        材.color = new Color(0.8f, 0.8f, 0.8f, 1f);

        if (已有 == null)
        {
            var 建造 = 创建按钮(行, "建造", "建造", () => 进入摆放(标识));
            UI工具.设锚(建造, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-70f, 0f), new Vector2(120f, 56f));
        }
        else if (等级 < 定义.最大等级)
        {
            var 升级 = 创建按钮(行, "升级", "升级", () => { 安全屋.升级(已有); 重建(); });
            UI工具.设锚(升级, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-70f, 0f), new Vector2(120f, 56f));
        }
        else
        {
            var 满 = UI工具.创建文本(行, "满", "已满级", 20, TextAlignmentOptions.Center);
            UI工具.设锚(满.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-70f, 0f), new Vector2(120f, 56f));
            满.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        }
    }

    // —— 操作行（睡觉 / 收听 / 装电池 / 储物背包） ——
    private void 创建操作区()
    {
        var 区 = UI工具.创建物体(transform, "操作", new Vector2(0f, 1f), new Vector2(0f, 1f));
        UI工具.设锚(区, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -810f), new Vector2(900f, 90f));
        float[] 横 = { 0f, 230f, 460f, 690f };

        var 睡 = 创建按钮(区, "睡觉", "睡觉（床）", () =>
        {
            玩家.睡觉();
            事件.发布(new 生命变化事件(玩家.生命, 玩家.最大生命, 玩家.生命));
            事件.发布(new 精力变化事件(玩家.行动点, 玩家.最大行动点, 玩家.行动点));
            foreach (伤病类型 类型 in System.Enum.GetValues(typeof(伤病类型)))
                事件.发布(new 伤病变化事件(类型, 玩家.伤病值(类型), 0));
            事件.发布(new 日志事件(日志类型.反馈, "你在床上睡了一觉，天亮了。"));
            重建();
        });
        UI工具.设锚(睡, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(横[0], 0f), new Vector2(210f, 70f));

        var 听 = 创建按钮(区, "收听", "收听广播", () => 收听广播());
        UI工具.设锚(听, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(横[1], 0f), new Vector2(210f, 70f));

        var 电 = 创建按钮(区, "装电池", "装电池", () => { 安全屋.装电池(); 重建(); });
        UI工具.设锚(电, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(横[2], 0f), new Vector2(210f, 70f));

        var 包 = 创建按钮(区, "背包", "储物与背包", () => 面板管理器.实例?.显示面板类型<持有面板>());
        UI工具.设锚(包, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(横[3], 0f), new Vector2(210f, 70f));
    }

    // 收听广播：耗 电量 1 次 → 随机播报 一条 情报（等级 ≤ 收音机 等级）
    private void 收听广播()
    {
        if (!安全屋.尝试收听()) { 事件.发布(new 日志事件(日志类型.反馈坏, "收音机没电了（或还没造），点「装电池」。") ); 重建(); return; }
        int 等级 = 玩家.家具等级("收音机");
        var 候选 = new List<情报条目>();
        foreach (var 条 in 数据.情报)
            if (条 != null && 条.等级 <= 等级) 候选.Add(条);
        if (候选.Count > 0)
        {
            var 条 = 候选[Random.Range(0, 候选.Count)];
            事件.发布(new 日志事件(日志类型.反馈, $"[广播] {条.文本}"));
        }
        else 事件.发布(new 日志事件(日志类型.反馈, "[广播] 收音机里只有沙沙声……"));
        重建();
    }

    // ===== 工具 =====

    private string 材料文本(配方材料[] 材料)
    {
        if (材料 == null) return "";
        var 段 = new List<string>();
        foreach (var 材 in 材料)
            if (材 != null) 段.Add($"{物品名(材.物品)}×{材.数量}");
        return string.Join(" ", 段);
    }

    private string 物品名(string 标识) => 数据.物品.TryGetValue(标识, out var 物) ? 物.名称 : 标识;

    // 通用按钮：Image 底 + 居中 TMP + 点击（按钮音效 统一注册）；返回 矩形（调用方 设锚/定位）
    private RectTransform 创建按钮(Transform 父, string 名, string 文本, UnityEngine.Events.UnityAction 回调)
    {
        var 图 = UI工具.创建<Image>(父, 名, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        图.color = new Color(0.2f, 0.22f, 0.26f, 1f);
        图.raycastTarget = true;
        var 按钮 = 图.gameObject.AddComponent<Button>();
        按钮.onClick.AddListener(回调);
        音效管理器.实例?.注册按钮(按钮);
        var 文本体 = UI工具.创建文本(图.rectTransform, "文本", 文本, 22, TextAlignmentOptions.Center);
        UI工具.铺满(文本体.rectTransform);
        return 图.rectTransform;
    }
}
