using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 家具网格面板：安全屋 房间网格（家具 模式 子类）——继承 网格面板基类，只写 家具 语义 钩子。
// 家具 = 物品堆叠 承载（标识 含 等级后缀"储物箱_2"；数量恒 1；列/行/旋转 = 房间网格位置）。
// 渲染：与 物品 同构——框根 全尺寸 贴格（透明：线 透出，边界线 亮线 可见）+ 内容层 色块（内缩 边距，
//       尺寸 恒 未旋转、旋转 时 转 90°；选中 变 选中色）+ 名称 + 等级角标（框根，不旋转）。
// 交互：右键 家具 菜单（使用/升级/拆除/详情；单击/双击 无 操作）；拖拽中 按 R 旋转（基类 通用，与 物品 一致）。
// 拖拽：只 同 网格 移动/换位（区域互换，与 物品 同款）——空区=移动（含 R 旋转）/ 被占区=整体换位
//       （拖拽旋转 应用 到 源；目标 家具 搬回 只 改 列/行，旋转 永 不 被 改）；取消=回原位。
// 宿主：安全屋面板（数据源 注入 = 安全屋管理器.网格()；双击 详情 转交；编辑/摆放 模式 保护）。
public sealed class 家具网格面板 : 网格面板基类
{
    public 安全屋面板 家具宿主;   // 安全屋面板（双击 详情 转交 / 编辑·摆放 模式 保护）

    // ===== 钩子 override：家具 语义 =====

    protected override 物品框 创建实体框(物品堆叠 堆叠) => 创建家具框(堆叠);
    protected override void 更新实体框(物品框 框, 物品堆叠 堆叠) => 更新家具框(框, 堆叠);

    protected override void 单击实体(物品堆叠 堆叠, int 点击次数)
    {
        // 家具 单击/双击 无 自定义 操作（选中 由 基类 维护 供 右键 菜单 用；操作 全 走 右键 菜单）
    }

    protected override void 右键实体(物品堆叠 堆叠, RectTransform 框)
    {
        if (右键菜单.实例 != null)
        {
            右键菜单.实例.目标面板 = this;
            右键菜单.实例.显示家具(堆叠, 框);   // 家具 菜单（升级/拆除/详情）
        }
    }

    // 完成拖拽：区域互换（与 物品 同款）——空区=移动（含 R 旋转）/ 被占区=整体换位（拖拽旋转 应用到 源；目标 搬回 不改 旋转）；取消=回原位
    protected override bool 完成拖拽(PointerEventData 事件, 物品堆叠 源)
    {
        if (!落点有效) return false;
        return 服务.区域互换(源, 落点列, 落点行, 拖拽旋转);
    }

    protected override bool 允许跨面板() => false;   // 家具：只 同 网格（禁止 拖 出 房间）
    protected override bool 允许开始拖拽(物品堆叠 堆叠)
        => 家具宿主 != null && 家具宿主.编辑模式 && !家具宿主.摆放中;   // 非 编辑 模式：家具 不可 拖拽；摆放 模式 忽略

    protected override void 创建代理内容(Image 图, 物品堆叠 堆叠)
    {
        var 色 = 网格面板配色.家具内容色;
        图.color = new Color(色.r, 色.g, 色.b, 0.85f);
    }

    // 放下 音效：家具 专用（拖拽 移动/换位 成功）
    protected override void 实体放下音效() => 音效管理器.实例?.播放家具放下();

    // 块 网格 线 / 底格：用 基类 容器 版（房间 = 独立 闭合 轮廓 + 缝 空隙——容器 口袋 式 隔离；不 override）

    // ===== 家具 操作（右键菜单 调用） =====

    // 菜单打开：储物箱 → 打开 仓库（持有面板）；工作台/灶台/医疗站 → 打开 制作面板（按 家具类型 注入，经 家具宿主）
    public override void 菜单打开()
    {
        if (选中 == null) return;
        var (定义标识, _) = 家具工具.解码(选中.标识);
        if (定义标识 == "储物箱") { 面板管理器.实例?.显示面板类型<持有面板>(); return; }
        if (定义标识 == "工作台" || 定义标识 == "灶台" || 定义标识 == "医疗站")
        {
            if (家具宿主 != null) 家具宿主.打开制作(定义标识);
            else UnityEngine.Debug.LogWarning("[家具网格面板] 家具宿主 未接线，无法 打开 制作面板。");
        }
    }

    // 菜单查看详情：弹出 信息面板 显示 家具详情（名称/等级/描述/效果/占格/材料——家具数据 驱动）
    public override void 菜单查看详情()
    {
        if (选中 == null) return;
        信息面板.显示家具详情(选中, transform as RectTransform);
    }

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
        服务.网格物品.Remove(拆);
        请求刷新();
        音效管理器.实例?.播放成功();
        ServiceRegistry.Get<EventBus>().发布(new 日志事件(日志类型.反馈, $"拆除了 {家具名称(拆.标识)}。"));
    }

    // ===== 家具框 渲染（与 物品 同构：透明 框根 + 内缩 色块 内容层 + 名称 + 等级角标） =====

    private 物品框 创建家具框(物品堆叠 堆叠)
    {
        if (堆叠 == null) return null;
        var 框 = new 物品框();
        var (宽, 高) = 服务.物品占格(堆叠);
        var 物体 = new GameObject($"家具_{家具名称(堆叠.标识)}", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(物品层, false);
        框.根 = 物体.GetComponent<RectTransform>();
        物体.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // 框根 透明（同 物品 普通：线 透出，边界线 可见）
        var 边框 = 物体.AddComponent<Outline>();
        边框.effectColor = 物品描边色;
        边框.effectDistance = 物品描边距离;
        定位(框.根, 堆叠.列, 堆叠.行, 宽, 高);   // 全尺寸 贴格（同 物品）
        框.高光层 = 创建高光层(物体.transform);
        // 内容层 = 家具 色块（与 物品 内容层 同构：内缩 边距、尺寸 恒 未旋转、旋转 时 转 90°；选中 变 选中色）
        var 未旋转 = 服务.形状解析?.Invoke(堆叠.标识) ?? new 物品形状(1, 1);
        var 内容图 = UI工具.创建图(物体.transform, "内容", null, 选中 == 堆叠 ? 网格面板配色.家具选中色 : 网格面板配色.家具底色, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        UI工具.设锚(内容图.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(未旋转.宽 * 格尺寸 - 网格面板配色.物品边距 * 2f, 未旋转.高 * 格尺寸 - 网格面板配色.物品边距 * 2f));
        内容图.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 堆叠.旋转 ? 90f : 0f);
        框.内容层 = 内容图.rectTransform;
        框.框图 = 内容图;   // 框图 指向 内容层（色块 随 选中 变色；框根 透明 固定）
        var 名称 = UI工具.创建文本(物体.transform, "名称", 家具名称(堆叠.标识), Mathf.Clamp(格尺寸 * 0.22f, 14f, 44f), TextAlignmentOptions.Center);
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
        // 内容层 旋转 同步（同 物品：尺寸 恒 未旋转，只 转 90° 适配 框根 换向）
        if (框.内容层 != null)
        {
            var 旋转 = Quaternion.Euler(0f, 0f, 堆叠.旋转 ? 90f : 0f);
            if (框.内容层.localRotation != 旋转) 框.内容层.localRotation = 旋转;
        }
        // 色块 颜色（选中 变 选中色；框图 已 指向 内容层）
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
