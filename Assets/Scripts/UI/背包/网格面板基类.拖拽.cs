using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// ============================================================
// 网格面板基类.拖拽 —— 分部类：拖拽框架（代理 / 投影 / 移动 / R旋转 / 落格 / 事件下方面板 / 结束分派）。
// 与 网格面板基类.cs 同一 partial 类（共享字段与方法）；纯组织性拆分，无行为改动。
// ============================================================
public abstract partial class 网格面板基类
{
    protected void 确保投影()
    {
        if (落点投影 != null || 物品层 == null) return;
        var 投体 = new GameObject("落点投影", typeof(RectTransform), typeof(Image));
        投体.transform.SetParent(物品层, false);
        落点投影 = 投体.GetComponent<Image>();
        落点投影.color = 网格面板配色.放置可色;
        落点投影.raycastTarget = false;
        var 投影矩形 = 投体.GetComponent<RectTransform>();
        UI工具.设锚点(投影矩形, new Vector2(0, 1), new Vector2(0, 1));
        落点投影.gameObject.SetActive(false);
    }

    protected Vector2 输入鼠标位置()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null) return UnityEngine.InputSystem.Mouse.current.position.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#endif
        return Vector2.zero;
    }

    protected static void 清理所有面板投影()
    {
        foreach (var 面板 in 全部面板)
            if (面板.落点投影 != null) 面板.落点投影.gameObject.SetActive(false);
    }

    protected void 开始拖拽(物品堆叠 堆叠, PointerEventData 事件)
    {
        if (!允许开始拖拽(堆叠)) return;   // 钩子：家具 摆放 模式 忽略
        右键菜单.实例?.隐藏();
        音效管理器.实例?.播放拿起();
        拖拽源 = 堆叠;
        拖拽旋转 = 堆叠.旋转;
        落点有效 = false;
        上次判定有效 = false;
        拖拽发起面板 = this;
        拖拽源服务 = 服务;
        拖拽中堆叠 = 堆叠;
        创建拖拽视觉(堆叠);
        拖拽移动(事件);
    }

    protected void 创建拖拽视觉(物品堆叠 堆叠)
    {
        var 物体 = new GameObject("拖拽代理", typeof(RectTransform), typeof(RectMask2D));
        var 顶层 = GetComponentInParent<Canvas>();
        物体.transform.SetParent(顶层 != null ? 顶层.transform : transform.root, false);
        var 内容体 = new GameObject("内容", typeof(RectTransform), typeof(Image));
        内容体.transform.SetParent(物体.transform, false);
        var 图 = 内容体.GetComponent<Image>();
        图.raycastTarget = false;
        创建代理内容(图, 堆叠);   // 钩子：物品=图标/品质底；家具=色块
        var 内容矩 = 内容体.GetComponent<RectTransform>();
        UI工具.设锚点(内容矩, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        内容矩.anchoredPosition = Vector2.zero;
        拖拽代理 = 物体.GetComponent<RectTransform>();
        UI工具.设锚点(拖拽代理, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        确保投影();
        更新代理尺寸();
        var 影体 = new GameObject("原位置影子", typeof(RectTransform), typeof(Image));
        影体.transform.SetParent(物品层, false);
        var 影图 = 影体.GetComponent<Image>();
        影图.color = new Color(0.65f, 0.65f, 0.7f, 0.3f);
        影图.raycastTarget = false;
        var 影矩形 = 影体.GetComponent<RectTransform>();
        var (影宽, 影高) = 服务.物品占格(堆叠);
        UI工具.设锚(影矩形, new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(格x(堆叠.列, 堆叠.行) + 网格面板配色.物品边距, -堆叠.行 * 格尺寸 - 网格面板配色.物品边距),
            new Vector2(影宽 * 格尺寸 - 网格面板配色.物品边距 * 2f, 影高 * 格尺寸 - 网格面板配色.物品边距 * 2f));
        原位置影子 = 影体;
        拖拽代理.SetAsLastSibling();
    }

    protected void 更新代理尺寸()
    {
        if (拖拽代理 == null || 拖拽源 == null) return;
        var 未旋转 = 服务.形状解析?.Invoke(拖拽源.标识) ?? new 物品形状(1, 1);
        var (宽, 高) = 预览占格(拖拽源);
        拖拽代理.sizeDelta = new Vector2(宽 * 格尺寸, 高 * 格尺寸);
        拖拽代理.localRotation = Quaternion.identity;
        var 内容矩 = 拖拽代理.GetChild(0) as RectTransform;
        if (内容矩 != null)
        {
            float 内容宽 = 未旋转.宽 * 格尺寸;
            float 内容高 = 未旋转.高 * 格尺寸;
            var 图标 = 内容矩.GetComponent<Image>().sprite;
            if (图标 != null)
            {
                var 盒 = 精灵内容包围盒.获取(图标);
                float 画布宽 = 图标.bounds.size.x, 画布高 = 图标.bounds.size.y;
                float 内容宽盒 = 画布宽 * 盒.width, 内容高盒 = 画布高 * 盒.height;
                float 放大 = Mathf.Max(内容宽 / 内容宽盒, 内容高 / 内容高盒);
                内容宽 = 画布宽 * 放大;
                内容高 = 画布高 * 放大;
                内容矩.pivot = new Vector2(盒.x, 盒.y);
            }
            内容矩.sizeDelta = new Vector2(内容宽, 内容高);
            内容矩.localRotation = Quaternion.Euler(0f, 0f, 拖拽旋转 ? 90f : 0f);
        }
        if (落点投影 != null) 落点投影.rectTransform.sizeDelta = new Vector2(宽 * 格尺寸, 高 * 格尺寸);
    }

    protected (int 宽, int 高) 预览占格(物品堆叠 堆叠)
    {
        var 未旋转 = 服务.形状解析?.Invoke(堆叠.标识) ?? new 物品形状(1, 1);
        return 拖拽旋转 ? (未旋转.高, 未旋转.宽) : (未旋转.宽, 未旋转.高);
    }

    protected bool 检测按R()
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

    // 屏幕点 → 相对容器左下（逻辑单位，除以 Canvas 缩放——与 格尺寸 同基准）
    protected bool 屏幕到容器相对(PointerEventData 事件, out Vector2 相对, out Vector2 容器尺寸)
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
        相对 = new Vector2((世界点.x - 原点.x) / 缩放.x, (世界点.y - 原点.y) / 缩放.y);
        return true;
    }

    // 拖拽中：代理跟手 + 落点判定（装备槽 钩子 + 容器面板 底座 拦截 + 落格 投影）
    protected void 拖拽移动(PointerEventData 事件)
    {
        if (拖拽源 == null || 拖拽代理 == null) return;
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
        var 下方面板 = 事件下方面板(事件);
        var 自己面板 = GetComponentInParent<容器面板>();
        var 最上层容器 = 最上层容器面板(事件);
        if (最上层容器 != null)
        {
            if (最上层容器 != 自己面板)
            {
                落点有效 = false;
                if (落点投影 != null) 落点投影.gameObject.SetActive(false);
                return;
            }
            if (!最上层容器.命中网格(事件.position))
            {
                落点有效 = false;
                if (落点投影 != null) 落点投影.gameObject.SetActive(false);
                清除拖拽高亮();
                return;
            }
        }
        else
        {
            if (下方面板 != null && 下方面板 != this)
            {
                落点有效 = false;
                if (落点投影 != null) 落点投影.gameObject.SetActive(false);
                return;
            }
            if (处理装备槽落点(事件)) return;   // 粗钩子：物品=命中+匹配+高亮+消费；家具=false 走正常落格
            清除拖拽高亮();
        }
        if (!屏幕到容器相对(事件, out var 相对, out var 尺寸))
        {
            if (落点投影 != null) 落点投影.gameObject.SetActive(false);
            return;
        }
        var (物宽, 物高) = 预览占格(拖拽源);
        float 相对顶 = 尺寸.y - 相对.y;
        if (!落格(相对.x, 相对顶, 物宽, 物高, out int 列, out int 行))
        {
            落点有效 = false;
            if (落点投影 != null) 落点投影.gameObject.SetActive(false);
            上次判定有效 = false;
            return;
        }
        落点有效 = true; 落点列 = 列; 落点行 = 行;
        if (!(列 == 上次判定列 && 行 == 上次判定行 && 拖拽旋转 == 上次判定旋转 && 上次判定有效))
        {
            上次判定列 = 列; 上次判定行 = 行; 上次判定旋转 = 拖拽旋转; 上次判定有效 = true;
            var 目标 = 该格物品(列, 行);
            if (目标 != null && 目标 != 拖拽源 && 服务.可合并(目标, 拖拽源)) { 上次可放 = true; 上次可合并 = true; }
            else if (可存入容器(目标, 拖拽源)) { 上次可放 = true; 上次可合并 = false; }
            else if (目标 != null && 目标 != 拖拽源 && ServiceRegistry.Get<容器服务>().是容器(目标)) { 上次可放 = false; 上次可合并 = false; }
            else { 上次可放 = 服务.区域可互换(拖拽源, 列, 行, 拖拽旋转); 上次可合并 = false; }
        }
        if (落点投影 != null)
        {
            落点投影.gameObject.SetActive(true);
            落点投影.rectTransform.anchoredPosition = new Vector2(格x(列, 行), -行 * 格尺寸);
            落点投影.color = 上次可合并 ? 网格面板配色.合并色 : (上次可放 ? 网格面板配色.放置可色 : 网格面板配色.放置禁色);
        }
    }

    // 结束拖拽（内部组件 调用）：清理 视觉 → 钩子 完成拖拽（放置 语义）→ 收尾
    protected void 结束拖拽(PointerEventData 事件)
    {
        var 源 = 拖拽源;
        拖拽源 = null;
        清理所有面板投影();
        清除拖拽高亮();
        if (拖拽代理 != null) { Destroy(拖拽代理.gameObject); 拖拽代理 = null; }
        if (落点投影 != null) { Destroy(落点投影.gameObject); 落点投影 = null; }
        if (原位置影子 != null) { Destroy(原位置影子); 原位置影子 = null; }
        if (源 == null) { 拖拽发起面板 = null; return; }
        bool 成功 = 完成拖拽(事件, 源);   // 钩子：物品=跨面板/装备槽/合并/存入/区域互换；家具=移动/换位
        if (成功) { 请求刷新(); 音效管理器.实例?.播放放下(); }
        else 音效管理器.实例?.播放失败();
        拖拽发起面板 = null; 拖拽源服务 = null; 拖拽中堆叠 = null;
    }

    // 判定：目标格是容器物品 且 允许该类型 且 内部有空位 → 可存入（物品 语义；家具 网格 无 容器 无 影响）
    protected bool 可存入容器(物品堆叠 目标容器, 物品堆叠 堆叠)
    {
        if (目标容器 == null || 目标容器 == 堆叠) return false;
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        if (!容器服务.是容器(目标容器)) return false;
        if (!容器服务.允许放入(目标容器, 堆叠.标识)) return false;
        if (容器服务.是容器(堆叠)) return false;
        var 视图 = 容器服务.打开(目标容器);
        return 视图.寻找可放置格智能旋转(堆叠, out _) != null;
    }

    // 鼠标下方的 网格面板基类（矩形范围判定；容器面板 优先 于 普通面板）
    protected 网格面板基类 事件下方面板(PointerEventData 事件)
    {
        网格面板基类 容器命中 = null, 普通命中 = null;
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
                int 序号 = 容器面板.transform.GetSiblingIndex();
                if (序号 > 最大容器序号) { 最大容器序号 = 序号; 容器命中 = 面板; }
            }
            else
            {
                int 序号 = 面板.transform.GetSiblingIndex();
                if (序号 > 最大普通序号) { 最大普通序号 = 序号; 普通命中 = 面板; }
            }
        }
        return 容器命中 ?? 普通命中;
    }

    protected static 容器面板 最上层容器面板(PointerEventData 事件)
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

    protected bool 鼠标在容器面板底座上(PointerEventData 事件)
    {
        var 自己面板 = GetComponentInParent<容器面板>();
        var 最上层 = 最上层容器面板(事件);
        return 最上层 != null && 最上层 != 自己面板;
    }

    // 屏幕相对 点 → 网格落格（物品中心对齐 四舍五入；块偏移 反算）
    protected bool 落格(float 相对x, float 相对顶, int 物宽, int 物高, out int 列, out int 行)
    {
        行 = Mathf.RoundToInt((相对顶 - 物高 * 格尺寸 / 2f) / 格尺寸);
        float 估算x = 相对x - 物宽 * 格尺寸 / 2f;
        int 估算列 = Mathf.RoundToInt(估算x / 格尺寸);
        float 偏移 = 0f;
        if (块偏移 != null && 行 >= 0 && 行 < 当前行 && 估算列 >= 0 && 估算列 < 当前列)
        {
            int 块 = 服务.该格块(估算列, 行);
            if (块 >= 0 && 块 < 块偏移.Length) 偏移 = 块偏移[块];
        }
        列 = Mathf.RoundToInt((估算x - 偏移) / 格尺寸);
        return 列 >= 0 && 行 >= 0 && 列 < 当前列 && 行 < 当前行;
    }

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

    // 装备拖拽投影（装备区 拖装备 → 网格：落点投影；摆放 模式 复用）
    public void 显示装备拖拽投影(物品堆叠 堆叠, Vector2 屏幕点, bool 强制禁 = false)
    {
        确保投影();
        if (落点投影 == null) return;
        if (强制禁)
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
