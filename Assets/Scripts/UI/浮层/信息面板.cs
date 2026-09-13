using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 信息展示面板（预制体驱动 + 多实例）：右键菜单「详情」呼出，每次 显示 都 实例化 一个面板（可 同时 开 多个）。
//   预制体（Assets/Resources/Prefab/信息面板.prefab）搭好 UI（面板根 Image / 物品图片 Image / 详情 TMP / 关闭按钮），
//   本组件 暴露 引用（面板根/物品图片/详情文本/关闭按钮），动态 注入：物品 图标 + 完整 详情 文本；居中 显示。
//   整面板 可 拖拽 移动（世界坐标，锚点/pivot 无关），限制 在 画布 内；关闭 = 销毁 本实例。
public sealed class 信息面板 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    private const string 预制体路径 = "Prefab/信息面板";   // Resources 路径（预制体 静态 搭建）

    [SerializeField] private RectTransform 面板根;   // 面板根（预制体 已 挂 本组件；显示时 置顶 + 居中）
    [SerializeField] private Image 物品图片;          // 物品图标（可选：放 详情文本 上方；无图 = 透明占位）
    [SerializeField] private TMP_Text 详情文本;      // 物品完整详情（物品工具.构建详情 输出）
    [SerializeField] private Button 关闭按钮;        // 关闭按钮（Awake 自动 挂 关闭() + 注册 成功音效）

    private Vector3 拖拽偏移;   // 拖拽：面板 position 与 鼠标 世界点 的偏移

    // ===== 静态 入口（任意处 可调；每次 显示 新建 一个 面板，支持 同时 多开） =====
    public static void 显示详情(物品堆叠 堆叠, 网格服务 服务 = null, RectTransform 挂载父 = null)
    {
        if (堆叠 == null) return;
        var 面板 = 创建(挂载父);
        if (面板 != null) 面板.显示(堆叠, 服务);
    }

    // 显示 已装备槽位 的详情（构造临时堆叠：标识/耐久/配件）
    public static void 显示槽位详情(string 槽位, RectTransform 挂载父 = null)
    {
        var 档案 = ServiceRegistry.Get<PlayerService>().档案;
        if (档案 == null) return;
        var 记录 = 档案.装备.Find(e => e.槽位 == 槽位);
        if (记录 == null || string.IsNullOrEmpty(记录.标识)) return;
        显示详情(new 物品堆叠(记录.标识, 1) { 当前耐久 = 记录.当前耐久, 配件 = 记录.配件 }, 档案.网格服务, 挂载父);
    }

    // 实例化 预制体 + 挂 Canvas 顶层（不受 裁剪 / 不被 覆盖）+ 置顶
    private static 信息面板 创建(RectTransform 挂载父)
    {
        var 预制 = Resources.Load<信息面板>(预制体路径);
        if (预制 == null)
        {
            Debug.LogError($"[信息面板] 找不到预制体 Resources/{预制体路径}（请用 预制体 搭建信息面板 UI，并 拖好 引用）。");
            return null;
        }
        var 面板 = Instantiate(预制);
        var 画布 = 挂载父 != null ? 挂载父.GetComponentInParent<Canvas>() : Object.FindObjectOfType<Canvas>();
        if (画布 == null)
        {
            Destroy(面板.gameObject);
            Debug.LogError("[信息面板] 找不到 Canvas（无法挂载 顶层）。");
            return null;
        }
        面板.transform.SetParent(画布.transform, false);
        面板.transform.SetAsLastSibling();   // 置顶（拖拽代理创建时再顶到其上）
        return 面板;
    }

    void Awake()
    {
        if (关闭按钮 != null)
        {
            关闭按钮.onClick.AddListener(关闭);
            音效管理器.实例?.注册按钮(关闭按钮);   // 动态实例化，音效管理器 场景扫描 覆盖不到
        }
        if (面板根 != null) 面板根.gameObject.SetActive(false);   // 初始隐藏（显示 时 激活）
    }

    // 显示 背包/容器内 物品堆叠 的详情
    public void 显示(物品堆叠 堆叠, 网格服务 服务 = null)
    {
        if (面板根 == null || 堆叠 == null) return;
        var 档案 = ServiceRegistry.Get<PlayerService>().档案;
        var 数据 = ServiceRegistry.Get<DataService>();
        // 物品图标：按 物品标识 加载（无图/未配置 = 透明占位，不破坏布局）
        if (物品图片 != null && 数据.物品.TryGetValue(堆叠.标识, out var 物品))
        {
            var 图标 = 物品图标服务.获取(物品.标识);
            物品图片.sprite = 图标;
            物品图片.color = 图标 != null ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0f);
        }
        if (详情文本 != null) 详情文本.text = 物品工具.构建详情(档案, 数据, 堆叠, 服务 ?? 档案.网格服务);
        面板根.gameObject.SetActive(true);
        面板根.SetAsLastSibling();   // 置顶
        居中定位();
    }

    // 家具 详情 静态入口（安全屋 房间网格 右键「详情」）：每次 显示 新建 一个 面板（与 物品 详情 一致）
    public static void 显示家具详情(物品堆叠 堆叠, RectTransform 挂载父 = null)
    {
        if (堆叠 == null) return;
        var 面板 = 创建(挂载父);
        if (面板 != null) 面板.显示家具(堆叠);
    }

    // 显示 家具 详情（家具数据 驱动：名称/等级/描述/效果/占格/材料——非 物品数据）
    public void 显示家具(物品堆叠 堆叠)
    {
        if (面板根 == null || 堆叠 == null) return;
        var 数据 = ServiceRegistry.Get<DataService>();
        if (物品图片 != null) 物品图片.color = new Color(1f, 1f, 1f, 0f);   // 家具 无 图标：透明占位
        if (详情文本 != null)
        {
            var (定义标识, 等级) = 家具工具.解码(堆叠.标识);
            详情文本.text = 数据.家具.TryGetValue(定义标识, out var 定义) ? 家具详情文本(定义, 等级) : 堆叠.标识;
        }
        面板根.gameObject.SetActive(true);
        面板根.SetAsLastSibling();
        居中定位();
    }

    // 家具 详情 文本：名称 N级：描述 + 效果 + 占格 + 建造/升级 材料
    private string 家具详情文本(家具数据 定义, int 等级)
    {
        var 数据 = ServiceRegistry.Get<DataService>();
        var 段 = new List<string> { $"{定义.名称} {等级}级：{定义.描述}" };
        if (定义.效果 != null && 等级 - 1 < 定义.效果.Length)
            段.Add($"效果：{定义.效果[等级 - 1]}");
        段.Add($"占格：{定义.形状宽}×{定义.形状高}（旋转 宽高 互换）");
        if (定义.材料 != null && 定义.材料.Length > 0)
            段.Add("建造：" + 家具材料文本(数据, 定义.材料));
        if (定义.升级 != null && 等级 - 1 < 定义.升级.Length && 定义.升级[等级 - 1] != null)
            段.Add("升级：" + 家具材料文本(数据, 定义.升级[等级 - 1].材料));
        return string.Join("\n", 段);
    }

    private string 家具材料文本(DataService 数据, 配方材料[] 材料)
    {
        if (材料 == null || 材料.Length == 0) return "无";
        var 段 = new List<string>();
        foreach (var 材 in 材料)
            if (材 != null) 段.Add($"{物品名(数据, 材.物品)}×{材.数量}");
        return string.Join(" ", 段);
    }

    private string 物品名(DataService 数据, string 标识) => 数据.物品.TryGetValue(标识, out var 物) ? 物.标识 : 标识;

    public void 关闭()
    {
        Destroy(gameObject);   // 多实例：关闭 即 销毁（不再 隐藏 复用）
    }

    // 定位：屏幕中央（pivot 任意都居中）
    private void 居中定位()
    {
        var 画布 = 面板根.GetComponentInParent<Canvas>();
        var 画布根 = 画布 != null ? (RectTransform)画布.transform : null;
        if (画布根 == null) return;
        var 中心 = 画布根.TransformPoint(new Vector3(画布根.rect.xMin + 画布根.rect.width * 0.5f, 画布根.rect.yMin + 画布根.rect.height * 0.5f, 0f));
        float 宽 = 面板根.rect.width * 面板根.lossyScale.x;
        float 高 = 面板根.rect.height * 面板根.lossyScale.y;
        面板根.position = 中心 + 面板根.right * (宽 * (面板根.pivot.x - 0.5f)) - 面板根.up * (高 * (面板根.pivot.y - 0.5f));
    }

    // 点击 置顶：按下 本面板 任意 区域 → SetAsLastSibling（多开 叠放 时 点哪个 面板 浮到 最前，与 容器面板 一致）。
    // 事件 从 子元素 冒泡 到 面板根，拖拽 按下 也 先 置顶。
    public void OnPointerDown(PointerEventData 事件)
    {
        if (面板根 != null) 面板根.SetAsLastSibling();
    }

    // ===== 面板拖拽移动（世界坐标，锚点/pivot 无关；限制在画布内） =====
    public void OnBeginDrag(PointerEventData 事件)
    {
        var 画布 = 面板根.GetComponentInParent<Canvas>();
        var 相机 = 画布 != null && 画布.renderMode != RenderMode.ScreenSpaceOverlay ? 画布.worldCamera : null;
        Vector3 世界;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(面板根, 事件.position, 相机, out 世界))
            拖拽偏移 = 面板根.position - 世界;
    }
    public void OnDrag(PointerEventData 事件)
    {
        var 画布 = 面板根.GetComponentInParent<Canvas>();
        var 相机 = 画布 != null && 画布.renderMode != RenderMode.ScreenSpaceOverlay ? 画布.worldCamera : null;
        Vector3 世界;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(面板根, 事件.position, 相机, out 世界)) return;
        面板根.position = 世界 + 拖拽偏移;
        限制在画布内();   // 拖拽中同样限制
    }
    public void OnEndDrag(PointerEventData 事件) { }

    // 把面板限制在画布范围内（世界坐标矩形；防出屏——出屏既看不见也点不到）
    private void 限制在画布内()
    {
        var 画布 = 面板根.GetComponentInParent<Canvas>();
        var 画布根 = 画布 != null ? (RectTransform)画布.transform : null;
        if (画布根 == null) return;
        var 位置 = 面板根.position;
        float 宽 = 面板根.rect.width * 面板根.lossyScale.x;
        float 高 = 面板根.rect.height * 面板根.lossyScale.y;
        var 屏左下 = 画布根.TransformPoint(new Vector3(画布根.rect.xMin, 画布根.rect.yMin, 0f));
        var 屏右上 = 画布根.TransformPoint(new Vector3(画布根.rect.xMax, 画布根.rect.yMax, 0f));
        float 屏左 = 屏左下.x, 屏右 = 屏右上.x, 屏底 = 屏左下.y, 屏顶 = 屏右上.y;
        // 面板 左/上 边（按 pivot 归一化换算），clamp 后 反算 回 pivot 点
        float 面板左 = 位置.x - 面板根.right.x * (宽 * 面板根.pivot.x);
        float 面板顶 = 位置.y + 面板根.up.y * (高 * (1f - 面板根.pivot.y));
        面板左 = Mathf.Clamp(面板左, 屏左, Mathf.Max(屏左, 屏右 - 宽));
        面板顶 = Mathf.Clamp(面板顶, Mathf.Min(屏顶, 屏底 + 高), 屏顶);
        位置.x = 面板左 + 面板根.right.x * (宽 * 面板根.pivot.x);
        位置.y = 面板顶 - 面板根.up.y * (高 * (1f - 面板根.pivot.y));
        面板根.position = 位置;
    }
}
