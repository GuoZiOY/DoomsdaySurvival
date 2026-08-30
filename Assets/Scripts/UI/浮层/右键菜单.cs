using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 右键小菜单（场景手动搭建 UI，代码控制显隐/定位/按钮绑定/按需显示）：
//   暴露参数：菜单根（初始隐藏，显示时置顶 + 定位到物品右侧）+ 使用/装备/打开/拆分/分解/丢弃 按钮引用。
//   按需显示：右键物品时按物品特性 SetActive 对应按钮（恢复品=使用、槽位=装备、容器=打开；丢弃始终显示）。
//   取消规则（不拦截事件，下层物品/滚动正常响应）：
//     点击菜单外（按下帧且点击点不在菜单矩形内）/ 滚动滚轮（内容滚动造成视觉偏差）/ 左键点物品 / 拖拽 / 按钮执行后 均关闭。
//   按钮点击：Awake 自动绑定到 主背包面板 的公开操作（场景无需手动绑 onClick）。
public sealed class 右键菜单 : MonoBehaviour
{
    public static 右键菜单 实例;   // 场景挂载自动登记

    [SerializeField] private RectTransform 菜单根;   // 面板根（初始隐藏；显示时 SetAsLastSibling 置顶 + 定位到物品右侧）
    [SerializeField] private Button 使用按钮;        // 恢复品：使用
    [SerializeField] private Button 装备按钮;        // 槽位物品：装备
    [SerializeField] private Button 打开按钮;        // 容器：打开
    [SerializeField] private Button 拆分按钮;        // 数量>1：拆分（数量>1 才显示）
    [SerializeField] private Button 分解按钮;        // 分解（系统未设计，暂隐藏）
    [SerializeField] private Button 丢弃按钮;        // 丢弃（任何入格物品均可，始终显示）
    [SerializeField] private Button 详情按钮;        // 信息展示面板（任何物品/装备槽均显示）
    [SerializeField] private Button 卸下按钮;        // 卸下（仅 装备槽模式 显示：已装备物品卸下回背包）
    [SerializeField] private Button 家具升级按钮;    // 家具 模式：升级（安全屋 房间网格 右键 家具）
    [SerializeField] private Button 家具拆除按钮;    // 家具 模式：拆除（家具 移出 房间网格）

    private 网格面板基类 背包面板;   // 兜底操作目标（场景主背包面板，Awake 查找）
    public 网格面板基类 目标面板;    // 当前操作目标（发起右键的面板，显示时由调用方设置；优先于 背包面板）
    private string 目标槽位;          // 非空 = 装备槽模式（菜单显示 详情/卸下，操作对象是槽位而非堆叠）

    void Awake()
    {
        实例 = this;
        if (菜单根 != null) 菜单根.gameObject.SetActive(false);   // 初始隐藏
        foreach (var 面板 in FindObjectsOfType<网格面板基类>())
            if (面板.数据源 == null) { 背包面板 = 面板; break; }   // 主背包面板 = 兜底操作目标
        if (使用按钮 != null) 使用按钮.onClick.AddListener(() => { 隐藏(); (目标面板 ?? 背包面板)?.菜单使用(); });
        if (装备按钮 != null) 装备按钮.onClick.AddListener(() => { 隐藏(); (目标面板 ?? 背包面板)?.菜单装备(); });
        if (打开按钮 != null) 打开按钮.onClick.AddListener(() => { 隐藏(); (目标面板 ?? 背包面板)?.菜单打开(); });
        if (拆分按钮 != null) 拆分按钮.onClick.AddListener(() => { 隐藏(); (目标面板 ?? 背包面板)?.菜单打开拆分(); });
        if (丢弃按钮 != null) 丢弃按钮.onClick.AddListener(() => { 隐藏(); (目标面板 ?? 背包面板)?.菜单丢弃(); });
        if (详情按钮 != null) 详情按钮.onClick.AddListener(() => { 隐藏(); 查看详情(); });
        if (卸下按钮 != null) 卸下按钮.onClick.AddListener(() => { 隐藏(); 卸下装备(); });
        if (家具升级按钮 != null) 家具升级按钮.onClick.AddListener(() => { 隐藏(); (目标面板 ?? 背包面板)?.家具升级(); });   // 家具：升级（作用于 选中）
        if (家具拆除按钮 != null) 家具拆除按钮.onClick.AddListener(() => { 隐藏(); (目标面板 ?? 背包面板)?.家具拆除(); });
    }

    // 详情：装备槽模式 → 信息面板.显示槽位；物品模式 → 目标面板.菜单查看详情
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

    // 右键物品：按物品特性 显示对应按钮；无可操作则隐藏。定位：物品右边界外侧（不是鼠标处），超右界自动放左侧
    public void 显示(物品堆叠 堆叠, RectTransform 物品框)
    {
        if (菜单根 == null || 堆叠 == null) return;
        bool 有操作 = false;
        if (ServiceRegistry.Get<DataService>().物品.TryGetValue(堆叠.标识, out var 物品))
        {
            if (使用按钮 != null)
            {
                bool 可用 = 物品.恢复量 > 0;
                使用按钮.gameObject.SetActive(可用);
                if (可用) 有操作 = true;
            }
            if (装备按钮 != null)
            {
                bool 可用 = !string.IsNullOrEmpty(物品.槽位);
                装备按钮.gameObject.SetActive(可用);
                if (可用) 有操作 = true;
            }
            if (打开按钮 != null)
            {
                bool 可用 = ServiceRegistry.Get<容器服务>().是容器(堆叠);
                打开按钮.gameObject.SetActive(可用);
                if (可用) 有操作 = true;
            }
        }
        if (拆分按钮 != null)
        {
            bool 可用 = 堆叠.数量 > 1;   // 可堆叠（数量>1）才显示拆分
            拆分按钮.gameObject.SetActive(可用);
            if (可用) 有操作 = true;
        }
        if (分解按钮 != null) 分解按钮.gameObject.SetActive(false);   // 分解系统未设计（预留）
        if (丢弃按钮 != null) { 丢弃按钮.gameObject.SetActive(true); 有操作 = true; }   // 丢弃始终可用（入格物品）
        if (详情按钮 != null) { 详情按钮.gameObject.SetActive(true); 有操作 = true; }   // 详情始终可用
        if (卸下按钮 != null) 卸下按钮.gameObject.SetActive(false);   // 物品模式无卸下
        if (!有操作) { 隐藏(); return; }
        目标槽位 = null;   // 物品模式
        音效管理器.实例?.播放成功();   // 右键呼出菜单 → 按钮成功音效
        菜单根.gameObject.SetActive(true);
        菜单根.SetAsLastSibling();   // 置顶（不被其他面板遮挡）
        定位到物品右侧(物品框);
    }

    // 装备槽模式：菜单只显示 详情/卸下，操作对象 = 槽位（已装备物品）；定位到槽位框右侧
    public void 显示装备槽(string 槽位, RectTransform 框)
    {
        if (菜单根 == null) return;
        目标槽位 = 槽位;
        if (使用按钮 != null) 使用按钮.gameObject.SetActive(false);
        if (装备按钮 != null) 装备按钮.gameObject.SetActive(false);
        if (打开按钮 != null) 打开按钮.gameObject.SetActive(false);
        if (拆分按钮 != null) 拆分按钮.gameObject.SetActive(false);
        if (分解按钮 != null) 分解按钮.gameObject.SetActive(false);
        if (丢弃按钮 != null) 丢弃按钮.gameObject.SetActive(false);
        if (详情按钮 != null) 详情按钮.gameObject.SetActive(true);
        if (卸下按钮 != null) 卸下按钮.gameObject.SetActive(true);
        if (家具升级按钮 != null) 家具升级按钮.gameObject.SetActive(false);
        if (家具拆除按钮 != null) 家具拆除按钮.gameObject.SetActive(false);
        音效管理器.实例?.播放成功();   // 右键呼出菜单（装备槽模式）→ 按钮成功音效
        菜单根.gameObject.SetActive(true);
        菜单根.SetAsLastSibling();
        定位到物品右侧(框);
    }

    // 家具模式（安全屋 房间网格 右键 家具）：升级/拆除/详情（旋转 = R 键，移动 = 拖拽，不进菜单）。
    // 操作对象 = 目标面板（物品网格面板）的 选中；菜单定位到 家具框 右侧。
    public void 显示家具(物品堆叠 堆叠, RectTransform 框)
    {
        if (菜单根 == null || 堆叠 == null) return;
        目标槽位 = null;   // 家具 模式：物品 堆叠 操作（非 槽位）
        if (使用按钮 != null) 使用按钮.gameObject.SetActive(false);
        if (装备按钮 != null) 装备按钮.gameObject.SetActive(false);
        if (打开按钮 != null) 打开按钮.gameObject.SetActive(false);
        if (拆分按钮 != null) 拆分按钮.gameObject.SetActive(false);
        if (分解按钮 != null) 分解按钮.gameObject.SetActive(false);
        if (丢弃按钮 != null) 丢弃按钮.gameObject.SetActive(false);
        if (卸下按钮 != null) 卸下按钮.gameObject.SetActive(false);
        // 升级：未满级 才显示（满级 = 隐藏）
        bool 可升级 = false;
        if (家具升级按钮 != null)
        {
            var (定义标识, 等级) = 家具工具.解码(堆叠.标识);
            if (ServiceRegistry.Get<DataService>().家具.TryGetValue(定义标识, out var 定义))
                可升级 = 等级 < 定义.最大等级;
            家具升级按钮.gameObject.SetActive(可升级);
        }
        if (家具拆除按钮 != null) 家具拆除按钮.gameObject.SetActive(true);   // 拆除 恒 显示
        if (详情按钮 != null) 详情按钮.gameObject.SetActive(true);
        音效管理器.实例?.播放成功();   // 右键呼出菜单（家具模式）→ 按钮成功音效
        菜单根.gameObject.SetActive(true);
        菜单根.SetAsLastSibling();
        定位到物品右侧(框);
    }

    // 隐藏菜单（点击菜单外/滚轮/左键点物品/拖拽/执行操作 均走这里）
    public void 隐藏()
    {
        if (菜单根 != null) 菜单根.gameObject.SetActive(false);
    }

    // 定位：自动计算 物品尺寸 与 菜单尺寸，优先放 物品右侧（间距 8），右侧放不下 → 自动翻到左侧，仍放不下 → clamp 屏幕内。
    // 纵向：菜单顶 与 物品顶 平齐（略下移），并 clamp 屏幕内。坐标统一用 世界（与 Canvas 缩放无关）。
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
