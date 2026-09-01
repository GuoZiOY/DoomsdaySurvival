using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// 网格面板基类.渲染 —— 分部类：渲染框架（底格 / 分隔线 / 实体框表 / 增量刷新 / 定位 / 高光 / 挂接交互）。
// 与 网格面板基类.cs 同一 partial 类（共享字段与方法）；纯组织性拆分，无行为改动。
// ============================================================
public abstract partial class 网格面板基类
{
    // 刷新网格：尺寸/首建变化 → 全量重建（底座+线+实体框）；否则 → 增量刷新实体框（复用框，不重建底格线）。
    private void 刷新网格()
    {
        if (网格容器 == null) return;
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
            服务.网格列 = 有效列;
            服务.网格行 = 有效行;
        }
        if (当前列 != 有效列 || 当前行 != 有效行 || 底座层 == null || Mathf.Abs(上次格尺寸 - 格尺寸) > 0.01f)
        {
            当前列 = 有效列; 当前行 = 有效行;
            上次格尺寸 = 格尺寸;
            重建网格结构();
        }
        刷新物品();
    }

    private void 重建网格结构()
    {
        准备层();
        计算块偏移();
        var cf = 网格容器.GetComponent<ContentSizeFitter>();
        if (cf != null) cf.enabled = false;
        float 网格宽 = 当前列 * 格尺寸 + 最右偏移(), 网格高 = 当前行 * 格尺寸 + 最下偏移();
        if (数据源 != null)
            网格容器.sizeDelta = new Vector2(网格宽 + 网格面板配色.底盘外扩 * 2f, 网格高 + 网格面板配色.底盘外扩 * 2f);
        else
        {
            float 视口宽 = 网格容器.parent != null ? ((RectTransform)网格容器.parent).rect.width : 网格宽;
            网格容器.sizeDelta = new Vector2(Mathf.Max(视口宽, 网格宽), 网格高);
        }
        底座层.sizeDelta = new Vector2(网格宽, 网格高);
        线层.sizeDelta = new Vector2(网格宽, 网格高);
        物品层.sizeDelta = new Vector2(网格宽, 网格高);
        if (数据源 == null && 底盘 != null)
            底盘.sizeDelta = new Vector2(网格宽 + 网格面板配色.底盘外扩 * 2f, 网格高 + 网格面板配色.底盘外扩 * 2f);
        清空层(底座层); 清空层(线层); 清空层(物品层);
        for (int 行 = 0; 行 < 当前行; 行++)
            for (int 列 = 0; 列 < 当前列; 列++)
                创建底格(列, 行);
        画分隔线();
        更新布局快照();
        清空物品框表();
    }

    private void 清空物品框表()
    {
        foreach (var kv in 物品框表)
            if (kv.Value != null && kv.Value.根 != null) Destroy(kv.Value.根.gameObject);
        物品框表.Clear();
    }

    // 增量刷新实体框：遍历 服务.网格物品 —— 新增创建（钩子）/ 已有更新（钩子）/ 移除销毁
    private void 刷新物品()
    {
        if (物品层 == null) return;
        if (当前列 != 服务.网格列 || 当前行 != 服务.网格行 || Mathf.Abs(上次格尺寸 - 格尺寸) > 0.01f) { 刷新网格(); return; }
        bool 布局变化 = false;
        if (上次布局.Count != 服务.网格物品.Count) 布局变化 = true;
        else
        {
            for (int i = 0; i < 服务.网格物品.Count && !布局变化; i++)
            {
                var s = 服务.网格物品[i];
                if (s == null) continue;
                if (!上次布局.TryGetValue(s, out var 上次)) { 布局变化 = true; break; }
                if (上次.列 != s.列 || 上次.行 != s.行 || 上次.旋转 != s.旋转) 布局变化 = true;
            }
        }
        if (布局变化) { 重画分隔线(); 更新布局快照(); }
        foreach (var kv in 物品框表) kv.Value.存活 = false;
        foreach (var 堆叠 in 服务.网格物品)
        {
            if (堆叠 == null || 堆叠.列 < 0) continue;
            if (物品框表.TryGetValue(堆叠, out var 框)) { 框.存活 = true; 更新实体框(框, 堆叠); }
            else { var 新框 = 创建实体框(堆叠); if (新框 != null) { 新框.存活 = true; 物品框表[堆叠] = 新框; } }
        }
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

    protected void 更新布局快照()
    {
        上次布局.Clear();
        foreach (var s in 服务.网格物品)
            if (s != null) 上次布局[s] = (s.列, s.行, s.旋转);
    }

    private void 重画分隔线()
    {
        if (线层 == null) return;
        清空层(线层);
        画分隔线();
    }

    private void 准备层()
    {
        if (底座层 != null) return;
        创建底盘();
        底座层 = 创建网格层("底座层");
        线层 = 创建网格层("线层");
        物品层 = 创建网格层("物品层");
    }

    private void 创建底盘()
    {
        if (网格容器 == null || !string.IsNullOrEmpty(所属槽位)) return;
        var 物体 = new GameObject("底盘", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(网格容器, false);
        物体.transform.SetAsFirstSibling();
        var 图 = 物体.GetComponent<Image>();
        图.color = 网格面板配色.底盘色;
        图.raycastTarget = false;
        var 矩形 = 物体.GetComponent<RectTransform>();
        if (数据源 != null)
        {
            矩形.anchorMin = Vector2.zero;
            矩形.anchorMax = Vector2.one;
            矩形.offsetMin = Vector2.zero;
            矩形.offsetMax = Vector2.zero;
        }
        else
        {
            float 外扩 = 网格面板配色.底盘外扩;
            UI工具.设锚(矩形, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 外扩),
                new Vector2(当前列 * 格尺寸 + 最右偏移() + 外扩 * 2f, 当前行 * 格尺寸 + 最下偏移() + 外扩 * 2f));
        }
        底盘 = 矩形;
    }

    private RectTransform 创建网格层(string 名字)
    {
        var r = UI工具.创建物体(网格容器, 名字, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        r.anchoredPosition = new Vector2(0f, 数据源 != null ? -网格面板配色.底盘外扩 : 0f);
        r.sizeDelta = new Vector2(当前列 * 格尺寸 + 最右偏移(), 当前行 * 格尺寸 + 最下偏移());
        return r;
    }

    private void 创建底格(int 列, int 行)
    {
        if (!服务.该格可用(列, 行)) return;
        var 物体 = new GameObject($"底格_{行}_{列}", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(底座层, false);
        var 图 = 物体.GetComponent<Image>();
        图.sprite = 网格底层精灵();
        图.color = 块底格色(服务.该格块(列, 行));   // 按 块 着色（房间 之间 区别 明显）
        图.raycastTarget = false;
        定位(物体.GetComponent<RectTransform>(), 列, 行, 1, 1);
    }

    // 块 底格 色 钩子（基类 通用 = 底座 色；家具 户型 override：走廊 灰 + 房间 浅 色 表——子类 专属 视觉 不 污染 基类）
    protected virtual Color 块底格色(int 块) => 网格面板配色.底座色;

    private static Sprite 网格底层缓存;
    private static Sprite 网格底层精灵()
    {
        if (网格底层缓存 == null)
            网格底层缓存 = Resources.Load<Sprite>("Art/网格底层");
        return 网格底层缓存;
    }

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

    protected virtual void 画块网格线()
    {
        for (int 列 = 0; 列 <= 当前列; 列++)
            for (int 行 = 0; 行 < 当前行; 行++)
            {
                int 左块 = 列 > 0 ? 服务.该格块(列 - 1, 行) : -1;
                int 右块 = 列 < 当前列 ? 服务.该格块(列, 行) : -1;
                if (左块 < 0 && 右块 < 0) continue;
                if (左块 >= 0 && 右块 >= 0 && 左块 != 右块)
                {
                    画竖线段(列, 行, true, false, true, 块内有物品(左块));
                    画竖线段(列, 行, true, true, true, 块内有物品(右块));
                    continue;
                }
                if (左块 >= 0 && 右块 >= 0) { 画竖线段(列, 行, 竖线边界(列, 行)); continue; }
                画竖线段(列, 行, true, false, true, 块内有物品(左块 >= 0 ? 左块 : 右块));
            }
        for (int 行 = 0; 行 <= 当前行; 行++)
            for (int 列 = 0; 列 < 当前列; 列++)
            {
                int 上块 = 行 > 0 ? 服务.该格块(列, 行 - 1) : -1;
                int 下块 = 行 < 当前行 ? 服务.该格块(列, 行) : -1;
                if (上块 < 0 && 下块 < 0) continue;
                if (上块 >= 0 && 下块 >= 0 && 上块 != 下块) { 画横缝隙(列, 行); continue; }
                if (上块 >= 0 && 下块 >= 0) { 画横线段(列, 行, 横线边界(列, 行)); continue; }
                画横线段(列, 行, true, true, 块内有物品(上块 >= 0 ? 上块 : 下块));
            }
    }

    private bool 块内有物品(int 块索引)
    {
        var 块们 = 服务.形状块;
        if (块们 == null || 块索引 < 0 || 块索引 >= 块们.Count) return false;
        var 块 = 块们[块索引];
        foreach (var s in 服务.网格物品)
        {
            if (s == null || s.列 < 0) continue;
            var (宽, 高) = 服务.物品占格(s);
            if (s.列 < 块.列 + 块.宽 && s.列 + 宽 > 块.列 && s.行 < 块.行 + 块.高 && s.行 + 高 > 块.行) return true;
        }
        return false;
    }

    private void 画竖缝隙(int 列, int 行) { }
    // 上下 口袋 缝：上块 底边（参考行 = 行-1，y 到 底）+ 下块 顶边（参考行 = 行，y 到 顶）两条 横线（缝 留白）
    private void 画横缝隙(int 列, int 行)
    {
        int 上块 = 行 > 0 ? 服务.该格块(列, 行 - 1) : -1;
        int 下块 = 行 < 当前行 ? 服务.该格块(列, 行) : -1;
        画横线段(列, 行, true, true, 块内有物品(上块), 行 - 1);   // 上块 底边
        画横线段(列, 行, true, true, 块内有物品(下块), 行);       // 下块 顶边
    }

    private bool 竖线边界(int i, int j)
    {
        var 左格 = i > 0 ? 该格物品(i - 1, j) : null;
        var 右格 = i < 当前列 ? 该格物品(i, j) : null;
        if (左格 == null && 右格 == null) return false;
        if (左格 != null && 左格 == 右格) return false;
        return true;
    }

    private bool 横线边界(int i, int j)
    {
        var 上格 = j > 0 ? 该格物品(i, j - 1) : null;
        var 下格 = j < 当前行 ? 该格物品(i, j) : null;
        if (上格 == null && 下格 == null) return false;
        if (上格 != null && 上格 == 下格) return false;
        return true;
    }

    private void 画竖线段(int 列, int 行, bool 亮, bool 贴右 = false, bool 形状线 = false, bool 加粗 = false)
    {
        bool 左可用 = 列 > 0 && 服务.该格可用(列 - 1, 行);
        bool 右可用 = 列 < 当前列 && 服务.该格可用(列, 行);
        if (!左可用 && !右可用) return;
        float x = 贴右 ? 格x(列, 行) : (左可用 ? 格x(列 - 1, 行) + 格尺寸 : 格x(列, 行));
        var 物体 = new GameObject($"竖线_{列}_{行}", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(线层, false);
        var 图 = 物体.GetComponent<Image>();
        图.color = 形状线 ? 网格面板配色.形状边界色 : (亮 ? 网格面板配色.物品边界色 : 网格面板配色.线条色);
        图.raycastTarget = false;
        var 矩形 = 物体.GetComponent<RectTransform>();
        // y 用 有效 块 格（优先 左格；右格 空洞/越界 时 左格 才是 口袋 内——右边界 线 不 错位）
        int 有效列 = 左可用 ? 列 - 1 : 列;
        UI工具.设锚(矩形, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(x, -格y(有效列, 行) - 格尺寸 / 2f), new Vector2(线宽判定(亮, 形状线, 加粗), 格尺寸));
    }

    private void 画横线段(int 列, int 行, bool 亮, bool 形状线 = false, bool 加粗 = false, int 参考行覆盖 = -1)
    {
        bool 上可用 = 行 > 0 && 服务.该格可用(列, 行 - 1);
        bool 下可用 = 行 < 当前行 && 服务.该格可用(列, 行);
        if (!上可用 && !下可用)
            return;
        // 参考行：默认 = 下格 有效 ? 行 : 行-1（口袋 底边 下格 空洞/越界 时 用 上格——边界 线 不 错位）；缝隙 调用 覆盖
        int 参考行 = 参考行覆盖 >= 0 ? 参考行覆盖 : (下可用 ? 行 : 行 - 1);
        var 物体 = new GameObject($"横线_{行}_{列}", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(线层, false);
        var 图 = 物体.GetComponent<Image>();
        图.color = 形状线 ? 网格面板配色.形状边界色 : (亮 ? 网格面板配色.物品边界色 : 网格面板配色.线条色);
        图.raycastTarget = false;
        var 矩形 = 物体.GetComponent<RectTransform>();
        // y = 参考行 顶部（含 块 y 偏移）；参考行 < 行（上块 底边）时 再 + 格尺寸 到 底部
        float y = -格y(列, 参考行) - (参考行 < 行 ? 格尺寸 : 0f);
        UI工具.设锚(矩形, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(格x(列, 参考行) + 格尺寸 / 2f, y), new Vector2(格尺寸, 线宽判定(亮, 形状线, 加粗)));
    }

    private static float 线宽判定(bool 亮, bool 形状线, bool 加粗)
        => ((亮 && !形状线) || 加粗) ? 网格面板配色.物品边界线宽 : 网格面板配色.线宽;

    // 该格被哪个实体覆盖（领域判定，UI 只查询不计算）
    protected 物品堆叠 该格物品(int 列, int 行) => 服务.该格物品(列, 行);

    // 左上锚定定位：列/行 起点 + 宽×高 跨格（x/y 均 含 块偏移——左右/上下 口袋 缝）
    protected void 定位(RectTransform 矩形, int 列, int 行, int 宽, int 高)
    {
        矩形.anchorMin = new Vector2(0, 1);
        矩形.anchorMax = new Vector2(0, 1);
        矩形.pivot = new Vector2(0, 1);
        矩形.anchoredPosition = new Vector2(格x(列, 行), -格y(列, 行));
        矩形.sizeDelta = new Vector2(宽 * 格尺寸, 高 * 格尺寸);
    }

    // 格 (列,行) 的 视觉 x：列×格尺寸 + 所属块的 x 偏移（右缝）
    protected float 格x(int 列, int 行)
    {
        if (块偏移 == null) return 列 * 格尺寸;
        int 块 = 服务.该格块(列, 行);
        return 列 * 格尺寸 + (块 >= 0 && 块 < 块偏移.Length ? 块偏移[块].x : 0f);
    }

    // 格 (列,行) 的 视觉 y：行×格尺寸 + 所属块的 y 偏移（下缝）
    protected float 格y(int 列, int 行)
    {
        if (块偏移 == null) return 行 * 格尺寸;
        int 块 = 服务.该格块(列, 行);
        return 行 * 格尺寸 + (块 >= 0 && 块 < 块偏移.Length ? 块偏移[块].y : 0f);
    }

    private void 计算块偏移()
    {
        块偏移 = null;
        var 块们 = 服务.形状块;
        if (块们 == null || 块们.Count == 0) return;
        块偏移 = new Vector2[块们.Count];
        for (int i = 0; i < 块们.Count; i++)
            for (int j = 0; j < 块们.Count; j++)
            {
                if (i == j) continue;
                var A = 块们[j]; var B = 块们[i];
                // 左右 缝：B 在 A 正右侧 且 行 重叠 → B 向右 让 缝隙
                if (A.列 + A.宽 == B.列 && A.行 < B.行 + B.高 && B.行 < A.行 + A.高)
                    块偏移[i].x = Mathf.Max(块偏移[i].x, 块偏移[j].x + 块缝隙宽);
                // 上下 缝：B 在 A 正下方 且 列 重叠 → B 向下 让 缝隙
                if (A.行 + A.高 == B.行 && A.列 < B.列 + B.宽 && B.列 < A.列 + A.宽)
                    块偏移[i].y = Mathf.Max(块偏移[i].y, 块偏移[j].y + 块缝隙宽);
            }
    }

    private float 最右偏移()
    {
        if (块偏移 == null || 服务.形状块 == null || 服务.形状块.Count == 0) return 0f;
        float 最右 = 0f;
        for (int i = 0; i < 服务.形状块.Count; i++)
            if (服务.形状块[i].列 + 服务.形状块[i].宽 == 当前列)
                最右 = Mathf.Max(最右, 块偏移[i].x);
        return 最右;
    }

    // 最下偏移（对称 最右偏移）：底部 贴 网格 下边 的 块 的 y 偏移（上下 口袋 缝——网格 高度 含 下排 偏移）
    private float 最下偏移()
    {
        if (块偏移 == null || 服务.形状块 == null || 服务.形状块.Count == 0) return 0f;
        float 最下 = 0f;
        for (int i = 0; i < 服务.形状块.Count; i++)
            if (服务.形状块[i].行 + 服务.形状块[i].高 == 当前行)
                最下 = Mathf.Max(最下, 块偏移[i].y);
        return 最下;
    }

    protected void 清空层(RectTransform 层)
    {
        if (层 == null) return;
        for (int i = 层.childCount - 1; i >= 0; i--)
        {
            var 子 = 层.GetChild(i);
            if (子 != null) { 子.SetParent(null); Destroy(子.gameObject); }
        }
    }

    // ② 高光层：底色 与 内容 之间（白色半透明，悬停时显示；覆盖到 网格线 上）
    protected static GameObject 创建高光层(Transform 父)
    {
        var 高物体 = new GameObject("高光", typeof(RectTransform), typeof(Image));
        高物体.transform.SetParent(父, false);
        var 高图 = 高物体.GetComponent<Image>();
        高图.color = 网格面板配色.高光色;
        高图.raycastTarget = false;
        var 高矩 = 高物体.GetComponent<RectTransform>();
        高矩.anchorMin = Vector2.zero;
        高矩.anchorMax = Vector2.one;
        高矩.offsetMin = new Vector2(-网格面板配色.线宽 / 2f, -网格面板配色.线宽 / 2f);
        高矩.offsetMax = new Vector2(网格面板配色.线宽 / 2f, 网格面板配色.线宽 / 2f);
        高物体.SetActive(false);
        return 高物体;
    }

    // ④ 点击（非按钮） + 拖拽（挂在实体框上）
    protected void 挂接交互(物品框 框, 物品堆叠 堆叠, RectTransform 内容矩形)
    {
        var 点击 = 框.根.gameObject.AddComponent<物品点击>();
        点击.堆叠 = 堆叠;
        点击.面板 = this;
        点击.物品框 = 框.根;
        点击.内容层 = 内容矩形;
        点击.高光层 = 框.高光层;
        var 拖拽 = 框.根.gameObject.AddComponent<物品拖拽>();
        拖拽.堆叠 = 堆叠;
        拖拽.面板 = this;
    }

    // 悬停：内容图放大 1.05 + 高光层显示（替代选中底色）
    protected void 悬停(RectTransform 内容层, GameObject 高光层, bool 进入)
    {
        if (内容层 != null) 内容层.localScale = 进入 ? new Vector3(1.05f, 1.05f, 1f) : Vector3.one;
        if (高光层 != null) 高光层.SetActive(进入);
    }
}
