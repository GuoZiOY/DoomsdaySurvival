using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================
// 探索网格面板（基类）：**格子层 UI 的共用外壳** —— 房间层 / 区域层 只填"实体长什么样、右键给什么菜单"。
// 搬上来的是"格子层怎么表现与交互"：
//   · 实体框定位/尺寸/可见性骨架（显隐由 格子探索服务.档() 说了算，派生只管画）
//   · 点一格的统一反馈（音效 + 落点圈 + 路径当场改画 + 不可达闪红）
//   · 每格点击/悬停 转发入口（房间格点击 / 区域格点击 转发的就是这几个）
//   · 外观工具（摆满 / 摆居中 / 建名称 / 建线条 / 是墙 / 亮底）与「查看」说明项
//   · 拖拽三禁（格子层不搬东西）
// 派生必须给：探索服务（格子探索服务）、图层（探索图层接口），以及 网格面板基类 原有的抽象钩子。
// ⚠ 本类**绝对不能写 void Update()**：基类（网格面板基类）的 Update 是 private，子类再写一个会隐藏它，
//   基类的刷新调度（待刷新网格/待刷新物品/拖拽）会整个失效。每帧逻辑一律放 图层。
// ============================================================

// 图层最小接口：房间图层 / 区域图层 各自实现（悬停 / 闪红 / 落点 / 刷新 / 窗口可见性 / 取格框）
public interface 探索图层接口
{
    void 悬停(int 列, int 行);
    void 闪红(int 列, int 行);
    void 落点(int 列, int 行);
    void 刷新();
    bool 在窗口内(int 列, int 行);   // 相机裁掉的格子：mask 不裁 raycast，得自己拒绝
    RectTransform 格位框(int 列, int 行);   // 该格在"交互层"里那张透明图（右键菜单贴着它弹）
}

public abstract class 探索网格面板 : 网格面板基类
{
    // 玩家令牌的位置由 图层 平滑接管（否则基类增量刷新会把它一把拽回格子中心）
    [NonSerialized] public bool 接管玩家位置;

    // 相机视口（窗口大小）：由宿主面板的 `视口` 字段写进来。网格比它大就跟着令牌滚动；装得下就按网格铺满
    [NonSerialized] public Vector2 视口 = new Vector2(1720f, 960f);

    // 基类四层里的两层：图层 判断"物品层序号变了要重排 / 底座层要重建"时用
    public RectTransform 物品层引用 => 物品层;
    public RectTransform 底座层引用 => 底座层;

    // —— 派生给 ——
    protected abstract 格子探索服务 探索服务 { get; }   // 房间：房间探索服务 / 区域：区域探索服务
    public abstract 探索图层接口 图层();

    protected 网格数据 世界 => 探索服务?.当前世界;

    // 取某实体（按实例标识）的可见框——图层 用它平滑驱动玩家令牌
    public RectTransform 实体框矩形(string 标识)
    {
        var 网格 = 探索服务?.网格;
        if (网格 == null || string.IsNullOrEmpty(标识)) return null;
        foreach (var 堆叠 in 网格.网格物品)
            if (堆叠 != null && 堆叠.标识 == 标识) return 物品框矩形(堆叠);
        return null;
    }

    // ================= 交互（共用） =================

    // 点实体 = 走到它的**相邻格**（与点空格同一条反馈路径）。点墙/建筑也走这里：
    // 走得过去 = 站到它边上，走不过去 = 「那边过不去」+ 错误音 + 闪红（不再是"点了没反应"）
    protected override void 单击实体(物品堆叠 堆叠, int 点击次数)
    {
        var 实体 = 找实体(堆叠?.标识);
        if (实体 == null) return;
        点格反馈(实体.列, 实体.行);
    }

    // 格子层不搬东西（基类拖拽机制自然休眠：投影/跨面板都不用管）
    protected override bool 完成拖拽(PointerEventData 事件, 物品堆叠 堆叠) => false;
    protected override bool 允许跨面板() => false;
    protected override bool 允许开始拖拽(物品堆叠 堆叠) => false;

    // ================= 空格点击（由 格点击 组件转发） =================

    public void 左键格(int 列, int 行) => 点格反馈(列, 行);

    public virtual void 右键格(int 列, int 行)
    {
        // 空格右键默认不弹菜单（右键菜单只给"实体"）——区域层要用地块菜单就 override
    }

    public void 悬停格(int 列, int 行)
    {
        var 层 = 图层();
        if (层 != null && !层.在窗口内(列, 行)) return;
        层?.悬停(列, 行);
    }

    public void 离开格(int 列, int 行) => 图层()?.悬停(-1, -1);

    // 点一格的统一反馈（空格 / 实体 都走这里）——**点下去当场就有反应**，不等这一格走完：
    //   ① 音效（走得了 = 成功音；走不了 = 错误音）；
    //   ② 落点圈（效果层在目标格立刻弹一圈，0.30 秒淡出）；
    //   ③ 路径立刻改画 + 令牌当场拐弯（服务 点格 直接换路径；视觉由 图层.推进移动 连续接管）。
    protected void 点格反馈(int 列, int 行)
    {
        var 服 = 探索服务;
        // —— 诊断（这几条只在"点了没反应"时才会打，正常游玩不会刷屏）——
        if (服 == null) { Debug.LogWarning($"[探索] 收到点击 ({列},{行})，但探索服务未装配（GameBootstrap 装配失败？）"); return; }
        if (!服.探索中) { Debug.LogWarning($"[探索] 收到点击 ({列},{行})，但服务不在探索中（还没进入？）"); return; }
        // 相机裁掉的格子不响应点击（RectMask2D 只裁画面，raycast 照样能打到被裁掉的格子）
        var 层 = 图层();
        if (层 != null && !层.在窗口内(列, 行)) return;
        bool 成 = 服.点格(列, 行);
        if (成)
        {
            音效管理器.实例?.播放成功();
            图层()?.落点(列, 行);
        }
        else
        {
            // 失败要喊清楚原因（"点了没反应"里最难查的就是"点进来了但服务拒绝"）
            var 世界 = 服.当前世界;
            Debug.LogWarning($"[探索] 点格 ({列},{行}) 没有路：玩家在 ({服.玩家列},{服.玩家行})，"
                           + $"该格占位[{(世界 != null ? (世界.占位(列, 行)?.名称 ?? "无") : "无世界")}]，"
                           + $"可通行[{(世界 != null ? 世界.可通行(列, 行).ToString() : "-")}]");
            音效管理器.实例?.播放失败();
            图层()?.闪红(列, 行);
        }
        图层()?.刷新();
    }

    // 菜单里的"说明项"：点一下就把原因写进日志（代替灰项，不改共享菜单代码）
    protected void 说明(string 文本)
    {
        音效管理器.实例?.播放失败();
        ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.警告, 文本));
    }

    // ================= 外观工具（共用） =================

    // 铺满整块（墙 / 建筑块：相邻格子的贴图无缝接在一起）
    protected static Image 摆满(Transform 父, string 名, Sprite 精灵, Color 色, float 宽, float 高)
    {
        var 图 = UI工具.创建图(父, 名, 精灵, 色, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        UI工具.设锚(图.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(宽, 高));
        图.raycastTarget = false;
        return 图;
    }

    // 居中摆放（令牌 / 剪影用）
    protected static Image 摆居中(Transform 父, string 名, Sprite 精灵, float 边长, Vector2 偏移)
    {
        var 图 = UI工具.创建图(父, 名, 精灵, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        UI工具.设锚(图.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 偏移, new Vector2(边长, 边长));
        图.raycastTarget = false;
        return 图;
    }

    protected static TMP_Text 建名称(Transform 父, string 文本, Vector2 锚点, Vector2 尺寸, float 字号)
    {
        var 名 = UI工具.创建文本(父, "名称", 文本, 字号, TextAlignmentOptions.Center);
        UI工具.设锚(名.rectTransform, 锚点, 锚点, Vector2.zero, 尺寸);
        名.enableWordWrapping = true;
        名.color = new Color(1f, 1f, 1f, 0.72f);
        名.raycastTarget = false;
        return 名;
    }

    protected static void 建线条(Transform 父, string 名, Vector2 锚点, Vector2 尺寸)
    {
        var 图 = UI工具.创建图(父, 名, null, new Color(0f, 0f, 0f, 0.8f), 锚点, 锚点);
        UI工具.设锚(图.rectTransform, 锚点, 锚点, Vector2.zero, 尺寸);
        图.raycastTarget = false;
    }

    // 浅色剪影（冰箱/冷柜）→ 名称改用深色
    protected static bool 亮底(string 名称)
        => !string.IsNullOrEmpty(名称) && (名称.Contains("冰箱") || 名称.Contains("冷柜"));

    // 某格是不是墙（外圈墙的转角闭合 / 区域层的"建筑间隙"都用得着）
    protected static bool 是墙(网格数据 世界, int 列, int 行)
    {
        if (世界 == null || !世界.界内(列, 行)) return false;   // 界外 = 不是墙（外圈要描边，才有轮廓）
        var 实体 = 世界.格上实体(列, 行);
        return 实体 != null && 实体.类型 == 网格实体类型.墙;
    }

    // ================= 内部（共用） =================

    protected 网格实体 找实体(string 标识)
    {
        if (string.IsNullOrEmpty(标识) || 世界 == null) return null;
        return 世界.按标识(标识);
    }

    protected (int 宽, int 高) 格数(物品堆叠 堆叠)
    {
        var 网格 = 探索服务?.网格;
        return 网格 != null ? 网格.物品占格(堆叠) : (1, 1);
    }

    // 令牌上色：敌人/玩家按阵营色；其余贴图自带色（墙/容器/尸体/建筑）不再上色
    protected virtual Color 实体色(网格实体 实体)
    {
        if (实体 == null) return 网格面板配色.底座色;
        switch (实体.类型)
        {
            case 网格实体类型.敌人: return 网格面板配色.敌人令牌色;
            case 网格实体类型.玩家: return 网格面板配色.玩家令牌色;
            default: return Color.white;
        }
    }
}
