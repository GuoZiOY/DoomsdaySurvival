using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 安全屋面板（营地）：玩家据点——网格摆放式 家具 建造/升级。
// 房间网格 = 家具网格面板（家具 模式 子类）动态 挂载 到 场景 手动 搭 的 网格容器（代码 设 锚点 居中）；
//           渲染/拖拽/换位/R旋转/投影 全部 复用 网格面板基类；右键 家具 = 右键菜单（使用/升级/拆除/详情）。
// 家具 = 物品堆叠（玩家档案.家具：标识 含 等级后缀"储物箱_2"；数量恒 1；列/行/旋转 = 房间网格位置）。
// 建造 = 摆放模式（跟手预览 + 家具网格面板 落点投影 绿/红 → 左键 落格 建造）。
// UI 手动 搭建（不再 全动态）：面板 物体（挂 本组件）+ 网格容器（RectTransform）+ 建造按钮
//               + 建造面板（挂 建造面板 组件：显示 家具 信息 + 建造/升级）。
// 场景搭建：面板 物体 挂 本组件（面板基类），拖入 面板管理器.营地 引用位 + 各 引用位（网格容器/建造按钮/建造面板）。
public sealed class 安全屋面板 : 面板基类
{
    private const float 格 = 90f;                 // 单格像素（与 网格面板基类.格尺寸 一致）
    // 房间 网格 尺寸 动态（随 户型 房间 数：最少 10×7，最多 12×9）——重建 时 从 网格服务 读

    private 安全屋管理器 安全屋 => ServiceRegistry.Get<安全屋管理器>();
    private EventBus 事件 => ServiceRegistry.Get<EventBus>();

    // ===== 场景 手动 搭建 引用 =====
    [SerializeField] private RectTransform 网格容器;   // 房间网格 区域（家具网格面板 动态 挂载；代码 设 锚点 居中）
    [SerializeField] private Button 建造按钮;          // 建造（编辑）按钮：编辑 模式 开关
    [SerializeField] private TMP_Text 建造按钮文本;    // 建造按钮 文本（编辑 模式 切换 时 更新；可选）
    [SerializeField] private Button 拆除按钮;          // 拆除 按钮：拆墙 模式 开关（点击 后 左键 点 墙 拆）
    [SerializeField] private TMP_Text 拆除按钮文本;    // 拆除按钮 文本（拆墙 模式 切换 时 更新；可选）
    [SerializeField] private 建造面板 建造面板;        // 建造面板：显示 家具 信息 + 建造/升级
    // 注：制作面板 已 浮动 化（制作面板.创建）——不再 由 安全屋面板 管控

    // ===== 房间网格（家具网格面板：家具 模式 子类） =====
    private 家具网格面板 房间网格;

    // ===== 编辑模式（建造/移动/旋转 开关）：非 编辑 模式 家具 静态（不可 拖拽/旋转/建造） =====
    public bool 编辑模式 { get; private set; }

    // ===== 拆墙 模式（拆除 按钮 切换）：左键 点 墙 格 → 拆除（打通 房间 / 扩大） =====
    public bool 拆除模式 { get; private set; }
    private Image 拆除投影;          // 拆除 模式：鼠标 悬停 墙 格 投影（绿色）

    // ===== 摆放模式（建造） =====
    private GameObject 摆放预览;
    private 物品堆叠 摆放堆叠;   // 摆放 中 的 临时 家具堆叠（未 入 网格）
    private Image 摆放框图;
    public bool 摆放中 => 摆放预览 != null;   // 摆放 模式（家具网格面板 交互 保护 用）

    void Awake()
    {
        if (建造面板 != null) 建造面板.gameObject.SetActive(false);   // 建造面板 初始 隐藏（点 建造 才 出现）
        if (建造按钮 != null) 建造按钮.onClick.AddListener(切换编辑模式);
        if (拆除按钮 != null) 拆除按钮.onClick.AddListener(切换拆除模式);
    }

    protected override void 刷新(object 上下文)
    {
        重建();
        // 建造面板 显隐 跟随 编辑模式（打开 面板 时 保持 上次 状态）
        if (建造面板 != null)
        {
            建造面板.gameObject.SetActive(编辑模式);
            建造面板.刷新();
        }
    }

    public override void 显示面板(object 上下文 = null, bool 上下互切 = false, bool 返回方向 = false)
    {
        背景模糊层.显示模糊();   // 营地 打开：背景 模糊（与 持有面板 一致，复用 同一 模糊层）
        base.显示面板(上下文, 上下互切, 返回方向);
    }

    public override void 隐藏面板(bool 上下互切 = false, bool 返回方向 = false)
    {
        退出摆放();
        背景模糊层.隐藏模糊();
        base.隐藏面板(上下互切, 返回方向);
    }

    public override bool 回退()
    {
        if (摆放预览 != null) { 退出摆放(); 重建(); return true; }
        if (面板管理器.实例 != null) 面板管理器.实例.返回上一面板();
        return true;
    }

    public override string 取消文本 => "返回";

    // ===== 重建（不 销毁 手动 子物体；房间网格 挂载/全量 重建） =====
    private void 重建()
    {
        退出摆放();
        if (网格容器 == null) return;
        var 网格 = 安全屋.网格();   // 户型（含 动态 尺寸）
        // 网格容器 锚点 居中（网格 在 面板 中央；尺寸 = 户型 网格；内部 布局 不受 容器 锚点 影响）。
        // 保持 拖拽 移动 后 的 位置（anchoredPosition）——不 强制 复原 居中（中键 拖拽 面板 后 停留）
        Vector2 保持位置 = 网格容器.anchoredPosition;
        UI工具.设锚(网格容器, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 保持位置, new Vector2(网格.网格列 * 格, 网格.网格行 * 格));
        if (房间网格 == null)
        {
            房间网格 = 网格容器.gameObject.AddComponent<家具网格面板>();
            房间网格.绑定网格容器(网格容器);
            房间网格.数据源 = 网格;   // 家具网格（形状解析 = 家具形状[按等级]；家具 不 堆叠）
            房间网格.家具宿主 = this;
            房间网格.配置视图显示();
        }
        确保拖拽层(网格容器);   // 空白 区 拖拽 移动 面板（家具 框 在 上 优先 家具 拖拽）
        房间网格.强制重建();   // 全量 重建：户型/家具 变化 时 增量 刷新 会 残留 旧 底格（列/行 未 变 不 触发 结构 重建）——底格/分隔线/实体框 全 重画
    }

    // 网格 面板 拖拽 层：透明 覆盖 整 网格 区域（最 底——底格/线 raycastTarget=false 不 拦截；家具 框 在 上 优先）
    private void 确保拖拽层(RectTransform 容器)
    {
        if (容器.Find("面板拖拽层") != null) return;
        var 物体 = new GameObject("面板拖拽层", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(容器, false);
        var 图 = 物体.GetComponent<Image>();
        图.color = new Color(1f, 1f, 1f, 0f);
        图.raycastTarget = true;
        var 矩 = 物体.GetComponent<RectTransform>();
        矩.anchorMin = Vector2.zero;
        矩.anchorMax = Vector2.one;
        矩.offsetMin = Vector2.zero;
        矩.offsetMax = Vector2.zero;
        矩.SetAsFirstSibling();   // 最 底（底格/线 不 拦截；家具 框 优先）
        var 拖拽 = 物体.AddComponent<网格面板拖拽>();
        拖拽.初始化(容器, GetComponentInParent<Canvas>());
    }

    // 升级 后 占格 变化 → 网格 强制 重建（建造面板 调用）
    public void 强制重建网格()
    {
        if (房间网格 != null) 房间网格.强制重建();
    }

    // 切换 编辑模式（建造按钮）：允许 建造/移动/旋转 家具；非 编辑 模式 家具 静态
    private void 切换编辑模式()
    {
        编辑模式 = !编辑模式;
        if (建造按钮文本 != null) 建造按钮文本.text = 编辑模式 ? "退出编辑" : "编辑";
        事件.发布(new 日志事件(日志类型.系统, 编辑模式
            ? "编辑模式：可以 建造/移动/旋转 家具（再点 退出编辑）。"
            : "退出编辑模式。"));
        if (建造面板 != null)
        {
            建造面板.gameObject.SetActive(编辑模式);   // 点 建造 → 面板 出现；退出 编辑 → 隐藏
            建造面板.刷新();
        }
    }

    // 切换 拆墙 模式（拆除 按钮）：点击 后 鼠标 悬停 墙 格 投影 → 左键 拆除；再 点 退出
    private void 切换拆除模式()
    {
        拆除模式 = !拆除模式;
        if (拆除按钮文本 != null) 拆除按钮文本.text = 拆除模式 ? "退出拆除" : "拆除墙";
        事件.发布(new 日志事件(日志类型.系统, 拆除模式
            ? "拆除模式：鼠标 悬停 墙 格 变 绿，左键 点击 拆除（打通 房间 / 扩大 可用 范围）。"
            : "退出拆除模式。"));
        if (!拆除模式) 隐藏拆除投影();
    }

    // 拆除 投影（鼠标 悬停 墙 格）：绿色 单 格（可 拆 提示）
    private void 确保拆除投影()
    {
        if (拆除投影 != null) return;
        var 物体 = UI工具.创建图(网格容器, "拆除投影", null, 网格面板配色.放置可色, new Vector2(0, 1), new Vector2(0, 1));
        物体.raycastTarget = false;
        物体.rectTransform.sizeDelta = new Vector2(格, 格);
        物体.transform.SetAsLastSibling();
        拆除投影 = 物体;
    }

    private void 显示拆除投影(int 列, int 行)
    {
        确保拆除投影();
        if (拆除投影 == null) return;
        拆除投影.gameObject.SetActive(true);
        拆除投影.rectTransform.anchoredPosition = new Vector2(列 * 格, -行 * 格);
    }

    private void 隐藏拆除投影()
    {
        if (拆除投影 != null) 拆除投影.gameObject.SetActive(false);
    }

    // 进入 摆放模式（建造面板「建造」调用）：需 编辑 模式
    public void 进入摆放(string 定义标识)
    {
        if (!编辑模式) { 事件.发布(new 日志事件(日志类型.警告, "请先点击「建造（编辑）」进入编辑模式。")); return; }
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
        事件.发布(new 日志事件(日志类型.系统, "摆放模式：移动鼠标选择位置，左键 放置，R 旋转，返回 取消。"));
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
                房间网格.显示装备拖拽投影(摆放堆叠, 鼠标位置(), false);   // 落点 投影（绿/红，复用 网格面板基类）
                if (左键按下() && 房间网格.屏幕到格(鼠标位置(), 摆放堆叠, out int 列, out int 行))
                {
                    bool 成功 = 安全屋.建造(家具工具.解码(摆放堆叠.标识).定义, 列, 行, 摆放堆叠.旋转);
                    if (成功)
                    {
                        退出摆放();
                        音效管理器.实例?.播放家具放下();   // 建造 落格
                        重建();
                        建造面板?.刷新();
                    }
                }
            }
            return;
        }
        // 拆除 模式：鼠标 悬停 墙 格 投影 → 左键 拆除（家具 格 由 组件 处理）
        if (拆除模式 && 房间网格 != null)
        {
            bool 悬停墙 = false;
            if (房间网格.屏幕到格(鼠标位置(), 单格堆叠, out int 悬停列, out int 悬停行))
            {
                var 网格 = 安全屋.网格();
                if (网格.格所属块(悬停列, 悬停行) == -2)   // 墙 格（可 拆）
                {
                    悬停墙 = true;
                    显示拆除投影(悬停列, 悬停行);
                    if (左键按下() && 安全屋.拆除墙(悬停列, 悬停行))
                    {
                        音效管理器.实例?.播放家具放下();
                        房间网格.强制重建();   // 墙 格 变 可用 → 底格 重画
                        建造面板?.刷新();
                    }
                }
            }
            if (!悬停墙) 隐藏拆除投影();
        }
    }

    private static readonly 物品堆叠 单格堆叠 = new 物品堆叠("收音机", 1);   // 1×1 占格（墙 格 屏幕 → 格 换算）

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
}
