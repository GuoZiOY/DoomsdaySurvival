using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// ============================================================
// 网格面板基类：通用塔科夫式网格 底层框架（抽象）——渲染/拖拽/交互 骨架 + 差异点 钩子。
// 子类 = 网格上的"实体"语义：
//   物品网格面板（背包/仓库/容器/穿戴/搜索——物品网格面板.cs）
//   家具网格面板（安全屋 房间网格——家具网格面板.cs）
//   房间网格面板（探索类网格：房间/墙体/容器/敌我令牌）
// 本文件 = 门面：字段 / 生命周期 / 刷新调度 / 钩子声明 / 公共 API / Update 调度。
// 分部文件：网格面板基类.渲染.cs（渲染框架）/ 网格面板基类.拖拽.cs（拖拽框架）/ 网格面板基类.交互.cs（交互分派+内部组件）。
// 原则：子类 只写 实体 语义；本类 不含 任何 具体 实体 渲染/交互 代码。
// ============================================================
public abstract partial class 网格面板基类 : MonoBehaviour
{
    // ===== 网格配置（场景 Inspector 配置） =====
    [SerializeField] protected RectTransform 网格容器;   // 网格区域（左上锚定；代码动态生成底座与物品）。可运行时 绑定网格容器 覆盖（动态容器面板）
    [SerializeField] private int 固定列数 = 6;         // Inspector 固定覆盖 服务.网格列（0 = 用服务尺寸；主背包测试参数）
    [SerializeField] private int 固定行数 = 10;        // Inspector 固定覆盖 服务.网格行（0 = 用服务尺寸；主背包测试参数）
    public const float 格尺寸 = 90f;                   // 单格像素：**全项目统一 90**（主背包/仓库/容器/穿戴容器/家具/房间/区域 一致，2K 基准，兼顾格内文字可读性）
    [SerializeField] private int 固定列数最小 = 1;
    [SerializeField] private int 固定列数最大 = 100;
    [SerializeField] private int 固定行数最小 = 1;
    [SerializeField] private int 固定行数最大 = 100;

    protected 玩家档案 档案 => ServiceRegistry.Get<PlayerService>().档案;
    protected DataService 数据 => ServiceRegistry.Get<DataService>();
    protected 物品堆叠 选中;
    protected RectTransform 底盘;                   // 网格整体 衬底
    protected RectTransform 底座层, 线层, 物品层;   // 网格分层（底座 / 分隔线 / 物品）

    protected 网格服务 服务 => 数据源 ?? 档案.网格服务;


    protected virtual bool 建底格 => true;
    protected virtual bool 建格线 => true;

    // 格线画法（v51 刀20）：false = 逐段建图（默认；容器/口袋 的形状线靠它）；
    //   true = **一张 Tiled Image 平铺满整块**（探索层用：那边没有形状块、线铺满整块，
    //   逐段建图在区域层是 33×22 + 23×32 = **1462 张** → 1 张）。
    protected virtual bool 格线用平铺 => false;
    protected virtual bool 需要底格(int 列, int 行) => true;

    protected virtual bool 需要拖拽组件 => true;

    [NonSerialized] public 网格服务 数据源;   // 注入 外部视图（容器/仓库/穿戴容器/家具网格）
    [NonSerialized] public 物品堆叠 所属容器;  // 非空 = 本面板显示的容器实例（跨面板转移用；容器面板注入）
    [NonSerialized] public string 所属槽位;   // 非空 = 本面板显示的穿戴容器槽位（弹挂/腰封/背包；装具区注入）

    // ===== 实体框表（增量刷新核心）：堆叠实例 → 实体框 =====
    protected sealed class 物品框
    {
        public RectTransform 根;       // 实体根（框层：底色+黑描边，点击/拖拽挂这里）
        public Image 框图;             // 底层色（变化才改）
        public RectTransform 内容层;   // 悬停放大（内缩内容层）
        public GameObject 高光层;      // 悬停高光
        public TMP_Text 名称;          // 实体名（创建时定，不变）
        public TMP_Text 耐久;          // 耐久文本（物品 用；家具 无）
        public TMP_Text 数量;          // 数量角标（物品=数量 / 家具=等级）
        public bool 存活;              // 本帧刷新命中标记
        public int 上次数量;           // 数量/等级 文本去重
        public string 上次耐久;        // 耐久文本去重
        public string 创建标识;        // 创建时 堆叠.标识（变质 替换 标识 变化 → 重建 框：图标/名称 刷新）
        public CanvasGroup 组;         // 实体根的 CanvasGroup（探索层：已搜灰化；v51 刀18 缓存，见 更新实体框）
    }
    protected readonly Dictionary<物品堆叠, 物品框> 物品框表 = new Dictionary<物品堆叠, 物品框>();

    // 拖拽判定缓存：落点格/旋转 未变 → 复用上次合法性判定
    protected int 上次判定列 = -1, 上次判定行 = -1;
    protected bool 上次判定旋转, 上次判定有效, 上次可放, 上次可合并;

    // 本次网格实际尺寸（有效列/有效行 经 clamp 后）：渲染统一用它
    protected int 当前列, 当前行;
    protected float 上次格尺寸;
    public int 渲染列 => 当前列;
    public int 渲染行 => 当前行;
    public float 渲染网格宽 => 当前列 * 格尺寸 + 最右偏移();
    public float 渲染网格高 => 当前行 * 格尺寸;

    // ===== 全局活动拖拽（跨面板转移 状态；发起面板 记录，任一面板 结束 消费） =====
    protected static 网格面板基类 拖拽发起面板;
    protected static 网格服务 拖拽源服务;
    protected static 物品堆叠 拖拽中堆叠;
    protected static readonly List<网格面板基类> 全部面板 = new List<网格面板基类>();
    public static List<网格面板基类> 面板登记表 => 全部面板;

    // ===== 拖拽状态（实例） =====
    protected 物品堆叠 拖拽源;
    protected RectTransform 拖拽代理;
    protected Image 落点投影;                              // 全网格 禁放 红框（装备槽 拖拽 到自己 容器 用；含 最右/最下 偏移）
    protected readonly List<Image> 投影格 = new List<Image>();   // 逐格 落点 投影（口袋 容器：每格 独立 定位，缝隙 天然 留空）
    protected GameObject 原位置影子;
    protected static bool 拖拽旋转;
    protected int 落点列, 落点行;
    protected bool 落点有效;


    // ===== 容器形状 块偏移（口袋 缝隙：x=右缝、y=下缝——左右/上下 并排 口袋 之间 留 缝隙） =====
    private const float 块缝隙宽 = 10f;
    protected static readonly Color 物品描边色 = new Color(0f, 0f, 0f, 0.6f);   // 实体框 黑描边（物品/家具 通用）
    protected static readonly Vector2 物品描边距离 = new Vector2(3f, -3f);
    protected Vector2[] 块偏移;

    // ============================================================
    // 差异点 钩子（子类 实现 实体 语义）
    // ============================================================

    /// 渲染：创建 实体框（物品框/家具框/探索格框）；null = 数据缺失不创建
    protected abstract 物品框 创建实体框(物品堆叠 堆叠);
    /// 渲染：更新 已有 实体框（增量：位置/旋转/色/角标 变化才改）
    protected abstract void 更新实体框(物品框 框, 物品堆叠 堆叠);
    /// 交互：单击/双击 实体（点击次数 传入；子类 处理 选中 高亮/双击 语义）
    protected abstract void 单击实体(物品堆叠 堆叠, int 点击次数);
    /// 交互：右键 实体（呼出 菜单）
    protected abstract void 右键实体(物品堆叠 堆叠, RectTransform 框);
    /// 拖拽：同网格 落格 放置 语义（物品=合并/存入容器/区域互换；家具=移动/换位）；返回 成功
    protected abstract bool 完成拖拽(PointerEventData 事件, 物品堆叠 堆叠);
    /// 拖拽：跨面板 策略（物品=true；家具=false——房间 固定 物）
    protected virtual bool 允许跨面板() => true;
    /// 拖拽：开始 前 拦截（家具 摆放 模式 忽略 拖拽）
    protected virtual bool 允许开始拖拽(物品堆叠 堆叠) => true;
    /// 拖拽：代理 内容 显示（物品=图标/品质底；家具=色块）
    protected virtual void 创建代理内容(Image 图, 物品堆叠 堆叠) { }
    /// 拖拽：装备槽 落点 反馈（整段行为 交给 子类——基类 零 装备槽 概念）。
    /// 物品=命中装备槽→槽位匹配→高亮（绿/红）→返回 true（消费本帧，隐藏网格投影）；家具=false（走正常落格）
    protected virtual bool 处理装备槽落点(PointerEventData 事件) => false;
    /// 拖拽：清除 拖拽 高亮（物品=装备区 槽位 高亮 清除；家具=空）
    protected virtual void 清除拖拽高亮() { }
    /// 拖拽投影：目标 允许 放入（物品=容器 类型 校验；家具=true）
    protected virtual bool 目标允许放入(string 标识) => true;
    /// 拖拽投影：目标 禁 放入（物品=嵌套 防护；家具=false）
    protected virtual bool 目标禁放入(物品堆叠 拖入) => false;
    /// 接收 跨面板 转移（物品=实现；家具/探索=false 拒绝）
    public virtual bool 接收跨面板转移(网格服务 源服务, 物品堆叠 堆叠, PointerEventData 事件, bool 拖拽旋转 = false) => false;
    // 右键菜单 公开 操作（物品=使用/装备/打开/丢弃/拆分/详情；家具=打开/升级/拆除）——子类 覆盖 需要的
    public virtual void 菜单使用() { }
    public virtual void 菜单装备() { }
    public virtual void 菜单打开() { }
    public virtual void 菜单丢弃() { }
    public virtual void 菜单打开拆分() { }
    public virtual void 菜单拆分(int 数量) { }
    public virtual void 菜单查看详情() { }
    public virtual void 家具升级() { }
    public virtual void 家具拆除() { }

    // ============================================================
    // 生命周期 / 刷新调度
    // ============================================================
    protected bool 待刷新网格, 待刷新物品;

    void Awake()
    {
        全部面板.Add(this);
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<背包变化事件>(背包变化响应);
    }

    void OnDestroy()
    {
        全部面板.Remove(this);
        if (ServiceRegistry.已注册<EventBus>())
        {
            var 事件 = ServiceRegistry.Get<EventBus>();
            事件.取消订阅<背包变化事件>(背包变化响应);
        }
    }

    private void 背包变化响应(背包变化事件 _) => 待刷新物品 = true;

    public void 请求刷新() => 待刷新网格 = true;
    public void 立即刷新() => 刷新网格();
    public void 强制重建()
    {
        销毁网格结构();
        当前列 = 当前行 = 0;
        上次格尺寸 = 0f;
        刷新网格();
    }

    // 销毁 网格 结构（底盘 + 底座层/线层/物品层——全 销毁，防 重建 残留 累积；先 SetActive(false) 立即 隐藏——Destroy 延迟，避免 同 帧 新旧 层 视觉 重叠）
    private void 销毁网格结构()
    {
        if (底盘 != null) { 底盘.gameObject.SetActive(false); Destroy(底盘.gameObject); 底盘 = null; }
        if (底座层 != null) { 底座层.gameObject.SetActive(false); Destroy(底座层.gameObject); 底座层 = null; }
        if (线层 != null) { 线层.gameObject.SetActive(false); Destroy(线层.gameObject); 线层 = null; }
        if (物品层 != null) { 物品层.gameObject.SetActive(false); Destroy(物品层.gameObject); 物品层 = null; }
    }

    // ============================================================
    // 浮层宿主（容器面板/净水器面板/制作面板：浮动面板基类 子类 内部 的 网格）——
    // 拖拽 命中 时 以 宿主 为 拦截 单位：浮层 盖 在 下层 网格 之上，鼠标 在 宿主 上 不 穿透 下层。
    // ============================================================
    protected 浮动面板基类 浮动宿主 => GetComponentInParent<浮动面板基类>();

    // ============================================================
    // 更新循环（调度 刷新 + 拖拽 跨面板 投影 + 钩子）
    // ============================================================
    void Update()
    {
        if (待刷新网格)
            { 待刷新网格 = false; 待刷新物品 = false; 刷新网格(); }
        else if (待刷新物品)
            { 待刷新物品 = false; 刷新物品(); }
        if (拖拽中堆叠 == null)
            return;
        Vector2 鼠标 = 输入鼠标位置();
        var 伪事件 = new PointerEventData(EventSystem.current) { position = 鼠标 };
        // 拖拽中 R 旋转预览（所有 实体 通用：物品/家具 一致）——必须在 跨面板 return 之前（家具 允许跨面板=false）
        if (拖拽发起面板 == this && 检测按R())
        {
            拖拽旋转 = !拖拽旋转;
            更新代理尺寸();
            拖拽移动(伪事件);
        }
        if (!允许跨面板() || 拖拽发起面板 == this)
            return;   // 家具 网格：不 参与 跨面板 投影（拖拽 中 R 旋转 已 处理；落点 投影 走 OnDrag）
        确保投影();
        if (落点投影 == null)
            return;

        var 下方 = 事件下方面板(伪事件);
        if (下方 != this)
            { 隐藏全部投影(); return; }   // 鼠标 不在 本 网格（含 浮层 底座 空白/别 浮层）→ 不 投影
        if (!屏幕到容器相对(伪事件, out var 相对, out var 尺寸))
            { 隐藏全部投影(); return; }

        var (物宽, 物高) = 预览占格(拖拽中堆叠);
        float 相对顶 = 尺寸.y - 相对.y;
        if (!落格(相对.x, 相对顶, 物宽, 物高, out int 列, out int 行))
            { 隐藏全部投影(); return; }

        同步投影格(物宽, 物高);
        bool 可放 = 目标允许放入(拖拽中堆叠.标识) && !目标禁放入(拖拽中堆叠);
        if (可放)
        {
            var 落点物品 = 服务.该格物品(列, 行);
            可放 = (落点物品 != null && 落点物品 != 拖拽中堆叠 && 服务.可合并(落点物品, 拖拽中堆叠))
                || 可存入容器(落点物品, 拖拽中堆叠)
                || 服务.可放置(拖拽中堆叠.标识, 列, 行, 拖拽旋转, 拖拽中堆叠);
        }
        显示投影格(列, 行, 物宽, 物高, 可放 ? 网格面板配色.放置可色 : 网格面板配色.放置禁色);
    }

    // ============================================================
    // 公共 API
    // ============================================================

    public bool 屏幕命中(Vector2 屏幕点)
    {
        var 矩形 = 网格容器;
        if (矩形 == null) return false;
        var 画布 = 矩形.GetComponentInParent<Canvas>();
        var 相机 = 画布 != null && 画布.renderMode != RenderMode.ScreenSpaceOverlay ? 画布.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(矩形, 屏幕点, 相机);
    }

    public RectTransform 网格区域 => 网格容器;

    public RectTransform 物品框矩形(物品堆叠 堆叠)
    {
        if (堆叠 == null) return null;
        return 物品框表.TryGetValue(堆叠, out var 框) ? 框.根 : null;
    }

    public void 绑定网格容器(RectTransform 容器)
    {
        网格容器 = 容器;
        销毁网格结构();
    }

    public void 配置视图显示()
    {
        固定列数 = 0;
        固定行数 = 0;
    }

    // 当前面板网格服务（装备拖拽放置用）
    public 网格服务 视图服务 => 服务;
}
