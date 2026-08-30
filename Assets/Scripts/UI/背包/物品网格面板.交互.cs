using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// 物品网格面板.交互 —— 分部类（物品网格面板）：物品 点击/右键/快捷操作/容器/拆分/嵌套校验。
// 通用 交互 分派（物品被点击/物品右键 → 钩子）在 网格面板基类.cs。
// ============================================================
public sealed partial class 物品网格面板
{
    // 单击/双击 实体（基类 单击实体 钩子 调用）：物品=双击 快捷操作（使用/装备/打开容器）
    private void 物品单击(物品堆叠 堆叠, int 点击次数)
    {
        if (点击次数 >= 2)
        {
            快捷操作(堆叠);   // 双击：操作（使用/装备/打开容器）
            选中 = null;
            请求刷新();
        }
        // 单击：仅记录选中（详情由右键菜单「详情」呼出；不重建网格——否则销毁物品框会破坏双击的 clickCount 累积）
    }

    // 右键 实体（基类 右键实体 钩子 调用）：物品 菜单
    private void 物品菜单右键(物品堆叠 堆叠, RectTransform 物品框)
    {
        if (右键菜单.实例 != null)
        {
            右键菜单.实例.目标面板 = this;   // 操作目标 = 发起右键的面板（主背包/容器面板 各自正确）
            右键菜单.实例.显示(堆叠, 物品框);
        }
    }

    // 右键菜单公开操作（基类 菜单XX 钩子 → 本类；作用于当前 选中）

    private void 物品丢弃()
    {
        if (选中 == null || 选中.列 < 0) return;
        var 丢 = 选中;
        选中 = null;
        服务.背包.Remove(丢);
        请求刷新();
        音效管理器.实例?.播放成功();
        ServiceRegistry.Get<EventBus>().发布(new 背包变化事件(丢.标识, -丢.数量, 变化原因.失去));
    }

    private void 物品打开拆分()
    {
        if (选中 == null) { ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.反馈坏, "[背包] 拆分：未选中物品（右键目标面板未匹配）。")); return; }
        if (选中.数量 <= 1) { ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.反馈坏, $"[背包] 拆分：{选中.标识} 数量 {选中.数量} ≤ 1，不可拆分。")); return; }
        var 面板 = 拆分面板.实例;
        if (面板 == null)
        {
            var 全部 = FindObjectsOfType<拆分面板>(true);
            if (全部.Length > 0) 面板 = 全部[0];
        }
        if (面板 == null)
        {
            ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.反馈坏, "[背包] 场景未搭建 拆分面板（拆分面板 组件 + 面板根）。"));
            return;
        }
        面板.打开(选中, this);
    }

    // 拆分面板确认 → 执行拆分（安全放置：本网格空位优先；容器拆分时容器满 → 兜底统一放入；都满 → 不允许）
    private void 物品拆分(int 数量)
    {
        if (选中 == null || 数量 <= 0 || 数量 >= 选中.数量) return;
        bool 容器视图 = 数据源 != null;
        var 事件 = ServiceRegistry.Get<EventBus>();
        var 新 = 服务.拆分(选中, 数量);
        if (新 != null)
        {
            音效管理器.实例?.播放成功();
            选中 = null;
            请求刷新();
            return;
        }
        if (容器视图)
        {
            var 新堆叠 = new 物品堆叠(选中.标识, 数量) { 旋转 = 选中.旋转, 当前耐久 = 选中.当前耐久, 品质 = 选中.品质, 词缀 = 选中.词缀 != null ? new List<词缀条>(选中.词缀) : null };
            选中.数量 -= 数量;
            int 实放 = 档案.放入堆叠(新堆叠);
            if (实放 > 0)
            {
                音效管理器.实例?.播放成功();
                选中 = null;
                请求刷新();
                事件?.发布(new 日志事件(日志类型.反馈, $"[背包] 容器已满，拆分出的 {新堆叠.标识}×{数量} 放入穿戴容器/仓库。"));
                事件?.发布(new 背包变化事件(新堆叠.标识, 实放, 变化原因.获得));
                return;
            }
            选中.数量 += 数量;
        }
        音效管理器.实例?.播放失败();
        事件?.发布(new 日志事件(日志类型.反馈坏, "[背包] 拆分失败：容器与 穿戴容器/仓库 均已满，无空位放置拆出物品。"));
    }

    private void 物品查看详情()
    {
        if (选中 == null) return;
        音效管理器.实例?.播放成功();
        信息面板.显示详情(选中, 服务, (RectTransform)transform);
    }

    // 双击快捷操作：恢复品=使用；装备类=换装；容器=打开；技能书=学习
    private void 快捷操作(物品堆叠 堆叠)
    {
        if (!数据.物品.TryGetValue(堆叠.标识, out var 物品)) return;
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        if (容器服务.是容器(堆叠)) { 打开容器(堆叠); return; }
        if (物品.恢复量 > 0) { 使用选中(); return; }
        if (!string.IsNullOrEmpty(物品.槽位)) { 装备选中(); return; }
        if (物品.类型 == "技能书") { 面板操作.学习技能书(档案, 物品); return; }
        音效管理器.实例?.播放失败();
    }

    // 双击容器物品：搜索面板 内 → 4 区 原位替换（箱中箱）；否则 动态搭建浮动容器面板
    private void 打开容器(物品堆叠 堆叠)
    {
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        if (!容器服务.是容器(堆叠)) { 音效管理器.实例?.播放失败(); return; }
        容器服务.初始化容器(堆叠);
        音效管理器.实例?.播放成功();
        if (搜索宿主 != null) { 搜索宿主.打开嵌套(堆叠); return; }
        var 滚动 = 网格容器 != null ? 网格容器.parent?.parent : null;
        var 挂载父 = 滚动 != null ? (RectTransform)滚动 : 网格容器;
        容器面板.创建(挂载父, 堆叠);
    }

    // ===== 操作按钮 =====

    private void 使用选中()
    {
        if (选中 == null) return;
        if (!数据.物品.TryGetValue(选中.标识, out var 物品) || 物品.恢复量 <= 0) { 音效管理器.实例?.播放失败(); return; }
        面板操作.使用恢复(档案, 数据, 选中.标识);
        选中 = null;
        请求刷新();
    }

    private void 装备选中()
    {
        if (选中 == null) return;
        if (!数据.物品.TryGetValue(选中.标识, out var 物品) || string.IsNullOrEmpty(物品.槽位)) { 音效管理器.实例?.播放失败(); return; }
        if (面板操作.换装堆叠(档案, 选中, null, 服务)) 选中 = null;
        请求刷新();
    }

    // ===== 目标容器校验（跨面板拖拽）：所属容器（容器面板）或 所属槽位（装具块） =====

    private bool 物品允许放入(string 标识)
    {
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        if (所属容器 != null) return 容器服务.允许放入(所属容器, 标识);
        if (!string.IsNullOrEmpty(所属槽位))
        {
            var 记录 = 档案?.装备.Find(e => e.槽位 == 所属槽位);
            if (记录 != null && !string.IsNullOrEmpty(记录.标识)) return 容器服务.允许放入(记录.标识, 标识);
        }
        return true;
    }

    // 套娃上限：容器 最多 嵌套 3 层
    private const int 嵌套上限 = 3;

    // 嵌套防护：禁止 ① 自己套自己 ② 循环引用 ③ 超过套娃上限
    private bool 物品禁放入(物品堆叠 拖入)
    {
        if (拖入 == null) return false;
        if (拖入 == 所属容器) return true;
        if (所属容器 != null && 嵌套包含(拖入, 所属容器, 0)) return true;
        if (所属容器 != null || !string.IsNullOrEmpty(所属槽位))
        {
            if (目标容器深度() + 嵌套深度(拖入) > 嵌套上限) return true;
        }
        return false;
    }

    private int 目标容器深度()
    {
        if (所属容器 == null) return 1;
        int d = 查找深度(所属容器);
        return d > 0 ? d : 1;
    }

    private int 查找深度(物品堆叠 目标)
    {
        foreach (var 槽 in new[] { "弹挂", "腰封", "背包" })
        {
            var 记录 = 档案?.装备.Find(e => e.槽位 == 槽);
            if (记录?.容器物品 == null) continue;
            int d = 找于列表(记录.容器物品, 目标, 1);
            if (d > 0) return d;
        }
        return 找于列表(档案?.仓库物品, 目标, 1);
    }

    private int 找于列表(List<物品堆叠> 列表, 物品堆叠 目标, int 深度)
    {
        if (列表 == null) return 0;
        foreach (var 堆 in 列表)
        {
            if (堆 == 目标) return 深度;
            if (堆.容器物品 != null)
            {
                int d = 找于列表(堆.容器物品, 目标, 深度 + 1);
                if (d > 0) return d;
            }
        }
        return 0;
    }

    private static bool 嵌套包含(物品堆叠 容器, 物品堆叠 目标, int 深度)
    {
        if (容器.容器物品 == null || 深度 >= 3) return false;
        foreach (var 内 in 容器.容器物品)
        {
            if (内 == 目标) return true;
            if (嵌套包含(内, 目标, 深度 + 1)) return true;
        }
        return false;
    }

    private static int 嵌套深度(物品堆叠 堆叠)
    {
        if (堆叠?.容器物品 == null) return 0;
        int 深 = 1;
        foreach (var 内 in 堆叠.容器物品)
            if (内 != null) 深 = Math.Max(深, 嵌套深度(内) + 1);
        return 深;
    }
}
