using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// 探索图层（基类）：**格子层自建的图层栈 + 逐格内容 + 令牌走路** —— 房间层 / 区域层共用。
//   地表层（地表贴图）/ 迷雾层（三态，可关）/ 路径指示层（高亮 + 点线 + 终点）/ 效果层（悬停 / 不可达闪红 / 落点圈）
//   / 交互层（**整层一张命中面**，v51 刀20 起——原来每格一张透明图 + 一个 探索格点击 组件）
//
// 为什么要自建（三条都是读基类源码得到的结论）：
//   ① 网格面板基类 四层只画"格子与物品"，地表贴图与迷雾没有位置；
//   ② **基类 Update 在无拖拽时会 隐藏全部投影**——路径高亮蹭它的"投影格"会被每帧抹掉；
//   ③ 基类 销毁网格结构 只销毁它自己那四层，自建层必须自己跟着网格尺寸重建。
//
// 另一个坑：**面板类里不能再写 void Update()**（基类 Update 是 private，会互相隐藏），
// 所以"检测重建 + 沿路径逐格推进 + 悬停跟随"这三件每帧的事，都放在本组件里。
//
// 令牌怎么走（第 2 版，替掉"按固定周期把 甲→乙 插值"）：
//   令牌自己"走路"：从**它此刻所在的位置**匀速走向"下一步的格子"；逻辑格在**令牌真的踩到格子**那一刻才推进。
//   → 走到一半点别处：令牌当场拐弯（立刻有反应），插值全程连续（永不瞬移），逻辑与视觉也不会各走各的。
//
// 层级顺序（z 从下到上）：
//   底盘 → 底座层 → 地表层 → 线层 → 交互层 → 物品层 → 迷雾层 → 路径层 → 效果层
//   **迷雾在物品层之上**：一层里"暗"这件事只由雾统一负责（地板/家具/墙/门一起压），实体不再各自压一遍
//   （原来雾在物品层之下，于是"实体的暗"要靠每个实体自己 CanvasGroup.alpha —— 新加一种实体忘了写就会在暗处亮着）。
//   交互层压在物品层之下：Unity 取最上层命中 → 点实体优先给实体框，没被覆盖的格子才落到交互层。
//   **交互层整层只有一张命中面**（v51 刀20）：空格点击 = 把指针位置换算成格坐标，不需要每格一个 raycast 目标。
//   ⚠ 真正决定"看得见看不见"的**不是层级**，而是 探索网格面板 子类的 更新实体框 里按"档"算的显示规则。
//
// 派生要填的（房间图层 / 区域图层）：
//   服务（格子探索服务）/ 地表贴图()；可覆写：有迷雾（默认开，房间与区域都开）/ 每格现实秒 / 点线贴图 / 落点贴图
// ============================================================
public abstract class 探索图层 : MonoBehaviour, 探索图层接口
{
    private const int 路径池上限 = 96;
    private const float 落点时长 = 0.30f;   // 点击落点圈：亮到灭的秒数（= 走一格的时间）

    protected 探索网格面板 面板;
    protected RectTransform 地表层, 迷雾层, 路径层, 效果层, 交互层;
    private readonly List<Image> 地表格 = new List<Image>();
    private readonly List<Image> 迷雾格 = new List<Image>();
    private 探索交互面 交互面;          // 交互层**唯一**的命中面（v51 刀20：替掉"每格一张透明图 + 一个组件"）
    private RectTransform 格位标记;      // 右键菜单定位用的"这一格的矩形"（按需挪动，见 格位框）

    // ===== 格子池（大网格专用，见 格子池化）=====
    // 池里的图与"格坐标"**解耦**：池槽只有下标，它此刻显示哪一格记在 池格列/池格行 里，
    // 相机滚过一格就重排一次（把窗口覆盖的格重新铺进池槽）。非池化时这些全是空表。
    // 注：v51 刀20 起**交互层不再池化**（整层一张命中面盖住全网格，格子池只管地表与迷雾）。
    private bool 池启用;
    private int 池容量;
    private int[] 池格列, 池格行;                     // 池槽 → 当前显示的格（-1 = 空槽）
    private readonly List<Image> 池地表 = new List<Image>();
    private readonly List<Image> 池迷雾 = new List<Image>();
    private int 池上次指纹 = int.MinValue;
    private readonly List<Image> 路径池 = new List<Image>();
    private readonly List<Image> 点线池 = new List<Image>();
    private Image 终点标, 悬停标, 闪红标, 落点标;

    protected int 列, 行, 上次物品层索引 = -2;
    private float 闪红剩余, 落点剩余;
    private int 悬停列 = -1, 悬停行 = -1;
    private int 落点列 = -1, 落点行 = -1;
    private int 上次种子 = int.MinValue;      // 认"换了世界"用：种子 + 模板变了 = 换了一间房 / 一个区域
    private string 上次模板标识;
    // ★ v51 刀18：两个"要不要重画"的去重键（理由见 刷迷雾 / 刷路径）
    private int 上次迷雾版本 = int.MinValue;  // 服务侧 视野版本：只有它变了，雾才有可能变
    private float 上次迷雾压暗 = -1f;
    private int 上次路径签名 = int.MinValue;  // 当前路径 的滚动哈希

    // 玩家令牌的连续位置（逻辑格子由服务给，令牌的"走到哪了"在这里——不改逻辑，只改观感）
    private RectTransform 玩家框;
    private Vector2 视觉位置;           // 令牌此刻在网格里的像素位置
    private bool 段有效;                // 有没有正在走的这一段
    private Vector2 段起点, 段目标;     // 本段的起止（起点 = 开段时令牌所在处，可能是格子里任意位置）
    private float 段计时, 段总时;       // 段总时 = 每格现实秒 × 段长（格）→ 直线速度恒定

    // ================= 派生给 =================

    protected abstract 格子探索服务 服务 { get; }
    protected abstract Sprite 地表贴图();                                  // 房间：房间贴图.地板()
    protected virtual bool 有迷雾 => true;                                 // 默认开（房间 / 区域都用）；false = 不建迷雾层
    protected virtual float 每格现实秒 => 格子探索服务.每格现实秒;          // 令牌速度 = 1 格 / 每格现实秒
    protected virtual Sprite 点线贴图() => 房间贴图.圆点();
    protected virtual Sprite 落点贴图() => 房间贴图.圆环();
    // 派生可给某一格加"地面装饰"（区域层：楼的入口格上画一扇门）——在重建时逐格调用
    // ⚠ 全仓库**目前没有任何子类覆写它**（区域层的门是画在实体框上的）。池化模式会反复重排格子，
    //   谁要用装饰就得自己管回收（否则装饰会随重排越堆越多）—— 建议改走"实体框"那条路。
    protected virtual void 建格装饰(RectTransform 地表, int 列, int 行) { }

    // ===== 格子池化（大世界 100×100 专用）=====
    // true  = 地表 / 迷雾 两层**只实例化相机窗口覆盖的那些格**，相机滚动时把池槽重定位重画。
    // false = 逐格全建（**默认**）。
    // 为什么默认关：房间层 16×10 / 区域层 32×22 现在这套跑得好好的，不该被牵连 ——
    //   而 100×100 若逐格全建是 2×10000 = 20,000 张 Image（地表+迷雾），
    //   加上底格/格线（大世界已用 网格面板基类.建底格/建格线 关掉，省 30,200 张）会直接把 Canvas 拖死。
    //   （v51 刀20 起交互层不再逐格建、也不参与池化：整层一张命中面，见 建交互面）
    // 开关语义：就算覆写成 true，**网格装得下窗口时仍走全格**（池化只在网格比窗口大时才有意义）。
    protected virtual bool 格子池化 => false;
    private const int 池边距 = 2;   // 窗口外多铺几格：不让边缘因为相机平滑跟随而露出空档

    private void Awake() => 面板 = GetComponent<探索网格面板>();

    private void Update()
    {
        if (面板 == null)
        {
            面板 = GetComponent<探索网格面板>();
            if (面板 == null) return;
        }
        var 服 = 服务;
        // 尺寸以**服务里的世界**为准：换世界当帧基类可能还没重建完（面板.渲染列还是旧值），
        // 若拿它跟自建层比大小，会先按旧尺寸重建一次、再重建一次 —— 露出两帧错尺寸的图层。
        int 新列 = 服?.当前世界 != null ? 服.当前世界.列 : 面板.渲染列;
        int 新行 = 服?.当前世界 != null ? 服.当前世界.行 : 面板.渲染行;
        if (新列 <= 0 || 新行 <= 0) return;
        int 物品索引 = 面板.物品层引用 != null ? 面板.物品层引用.GetSiblingIndex() : -1;
        if (地表层 == null || 新列 != 列 || 新行 != 行 || 物品索引 != 上次物品层索引)
        {
            列 = 新列;
            行 = 新行;
            重建();
            刷新();
        }
        推进移动();
        跟随玩家();
        跟随相机();
        更新池();          // 池化：相机滚过一格就把池槽重排（非池化时是空操作）
        跟随悬停();
        推进闪红();
        推进落点();
    }

    // ================= 重建 =================

    private void 重建()
    {
        销毁层(地表层); 销毁层(迷雾层); 销毁层(路径层); 销毁层(效果层); 销毁层(交互层);
        地表格.Clear(); 迷雾格.Clear(); 路径池.Clear(); 点线池.Clear();
        池地表.Clear(); 池迷雾.Clear();
        池启用 = false; 池容量 = 0; 池格列 = null; 池格行 = null; 池上次指纹 = int.MinValue;
        终点标 = null; 悬停标 = null; 闪红标 = null; 落点标 = null;
        交互面 = null; 格位标记 = null;
        // 去重键一并作废：图都重建了，必须重画一次（否则"雾/路径没变"会让我们跳过唯一的重画机会）
        上次迷雾版本 = int.MinValue; 上次迷雾压暗 = -1f; 上次路径签名 = int.MinValue;

        地表层 = 建层("地表层");
        迷雾层 = 有迷雾 ? 建层("迷雾层") : null;
        路径层 = 建层("路径层");
        效果层 = 建层("效果层");
        交互层 = 建层("交互层");

        排列层();
        确保视口();     // 相机：视口（裁切窗口）没了就补一层 —— **必须在建格子之前**：池化要看窗口多大
        建格子();       // 全格 or 格子池（由 格子池化 + 网格是否比窗口大 决定）
        建交互面();     // 交互层：整层一张命中面（与全格/池化无关，见 探索交互面）

        for (int i = 0; i < 路径池上限; i++)
        {
            路径池.Add(建格图(路径层, $"路{i}", null, 网格面板配色.路径格色));
            点线池.Add(建格图(路径层, $"点线{i}", 点线贴图(), 网格面板配色.路径线色));
        }
        终点标 = 建格图(路径层, "终点", null, 网格面板配色.路径终色);
        悬停标 = 建格图(效果层, "悬停", null, 网格面板配色.悬停格色);
        闪红标 = 建格图(效果层, "不可达", null, 网格面板配色.不可达色);
        落点标 = 建格图(效果层, "落点", 落点贴图(), 网格面板配色.路径终色);
        落点标.enabled = false;
        隐藏全部路径();

        上次物品层索引 = 面板.物品层引用 != null ? 面板.物品层引用.GetSiblingIndex() : -1;
        // 诊断：重建后自报一次（格子数 / 命中面 / 有没有拿到玩家框）——"点了没反应"时先看这条
        Debug.Log($"[探索] 图层重建 {列}×{行}：交互面 {(交互面 != null ? "1 张（整层单面）" : "缺！")}，物品层 {(面板?.物品层引用 != null ? 面板.物品层引用.childCount : 0)} 个实体框，"
                + $"玩家框[{(面板 != null ? (面板.实体框矩形(服务?.当前世界?.玩家()?.标识) != null ? "有" : "没有") : "无面板")}]，迷雾[{(有迷雾 ? "开" : "关")}]"
                + $"，格子池[{(池启用 ? $"开（{池容量} 槽，窗口外全不建）" : "关（逐格全建）")}]");
    }

    // ================= 建格子（全格 / 格子池） =================

    private void 建格子()
    {
        float 格 = 网格面板基类.格尺寸;
        float 窗宽 = 视口 != null ? 视口.sizeDelta.x : 列 * 格;
        float 窗高 = 视口 != null ? 视口.sizeDelta.y : 行 * 格;

        // 网格装得下窗口（多出不到一格也算装得下）→ 池化没有意义，直接走全格那条路（房间层走的就是这条）
        池启用 = 格子池化 && (列 * 格 > 窗宽 + 格 || 行 * 格 > 窗高 + 格);
        if (!池启用) { 建全格(); return; }

        // 池容量 = 窗口最多能同时盖住的格数（+1 是"跨边界那两格" + 两侧 池边距）
        int 可列 = Mathf.CeilToInt(窗宽 / 格) + 1 + 池边距 * 2;
        int 可行 = Mathf.CeilToInt(窗高 / 格) + 1 + 池边距 * 2;
        池容量 = Mathf.Min(可列 * 可行, 列 * 行);
        池格列 = new int[池容量];
        池格行 = new int[池容量];

        for (int i = 0; i < 池容量; i++)
        {
            池地表.Add(建格图(地表层, $"地_{i}", 地表贴图(), Color.white));
            if (迷雾层 != null)
            {
                var 雾 = 建格图(迷雾层, $"雾_{i}", null, Color.white);
                雾.raycastTarget = false;
                池迷雾.Add(雾);
            }
            池格列[i] = -1; 池格行[i] = -1;
        }
        自检池覆盖();   // 两套判据（点击的 在窗口内 / 建池的 窗口格区间）当场对一遍
        更新池(true);
    }

    // 交互层：**整层一张透明命中面**（v51 刀20）。像素零变化 —— 原来那 704 张图是全透明的，
    // 它们只是"每格一个 raycast 目标"；换成一个面 + 坐标换算，命中的格完全一样（见 探索交互面）。
    private void 建交互面()
    {
        if (交互层 == null) return;
        var 图 = 建格图(交互层, "交互面", null, new Color(0f, 0f, 0f, 0f));
        图.raycastTarget = true;   // 交互层唯一的命中目标（alpha=0 不吃画面，但吃 raycast）
        float 格 = 网格面板基类.格尺寸;
        UI工具.设锚(图.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
            new Vector2(列 * 格, 行 * 格));
        交互面 = 图.gameObject.AddComponent<探索交互面>();
        交互面.面板 = 面板;
        交互面.图层 = this;
    }

    private void 建全格()
    {
        var 世界 = 服务?.当前世界;
        for (int r = 0; r < 行; r++)
            for (int c = 0; c < 列; c++)
            {
                // 界外格（楼梯间那一侧的空档）**不铺地表**：看着就是"楼外"，而不是多出来一条地板
                var 格上 = 世界?.格上实体(c, r);
                bool 是界外 = 格上 != null && 格上.类型 == 网格实体类型.界外;
                if (!是界外)
                {
                    var 地板 = 建格图(地表层, $"地_{c}_{r}", 地表贴图(), 格明度(c, r));
                    摆格(地板, c, r);
                    地表格.Add(地板);
                    建格装饰(地表层, c, r);
                }

                if (迷雾层 != null)
                {
                    var 雾 = 建格图(迷雾层, $"雾_{c}_{r}", null, Color.white);
                    雾.raycastTarget = false;
                    摆格(雾, c, r);
                    迷雾格.Add(雾);
                }
                // 交互层不在这里建：整层只有一张命中面（见 建交互面）—— 原来每格一张透明图 + 一个组件
            }
    }

    // 相机窗口覆盖的格区间（闭区间，已夹进网格）—— 池化建格与自检共用这一份。
    // 推导：由 在窗口内() 的两条不等式解出列/行范围
    //   x = c*格 + 相机位置.x ∈ (-格, 窗宽)   → c ∈ ((-相机位置.x - 格)/格, (窗宽 - 相机位置.x)/格)
    //   y = -(r*格) + 相机位置.y ∈ (-窗高, 格) → r ∈ ((相机位置.y - 格)/格, (相机位置.y + 窗高)/格)
    private (int 首列, int 首行, int 末列, int 末行) 窗口格区间(int 外扩)
    {
        float 格 = Mathf.Max(1f, 网格面板基类.格尺寸);
        float 窗宽 = 视口 != null ? 视口.sizeDelta.x : 列 * 格;
        float 窗高 = 视口 != null ? 视口.sizeDelta.y : 行 * 格;
        int 首列 = Mathf.FloorToInt(-相机位置.x / 格) - 外扩;
        int 末列 = Mathf.FloorToInt((窗宽 - 相机位置.x) / 格) + 外扩;
        int 首行 = Mathf.FloorToInt(相机位置.y / 格) - 外扩;
        int 末行 = Mathf.FloorToInt((相机位置.y + 窗高) / 格) + 外扩;
        return (Mathf.Max(0, 首列), Mathf.Max(0, 首行), Mathf.Min(列 - 1, 末列), Mathf.Min(行 - 1, 末行));
    }

    // 一次性自检（只在重建时跑）：**在窗口内() 为真的格必须全部落在池区间里**。
    // 为什么值得写：点击判据（在窗口内）与建池判据（窗口格区间）是分开写的两段算术，
    // 一旦不一致，表现就是"看得见却点不到"或"点了错一格"——这类 bug 光看代码很难发现。
    // 10,000 次纯算术，只跑一次，代价可以忽略。
    private void 自检池覆盖()
    {
        var (首列, 首行, 末列, 末行) = 窗口格区间(池边距);
        int 漏 = 0;
        for (int r = 0; r < 行; r++)
            for (int c = 0; c < 列; c++)
            {
                if (!在窗口内(c, r)) continue;
                if (c < 首列 || c > 末列 || r < 首行 || r > 末行)
                {
                    if (漏 < 5)
                        Debug.LogWarning($"[探索] 池覆盖自检：格({c},{r}) 在窗口内但不在池区间 "
                                       + $"({首列}~{末列}, {首行}~{末行}) —— 这一格会看不见/点不到。");
                    漏++;
                }
            }
        if (漏 > 0)
            Debug.LogError($"[探索] 格子池覆盖自检**失败**：{漏} 格落在池区间之外（网格 {列}×{行}）。"
                         + "说明 在窗口内() 与 窗口格区间() 两套判据已经不一致。");
        else
            Debug.Log($"[探索] 格子池覆盖自检通过：窗口内的格全部落在池区间内（网格 {列}×{行}，池 {池容量} 槽）。");
    }

    // 池化：把"相机窗口覆盖的那些格"重新铺进池槽。相机滚过一格才重排（按格量化做指纹，避免每帧重排）。
    private void 更新池(bool 强制 = false)
    {
        if (!池启用 || 视口 == null || 池容量 <= 0) return;

        float 格 = 网格面板基类.格尺寸;
        // ★ 指纹必须同时含**相机格位**与**视口尺寸**（v51 刀15 修）：
        //   `窗口格区间()` 与 `在窗口内()` 都读 `视口.sizeDelta`，而原来的指纹只看相机位置 ——
        //   视口变了而相机没动时**池不重排** → 会出现"看得见却点不到 / 看不到却能点到"。
        //   而两道防线（运行时 `自检池覆盖` + 离线 `Tools/格子池区间核对.ps1`）当时都只覆盖"相机位置"这一维。
        int 相机指纹 = Mathf.FloorToInt(相机位置.x / 格) * 1000003 + Mathf.FloorToInt(相机位置.y / 格);
        int 视口指纹 = Mathf.RoundToInt(视口.sizeDelta.x) * 100003 + Mathf.RoundToInt(视口.sizeDelta.y);
        int 指纹 = 相机指纹 * 31 + 视口指纹 * 17;
        if (!强制 && 指纹 == 池上次指纹) return;
        池上次指纹 = 指纹;

        var (首列, 首行, 末列, 末行) = 窗口格区间(池边距);

        int 用 = 0;
        bool 溢出 = false;
        for (int r = 首行; r <= 末行; r++)
        {
            for (int c = 首列; c <= 末列; c++)
            {
                if (用 >= 池容量) { 溢出 = true; break; }
                摆池槽(用++, c, r);
            }
            if (溢出) break;
        }
        if (溢出)
            Debug.LogWarning($"[探索] 格子池不够用：池 {池容量} 槽，这一屏需要更多 —— 边上会缺格。"
                           + $"（列 {首列}~{末列} / 行 {首行}~{末行}）请调大 建格子() 里的 +1 / 池边距。");
        for (; 用 < 池容量; 用++) 空池槽(用);

        刷迷雾(true);   // 池重排后，池槽显示的是**别的格**了 → 必须强制重画（不能走"视野版本没变"的短路）
    }

    private void 摆池槽(int i, int c, int r)
    {
        // ★ v51 刀18：**同一格不重摆**。指纹只按"相机跨过格线"量化，所以一次重排里
        //   仍会有大量槽显示的其实还是原来那一格（视口变化 / 强制重排时更明显）——
        //   那些槽整段（贴图 + 明度 + 迷雾 + 交互定位）都可以直接跳过。
        //   ⚠ 位置相关的写入（摆格）与 池点击 的 列/行 必须一起跳过或一起做，不能只跳一半。
        if (池格列[i] == c && 池格行[i] == r) return;
        var 世界 = 服务?.当前世界;
        var 格上 = 世界?.格上实体(c, r);
        bool 是界外 = 格上 != null && 格上.类型 == 网格实体类型.界外;

        var 地板 = 池地表[i];
        if (是界外) { if (地板.enabled) 地板.enabled = false; }
        else
        {
            地板.sprite = 地表贴图();
            地板.color = 格明度(c, r);   // 每格轻微明度扰动（打破"一片死板同色"）
            摆格(地板, c, r);            // 摆格 内含 enabled = true
        }
        if (i < 池迷雾.Count) 摆格(池迷雾[i], c, r);
        池格列[i] = c; 池格行[i] = r;
    }

    private void 空池槽(int i)
    {
        if (i < 池地表.Count && 池地表[i].enabled) 池地表[i].enabled = false;
        if (i < 池迷雾.Count && 池迷雾[i].enabled) 池迷雾[i].enabled = false;
        池格列[i] = -1; 池格行[i] = -1;
    }

    private RectTransform 建层(string 名字)
    {
        var 层 = UI工具.创建物体(transform, 名字, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        层.anchoredPosition = new Vector2(0f, -网格面板配色.底盘外扩);
        层.sizeDelta = new Vector2(列 * 网格面板基类.格尺寸, 行 * 网格面板基类.格尺寸);
        return 层;
    }

    private Image 建格图(RectTransform 层, string 名字, Sprite 精灵, Color 色)
    {
        var 图 = UI工具.创建图(层, 名字, 精灵, 色, new Vector2(0f, 1f), new Vector2(0f, 1f));
        图.raycastTarget = false;
        return 图;
    }

    private void 销毁层(RectTransform 层)
    {
        if (层 == null) return;
        层.gameObject.SetActive(false);
        Destroy(层.gameObject);
    }

    // 层级顺序（z 从下到上）：
    //   底盘 → 底座层 → 地表层 → 线层 → 交互层 → 物品层 → 迷雾层 → 路径层 → 效果层
    //   · **迷雾在物品层之上**：雾负责"压暗一切"（地板、家具、墙、门一起压），实体不用各自再压一遍；
    //     迷雾图片 raycastTarget = false，所以盖在实体上面也不挡点击。
    //   · **交互层仍压在物品层之下**：Unity 取最上层命中 → 点实体优先给实体框，空格才落到交互层（右键菜单靠这条）。
    //   · 路径 / 效果在迷雾之上：路径要看得见往哪走，悬停 / 落点圈 / 闪红要保持清晰。
    //   ⚠ "看得见看不见"**不由层级决定**：显隐规则在 探索网格面板 子类的 更新实体框 里 —— 雾只压暗，不藏东西。
    private void 排列层()
    {
        int 序 = 0;
        置序(找子("底盘"), 序++);
        置序(找子("底座层"), 序++);
        置序(地表层, 序++);
        置序(找子("线层"), 序++);
        置序(交互层, 序++);
        置序(找子("物品层"), 序++);
        置序(迷雾层, 序++);
        置序(路径层, 序++);
        置序(效果层, 序);
    }

    private RectTransform 找子(string 名字)
    {
        var 子 = transform.Find(名字);
        return 子 as RectTransform;
    }

    private static void 置序(RectTransform 层, int 序)
    {
        if (层 != null) 层.SetSiblingIndex(序);
    }

    // ================= 相机（视口 + 内容） =================
    // 结构：面板 ── **视口**（RectMask2D，一屏窗口）── **内容**（= 本组件所在物体，装着整个网格）
    //   · 网格装得下 → 视口就按网格大小铺满：不裁切、不滚动，和"没有相机"时一模一样
    //   · 网格装不下 → 视口是一屏窗口，**相机跟着令牌走**（到边夹住），所以地图可以开很大
    // 为什么不做"等比缩小到一屏"：格子会跟着变小、字和图都糊；裁切+跟随才是"大地图"的正确做法。
    private RectTransform 视口;
    private Vector2 相机位置;
    private bool 相机已定位;
    private bool 报过视口;

    // 想要的窗口大小（宿主面板的 `视口` 字段）
    //   · 填正数 = 就用这个像素尺寸（默认 1720×960）
    //   · 填 0   = **自动铺满面板**（跟着分辨率走）
    private Vector2 想要的窗口
    {
        get
        {
            var 面板视口 = 面板 != null ? 面板.视口 : Vector2.zero;
            if (面板视口.x <= 0f || 面板视口.y <= 0f)
            {
                var 父 = (视口 != null ? 视口.parent : transform.parent) as RectTransform;
                if (父 != null && 父.rect.width > 1f && 父.rect.height > 1f)
                    return new Vector2(父.rect.width, 父.rect.height);
            }
            return 面板视口.x > 0f && 面板视口.y > 0f ? 面板视口 : new Vector2(1720f, 960f);
        }
    }
    protected Vector2 视口尺寸 => 视口 != null ? 视口.sizeDelta : 想要的窗口;
    public RectTransform 视口引用 => 视口;

    // 没有视口就补一层：**位置/ pivot 照抄内容原来的矩形**（手搭的网格容器摆在哪，窗口就摆在哪），
    // 但**大小用 `视口` 字段**（容器本身多大不决定窗口多大——否则手搭小了窗口就小）。
    private void 确保视口()
    {
        var 内容 = (RectTransform)transform;
        内容.localScale = Vector3.one;   // 旧版"等比缩小"已废弃：这里兜底复位
        if (视口 == null)
        {
            var 父 = 内容.parent as RectTransform;
            if (父 != null && 父.GetComponent<RectMask2D>() != null) 视口 = 父;
            else
            {
                var 原pos = 内容.anchoredPosition;
                var 物 = new GameObject("视口", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
                var 矩 = (RectTransform)物.transform;
                矩.SetParent(父, false);
                矩.anchorMin = new Vector2(0.5f, 0.5f);   // 固定大小、居中锚（位置沿用容器原来的）
                矩.anchorMax = new Vector2(0.5f, 0.5f);
                矩.pivot = new Vector2(0.5f, 0.5f);
                矩.anchoredPosition = 原pos;
                矩.sizeDelta = 想要的窗口;
                // 视口自己也要挡点击：mask 只裁画面不裁 raycast，窗口里"没格子的地方"由它吃掉
                var 罩 = 物.GetComponent<Image>();
                罩.color = new Color(0f, 0f, 0f, 0f);
                罩.raycastTarget = true;
                if (父 != null) 矩.SetSiblingIndex(内容.GetSiblingIndex());
                内容.SetParent(矩, false);
                内容.anchorMin = new Vector2(0f, 1f);
                内容.anchorMax = new Vector2(0f, 1f);
                内容.pivot = new Vector2(0f, 1f);
                内容.anchoredPosition = Vector2.zero;
                视口 = 矩;
                if (!报过视口)
                {
                    报过视口 = true;
                    Debug.Log($"[探索] 已自动补一层「视口」：窗口 {想要的窗口.x:0}×{想要的窗口.y:0}（RectMask2D）。要改大小就调面板上的「视口」字段。");
                }
            }
        }
        if (视口 == null) return;
        // 窗口大小：网格装得下就铺满（比窗口多出不到一格也算装得下 → 那种情况整间看全更舒服）
        float 格 = 网格面板基类.格尺寸;
        float 想宽 = 想要的窗口.x, 想高 = 想要的窗口.y;
        float 要宽 = 列 * 格 + 网格面板配色.底盘外扩 * 2f;
        float 要高 = 行 * 格 + 网格面板配色.底盘外扩 * 2f;
        float 用宽 = 要宽 <= 想宽 + 格 ? 要宽 : 想宽;
        float 用高 = 要高 <= 想高 + 格 ? 要高 : 想高;
        if (Mathf.Abs(视口.sizeDelta.x - 用宽) > 0.5f || Mathf.Abs(视口.sizeDelta.y - 用高) > 0.5f)
            视口.sizeDelta = new Vector2(用宽, 用高);
    }

    // 这一格的矩形 —— 右键菜单用它定位（菜单贴着被点的那一格弹）。
    // v51 刀20：交互层整层只有一张命中面了，所以这里**不再反查"那一格的透明图"**，
    // 而是维护**一个可复用的标记矩形**（按需挪到目标格；菜单只读它的 position/rect/pivot/lossyScale，
    // 见 右键菜单.展示），语义与旧实现完全一致，但少掉了池化时"反查哪个槽显示这一格"的线性扫描。
    public RectTransform 格位框(int 列2, int 行2)
    {
        if (列2 < 0 || 行2 < 0 || 列2 >= 列 || 行2 >= 行) return null;
        if (格位标记 == null)
        {
            格位标记 = UI工具.创建物体(交互层 != null ? 交互层 : transform, "格位框",
                new Vector2(0f, 1f), new Vector2(0f, 1f));
            // 只是个定位用的空矩形：没有 Graphic，既不画东西也不吃点击（也不需要 SetActive(false)）
        }
        float 格 = 网格面板基类.格尺寸;
        UI工具.设锚(格位标记, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(列2 * 格, -行2 * 格), new Vector2(格, 格));
        return 格位标记;
    }

    // 这一格现在在窗口里吗？—— RectMask2D 只裁画面、**不裁点击**，所以裁掉的那些格得自己拒绝响应
    public bool 在窗口内(int 列2, int 行2)
    {
        if (视口 == null) return true;
        float 格 = 网格面板基类.格尺寸;
        float x = 列2 * 格 + 相机位置.x;        // 相对视口左上角
        float y = -(行2 * 格) + 相机位置.y;      // y 向上（视口可见区 y ∈ [-窗高, 0]）
        float 窗宽 = 视口.sizeDelta.x, 窗高 = 视口.sizeDelta.y;
        return x + 格 > 0f && x < 窗宽 && y > -窗高 && y < 格;
    }

    // 每帧：相机跟到令牌身上（跟随 + 夹边）。立即 = 换世界/刚进图时直接定位，不做滑动
    private void 跟随相机(bool 立即 = false)
    {
        if (视口 == null || 列 <= 0 || 行 <= 0) return;
        float 格 = 网格面板基类.格尺寸;
        float 网格宽 = 列 * 格, 网格高 = 行 * 格;
        float 窗宽 = 视口.sizeDelta.x, 窗高 = 视口.sizeDelta.y;
        // 目标：令牌中心落在窗口中心（内容 pivot = 左上，anchoredPosition y 向上）
        float 想x = 窗宽 * 0.5f - 视觉位置.x - 格 * 0.5f;
        float 想y = -窗高 * 0.5f - 视觉位置.y + 格 * 0.5f;
        var 目标 = new Vector2(
            网格宽 <= 窗宽 ? (窗宽 - 网格宽) * 0.5f : Mathf.Clamp(想x, 窗宽 - 网格宽, 0f),
            网格高 <= 窗高 ? (网格高 - 窗高) * 0.5f : Mathf.Clamp(想y, 0f, 网格高 - 窗高));
        if (立即 || !相机已定位) { 相机位置 = 目标; 相机已定位 = true; }
        else 相机位置 = Vector2.Lerp(相机位置, 目标, Mathf.Clamp01(Time.deltaTime * 10f));
        ((RectTransform)transform).anchoredPosition = 相机位置;
    }

    // ================= 内容刷新 =================

    // 面板/事件随时会调这个（换世界时由显示事件同步调到 → **当帧就把新的画对**）
    public void 刷新()
    {
        var 服 = 服务;
        if (服 != null && 换了世界(服)) 切世界(服);
        if (地表层 == null) return;
        刷迷雾();
        刷路径();
    }

    // 是不是换了世界（走门 / 重进 / 出楼再进）：用「种子 + 模板标识」认
    private bool 换了世界(格子探索服务 服)
    {
        if (服?.当前世界 == null) return false;
        bool 变了 = 服.当前种子 != 上次种子 || 服.当前世界.模板标识 != 上次模板标识;
        上次种子 = 服.当前种子;
        上次模板标识 = 服.当前世界.模板标识;
        return 变了;
    }

    // 换世界：跟"上一个世界"绑定的表现状态全部丢掉，并**立刻按新尺寸重建图层**。
    // 为什么不能等下一帧的尺寸检测：那次检测要等基类先把网格重建完，中间会露出一帧"旧世界的迷雾/地格"，
    // 看起来就是"视野没及时更新"。这里直接用服务给的新尺寸重建，当帧就是对的。
    private void 切世界(格子探索服务 服)
    {
        段有效 = false;
        段起点 = 段目标 = Vector2.zero;
        段计时 = 段总时 = 0f;
        视觉位置 = Vector2.zero;
        悬停列 = 悬停行 = -1;
        落点列 = 落点行 = -1;
        落点剩余 = 0f;
        闪红剩余 = 0f;
        玩家框 = null;
        if (面板 != null) 面板.接管玩家位置 = false;
        相机已定位 = false;      // 换了世界：相机直接定到新图上，不做横移
        if (服?.当前世界 == null) return;
        列 = 服.当前世界.列;
        行 = 服.当前世界.行;
        重建();
    }

    // 迷雾按"档"画：亮 = 不遮；阴影（白天记忆 / 夜晚弱视野）= 压暗；全黑 = 不透明黑板。
    // ★ v51 刀18：**只在"视野真的重算过"时才重画**（服务侧 视野版本 变了 / 压暗值变了 / 强制）。
    //   原来任何 刷新() 都会无条件全网格重画：区域层 704 格 × 每格 `服.档()`（encode + 两次 HashSet 查），
    //   而"点一格格子""显示事件刷新"这些路径**根本不改变视野** —— 白算 704 格。
    //   为什么可以这样去重（`档()` 的全部输入都挂在这两个键上）：
    //     · 当前世界 变了 → 切世界 → 重建()（重建里把键重置成 MinValue）→ 必刷；
    //     · 当前可见 / 已探索 / 当前时段 只在 刷新视野() 里变 → 视野版本 必变；
    //     · 阴影压暗 来自视野.json（静态），一并比一次 float 兜底。
    private void 刷迷雾(bool 强制 = false)
    {
        var 服 = 服务;
        if (!有迷雾 || 服 == null) return;
        float 压暗 = Mathf.Clamp01(服.阴影压暗);
        if (!强制 && 服.视野版本 == 上次迷雾版本 && Mathf.Abs(压暗 - 上次迷雾压暗) < 0.001f) return;
        上次迷雾版本 = 服.视野版本;
        上次迷雾压暗 = 压暗;
        var 黑 = 网格面板配色.迷雾未探索色;

        // 池化：只刷池槽里的那些格（池槽 → 格 的映射由 更新池 维护）
        if (池启用)
        {
            for (int i = 0; i < 池迷雾.Count; i++)
            {
                int c = 池格列[i], r = 池格行[i];
                if (c < 0 || r < 0) continue;
                画一格雾(池迷雾[i], 服.档(c, r), 压暗, 黑);
            }
            return;
        }

        if (迷雾格.Count == 0) return;
        for (int r = 0; r < 行; r++)
            for (int c = 0; c < 列; c++)
                画一格雾(迷雾格[r * 列 + c], 服.档(c, r), 压暗, 黑);
    }

    // 一格的雾：亮 → 关掉；否则开 + 上色（值没变就不写：少脏一个 Graphic，就少一次画布重建）
    private static void 画一格雾(Image 图, 视野档 档, float 压暗, Color 黑)
    {
        if (档 == 视野档.亮)
        {
            if (图.enabled) 图.enabled = false;
            return;
        }
        if (!图.enabled) 图.enabled = true;
        黑.a = 档 == 视野档.全黑 ? 1f : 压暗;
        if (图.color != 黑) 图.color = 黑;
    }

    // 路径：格子高亮 + 中点"点线" + 终点准星（走过的格随 当前路径 缩短而消失）
    // ★ v51 刀18 两处：
    //   ① **签名去重**：路径没变就不重摆（点同一格 / 悬停 / 时间刷新 这些"视野外的刷新"不再刷 192 张图）；
    //   ② **从终点往前数着分配池槽**（原来从起点数）。走路时 当前路径 每步从队首缩短一格 ——
    //      按"从头数"分配时，这一步会让**几乎每一张**路径图都改画到另一格；
    //      按"从尾数"分配时，终点、点线、中段的图全都待在原地，只有队尾那几张要关掉。
    //      离线实测（一条 30 格直线走完，池 96 张）：平均每步改画的图 **16 张 → 1 张**（点线同理）。
    private void 刷路径(bool 强制 = false)
    {
        var 服 = 服务;
        var 路 = 服?.当前路径;
        int 签 = 路 == null ? 0 : 路.Count;
        if (路 != null)
            for (int i = 0; i < 路.Count; i++) 签 = 签 * 31 + (路[i].列 * 4096 + 路[i].行);
        if (!强制 && 签 == 上次路径签名) return;
        上次路径签名 = 签;
        int 用 = 0;
        float 格 = 网格面板基类.格尺寸;
        if (路 != null && 路.Count > 0)
        {
            for (int i = 路.Count - 1; i >= 0 && 用 < 路径池.Count; i--, 用++)
                摆格(路径池[用], 路[i].列, 路[i].行);
            int 点用 = 0;
            for (int i = 路.Count - 2; i >= 0 && 点用 < 点线池.Count; i--, 点用++)
            {
                var 图 = 点线池[点用];
                if (!图.enabled) 图.enabled = true;
                UI工具.设锚(图.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2((路[i].列 + 路[i + 1].列) * 0.5f * 格 + 格 * 0.5f,
                                -((路[i].行 + 路[i + 1].行) * 0.5f * 格 + 格 * 0.5f)),
                    new Vector2(9f, 9f));
            }
            for (int i = 点用; i < 点线池.Count; i++)
                if (点线池[i].enabled) 点线池[i].enabled = false;

            if (终点标 != null)
            {
                if (!终点标.enabled) 终点标.enabled = true;
                UI工具.设锚(终点标.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(路[路.Count - 1].列 * 格 + 格 * 0.5f, -(路[路.Count - 1].行 * 格 + 格 * 0.5f)),
                    new Vector2(16f, 16f));
            }
        }
        else
        {
            for (int i = 0; i < 点线池.Count; i++)
                if (点线池[i].enabled) 点线池[i].enabled = false;
            if (终点标 != null && 终点标.enabled) 终点标.enabled = false;
        }
        for (int i = 用; i < 路径池.Count; i++)
            if (路径池[i].enabled) 路径池[i].enabled = false;
    }

    private void 隐藏全部路径()
    {
        foreach (var 图 in 路径池) 图.enabled = false;
        foreach (var 图 in 点线池) 图.enabled = false;
        if (终点标 != null) 终点标.enabled = false;
    }

    private void 摆格(Image 图, int 列2, int 行2)
    {
        if (!图.enabled) 图.enabled = true;
        UI工具.设锚(图.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(列2 * 网格面板基类.格尺寸, -行2 * 网格面板基类.格尺寸),
            new Vector2(网格面板基类.格尺寸, 网格面板基类.格尺寸));
    }

    // ================= 每帧：推进 / 悬停 / 闪红 / 落点 =================

    // 玩家令牌：位置 = 视觉位置（自己走出来的），基类增量刷新不许把它拽回格子中心
    private void 跟随玩家()
    {
        var 服 = 服务;
        if (服 == null || !服.探索中)
        {
            玩家框 = null;
            if (面板 != null) 面板.接管玩家位置 = false;
            return;
        }
        if (玩家框 == null)
        {
            玩家框 = 面板?.实体框矩形(服.当前世界?.玩家()?.标识);
            if (面板 != null) 面板.接管玩家位置 = 玩家框 != null;
            if (玩家框 == null) return;
        }
        玩家框.anchoredPosition = 视觉位置;
    }

    // 令牌走路（逻辑推进的唯一入口）：从**当前位置**朝"下一步的格子"匀速走，走到格心这一刻才 推进一步。
    //   · 直线走：每段正好 1 格 = 每格现实秒（与旧版节奏一致）；
    //   · 半路改向：段起点就是令牌此刻所在处 → 当场拐弯，位置连续，永远不会"瞬移一格"；
    //     多走的那一截（最多 ~1.8 格）按同一速度走完，所以那一步会略慢一点点。
    private void 推进移动()
    {
        var 服 = 服务;
        if (服 == null || !服.探索中) { 段有效 = false; 玩家框 = null; return; }
        float 格 = 网格面板基类.格尺寸;
        if (服.遭遇中) { 段有效 = false; 视觉位置 = 格位(服.玩家列, 服.玩家行, 格); return; }   // 遭遇：停在这一格
        if (!段有效) 视觉位置 = 格位(服.玩家列, 服.玩家行, 格);   // 没在走：令牌贴在逻辑格中心（含刚走完/被拽回）

        var 路 = 服.当前路径;
        if (路 == null || 路.Count <= 1) { 段有效 = false; 视觉位置 = 格位(服.玩家列, 服.玩家行, 格); return; }
        var 目标 = 格位(路[1].列, 路[1].行, 格);
        if (!段有效 || 段目标 != 目标) 开段(目标);   // 新一步 / 点了别处 → 从当前位置重新开一段

        段计时 += Time.deltaTime;
        float t = 段总时 <= 0f ? 1f : Mathf.Clamp01(段计时 / 段总时);
        视觉位置 = Vector2.Lerp(段起点, 段目标, t);
        if (t < 1f) return;

        段有效 = false;   // 踩到格心：逻辑格在这一刻推进（同一帧里视野/迷雾也一起刷新）
        if (!服.推进一步()) 视觉位置 = 格位(服.玩家列, 服.玩家行, 格);   // 到头 / 遭遇 → 停在格心
        刷新();
    }

    // 开一段：起点 = 令牌此刻的位置（不是格子中心——所以半路拐弯也连续），时长按距离等比给
    private void 开段(Vector2 目标)
    {
        float 格 = Mathf.Max(1f, 网格面板基类.格尺寸);
        段有效 = true;
        段起点 = 视觉位置;
        段目标 = 目标;
        段计时 = 0f;
        段总时 = 每格现实秒 * Vector2.Distance(段起点, 段目标) / 格;
        if (段总时 < 0.02f) 段总时 = 0.02f;
    }

    // 格（列,行）左上角在网格容器里的锚点
    private static Vector2 格位(int 列2, int 行2, float 格) => new Vector2(列2 * 格, -行2 * 格);

    public void 悬停(int 列2, int 行2)
    {
        悬停列 = 列2;
        悬停行 = 行2;
    }

    private void 跟随悬停()
    {
        if (悬停标 == null) return;
        bool 显 = 悬停列 >= 0 && 悬停行 >= 0 && 悬停列 < 列 && 悬停行 < 行;
        if (显) 摆格(悬停标, 悬停列, 悬停行);
        if (悬停标.enabled != 显) 悬停标.enabled = 显;
    }

    // 目标不可达：闪一下淡红（0.35 秒淡出）
    public void 闪红(int 列2, int 行2)
    {
        if (闪红标 == null) return;
        if (列2 < 0 || 行2 < 0 || 列2 >= 列 || 行2 >= 行) return;
        摆格(闪红标, 列2, 行2);
        闪红剩余 = 0.35f;
    }

    private void 推进闪红()
    {
        if (闪红标 == null || !闪红标.enabled) return;
        闪红剩余 -= Time.deltaTime;
        if (闪红剩余 <= 0f)
        {
            闪红标.enabled = false;
            return;
        }
        var 色 = 网格面板配色.不可达色;
        色.a *= Mathf.Clamp01(闪红剩余 / 0.35f);
        闪红标.color = 色;
    }

    // ================= 点击落点圈 =================

    // 点了哪一格，就在那一格**当场**弹一圈（金色圆环由大到小收进格子 + 淡出）——
    // 这是"点下去有反应"的第一眼证据：不等令牌走，也不管那格在不在迷雾里。
    public void 落点(int 列2, int 行2)
    {
        if (落点标 == null) return;
        if (列2 < 0 || 行2 < 0 || 列2 >= 列 || 行2 >= 行) return;
        落点列 = 列2;
        落点行 = 行2;
        落点剩余 = 落点时长;
        if (!落点标.enabled) 落点标.enabled = true;
    }

    private void 推进落点()
    {
        if (落点标 == null || !落点标.enabled) return;
        落点剩余 -= Time.deltaTime;
        if (落点剩余 <= 0f)
        {
            落点标.enabled = false;
            return;
        }
        float t = Mathf.Clamp01(落点剩余 / 落点时长);      // 1 → 0
        var 色 = 网格面板配色.路径终色;
        色.a *= t;
        if (落点标.color != 色) 落点标.color = 色;
        摆格缩放(落点标, 落点列, 落点行, 1f + 0.45f * t);   // 1.45 格 → 1 格：收进来，像"点到这儿"
    }

    // 摆一格（可放大/缩小：落点圈用；普通高亮一律走 摆格）
    private void 摆格缩放(Image 图, int 列2, int 行2, float 倍)
    {
        if (!图.enabled) 图.enabled = true;
        float 格 = 网格面板基类.格尺寸;
        UI工具.设锚(图.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(列2 * 格 + 格 * 0.5f, -(行2 * 格 + 格 * 0.5f)),
            new Vector2(格 * 倍, 格 * 倍));
    }

    // 每格轻微明度扰动（打破"一片死板同色"）——返回 Image 的 tint 色
    private Color 格明度(int 列2, int 行2)
    {
        int 混 = 列2 * 73856093 ^ 行2 * 19349663 ^ 列 * 83492791;
        float t = Mathf.Abs(混 % 1000) / 1000f;   // 0~1
        float 明 = 0.90f + t * 0.20f;
        return new Color(明, 明, 明, 1f);
    }
}
