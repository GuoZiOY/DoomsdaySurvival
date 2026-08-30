using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 家具网格面板：安全屋 房间网格（家具 模式 子类）——继承 网格面板基类，只写 家具 语义 钩子。
// 家具 = 物品堆叠 承载（标识 含 等级后缀"储物箱_2"；数量恒 1；列/行/旋转 = 房间网格位置）。
// 渲染：色块（统一底色/选中亮）+ 名称 + 等级角标；交互：单击选中/R旋转/双击详情/右键 家具 菜单；
// 拖拽：只 同 网格 移动/换位（禁止 装备槽/跨面板/容器——房间 固定 物）。
// 宿主：安全屋面板（数据源 注入 = 安全屋管理器.网格()；双击 详情 转交；摆放 模式 保护）。
public sealed class 家具网格面板 : 网格面板基类
{
    public 安全屋面板 家具宿主;   // 安全屋面板（双击 详情 转交 / 摆放 模式 保护）

    // ===== 钩子 override：家具 语义 =====

    protected override 物品框 创建实体框(物品堆叠 堆叠) => 创建家具(堆叠);
    protected override void 更新实体框(物品框 框, 物品堆叠 堆叠) => 更新家具框(框, 堆叠);

    protected override void 单击实体(物品堆叠 堆叠, int 点击次数)
    {
        请求刷新();   // 选中 高亮（家具 选中色）
        if (点击次数 >= 2 && 家具宿主 != null) 家具宿主.家具被点击(堆叠);   // 双击：详情/提示
    }

    protected override void 右键实体(物品堆叠 堆叠, RectTransform 框)
    {
        if (右键菜单.实例 != null)
        {
            右键菜单.实例.目标面板 = this;
            右键菜单.实例.显示家具(堆叠, 框);   // 家具 菜单（升级/拆除/详情）
        }
    }

    protected override bool 完成拖拽(PointerEventData 事件, 物品堆叠 源)
    {
        if (落点有效)
        {
            int 落列 = 落点列, 落行 = 落点行;
            var 目标 = 该格物品(落列, 落行);
            if (目标 != null && 目标 != 源) return 服务.换位(源, 目标);   // 家具 换位
            if (目标 == null) return 服务.移动堆叠(源, 落列, 落行, 拖拽旋转);   // 移动（含 R 旋转）
        }
        return false;
    }

    protected override bool 允许跨面板() => false;   // 家具：只 同 网格（禁止 拖 出 房间）
    protected override bool 允许开始拖拽(物品堆叠 堆叠) => !(家具宿主 != null && 家具宿主.摆放中);   // 摆放 模式 忽略
    protected override void 创建代理内容(Image 图, 物品堆叠 堆叠)
    {
        var 色 = 网格面板配色.家具内容色;
        图.color = new Color(色.r, 色.g, 色.b, 0.85f);
    }

    protected override void 更新选中旋转()
    {
        if (选中 != null && 拖拽中堆叠 == null && 检测按R())
        {
            if (服务.移动堆叠(选中, 选中.列, 选中.行, !选中.旋转)) { 请求刷新(); }
            else ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.反馈坏, "旋转后放不下。"));
        }
    }

    // ===== 家具 操作（右键菜单 调用） =====

    public override void 家具升级()
    {
        if (选中 == null) return;
        if (ServiceRegistry.Get<安全屋管理器>().升级(选中)) 强制重建();   // 升级 占格/分隔线 变化 → 全量重建
    }

    public override void 家具拆除()
    {
        if (选中 == null || 选中.列 < 0) return;
        var 拆 = 选中;
        选中 = null;
        服务.背包.Remove(拆);
        请求刷新();
        音效管理器.实例?.播放成功();
        ServiceRegistry.Get<EventBus>().发布(new 日志事件(日志类型.反馈, $"拆除了 {家具名称(拆.标识)}。"));
    }

    // ===== 家具框 渲染（色块 + 名称 + 等级角标） =====

    private 物品框 创建家具(物品堆叠 堆叠)
    {
        if (堆叠 == null) return null;
        var 框 = new 物品框();
        var (宽, 高) = 服务.物品占格(堆叠);
        var 物体 = new GameObject($"家具_{家具名称(堆叠.标识)}", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(物品层, false);
        框.根 = 物体.GetComponent<RectTransform>();
        框.框图 = 物体.GetComponent<Image>();
        框.框图.color = 选中 == 堆叠 ? 网格面板配色.家具选中色 : 网格面板配色.家具底色;
        var 边框 = 物体.AddComponent<Outline>();
        边框.effectColor = 物品描边色;
        边框.effectDistance = 物品描边距离;
        定位(框.根, 堆叠.列, 堆叠.行, 宽, 高);
        框.高光层 = 创建高光层(物体.transform);
        var 内容图 = UI工具.创建图(物体.transform, "内容", null, 网格面板配色.家具内容色, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        UI工具.设锚(内容图.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(宽 * 格尺寸 - 网格面板配色.物品边距 * 2f, 高 * 格尺寸 - 网格面板配色.物品边距 * 2f));
        框.内容层 = 内容图.rectTransform;
        var 名称 = UI工具.创建文本(内容图.transform, "名称", 家具名称(堆叠.标识), Mathf.Clamp(格尺寸 * 0.22f, 14f, 44f), TextAlignmentOptions.Center);
        UI工具.铺满(名称.rectTransform);
        名称.enableWordWrapping = true;
        框.名称 = 名称;
        int 等级 = 家具工具.解码(堆叠.标识).等级;
        var 数 = UI工具.创建文本(物体.transform, "数量", $"{等级}级", 40f, TextAlignmentOptions.TopRight);
        UI工具.设锚(数.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-5f, -2f), new Vector2(70f, 34f));
        框.数量 = 数;
        框.上次数量 = 等级;
        挂接交互(框, 堆叠, 内容图.rectTransform);
        return 框;
    }

    private void 更新家具框(物品框 框, 物品堆叠 堆叠)
    {
        var (宽, 高) = 服务.物品占格(堆叠);
        框.根.anchoredPosition = new Vector2(格x(堆叠.列, 堆叠.行), -堆叠.行 * 格尺寸);
        float 新宽 = 宽 * 格尺寸, 新高 = 高 * 格尺寸;
        if (Mathf.Abs(框.根.sizeDelta.x - 新宽) > 0.01f || Mathf.Abs(框.根.sizeDelta.y - 新高) > 0.01f)
            框.根.sizeDelta = new Vector2(新宽, 新高);
        if (框.内容层 != null)
        {
            var 旋转 = Quaternion.Euler(0f, 0f, 堆叠.旋转 ? 90f : 0f);
            if (框.内容层.localRotation != 旋转) 框.内容层.localRotation = 旋转;
        }
        var 色 = 选中 == 堆叠 ? 网格面板配色.家具选中色 : 网格面板配色.家具底色;
        if (框.框图 != null && 框.框图.color != 色)
            框.框图.color = 色;
        if (框.数量 != null)
        {
            int 等级 = 家具工具.解码(堆叠.标识).等级;
            if (框.上次数量 != 等级)
            {
                框.数量.text = $"{等级}级";
                框.上次数量 = 等级;
            }
        }
    }

    private string 家具名称(string 实例标识)
    {
        var (定义标识, _) = 家具工具.解码(实例标识);
        return 数据.家具.TryGetValue(定义标识, out var 定义) ? 定义.名称 : 实例标识;
    }
}
