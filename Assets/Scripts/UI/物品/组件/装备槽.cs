using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 装备槽（挂 装备槽预制体 根上）：一个槽位的显示与交互，参数在预制体上配置一次。
//   暴露参数：槽位名 + 品质图/物品图/物品名/耐久 + 框（预制体配好，改预制体全局生效）。
//   刷新：设置(玩家, 数据) 更新 品质图（品质色/空槽灰）、物品图（sprite）、物品名、耐久。
//   交互：点击本槽（最上层的 物品图 Image 接收点击，raycastTarget 保持 true）→ 右键菜单（详情/卸下）；
//         拖拽本槽装备 → 背包网格（卸下入格）/ 其他装备槽（类型匹配换槽）；框 供 装备区 拖拽穿戴命中。
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
    private 物品堆叠 拖拽堆叠;     // 拖拽期间的临时堆叠（缓存一次，避免 OnDrag 每帧 new 产生 GC）
    private RectTransform 代理根矩;   // 拖拽代理 根矩形（R 旋转 换 尺寸 用）
    private RectTransform 代理内容矩; // 拖拽代理 内容矩形（R 旋转 转 90° 用）
    private 物品形状 拖拽形状;        // 未旋转 形状（R 旋转 换向 用）

    // 刷新本槽显示（装备区 遍历调用）
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
                var 图标 = 物品图标服务.获取(物品.标识);
                物品图.sprite = 图标;
                物品图.color = 图标 != null ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0f);
            }
            面板基类.设文本(物品名, 物品.标识);
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

    // 点击本槽：有装备 → 右键呼出小菜单（详情/卸下）
    public void OnPointerClick(PointerEventData 事件)
    {
        if (事件.button != PointerEventData.InputButton.Right) return;
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null || string.IsNullOrEmpty(玩家.装备标识(槽位名))) return;   // 空槽：无操作
        if (右键菜单.实例 != null) 右键菜单.实例.显示装备槽(槽位名, 框);
    }

    // 拖拽落点高亮：预制体上的专门高亮图（绿=可放 / 红=类型不匹配）——装备区 遍历调用
    public void 显示高亮(bool 可放)
    {
        if (高亮图 == null) return;
        高亮图.color = 可放 ? 网格面板配色.放置可色 : 网格面板配色.放置禁色;   // 与网格拖拽投影同款配色（统一规范）
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
        拖拽堆叠 = new 物品堆叠(记录.标识, 1) { 配件 = 记录.配件, 当前耐久 = 记录.当前耐久, 已装填 = 记录.已装填 };   // 缓存本次拖拽的临时堆叠（投影/卸下用）
        拖拽形状 = 玩家?.网格服务?.形状解析?.Invoke(记录.标识) ?? new 物品形状(1, 1);
        拖拽堆叠.旋转 = false;   // 拖拽 起点：未旋转（R 可 翻转）
        音效管理器.实例?.播放拿起();   // 拿起音效（与网格内拖拽同流程）
        清除拖拽投影();
        创建拖拽代理(记录.标识);
    }

    public void OnDrag(PointerEventData 事件)
    {
        if (拖拽代理 != null) 拖拽代理.transform.position = 事件.position;   // 跟手
        更新拖拽投影(事件);   // 投影提示：目标槽位 绿/红、背包网格 绿框
    }

    // 拖拽中 R 旋转（每帧检测——按住不动 也 生效；与 网格内 拖拽 一致。放 Update 而非 OnDrag：防 同帧 双翻转）
    private void Update()
    {
        if (!拖拽中 || 拖拽堆叠 == null || !检测按R()) return;
        拖拽堆叠.旋转 = !拖拽堆叠.旋转;
        更新代理旋转();
        // 按 新 旋转 重判 落点 投影（伪事件 = 当前 鼠标 位置）
        var 伪事件 = new PointerEventData(EventSystem.current) { position = 输入鼠标位置() };
        更新拖拽投影(伪事件);
    }

    private static Vector2 输入鼠标位置()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null) return UnityEngine.InputSystem.Mouse.current.position.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#endif
        return Vector2.zero;
    }

    // 代理 随 旋转：根 尺寸 宽高 互换 + 内容 转 90°（与 网格内 拖拽代理 同款视觉）
    private void 更新代理旋转()
    {
        if (拖拽代理 == null || 拖拽形状 == null) return;
        float 格 = 物品网格面板.格尺寸;
        float 宽 = 拖拽堆叠.旋转 ? 拖拽形状.高 * 格 : 拖拽形状.宽 * 格;
        float 高 = 拖拽堆叠.旋转 ? 拖拽形状.宽 * 格 : 拖拽形状.高 * 格;
        if (代理根矩 != null)
        {
            代理根矩.sizeDelta = new Vector2(宽, 高);
            代理根矩.localRotation = Quaternion.identity;   // 根 不 转（转 内容）
        }
        if (代理内容矩 != null) 代理内容矩.localRotation = Quaternion.Euler(0f, 0f, 拖拽堆叠.旋转 ? 90f : 0f);
    }

    // 拖拽中 R 键 按下（新输入 优先；旧 Input 兜底——与 网格内 拖拽 一致）
    private bool 检测按R()
    {
        bool 按下 = false;
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame) 按下 = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.R)) 按下 = true;
#endif
        return 按下;
    }

    public void OnEndDrag(PointerEventData 事件)
    {
        if (!拖拽中) return;
        拖拽中 = false;
        清除拖拽投影();
        if (拖拽代理 != null) { Destroy(拖拽代理); 拖拽代理 = null; }
        代理根矩 = null; 代理内容矩 = null; 拖拽形状 = null;
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null) return;
        // ① 拖到 背包网格（任一 物品网格面板）→ 卸下到 指定格（像背包内拖拽：拖到哪放哪；目标格不可放 → 穿回原位）——登记表遍历（替代 FindObjectsOfType）
        foreach (var 面板 in 物品网格面板.面板登记表)
        {
            if (面板 is 家具网格面板) continue;   // 家具 网格（安全屋 房间）：装备 不 能 卸 进去
            if (!面板.gameObject.activeInHierarchy || !面板.屏幕命中(事件.position)) continue;
            // 保护：穿戴容器不能放进自己的容器里（目标网格的背包列表 == 本槽穿戴容器的 容器物品 同一引用 = 自己装自己）
            if (拖拽记录.容器物品 != null && 面板.视图服务 != null && 面板.视图服务.网格物品 == 拖拽记录.容器物品)
            {
                音效管理器.实例?.播放失败();
                if (装备区.实例 != null) 装备区.实例.请求刷新();   // 脏标记合并（事件驱动 Update 统一刷新）
                拖拽记录 = null; 拖拽堆叠 = null;
                return;
            }
            bool 成功;
            // 背包槽卸下也走 卸下到格（到哪放哪；卸下到格 内部处理 背包缩容校验/网格尺寸还原）——不再特例回主背包
            if (面板.屏幕到格(事件.position, 拖拽堆叠, out var 列, out var 行))
                成功 = 面板操作.卸下到格(玩家, 槽位名, 面板.视图服务, 列, 行, 拖拽堆叠.旋转);   // R 旋转 落点方向
            else 成功 = false;   // 落点在网格外/格坐标无效
            if (成功) 音效管理器.实例?.播放放下();
            else 音效管理器.实例?.播放失败();
            if (装备区.实例 != null) 装备区.实例.请求刷新();
            拖拽记录 = null; 拖拽堆叠 = null;
            return;
        }
        // ② 拖到 其他装备槽 → 换槽（槽位兼容：主副手互通，其余严格；互换：源槽 ↔ 目标槽 交换，旧件不回背包）
        if (装备区.实例 != null && 装备区.实例.命中槽位(事件.position, out var 目标槽) && 目标槽 != 槽位名)
        {
            var 数据 = ServiceRegistry.Get<DataService>();
            if (数据.物品.TryGetValue(拖拽记录.标识, out var 物品) && 面板操作.槽位匹配(物品.槽位, 目标槽))
            {
                var 事件总线 = ServiceRegistry.Get<EventBus>();
                var 源记录 = 玩家.卸下装备(槽位名);   // 源槽取出（拖拽的"这一件"）
                if (源记录 == null) { 音效管理器.实例?.播放失败(); 拖拽记录 = null; 拖拽堆叠 = null; return; }
                var 目标记录 = 玩家.卸下装备(目标槽);   // 目标槽取出（将被替换的旧件）
                // 互换：源 → 目标槽；目标 → 源槽（主副手互通 已由 进入条件 保证——目标 槽位 与 源槽 同类）
                玩家.装备到槽(目标槽, 源记录);
                if (目标记录 != null && !string.IsNullOrEmpty(目标记录.标识))
                    玩家.装备到槽(槽位名, 目标记录);
                事件总线?.发布(new 背包变化事件(源记录.标识, -1, 变化原因.消耗));
                事件总线?.发布(new 属性变化事件(玩家.体质, 玩家.力量, 玩家.智慧, 玩家.敏捷, 玩家.意志, 玩家.自由属性点));
                音效管理器.实例?.播放装备();   // 换槽（装备到 装备槽）→ 装备音效
                if (装备区.实例 != null) 装备区.实例.请求刷新();
                拖拽记录 = null; 拖拽堆叠 = null;
                return;
            }
        }
        音效管理器.实例?.播放失败();   // 无效落点：回原位（槽位未变）
        拖拽记录 = null; 拖拽堆叠 = null;
    }

    // 拖拽中 更新落点投影：装备槽（绿/红）或 背包网格（绿框）；每帧先清后显
    private void 更新拖拽投影(PointerEventData 事件)
    {
        清除拖拽投影();
        if (!拖拽中) return;
        // ① 其他装备槽：槽位兼容 → 绿，否则红
        if (装备区.实例 != null && 装备区.实例.命中槽位(事件.position, out var 目标槽) && 目标槽 != 槽位名)
        {
            var 数据 = ServiceRegistry.Get<DataService>();
            bool 匹配 = 数据.物品.TryGetValue(拖拽记录.标识, out var 物品) && 面板操作.槽位匹配(物品.槽位, 目标槽);
            装备区.实例.高亮槽位(目标槽, 匹配);
            return;
        }
        // ② 背包网格 → 落格投影（可放绿/不可放红，同背包内拖拽；自己容器 → 强制红）——登记表遍历（替代 FindObjectsOfType）
        if (拖拽堆叠 == null) return;
        foreach (var 面板 in 物品网格面板.面板登记表)
        {
            if (面板 is 家具网格面板) continue;   // 家具 网格：装备 不 显示 投影
            if (面板.gameObject.activeInHierarchy && 面板.屏幕命中(事件.position))
            {
                bool 自己容器 = 拖拽记录.容器物品 != null && 面板.视图服务 != null && 面板.视图服务.网格物品 == 拖拽记录.容器物品;
                面板.显示装备拖拽投影(拖拽堆叠, 事件.position, 自己容器);
                return;
            }
        }
    }

    // 清除全部拖拽投影（装备槽高亮 + 网格绿框）——登记表遍历（替代 FindObjectsOfType）
    private void 清除拖拽投影()
    {
        if (装备区.实例 != null) 装备区.实例.清除全部高亮();
        foreach (var 面板 in 物品网格面板.面板登记表)
            面板.隐藏装备拖拽投影();
    }

    // 跟手代理：物品图 挂 Canvas 顶层（半透明）
    // 结构 与 物品网格面板 拖拽代理 一致：根 = RectMask2D（裁剪 cover 溢出）+ 子 Image 内容图（等比放大铺满占格）
    private void 创建拖拽代理(string 标识)
    {
        var 数据 = ServiceRegistry.Get<DataService>();
        var 画布 = GetComponentInParent<Canvas>();
        if (画布 == null || 数据 == null || !数据.物品.TryGetValue(标识, out var 物品)) return;
        var 物体 = new GameObject("装备拖拽代理", typeof(RectTransform), typeof(RectMask2D));
        物体.transform.SetParent(画布.transform, false);
        物体.transform.SetAsLastSibling();
        var 内容体 = new GameObject("内容", typeof(RectTransform), typeof(Image));
        内容体.transform.SetParent(物体.transform, false);
        var 图 = 内容体.GetComponent<Image>();
        图.raycastTarget = false;   // 关键：不拦截 滚轮/点击（否则 Canvas 顶层大代理 挡住 下层 ScrollRect 滚动）
        var 图标 = 物品图标服务.获取(物品.标识);
        图.sprite = 图标;
        图.preserveAspect = false;   // cover：等比放大铺满，不拉伸变形
        图.color = 图标 != null ? new Color(1f, 1f, 1f, 0.7f) : new Color(0.6f, 0.6f, 0.7f, 0.7f);
        var 内容矩 = 内容体.GetComponent<RectTransform>();
        UI工具.设锚点(内容矩, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        内容矩.anchoredPosition = Vector2.zero;
        // 统一规格：代理 = 物品占格 × 统一格尺寸（物品网格面板.格尺寸=90，与网格内拖拽同规格）
        float 格 = 物品网格面板.格尺寸;
        var 档案 = ServiceRegistry.Get<PlayerService>()?.档案;
        var 形状 = 档案?.网格服务?.形状解析?.Invoke(标识) ?? new 物品形状(1, 1);
        var 矩 = 物体.GetComponent<RectTransform>();
        矩.sizeDelta = new Vector2(形状.宽 * 格, 形状.高 * 格);
        矩.pivot = new Vector2(0.5f, 0.5f);
        // 内容图 智能 cover：先 trim 透明留白（内容包围盒）→ 按 实际内容 等比放大至覆盖整个占格
        // （保持长宽比不变形；超出部分被 RectMask2D 居中裁剪；小物品 图标 不再 大片 空白）
        if (图标 != null)
        {
            float 内容宽 = 形状.宽 * 格;
            float 内容高 = 形状.高 * 格;
            var 盒 = 精灵内容包围盒.获取(图标);   // position=内容中心(归一化)，size=内容占比(归一化)
            float 画布宽 = 图标.bounds.size.x, 画布高 = 图标.bounds.size.y;
            float 内容宽盒 = 画布宽 * 盒.width, 内容高盒 = 画布高 * 盒.height;
            float 放大 = Mathf.Max(内容宽 / 内容宽盒, 内容高 / 内容高盒);
            内容矩.sizeDelta = new Vector2(画布宽 * 放大, 画布高 * 放大);
            内容矩.pivot = new Vector2(盒.x, 盒.y);   // pivot 移到 内容中心：放大后 内容 居中于占格
        }
        else 内容矩.sizeDelta = new Vector2(形状.宽 * 格, 形状.高 * 格);
        拖拽代理 = 物体;
        代理根矩 = 矩;
        代理内容矩 = 内容矩;
    }
}
