using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 物品网格面板：背包/仓库/容器/穿戴/搜索 的物品网格语义（类名 即 语义；场景/预制体 引用 安全——继承 基类）。
// 继承 网格面板基类（通用骨架：字段/生命周期/渲染框架/拖拽框架/交互框架/钩子）；本类 + 渲染/拖拽/交互 partial 只写 物品 语义。
// 框架：网格面板基类.cs（抽象骨架+钩子）/ 家具网格面板.cs（安全屋 房间网格）。
// 差异 钩子 全部 override（转发 到 partial 的 物品 语义 方法）；基类 骨架 零 物品 逻辑。
public sealed partial class 物品网格面板 : 网格面板基类
{
    [NonSerialized] public 搜索面板 搜索宿主;  // 非空 = 本面板 是 搜索面板 的 网格：双击 容器 物品 → 4 区 原位 替换（箱中箱）

    // ===== 钩子 override（转发 到 partial 的 物品 语义 方法） =====

    protected override 物品框 创建实体框(物品堆叠 堆叠) => 创建物品(堆叠);
    protected override void 更新实体框(物品框 框, 物品堆叠 堆叠) => 更新物品框(框, 堆叠);
    protected override void 单击实体(物品堆叠 堆叠, int 点击次数) => 物品单击(堆叠, 点击次数);
    protected override void 右键实体(物品堆叠 堆叠, RectTransform 框) => 物品菜单右键(堆叠, 框);
    protected override bool 完成拖拽(PointerEventData 事件, 物品堆叠 堆叠) => 物品完成拖拽(事件, 堆叠);
    protected override bool 允许跨面板() => true;   // 物品：跨面板 转移（背包↔容器↔仓库）
    protected override void 创建代理内容(Image 图, 物品堆叠 堆叠) => 物品代理内容(图, 堆叠);
    protected override bool 命中装备槽(PointerEventData 事件) => 装备区.实例 != null && 装备区.实例.命中槽位(事件.position, out _);
    protected override bool 目标允许放入(string 标识) => 物品允许放入(标识);
    protected override bool 目标禁放入(物品堆叠 拖入) => 物品禁放入(拖入);
    public override bool 接收跨面板转移(背包服务 源服务, 物品堆叠 堆叠, PointerEventData 事件, bool 拖拽旋转 = false)
        => 物品接收跨面板转移(源服务, 堆叠, 事件, 拖拽旋转);

    // 右键菜单 公开 操作（物品 语义）
    public override void 菜单使用() => 使用选中();
    public override void 菜单装备() => 装备选中();
    public override void 菜单打开() { if (选中 != null) 打开容器(选中); }
    public override void 菜单丢弃() => 物品丢弃();
    public override void 菜单打开拆分() => 物品打开拆分();
    public override void 菜单拆分(int 数量) => 物品拆分(数量);
    public override void 菜单查看详情() => 物品查看详情();
}
