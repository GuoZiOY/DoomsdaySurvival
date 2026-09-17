using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 角色面板：全局面板。一屏**三栏** = 一块浮在安全屋画面上的"幸存者档案板"
//   （板内：顶部身份条 + 顶部横排三个大字 Tab + 内容区；三个栏页的内容各自在子面板里）。
//
// ★ 本文件是**第五次改写**。前四代的弯路记在这里，别再翻 git：
//     ① 刀78  代码自建 + "不铺满屏 / 组件放大 / 暖调近黑 + 唯一暗锈红" —— 用户认了这个观感；
//     ② 刀79  整批拆掉自建，改成"用户自己搭 + Inspector 接线"；用户随后改主意；
//     ③ 刀80  自建回来，12 个结构引用保留为"可选覆盖"；
//     ④ 刀81  **"皮肤"这个概念整个删掉**（`角色面板皮肤.cs` 连 meta 一起没了）+ 四栏压成三栏
//             （属性 / 技能 / 知识 —— 天赋内容并入 属性页 当第 4 个小段）；
//     ⑤ 本批  **运行时不再建任何节点**。结构 100% 以 `Assets/Resources/Prefab/角色面板.prefab` 为准。
//
// 为什么⑤走这个方向（前四代自建踩到的两个真问题）：
//   · 自建 = 每次 `Awake` 先"清掉自己建的那一套"（把账本上那 177 个节点全销毁）再重建
//     → **在 Unity 里手调好的任何东西，一进 Play 就被冲掉**；连"自己往板子里加一个节点"都会被
//       当成"你拖进来的"拎到板子外面去（因为它不在账本上）；
//   · 自建代码与预制体是**两份真相**：改一处忘一处，表现就是"面板上少一块、还查不出为什么"。
//
// 所以现在的分工是一条硬边界：
//   **预制体管**：结构 / 尺寸 / 锚点 / 字号 / 静态颜色 / ColorBlock / 静态文案
//                （= 你在 Unity 里随便调，本组件一个字节都不碰）；
//   **本组件管**：只写"随状态变的东西" —— 身份行与等级的文字、经验格的亮灭、Tab 的选中色与下划线显隐、
//                栏目标题的显隐切换，以及**切到哪一页让哪一页自己刷**。
//
// ⚠ 由此带来的两条约定（改这个面板前先读）：
//   ① **要加/改节点，去 Unity 里改预制体** —— 代码不会再帮你补。缺了谁，`Awake` 会打一条
//      列清名字的 LogError（不静默）。
//   ② **节点名就是契约**：`自找()` 按 `板子/身份条/身份文本` 这类路径把引用兜底找回来。
//      重命名节点要同步改这里（或继续用 Inspector 引用位 —— 预制体里本来就都接好了，
//      兜底只是给"手搭一半 / 场景里现搭"的场景用的）。
//   ③ 预制体的整备脚本在 `.workbuddy/分析/角色面板-prefab整备.py`（补节点 / 改尺寸都走它，
//      免得手改 YAML 出静默错误）。
//
// 现成的节点树（Play 里对着 Hierarchy 核；缩进 = 父子）：
//   角色面板（满屏 —— `面板管理器` 建/摆面板的前提；预制体已烘成拉伸锚）
//     ├─ 点击遮罩            满屏透明 Image（raycastTarget = true：**吃掉板子外面的点击**；垫在最底层）
//     ├─ 遮罩层              满屏 Image（65% 黑，raycastTarget = false：只负责把画面压暗）
//     └─ 板子                1400×980（居中定尺；四边 2px 边框）
//          ├─ 身份条         内容底 + 下沿 2px 强调线
//          │    ├─ 身份文本   "幸存者 · 职业"
//          │    ├─ 等级文本   "Lv.N"
//          │    └─ 经验格父   下面铺着 10 个 10×10 经验格（间隔 2）
//          ├─ 属性 / 技能 / 知识    三个 Tab（底图 + 标签 + 2px 强调下划线）
//          ├─ 分隔线         边框色 2px，左右缩 页边距
//          └─ 内容区         内容底
//               ├─ 栏目标题  随 Tab 变
//               ├─ 标题下划线 强调色 2px（宽 = 标题文字宽）
//               └─ 属性页 / 技能页 / 知识页   三个子面板（切栏 = SetActive）
//
// 口径（外观）：**不铺满屏**（板 1400×980。参考分辨率 2560×1440 下约 55% × 68%，四周留着画面）·
//   板外 65% 黑遮罩（不挡点击）+ 满屏透明图（挡点击）· 板内留白 40 · 2px 边框/分隔线 ·
//   实心方块 + 文字，**没有**圆角 / 阴影 / 发光 / 渐变 / emoji / 入场动画 / **缩放反馈**（用户明令）。
//
// 开合沿用 `面板管理器` 的既有约定，不自造一套：
//   · 打开 = `打开角色面板事件`（或 C 键）→ `面板管理器.显示(角色)`；
//   · 关闭 = HUD 上那颗统一关闭按钮 → 调本面板的 `回退()`；
//   · 项目**没有全局 Esc 回退键**，所以这里也不绑 Esc；
//   · 板外那层透明图**只吃点击、不关面板**（关闭只有一个入口 = HUD 那颗按钮）。
public sealed class 角色面板 : 面板基类
{
    // 三个栏目（顺序 = 顶部 Tab 从左到右，也就是下面三个数组与 `子面板` 的下标 0..2）。
    // **没有"装备"栏**（装备归 `持有面板`，这里不重复一份）；
    // **没有"天赋"栏**（天赋内容并进 属性页 当第 4 个小段，见 `属性子面板`）。
    // 这三个字同时是**节点名的一半**（`板子/属性`、`板子/属性/标签`…）→ 改字要连预制体一起改。
    private static readonly string[] 栏目表 = { "属性", "技能", "知识" };

    // ================= Inspector：结构引用（预制体里已全部接好） =================
    [Header("结构引用（预制体已接好；留空 = 按名字兜底找回来，见 自找）")]
    [SerializeField] private TMP_Text 身份文本;           // "幸存者 · 职业"（板子/身份条/身份文本）
    [SerializeField] private TMP_Text 等级文本;           // "Lv.N"（板子/身份条/等级文本）
    [SerializeField] private RectTransform 经验格父;       // 板子/身份条/经验格父：子节点就是那一排经验格
    [SerializeField] private Button[] Tab按钮 = new Button[3];      // 板子/{栏}（下标 = 栏目顺序）
    [SerializeField] private TMP_Text[] Tab标签 = new TMP_Text[3];  // 板子/{栏}/标签（选中/未选中只改它的颜色）
    [SerializeField] private Image[] Tab下划线 = new Image[3];      // 板子/{栏}/下划线（只显示选中那条）
    [SerializeField] private TMP_Text 栏目标题;           // 板子/内容区/栏目标题（随 Tab 变）
    [SerializeField] private 属性子面板 属性页;             // 板子/内容区/属性页
    [SerializeField] private 技能子面板 技能页;             // 板子/内容区/技能页
    [SerializeField] private 知识子面板 知识页;             // 板子/内容区/知识页

    // ================= Inspector：本面板**自己的**颜色 =================
    // 口径：**只有"随状态变"的颜色才放这里**（文字内容/颜色状态/显隐/行位置是代码的活）。
    //   板底色 / 内容底色 / 边框色 / 遮罩色 / 悬停底 / ColorBlock 一律**归预制体**（代码不碰 → 在 Unity 里调得动）。
    [Header("颜色（只用于随状态变的那几处：Tab 文字色 + 经验格）")]
    [SerializeField] private Color 正文色 = new Color(0.8941f, 0.8745f, 0.8392f, 1f);   // #E4DFD6 选中的 Tab
    [SerializeField] private Color 次要色 = new Color(0.5412f, 0.5137f, 0.4706f, 1f);   // #8A8378 未选中的 Tab
    [SerializeField] private Color 强调色 = new Color(0.7059f, 0.3333f, 0.2353f, 1f);   // #B4553C 已点亮的经验格
    [SerializeField] private Color 禁选底色 = new Color(0.2275f, 0.2118f, 0.1882f, 1f); // #3A3630 未点亮的经验格

    private readonly List<Image> 经验格 = new List<Image>();   // 经验格父 的 10 个子节点（第一次刷新时收一次）
    private int 当前栏;
    private bool 报过缺引用;

    void Awake()
    {
        备数组(ref Tab按钮);
        备数组(ref Tab标签);
        备数组(ref Tab下划线);
        自找();
        接Tab();
        订阅事件();
        切换栏(0);   // 建完先落在第 0 栏（此时还没开档 → 三页各自刷新会自己 return，不报错）
    }

    // ================= 事件订阅 =================
    // 刷新口径：属性变化（加点/装备变化都会发）+ 生命/精力变化（身份条与属性页的生存段）+ 经验变化（身份条的经验格）。
    private void 订阅事件()
    {
        // `已注册` 再取：`ServiceRegistry.Get` 在未注册时是**抛异常**的，而本组件的 Awake 有可能早于
        //   面板管理器.Awake（脚本执行顺序不保证）→ 直接取会让整个组件炸在这里。
        var 事件 = ServiceRegistry.已注册<EventBus>() ? ServiceRegistry.Get<EventBus>() : null;
        if (事件 == null) return;
        事件.订阅<属性变化事件>(_ => 刷新(null));
        事件.订阅<经验变化事件>(_ => 刷新(null));
        事件.订阅<生命变化事件>(_ => 刷新(null));
        事件.订阅<精力变化事件>(_ => 刷新(null));
    }

    protected override void 刷新(object 上下文)
    {
        var 玩家 = 当前档案();
        if (玩家 == null) return;
        面板基类.设文本(身份文本, 身份行(玩家));
        面板基类.设文本(等级文本, $"Lv.{玩家.等级}");
        收经验格();
        刷经验格(玩家);
        // ★ **只刷当前那一页**（刀82 优化）：另外两页在 `切换栏` 时会被 SetActive(true) → 各自的 `OnEnable`
        //   会刷一次，所以它们不会"看到旧数据"。原来这里三页各刷一遍，而每次 生命/精力 变化都会走到这
        //   —— 翻三倍的无用功，且另两页的刷新全打在隐藏节点上（白算）。
        刷当前页();
    }

    // ================= 引用兜底：按名字找回来 =================
    // 预制体里已经接好了，这里是"手搭一半 / 场景里现搭 / 改了引用位"的兜底。
    // ⚠ 名字写死在这（与 `栏目表` 同源）—— 重命名节点要么同步改这里，要么把引用位接上。
    private void 自找()
    {
        if (身份文本 == null) 身份文本 = 找<TMP_Text>("板子/身份条/身份文本");
        if (等级文本 == null) 等级文本 = 找<TMP_Text>("板子/身份条/等级文本");
        if (经验格父 == null) 经验格父 = 找<RectTransform>("板子/身份条/经验格父");
        if (栏目标题 == null) 栏目标题 = 找<TMP_Text>("板子/内容区/栏目标题");
        for (int i = 0; i < 栏目表.Length; i++)
        {
            if (取(Tab按钮, i) == null) Tab按钮[i] = 找<Button>("板子/" + 栏目表[i]);
            if (取(Tab标签, i) == null) Tab标签[i] = 找<TMP_Text>("板子/" + 栏目表[i] + "/标签");
            if (取(Tab下划线, i) == null) Tab下划线[i] = 找<Image>("板子/" + 栏目表[i] + "/下划线");
        }
        if (属性页 == null) 属性页 = 找<属性子面板>("板子/内容区/属性页");
        if (技能页 == null) 技能页 = 找<技能子面板>("板子/内容区/技能页");
        if (知识页 == null) 知识页 = 找<知识子面板>("板子/内容区/知识页");
        报缺引用();
    }

    private T 找<T>(string 路径) where T : Component
    {
        var 节点 = transform.Find(路径);
        return 节点 != null ? 节点.GetComponent<T>() : null;
    }

    // 缺引用**要出声**：以前这条路是静默的（Inspector 少接一个位 → 表现只是"面板上少一块"，
    //   一条日志都没有，只能靠肉眼比对）。只在第一次报，免得每帧刷屏。
    private void 报缺引用()
    {
        if (报过缺引用) return;
        var 缺 = new List<string>();
        if (身份文本 == null) 缺.Add("身份文本");
        if (等级文本 == null) 缺.Add("等级文本");
        if (经验格父 == null) 缺.Add("经验格父");
        if (栏目标题 == null) 缺.Add("栏目标题");
        for (int i = 0; i < 栏目表.Length; i++)
        {
            if (取(Tab按钮, i) == null) 缺.Add($"Tab按钮[{i}]({栏目表[i]})");
            if (取(Tab标签, i) == null) 缺.Add($"Tab标签[{i}]({栏目表[i]})");
            if (取(Tab下划线, i) == null) 缺.Add($"Tab下划线[{i}]({栏目表[i]})");
        }
        if (属性页 == null) 缺.Add("属性页");
        if (技能页 == null) 缺.Add("技能页");
        if (知识页 == null) 缺.Add("知识页");
        if (缺.Count == 0) return;
        报过缺引用 = true;
        Debug.LogError("[角色面板] 结构不完整，缺引用：" + string.Join("、", 缺) +
                       "。本组件**运行时不再自建节点** —— 请把这些节点补进 `Assets/Resources/Prefab/角色面板.prefab`" +
                       "（整备脚本：`.workbuddy/分析/角色面板-prefab整备.py`），或把 Inspector 引用位接上。");
    }

    // ================= Tab =================
    // 只接点击：**底色/悬停色/过渡一律沿用预制体里烘好的 ColorBlock**（在 Unity 里改得动，也能整批换观感）。
    private void 接Tab()
    {
        for (int i = 0; i < 栏目表.Length; i++)
        {
            int 序 = i;   // 闭包捕获：循环变量必须另存一份，否则三个按钮全会切到最后一栏
            var 钮 = 取(Tab按钮, i);
            if (钮 == null) continue;
            // 先清旧监听：本组件接管这三个 Tab 的点击（`Awake` 只跑一次，但"场景里被重新激活"时可能重进）
            钮.onClick.RemoveAllListeners();
            钮.onClick.AddListener(() => 切换栏(序));
        }
    }

    // ================= 切栏 =================
    // 切栏 = 改 Tab 选中态（文字色 + 下划线显隐）+ 换栏目标题 + 三个子面板显隐。
    // **不在这里刷页**：被点亮的那一页会收到 `OnEnable` → 自己刷一次；而"已经亮着的那一页"说明数据没变过
    //   （真变了会走本面板的事件订阅 → `刷新` → 只刷当前页）。两条路各管一头，不重叠。
    private void 切换栏(int 序)
    {
        当前栏 = Mathf.Clamp(序, 0, 栏目表.Length - 1);
        for (int i = 0; i < 栏目表.Length; i++)
        {
            bool 选中 = i == 当前栏;
            var 文字 = 取(Tab标签, i);
            if (文字 != null) 文字.color = 选中 ? 正文色 : 次要色;
            var 线 = 取(Tab下划线, i);
            if (线 != null) 线.gameObject.SetActive(选中);   // 只有选中那条下划线显示
        }
        面板基类.设文本(栏目标题, 栏目表[当前栏]);
        只亮(属性页, 当前栏 == 0);
        只亮(技能页, 当前栏 == 1);
        只亮(知识页, 当前栏 == 2);
    }

    // 只刷"当前亮着的那一栏"的子面板。
    // ⚠ 三个子面板是 **`MonoBehaviour`（不是 `面板基类`）** —— 它们没有 `回退/取消文本` 那套出口协议，
    //   只是"内容块"，所以这里**不能**抽成"返回 面板基类 再调 `.刷新()`"（那样类型对不上、编不过）。
    //   逐个点名调最直白：加第 4 栏时，这里和 `切换栏` 各补一行。
    private void 刷当前页()
    {
        switch (当前栏)
        {
            case 0: if (属性页 != null) 属性页.刷新(); break;
            case 1: if (技能页 != null) 技能页.刷新(); break;
            case 2: if (知识页 != null) 知识页.刷新(); break;
        }
    }

    private static void 只亮(Component 目标, bool 亮)
    {
        if (目标 != null && 目标.gameObject.activeSelf != 亮) 目标.gameObject.SetActive(亮);
    }

    // ================= 顶部身份条 =================
    // "幸存者 · 职业"。职业名从数据表取（`职业数据.名称`）；取不到就退回职业标识，
    // 连标识都空（还没开档）就只显示"幸存者"。
    private static string 身份行(玩家档案 玩家)
    {
        string 职业 = "";
        if (!string.IsNullOrEmpty(玩家.职业))
        {
            var 数据 = ServiceRegistry.已注册<DataService>() ? ServiceRegistry.Get<DataService>() : null;
            职业 = 数据 != null && 数据.职业 != null && 数据.职业.TryGetValue(玩家.职业, out var 定义) && 定义 != null
                   && !string.IsNullOrEmpty(定义.名称)
                ? 定义.名称
                : 玩家.职业;
        }
        return string.IsNullOrEmpty(职业) ? "幸存者" : $"幸存者 · {职业}";
    }

    // 经验条：**一格一格地点亮**，不做连续填充（比例 = 经验 / 升级所需经验，CeilToInt(比例 × 格数) 亮前 N 格）。
    // 格子**由预制体铺**（`板子/身份条/经验格父` 下 10 个 `经验格`）—— 代码只在第一次刷新时把它们的 Image
    //   收进列表、之后按比例改颜色。格数/格宽/间隔都在预制体里，想改去 Unity 里改。
    private void 收经验格()
    {
        if (经验格.Count > 0 || 经验格父 == null) return;
        for (int i = 0; i < 经验格父.childCount; i++)
            经验格.Add(经验格父.GetChild(i).GetComponent<Image>());
    }

    private void 刷经验格(玩家档案 玩家)
    {
        int 总格 = 经验格.Count;
        if (总格 == 0) return;   // 预制体还没铺格子 → 什么都不画（缺引用已经报过了）
        int 需要 = Mathf.Max(1, 玩家.升级所需经验);
        float 比例 = Mathf.Clamp01(玩家.经验 / (float)需要);
        int 亮 = Mathf.Clamp(Mathf.CeilToInt(比例 * 总格), 0, 总格);
        for (int i = 0; i < 总格; i++)
        {
            var 格 = 经验格[i];
            if (格 == null) continue;   // 子物体只是个分组/占位（没挂 Image）→ 跳过，不算错
            格.color = i < 亮 ? 强调色 : 禁选底色;
        }
    }

    // ================= 出口协议（沿用 面板管理器 / HUD 关闭按钮 的既有约定） =================

    // 全局关闭按钮 = 返回上一面板（无上一面板则不可取消）——与原实现一致
    public override bool 回退()
    {
        if (面板管理器.实例?.上一个面板 == null) return false;
        面板管理器.实例.返回上一面板();
        return true;
    }

    // HUD 关闭按钮按这个文案选图标（"关闭" = 把这一页收掉，不是"离开某个地方"）
    public override string 取消文本 => "关闭";

    // ================= 小工具 =================

    // 取按钮/标签/下划线数组的第 序 个（越界或没接都返回 null → 调用方一句判空即可）
    private static T 取<T>(T[] 数组, int 序) where T : class
        => 数组 != null && 序 >= 0 && 序 < 数组.Length ? 数组[序] : null;

    // 旧场景可能把数组存成空数组/短数组 → 先补到 3 个（否则按下标赋值就崩）。
    // ⚠ 短数组里**已有的引用要带过来**：直接 `数组 = new T[3]` 会把"只拖了两个 Tab"的那两个引用丢掉。
    private static void 备数组<T>(ref T[] 数组) where T : class
    {
        if (数组 != null && 数组.Length == 栏目表.Length) return;
        var 旧 = 数组;
        var 新 = new T[栏目表.Length];
        if (旧 != null)
            for (int i = 0; i < 旧.Length && i < 新.Length; i++) 新[i] = 旧[i];
        数组 = 新;
    }

    // 取当前档案。**先问"注册了没有"再取**：`ServiceRegistry.Get` 未注册时是抛异常的。
    private static 玩家档案 当前档案()
        => ServiceRegistry.已注册<PlayerService>() ? ServiceRegistry.Get<PlayerService>()?.档案 : null;
}
