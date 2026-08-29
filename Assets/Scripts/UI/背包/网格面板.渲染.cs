using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// 网格面板.渲染 —— 分部类：网格结构 / 底格 / 分隔线 / 物品框 / 容器形状
// 与 网格面板.cs 同一 partial 类（共享字段与方法）；纯搬移，无行为改动。
// ============================================================
public sealed partial class 网格面板
{
    // 容器形状 块偏移：每个块 整体向右 偏移（左侧 并排块 的 缝隙 累计）——块间 真实 空隙，格子 尺寸 不变。
    // 例：弹挂 4 块 1×2 并排，块偏移 = 0, 6, 12, 18（每块间 6px 缝隙）；无形状/无并排 = 全 0。
    private const float 块缝隙宽 = 10f;   // 口袋 间 空隙（px）：块偏移 后 自然 露出，不 填色
    private float[] 块偏移;

    // 格 (列,行) 的 视觉 x：列×格尺寸 + 所属块的累计偏移（空洞格/无形状 = 列×格尺寸）
    private float 格x(int 列, int 行)
    {
        if (块偏移 == null) return 列 * 格尺寸;
        int 块 = 服务.该格块(列, 行);
        return 列 * 格尺寸 + (块 >= 0 && 块 < 块偏移.Length ? 块偏移[块] : 0f);
    }

    // 重建网格结构时 计算块偏移：对每对 (左块 A, 右块 B) 且 A 右边界 == B 左边界（并排贴邻）且 行范围重叠
    // → B 偏移 = A 偏移 + 缝隙宽（累计；无形状 或 无 并排 贴邻 = 全 0）
    private void 计算块偏移()
    {
        块偏移 = null;
        var 块们 = 服务.形状块;
        if (块们 == null || 块们.Count == 0) return;
        块偏移 = new float[块们.Count];
        for (int i = 0; i < 块们.Count; i++)
            for (int j = 0; j < 块们.Count; j++)
            {
                if (i == j) continue;
                var A = 块们[j]; var B = 块们[i];
                if (A.列 + A.宽 == B.列 && A.行 < B.行 + B.高 && B.行 < A.行 + A.高)   // A 在 B 左侧 且 贴邻 且 行重叠
                    块偏移[i] = Mathf.Max(块偏移[i], 块偏移[j] + 块缝隙宽);
            }
    }

    // 最右侧块 的 累计偏移（网格 宽度 补偿用；弹挂 4 块 = 3×缝隙）
    private float 最右偏移()
    {
        if (块偏移 == null || 服务.形状块 == null || 服务.形状块.Count == 0) return 0f;
        float 最右 = 0f;
        for (int i = 0; i < 服务.形状块.Count; i++)
            if (服务.形状块[i].列 + 服务.形状块[i].宽 == 当前列)   // 块 贴 网格 右边缘
                最右 = Mathf.Max(最右, 块偏移[i]);
        return 最右;
    }

    // 刷新网格：尺寸/首建变化 → 全量重建（底座+线+物品）；否则 → 增量刷新物品（复用物品框，不重建底格线）。
    // 增量刷新是性能核心：拖拽/装备只动变化的物品框，不再每次销毁重建整张网格（仓库 200 格 + 420 线）。
    private void 刷新网格()
    {
        if (网格容器 == null) return;
        // 外部视图（数据源非空）：用服务自身尺寸（容器/仓库/穿戴容器 由 注入方设置 服务.网格列/行）
        int 有效列, 有效行;
        if (数据源 != null)
        {
            有效列 = 服务.网格列;
            有效行 = 服务.网格行;
        }
        else
        {
            有效列 = 固定列数 > 0 ? 固定列数 : 服务.网格列;
            有效列 = Mathf.Clamp(有效列, 固定列数最小, 固定列数最大);
            有效行 = Mathf.Clamp(固定行数 > 0 ? 固定行数 : 服务.网格行, 固定行数最小, 固定行数最大);
            // 主背包：Inspector 参数写回 服务 网格（判定/放置 与渲染一致）；格尺寸 固定常量（网格面板.格尺寸）
            服务.网格列 = 有效列;
            服务.网格行 = 有效行;
        }
        if (当前列 != 有效列 || 当前行 != 有效行 || 底座层 == null || Mathf.Abs(上次格尺寸 - 格尺寸) > 0.01f)
        {
            当前列 = 有效列; 当前行 = 有效行;   // 渲染基准（层/线/格/物品统一用它）
            上次格尺寸 = 格尺寸;
            重建网格结构();
        }
        刷新物品();   // 增量：只动变化的物品框（尺寸刚变时 = 全建）
    }

    // 全量重建网格结构（仅 尺寸变化/首次）：尺寸设置 + 三层 + 底格 + 分隔线。物品框由 刷新物品 增量处理。
    private void 重建网格结构()
    {
        准备层();
        计算块偏移();   // 有形状（独立口袋）：块间 偏移 缝隙——视觉 空隙，格子 尺寸 不变
        var cf = 网格容器.GetComponent<ContentSizeFitter>();
        if (cf != null) cf.enabled = false;   // 禁用可能残留的 ContentSizeFitter，避免按子对象 preferred 把 Content 撑成 0（尺寸由本方法设置）
        // 网格宽 含 最右侧块 的 累计偏移（块间 缝隙 占用 的 宽度）
        float 网格宽 = 当前列 * 格尺寸 + 最右偏移(), 网格高 = 当前行 * 格尺寸;
        // 注：不 改动 Content 的 锚点/位置（场景 手动 配置 为准）——只 设置 尺寸。
        // 单点锚 下 sizeDelta 生效；拉伸锚 请 在 场景 配好 Content 尺寸（offset 拉伸 时 sizeDelta 无效）。
        if (数据源 != null)
            网格容器.sizeDelta = new Vector2(网格宽 + 网格面板配色.底盘外扩 * 2f, 网格高 + 网格面板配色.底盘外扩 * 2f);   // 容器 = 网格 + 底盘外框（四周 各 外扩；黑布/底盘 铺满 容器）
        else
        {
            float 视口宽 = 网格容器.parent != null ? ((RectTransform)网格容器.parent).rect.width : 网格宽;
            网格容器.sizeDelta = new Vector2(Mathf.Max(视口宽, 网格宽), 网格高);   // 主背包：宽=视口宽（内容可水平居中），高=网格高（滚动）
        }
        底座层.sizeDelta = new Vector2(网格宽, 网格高);   // 三层都以 网格 为基准：顶部 + 水平居中 于 Content
        线层.sizeDelta = new Vector2(网格宽, 网格高);
        物品层.sizeDelta = new Vector2(网格宽, 网格高);
        // 底盘：数据源非空 = stretch 铺满容器（offset 恒 0，容器 = 网格 + 底盘外框，自动跟随，勿设 sizeDelta——会重算 offset 出现数字）；
        // 主背包（数据源 null）= 手算 sizeDelta（容器 = 视口宽，不铺满）
        if (数据源 == null && 底盘 != null)
            底盘.sizeDelta = new Vector2(网格宽 + 网格面板配色.底盘外扩 * 2f, 网格高 + 网格面板配色.底盘外扩 * 2f);
        清空层(底座层); 清空层(线层); 清空层(物品层);
        for (int 行 = 0; 行 < 当前行; 行++)
            for (int 列 = 0; 列 < 当前列; 列++)
                创建底格(列, 行);
        画分隔线();   // 线在格子之间（格子在线内）
        更新布局快照();   // 全量重建：布局快照 与 网格 同步（物品框 由 下次 刷新物品 全建）
        清空物品框表();   // 网格结构变化 → 旧物品框全部失效（下次 刷新物品 全建）
    }

    // 清空物品框表（全量重建前调用：旧框销毁，下次 刷新物品 全建）
    private void 清空物品框表()
    {
        foreach (var kv in 物品框表)
            if (kv.Value != null && kv.Value.根 != null) Destroy(kv.Value.根.gameObject);
        物品框表.Clear();
    }

    // 增量刷新物品：遍历 服务.背包 —— 新增创建 / 已有更新（位置/旋转/品质/数量/耐久）/ 移除销毁；不重建底格。
    // 物品 布局（增删/移动/旋转）变化 → 重画 分隔线（物品边界线 亮/内部 淡 跟随 物品 覆盖）。
    private void 刷新物品()
    {
        if (物品层 == null) return;
        // 兜底：尺寸/格尺寸 可能已变（换包/外部注入新尺寸）→ 走全量重建
        if (当前列 != 服务.网格列 || 当前行 != 服务.网格行 || Mathf.Abs(上次格尺寸 - 格尺寸) > 0.01f) { 刷新网格(); return; }
        // 布局快照 对比：任一 堆叠 的 列/行/旋转 变化（含 增删）→ 重画 分隔线
        bool 布局变化 = false;
        if (上次布局.Count != 服务.背包.Count) 布局变化 = true;
        else
        {
            for (int i = 0; i < 服务.背包.Count && !布局变化; i++)
            {
                var s = 服务.背包[i];
                if (s == null) continue;
                if (!上次布局.TryGetValue(s, out var 上次)) { 布局变化 = true; break; }
                if (上次.列 != s.列 || 上次.行 != s.行 || 上次.旋转 != s.旋转) 布局变化 = true;
            }
        }
        if (布局变化) { 重画分隔线(); 更新布局快照(); }
        // 先复位全部存活标记：上一帧命中的框 存活=true，本帧必须从 false 起算——
        // 否则"本帧被移除的物品"的框 存活 沿用 true → 不销毁 → 物品图片残留（装备成功但图片还在 的 bug）
        foreach (var kv in 物品框表) kv.Value.存活 = false;
        foreach (var 堆叠 in 服务.背包)
        {
            if (堆叠 == null || 堆叠.列 < 0) continue;
            if (物品框表.TryGetValue(堆叠, out var 框)) { 框.存活 = true; 更新物品框(框, 堆叠); }
            else { var 新框 = 创建物品(堆叠); if (新框 != null) { 新框.存活 = true; 物品框表[堆叠] = 新框; } }
        }
        // 未命中的框（存活=false：物品已从网格移除）→ 销毁并从表移除
        if (物品框表.Count == 0) return;
        List<物品堆叠> 待删 = null;
        foreach (var kv in 物品框表)
            if (!kv.Value.存活) (待删 ??= new List<物品堆叠>()).Add(kv.Key);
        if (待删 != null)
            foreach (var 堆叠 in 待删)
            {
                if (物品框表.TryGetValue(堆叠, out var 框) && 框.根 != null) Destroy(框.根.gameObject);
                物品框表.Remove(堆叠);
            }
    }

    // —— 物品 布局 快照（分隔线 重画 判定）——
    private readonly Dictionary<物品堆叠, (int 列, int 行, bool 旋转)> 上次布局 = new Dictionary<物品堆叠, (int, int, bool)>();
    private void 更新布局快照()
    {
        上次布局.Clear();
        foreach (var s in 服务.背包)
            if (s != null) 上次布局[s] = (s.列, s.行, s.旋转);
    }

    // 只 重画 分隔线（线层 全清 重画；底格/物品 不动）——物品 边界线 跟随 物品 覆盖
    private void 重画分隔线()
    {
        if (线层 == null) return;
        清空层(线层);
        画分隔线();
    }

    // 首次准备网格分层：Content(网格容器) 下 底盘 / 底座层 / 线层 / 物品层；手动定位（不挂 GridLayoutGroup）
    private void 准备层()
    {
        if (底座层 != null) return;
        创建底盘();   // 整块 衬底（垫底：底格/线/物品 之下）——比 网格层 四周 各大 1px
        底座层 = 创建网格层("底座层");
        线层 = 创建网格层("线层");
        物品层 = 创建网格层("物品层");
        // 注：不挂 GridLayoutGroup/ContentSizeFitter——底格手动定位、Content 尺寸手动撑，
        //     避免 GridLayoutGroup 的 LayoutRebuilder 在销毁格后访问已销毁实例的 MissingReference 报错。
    }

    // 网格底盘：一张完整的 衬底图（Image），比 网格层 四周 各大 底盘外扩——网格 整体 的 托盘/边框 感。
    // 外部视图（数据源非空）：容器 = 网格 + 外扩×2 → 底盘 stretch 铺满容器（= 网格 + 四周各 外扩，均匀）；
    // 主背包（数据源 null）：容器 = 视口宽（不含外扩）→ 底盘 按 网格 手算（顶部对齐 + 上移 外扩 补偿）。
    // 穿戴容器块（所属槽位 非空，中区 三块）不需要 底盘——纯代码判定，无需手动配置。
    private void 创建底盘()
    {
        if (网格容器 == null || !string.IsNullOrEmpty(所属槽位)) return;
        var 物体 = new GameObject("底盘", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(网格容器, false);
        物体.transform.SetAsFirstSibling();   // 垫底（底格/线/物品 全部 之上）
        var 图 = 物体.GetComponent<Image>();
        图.color = 网格面板配色.底盘色;
        图.raycastTarget = false;   // 纯衬底，不挡交互
        var 矩形 = 物体.GetComponent<RectTransform>();
        if (数据源 != null)
        {
            // 容器 = 网格 + 外扩×2：底盘 铺满 容器（四周 均匀 各 外扩）
            矩形.anchorMin = Vector2.zero;
            矩形.anchorMax = Vector2.one;
            矩形.offsetMin = Vector2.zero;
            矩形.offsetMax = Vector2.zero;
        }
        else
        {
            // 主背包：容器 = 视口宽 → 底盘 按 网格 手算（顶部对齐，上移 外扩 使 四周 均匀）
            矩形.anchorMin = new Vector2(0.5f, 1f);
            矩形.anchorMax = new Vector2(0.5f, 1f);
            矩形.pivot = new Vector2(0.5f, 1f);
            矩形.anchoredPosition = new Vector2(0f, 网格面板配色.底盘外扩);
            矩形.sizeDelta = new Vector2(当前列 * 格尺寸 + 最右偏移() + 网格面板配色.底盘外扩 * 2f, 当前行 * 格尺寸 + 网格面板配色.底盘外扩 * 2f);
        }
        底盘 = 矩形;
    }

    // 创建网格层（在 网格容器 下）：撑满 Content、pivot 左上——物品/线 以 网格 左上为原点绝对定位
    // 外部视图（数据源非空）：容器 = 网格 + 底盘外扩×2 → 层 下移 外扩（网格内容 在 容器内 垂直居中，四周 均匀 露出 底盘）
    private RectTransform 创建网格层(string 名字)
    {
        var 物体 = new GameObject(名字, typeof(RectTransform));
        物体.transform.SetParent(网格容器, false);
        var r = 物体.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 1f);
        r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(0f, 数据源 != null ? -网格面板配色.底盘外扩 : 0f);
        r.sizeDelta = new Vector2(当前列 * 格尺寸 + 最右偏移(), 当前行 * 格尺寸);   // 层 = 网格尺寸（含块偏移），顶部 + 水平居中 于 Content
        return r;
    }

    // 底座一格（底图；手动铺格）
    // 容器内部形状：空洞格（服务.该格可用=false）不画底图——空洞区 与 块间缝隙（画在 线层）区分
    private void 创建底格(int 列, int 行)
    {
        if (!服务.该格可用(列, 行)) return;   // 空洞格：不创建底图
        var 物体 = new GameObject($"底格_{行}_{列}", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(底座层, false);
        var 图 = 物体.GetComponent<Image>();
        图.sprite = 网格底层精灵();   // 网格底层纹理（Resources/Art/网格底层.png；加载失败 = null → 纯色兜底）
        图.color = 网格面板配色.底座色;   // 纯白：精灵 原样 着色
        图.raycastTarget = false;   // 纯底图（不描边——分隔线由 画分隔线 统一绘制，格子在线内）
        定位(物体.GetComponent<RectTransform>(), 列, 行, 1, 1);   // 手动铺格（相对 底座层 左上）
    }

    // 网格底层精灵：缓存加载 Resources/Art/网格底层.png（Single 模式 → Resources.Load 直接取精灵）
    private static Sprite 网格底层缓存;
    private static Sprite 网格底层精灵()
    {
        if (网格底层缓存 == null)
            网格底层缓存 = Resources.Load<Sprite>("Art/网格底层");
        return 网格底层缓存;
    }

    // 画网格分隔线（按物品覆盖分段）：物品边界线亮；物品内部线与空格线一样淡。
    // 无形状（整矩形）→ 原全网格连续线；有形状（独立口袋）→ 格子尺寸不变，块间自然空隙，每块四边轮廓框
    private void 画分隔线()
    {
        if (服务.形状块 != null && 服务.形状块.Count > 0)
        {
            画块网格线();
            return;
        }
        for (int i = 0; i <= 当前列; i++)
            for (int j = 0; j < 当前行; j++)
                画竖线段(i, j, 竖线边界(i, j));
        for (int j = 0; j <= 当前行; j++)
            for (int i = 0; i < 当前列; i++)
                画横线段(i, j, 横线边界(i, j));
    }

    // 有形状网格的分隔线：逐格线判定——同块内 = 按物品边界（边缘亮/同物品内部淡）；不同块相邻 = 两条形状框线；
    // 块外边界 = 形状 轮廓 框（形状边界色；块内 有 物品 → 加粗）
    private void 画块网格线()
    {
        // 竖线：分隔 左格(列-1,行) 与 右格(列,行)
        for (int 列 = 0; 列 <= 当前列; 列++)
            for (int 行 = 0; 行 < 当前行; 行++)
            {
                int 左块 = 列 > 0 ? 服务.该格块(列 - 1, 行) : -1;
                int 右块 = 列 < 当前列 ? 服务.该格块(列, 行) : -1;
                if (左块 < 0 && 右块 < 0) continue;   // 两侧都空洞 → 不画
                if (左块 >= 0 && 右块 >= 0 && 左块 != 右块)
                {
                    // 不同口袋 相邻：两块 各画 一条 形状框线（左块右缘 + 右块左缘）——中间 空隙 自然 露出；有 物品 的 口袋 加粗
                    画竖线段(列, 行, true, false, true, 块内有物品(左块));
                    画竖线段(列, 行, true, true, true, 块内有物品(右块));
                    continue;
                }
                if (左块 >= 0 && 右块 >= 0) { 画竖线段(列, 行, 竖线边界(列, 行)); continue; }   // 同口袋内部 → 按 物品 边界（边缘亮/同物品淡）
                画竖线段(列, 行, true, false, true, 块内有物品(左块 >= 0 ? 左块 : 右块));      // 口袋外边界 → 形状 轮廓 框（有物品加粗）
            }
        // 横线：分隔 上格(列,行-1) 与 下格(列,行)
        for (int 行 = 0; 行 <= 当前行; 行++)
            for (int 列 = 0; 列 < 当前列; 列++)
            {
                int 上块 = 行 > 0 ? 服务.该格块(列, 行 - 1) : -1;
                int 下块 = 行 < 当前行 ? 服务.该格块(列, 行) : -1;
                if (上块 < 0 && 下块 < 0) continue;
                if (上块 >= 0 && 下块 >= 0 && 上块 != 下块) { 画横缝隙(列, 行); continue; }
                if (上块 >= 0 && 下块 >= 0) { 画横线段(列, 行, 横线边界(列, 行)); continue; }   // 同口袋内部 → 按 物品 边界
                画横线段(列, 行, true, true, 块内有物品(上块 >= 0 ? 上块 : 下块));              // 口袋外边界 → 形状 轮廓 框（有物品加粗）
            }
    }

    // 该块（口袋）内 是否 有 物品（有 → 口袋 边界 加粗）
    private bool 块内有物品(int 块索引)
    {
        var 块们 = 服务.形状块;
        if (块们 == null || 块索引 < 0 || 块索引 >= 块们.Count) return false;
        var 块 = 块们[块索引];
        foreach (var s in 服务.背包)
        {
            if (s == null || s.列 < 0) continue;
            var (宽, 高) = 服务.物品占格(s);
            if (s.列 < 块.列 + 块.宽 && s.列 + 宽 > 块.列 && s.行 < 块.行 + 块.高 && s.行 + 高 > 块.行) return true;
        }
        return false;
    }

    // 竖缝隙条：已废弃——块间空隙 由 块偏移 自然 露出（缝隙 不 填色），本方法 不再 使用
    private void 画竖缝隙(int 列, int 行) { }

    // 横缝隙条：已废弃——块间空隙 由 块偏移 自然 露出（缝隙 不 填色），本方法 不再 使用
    private void 画横缝隙(int 列, int 行) { }

    // 竖线段的"物品边界"判定：两侧都是空格 → 淡；同一物品内部 → 淡；否则（一侧有物品/不同物品）→ 亮
    private bool 竖线边界(int i, int j)
    {
        var 左格 = i > 0 ? 该格物品(i - 1, j) : null;
        var 右格 = i < 当前列 ? 该格物品(i, j) : null;
        if (左格 == null && 右格 == null) return false;   // 两侧都空 → 淡
        if (左格 != null && 左格 == 右格) return false;   // 同一物品内部 → 淡
        return true;                                       // 物品边缘 → 亮
    }

    // 横线段的"物品边界"判定
    private bool 横线边界(int i, int j)
    {
        var 上格 = j > 0 ? 该格物品(i, j - 1) : null;
        var 下格 = j < 当前行 ? 该格物品(i, j) : null;
        if (上格 == null && 下格 == null) return false;
        if (上格 != null && 上格 == 下格) return false;
        return true;
    }

    // 竖线 | 格边界：列 i 与 行 j 交点的一段（向下 格尺寸 高）；亮=物品边界，否则内部/空格（淡）
    // 空洞格（容器形状外）不画线——空洞区保持干净背景。
    // 竖线在 (列,行) 分隔 左格(列-1,行) 与 右格(列,行)：任一侧可用才画（最左/最右边界线不因越界被跳过）；x 含块偏移
    // 贴右 = true：画在 右格 左缘（并排块 的 左框线）；false = 左格 右缘（左框线 或 单侧）
    // 形状线 = true：用 形状边界色（口袋 轮廓 框 专属 色）；加粗 = true：用 物品边界线宽（口袋 有 物品 时 边界 加粗）
    private void 画竖线段(int 列, int 行, bool 亮, bool 贴右 = false, bool 形状线 = false, bool 加粗 = false)
    {
        bool 左可用 = 列 > 0 && 服务.该格可用(列 - 1, 行);
        bool 右可用 = 列 < 当前列 && 服务.该格可用(列, 行);
        if (!左可用 && !右可用) return;   // 两侧都空洞 → 不画
        float x = 贴右 ? 格x(列, 行) : (左可用 ? 格x(列 - 1, 行) + 格尺寸 : 格x(列, 行));
        var 物体 = new GameObject($"竖线_{列}_{行}", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(线层, false);
        var 图 = 物体.GetComponent<Image>();
        图.color = 形状线 ? 网格面板配色.形状边界色 : (亮 ? 网格面板配色.物品边界色 : 网格面板配色.线条色);
        图.raycastTarget = false;
        var 矩形 = 物体.GetComponent<RectTransform>();
        矩形.anchorMin = new Vector2(0, 1);
        矩形.anchorMax = new Vector2(0, 1);
        矩形.pivot = new Vector2(0.5f, 0.5f);
        矩形.anchoredPosition = new Vector2(x, -(行 + 0.5f) * 格尺寸);
        // 线宽：物品边界（亮且非形状线）或 口袋 加粗（有物品）→ 物品边界线宽；否则 普通线宽
        float 线宽 = ((亮 && !形状线) || 加粗) ? 网格面板配色.物品边界线宽 : 网格面板配色.线宽;
        矩形.sizeDelta = new Vector2(线宽, 格尺寸);
    }

    // 横线 ─ 格边界：行 j 与 列 i 交点的一段（向右 格尺寸 宽）；亮=物品边界，否则内部/空格（淡）
    // 空洞格（容器形状外）不画线——空洞区保持干净背景。
    // 横线在 (列,行) 分隔 上格(列,行-1) 与 下格(列,行)：任一侧可用才画（最上/最下边界线不因越界被跳过）；x 含块偏移
    // 注：下边界（行 == 当前行，越界）x 偏移 取 上格（行-1）的块——否则 偏移 丢失，下边界线 错位 连成 一条
    // 形状线 = true：用 形状边界色（口袋 轮廓 框 专属 色）
    private void 画横线段(int 列, int 行, bool 亮, bool 形状线 = false, bool 加粗 = false)
    {
        bool 上可用 = 行 > 0 && 服务.该格可用(列, 行 - 1);
        bool 下可用 = 行 < 当前行 && 服务.该格可用(列, 行);
        if (!上可用 && !下可用) return;   // 两侧都空洞 → 不画
        int 参考行 = 行 < 当前行 ? 行 : 行 - 1;   // 下边界：用 上格 的 行（取 块偏移）
        var 物体 = new GameObject($"横线_{行}_{列}", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(线层, false);
        var 图 = 物体.GetComponent<Image>();
        图.color = 形状线 ? 网格面板配色.形状边界色 : (亮 ? 网格面板配色.物品边界色 : 网格面板配色.线条色);
        图.raycastTarget = false;
        var 矩形 = 物体.GetComponent<RectTransform>();
        矩形.anchorMin = new Vector2(0, 1);
        矩形.anchorMax = new Vector2(0, 1);
        矩形.pivot = new Vector2(0.5f, 0.5f);
        矩形.anchoredPosition = new Vector2(格x(列, 参考行) + 格尺寸 / 2f, -行 * 格尺寸);
        // 线宽：物品边界（亮且非形状线）或 口袋 加粗（有物品）→ 物品边界线宽；否则 普通线宽
        float 线宽 = ((亮 && !形状线) || 加粗) ? 网格面板配色.物品边界线宽 : 网格面板配色.线宽;
        矩形.sizeDelta = new Vector2(格尺寸, 线宽);
    }

    // 物品：两层结构 —— ① 物品框（全尺寸 Image = 品质底层色 + 黑描边，点击/拖拽挂这里）→ ② 内容层（内缩 Image = 深色占位块，将来贴美术图）。
    // 品质色永远在框层：内容层内缩 物品边距，无论现在是色块还是将来的美术图，四周都会露出品质色环。
    // 返回 物品框（增量刷新：键=堆叠实例，复用更新；null = 数据缺失不创建）
    private 物品框 创建物品(物品堆叠 堆叠)
    {
        if (!数据.物品.TryGetValue(堆叠.标识, out var 物品)) return null;
        var 框 = new 物品框();
        var (宽, 高) = 服务.物品占格(堆叠);   // 领域规则：形状×旋转 → 占格
        // ① 物品框：全尺寸贴格（品质底层色；选中 = 选中底色）
        var 物体 = new GameObject($"物品_{物品.名称}", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(物品层, false);
        框.根 = 物体.GetComponent<RectTransform>();
        框.框图 = 物体.GetComponent<Image>();
        框.框图.color = 品质底层色(堆叠);   // 不再用选中底色
        var 边框 = 物体.AddComponent<Outline>();
        边框.effectColor = new Color(0f, 0f, 0f, 0.6f);
        边框.effectDistance = new Vector2(3f, -3f);
        框.根.anchorMin = new Vector2(0, 1);
        框.根.anchorMax = new Vector2(0, 1);
        框.根.pivot = new Vector2(0, 1);
        框.根.anchoredPosition = new Vector2(格x(堆叠.列, 堆叠.行), -堆叠.行 * 格尺寸);
        框.根.sizeDelta = new Vector2(宽 * 格尺寸, 高 * 格尺寸);
        物体.AddComponent<RectMask2D>();   // 裁剪 cover 图标溢出（物品图片填满格子，超出部分裁掉）
        // ② 高光层：品质底 与 内容 之间（白色半透明，悬停时显示）
        var 高物体 = new GameObject("高光", typeof(RectTransform), typeof(Image));
        高物体.transform.SetParent(物体.transform, false);
        var 高图 = 高物体.GetComponent<Image>();
        高图.color = 网格面板配色.高光色;
        高图.raycastTarget = false;
        var 高矩 = 高物体.GetComponent<RectTransform>();
        高矩.anchorMin = Vector2.zero;
        高矩.anchorMax = Vector2.one;
        高矩.offsetMin = new Vector2(-网格面板配色.线宽 / 2f, -网格面板配色.线宽 / 2f);
        高矩.offsetMax = new Vector2(网格面板配色.线宽 / 2f, 网格面板配色.线宽 / 2f);   // 高光层覆盖到网格线条上
        高物体.SetActive(false);   // 默认隐藏，悬停显示
        框.高光层 = 高物体;
        // ③ 内容层（内缩：尺寸少 2×边距，向格内偏移——不压网格线、不叠品质环；将来替换为美术图 sprite）
        var 内容物体 = new GameObject("内容", typeof(RectTransform), typeof(Image));
        内容物体.transform.SetParent(物体.transform, false);
        var 内容图 = 内容物体.GetComponent<Image>();
        内容图.color = 网格面板配色.物品底色;
        内容图.raycastTarget = false;   // 不挡底层交互（点击/拖拽挂在物品框上）
        // 手动挂图：items.json 的 "图片" 引用 → 内容层显示精灵；无图/未挂 = 保持色块（品质色环在物品框层不受影响）
        var 未旋转 = 服务.形状解析?.Invoke(堆叠.标识) ?? new 物品形状(1, 1);   // 内容层用未旋转宽高（旋转由 rotation 承担）
        float 内容宽 = 未旋转.宽 * 格尺寸 - 网格面板配色.物品边距 * 2f;
        float 内容高 = 未旋转.高 * 格尺寸 - 网格面板配色.物品边距 * 2f;
        var 图标 = 物品图标服务.获取(物品.图片);
        Vector2 内容中心 = new Vector2(0.5f, 0.5f);   // 内容 pivot（默认画布中心；trim 后 = 内容包围盒中心）
        if (图标 != null)
        {
            内容图.sprite = 图标;
            内容图.color = Color.white;          // 有图时不再用色底染色
            // 智能 cover：先 trim 透明留白（内容包围盒）→ 按 实际内容 等比放大至覆盖整个物品框
            // （保持长宽比不变形；超出部分被 RectMask2D 居中裁剪；小物品 图标 不再 大片 空白）
            内容图.preserveAspect = false;
            var 盒 = 精灵内容包围盒.获取(图标);   // position=内容中心(归一化)，size=内容占比(归一化)
            float 画布宽 = 图标.bounds.size.x, 画布高 = 图标.bounds.size.y;   // 整张画布（含透明边）
            float 内容宽盒 = 画布宽 * 盒.width, 内容高盒 = 画布高 * 盒.height;   // 实际内容尺寸
            float 放大 = Mathf.Max(内容宽 / 内容宽盒, 内容高 / 内容高盒);       // 内容 cover 撑满框
            内容宽 = 画布宽 * 放大;   // 画布整体按同倍率放大（内容部分恰好覆盖框）
            内容高 = 画布高 * 放大;
            内容中心 = new Vector2(盒.x, 盒.y);   // pivot 移到 内容中心：放大后 内容 居中于框
        }
        var 内容矩形 = 内容物体.GetComponent<RectTransform>();
        内容矩形.anchorMin = new Vector2(0.5f, 0.5f);
        内容矩形.anchorMax = new Vector2(0.5f, 0.5f);
        内容矩形.pivot = 内容中心;
        内容矩形.anchoredPosition = Vector2.zero;   // 居中于物品框
        内容矩形.sizeDelta = new Vector2(内容宽, 内容高);
        内容矩形.localRotation = Quaternion.Euler(0f, 0f, 堆叠.旋转 ? 90f : 0f);   // 图标跟随物品旋转 90°
        框.内容层 = 内容矩形;
        // 物品名文本：居中显示（美术资源缺失时以文本标识物品；叠加在物品块中央，不随内容层旋转）
        var 名称体 = new GameObject("名称", typeof(RectTransform), typeof(TextMeshProUGUI));
        名称体.transform.SetParent(物体.transform, false);
        var 名称矩 = 名称体.GetComponent<RectTransform>();
        名称矩.anchorMin = Vector2.zero;
        名称矩.anchorMax = Vector2.one;
        名称矩.offsetMin = Vector2.zero;
        名称矩.offsetMax = Vector2.zero;
        var 名称 = 名称体.GetComponent<TextMeshProUGUI>();
        名称.text = 物品.名称;
        名称.fontSize = Mathf.Clamp(格尺寸 * 0.22f, 14f, 44f);   // 字号随格尺寸（2K 基准）
        名称.alignment = TextAlignmentOptions.Center;
        名称.enableWordWrapping = true;
        名称.color = Color.white;
        名称.raycastTarget = false;
        框.名称 = 名称;
        // 耐久：有最大耐久的物品在格底显示 当前/最大；损坏变红
        int 耐久上限 = 档案.有效最大耐久(堆叠.标识);
        if (耐久上限 > 0)
        {
            var 耐体 = new GameObject("耐久", typeof(RectTransform), typeof(TextMeshProUGUI));
            耐体.transform.SetParent(物体.transform, false);
            var 耐 = 耐体.GetComponent<TextMeshProUGUI>();
            耐.text = 堆叠.当前耐久 <= 0 ? "损坏" : $"{堆叠.当前耐久}/{耐久上限}";
            耐.fontSize = 29f;
            耐.alignment = TextAlignmentOptions.Bottom;
            耐.color = 堆叠.当前耐久 <= 0 ? new Color(1f, 0.5f, 0.4f) : new Color(0.92f, 0.92f, 0.92f);
            耐.raycastTarget = false;
            var 耐矩 = 耐体.GetComponent<RectTransform>();
            耐矩.anchorMin = new Vector2(0, 0);
            耐矩.anchorMax = new Vector2(1, 0);
            耐矩.pivot = new Vector2(0.5f, 0);
            耐矩.anchoredPosition = new Vector2(0, 2f);
            耐矩.sizeDelta = new Vector2(-8f, 26f);
            框.耐久 = 耐;
            框.上次耐久 = 耐.text;
        }
        // 数量角标：**总是创建**（数量 ≤1 时隐藏、>1 显示）——若创建时 数量≤1 不建，框.数量 为 null，
        // 之后 合并/拆回 数量>1 也无法补显（bug：拆分 5→4+1 再合并回 5，目标框 数量文本 永远不出现）
        var 数体 = new GameObject("数量", typeof(RectTransform), typeof(TextMeshProUGUI));
        数体.transform.SetParent(物体.transform, false);
        var 数 = 数体.GetComponent<TextMeshProUGUI>();
        数.text = 堆叠.数量.ToString();
        数.fontSize = 40f;
        数.alignment = TextAlignmentOptions.TopRight;   // 居顶（数量 从 角标 顶部 排布）
        数.color = Color.white;
        数.raycastTarget = false;
        var 数矩 = 数体.GetComponent<RectTransform>();
        数矩.anchorMin = new Vector2(1, 0);
        数矩.anchorMax = new Vector2(1, 0);
        数矩.pivot = new Vector2(1, 0);
        数矩.anchoredPosition = new Vector2(-5f, -2f);   // 右下顶角
        数矩.sizeDelta = new Vector2(70f, 34f);
        数体.SetActive(堆叠.数量 > 1);   // 初始：>1 显示（≤1 隐藏，更新 时 自动 切换）
        框.数量 = 数;
        框.上次数量 = 堆叠.数量;
        // ④ 点击（非按钮） + 拖拽（挂在物品框上）
        var 点击 = 物体.AddComponent<物品点击>();
        点击.堆叠 = 堆叠;
        点击.面板 = this;
        点击.物品框 = 框.根;   // 右键菜单定位参考（物品右边界）
        点击.内容层 = 内容矩形;   // 悬停放大
        点击.高光层 = 高物体;      // 悬停显示高光
        var 拖拽 = 物体.AddComponent<物品拖拽>();
        拖拽.堆叠 = 堆叠;
        拖拽.面板 = this;
        return 框;
    }

    // 更新已有物品框（增量）：位置/尺寸/旋转/品质色/数量/耐久 变化才改；不重建任何组件（性能核心：拖拽移动只改位置）
    private void 更新物品框(物品框 框, 物品堆叠 堆叠)
    {
        if (框 == null || 框.根 == null) return;
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
        if (框.框图 != null)
        {
            var 色 = 品质底层色(堆叠);
            if (框.框图.color != 色) 框.框图.color = 色;
        }
        // 数量角标：>1 显示，否则隐藏
        if (框.数量 != null)
        {
            if (堆叠.数量 > 1)
            {
                if (!框.数量.gameObject.activeSelf) 框.数量.gameObject.SetActive(true);
                if (框.上次数量 != 堆叠.数量) { 框.数量.text = 堆叠.数量.ToString(); 框.上次数量 = 堆叠.数量; }
            }
            else if (框.数量.gameObject.activeSelf) { 框.数量.gameObject.SetActive(false); 框.上次数量 = 0; }
        }
        // 耐久：上限>0 显示（损坏变红），否则隐藏
        if (框.耐久 != null)
        {
            int 上限 = 档案.有效最大耐久(堆叠.标识);
            if (上限 > 0)
            {
                string 文本 = 堆叠.当前耐久 <= 0 ? "损坏" : $"{堆叠.当前耐久}/{上限}";
                if (框.上次耐久 != 文本) { 框.耐久.text = 文本; 框.上次耐久 = 文本; }
                if (!框.耐久.gameObject.activeSelf) 框.耐久.gameObject.SetActive(true);
                框.耐久.color = 堆叠.当前耐久 <= 0 ? new Color(1f, 0.5f, 0.4f) : new Color(0.92f, 0.92f, 0.92f);
            }
            else if (框.耐久.gameObject.activeSelf) { 框.耐久.gameObject.SetActive(false); 框.上次耐久 = null; }
        }
    }

    // 左上锚定定位：列/行 起点 + 宽×高 跨格（x 含 块偏移——块间 缝隙）
    private void 定位(RectTransform 矩形, int 列, int 行, int 宽, int 高)
    {
        矩形.anchorMin = new Vector2(0, 1);
        矩形.anchorMax = new Vector2(0, 1);
        矩形.pivot = new Vector2(0, 1);
        矩形.anchoredPosition = new Vector2(格x(列, 行), -行 * 格尺寸);
        矩形.sizeDelta = new Vector2(宽 * 格尺寸, 高 * 格尺寸);
    }

    // 品质底层色（物品框）：全部物品按有效品质整块着色（半透明）；普通 = 纯白 A20（微亮底）；优秀~传奇 = 品质色混合。
    // 架构约定：品质色永远属于"物品框层"（全尺寸底层）——将来内容层换成美术图片后，四周仍露出品质色环。
    private Color 品质底层色(物品堆叠 堆叠)
    {
        if (堆叠 == null || !数据.物品.TryGetValue(堆叠.标识, out var 物品)) return new Color(0f, 0f, 0f, 0f);
        品质 档 = 有效品质(堆叠, 物品);
        if (档 == 品质.普通) return new Color(0f, 0f, 0f, 0f);   // 普通：纯白 A20
        var 色 = Color.Lerp(网格面板配色.物品底色, 品质工具.颜色(档), 0.55f);
        色.a = 网格面板配色.品质底色透明;   // 半透明（能看到底座格/分隔线，品质色仍是区分度）
        return 色;
    }

    // 拖拽代理底色：非普通 = 品质底层色；普通（纯白 A20 太淡）→ 内容层色（不透明，跟手可见）
    private Color 物品品质底(物品堆叠 堆叠)
    {
        var 层色 = 品质底层色(堆叠);
        return 层色.a <= 0.1f ? 网格面板配色.物品底色 : 层色;
    }

    // 有效品质：堆叠品质覆盖（合成提升）优先，否则取物品模板品质
    private static 品质 有效品质(物品堆叠 堆叠, 物品数据 模板)
        => !string.IsNullOrEmpty(堆叠.品质) ? 数据解析.枚举<品质>(堆叠.品质) : 模板.品质档;
}
