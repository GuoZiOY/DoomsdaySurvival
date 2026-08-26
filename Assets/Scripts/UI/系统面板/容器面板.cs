using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

    // 容器面板：完全代码动态搭建的浮动容器面板（塔科夫式）。
    // 双击容器物品 → 在主背包面板的 ScrollRect 下（Viewport 同级）动态生成：
    //   面板根（Image 半透明底，可整面板拖拽移动）→ 标题 + 关闭按钮 + 网格容器（挂 网格背包面板 组件显示容器内容）。
    // 关闭时销毁；与主背包跨网格拖拽转移（矩形判断，见 网格背包面板.事件下方面板）。
    public sealed class 容器面板 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private 网格背包面板 容器网格;   // 动态创建（显示容器内部）
        private RectTransform 面板根;     // 动态创建（含 Image，可拖拽）
        private RectTransform 网格容器;   // 动态创建（容器内部网格的 Content）
        private 物品堆叠 当前容器;
        private Vector2 拖拽偏移;

        // 供 网格背包面板 双击时调用：动态搭建容器面板并显示
        public static 容器面板 创建(RectTransform 挂载父, 物品堆叠 容器)
        {
            if (容器 == null) return null;
            var 服务 = ServiceRegistry.Get<容器服务>();
            var 物体 = new GameObject("容器面板", typeof(RectTransform), typeof(Image));
            var 根 = 物体.GetComponent<RectTransform>();
            根.SetParent(挂载父, false);
            根.SetAsLastSibling();   // 容器面板在最上层（覆盖主背包网格，代理在 Canvas 顶层仍在其上）
            根.anchorMin = new Vector2(0, 1);   // 挂载父左上（主背包 ScrollRect 下，Viewport 同级）
            根.anchorMax = new Vector2(0, 1);
            根.pivot = new Vector2(0, 1);
            根.anchoredPosition = new Vector2(挂载父.rect.width * 0.6f, -20f);   // 默认偏右下方
            根.sizeDelta = new Vector2(480f, 420f);   // 初始较大（实际按容器网格尺寸在 显示容器 里覆盖）
            var 图 = 物体.GetComponent<Image>();
            图.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);   // 半透明底（遮住下层）

            var 面板 = 物体.AddComponent<容器面板>();
            面板.面板根 = 根;
            面板.构建子结构(服务);
            面板.显示容器(容器);
            return 面板;
        }

        // 构建标题/关闭按钮/网格容器（全部代码生成）
        private void 构建子结构(容器服务 服务)
        {
            // 标题
            var 标题物体 = new GameObject("标题", typeof(RectTransform), typeof(TextMeshProUGUI));
            标题物体.transform.SetParent(面板根, false);
            var 标题矩形 = 标题物体.GetComponent<RectTransform>();
            标题矩形.anchorMin = new Vector2(0, 1);
            标题矩形.anchorMax = new Vector2(1, 1);
            标题矩形.pivot = new Vector2(0.5f, 1);
            标题矩形.anchoredPosition = new Vector2(0, -6f);
            标题矩形.sizeDelta = new Vector2(-60f, 28f);
            var 标题文本 = 标题物体.GetComponent<TextMeshProUGUI>();
            标题文本.fontSize = 20f;
            标题文本.alignment = TextAlignmentOptions.Left;
            标题文本.color = Color.white;
            标题文本.raycastTarget = false;
            // 关闭按钮（右上角 "×"）
            var 按钮物体 = new GameObject("关闭", typeof(RectTransform), typeof(Image), typeof(Button));
            按钮物体.transform.SetParent(面板根, false);
            var 按钮矩形 = 按钮物体.GetComponent<RectTransform>();
            按钮矩形.anchorMin = new Vector2(1, 1);
            按钮矩形.anchorMax = new Vector2(1, 1);
            按钮矩形.pivot = new Vector2(1, 1);
            按钮矩形.anchoredPosition = new Vector2(-6f, -6f);
            按钮矩形.sizeDelta = new Vector2(28f, 28f);
            按钮物体.GetComponent<Image>().color = new Color(1f, 0.3f, 0.3f, 0.8f);
            按钮物体.GetComponent<Button>().onClick.AddListener(关闭);
            // 网格容器（容器内部网格的 Content；挂 网格背包面板 组件渲染）
            // 用左上锚定 + 固定尺寸（不撑满面板根）：网格尺寸 = 列×格尺寸，面板根按它适配
            var 网格物体 = new GameObject("容器网格", typeof(RectTransform));
            网格物体.transform.SetParent(面板根, false);
            var 网格矩形 = 网格物体.GetComponent<RectTransform>();
            网格矩形.anchorMin = new Vector2(0, 1);
            网格矩形.anchorMax = new Vector2(0, 1);
            网格矩形.pivot = new Vector2(0, 1);
            网格矩形.anchoredPosition = new Vector2(12f, -34f);   // 标题下方（标题高约 28 + 边距 6）
            网格矩形.sizeDelta = new Vector2(100f, 100f);          // 占位，显示容器时按实际网格尺寸覆盖
            网格容器 = 网格矩形;
            // 动态挂 网格背包面板 组件（复用全部网格渲染/拖拽/转移逻辑）
            容器网格 = 网格物体.AddComponent<网格背包面板>();
            容器网格.绑定网格容器(网格矩形);
            容器网格.配置容器显示(100f);   // 容器内格子（与主背包接近，可操作性好）
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
            容器网格.重载网格();
            // 面板根大小 = 网格尺寸 + 边距（网格左上在 12,-34；左右各 12，下方留 12）；保证最小可操作尺寸
            float 网格宽 = 容器网格.渲染列 * 容器网格.格子尺寸;
            float 网格高 = 容器网格.渲染行 * 容器网格.格子尺寸;
            面板根.sizeDelta = new Vector2(Mathf.Max(400f, 网格宽 + 24f), Mathf.Max(320f, 34f + 网格高 + 12f));
            网格容器.sizeDelta = new Vector2(网格宽, 网格高);
        }

        public void 关闭()
        {
            当前容器 = null;
            if (容器网格 != null) { 容器网格.数据源 = null; 容器网格.所属容器 = null; }
            Destroy(gameObject);
        }

        // ===== 面板拖拽移动（整面板可拖） =====
        public void OnBeginDrag(PointerEventData 事件)
        {
            Vector2 屏幕;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform.parent, 事件.position, 事件.pressEventCamera, out 屏幕))
                拖拽偏移 = (Vector2)transform.localPosition - 屏幕;
        }
        public void OnDrag(PointerEventData 事件)
        {
            Vector2 屏幕;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform.parent, 事件.position, 事件.pressEventCamera, out 屏幕))
                transform.localPosition = 屏幕 + 拖拽偏移;
        }
        public void OnEndDrag(PointerEventData 事件) { }
    }
