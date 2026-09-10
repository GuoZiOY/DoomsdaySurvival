using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// 房间图层：房间网格面板 自建的图层栈 + 逐格内容（基类管不到这些，也不该管）。
//   地表层（地板贴图）/ 迷雾层（三态）/ 路径指示层（高亮 + 点线 + 终点）/ 效果层（悬停 / 不可达闪红）/ 交互层（每格点击区）
//
// 为什么要自建（三条都是读基类源码得到的结论）：
//   ① 基类四层只画"格子与物品"，地板纹理与迷雾没有位置；
//   ② **基类 Update 在无拖拽时会 隐藏全部投影**——路径高亮蹭它的"投影格"会被每帧抹掉；
//   ③ 基类 销毁网格结构 只销毁它自己那四层，自建层必须自己跟着网格尺寸重建。
//
// 另一个坑：**面板类里不能再写 void Update()**（基类 Update 是 private，会互相隐藏），
// 所以"检测重建 + 沿路径逐格推进 + 悬停跟随"这三件每帧的事，都放在本组件里。
//
// 层级顺序（z 从下到上）：底盘 → 底座层 → 地表层 → 线层 → 迷雾层 → 路径层 → 交互层 → 物品层 → 效果层
//   迷雾在物品层之下：实体不靠"被遮住"，而是**自己按可见性调 alpha/隐藏**（这样"已探索但看不见敌人的实时位置"才成立）；
//   交互层在物品层之下：Unity 取最上层命中 → 点实体优先给实体框，没被覆盖的格子才落到交互层。
//
// 令牌怎么走（第 2 版，替掉"按固定周期把 甲→乙 插值"）：
//   令牌自己"走路"：从**它此刻所在的位置**匀速走向"下一步的格子"；逻辑格在**令牌真的踩到格子**那一刻才推进。
//   → 走到一半点别处：令牌当场拐弯（立刻有反应），插值全程连续（永不瞬移），逻辑与视觉也不会各走各的。
// ============================================================
public sealed class 房间图层 : MonoBehaviour
{
    // 走一格的现实节奏由 房间探索服务.每格现实秒 给（令牌速度 = 1 格 / 每格现实秒）；拐弯那一步会多走一截，故略慢
    private const int 路径池上限 = 96;
    private const float 落点时长 = 0.30f;   // 点击落点圈：亮到灭的秒数（= 走一格的时间）

    private 房间网格面板 面板;
    private RectTransform 地表层, 迷雾层, 路径层, 效果层, 交互层;
    private readonly List<Image> 地表格 = new List<Image>();
    private readonly List<Image> 迷雾格 = new List<Image>();
    private readonly List<Image> 路径池 = new List<Image>();
    private readonly List<Image> 点线池 = new List<Image>();
    private Image 终点标, 悬停标, 闪红标, 落点标;

    private int 列, 行, 上次物品层索引 = -2;
    private float 闪红剩余, 落点剩余;
    private int 悬停列 = -1, 悬停行 = -1;
    private int 落点列 = -1, 落点行 = -1;

    // 玩家令牌的连续位置（逻辑格子由服务给，令牌的"走到哪了"在这里——不改逻辑，只改观感）
    private RectTransform 玩家框;
    private Vector2 视觉位置;           // 令牌此刻在房间里的像素位置
    private bool 段有效;                // 有没有正在走的这一段
    private Vector2 段起点, 段目标;     // 本段的起止（起点 = 开段时令牌所在处，可能是格子里任意位置）
    private float 段计时, 段总时;       // 段总时 = 每格现实秒 × 段长（格）→ 直线速度恒定


    private 房间探索服务 服务 => ServiceRegistry.Get<房间探索服务>();

    private void Awake() => 面板 = GetComponent<房间网格面板>();

    private void Update()
    {
        if (面板 == null)
        {
            面板 = GetComponent<房间网格面板>();
            if (面板 == null) return;
        }
        int 新列 = 面板.渲染列, 新行 = 面板.渲染行;
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
        跟随悬停();
        推进闪红();
        推进落点();
    }

    // ================= 重建 =================

    private void 重建()
    {
        销毁层(地表层); 销毁层(迷雾层); 销毁层(路径层); 销毁层(效果层); 销毁层(交互层);
        地表格.Clear(); 迷雾格.Clear(); 路径池.Clear(); 点线池.Clear();
        终点标 = null; 悬停标 = null; 闪红标 = null; 落点标 = null;

        地表层 = 建层("地表层");
        迷雾层 = 建层("迷雾层");
        路径层 = 建层("路径层");
        效果层 = 建层("效果层");
        交互层 = 建层("交互层");

        for (int r = 0; r < 行; r++)
            for (int c = 0; c < 列; c++)
            {
                var 地板 = 建格图(地表层, $"地_{c}_{r}", 房间贴图.地板(), 格明度(c, r));
                摆格(地板, c, r);
                地表格.Add(地板);

                var 雾 = 建格图(迷雾层, $"雾_{c}_{r}", null, Color.white);
                雾.raycastTarget = false;
                摆格(雾, c, r);
                迷雾格.Add(雾);

                // 交互层：每格一张透明图（raycastTarget = true）+ 房间格点击；位于 物品层 之下 → 点实体优先给实体
                var 点 = 建格图(交互层, $"点_{c}_{r}", null, new Color(0f, 0f, 0f, 0f));
                点.raycastTarget = true;
                摆格(点, c, r);
                var 点击 = 点.gameObject.AddComponent<房间格点击>();
                点击.面板 = 面板;                点击.列 = c;
                点击.行 = r;
            }

        for (int i = 0; i < 路径池上限; i++)
        {
            路径池.Add(建格图(路径层, $"路{i}", null, 网格面板配色.路径格色));
            点线池.Add(建格图(路径层, $"点线{i}", 房间贴图.圆点(), 网格面板配色.路径线色));
        }
        终点标 = 建格图(路径层, "终点", null, 网格面板配色.路径终色);
        悬停标 = 建格图(效果层, "悬停", null, 网格面板配色.悬停格色);
        闪红标 = 建格图(效果层, "不可达", null, 网格面板配色.不可达色);
        落点标 = 建格图(效果层, "落点", 房间贴图.圆环(), 网格面板配色.路径终色);
        落点标.enabled = false;
        隐藏全部路径();

        排列层();
        上次物品层索引 = 面板.物品层引用 != null ? 面板.物品层引用.GetSiblingIndex() : -1;
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

    // 层级顺序：底盘 → 底座层 → 地表层 → 线层 → 迷雾层 → 路径层 → 交互层 → 物品层 → 效果层
    private void 排列层()
    {
        int 序 = 0;
        置序(找子("底盘"), 序++);
        置序(找子("底座层"), 序++);
        置序(地表层, 序++);
        置序(找子("线层"), 序++);
        置序(迷雾层, 序++);
        置序(路径层, 序++);
        置序(交互层, 序++);
        置序(找子("物品层"), 序++);
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

    // ================= 内容刷新 =================

    public void 刷新()
    {
        if (地表层 == null) return;
        刷迷雾();
        刷路径();
    }

    // 迷雾按"档"画：亮 = 不遮；阴影（白天记忆 / 夜晚弱视野）= 压暗；全黑 = 不透明黑板。
    private void 刷迷雾()
    {
        var 服 = 服务;
        if (服 == null || 迷雾格.Count == 0) return;
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
            玩家框 = 面板?.实体框矩形(服.当前房间?.玩家()?.标识);
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
        段总时 = 房间探索服务.每格现实秒 * Vector2.Distance(段起点, 段目标) / 格;
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
