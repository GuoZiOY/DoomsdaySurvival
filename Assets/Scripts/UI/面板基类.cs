using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

//面板过渡样式：决定显示/隐藏时的 DOTween 动画组合
public enum 面板过渡样式
{
    对向滑入滑出,   // 默认：上下分层滑动——进入下一级 新面板自上落下、旧往上退；返回上一级 新面板自下上升、旧往下退
    右侧滑入右滑出  // 从屏幕右侧滑入显示（向左到位），退出向右滑出到屏外（角色/任务/设置等侧边面板）
}

// 面板基类：每个 UI 面板 = 一个组件（功能/系统/设施的交互表现）。
// 逻辑参数 ↔ UI 组件 通过 Inspector 拖拽绑定（[SerializeField]），不靠路径字符串。
public abstract class 面板基类 : MonoBehaviour
{
    // 共享按钮预制体（由 面板管理器 在 Inspector 设置，面板创建动态按钮用）
    public static GameObject 按钮预制体;

    // 内容区：面板动态内容（列表行/选项/正文等）的渲染容器。面板在 Inspector 拖入这一个内容锚点即可。
    // 子类可覆盖显示/隐藏只切换内容区（容器型面板，如 主视窗=中央视窗框架）保持框架本体常驻。
    [SerializeField] protected RectTransform 内容区;

    // ===== 过渡动画 =====
    // 子类可覆写：决定本面板使用哪种过渡样式（默认：对向滑入滑出）
    protected virtual 面板过渡样式 过渡样式 => 面板过渡样式.对向滑入滑出;

    // 过渡时长（秒）
    protected const float 过渡时长 = 0.4f;
    // 队列接力延迟：切换时 新面板比旧面板晚 0.15s 起步，形成"旧先退、新随后跟进"的排队感（旧立即退、新稍后进，时间重叠）
    private const float 队列延迟 = 0.2f;
    private const float 缩放入场 = 0.94f;   // 入场起始缩放（回弹用）
    private const float 缩放退场 = 0.96f;   // 退场收敛缩放

    private CanvasGroup _canvas组;
    private RectTransform _面板RT;
    private Vector2 _初始锚点位置;   // 记忆正常位置，动画后/重复播放不乱位
    private bool _已记录初始位置;     // 只记第一次（用 bool 而非 位置==0：全屏面板正常位常在 (0,0)）
    private Tween _当前过渡;         // 正在跑的过渡，新动画先 Kill 避免叠加

    protected CanvasGroup 画布组
    {
        get
        {
            if (_canvas组 == null)
            {
                _canvas组 = GetComponent<CanvasGroup>();
                if (_canvas组 == null) _canvas组 = gameObject.AddComponent<CanvasGroup>();
            }
            return _canvas组;
        }
    }

    protected RectTransform 面板矩形
    {
        get
        {
            if (_面板RT == null) _面板RT = GetComponent<RectTransform>();
            return _面板RT;
        }
    }

    // 面板管理器调用：激活自己 + 播入场动画 + 刷新内容
    // virtual：容器类面板（如 主视窗=中央视窗框架）可只切换自身内容区，保持框架本体常驻
    //   上下互切：侧边面板（角色/任务）互相切换时用上下滑动；从主界面进入保持各自的右滑，不传此参数
    //   返回方向：返回上一级时从下方上滑、旧面板往下退（进入下一级则相反）
    public static bool 跳过下次动画;   // 面板管理器设：目标==当前面板（自切）时跳过动画，只刷新内容
    public virtual void 显示面板(object 上下文 = null, bool 上下互切 = false, bool 返回方向 = false)
    {
        gameObject.SetActive(true);
        记录初始位置();
        bool 跳过 = 跳过下次动画;
        跳过下次动画 = false;
        if (跳过)
        {
            // 跳过动画（自切）：强制复位为完全可见的静态状态，避免上次残留 alpha/偏移导致内容不可见
            画布组.alpha = 1f;
            面板矩形.localScale = Vector3.one;
            面板矩形.anchoredPosition = _初始锚点位置;
        }
        else 播放入场(上下互切, 返回方向);
        刷新(上下文);
    }

    // 隐藏：播退场动画，结束后再 SetActive(false)（避免动画被瞬间掐断）
    public virtual void 隐藏面板(bool 上下互切 = false, bool 返回方向 = false)
    {
        记录初始位置();
        播放退场(上下互切, 返回方向, () => gameObject.SetActive(false));
    }

    // 是否为「侧边式」面板（角色/任务等右滑/上下切换的侧边面板）：
    // 供 面板管理器 在切换时判断「侧边↔侧边」互切，从而改用上下动画
    public bool 侧边式面板 => 过渡样式 == 面板过渡样式.右侧滑入右滑出;

    // 记录当前 anchoredPosition 为「正常位置」。只在首次记录（bool 防重复，见下）
    protected void 记录初始位置()
    {
        if (_已记录初始位置) return;
        if (_面板RT == null) _面板RT = GetComponent<RectTransform>();
        if (_面板RT == null) return;
        // 用 bool 而非 位置==0：全屏面板正常位常在 (0,0)，按位置判断会让隐藏动画中途误记偏移
        _初始锚点位置 = _面板RT.anchoredPosition;
        _已记录初始位置 = true;
    }

    // ===== 入场 / 退场编排 =====
    // （protected：不走面板管理器、由 OnEnable 驱动的面板——如 设置面板——也在激活时调用）
    protected void 播放入场(bool 上下互切 = false, bool 返回方向 = false)
    {
        if (_当前过渡 != null && _当前过渡.IsActive()) _当前过渡.Kill();
        画布组.DOKill();
        面板矩形.DOKill();

        if (过渡样式 == 面板过渡样式.右侧滑入右滑出)
        {
            // 侧边面板：互切时上下滑动，否则从屏右滑入
            if (上下互切) 入场_上下();
            else 入场_右侧滑入();
            return;
        }
        入场_分层进入(返回方向);
    }

    protected void 播放退场(bool 上下互切, bool 返回方向, TweenCallback 完成回调)
    {
        if (_当前过渡 != null && _当前过渡.IsActive()) _当前过渡.Kill();
        画布组.DOKill();
        面板矩形.DOKill();

        if (过渡样式 == 面板过渡样式.右侧滑入右滑出)
        {
            // 侧边面板：互切时上下滑动，否则向右滑出
            if (上下互切) 退场_上下(完成回调);
            else 退场_右滑出(完成回调);
            return;
        }
        退场_分层退出(返回方向, 完成回调);
    }

    // —— 上下分层滑动（默认）：进入下一级 新面板自上落下(往下)、旧面板往上退；返回上一级 新面板自下上升(往上)、旧面板往下退 ——
    // 入场带 队列延迟：滞后旧面板起步，形成"旧先退、新随后跟上"的排队接力（时间重叠）
    private void 入场_分层进入(bool 返回方向)
    {
        var 高度 = 面板矩形.rect.height;
        if (高度 <= 0f) 高度 = Screen.height * 0.5f;  // 高度拿不到时按屏幕比例兜底
        画布组.alpha = 0f;
        // 起始：进入下一级 从上方落下；返回上一级 从下方上升
        面板矩形.anchoredPosition = _初始锚点位置 + new Vector2(0f, 返回方向 ? -高度 : 高度);
        面板矩形.localScale = Vector3.one * 缩放入场;   // 从轻微缩小开始
        // 透明度 0→1（延迟起步）
        画布组.DOFade(1f, 过渡时长)
            .SetDelay(队列延迟)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetLink(gameObject);
        // 位置 → 正常位（向下落 / 向上升）
        面板矩形.DOAnchorPosY(_初始锚点位置.y, 过渡时长)
            .SetDelay(队列延迟)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetLink(gameObject);
        // 缩放 0.94→1：OutBack 轻微过冲回弹，到位带"弹"的活感
        _当前过渡 = 面板矩形.DOScale(Vector3.one, 过渡时长)
            .SetDelay(队列延迟)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void 退场_分层退出(bool 返回方向, TweenCallback 完成回调)
    {
        var 高度 = 面板矩形.rect.height;
        if (高度 <= 0f) 高度 = Screen.height * 0.5f;
        画布组.alpha = 1f;
        面板矩形.anchoredPosition = _初始锚点位置;
        面板矩形.localScale = Vector3.one;
        // 透明度 1→0
        画布组.DOFade(0f, 过渡时长)
            .SetEase(Ease.InCubic)
            .SetUpdate(true)
            .SetLink(gameObject);
        // 退出：进入下一级时旧面板往上退（让位给落下者）；返回时旧面板往下退
        面板矩形.DOAnchorPosY(_初始锚点位置.y + (返回方向 ? -高度 : 高度), 过渡时长)
            .SetEase(Ease.InCubic)
            .SetUpdate(true)
            .SetLink(gameObject);
        // 缩放 1→0.96 轻微收敛，与入场呼应
        _当前过渡 = 面板矩形.DOScale(Vector3.one * 缩放退场, 过渡时长)
            .SetEase(Ease.InCubic)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(完成回调);
    }

    // —— 从屏右滑入 / 向右滑出（角色 / 任务 / 设置等侧边面板）——
    private void 入场_右侧滑入()
    {
        var 宽度 = 面板矩形.rect.width;
        if (宽度 <= 0f) 宽度 = Screen.width * 0.6f;  // 宽度拿不到时用屏幕比例兜底
        画布组.alpha = 0f;
        // 起点：正常位往右一个面板宽（屏幕外右）
        面板矩形.anchoredPosition = _初始锚点位置 + new Vector2(宽度, 0f);
        // 透明度 微微淡入（配合滑动更有层次）
        画布组.DOFade(1f, 过渡时长 * 0.9f)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetLink(gameObject);
        // 滑动：屏幕外右 → 正常位（向左滑入）
        _当前过渡 = 面板矩形.DOAnchorPosX(_初始锚点位置.x, 过渡时长)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void 退场_右滑出(TweenCallback 完成回调)
    {
        var 宽度 = 面板矩形.rect.width;
        if (宽度 <= 0f) 宽度 = Screen.width * 0.6f;
        画布组.alpha = 1f;
        面板矩形.anchoredPosition = _初始锚点位置;
        // 透明度渐出
        画布组.DOFade(0f, 过渡时长 * 0.9f)
            .SetEase(Ease.InCubic)
            .SetUpdate(true)
            .SetLink(gameObject);
        // 滑动：正常位 → 屏幕外右（一个面板宽）
        _当前过渡 = 面板矩形.DOAnchorPosX(_初始锚点位置.x + 宽度, 过渡时长)
            .SetEase(Ease.InCubic)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(完成回调);
    }

    // —— 上下互切（侧边面板 角色↔任务 互切用）：旧面板向上滑出顶外，新面板从顶外滑下到位 ——
    private void 入场_上下()
    {
        var 高度 = 面板矩形.rect.height;
        if (高度 <= 0f) 高度 = Screen.height * 0.6f;  // 高度拿不到时按屏幕比例兜底
        画布组.alpha = 0f;   // 从透明淡入，避免上次退场残留 alpha=0 导致内容不可见
        // 起点：正常位往上一个面板高（屏幕外顶上方）
        面板矩形.anchoredPosition = _初始锚点位置 + new Vector2(0f, 高度);
        // 透明度渐入
        画布组.DOFade(1f, 过渡时长)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetLink(gameObject);
        // 滑动：上方 → 正常位（下落到位）
        _当前过渡 = 面板矩形.DOAnchorPosY(_初始锚点位置.y, 过渡时长)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void 退场_上下(TweenCallback 完成回调)
    {
        var 高度 = 面板矩形.rect.height;
        if (高度 <= 0f) 高度 = Screen.height * 0.6f;
        画布组.alpha = 1f;
        面板矩形.anchoredPosition = _初始锚点位置;
        // 透明度渐出
        画布组.DOFade(0f, 过渡时长)
            .SetEase(Ease.InCubic)
            .SetUpdate(true)
            .SetLink(gameObject);
        // 滑动：正常位 → 上方（滑出屏幕顶外）
        _当前过渡 = 面板矩形.DOAnchorPosY(_初始锚点位置.y + 高度, 过渡时长)
            .SetEase(Ease.InCubic)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(完成回调);
    }

    // 全局"取消/回退"的统一入口：子类按各自语义覆盖（如 战斗面板=取消选目标 / 功能面板=返回设施内部）。
    // 由 玩家输入系统（右键）与 侧边栏取消按钮 调用当前显示面板的 回退()。
    // 返回 true = 本次取消被消费（有动作）；false = 无可取消（调用方决定是否播错误音效）。
    public virtual bool 回退() => false;

    // 取消按钮的文案：随当前面板动态显示（默认"取消"；各面板覆写为 返回/离开城镇/关闭/回主菜单 等）
    public virtual string 取消文本 => "取消";

    // 子类：进入面板时填充内容（上下文 可由打开者传入，如设施返回节点）
    protected abstract void 刷新(object 上下文);

    // ===== 共享工具 =====

    // 在父级创建一行按钮（克隆共享按钮预制体 + 设文字 + 接点击）
    // 移除布局=true 去掉克隆体 LayoutElement（对话选项不需要固定高度）；文字居中=true 覆盖对齐为居中（对话选项）
    // static：子面板组件（非 面板基类 子类）也能调用
    public static void 创建行(RectTransform 父, string 文字, Action 点击, bool 移除布局 = false, bool 文字居中 = false)
    {
        if (按钮预制体 == null) { Debug.LogError("[面板基类] 未设置按钮预制体（请在 面板管理器 的 Inspector 拖入）"); return; }
        if (父 == null) { Debug.LogWarning("[面板基类] 创建行：内容区未接线"); return; }
        var 物体 = Instantiate(按钮预制体, 父, false);
        if (移除布局)
        {
            var 布局 = 物体.GetComponent<LayoutElement>();
            if (布局 != null) Destroy(布局);
        }
        var 文本 = 物体.transform.Find("文字")?.GetComponent<TMP_Text>();
        if (文本 != null)
        {
            文本.text = 文字;
            if (文字居中) 文本.alignment = TextAlignmentOptions.Center;
        }
        var 按钮 = 物体.GetComponent<Button>();
        if (按钮 != null)
        {
            按钮.onClick.AddListener(() => 点击());
            音效管理器.实例?.注册按钮(按钮);   // 动态按钮成功音效（场景静态按钮由管理器自动扫描）
        }
    }

    // 克隆一个「行模板」到父容器并激活；返回指定组件（专用行组件用，如 背包行）。
    // static：子面板组件（非 面板基类 子类）也能调用
    public static T 创建模板<T>(RectTransform 父, GameObject 模板) where T : Component
    {
        if (父 == null || 模板 == null) return null;
        var 物体 = Instantiate(模板, 父, false);
        物体.SetActive(true);
        return 物体.GetComponent<T>();
    }

    public static void 清空(RectTransform 列表)
    {
        if (列表 == null) return;
        for (int i = 列表.childCount - 1; i >= 0; i--) Destroy(列表.GetChild(i).gameObject);
    }

    // 创建一行标签（非按钮，纯文字；分区标题等），用 TMP 默认字体
    public static void 创建标签(RectTransform 父, string 文字)
    {
        if (父 == null) return;
        var 物体 = new GameObject("标签", typeof(RectTransform), typeof(TextMeshProUGUI));
        物体.transform.SetParent(父, false);
        var 文本 = 物体.GetComponent<TextMeshProUGUI>();
        文本.text = 文字;
        文本.color = 游戏主题.暗淡;
        文本.fontSize = 20;
        文本.enableWordWrapping = false;
    }

    // 设文本（空引用安全）
    public static void 设文本(TMP_Text 文本, string 内容)
    {
        if (文本 != null) 文本.text = 内容;
    }

    // 设选中缩放：Tab/排序 等切换按钮的选中态——固定放大一点（替代颜色高亮），悬停反馈叠在其上
    public const float 选中缩放 = 1.08f;

    public static void 设选中缩放(Button 按钮, bool 选中)
    {
        if (按钮 == null) return;
        float 目标 = 选中 ? 选中缩放 : 1f;
        var 反馈 = 按钮.GetComponent<悬停反馈>();
        if (反馈 != null) 反馈.设基座(目标);
        else 按钮.transform.localScale = Vector3.one * 目标;
    }

    // 设施返回：从城镇地图进入的设施回城镇地图；从剧情进入的回剧情节点
    protected void 返回设施(string 返回节点)
    {
        if (string.IsNullOrEmpty(返回节点)) return;
        if (返回节点.StartsWith("城镇:")) { ServiceRegistry.Get<地图服务>().回到城镇地图(); return; }
        ServiceRegistry.Get<DialogueService>().进入节点(返回节点);
    }
}
