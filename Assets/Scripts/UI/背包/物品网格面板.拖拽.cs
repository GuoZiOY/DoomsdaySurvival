using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// ============================================================
// 物品网格面板.拖拽 —— 分部类（物品网格面板）：物品 拖拽 放置 语义（跨面板/装备槽/合并/存入容器/区域互换）。
// 通用 拖拽 框架（代理/投影/移动/R旋转/落格/事件下方面板）在 网格面板基类.cs。
// ============================================================
public partial class 物品网格面板
{
    // 拖拽 放置 语义（基类 结束拖拽 钩子 完成拖拽 调用）：物品=跨面板/装备槽/合并/存入/区域互换
    private bool 物品完成拖拽(PointerEventData 事件, 物品堆叠 源)
    {
        // 跨面板：先找鼠标下方是否有别的 网格面板（基类子类：容器面板/装具块/仓库面板）
        var 目标面板 = 事件下方面板(事件);
        if (目标面板 != null && 目标面板 != this)
            return 目标面板.接收跨面板转移(服务, 源, 事件, 拖拽旋转);   // 传拖拽旋转：跨面板 R 旋转 生效
        // 容器面板 底座拦截：鼠标在 任一 容器面板 底座 上 → 落点归面板；叠放取最上层；
        // 自己面板：网格内 松手 = 同面板 移动/换位（跳过 装备槽 分支——穿透 修复）
        var 自己面板 = GetComponentInParent<容器面板>();
        var 最上层容器 = 最上层容器面板(事件);
        bool 在自己面板网格内 = false;
        if (最上层容器 != null)
        {
            if (最上层容器 != 自己面板) return false;   // 其他 容器面板 上 → 拦截
            if (!最上层容器.命中网格(事件.position)) return false;   // 底座空白区 → 拦截
            在自己面板网格内 = true;
        }
        // 拖到装备槽位（装备区）→ 穿戴：槽位兼容才可穿（实例级换装到"命中槽位"，旧件回背包/回滚已处理）
        if (!在自己面板网格内 && 装备区.实例 != null && 装备区.实例.命中槽位(事件.position, out var 槽位名) && 源 != null)
        {
            bool 成功 = 数据.物品.TryGetValue(源.标识, out var 装备) && 面板操作.槽位匹配(装备.槽位, 槽位名)
                && 面板操作.换装堆叠(档案, 源, 槽位名, 服务);   // 指定目标槽 + 源服务；内部已播 装备音效
            if (成功) { 请求刷新(); return true; }
            return false;
        }
        if (!落点有效)
            return false;   // 拖出网格外（无有效投影）= 取消
        // 与拖拽中一致：直接用最后投影的落格（投影在哪就放在哪，不随松手鼠标重算）
        int 列 = 落点列, 行 = 落点行;
        var 目标物品 = 该格物品(列, 行);
        // 同标识可堆叠 → 合并；目标格是容器物品且允许+有空位 → 存入容器（而不是换位）；
        // 目标格是容器物品但不可存入 → 严格失败（容器不做换位目标）；否则 区域交换（空区=移动 / 被占区=整体换位）
        if (服务.合并堆叠(目标物品, 源) > 0) return true;
        if (存入容器(目标物品, 源)) return true;   // 内部已处理 刷新/事件
        if (目标物品 != null && 目标物品 != 源 && ServiceRegistry.Get<容器服务>().是容器(目标物品)) return false;
        return 服务.区域互换(源, 列, 行, 拖拽旋转);
    }

    // 拖拽 代理 内容（基类 创建代理内容 钩子）：物品 图标/品质底
    private void 物品代理内容(Image 图, 物品堆叠 堆叠)
    {
        var 代理物品 = 数据.物品.TryGetValue(堆叠.标识, out var 代理数据) ? 代理数据 : null;
        var 代理图标 = 代理物品 != null ? 物品图标服务.获取(代理物品.标识) : null;
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
    }

    // 拖拽源 放到 目标容器物品 上：容器允许该类型 且 内部有空位（含 旋转 90°）→ 存入容器（而不是换位/失败）。
    // 智能旋转：当前旋转 放不下 → 自动 旋转 90° 存入（写回 堆叠.旋转；塔科夫式 快捷收入）。
    private bool 存入容器(物品堆叠 目标容器, 物品堆叠 堆叠)
    {
        if (!可存入容器(目标容器, 堆叠)) return false;
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        var 视图 = 容器服务.打开(目标容器);
        var 空位 = 视图.寻找可放置格智能旋转(堆叠, out bool 需旋转);
        if (空位 == null) return false;
        bool 原旋转 = 堆叠.旋转;
        if (需旋转) 堆叠.旋转 = !原旋转;
        int 转移 = 容器服务.跨网格转移(服务, 堆叠, 视图, 空位.Value.列, 空位.Value.行);
        if (转移 <= 0) { 堆叠.旋转 = 原旋转; return false; }
        ServiceRegistry.Get<EventBus>().发布(new 背包变化事件(堆叠.标识, 转移, 变化原因.获得));
        return true;
    }

    // 接收跨面板转移（物品 语义）：把 (源服务) 里的 堆叠 放到本面板 (列,行)。返回 是否成功（音效由调用方播）
    private bool 物品接收跨面板转移(网格服务 源服务, 物品堆叠 堆叠, PointerEventData 事件, bool 拖拽旋转 = false)
    {
        if (源服务 == null || 堆叠 == null) return false;
        if (!屏幕到容器相对(事件, out var 相对, out var 尺寸)) return false;
        var 形状 = 服务.形状解析?.Invoke(堆叠.标识) ?? new 物品形状(1, 1);
        int 物宽 = 拖拽旋转 ? 形状.高 : 形状.宽;
        int 物高 = 拖拽旋转 ? 形状.宽 : 形状.高;
        float 相对顶 = 尺寸.y - 相对.y;
        if (!落格(相对.x, 相对顶, 物宽, 物高, out int 列, out int 行)) return false;
        if (!目标允许放入(堆叠.标识)) return false;
        if (目标禁放入(堆叠)) return false;
        // 快捷收入：落点格 是 容器物品（如 背包里 的 医疗箱）→ 直接存入该容器
        var 落点物 = 服务.该格物品(列, 行);
        if (落点物 != null && 落点物 != 堆叠 && 可存入容器(落点物, 堆叠))
        {
            var 视图 = ServiceRegistry.Get<容器服务>().打开(落点物);
            var 空位 = 视图.寻找可放置格智能旋转(堆叠, out bool 需旋转);
            if (空位 != null)
            {
                bool 快捷原旋转 = 堆叠.旋转;
                if (需旋转) 堆叠.旋转 = !快捷原旋转;
                int 存入数 = ServiceRegistry.Get<容器服务>().跨网格转移(源服务, 堆叠, 视图, 空位.Value.列, 空位.Value.行);
                if (存入数 <= 0) { 堆叠.旋转 = 快捷原旋转; return false; }
                请求刷新();
                ServiceRegistry.Get<EventBus>().发布(new 背包变化事件(堆叠.标识, 存入数, 变化原因.获得));
                return true;
            }
            return false;
        }
        bool 原旋转 = 堆叠.旋转;
        if (拖拽旋转 != 原旋转) 堆叠.旋转 = 拖拽旋转;
        int 转移 = ServiceRegistry.Get<容器服务>().跨网格转移(源服务, 堆叠, 服务, 列, 行);
        if (转移 <= 0) { 堆叠.旋转 = 原旋转; return false; }
        请求刷新();
        ServiceRegistry.Get<EventBus>().发布(new 背包变化事件(堆叠.标识, 转移, 变化原因.获得));
        return true;
    }
}
