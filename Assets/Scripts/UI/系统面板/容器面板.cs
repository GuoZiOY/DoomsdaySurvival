using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

    // 容器面板：完全代码动态搭建的浮动容器面板（塔科夫式）。
    // 双击容器物品 → 动态生成并挂到 Canvas 顶层（不受 ScrollRect/Viewport 裁剪、不被任何面板覆盖——与跨面板拖拽代理一致）：
    //   面板根（Image 半透明底，可整面板拖拽移动，限制在屏幕内）→ 标题 + 关闭按钮 + 网格容器（挂 网格背包面板 组件显示容器内容）。
    // 关闭时销毁；与主背包跨网格拖拽转移（矩形判断，见 网格背包面板.事件下方面板）。
    public sealed class 容器面板 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // 已打开的容器实例 → 面板；同一容器不可重复打开（双击/连点防重）
        private static readonly System.Collections.Generic.Dictionary<物品堆叠, 容器面板> 已打开
            = new System.Collections.Generic.Dictionary<物品堆叠, 容器面板>();

        // ===== 布局常量（集中调整容器面板外观：大小/格子/间距，改这里全局生效） =====
        private const float 格尺寸 = 100f;            // 容器内单格像素（固定值；90 足够普通容器使用，最多可摆约 10 列宽；面板大小随容量 列×行 自动伸缩）
        private const float 边距 = 12f;              // 网格/文本 距面板左缘
        private const float 标题高 = 56f;            // 顶部行占位（上边距 12 + 标题行 44）
        private const float 信息条宽 = 180f;         // 顶部行：信息条固定宽（右段，贴按钮左侧；标题让位给它）
        private const float 信息条高 = 30f;          // 顶部行：信息条高度（容器统计；物品详情统一显示在主背包详情文本）
        private const float 顶行间距 = 10f;          // 顶部行：标题 与 信息条 间距
        private const float 按钮尺寸 = 40f;          // 顶部行：关闭按钮尺寸（右上角）
        private const float 区间距 = 6f;             // 顶部行 与网格 及底边距
        private const float 最小面板宽 = 360f;       // 面板宽度下限（容纳顶部行：标题+信息条+按钮；小容器面板高度仍随容量伸缩）
        private const float 最小面板高 = 250f;       // 面板高度下限（顶部行 56 + 2×2 网格 180 + 底边距）
        private const float 初始宽 = 480f, 初始高 = 420f;   // 创建时占位尺寸（显示容器时按网格覆盖）
        private const float 面板底透明 = 1f;    // 面板底座透明度（半透明，可透出下层主背包；调低更透）
        private const float 初始右偏比例 = 0.15f;    // 初始位置：主背包左上 向右偏移比例（×挂载父宽）
        private const float 初始下偏 = 20f;          // 初始位置：向下偏移
        private const float 兜底右偏比例 = 0.4f;     // 位置换算失败兜底：Canvas 左上偏右比例

        private 网格背包面板 容器网格;   // 动态创建（显示容器内部）
        private RectTransform 面板根;     // 动态创建（含 Image，可拖拽）
        private RectTransform 网格容器;   // 动态创建（容器内部网格的 Content）
        private RectTransform 信息条矩形;  // 动态创建（标题下方：容器统计；物品详情统一显示在主背包详情文本）
        private 物品堆叠 当前容器;
        private Vector2 拖拽偏移;

        // 供 网格背包面板 双击时调用：动态搭建容器面板并显示
        public static 容器面板 创建(RectTransform 挂载父, 物品堆叠 容器)
        {
            if (容器 == null) return null;
            // 防重：同一容器已打开 → 提到最上层并复用，不重复创建
            容器面板 已有;
            if (已打开.TryGetValue(容器, out 已有) && 已有 != null)
            {
                已有.面板根.SetAsLastSibling();   // 聚焦已有面板（顶到最上层）
                return 已有;
            }
            var 服务 = ServiceRegistry.Get<容器服务>();
            // 挂 Canvas 顶层：不受 ScrollRect/Viewport 裁剪、不被任何面板覆盖（与跨面板拖拽代理一致）
            var 画布 = 挂载父.GetComponentInParent<Canvas>();
            var 顶层 = 画布 != null ? (RectTransform)画布.transform : 挂载父;
            var 物体 = new GameObject("容器面板", typeof(RectTransform), typeof(Image));
            var 根 = 物体.GetComponent<RectTransform>();
            根.SetParent(顶层, false);
            根.SetAsLastSibling();   // 容器面板在 Canvas 最上层（拖拽代理创建时再顶到其上）
            根.anchorMin = new Vector2(0, 1);   // 左上锚定
            根.anchorMax = new Vector2(0, 1);
            根.pivot = new Vector2(0, 1);
            // 初始位置：主背包 ScrollRect 左上角 → Canvas 局部坐标 → 相对 Canvas 左上锚点(0,1) 的偏移 + 右下偏移（避开原挂载点）
            var 相机 = 画布 != null && 画布.renderMode != RenderMode.ScreenSpaceOverlay ? 画布.worldCamera : null;
            Vector2 挂载父左上;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(顶层,
                    RectTransformUtility.WorldToScreenPoint(相机, 挂载父.TransformPoint(new Vector3(挂载父.rect.xMin, 挂载父.rect.yMax, 0f))),
                    相机, out 挂载父左上))
                // 锚点(0,1)=Canvas 左上在局部坐标 (rect.xMin, rect.yMax)；偏移 = 挂载父左上 - 锚点（y 需取负方向：往下）
                根.anchoredPosition = 挂载父左上 - new Vector2(顶层.rect.xMin, 顶层.rect.yMax) + new Vector2(挂载父.rect.width * 初始右偏比例, -初始下偏);
            else
                根.anchoredPosition = new Vector2(顶层.rect.width * 兜底右偏比例, -初始下偏);   // 兜底：Canvas 左上偏右（屏幕内）
            根.sizeDelta = new Vector2(初始宽, 初始高);   // 初始占位（实际按容器网格尺寸在 显示容器 里覆盖）
            var 图 = 物体.GetComponent<Image>();
            图.color = new Color(0.08f, 0.08f, 0.1f, 面板底透明);   // 半透明底（透出下层，透明度见常量）

            var 面板 = 物体.AddComponent<容器面板>();
            面板.面板根 = 根;
            面板.当前容器 = 容器;
            已打开[容器] = 面板;   // 登记：同一容器只允许一个面板
            面板.构建子结构(服务);
            面板.显示容器(容器);
            return 面板;
        }

        // 构建标题/关闭按钮/网格容器（全部代码生成）
        private void 构建子结构(容器服务 服务)
        {
            // 顶部行：标题（左，拉伸自适应——右侧让位给 信息条+按钮；超宽省略号截断）——同一水平行：标题 | 信息条 | 关闭按钮
            var 标题物体 = new GameObject("标题", typeof(RectTransform), typeof(TextMeshProUGUI));
            标题物体.transform.SetParent(面板根, false);
            var 标题矩形 = 标题物体.GetComponent<RectTransform>();
            标题矩形.anchorMin = new Vector2(0, 1);
            标题矩形.anchorMax = new Vector2(1, 1);
            标题矩形.pivot = new Vector2(0.5f, 1);
            标题矩形.offsetMin = new Vector2(边距, -12f - 44f);
            标题矩形.offsetMax = new Vector2(-(8f + 按钮尺寸 + 8f + 顶行间距 + 信息条宽), -12f);
            var 标题文本 = 标题物体.GetComponent<TextMeshProUGUI>();
            标题文本.fontSize = 30f;
            标题文本.alignment = TextAlignmentOptions.Left;
            标题文本.color = Color.white;
            标题文本.raycastTarget = false;
            标题文本.overflowMode = TextOverflowModes.Ellipsis;   // 面板窄时省略号
            // 顶部行：关闭按钮（右上角 "×"）
            var 按钮物体 = new GameObject("关闭", typeof(RectTransform), typeof(Image), typeof(Button));
            按钮物体.transform.SetParent(面板根, false);
            var 按钮矩形 = 按钮物体.GetComponent<RectTransform>();
            按钮矩形.anchorMin = new Vector2(1, 1);
            按钮矩形.anchorMax = new Vector2(1, 1);
            按钮矩形.pivot = new Vector2(1, 1);
            按钮矩形.anchoredPosition = new Vector2(-8f, -12f);
            按钮矩形.sizeDelta = new Vector2(按钮尺寸, 按钮尺寸);
            按钮物体.GetComponent<Image>().color = new Color(1f, 0.3f, 0.3f, 0.8f);
            按钮物体.GetComponent<Button>().onClick.AddListener(关闭);
            // 按钮上的 "x" 文本（撑满按钮；不拦截点击——点击落在按钮组件上）
            var 按钮文本物体 = new GameObject("文本", typeof(RectTransform), typeof(TextMeshProUGUI));
            按钮文本物体.transform.SetParent(按钮物体.transform, false);
            var 按钮文本矩形 = 按钮文本物体.GetComponent<RectTransform>();
            按钮文本矩形.anchorMin = Vector2.zero;
            按钮文本矩形.anchorMax = Vector2.one;
            按钮文本矩形.offsetMin = Vector2.zero;
            按钮文本矩形.offsetMax = Vector2.zero;
            var 按钮文本 = 按钮文本物体.GetComponent<TextMeshProUGUI>();
            按钮文本.text = "x";
            按钮文本.fontSize = 28f;
            按钮文本.alignment = TextAlignmentOptions.Center;
            按钮文本.color = Color.white;
            按钮文本.raycastTarget = false;   // 不拦截点击（按钮在父物体上）
            // 网格容器（容器内部网格的 Content；挂 网格背包面板 组件渲染）
            // 用左上锚定 + 固定尺寸（不撑满面板根）：网格尺寸 = 列×格尺寸，面板根按它适配
            var 网格物体 = new GameObject("容器网格", typeof(RectTransform));
            网格物体.transform.SetParent(面板根, false);
            var 网格矩形 = 网格物体.GetComponent<RectTransform>();
            网格矩形.anchorMin = new Vector2(0, 1);
            网格矩形.anchorMax = new Vector2(0, 1);
            网格矩形.pivot = new Vector2(0, 1);
            网格矩形.anchoredPosition = new Vector2(边距, -标题高 - 区间距);   // 顶部行下方
            网格矩形.sizeDelta = new Vector2(格尺寸, 格尺寸);         // 占位，显示容器时按实际网格尺寸覆盖
            网格容器 = 网格矩形;
            // 动态挂 网格背包面板 组件（复用全部网格渲染/拖拽/转移逻辑）
            容器网格 = 网格物体.AddComponent<网格背包面板>();
            容器网格.绑定网格容器(网格矩形);
            容器网格.配置容器显示(格尺寸);   // 容器内格子尺寸
            // 顶部行：信息条（固定宽，右段贴按钮左侧，右对齐；容器统计：已用格/负重）
            var 信息条物体 = new GameObject("信息条", typeof(RectTransform), typeof(TextMeshProUGUI));
            信息条物体.transform.SetParent(面板根, false);
            信息条矩形 = 信息条物体.GetComponent<RectTransform>();
            信息条矩形.anchorMin = new Vector2(1, 1);
            信息条矩形.anchorMax = new Vector2(1, 1);
            信息条矩形.pivot = new Vector2(1, 1);
            信息条矩形.anchoredPosition = new Vector2(-(8f + 按钮尺寸 + 8f), -12f);   // 按钮左侧
            信息条矩形.sizeDelta = new Vector2(信息条宽, 信息条高);
            var 信息条文本 = 信息条物体.GetComponent<TextMeshProUGUI>();
            信息条文本.fontSize = 21f;
            信息条文本.alignment = TextAlignmentOptions.Right;
            信息条文本.color = new Color(0.85f, 0.85f, 0.9f, 1f);
            信息条文本.raycastTarget = false;
            信息条文本.enableWordWrapping = false;
            信息条文本.overflowMode = TextOverflowModes.Ellipsis;   // 面板窄时省略号
            容器网格.绑定信息(信息条文本);   // 详情不绑：统一显示在主背包详情文本
        }

        // 显示容器：注入容器视图数据源并渲染
        private void 显示容器(物品堆叠 容器)
        {
            当前容器 = 容器;
            var 服务 = ServiceRegistry.Get<容器服务>();
            服务.初始化容器(容器);
            容器网格.数据源 = 服务.打开(容器);
            容器网格.所属容器 = 容器;
            var 标题 = 面板根.Find("标题")?.GetComponent<TextMeshProUGUI>();
            if (标题 != null) 标题.text = 容器.标识;
            容器网格.重载网格();   // 格尺寸固定（90），按容器 列×行 渲染
            // 面板根大小 = 顶部行（标题|信息条|按钮） + 网格 + 底边距；随容量伸缩，保证最小可操作尺寸
            float 网格宽 = 容器网格.渲染列 * 容器网格.格子尺寸;
            float 网格高 = 容器网格.渲染行 * 容器网格.格子尺寸;
            // 布局：顶部行 y=0~-标题高 → 网格 (边距,-标题高-区间距) 高 网格高 → 底边距（信息条用 offset 拉伸，随面板宽自适应）
            面板根.sizeDelta = new Vector2(Mathf.Max(最小面板宽, 网格宽 + 边距 * 2f), Mathf.Max(最小面板高, 标题高 + 区间距 + 网格高 + 区间距));
            网格容器.sizeDelta = new Vector2(网格宽, 网格高);
            限制在屏幕内();   // 面板尺寸定稿后自动校正位置，确保创建出来就在屏幕内
        }

        // 把面板位置限制在父（Canvas 顶层）范围内：创建后/拖拽时调用，防止面板出屏（出屏既看不见也点不到）
        // 锚 0,1 = 面板左上对齐父左上（父左上局部坐标 = (rect.xMin, rect.yMax)，anchoredPosition = 面板左上局部 − 父左上）：
        //   x 向右为正：贴左=0，贴右=父宽−面板宽；y 向下为负：贴顶=0，贴底=−(父高−面板高)
        private void 限制在屏幕内()
        {
            var 父 = 面板根.parent as RectTransform;
            if (父 == null) return;
            var 位置 = 面板根.anchoredPosition;
            float 父宽 = 父.rect.width, 父高 = 父.rect.height;
            float 面板宽 = 面板根.sizeDelta.x, 面板高 = 面板根.sizeDelta.y;
            位置.x = Mathf.Clamp(位置.x, 0f, Mathf.Max(0f, 父宽 - 面板宽));      // 贴左 → 贴右（父宽<面板宽时锁贴左兜底）
            位置.y = Mathf.Clamp(位置.y, Mathf.Min(0f, -(父高 - 面板高)), 0f);   // 贴顶 → 贴底
            面板根.anchoredPosition = 位置;
        }

        public void 关闭()
        {
            if (当前容器 != null) 已打开.Remove(当前容器);
            当前容器 = null;
            if (容器网格 != null) { 容器网格.数据源 = null; 容器网格.所属容器 = null; }
            Destroy(gameObject);
        }

        // 兜底：面板被其他方式销毁时同步清理登记
        private void OnDestroy()
        {
            if (当前容器 != null) 已打开.Remove(当前容器);
        }

        // ===== 面板拖拽移动（整面板可拖，限制在 Canvas 内） =====
        public void OnBeginDrag(PointerEventData 事件)
        {
            Vector2 屏幕;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform.parent, 事件.position, 事件.pressEventCamera, out 屏幕))
                拖拽偏移 = 面板根.anchoredPosition - 屏幕;
        }
        public void OnDrag(PointerEventData 事件)
        {
            Vector2 屏幕;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform.parent, 事件.position, 事件.pressEventCamera, out 屏幕)) return;
            面板根.anchoredPosition = 屏幕 + 拖拽偏移;
            限制在屏幕内();   // 拖拽中同样限制在屏幕内
        }
        public void OnEndDrag(PointerEventData 事件) { }
    }
