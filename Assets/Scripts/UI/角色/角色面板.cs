using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 角色面板 —— **壳**（从零重建的第一刀，本批只立骨架）。
// 职责只有两件：
//   ① 切页：三颗 Tab（下标 0/1/2 = 属性/技能/知识）↔ 三个页的 `SetActive`；
//   ② 被打开/关闭：沿用 `面板基类` 与 `面板管理器` 的既有出口协议（`显示面板` 进 / `回退()` 出），不自造一套。
//
// 为什么页是 `GameObject` 而不是子面板组件类型：三个子面板（属性/知识/技能）**下一批才建**。
//   现在用 `GameObject` 只表达"哪一块亮着"；等子面板到位，改本文件的数组类型 + 那一处 `SetActive` 即可
//   —— 壳与页的内容始终互不认识（页自己负责自己的刷新，壳不管）。
//
// 分工（本批定的硬边界，别越界）：
//   · 本组件管：Tab 点击 → 切页；栏目标题的文字（接了才写）；被打开时把当前页重新亮一次。
//   · **外观 100% 归场景 / 预制体**：本文件不写任何颜色、字号、尺寸、进度数值
//     （用户 2026-09-17 定调："那些色号、字号不要 —— 我在 Unity 里自己调外观"）。自查：0 处。
//   · 本批**不做**：身份条 / 经验条 / 经验格 / 板子 / 遮罩 / 下划线 / Tab 上的文字 / Tab 选中高亮 / 过渡动画。
//
// 兜底顺序（引用位缺了就按这个顺序找，**不建节点**）：
//   Inspector 引用 → 按节点名 `transform.Find` 兜底 → 仍找不到 → `报缺引用()` 打**一条** LogError 点名。
//   **为什么不建节点**：上一代（刀80~87）是"缺什么就运行期补建一个空节点"，结果是面板看起来能开、
//   实际全是没样式的空壳，排查很久才发现节点不是自己搭的那一套。这一批改口径：**缺什么就说什么**，
//   结构仍由用户在 Unity 里搭 —— 缺引用只报错，不崩、也不中断别的面板。
//
// 场景里的节点约定（`transform.Find` 兜底用的名字；Inspector 接上引用位就与名字无关）：
//   角色面板（满屏 RectTransform；由 面板管理器 收藏/显示，见 `面板管理器.角色`）
//     ├─ 属性页 / 技能页 / 知识页   ← 三个页（直接子节点）
//     ├─ 属性 / 技能 / 知识         ← 三颗 Tab 按钮（直接子节点；名字 = `栏目表[i]`）
//     └─ 栏目标题                   ← **可空**：TMP 文本，切页时跟着 `栏目表` 走
public sealed class 角色面板 : 面板基类
{
    // 三个栏目：顺序 = Tab 下标 = 页下标（属性/技能/知识）。
    // 文字只用来写 `栏目标题`（不参与显隐判断）→ 改字**不会**改变切页行为。
    [SerializeField] private string[] 栏目表 = { "属性", "技能", "知识" };

    // 三颗 Tab 按钮（下标 0/1/2 ↔ 属性/技能/知识）：点击 = 切页
    [SerializeField] private Button[] Tab按钮 = new Button[3];

    // 三个页（本批是 `GameObject`，理由见文件头）：切页 = `页[j].SetActive(j == 当前栏)`
    [SerializeField] private GameObject[] 页 = new GameObject[3];

    // 栏目标题（**可空**）：接了才写文字，没接就跳过 —— 标题是加分项，不是面板能开的前提
    [SerializeField] private TMP_Text 栏目标题;

    // 页的节点名（`页` 的引用位没接时，按这些名字在 角色面板 下兜底找）
    private static readonly string[] 页节点名 = { "属性页", "技能页", "知识页" };

    private int 当前栏;

    void Awake()
    {
        备数组(ref Tab按钮);
        备数组(ref 页);
        自找();          // Inspector → 节点名 兜底（只找不建）
        接Tab();
        切换栏(0);       // 默认落在属性页
        报缺引用();      // 找完再点名，报的是"两边都没找到"的那些位
    }

    // ================= 切页 =================

    // 切页：只做"哪一块亮着"这一件事 —— 不碰颜色 / 字号 / 尺寸 / 选中态（全归场景）。
    private void 切换栏(int i)
    {
        当前栏 = Mathf.Clamp(i, 0, 页.Length - 1);
        for (int j = 0; j < 页.Length; j++)
        {
            var 块 = 取(页, j);
            if (块 != null && 块.activeSelf != (j == 当前栏)) 块.SetActive(j == 当前栏);
        }
        // 栏目标题 可空：没接线时 `设文本` 自己判空跳过（本组件不因为"没有标题"而报错）
        面板基类.设文本(栏目标题, 栏目(当前栏));
    }

    // `面板基类.显示面板()` 在 `SetActive(true)` 之后调它。
    // 壳**只做一件事**：把"当前是哪一栏"重新应用一次（页的显隐兜底）。
    // 页**里面的内容**归页自己（子面板那批各自在 OnEnable / 事件订阅里刷），壳不管。
    protected override void 刷新(object 上下文) => 切换栏(当前栏);

    // Tab 的点击：**只接点击** —— 底色 / 悬停 / 按下 / 过渡一律沿用场景里调好的按钮设置（在 Unity 里改得动）。
    private void 接Tab()
    {
        for (int i = 0; i < Tab按钮.Length; i++)
        {
            int 序 = i;   // 闭包捕获：循环变量必须另存一份，否则三颗按钮全会切到最后一栏
            var 钮 = 取(Tab按钮, i);
            if (钮 == null) continue;
            钮.onClick.RemoveAllListeners();   // 本组件接管这三颗按钮的点击（物体被重新激活时会再进 Awake）
            钮.onClick.AddListener(() => 切换栏(序));
        }
    }

    // ================= 出口协议（沿用 面板管理器 / HUD 关闭按钮 的既有约定） =================

    // HUD 上那颗统一关闭按钮读这个文案（"关闭" = 把这一页收掉，不是"离开某个地方"）。
    // 覆写它 = 声明"本面板有可执行的退出口"（`面板基类.可关闭` 正是这么判的）→ 必须让下面的 `回退()` 真能退出去。
    public override string 取消文本 => "关闭";

    // 关闭 = 回上一面板（没有上一面板时返回 false，由调用方决定要不要播错误音效）。
    // 项目**没有全局 Esc 回退键**（用户拍板），所以这里也不绑 Esc。
    public override bool 回退()
    {
        if (面板管理器.实例?.上一个面板 == null) return false;
        面板管理器.实例.返回上一面板();
        return true;
    }

    // ================= 引用兜底：按节点名找回来（只找，不建） =================

    // 名字来源：Tab = `栏目表[i]`；页 = `页节点名[i]`。都在**直接子节点**里找（场景约定见文件头）。
    private void 自找()
    {
        for (int i = 0; i < 页节点名.Length; i++)
        {
            if (取(Tab按钮, i) == null && !string.IsNullOrEmpty(栏目(i)))
            {
                var 节点 = transform.Find(栏目(i));
                if (节点 != null) Tab按钮[i] = 节点.GetComponent<Button>();
            }
            if (取(页, i) == null)
            {
                var 节点 = transform.Find(页节点名[i]);
                if (节点 != null) 页[i] = 节点.gameObject;
            }
        }
    }

    // 缺引用**要出声**：Inspector 少接一个位 → 表现只是"某一块永远不亮 / 点了没反应"，一条日志都没有。
    // 打**一条** LogError 把缺的名字列全（不崩、不中断）。
    private void 报缺引用()
    {
        var 缺 = new List<string>();
        for (int i = 0; i < 页节点名.Length; i++)
        {
            if (取(Tab按钮, i) == null) 缺.Add($"Tab按钮[{i}]（节点名「{栏目(i)}」）");
            if (取(页, i) == null) 缺.Add($"页[{i}]（节点名「{页节点名[i]}」）");
        }
        if (缺.Count == 0) return;
        Debug.LogError("[角色面板] 缺引用：" + string.Join("、", 缺) +
                       "。请在场景里把这些节点拖到本组件的 Inspector 引用位上" +
                       "（或让节点名与括号里的一致、直接挂在 角色面板 下）。" +
                       "外观在场景 / 预制体里调，本组件不建节点。");
    }

    // ================= 小工具 =================

    private string 栏目(int i) => 取(栏目表, i) ?? "";

    // 取数组第 序 个（越界或没接都返回 null → 调用方一句判空即可）
    private static T 取<T>(T[] 数组, int 序) where T : class
        => 数组 != null && 序 >= 0 && 序 < 数组.Length ? 数组[序] : null;

    // 数组可能是 null / 短数组（老场景里存下来的）→ 先补到 3 个，否则按 3 个下标遍历就会越界。
    // ⚠ 短数组里**已有的引用要带过来**：直接 `数组 = new T[3]` 会把用户已经拖好的那两颗按钮丢掉。
    private static void 备数组<T>(ref T[] 数组) where T : class
    {
        if (数组 != null && 数组.Length == 3) return;
        var 旧 = 数组;
        var 新 = new T[3];
        if (旧 != null)
            for (int i = 0; i < 旧.Length && i < 新.Length; i++) 新[i] = 旧[i];
        数组 = 新;
    }
}
