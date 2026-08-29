using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// ============================================================
// 网格面板.拖拽 —— 分部类：拖拽代理 / 落点投影 / 跨面板 / R 旋转 / 落格
// 与 网格面板.cs 同一 partial 类（共享字段与方法）；纯搬移，无行为改动。
// ============================================================
public sealed partial class 网格面板
{
    // —— 拖拽状态 ——
    private 物品堆叠 拖拽源;
    private RectTransform 拖拽代理;   // 跟手物品图片（吸附格子）
    private Image 落点投影;           // 网格上的绿/红落点指示
    private GameObject 原位置影子;    // 原位置的半透明虚影
    private static bool 拖拽旋转;      // 全局拖拽预览旋转（R 键；跨面板投影共享——任意面板 Update 都按它算投影）
    private int 落点列, 落点行;        // 拖拽中最后有效投影格（放下用，不随松手重算）
    private bool 落点有效;            // 投影当前是否有效（在网格内）

    // 懒创建本面板的落点投影（挂在物品层，贴格显示）。发起面板在 创建拖拽视觉 已建；其他面板跨面板拖拽时首次建。
    private void 确保投影()
    {
        if (落点投影 != null || 物品层 == null) return;
        var 投体 = new GameObject("落点投影", typeof(RectTransform), typeof(Image));
        投体.transform.SetParent(物品层, false);
        落点投影 = 投体.GetComponent<Image>();
        落点投影.color = 网格面板配色.放置可色;
        落点投影.raycastTarget = false;
        var 投影矩形 = 投体.GetComponent<RectTransform>();
        投影矩形.anchorMin = new Vector2(0, 1);
        投影矩形.anchorMax = new Vector2(0, 1);
        投影矩形.pivot = new Vector2(0, 1);
        落点投影.gameObject.SetActive(false);
    }

    // 鼠标屏幕位置（兼容新旧输入）
    private Vector2 输入鼠标位置()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null) return UnityEngine.InputSystem.Mouse.current.position.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#endif
        return Vector2.zero;
    }

    // 拖拽结束时隐藏所有面板的跨面板投影（避免容器面板投影残留）——登记表遍历（替代 FindObjectsOfType）
    private static void 清理所有面板投影()
    {
        foreach (var 面板 in 全部面板)
            if (面板.落点投影 != null) 面板.落点投影.gameObject.SetActive(false);
    }

    // 按下进入拖拽：记录源物品 + 创建视觉（代理/投影/影子），立即开始
    private void 开始拖拽(物品堆叠 堆叠, PointerEventData 事件)
    {
        右键菜单.实例?.隐藏();   // 拖拽时关闭右键菜单
        音效管理器.实例?.播放拿起();   // 拿起物品音效
        拖拽源 = 堆叠;
        拖拽旋转 = 堆叠.旋转;
        落点有效 = false;
        上次判定有效 = false;   // 新拖拽：判定缓存失效（首次落点重算）
        // 记录全局活动拖拽（跨面板转移用）：发起面板 + 源服务 + 堆叠
        拖拽发起面板 = this;
        拖拽源服务 = 服务;
        拖拽中堆叠 = 堆叠;
        创建拖拽视觉(堆叠);
        拖拽移动(事件);
    }

    // 首次超过启动阈值：创建 跟手代理 + 落点投影 + 原位置影子
    private void 创建拖拽视觉(物品堆叠 堆叠)
    {
        // ① 跟手代理：挂 Canvas 顶层（不被 Viewport 裁剪、不被任何面板覆盖——跨面板拖拽可见）
        // 结构：根 = RectMask2D（裁剪 cover 溢出）+ 子 Image 内容图（等比放大铺满占格）——与 物品框 显示一致
        var 物体 = new GameObject("拖拽代理", typeof(RectTransform), typeof(RectMask2D));
        var 顶层 = GetComponentInParent<Canvas>();
        物体.transform.SetParent(顶层 != null ? 顶层.transform : transform.root, false);
        var 内容体 = new GameObject("内容", typeof(RectTransform), typeof(Image));
        内容体.transform.SetParent(物体.transform, false);
        var 图 = 内容体.GetComponent<Image>();
        图.raycastTarget = false;
        // 代理优先显示挂载图标；无图则用品质底色块（半透明跟手）
        var 代理物品 = 数据.物品.TryGetValue(堆叠.标识, out var 代理数据) ? 代理数据 : null;
        var 代理图标 = 代理物品 != null ? 物品图标服务.获取(代理物品.图片) : null;
        if (代理图标 != null)
        {
            图.sprite = 代理图标;
            图.color = new Color(1f, 1f, 1f, 0.85f);
            图.preserveAspect = false;
        }
        else
        {
            var 代理底 = 物品品质底(堆叠);
            图.color = new Color(代理底.r, 代理底.g, 代理底.b, 0.85f);
        }
        var 内容矩 = 内容体.GetComponent<RectTransform>();
        内容矩.anchorMin = new Vector2(0.5f, 0.5f);
        内容矩.anchorMax = new Vector2(0.5f, 0.5f);
        内容矩.pivot = new Vector2(0.5f, 0.5f);
        内容矩.anchoredPosition = Vector2.zero;   // 居中于代理根（cover 放大由 更新代理尺寸 计算）
        拖拽代理 = 物体.GetComponent<RectTransform>();
        拖拽代理.anchorMin = new Vector2(0.5f, 0.5f);   // Canvas 顶层：中心锚定，屏幕坐标定位
        拖拽代理.anchorMax = new Vector2(0.5f, 0.5f);
        拖拽代理.pivot = new Vector2(0.5f, 0.5f);   // 中心跟随鼠标
        // ② 落点投影（绿/红，贴格）——复用懒创建，尺寸由 更新代理尺寸 设置
        确保投影();
        更新代理尺寸();
        // ③ 原位置半透明影子（虚影：物品将离开的位置；内缩尺寸与物品一致）
        var 影体 = new GameObject("原位置影子", typeof(RectTransform), typeof(Image));
        影体.transform.SetParent(物品层, false);
        var 影图 = 影体.GetComponent<Image>();
        影图.color = new Color(0.65f, 0.65f, 0.7f, 0.3f);   // 半透明灰
        影图.raycastTarget = false;
        var 影矩形 = 影体.GetComponent<RectTransform>();
        影矩形.anchorMin = new Vector2(0, 1);
        影矩形.anchorMax = new Vector2(0, 1);
        影矩形.pivot = new Vector2(0, 1);
        影矩形.anchoredPosition = new Vector2(格x(堆叠.列, 堆叠.行) + 网格面板配色.物品边距, -堆叠.行 * 格尺寸 - 网格面板配色.物品边距);
        var (影宽, 影高) = 服务.物品占格(堆叠);   // 影子 = 物品原本占格（原始旋转；旋转预览不影响它）
        影矩形.sizeDelta = new Vector2(影宽 * 格尺寸 - 网格面板配色.物品边距 * 2f, 影高 * 格尺寸 - 网格面板配色.物品边距 * 2f);
        原位置影子 = 影体;
        拖拽代理.SetAsLastSibling();   // 代理置顶渲染——否则同格的落点投影（后创建）会盖住它
    }

    // 屏幕点 → 相对容器左下（**逻辑单位**，除以 Canvas 缩放——与 格尺寸 同基准，任何分辨率/缩放下都准）。
    // 以 物品层(=网格尺寸) 为基准：内容在 Content 里居中时仍与物品坐标对齐，拖拽不错位。
    private bool 屏幕到容器相对(PointerEventData 事件, out Vector2 相对, out Vector2 容器尺寸)
    {
        var 基准 = 物品层 != null ? 物品层 : 网格容器;
        容器尺寸 = 基准.rect.size;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(基准, 事件.position, 事件.pressEventCamera, out var 世界点))
        {
            相对 = Vector2.zero;
            return false;
        }
        var 原点 = 基准.TransformPoint(new Vector3(-基准.rect.width * 基准.pivot.x, -基准.rect.height * 基准.pivot.y, 0f));
        var 缩放 = 基准.lossyScale;
        相对 = new Vector2((世界点.x - 原点.x) / 缩放.x, (世界点.y - 原点.y) / 缩放.y);   // 逻辑单位（x 向右、y 向上）
        return true;
    }

    // 拖拽中：物品图片吸附鼠标所在格（格内锁定不移动，跨格才跳）→ 投影贴格同格；R 键旋转由 Update 每帧检测
    private void 拖拽移动(PointerEventData 事件)
    {
        if (拖拽源 == null || 拖拽代理 == null) return;
        // 物品图始终跟随鼠标（挂 Canvas 顶层：跨面板拖拽不消失、不被遮挡）
        拖拽代理.gameObject.SetActive(true);
        var 顶层 = GetComponentInParent<Canvas>();
        if (顶层 != null)
        {
            Vector2 局部;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)顶层.transform, 事件.position, 事件.pressEventCamera, out 局部))
                拖拽代理.anchoredPosition = 局部;
            else 拖拽代理.position = 事件.position;
        }
        else 拖拽代理.position = 事件.position;
        // 跨面板：鼠标在别的面板（容器）上 → 本面板不显示投影（代理仍跟手）
        var 下方面板 = 事件下方面板(事件);
        // 容器面板 底座 拦截：鼠标在 任一 容器面板 底座（含 网格 区域）上 → 本面板 不显示 投影
        // （容器面板 的 网格面板 若 未 登记/未 命中，底座 仍 应 阻隔 下方 主背包 的 投影）
        // 叠放（两个 容器面板 上下 重叠）：取 最上层——鼠标 在 上层 网格内 时 下层 也 命中（矩形 重叠），
        // 若 按 下层 拦截 会 导致 上层 面板 内 无法 拖拽 放下（叠放 拖拽 修复）。
        var 自己面板 = GetComponentInParent<容器面板>();
        var 最上层容器 = 最上层容器面板(事件);
        if (最上层容器 != null)
        {
            // 鼠标 在 容器面板 上（叠放 取 最上层）：落点 归 面板，不 穿透 下层
            if (最上层容器 != 自己面板)
            {
                落点有效 = false;
                if (落点投影 != null) 落点投影.gameObject.SetActive(false);
                return;
            }
            // 自己面板：网格内 → 跳过 装备槽 预览，走 自己网格 投影；底座空白区 → 阻隔（不 触发 底下 装备槽）
            if (!最上层容器.命中网格(事件.position))
            {
                落点有效 = false;
                if (落点投影 != null) 落点投影.gameObject.SetActive(false);
                装备面板.实例?.清除全部高亮();
                return;
            }
        }
        else
        {
            // 不在 任何 容器面板 上：其他 网格面板（主背包/仓库/穿戴区）跨面板 + 装备槽 高亮
            if (下方面板 != null && 下方面板 != this)
            {
                落点有效 = false;
                if (落点投影 != null) 落点投影.gameObject.SetActive(false);
                return;
            }
            // 拖到 装备槽（装备区）→ 槽位高亮提示（绿=槽位兼容 / 红=不兼容），本面板不显示网格投影
            if (装备面板.实例 != null && 装备面板.实例.命中槽位(事件.position, out var 槽位名))
            {
                落点有效 = false;
                if (落点投影 != null) 落点投影.gameObject.SetActive(false);
                bool 匹配 = 数据.物品.TryGetValue(拖拽源.标识, out var 装备) && 面板操作.槽位匹配(装备.槽位, 槽位名);
                装备面板.实例.高亮槽位(槽位名, 匹配);
                return;
            }
            装备面板.实例?.清除全部高亮();
        }
        if (!屏幕到容器相对(事件, out var 相对, out var 尺寸))
        {
            if (落点投影 != null) 落点投影.gameObject.SetActive(false);
            return;
        }
        // 投影格 = 物品中心对齐（四舍五入：偏差对称 ±半格内，1×1 精确——大物体不错位）；越界贴边（右/下侧可放）
        var (物宽, 物高) = 预览占格(拖拽源);   // 按 拖拽旋转（R 预览）计算，旋转后投影/影子同步变化
        float 相对顶 = 尺寸.y - 相对.y;
        if (!落格(相对.x, 相对顶, 物宽, 物高, out int 列, out int 行))
        {
            落点有效 = false;
            if (落点投影 != null) 落点投影.gameObject.SetActive(false);
            上次判定有效 = false;   // 出网格：缓存失效（回网格重算）
            return;   // 物品比网格大 或 网格未渲染：隐藏投影（代理仍跟手）
        }
        落点有效 = true; 落点列 = 列; 落点行 = 行;   // 记录本次投影格（放下用）
        // ② 落点投影：吸附网格贴格（鼠标在格内投影不移动，跨格才跳；绿/红/蓝指示落格合法性：可放/不可放/可合并）
        // 判定缓存：落点格/旋转 未变 → 复用上次结果（该格物品/可合并/可存入容器/区域互换 都是重算法，拖拽中每帧重算很贵）
        if (!(列 == 上次判定列 && 行 == 上次判定行 && 拖拽旋转 == 上次判定旋转 && 上次判定有效))
        {
            上次判定列 = 列; 上次判定行 = 行; 上次判定旋转 = 拖拽旋转; 上次判定有效 = true;
            var 目标 = 该格物品(列, 行);
            if (目标 != null && 目标 != 拖拽源 && 服务.可合并(目标, 拖拽源)) { 上次可放 = true; 上次可合并 = true; }
            else if (可存入容器(目标, 拖拽源)) { 上次可放 = true; 上次可合并 = false; }   // 目标格是容器物品且可存入 → 绿（松手=存入而非换位）
            else if (目标 != null && 目标 != 拖拽源 && ServiceRegistry.Get<容器服务>().是容器(目标)) { 上次可放 = false; 上次可合并 = false; }   // 严格：容器不做换位目标 → 红
            else { 上次可放 = 服务.区域可互换(拖拽源, 列, 行, 拖拽旋转); 上次可合并 = false; }   // 覆盖 移动(空区) + 整体换位(多物品/被占区)
        }
        if (落点投影 != null)
        {
            落点投影.gameObject.SetActive(true);
            落点投影.rectTransform.anchoredPosition = new Vector2(格x(列, 行), -行 * 格尺寸);   // 精确贴格（吸附网格）
            落点投影.color = 上次可合并 ? 网格面板配色.合并色 : (上次可放 ? 网格面板配色.放置可色 : 网格面板配色.放置禁色);
        }
    }

    // 代理尺寸 = 物品占格（不内缩）；投影尺寸 = 完整占格（贴格指示）——随 拖拽旋转（R 预览）同步；原位置影子保持原始占格不变
    private void 更新代理尺寸()
    {
        if (拖拽代理 == null || 拖拽源 == null) return;
        var 未旋转 = 服务.形状解析?.Invoke(拖拽源.标识) ?? new 物品形状(1, 1);
        var (宽, 高) = 预览占格(拖拽源);   // 按 拖拽旋转 计算（不写回 堆叠.旋转）
        // 代理根：尺寸 = 旋转后 占格（宽高已按旋转交换），根 不旋转——
        // RectMask2D 是轴对齐裁剪，根旋转会裁掉内容图（古早 bug：R 旋转后图片消失）；内容图旋转由子层 localRotation 承担（同物品框）
        拖拽代理.sizeDelta = new Vector2(宽 * 格尺寸, 高 * 格尺寸);
        拖拽代理.localRotation = Quaternion.identity;
        // 内容图 智能 cover：按 未旋转 宽高 等比放大至覆盖整个占格（与 物品框 一致——旋转后恰好匹配，不溢出裁剪）
        var 内容矩 = 拖拽代理.GetChild(0) as RectTransform;
        if (内容矩 != null)
        {
            float 内容宽 = 未旋转.宽 * 格尺寸;
            float 内容高 = 未旋转.高 * 格尺寸;
            var 图标 = 内容矩.GetComponent<Image>().sprite;
            if (图标 != null)
            {
                var 盒 = 精灵内容包围盒.获取(图标);   // position=内容中心(归一化)，size=内容占比(归一化)
                float 画布宽 = 图标.bounds.size.x, 画布高 = 图标.bounds.size.y;
                float 内容宽盒 = 画布宽 * 盒.width, 内容高盒 = 画布高 * 盒.height;
                float 放大 = Mathf.Max(内容宽 / 内容宽盒, 内容高 / 内容高盒);
                内容宽 = 画布宽 * 放大;
                内容高 = 画布高 * 放大;
                内容矩.pivot = new Vector2(盒.x, 盒.y);   // pivot 移到 内容中心：放大后 内容 居中于占格
            }
            内容矩.sizeDelta = new Vector2(内容宽, 内容高);
            内容矩.localRotation = Quaternion.Euler(0f, 0f, 拖拽旋转 ? 90f : 0f);   // 内容图跟随旋转（根不转——RectMask2D 轴对齐裁剪）
        }
        if (落点投影 != null) 落点投影.rectTransform.sizeDelta = new Vector2(宽 * 格尺寸, 高 * 格尺寸);   // 投影贴格（旋转后）
        // 注：原位置影子不更新——它表示物品原本的占格（原始旋转），旋转预览只影响新位置
    }

    // 拖拽预览占格：按 拖拽旋转（R 预览）计算宽高（不写回 堆叠.旋转；拖拽开始时与物品当前旋转一致）
    private (int 宽, int 高) 预览占格(物品堆叠 堆叠)
    {
        var 未旋转 = 服务.形状解析?.Invoke(堆叠.标识) ?? new 物品形状(1, 1);
        return 拖拽旋转 ? (未旋转.高, 未旋转.宽) : (未旋转.宽, 未旋转.高);
    }

    // R 键检测（拖拽中旋转预览）：兼容新(InputSystem)/旧(Input Manager)
    private bool 检测按R()
    {
        bool 按下 = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) 按下 = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.R)) 按下 = true;
#endif
        return 按下;
    }

    // 结束拖拽：按拖拽中最后投影的落格 → 空格=移动(带旋转) / 被占=换位 / 网格外=取消 / 别的面板上=跨网格转移。
    // 音效：成功 播放下，失败 只播失败（不再无条件播放下）。
    private void 结束拖拽(PointerEventData 事件)
    {
        var 源 = 拖拽源;
        拖拽源 = null;
        清理所有面板投影();   // 隐藏其他面板（容器）跨面板显示的投影
        装备面板.实例?.清除全部高亮();   // 装备槽高亮（背包↔装备区拖拽提示）
        if (拖拽代理 != null) { Destroy(拖拽代理.gameObject); 拖拽代理 = null; }
        if (落点投影 != null) { Destroy(落点投影.gameObject); 落点投影 = null; }
        if (原位置影子 != null) { Destroy(原位置影子); 原位置影子 = null; }
        if (源 == null) { 拖拽发起面板 = null; return; }
        bool 成功 = false;   // 各分支统一：成功 播放下，失败 只播失败
        // 跨面板：先找鼠标下方是否有别的 网格面板（容器面板/穿戴容器块/仓库）
        var 目标面板 = 事件下方面板(事件);
        if (目标面板 != null && 目标面板 != this)
        {
            成功 = 目标面板.接收跨面板转移(服务, 源, 事件, 拖拽旋转);   // 传拖拽旋转：跨面板 R 旋转 生效
            if (成功) 音效管理器.实例?.播放放下();
            else 音效管理器.实例?.播放失败();
            拖拽发起面板 = null; 拖拽源服务 = null; 拖拽中堆叠 = null;
            return;
        }
        拖拽发起面板 = null; 拖拽源服务 = null; 拖拽中堆叠 = null;
        // 容器面板 底座拦截：鼠标在 任一 容器面板 底座 上 → 落点归面板（不触发底下装备槽/面板）。
        // 叠放（两个 容器面板 上下 重叠）取 最上层：鼠标 在 上层 网格内 时 下层 也 命中（矩形 重叠），
        // 若 按 下层 拦截 会 导致 上层 面板 内 无法 拖拽 放下（叠放 拖拽 修复）。
        // 自己面板：网格内 松手 = 同面板 移动/换位（跳过 装备槽 分支——网格内 落点 也可能 对准 下层 装备槽，
        // 若 放行 到 装备槽 分支 会 穿透 装备 —— 穿透 修复）；底座空白区 = 拦截取消。
        var 自己面板 = GetComponentInParent<容器面板>();
        var 最上层容器 = 最上层容器面板(事件);
        bool 在自己面板网格内 = false;
        if (最上层容器 != null)
        {
            if (最上层容器 != 自己面板) { 音效管理器.实例?.播放失败(); return; }   // 其他 容器面板 上 → 拦截
            if (!最上层容器.命中网格(事件.position)) { 音效管理器.实例?.播放失败(); return; }   // 底座空白区 → 拦截
            在自己面板网格内 = true;   // 网格内 → 同面板 移动/换位
        }
        // 拖到装备槽位（装备面板）→ 穿戴：槽位兼容才可穿（实例级换装到"命中槽位"，旧件回背包/回滚已处理）
        // 自己面板 网格内 松手 不 走 装备槽（落点 归 容器面板）
        if (!在自己面板网格内 && 装备面板.实例 != null && 装备面板.实例.命中槽位(事件.position, out var 槽位名) && 源 != null)
        {
            成功 = 数据.物品.TryGetValue(源.标识, out var 装备) && 面板操作.槽位匹配(装备.槽位, 槽位名)
                && 面板操作.换装堆叠(档案, 源, 槽位名, 服务);   // 指定目标槽 + 源服务（穿戴容器/主背包）；内部已播 装备音效
            if (成功) { 请求刷新(); }
            else 音效管理器.实例?.播放失败();
            return;
        }
        if (!落点有效)
        {
            音效管理器.实例?.播放失败();   // 拖出网格外（无有效投影）= 取消
            return;
        }
        // 与拖拽中一致：直接用最后投影的落格（投影在哪就放在哪，不随松手鼠标重算）
        int 列 = 落点列, 行 = 落点行;
        var 目标物品 = 该格物品(列, 行);
        // 同标识可堆叠 → 合并；目标格是容器物品且允许+有空位 → 存入容器（而不是换位）；
        // 目标格是容器物品但不可存入 → 严格失败（容器不做换位目标）；否则 区域交换（空区=移动 / 被占区=整体换位）
        if (服务.合并堆叠(目标物品, 源) > 0) 成功 = true;
        else if (存入容器(目标物品, 源)) 成功 = true;   // 内部已处理 刷新/事件
        else if (目标物品 != null && 目标物品 != 源 && ServiceRegistry.Get<容器服务>().是容器(目标物品)) { }
        else if (服务.区域互换(源, 列, 行, 拖拽旋转)) 成功 = true;
        if (成功) { 请求刷新(); 音效管理器.实例?.播放放下(); }
        else 音效管理器.实例?.播放失败();
    }

    // 判定：目标格是容器物品 且 允许该类型 且 非容器类物品 且 内部有空位（含 旋转 90° 后 有空位）→ 可存入（投影显示绿色）
    private bool 可存入容器(物品堆叠 目标容器, 物品堆叠 堆叠)
    {
        if (目标容器 == null || 目标容器 == 堆叠) return false;
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        if (!容器服务.是容器(目标容器)) return false;
        if (!容器服务.允许放入(目标容器, 堆叠.标识)) return false;
        if (容器服务.是容器(堆叠)) return false;   // 嵌套限制：容器类物品不自动存入（需打开后手动放入）
        var 视图 = 容器服务.打开(目标容器);
        return 视图.寻找可放置格智能旋转(堆叠, out _) != null;   // 智能旋转：当前 放不下 试 旋转 90°
    }

    // 拖拽源 放到 目标容器物品 上：容器允许该类型 且 内部有空位（含 旋转 90°）→ 存入容器（而不是换位/失败）。
    // 智能旋转：当前旋转 放不下 → 自动 旋转 90° 存入（写回 堆叠.旋转；塔科夫式 快捷收入）。
    // 跨网格转移 复用：目标=容器视图（其 背包 与 容器.容器物品 同一引用，直接写入容器内部）。
    private bool 存入容器(物品堆叠 目标容器, 物品堆叠 堆叠)
    {
        if (!可存入容器(目标容器, 堆叠)) return false;
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        var 视图 = 容器服务.打开(目标容器);
        var 空位 = 视图.寻找可放置格智能旋转(堆叠, out bool 需旋转);
        if (空位 == null) return false;
        bool 原旋转 = 堆叠.旋转;
        if (需旋转) 堆叠.旋转 = !原旋转;   // 智能旋转：写回 堆叠（落格 用 旋转后 占格）
        int 转移 = 容器服务.跨网格转移(服务, 堆叠, 视图, 空位.Value.列, 空位.Value.行);
        if (转移 <= 0) { 堆叠.旋转 = 原旋转; return false; }   // 失败回滚旋转
        ServiceRegistry.Get<EventBus>().发布(new 背包变化事件(堆叠.标识, 转移, 变化原因.获得));   // 让打开的容器面板刷新
        return true;
    }

    // 鼠标下方的 网格面板：用矩形范围判断（不依赖 raycastTarget），返回命中面面板（含自己）。
    // 主背包：检查 网格容器 矩形；容器面板：检查其 面板根 矩形（更大，拖到面板任意处都能命中）
    // 排序：容器面板 用 面板根 sibling index（Canvas 顶层 同父，直接 可比——叠放 时 矩形 重叠，面积 无法 区分 上下）；
    //       普通面板 用 自身 sibling（局部；互不重叠 够用）。容器面板 在 Canvas 顶层 → 优先 于 普通面板。
    // 登记表遍历（替代 FindObjectsOfType——拖拽中每帧调用，FindObjectsOfType 很慢）
    private 网格面板 事件下方面板(PointerEventData 事件)
    {
        网格面板 容器命中 = null, 普通命中 = null;
        int 最大容器序号 = -1, 最大普通序号 = -1;
        foreach (var 面板 in 全部面板)
        {
            if (面板 == null || !面板.gameObject.activeInHierarchy) continue;
            var 容器面板 = 面板.所属容器 != null ? 面板.GetComponentInParent<容器面板>() : null;
            RectTransform 矩形;
            矩形 = 容器面板 != null ? (RectTransform)容器面板.transform : 面板.网格容器;
            if (矩形 == null) continue;
            if (!RectTransformUtility.RectangleContainsScreenPoint(矩形, 事件.position, 事件.pressEventCamera)) continue;
            if (容器面板 != null)
            {
                int 序号 = 容器面板.transform.GetSiblingIndex();   // Canvas 顶层 同父，直接 可比
                if (序号 > 最大容器序号) { 最大容器序号 = 序号; 容器命中 = 面板; }
            }
            else
            {
                int 序号 = 面板.transform.GetSiblingIndex();
                if (序号 > 最大普通序号) { 最大普通序号 = 序号; 普通命中 = 面板; }
            }
        }
        return 容器命中 ?? 普通命中;   // 容器面板 在 Canvas 顶层，优先
    }

    // 鼠标 位置 上 最上层 的 容器面板（叠放 时 取 视觉 最前；无 = null）。
    // 容器面板 都 挂 Canvas 顶层（同父），sibling index 直接 可比——矩形 重叠 时 面积 无法 区分 上下（叠放 穿透 修复）。
    private static 容器面板 最上层容器面板(PointerEventData 事件)
    {
        容器面板 最上层 = null;
        int 最大序号 = -1;
        foreach (var 面板 in FindObjectsOfType<容器面板>(true))
        {
            if (面板 == null || !面板.gameObject.activeInHierarchy) continue;
            if (!面板.命中(事件.position)) continue;
            int 序号 = 面板.transform.GetSiblingIndex();
            if (序号 > 最大序号) { 最大序号 = 序号; 最上层 = 面板; }
        }
        return 最上层;
    }

    // 鼠标 是否 在 任一 容器面板 底座 上（含 网格 区域）——拖拽中 阻隔 下方 面板 投影 用。
    // 排除 自己 所属 的 容器面板（本面板 就是 该 容器面板 的 网格 → 正常 显示 投影）；叠放 取 最上层。
    private bool 鼠标在容器面板底座上(PointerEventData 事件)
    {
        var 自己面板 = GetComponentInParent<容器面板>();
        var 最上层 = 最上层容器面板(事件);
        return 最上层 != null && 最上层 != 自己面板;
    }

    // 接收跨面板转移：把 (源服务) 里的 堆叠 放到本面板 (列,行)（由 容器服务.跨网格转移 执行）。返回 是否成功（音效由调用方播）
    // 拖拽旋转：发起面板 R 预览的旋转状态——跨面板 落格/转移 时应用（否则跨面板旋转被丢弃）
    public bool 接收跨面板转移(背包服务 源服务, 物品堆叠 堆叠, PointerEventData 事件, bool 拖拽旋转 = false)
    {
        if (源服务 == null || 堆叠 == null) return false;
        if (!屏幕到容器相对(事件, out var 相对, out var 尺寸)) return false;
        var 形状 = 服务.形状解析?.Invoke(堆叠.标识) ?? new 物品形状(1, 1);
        int 物宽 = 拖拽旋转 ? 形状.高 : 形状.宽;
        int 物高 = 拖拽旋转 ? 形状.宽 : 形状.高;
        float 相对顶 = 尺寸.y - 相对.y;
        if (!落格(相对.x, 相对顶, 物宽, 物高, out int 列, out int 行)) return false;
        // 转移前校验：目标容器类型限制 + 嵌套防护（自己套自己/循环/套娃上限）
        if (!目标允许放入(堆叠.标识)) return false;
        if (目标禁放入(堆叠)) return false;
        // 快捷收入：落点格 是 容器物品（如 背包里 的 医疗箱）→ 直接存入该容器（无需打开容器面板；同面板拖拽已有，跨面板补齐）
        var 落点物 = 服务.该格物品(列, 行);
        if (落点物 != null && 落点物 != 堆叠 && 可存入容器(落点物, 堆叠))
        {
            var 视图 = ServiceRegistry.Get<容器服务>().打开(落点物);
            var 空位 = 视图.寻找可放置格智能旋转(堆叠, out bool 需旋转);
            if (空位 != null)
            {
                bool 快捷原旋转 = 堆叠.旋转;
                if (需旋转) 堆叠.旋转 = !快捷原旋转;   // 智能旋转：横放 2×1 自动 竖放 存入
                int 存入数 = ServiceRegistry.Get<容器服务>().跨网格转移(源服务, 堆叠, 视图, 空位.Value.列, 空位.Value.行);
                if (存入数 <= 0) { 堆叠.旋转 = 快捷原旋转; return false; }   // 失败回滚旋转
                请求刷新();
                ServiceRegistry.Get<EventBus>().发布(new 背包变化事件(堆叠.标识, 存入数, 变化原因.获得));
                return true;
            }
            return false;   // 容器不允许类型 / 已满 → 失败
        }
        // 跨网格转移 前 应用拖拽旋转（临时改堆叠旋转 → 转移 → 落格判定用旋转后占格）
        bool 原旋转 = 堆叠.旋转;
        if (拖拽旋转 != 原旋转) 堆叠.旋转 = 拖拽旋转;
        int 转移 = ServiceRegistry.Get<容器服务>().跨网格转移(源服务, 堆叠, 服务, 列, 行);
        if (转移 <= 0) { 堆叠.旋转 = 原旋转; return false; }   // 失败回滚旋转
        // 成功：旋转已随堆叠入格（跨网格转移 内部 列/行 已按当前旋转写回）
        请求刷新();
        ServiceRegistry.Get<EventBus>().发布(new 背包变化事件(堆叠.标识, 转移, 变化原因.获得));
        return true;
    }

    // 屏幕相对 点 → 网格落格（物品中心对齐：四舍五入，偏差对称 ±半格内，1×1 精确——大物体不错位）
    // 块偏移 反算：行 无偏移；列 先按 无偏移 估算 所属块，再 用 该块 偏移 精算（两块 迭代 收敛）
    private bool 落格(float 相对x, float 相对顶, int 物宽, int 物高, out int 列, out int 行)
    {
        行 = Mathf.RoundToInt((相对顶 - 物高 * 格尺寸 / 2f) / 格尺寸);
        float 估算x = 相对x - 物宽 * 格尺寸 / 2f;
        int 估算列 = Mathf.RoundToInt(估算x / 格尺寸);
        // 用 估算列 找 所属块偏移，再精算（弹挂 等 并排块 偏移后 落格 正确）
        float 偏移 = 0f;
        if (块偏移 != null && 行 >= 0 && 行 < 当前行 && 估算列 >= 0 && 估算列 < 当前列)
        {
            int 块 = 服务.该格块(估算列, 行);
            if (块 >= 0 && 块 < 块偏移.Length) 偏移 = 块偏移[块];
        }
        列 = Mathf.RoundToInt((估算x - 偏移) / 格尺寸);
        return 列 >= 0 && 行 >= 0 && 列 < 当前列 && 行 < 当前行;
    }

    // 屏幕点 → 网格格坐标（装备拖拽落点用；物品中心对齐 + 越界贴边，同背包内拖拽判定）
    public bool 屏幕到格(Vector2 屏幕点, 物品堆叠 堆叠, out int 列, out int 行)
    {
        列 = 行 = -1;
        if (堆叠 == null) return false;
        var 伪事件 = new PointerEventData(EventSystem.current) { position = 屏幕点 };
        if (!屏幕到容器相对(伪事件, out var 相对, out var 尺寸)) return false;
        var (物宽, 物高) = 服务.物品占格(堆叠);
        float 相对顶 = 尺寸.y - 相对.y;
        return 落格(相对.x, 相对顶, 物宽, 物高, out 列, out 行);
    }

    // —— 装备拖拽投影（装备区拖装备 → 背包网格：落格投影，可放绿/不可放红，同背包内拖拽）——
    public void 显示装备拖拽投影(物品堆叠 堆叠, Vector2 屏幕点, bool 强制禁 = false)
    {
        确保投影();
        if (落点投影 == null) return;
        if (强制禁)   // 保护（如"穿戴容器不能放进自己容器"）→ 全网格红框
        {
            落点投影.gameObject.SetActive(true);
            落点投影.rectTransform.anchoredPosition = Vector2.zero;
            落点投影.rectTransform.sizeDelta = new Vector2(当前列 * 格尺寸 + 最右偏移(), 当前行 * 格尺寸);
            落点投影.color = 网格面板配色.放置禁色;
            return;
        }
        if (堆叠 == null || !屏幕到格(屏幕点, 堆叠, out var 列, out var 行))
        {
            落点投影.gameObject.SetActive(false);
            return;
        }
        var (宽, 高) = 服务.物品占格(堆叠);
        落点投影.gameObject.SetActive(true);
        落点投影.rectTransform.anchoredPosition = new Vector2(格x(列, 行), -行 * 格尺寸);
        落点投影.rectTransform.sizeDelta = new Vector2(宽 * 格尺寸, 高 * 格尺寸);
        落点投影.color = 服务.可放置(堆叠.标识, 列, 行, 堆叠.旋转) ? 网格面板配色.放置可色 : 网格面板配色.放置禁色;
    }

    public void 隐藏装备拖拽投影()
    {
        if (落点投影 != null) 落点投影.gameObject.SetActive(false);
    }
}
