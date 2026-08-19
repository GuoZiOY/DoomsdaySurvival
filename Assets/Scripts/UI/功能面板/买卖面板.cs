using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 买卖面板：买卖功能（商店/武馆/学堂共用——商品与收购筛选由各设施决定）。
// 购买/卖出 共用同一块区域 + 同一个滚动列表：Tab 点击只切换内容（商品行 ↔ 卖出行）。
// 排序 4 键：默认 / 价格 / 品质 / 名称——价格/品质/名称点击切换方向（箭头 ↑↓ 换图），再点同键反转方向。
// 详情区：点击行选中 → 完整信息 + 操作按钮（买入/卖出）。金币余额实时刷新。
public sealed class 买卖面板 : 设施功能面板基类
{
    // —— 标题栏 / Tab ——
    [SerializeField] private TMP_Text 金币余额;
    [SerializeField] private Button 购买Tab, 卖出Tab;

    // —— 共用列表（购买/卖出 同一个滚动列表 Content）——
    [SerializeField] private RectTransform 列表容器;
    [SerializeField] private GameObject 商品行模板, 卖出行模板;
    [SerializeField] private TMP_Text 空态;               // 共用空态提示

    // —— 排序（4 键：默认/价格/品质/名称；后三键带箭头 ↑↓）——
    [SerializeField] private Button[] 排序按钮;           // [默认, 价格, 品质, 名称]
    [SerializeField] private Image[] 排序箭头;            // [价格, 品质, 名称] 箭头（默认无）
    [SerializeField] private Sprite 箭头升, 箭头降;

    // —— 详情区 ——
    [SerializeField] private TMP_Text 详情名称, 详情描述, 详情数值, 详情价格;   // 名称含品质标签
    [SerializeField] private Button 详情操作按钮;
    [SerializeField] private TMP_Text 详情操作文字;

    private enum 排序列 { 默认, 价格, 品质, 名称 }
    private 排序列 当前排序 = 排序列.默认;
    private bool 价格降序, 品质降序 = true, 名称降序;   // 品质默认降序（传奇在前），价格/名称默认升序

    private 买卖功能 买卖 => 逻辑 as 买卖功能;
    private bool 显示购买 = true;
    private string 选中标识;    // 选中物品标识（null=未选中）
    private bool 选中是购买;    // 详情区操作：true=买入 / false=卖出

    protected override string 标题文字() => $"{逻辑.名称} · 买卖";

    protected override void Awake()
    {
        base.Awake();
        购买Tab?.onClick.AddListener(() => 切换Tab(true));
        卖出Tab?.onClick.AddListener(() => 切换Tab(false));
        // 排序 4 键（索引 = 排序列枚举）
        for (int i = 0; i < (排序按钮?.Length ?? 0) && i < 4; i++)
        {
            var 列 = (排序列)i;
            var 按钮 = 排序按钮[i];
            if (按钮 != null) 按钮.onClick.AddListener(() => 点击排序(列));
        }
        // 背包变化（买入入包/卖出扣物）→ 重建当前列表（卖出列表随背包实时变）
        ServiceRegistry.Get<EventBus>()?.订阅<背包变化事件>(_ => 渲染列表());
        详情操作按钮?.onClick.AddListener(执行操作);
    }

    // 金币变化：只刷新余额文本，不整面板重绘（保持列表滚动位置）
    protected override void 金币变化() => 更新余额();

    protected override void 刷新(object 上下文)
    {
        base.刷新(上下文);
        更新余额();
    }

    protected override void 渲染内容()
    {
        if (买卖 == null) return;
        // 未接线新布局 → 回退旧列表样式
        if (商品行模板 == null || 列表容器 == null) { 旧样式(); return; }
        切换Tab(显示购买);
    }

    private void 更新余额()
    {
        if (金币余额 != null && 逻辑 != null) 金币余额.text = $"持有 {货币工具.文本(逻辑.当前玩家.铜币)}";
    }

    // Tab 切换：购买/卖出 共用区域与列表，只换内容；选中态=固定放大（替代颜色高亮）
    private void 切换Tab(bool 购买)
    {
        显示购买 = 购买;
        渲染列表();
        刷新详情();
        设选中缩放(购买Tab, 购买);
        设选中缩放(卖出Tab, !购买);
    }

    // ===== 排序（4 键） =====

    private void 点击排序(排序列 列)
    {
        if (列 == 排序列.默认) 当前排序 = 排序列.默认;
        else if (当前排序 == 列) 切换方向(列);   // 同键 → 反转方向（箭头换图）
        else 当前排序 = 列;                      // 换键 → 用该键当前方向
        渲染列表();
    }

    private void 切换方向(排序列 列)
    {
        switch (列)
        {
            case 排序列.价格: 价格降序 = !价格降序; break;
            case 排序列.品质: 品质降序 = !品质降序; break;
            case 排序列.名称: 名称降序 = !名称降序; break;
        }
    }

    private void 刷新排序高亮()
    {
        for (int i = 0; i < (排序按钮?.Length ?? 0) && i < 4; i++)
            设选中缩放(排序按钮[i], (排序列)i == 当前排序);
        // 箭头显示各键当前方向（非激活键也可见，提示默认方向）
        if (排序箭头 != null)
        {
            if (排序箭头.Length > 0 && 排序箭头[0] != null) 排序箭头[0].sprite = 价格降序 ? 箭头降 : 箭头升;
            if (排序箭头.Length > 1 && 排序箭头[1] != null) 排序箭头[1].sprite = 品质降序 ? 箭头降 : 箭头升;
            if (排序箭头.Length > 2 && 排序箭头[2] != null) 排序箭头[2].sprite = 名称降序 ? 箭头降 : 箭头升;
        }
    }

    // ===== 共用列表 =====

    private void 渲染列表()
    {
        if (列表容器 == null || 买卖 == null) return;
        面板基类.清空(列表容器);
        if (显示购买) 渲染商品行();
        else 渲染卖出行();
        刷新排序高亮();
    }

    private void 渲染商品行()
    {
        var 商品 = new List<物品数据>(买卖.可购商品());
        商品.Sort(商品比较);
        foreach (var 物品 in 商品)
        {
            var 行 = 面板基类.创建模板<商品行>(列表容器, 商品行模板);
            if (行 == null) continue;
            if (行.名称 != null) 行.名称.text = 物品工具.品质名称(物品.品质档, 物品.名称);
            if (行.价格 != null) 行.价格.text = 货币工具.文本(物品.价格);
            if (行.选中图 != null) 行.选中图.gameObject.SetActive(选中标识 == 物品.标识 && 选中是购买);
            var 标识 = 物品.标识;
            挂行点击(行.GetComponent<Button>(), () => 点击行(标识, true));
        }
        if (空态 != null) 空态.gameObject.SetActive(商品.Count == 0);
    }

    private void 渲染卖出行()
    {
        var 数据 = ServiceRegistry.Get<DataService>();
        var 可卖 = new List<物品堆叠>(买卖.可卖物品());
        可卖.Sort(卖出比较(数据));
        foreach (var 堆叠 in 可卖)
        {
            if (!数据.物品.TryGetValue(堆叠.标识, out var 物品)) continue;
            var 行 = 面板基类.创建模板<卖出行>(列表容器, 卖出行模板);
            if (行 == null) continue;
            if (行.名称 != null) 行.名称.text = 物品工具.品质名称(物品.品质档, 物品.名称);
            if (行.数量 != null) 行.数量.text = 堆叠.数量 > 1 ? $"×{堆叠.数量}" : "";
            if (行.选中图 != null) 行.选中图.gameObject.SetActive(选中标识 == 堆叠.标识 && !选中是购买);
            var 标识 = 堆叠.标识;
            挂行点击(行.GetComponent<Button>(), () => 点击行(标识, false));
        }
        if (空态 != null) 空态.gameObject.SetActive(可卖.Count == 0);
    }

    private static void 挂行点击(Button 按钮, System.Action 点击)
    {
        if (按钮 == null) return;
        按钮.onClick.AddListener(() => 点击());
        音效管理器.实例?.注册按钮(按钮);   // 动态行按钮成功音效
    }

    private void 点击行(string 标识, bool 是购买)
    {
        选中标识 = 标识;
        选中是购买 = 是购买;
        渲染列表();
        刷新详情();
    }

    // ===== 排序比较 =====

    private int 商品比较(物品数据 a, 物品数据 b)
    {
        switch (当前排序)
        {
            case 排序列.价格: return 价格降序 ? b.价格.CompareTo(a.价格) : a.价格.CompareTo(b.价格);
            case 排序列.品质: return 品质降序 ? b.品质档.CompareTo(a.品质档) : a.品质档.CompareTo(b.品质档);
            case 排序列.名称: return 名称降序 ? string.CompareOrdinal(b.名称, a.名称) : string.CompareOrdinal(a.名称, b.名称);
            default: return 0;   // 默认：数据顺序（设施筛选顺序）
        }
    }

    private System.Comparison<物品堆叠> 卖出比较(DataService 数据)
    {
        return (a, b) =>
        {
            int 价A = Mathf.Max(1, 数据.物品.TryGetValue(a.标识, out var 物品A) ? 物品A.价格 / 2 : 0);
            int 价B = Mathf.Max(1, 数据.物品.TryGetValue(b.标识, out var 物品B) ? 物品B.价格 / 2 : 0);
            switch (当前排序)
            {
                case 排序列.价格: return 价格降序 ? 价B.CompareTo(价A) : 价A.CompareTo(价B);
                case 排序列.品质: return 品质降序 ? 品质档(数据, b.标识).CompareTo(品质档(数据, a.标识)) : 品质档(数据, a.标识).CompareTo(品质档(数据, b.标识));
                case 排序列.名称: return 名称降序 ? string.CompareOrdinal(名称(数据, b.标识), 名称(数据, a.标识)) : string.CompareOrdinal(名称(数据, a.标识), 名称(数据, b.标识));
                default: return 0;
            }
        };
    }

    private static 品质 品质档(DataService 数据, string 标识)
        => 数据.物品.TryGetValue(标识, out var 物品) ? 物品.品质档 : 品质.普通;

    private static string 名称(DataService 数据, string 标识)
        => 数据.物品.TryGetValue(标识, out var 物品) ? 物品.名称 : 标识;

    // ===== 详情区 =====

    private void 刷新详情()
    {
        var 数据 = ServiceRegistry.Get<DataService>();
        // 规范写法：out 变量在 if 条件末尾
        if (string.IsNullOrEmpty(选中标识) || !数据.物品.TryGetValue(选中标识, out var 物品))
        {
            显示未选中();
            return;
        }
        var 玩家 = 逻辑?.当前玩家;
        // 卖出选中：物品可能已被卖光 → 视为未选中
        bool 有效 = 选中是购买 || (玩家 != null && 玩家.背包.Exists(s => s.标识 == 选中标识));
        if (!有效)
        {
            显示未选中();
            return;
        }
        面板基类.设文本(详情名称, 物品工具.品质名称(物品.品质档, 物品.名称));   // 品质+名称 合并一个文本
        面板基类.设文本(详情描述, 物品.描述);
        面板基类.设文本(详情数值, 物品工具.数值文本(数据, 物品));
        面板基类.设文本(详情价格, 物品.价格 > 0 ? $"价格 {货币工具.文本(物品.价格)}" : "");
        if (详情操作按钮 != null)
        {
            详情操作按钮.gameObject.SetActive(true);
            面板基类.设文本(详情操作文字, 选中是购买 ? "买入" : "卖出");
        }
    }

    private void 显示未选中()
    {
        选中标识 = null;
        面板基类.设文本(详情名称, "—— 未选中 ——");
        面板基类.设文本(详情描述, "点击左侧货架选择物品。");
        面板基类.设文本(详情数值, "");
        面板基类.设文本(详情价格, "");
        if (详情操作按钮 != null) 详情操作按钮.gameObject.SetActive(false);
    }

    // 详情区操作按钮：买入 / 卖出
    private void 执行操作()
    {
        if (string.IsNullOrEmpty(选中标识) || 买卖 == null) return;
        if (选中是购买) 买卖.尝试购买(选中标识);
        else 买卖.尝试卖出(选中标识);
        刷新详情();   // 卖出后物品可能卖光 → 清除选中态
    }

    // ===== 旧样式回退（场景未接入新布局时保持可玩） =====

    private void 旧样式()
    {
        清空(内容区);
        foreach (var 物品 in 买卖.可购商品())
        {
            var 标识 = 物品.标识;
            创建行(内容区, $"【买】{品质工具.标签(物品.品质档)}{物品.名称}（{物品.描述}）  {货币工具.文本(物品.价格)}", () => 买卖.尝试购买(标识));
        }
        var 数据 = ServiceRegistry.Get<DataService>();
        foreach (var 堆叠 in 买卖.可卖物品())
        {
            if (!数据.物品.TryGetValue(堆叠.标识, out var 物品)) continue;
            int 价 = Mathf.Max(1, 物品.价格 / 2);
            var 标识 = 堆叠.标识;
            创建行(内容区, $"【卖】{物品.名称} ×{堆叠.数量}  {货币工具.文本(价)}/个", () => 买卖.尝试卖出(标识));
        }
    }
}
