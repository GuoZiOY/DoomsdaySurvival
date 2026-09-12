using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================
// 区域网格面板：探索网格面板 子类（对位 房间网格面板）——只写「区域里的实体语义」。
// 实体承载：墙（街道尽头的废墟边界）/ 建筑（占多格）/ 障碍 / 玩家 都是 物品堆叠
//           （由 区域探索服务 同步进 数据源 网格服务）；通行判定 = 该格有没有实体。
//
// 外观（程序化，零美术资源）：
//   建筑 = 按掩码**逐格**铺楼体（楼层线 + 窗）+ 只描朝外的轮廓线 + 楼名；门画在楼体自己底边那一格上
//   墙   = 复用房间层墙顶贴图 + 转角描边（区域边界 = 一圈废墟墙）
//   障碍 = 废车 / 砖堆剪影 + 名称；玩家 = 圆盘 + 人形 + 圆环 + "你"
// 点击规矩（和房间层一致）：**点哪一格就走到哪一格**（占格的就走到它旁边），除非过不去；
//   楼自己不接点击（图形 raycastTarget = false），所以点在楼上的哪一格都落到"那一格的交互层" ——
//   点楼体 = 走到楼边、点**门那一格** = 走到门口（走上去就进楼）、点凹口 = 走进天井。右键那一格 = 楼的菜单。
// 迷雾：**和房间层同一套**（服务那边不覆写 有迷雾 / 视野边长）——没去过的街区是黑板，走过的压暗成记忆，
//   跟前一圈才全亮；夜里只剩弱视野圈里的楼影。可见性规则见 更新实体框。
// ⚠ 本类**绝对不能写 void Update()**（基类的 Update 是 private，会隐藏它）——每帧逻辑一律放 区域图层。
// ============================================================
public sealed class 区域网格面板 : 探索网格面板
{
    private 区域图层 图层缓存;
    private bool 报过缺图层;

    private 区域探索服务 区域服务 => ServiceRegistry.Get<区域探索服务>();
    private 网格数据 区域 => 区域服务?.当前区域;

    protected override 格子探索服务 探索服务 => 区域服务;

    public override 探索图层接口 图层()
    {
        if (图层缓存 == null)
        {
            图层缓存 = GetComponent<区域图层>();
            if (图层缓存 == null && !报过缺图层)
            {
                报过缺图层 = true;
                Debug.LogError($"[区域] {name} 上找不到 区域图层 —— 地表/迷雾/路径/落点/逐格点击都靠它，点了也不会动。请在同一物体上加 区域图层 组件。");
            }
        }
        return 图层缓存;
    }

    // ================= 实体框（区域外观） =================

    protected override 物品框 创建实体框(物品堆叠 堆叠)
    {
        if (堆叠 == null) return null;
        var 实体 = 找实体(堆叠.标识);
        var (宽, 高) = 格数(堆叠);
        bool 是墙 = 实体 != null && 实体.类型 == 网格实体类型.墙;
        bool 是楼 = 实体 != null && 实体.类型 == 网格实体类型.建筑;
        bool 是障碍 = 实体 != null && 实体.类型 == 网格实体类型.障碍;
        bool 是敌人 = 实体 != null && 实体.类型 == 网格实体类型.敌人;
        bool 是门 = 实体 != null && 实体.类型 == 网格实体类型.门;   // 街口（区域唯一的出口，走上去就出门）
        bool 是玩家 = 实体 != null && 实体.是玩家;

        var 物体 = new GameObject($"实体_{堆叠.标识}", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(物品层, false);
        var 框 = new 物品框();
        框.根 = 物体.GetComponent<RectTransform>();
        物体.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // 框根透明（只留贴图与描边）
        // 玩家令牌不吃点击（和房间层同一条：令牌常和脚下那一格的实体重叠 → 一接点击就把它盖住）
        if (是玩家) 物体.GetComponent<Image>().raycastTarget = false;
        物体.AddComponent<CanvasGroup>();
        if (是楼 || 是障碍)   // 楼与障碍给方块黑描边（体量感）；墙靠转角描边、令牌用圆盘
        {
            var 描边 = 物体.AddComponent<Outline>();
            描边.effectColor = 网格面板配色.网格实体描边;
            描边.effectDistance = 物品描边距离;
        }
        定位(框.根, 堆叠.列, 堆叠.行, 宽, 高);
        框.高光层 = 创建高光层(物体.transform);

        float 整宽 = 宽 * 格尺寸, 整高 = 高 * 格尺寸;
        if (是楼)
        {
            // 楼体：**按掩码逐格铺**（L / 凸 / 凹 都能画对）+ 只在"邻居不是本栋楼"的边描暗边。
            // 点击：楼**不自己接点击**（框根与每格楼体都 raycastTarget = false）——
            //   于是点在楼上的哪一格就落到**那一格的交互层**，面板拿到的就是"被点的那一格"，
            //   点楼体 = 走到它旁边，点**门那一格** = 走到门口（走上去就进楼），点凹口 = 走进天井。
            //   内容层 = null → 悬停不放大（基类对 null 安全）
            物体.GetComponent<Image>().raycastTarget = false;
            框.内容层 = null;
            for (int r = 0; r < 实体.高; r++)
                for (int c = 0; c < 实体.宽; c++)
                {
                    if (!实体.覆盖(实体.列 + c, 实体.行 + r)) continue;
                    var 格图 = 摆楼格(物体.transform, c, r);
                    建楼边(格图.transform, 实体, c, r);
                }
            框.名称 = 建名称(物体.transform, 实体.名称, new Vector2(0.5f, 0.5f), new Vector2(整宽 - 10f, 34f), 16f);
            // 门画在**楼体自己底边那一格**上（门口格：走上去就进楼）
            var 门格 = 区域生成器.建筑入口格(实体);
            if (门格.列 >= 0 && 实体.覆盖(门格.列, 门格.行))
            {
                var 门图 = UI工具.创建图(物体.transform, "楼门", 区域贴图.入口(), Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f));
                UI工具.设锚(门图.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2((门格.列 - 实体.列) * 格尺寸, -(门格.行 - 实体.行) * 格尺寸),
                    new Vector2(格尺寸, 格尺寸));
                门图.raycastTarget = false;
            }
        }
        else if (是墙)
        {
            var 墙图 = 摆满(物体.transform, "墙", 区域贴图.墙顶(), Color.white, 整宽, 整高);
            框.内容层 = 墙图.rectTransform;
            建街边(物体.transform, 实体, 整宽, 整高);
        }
        else if (是障碍)
        {
            var 图 = 摆满(物体.transform, "障碍", 区域贴图.障碍(实体.名称), Color.white, 整宽 - 8f, 整高 - 8f);
            框.内容层 = 图.rectTransform;
            框.名称 = 建名称(物体.transform, 实体.名称, new Vector2(0.5f, 0f), new Vector2(整宽, 18f), 13f);
        }
        else if (是门)
        {
            // 街口：**站在边界墙上那个门洞**（区域唯一的出口）。贴图直接复用楼门那张（同一种"楼道口"）。
            // 它 **占格 = false** —— 玩家要走上去才出得去，所以不能占格。
            var 图 = 摆满(物体.transform, "街口", 区域贴图.入口(), Color.white, 整宽 - 6f, 整高 - 6f);
            框.内容层 = 图.rectTransform;
            框.名称 = 建名称(物体.transform, "街口", new Vector2(0.5f, 0f), new Vector2(格尺寸 * 宽, 18f), 14f);
        }
        else if (是敌人)
        {
            // 街上敌人：与房间层同一套画法（圆盘 + 剪影 + 名字），只是尺寸按街上那格来
            float 边长 = Mathf.Min(整宽, 整高) - 18f;
            var 偏移 = new Vector2(0f, 6f);
            var 盘 = 摆居中(物体.transform, "盘", 区域贴图.圆盘(), 边长, 偏移);
            摆居中(物体.transform, "剪影", 区域贴图.剪影(), 边长, 偏移);
            框.内容层 = 盘.rectTransform;
            框.框图 = 盘;   // 圆盘按阵营上色（更新时改色）
            框.名称 = 建名称(物体.transform, 实体.名称 ?? "?", new Vector2(0.5f, 0f), new Vector2(格尺寸 * 宽, 20f), 15f);
        }
        else if (是玩家)
        {
            float 边长 = Mathf.Min(整宽, 整高) - 18f;
            var 偏移 = new Vector2(0f, 6f);
            var 盘 = 摆居中(物体.transform, "盘", 区域贴图.圆盘(), 边长, 偏移);
            摆居中(物体.transform, "剪影", 区域贴图.剪影(), 边长, 偏移);
            摆居中(物体.transform, "环", 区域贴图.圆环(), 边长, 偏移).color = 网格面板配色.玩家令牌边;
            框.内容层 = 盘.rectTransform;
            框.框图 = 盘;   // 令牌：圆盘按阵营上色
            框.名称 = 建名称(物体.transform, "你", new Vector2(0.5f, 0f), new Vector2(整宽, 20f), 15f);
        }
        else
        {
            var 图 = 摆满(物体.transform, "块", null, 网格面板配色.底座色, 整宽 - 10f, 整高 - 10f);
            框.内容层 = 图.rectTransform;
            框.名称 = 建名称(物体.transform, 实体?.名称 ?? 堆叠.标识, new Vector2(0.5f, 0.5f), new Vector2(整宽, 整高), 14f);
        }

        挂接交互(框, 堆叠, 框.内容层);
        return 框;
    }

    protected override void 更新实体框(物品框 框, 物品堆叠 堆叠)
    {
        if (框?.根 == null || 堆叠 == null) return;
        var 实体 = 找实体(堆叠.标识);
        var (宽, 高) = 格数(堆叠);
        // 玩家令牌：位置由 区域图层 平滑接管时，这里不要把它拽回格子（否则会和插值打架）
        if (!(接管玩家位置 && 实体 != null && 实体.是玩家))
            框.根.anchoredPosition = new Vector2(格x(堆叠.列, 堆叠.行), -格y(堆叠.列, 堆叠.行));
        var 目标尺寸 = new Vector2(宽 * 格尺寸, 高 * 格尺寸);
        if ((框.根.sizeDelta - 目标尺寸).sqrMagnitude > 0.01f) 框.根.sizeDelta = 目标尺寸;

        // 可见性按"档"（与房间层同一套规矩）：楼 / 墙 / **街口（门）** = 只要不是全黑就画
        // （记忆里留轮廓、夜里也看得见楼影 —— 街口尤其要这样：它是唯一的出路，不能黑着找不到）；
        // 障碍 / 敌人 = 只在亮 / 白天记忆里画（黑暗里看不见废车、也看不见街上的丧尸）；
        // 玩家永远画。
        // 楼是**多格实体**：档取占地里最亮的那一格（看见一个角，整栋就画出来，剩下的部分由迷雾层盖黑）。
        var 档 = 整体档(实体);
        bool 亮 = 档 == 视野档.亮;
        bool 显示 = 实体 == null || 实体.是玩家;
        if (实体 != null && !实体.是玩家)
        {
            if (实体.类型 == 网格实体类型.建筑 || 实体.类型 == 网格实体类型.墙
                || 实体.类型 == 网格实体类型.门) 显示 = 档 != 视野档.全黑;
            else 显示 = 亮 || 档 == 视野档.暗记忆;   // 障碍 / 敌人（和房间层的容器/尸体同一条）
        }
        if (框.根.gameObject.activeSelf != 显示) 框.根.gameObject.SetActive(显示);
        if (!显示) return;
        // 暗处的压暗**不在这里做**：迷雾层在物品层之上，地板/楼/障碍一起压同一个 阴影压暗 值
        var 组 = 框.根.GetComponent<CanvasGroup>();
        if (组 != null && Mathf.Abs(组.alpha - 1f) > 0.01f) 组.alpha = 1f;
        if (框.框图 != null && 实体 != null)
        {
            var 色 = 实体色(实体);
            if (框.框图.color != 色) 框.框图.color = 色;
        }
    }

    // 多格实体（楼）的"整体档"：取占地掩码里**最亮**的那一格 ——
    // 只看见楼的一个角，整栋就该画出来（没看见的部分由 迷雾层 逐格盖黑，不必整栋一起藏）。
    // 单格实体退化成 档(列,行)，与房间层那些单格实体的判定等价。
    private 视野档 整体档(网格实体 实体)
    {
        var 服 = 探索服务;
        if (服 == null || 实体 == null) return 视野档.亮;
        var 最好 = 视野档.全黑;
        for (int r = 实体.行; r < 实体.行 + 实体.高; r++)
            for (int c = 实体.列; c < 实体.列 + 实体.宽; c++)
            {
                if (!实体.覆盖(c, r)) continue;      // 凹口不算楼体
                var 档 = 服.档(c, r);
                if (档 > 最好) 最好 = 档;
                if (最好 == 视野档.亮) return 最好;   // 已经最亮，不用再看
            }
        return 最好;
    }

    // ================= 右键菜单（区域内容） =================

    // 右键一格：先看这一格上有什么（楼 / 障碍 → 菜单；空地 → 什么都不弹）
    // 楼的右键走这里（楼的图形不吃点击，右键也落到交互层，所以拿得到"被点的那一格"）
    public override void 右键格(int 列, int 行)
    {
        var 实体 = 世界?.格上实体(列, 行);
        if (实体 == null) return;
        打开菜单(实体, 图层()?.格位框(列, 行));
    }

    protected override void 右键实体(物品堆叠 堆叠, RectTransform 框)
        => 打开菜单(找实体(堆叠?.标识), 框);

    private void 打开菜单(网格实体 实体, RectTransform 框)
    {
        if (实体 == null || 右键菜单.实例 == null) return;
        var 类 = 实体.类型;
        if (类 != 网格实体类型.建筑 && 类 != 网格实体类型.障碍
            && 类 != 网格实体类型.门 && 类 != 网格实体类型.敌人) return;
        右键菜单.实例.目标面板 = this;
        var 条目 = new List<(string 文本, Action 动作)>();
        if (类 == 网格实体类型.建筑)
        {
            // 楼的入口长在楼自己身上：给一条"走到门口"（走上去就进楼，和服务层是同一条路）
            var 门 = 区域生成器.建筑入口格(实体);
            条目.Add(($"走到门口（{门.列},{门.行}）", () => 点格反馈(门.列, 门.行)));
        }
        else if (类 == 网格实体类型.门)
        {
            // 街口：给一条"出去"（和走上去等价 —— 区域探索服务.离开()，Esc 走的也是它）
            条目.Add(("从街口出去", () => ServiceRegistry.Get<区域探索服务>()?.离开()));
        }
        else if (类 == 网格实体类型.敌人)
        {
            // 敌人：给一条"走过去"（走上去/走到相邻格就触发遭遇，与服务层同一条路）
            条目.Add(($"走过去（{实体.列},{实体.行}）", () => 点格反馈(实体.列, 实体.行)));
        }
        条目.Add(("查看", () => 查看(实体)));
        右键菜单.实例.显示动作(条目, 框);
    }

    // 楼体单格（相对建筑包围盒的左上角摆）——**不吃点击**：让点击落到交互层，面板才知道被点的是哪一格
    private Image 摆楼格(Transform 父, int 列偏移, int 行偏移)
    {
        var 图 = UI工具.创建图(父, $"楼_{列偏移}_{行偏移}", 区域贴图.楼体格(), Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f));
        UI工具.设锚(图.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(列偏移 * 格尺寸, -行偏移 * 格尺寸), new Vector2(格尺寸, 格尺寸));
        图.raycastTarget = false;
        return 图;
    }

    // 楼的外轮廓：只在"邻居不是本栋楼"的边补一条暗线（L 型的凹口、转角都自然闭合）
    private void 建楼边(Transform 格根, 网格实体 楼, int c, int r)
    {
        int 列 = 楼.列 + c, 行 = 楼.行 + r;
        if (!楼.覆盖(列, 行 - 1)) 建线条(格根, "边上", new Vector2(0.5f, 1f), new Vector2(格尺寸, 3f));
        if (!楼.覆盖(列, 行 + 1)) 建线条(格根, "边下", new Vector2(0.5f, 0f), new Vector2(格尺寸, 3f));
        if (!楼.覆盖(列 - 1, 行)) 建线条(格根, "边左", new Vector2(0f, 0.5f), new Vector2(3f, 格尺寸));
        if (!楼.覆盖(列 + 1, 行)) 建线条(格根, "边右", new Vector2(1f, 0.5f), new Vector2(3f, 格尺寸));
    }

    private void 查看(网格实体 实体)
    {
        string 文本;
        if (实体.类型 == 网格实体类型.建筑)
        {
            var 门 = 区域生成器.建筑入口格(实体);
            文本 = $"{实体.名称}：门长在楼自己身上，在（{门.列},{门.行}）那一格——点那一格就走过去，走上去就进楼。";
        }
        else if (实体.类型 == 网格实体类型.门)
            文本 = "街口。走出去就回到废城街上——这一片街区里的一切都留在原地，下次进来还是这副样子。";
        else if (实体.类型 == 网格实体类型.敌人)
            文本 = $"{实体.名称}：它在街上晃。走到它旁边，它就扑上来。";
        else 文本 = $"{实体.名称}：挡在路中间，绕过去就行。";
        ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.探索, 文本));
    }

    // 街边（区域边界墙）的转角闭合：只在与"非墙"相邻的边描暗边（和房间层同一套规矩）
    private void 建街边(Transform 父, 网格实体 墙, float 宽, float 高)
    {
        var 区 = 区域;
        if (区 == null || 墙 == null) return;
        if (!是墙(区, 墙.列, 墙.行 - 1)) 建线条(父, "边上", new Vector2(0.5f, 1f), new Vector2(宽, 3f));
        if (!是墙(区, 墙.列, 墙.行 + 1)) 建线条(父, "边下", new Vector2(0.5f, 0f), new Vector2(宽, 3f));
        if (!是墙(区, 墙.列 - 1, 墙.行)) 建线条(父, "边左", new Vector2(0f, 0.5f), new Vector2(3f, 高));
        if (!是墙(区, 墙.列 + 1, 墙.行)) 建线条(父, "边右", new Vector2(1f, 0.5f), new Vector2(3f, 高));
    }
}
