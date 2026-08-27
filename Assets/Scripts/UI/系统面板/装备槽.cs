using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 装备槽（挂 装备槽预制体 根上）：一个槽位的显示与交互，参数在预制体上配置一次。
//   暴露参数：槽位名 + 品质图/物品图/物品名/耐久 + 框（预制体配好，改预制体全局生效）。
//   刷新：设置(玩家, 数据) 更新 品质图（品质色/空槽灰）、物品图（sprite）、物品名、耐久。
//   交互：点击本槽（最上层的 物品图 Image 接收点击，raycastTarget 保持 true）→ 右键菜单（详情/卸下）；
//         拖拽本槽装备 → 背包网格（卸下入格）/ 其他装备槽（类型匹配换槽）；框 供 装备面板 拖拽穿戴命中。
public sealed class 装备槽 : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private string 槽位名;          // 该槽名称（主手/副手/头部/胸部/腿部/脚部/手部）
    [SerializeField] private Image 品质图;           // 品质底色（空槽=暗灰占位；已装备=物品品质色）——下层
    [SerializeField] private Image 物品图;           // 物品图标（sprite；无图=隐藏）——最上层，接收点击（raycastTarget=true）
    [SerializeField] private TMP_Text 物品名;        // 物品名称（空 = 未装备）
    [SerializeField] private TMP_Text 耐久;          // 耐久（无耐久为空）
    [SerializeField] private Image 高亮图;           // 拖拽落点高亮图（预制体专门放置；初始隐藏，绿=可放/红=不匹配）
    [SerializeField] private RectTransform 框;       // 槽位矩形（拖拽穿戴命中）

    public string 槽位 => 槽位名;
    public RectTransform 框体 => 框;

    // —— 拖拽状态 ——
    private bool 拖拽中;
    private 装备记录 拖拽记录;
    private GameObject 拖拽代理;   // 跟手物品图（挂 Canvas 顶层）

    // 刷新本槽显示（装备面板 遍历调用）
    public void 设置(玩家档案 玩家, DataService 数据)
    {
        if (玩家 == null) return;
        string 标识 = 玩家.装备标识(槽位名);
        if (string.IsNullOrEmpty(标识))
        {
            // 空槽：品质图/物品图 隐藏（视觉为空）；名称/耐久 清空
            if (品质图 != null) 品质图.gameObject.SetActive(false);
            if (物品图 != null) 物品图.gameObject.SetActive(false);
            面板基类.设文本(物品名, "");
            面板基类.设文本(耐久, "");
            return;
        }
        if (品质图 != null) 品质图.gameObject.SetActive(true);
        if (物品图 != null) 物品图.gameObject.SetActive(true);
        if (数据 != null && 数据.物品.TryGetValue(标识, out var 物品))
        {
            if (品质图 != null) 品质图.color = 品质工具.颜色(物品.品质档);   // 品质色底
            if (物品图 != null)
            {
                var 图标 = 物品图标服务.获取(物品.图片);
                物品图.sprite = 图标;
                物品图.color = 图标 != null ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0f);
            }
            面板基类.设文本(物品名, 物品.名称);
        }
        else
        {
            if (品质图 != null) { 品质图.gameObject.SetActive(true); 品质图.color = new Color(0.5f, 0.5f, 0.5f, 0.6f); }   // 数据缺失：中性灰占位
            面板基类.设文本(物品名, 标识);
        }
        int 耐上 = 玩家.有效最大耐久(标识);
        if (耐上 > 0) 面板基类.设文本(耐久, $"{玩家.装备当前耐久(槽位名)}/{耐上}");
        else 面板基类.设文本(耐久, "");
    }

    // 点击本槽：有装备 → 呼出右键菜单（详情/卸下）
    public void OnPointerClick(PointerEventData 事件)
    {
        if (事件.button != PointerEventData.InputButton.Left) return;
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null || string.IsNullOrEmpty(玩家.装备标识(槽位名))) return;   // 空槽：无操作
        if (右键菜单.实例 != null) 右键菜单.实例.显示装备槽(槽位名, 框);
    }

    // 拖拽落点高亮：预制体上的专门高亮图（绿=可放 / 红=类型不匹配）——装备面板 遍历调用
    public void 显示高亮(bool 可放)
    {
        if (高亮图 == null) return;
        高亮图.color = 可放 ? new Color(0.45f, 1f, 0.5f, 0.35f) : new Color(1f, 0.4f, 0.4f, 0.35f);
        高亮图.gameObject.SetActive(true);
    }

    public void 清除高亮()
    {
        if (高亮图 != null) 高亮图.gameObject.SetActive(false);
    }

    // ===== 拖拽（装备区 → 背包网格卸下 / 其他装备槽换槽） =====

    public void OnBeginDrag(PointerEventData 事件)
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        var 记录 = 玩家?.装备.Find(e => e.槽位 == 槽位名);
        if (记录 == null || string.IsNullOrEmpty(记录.标识)) return;   // 空槽不可拖
        拖拽中 = true;
        拖拽记录 = 记录;
        清除拖拽投影();
        创建拖拽代理(记录.标识);
    }

    public void OnDrag(PointerEventData 事件)
    {
        if (拖拽代理 != null) 拖拽代理.transform.position = 事件.position;   // 跟手
        更新拖拽投影(事件);   // 投影提示：目标槽位 绿/红、背包网格 绿框
    }

    public void OnEndDrag(PointerEventData 事件)
    {
        if (!拖拽中) return;
        拖拽中 = false;
        清除拖拽投影();
        if (拖拽代理 != null) { Destroy(拖拽代理); 拖拽代理 = null; }
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null) return;
        // ① 拖到 背包网格（任一 网格背包面板）→ 卸下到 指定格（像背包内拖拽：拖到哪放哪；目标格不可放 → 穿回原位）
        var 临时堆叠 = new 物品堆叠(拖拽记录.标识, 1) { 词缀 = 拖拽记录.词缀, 当前耐久 = 拖拽记录.当前耐久 };
        foreach (var 面板 in FindObjectsOfType<网格背包面板>())
        {
            if (!面板.gameObject.activeInHierarchy || !面板.屏幕命中(事件.position)) continue;
            // 保护：穿戴容器不能放进自己的容器里（目标网格的背包列表 == 本槽穿戴容器的 容器物品 同一引用 = 自己装自己）
            if (拖拽记录.容器物品 != null && 面板.视图服务 != null && 面板.视图服务.背包 == 拖拽记录.容器物品)
            {
                音效管理器.实例?.播放失败();
                if (装备面板.实例 != null) 装备面板.实例.刷新();
                return;
            }
            bool 成功;
            if (槽位名 == "背包")   // 背包槽卸下影响网格尺寸，走原自动逻辑（含缩容校验/回滚）
            {
                面板操作.卸下(玩家, 槽位名);
                成功 = 玩家.装备标识(槽位名) == null;
            }
            else if (面板.屏幕到格(事件.position, 临时堆叠, out var 列, out var 行))
                成功 = 面板操作.卸下到格(玩家, 槽位名, 面板.视图服务, 列, 行);
            else 成功 = false;   // 落点在网格外/格坐标无效
            if (成功) 音效管理器.实例?.播放成功();
            else 音效管理器.实例?.播放失败();
            if (装备面板.实例 != null) 装备面板.实例.刷新();
            return;
        }
        // ② 拖到 其他装备槽 → 换槽（槽位兼容：主副手互通，其余严格；实例级：保留词缀/耐久，不按标识取件）
        if (装备面板.实例 != null && 装备面板.实例.命中槽位(事件.position, out var 目标槽) && 目标槽 != 槽位名)
        {
            var 数据 = ServiceRegistry.Get<DataService>();
            if (数据.物品.TryGetValue(拖拽记录.标识, out var 物品) && 面板操作.槽位匹配(物品.槽位, 目标槽))
            {
                var 事件总线 = ServiceRegistry.Get<EventBus>();
                var 记录 = 玩家.卸下装备(槽位名);   // 源槽取出（拖拽的"这一件"）
                if (记录 == null) { 音效管理器.实例?.播放失败(); return; }
                var 容器源 = 记录.容器列 > 0 ? new 物品堆叠(记录.标识, 1) { 容器列 = 记录.容器列, 容器行 = 记录.容器行, 容器物品 = 记录.容器物品 } : null;   // 容器位换槽：内部物品保留
                var 旧 = 玩家.装备到槽(目标槽, 记录.标识, 记录.词缀, 记录.当前耐久, 容器源);   // 装入目标槽（旧件被替换返回）
                if (旧 != null && !string.IsNullOrEmpty(旧.标识) && 旧.标识 != 记录.标识)
                {
                    if (!玩家.放入网格堆叠(new 物品堆叠(旧.标识, 1) { 词缀 = 旧.词缀, 当前耐久 = 旧.当前耐久, 容器物品 = 旧.容器物品, 容器列 = 旧.容器列, 容器行 = 旧.容器行 }))
                    {
                        // 回滚：旧件回目标槽 + 拖拽装备回源槽
                        玩家.装备到槽(目标槽, 旧.标识, 旧.词缀, 旧.当前耐久, 旧.容器列 > 0 ? new 物品堆叠(旧.标识, 1) { 容器列 = 旧.容器列, 容器行 = 旧.容器行, 容器物品 = 旧.容器物品 } : null);
                        玩家.装备到槽(槽位名, 记录.标识, 记录.词缀, 记录.当前耐久, 容器源);
                        音效管理器.实例?.播放失败();
                        if (装备面板.实例 != null) 装备面板.实例.刷新();
                        return;
                    }
                    事件总线?.发布(new 背包变化事件(旧.标识, 1, 变化原因.获得));
                }
                事件总线?.发布(new 背包变化事件(记录.标识, -1, 变化原因.消耗));
                事件总线?.发布(new 属性变化事件(玩家.体质, 玩家.力量, 玩家.智慧, 玩家.敏捷, 玩家.意志, 玩家.自由属性点));
                音效管理器.实例?.播放成功();
                if (装备面板.实例 != null) 装备面板.实例.刷新();
                return;
            }
        }
        音效管理器.实例?.播放失败();   // 无效落点：回原位（槽位未变）
    }

    // 拖拽中 更新落点投影：装备槽（绿/红）或 背包网格（绿框）；每帧先清后显
    private void 更新拖拽投影(PointerEventData 事件)
    {
        清除拖拽投影();
        if (!拖拽中) return;
        // ① 其他装备槽：槽位兼容 → 绿，否则红
        if (装备面板.实例 != null && 装备面板.实例.命中槽位(事件.position, out var 目标槽) && 目标槽 != 槽位名)
        {
            var 数据 = ServiceRegistry.Get<DataService>();
            bool 匹配 = 数据.物品.TryGetValue(拖拽记录.标识, out var 物品) && 面板操作.槽位匹配(物品.槽位, 目标槽);
            装备面板.实例.高亮槽位(目标槽, 匹配);
            return;
        }
        // ② 背包网格 → 落格投影（可放绿/不可放红，同背包内拖拽；自己容器 → 强制红）
        var 堆叠 = new 物品堆叠(拖拽记录.标识, 1);
        foreach (var 面板 in FindObjectsOfType<网格背包面板>())
            if (面板.gameObject.activeInHierarchy && 面板.屏幕命中(事件.position))
            {
                bool 自己容器 = 拖拽记录.容器物品 != null && 面板.视图服务 != null && 面板.视图服务.背包 == 拖拽记录.容器物品;
                面板.显示装备拖拽投影(堆叠, 事件.position, 自己容器);
                return;
            }
    }

    // 清除全部拖拽投影（装备槽高亮 + 网格绿框）
    private void 清除拖拽投影()
    {
        if (装备面板.实例 != null) 装备面板.实例.清除全部高亮();
        foreach (var 面板 in FindObjectsOfType<网格背包面板>())
            面板.隐藏装备拖拽投影();
    }

    // 跟手代理：物品图 挂 Canvas 顶层（半透明）
    private void 创建拖拽代理(string 标识)
    {
        var 数据 = ServiceRegistry.Get<DataService>();
        var 画布 = GetComponentInParent<Canvas>();
        if (画布 == null || 数据 == null || !数据.物品.TryGetValue(标识, out var 物品)) return;
        var 物体 = new GameObject("装备拖拽代理", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(画布.transform, false);
        物体.transform.SetAsLastSibling();
        var 图 = 物体.GetComponent<Image>();
        var 图标 = 物品图标服务.获取(物品.图片);
        图.sprite = 图标;
        图.preserveAspect = true;   // 保持图标宽高比，不被压扁/拉伸
        图.color = 图标 != null ? new Color(1f, 1f, 1f, 0.7f) : new Color(0.6f, 0.6f, 0.7f, 0.7f);
        // 统一规格：代理 = 物品占格 × 主背包格尺寸（与背包内拖拽同规格）
        var 主面板 = 网格背包面板.主背包面板;
        float 格 = 主面板 != null ? 主面板.格子尺寸 : 90f;
        var 档案 = ServiceRegistry.Get<PlayerService>()?.档案;
        var 形状 = 档案?.背包服务?.形状解析?.Invoke(标识) ?? new 物品形状(1, 1);
        var 矩 = 物体.GetComponent<RectTransform>();
        矩.sizeDelta = new Vector2(形状.宽 * 格, 形状.高 * 格);
        矩.pivot = new Vector2(0.5f, 0.5f);
        拖拽代理 = 物体;
    }
}
