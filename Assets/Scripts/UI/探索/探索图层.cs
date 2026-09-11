using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// 探索图层（基类）：**格子层自建的图层栈 + 逐格内容 + 令牌走路** —— 房间层 / 区域层共用。
//   地表层（地表贴图）/ 迷雾层（三态，可关）/ 路径指示层（高亮 + 点线 + 终点）/ 效果层（悬停 / 不可达闪红 / 落点圈）
//   / 交互层（每格点击区）
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
    private readonly List<Image> 交互格 = new List<Image>();   // 交互层每格那张透明图（取格框给右键菜单定位用）
    private readonly List<Image> 路径池 = new List<Image>();
    private readonly List<Image> 点线池 = new List<Image>();
    private Image 终点标, 悬停标, 闪红标, 落点标;

    protected int 列, 行, 上次物品层索引 = -2;
    private float 闪红剩余, 落点剩余;
    private int 悬停列 = -1, 悬停行 = -1;
    private int 落点列 = -1, 落点行 = -1;
    private int 上次种子 = int.MinValue;      // 认"换了世界"用：种子 + 模板变了 = 换了一间房 / 一个区域
    private string 上次模板标识;

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
    protected virtual void 建格装饰(RectTransform 地表, int 列, int 行) { }

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
        跟随悬停();
        推进闪红();
        推进落点();
    }

    // ================= 重建 =================

    private void 重建()
    {
        销毁层(地表层); 销毁层(迷雾层); 销毁层(路径层); 销毁层(效果层); 销毁层(交互层);
        地表格.Clear(); 迷雾格.Clear(); 交互格.Clear(); 路径池.Clear(); 点线池.Clear();
        终点标 = null; 悬停标 = null; 闪红标 = null; 落点标 = null;

        地表层 = 建层("地表层");
        迷雾层 = 有迷雾 ? 建层("迷雾层") : null;
        路径层 = 建层("路径层");
        效果层 = 建层("效果层");
        交互层 = 建层("交互层");

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

                // 交互层：每格一张透明图（raycastTarget = true）+ 探索格点击；位于 物品层 之下 → 点实体优先给实体
                var 点 = 建格图(交互层, $"点_{c}_{r}", null, new Color(0f, 0f, 0f, 0f));
                点.raycastTarget = true;
                摆格(点, c, r);
                var 点击 = 点.gameObject.AddComponent<探索格点击>();
                点击.面板 = 面板;
                点击.列 = c;
                点击.行 = r;
                交互格.Add(点);
            }

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

        排列层();
        确保视口();                      // 相机：视口（裁切窗口）没了就补一层
        上次物品层索引 = 面板.物品层引用 != null ? 面板.物品层引用.GetSiblingIndex() : -1;
        // 诊断：重建后自报一次（格子数 / 交互层格数 / 有没有拿到玩家框）——"点了没反应"时先看这条
        Debug.Log($"[探索] 图层重建 {列}×{行}：交互层 {交互层.childCount} 格，物品层 {(面板?.物品层引用 != null ? 面板.物品层引用.childCount : 0)} 个实体框，"
                + $"玩家框[{(面板 != null ? (面板.实体框矩形(服务?.当前世界?.玩家()?.标识) != null ? "有" : "没有") : "无面板")}]，迷雾[{(有迷雾 ? "开" : "关")}]");
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

    // 这一格在"交互层"里那张透明图 —— 右键菜单用它定位（菜单贴着被点的那一格弹）
    public RectTransform 格位框(int 列2, int 行2)
    {
        if (列2 < 0 || 行2 < 0 || 列2 >= 列 || 行2 >= 行) return null;
        int i = 行2 * 列 + 列2;
        return i >= 0 && i < 交互格.Count ? 交互格[i].rectTransform : null;
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
    private void 刷迷雾()
    {
        var 服 = 服务;
        if (!有迷雾 || 服 == null || 迷雾格.Count == 0) return;
        float 压暗 = Mathf.Clamp01(服.阴影压暗);
        var 黑 = 网格面板配色.迷雾未探索色;
        for (int r = 0; r < 行; r++)
            for (int c = 0; c < 列; c++)
            {
                var 图2 = 迷雾格[r * 列 + c];
                var 档 = 服.档(c, r);
                if (档 == 视野档.亮)
                {
                    if (图2.enabled) 图2.enabled = false;
                    continue;
                }
                if (!图2.enabled) 图2.enabled = true;
                黑.a = 档 == 视野档.全黑 ? 1f : 压暗;
                if (图2.color != 黑) 图2.color = 黑;   // 值没变就不写：少脏一个 Graphic，就少一次画布重建
            }
    }

    // 路径：格子高亮 + 中点"点线" + 终点准星（走过的格随 当前路径 缩短而消失）
    private void 刷路径()
    {
        var 服 = 服务;
        var 路 = 服?.当前路径;
        int 用 = 0;
        float 格 = 网格面板基类.格尺寸;
        if (路 != null && 路.Count > 0)
        {
            for (int i = 0; i < 路.Count && 用 < 路径池.Count; i++, 用++)
                摆格(路径池[用], 路[i].列, 路[i].行);
            int 点用 = 0;
            for (int i = 0; i + 1 < 路.Count && 点用 < 点线池.Count; i++, 点用++)
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
