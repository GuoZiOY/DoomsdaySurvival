using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 右键小菜单（物品/装备槽/家具 通用）：**按钮全部按需实例化**，不再手动引用任何按钮。
//   暴露参数：菜单根（容器，初始隐藏，显示时置顶 + 定位到目标框右侧）+ 「菜单按钮」预制体（可选）。
//   按需显示：右键时按目标特性（物品/槽位/家具）现场生成条目——条目 = 文本 + 动作，
//            由 小菜单工具.建条目 实例化（预制体 → 共享按钮预制体 → 代码兜底 三级回落）。
//   取消规则（不拦截事件，下层物品/滚动正常响应）：
//     点击菜单外（按下帧且点击点不在菜单矩形内）/ 滚动滚轮 / 左键点物品 / 拖拽 / 按钮执行后 均关闭。
//   动作由 网格面板基类 的公开操作（菜单使用/菜单装备/菜单打开/菜单打开拆分/菜单丢弃/菜单查看详情/家具升级/家具拆除）承担。
public sealed class 右键菜单 : MonoBehaviour
{
    public static 右键菜单 实例;   // 场景挂载自动登记

    [Header("小菜单（按钮按需实例化，无需手动引用按钮）")]
    [SerializeField] private GameObject 菜单按钮预制体;   // 一行按钮的预制体（内含 Button + TMP_Text 即可）；留空回退共享按钮预制体/代码建行
    [SerializeField] private RectTransform 菜单根;        // 面板根（初始隐藏；显示时置顶 + 定位到目标框右侧）
    [SerializeField] private RectTransform 菜单列表;      // 条目父级（可选；留空 = 用 菜单根 自身，代码会给它补一个纵向布局）
    [SerializeField] private float 菜单宽度 = 170f;        // 代码兜底建行时的条目宽

    private 网格面板基类 背包面板;   // 兜底操作目标（场景主背包面板，Awake 查找）
    public 网格面板基类 目标面板;    // 当前操作目标（发起右键的面板，显示时由调用方设置；优先于 背包面板）
    private string 目标槽位;          // 非空 = 装备槽模式（菜单显示 详情/卸下，操作对象是槽位而非堆叠）

    private RectTransform 条目父级 => 菜单列表 != null ? 菜单列表 : 菜单根;

    void Awake()
    {
        实例 = this;
        if (菜单根 != null) 菜单根.gameObject.SetActive(false);   // 初始隐藏
        foreach (var 面板 in FindObjectsOfType<网格面板基类>())
            if (面板.数据源 == null) { 背包面板 = 面板; break; }   // 主背包面板 = 兜底操作目标
    }

    // ===== 显示（物品）=====
    // 按物品特性生成条目：使用 / 装备 / 打开 / 拆分 / 丢弃 / 详情；无可操作则隐藏。定位：物品右边界外侧，超右界自动放左侧
    public void 显示(物品堆叠 堆叠, RectTransform 物品框)
    {
        if (菜单根 == null || 堆叠 == null) return;
        目标槽位 = null;   // 物品模式
        var 条目 = new List<(string 文本, Action 动作)>();
        if (ServiceRegistry.Get<DataService>().物品.TryGetValue(堆叠.标识, out var 物品))
        {
            if (物品.恢复量 > 0 || (物品.类型 == "书籍" && (物品.书籍种类 == "技能书" || 物品.书籍种类 == "配方书")))
                条目.Add(("使用", () => (目标面板 ?? 背包面板)?.菜单使用()));
            if (!string.IsNullOrEmpty(物品.槽位))
                条目.Add(("装备", () => (目标面板 ?? 背包面板)?.菜单装备()));
            if (ServiceRegistry.Get<容器服务>().是容器(堆叠))
                条目.Add(("打开", () => (目标面板 ?? 背包面板)?.菜单打开()));
        }
        if (堆叠.数量 > 1)
            条目.Add(("拆分", () => (目标面板 ?? 背包面板)?.菜单打开拆分()));
        条目.Add(("丢弃", () => (目标面板 ?? 背包面板)?.菜单丢弃()));
        条目.Add(("详情", 查看详情));
        展示(条目, 物品框);
    }

    // ===== 显示（装备槽）=====
    // 槽位模式：卸下 / 详情（操作对象 = 槽位，非堆叠）；定位到槽位框右侧
    public void 显示装备槽(string 槽位, RectTransform 框)
    {
        if (菜单根 == null) return;
        目标槽位 = 槽位;
        var 条目 = new List<(string 文本, Action 动作)>
        {
            ("卸下", 卸下装备),
            ("详情", 查看详情),
        };
        展示(条目, 框);
    }

    // ===== 显示（家具）=====
    // 家具模式（安全屋 房间网格 右键 家具）：打开（储物箱=仓库、工作台/灶台/医疗站=制作面板、容器型家具=容器面板）/ 升级（未满级）/ 拆除 / 详情
    public void 显示家具(物品堆叠 堆叠, RectTransform 框)
    {
        if (菜单根 == null || 堆叠 == null) return;
        目标槽位 = null;   // 家具 模式：物品 堆叠 操作（非 槽位）
        var (定义标识, 等级) = 家具工具.解码(堆叠.标识);
        var 数据 = ServiceRegistry.Get<DataService>();
        var 条目 = new List<(string 文本, Action 动作)>();

        bool 可打开 = 定义标识 == "储物箱" || 定义标识 == "工作台" || 定义标识 == "灶台" || 定义标识 == "医疗站";
        if (!可打开 && 数据.家具.TryGetValue(定义标识, out var 家具定义)) 可打开 = 家具定义.是容器;
        if (可打开) 条目.Add(("打开", () => (目标面板 ?? 背包面板)?.菜单打开()));

        bool 可升级 = 数据.家具.TryGetValue(定义标识, out var 定义) && 等级 < 定义.最大等级;
        if (可升级) 条目.Add(("升级", () => (目标面板 ?? 背包面板)?.家具升级()));

        条目.Add(("拆除", () => (目标面板 ?? 背包面板)?.家具拆除()));
        条目.Add(("详情", 查看详情));
        展示(条目, 框);
    }

    // ===== 通用入口：任意动作列表（房间层 等 非物品 场景也统一走这一个菜单）=====
    // 条目 = (文本, 动作)；框 = 菜单贴着它的右侧（放不下自动翻左侧）
    public bool 显示动作(IList<(string 文本, Action 动作)> 条目, RectTransform 框, float 兜底宽 = 0f)
    {
        if (菜单根 == null) return false;
        目标槽位 = null;
        if (条目 == null || 条目.Count == 0) { 隐藏(); return false; }
        var 父 = 条目父级;
        if (父 == null) { 隐藏(); return false; }
        确保条目布局(父);
        小菜单工具.清条目(父);
        foreach (var (文本, 动作) in 条目)
        {
            var 动作副本 = 动作;
            小菜单工具.建条目(菜单按钮预制体, 父, 文本, () => { 隐藏(); 动作副本?.Invoke(); }, 兜底宽 > 0f ? 兜底宽 : 菜单宽度);
        }
        音效管理器.实例?.播放成功();
        菜单根.gameObject.SetActive(true);
        菜单根.SetAsLastSibling();
        定位到物品右侧(框);
        return true;
    }

    // 条目父级兜底布局：没挂纵向布局就现加一个（父级里原有的旧子物体——例如"背景框"——设 ignoreLayout，不参与排布）
    private void 确保条目布局(RectTransform 父)
    {
        if (父 == null || 父.GetComponent<VerticalLayoutGroup>() != null) return;
        var 布局 = 父.gameObject.AddComponent<VerticalLayoutGroup>();
        布局.padding = new RectOffset(6, 6, 6, 6);
        布局.spacing = 4f;
        布局.childControlWidth = true;
        布局.childControlHeight = false;
        布局.childForceExpandWidth = true;
        布局.childForceExpandHeight = false;
        布局.childAlignment = TextAnchor.UpperCenter;
        foreach (Transform 子 in 父)
        {
            var 元素 = 子.GetComponent<LayoutElement>();
            if (元素 == null) 元素 = 子.gameObject.AddComponent<LayoutElement>();
            元素.ignoreLayout = true;   // 背景框之类：留在原位，不跟着条目排
        }
        if (父.GetComponent<ContentSizeFitter>() == null)
        {
            var 适配 = 父.gameObject.AddComponent<ContentSizeFitter>();
            适配.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    // 统一展示：清旧条目 → 逐条实例化 → 空则隐藏；定位到目标框右侧
    private void 展示(List<(string 文本, Action 动作)> 条目, RectTransform 框)
    {
        if (条目 == null || 条目.Count == 0) { 隐藏(); return; }
        var 父 = 条目父级;
        if (父 == null) { 隐藏(); return; }
        确保条目布局(父);
        小菜单工具.清条目(父);
        foreach (var (文本, 动作) in 条目)
        {
            var 动作副本 = 动作;
            小菜单工具.建条目(菜单按钮预制体, 父, 文本, () => { 隐藏(); 动作副本?.Invoke(); }, 菜单宽度);
        }
        音效管理器.实例?.播放成功();   // 右键呼出菜单 → 按钮成功音效
        菜单根.gameObject.SetActive(true);
        菜单根.SetAsLastSibling();   // 置顶（不被其他面板遮挡）
        定位到物品右侧(框);
    }

    // 详情：装备槽模式 → 信息面板.显示槽位；物品/家具模式 → 目标面板.菜单查看详情
    private void 查看详情()
    {
        if (!string.IsNullOrEmpty(目标槽位))
        {
            音效管理器.实例?.播放成功();   // 详情 → 按钮成功音效
            var 挂载父 = (目标面板 ?? 背包面板)?.transform as RectTransform;
            信息面板.显示槽位详情(目标槽位, 挂载父);   // 多实例：每次 新建 面板
            return;
        }
        (目标面板 ?? 背包面板)?.菜单查看详情();
    }

    // 卸下：仅装备槽模式（已装备物品回背包，面板操作.卸下 已发事件）
    private void 卸下装备()
    {
        if (string.IsNullOrEmpty(目标槽位)) return;
        var 档案 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (档案 == null) return;
        面板操作.卸下(档案, 目标槽位);
        if (装备区.实例 != null) 装备区.实例.请求刷新();   // 脏标记合并（卸下已发事件，Update 统一刷新）
    }

    // 关闭检测：点击菜单外（左键/右键按下且不在菜单矩形内）/ 滚轮 → 关闭。不拦截事件——下层物品/滚动正常响应
    void Update()
    {
        if (菜单根 == null || !菜单根.gameObject.activeSelf) return;
        if (检测点击按下() && !点击在菜单内())
        {
            隐藏();
            return;
        }
        if (检测滚轮()) 隐藏();
    }

    // 左键/右键 按下帧（兼容新旧输入）
    private bool 检测点击按下()
    {
        bool 按下 = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) 按下 = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) 按下 = true;
#endif
        return 按下;
    }

    // 滚轮 滚动帧（兼容新旧输入）
    private bool 检测滚轮()
    {
        bool 滚动 = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.scroll.ReadValue().y != 0f) 滚动 = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.mouseScrollDelta.y != 0f) 滚动 = true;
#endif
        return 滚动;
    }

    // 当前鼠标屏幕位置（兼容新旧输入）
    private Vector2 输入鼠标位置()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null) return Mouse.current.position.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#endif
        return Vector2.zero;
    }

    // 点击点是否在 菜单根 矩形内（在菜单内 → 按钮自行处理，不关闭）
    private bool 点击在菜单内()
    {
        var 画布 = 菜单根.GetComponentInParent<Canvas>();
        var 相机 = 画布 != null && 画布.renderMode != RenderMode.ScreenSpaceOverlay ? 画布.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(菜单根, 输入鼠标位置(), 相机);
    }

    // 隐藏菜单（点击菜单外/滚轮/左键点物品/拖拽/执行操作 均走这里）
    public void 隐藏()
    {
        if (菜单根 != null) 菜单根.gameObject.SetActive(false);
    }

    // 定位：自动计算 目标框尺寸 与 菜单尺寸，优先放 目标右侧（间距 8），右侧放不下 → 自动翻到左侧，仍放不下 → clamp 屏幕内。
    // 纵向：菜单顶 与 目标顶 平齐（略下移），并 clamp 屏幕内。坐标统一用 世界（与 Canvas 缩放无关）。
    private void 定位到物品右侧(RectTransform 物品框)
    {
        if (物品框 == null || 菜单根 == null) return;
        // 强制 布局 重建：菜单 首次 激活 时 布局系统 尚未刷新 尺寸（rect 还是 旧值/0）→ 边界修正 用错 尺寸 → 位置 不准。
        // 激活后 ForceRebuild 立即 生效，尺寸 才 正确（第二次 起 布局 已 稳定，重复 重建 无 副作用）。
        LayoutRebuilder.ForceRebuildLayoutImmediate(菜单根);
        var 画布 = 菜单根.GetComponentInParent<Canvas>();
        var 画布根 = 画布 != null ? (RectTransform)画布.transform : null;
        if (画布根 == null) return;
        // —— 自动计算尺寸（世界）——
        float 物宽 = 物品框.rect.width * 物品框.lossyScale.x;
        float 物高 = 物品框.rect.height * 物品框.lossyScale.y;
        float 菜单世界宽 = 菜单根.rect.width * 菜单根.lossyScale.x;
        float 菜单世界高 = 菜单根.rect.height * 菜单根.lossyScale.y;
        const float 间距 = 8f, 安全边距 = 4f;
        // —— 屏幕范围（画布世界矩形，与 position 同基准）——
        var 屏左下 = 画布根.TransformPoint(new Vector3(画布根.rect.xMin, 画布根.rect.yMin, 0f));
        var 屏右上 = 画布根.TransformPoint(new Vector3(画布根.rect.xMax, 画布根.rect.yMax, 0f));
        float 屏左 = 屏左下.x, 屏右 = 屏右上.x, 屏底 = 屏左下.y, 屏顶 = 屏右上.y;
        // —— 物品四边（世界；position 是 pivot 点，按 pivot 换算——物品框 pivot 为左上(0,1)）——
        float 物右 = 物品框.position.x + 物品框.right.x * (物宽 * (1f - 物品框.pivot.x));
        float 物左 = 物品框.position.x - 物品框.right.x * (物宽 * 物品框.pivot.x);
        float 物顶 = 物品框.position.y + 物品框.up.y * (物高 * (1f - 物品框.pivot.y));
        // —— x：右侧 → 左侧 → clamp ——
        float 菜单左;
        if (物右 + 间距 + 菜单世界宽 <= 屏右 - 安全边距) 菜单左 = 物右 + 间距;                       // 放右侧
        else if (物左 - 间距 - 菜单世界宽 >= 屏左 + 安全边距) 菜单左 = 物左 - 间距 - 菜单世界宽;      // 翻左侧
        else 菜单左 = Mathf.Clamp(物右 + 间距, 屏左 + 安全边距, 屏右 - 菜单世界宽 - 安全边距);         // 兜底 clamp
        // —— y：菜单顶 与 物品顶 平齐（略下移），clamp 屏幕内 ——
        float 菜单顶 = Mathf.Clamp(物顶 - 4f, 屏底 + 菜单世界高 + 安全边距, 屏顶 - 安全边距);
        // —— 换算到 pivot 点（position 是 pivot；左/上边 对齐目标）——
        var 目标左上 = new Vector3(菜单左, 菜单顶, 0f);
        菜单根.position = 目标左上 + 菜单根.right * (菜单世界宽 * 菜单根.pivot.x) - 菜单根.up * (菜单世界高 * (1f - 菜单根.pivot.y));
    }
}
