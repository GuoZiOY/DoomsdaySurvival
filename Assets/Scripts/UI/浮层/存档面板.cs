using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 存档面板（v52 刀64）：槽位列表 + 保存 / 读取 / 删除。
//
// ★ 为什么这个面板是**运行期用代码搭**的，而不是像其它面板那样在场景里手搭 + Inspector 拖引用：
//   本作其余面板都是「全静态场景搭建 + 拖拽绑定」。这个面板是刀64 新增的，而**代码改动没法替 Unity
//   在场景里建物体、拖引用** —— 手搭方案会留下"面板在代码里存在、在场景里不存在"的半成品状态
//   （按钮永远打不开）。所以这里自建一套最小 uGUI 层级：不改场景、不依赖任何 SerializeField，
//   挂上就能用。代价是风格与手搭面板不完全一致（间距/字号是这里定的）。
//   将来若要把它并进 面板管理器 的静态路由，只需保留 打开() 这个入口、把内部换成显示场景面板即可。
//
// 入口（唯一）：存档面板.打开(读取模式)。侧边栏「存档」按钮 与 主菜单「继续」按钮 都走它。
public sealed class 存档面板 : MonoBehaviour
{
    private const float 面板宽 = 760f;
    private const float 行高 = 104f;
    private const float 行距 = 10f;

    private bool 读取模式;                 // true = 主菜单「继续」进来（默认动作是读取）
    private RectTransform 列表;
    private TMP_Text 标题;
    private TMP_Text 提示;

    // 二次确认：删除是不可逆的玩家操作，第一次点只改成"再点一次确认"。
    private int 待确认槽 = -1;
    private string 待确认动作 = "";

    private static 存档面板 当前;

    // ================= 入口 =================

    public static void 打开(bool 读取模式)
    {
        if (当前 != null) { 当前.读取模式 = 读取模式; 当前.重建(); return; }

        var 宿主 = 找宿主();
        if (宿主 == null) { Debug.LogError("[存档面板] 找不到 Canvas，无法打开存档面板。"); return; }

        var 物体 = new GameObject("存档面板", typeof(RectTransform));
        物体.transform.SetParent(宿主, false);
        物体.transform.SetAsLastSibling();
        var 面板 = 物体.AddComponent<存档面板>();
        面板.读取模式 = 读取模式;
        面板.搭建();
        当前 = 面板;
    }

    private static RectTransform 找宿主()
    {
        var 管理器 = 面板管理器.实例;
        if (管理器 != null)
        {
            var 画布 = 管理器.GetComponentInParent<Canvas>();
            if (画布 != null) return (RectTransform)画布.transform;
        }
        var 任意 = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        return 任意 != null ? (RectTransform)任意.transform : null;
    }

    // ================= 搭建（只做一次） =================

    private void 搭建()
    {
        var 根 = (RectTransform)transform;
        UI工具.铺满(根);   // 铺满整块画布（遮罩层 + 居中面板都在里面）

        // 遮罩：挡住背后的点击（半透明黑，不关闭——防误触把面板关掉）
        var 遮罩 = UI工具.创建<Image>(根, "遮罩", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        UI工具.铺满(遮罩.rectTransform);
        遮罩.color = new Color(0f, 0f, 0f, 0.62f);
        遮罩.raycastTarget = true;

        // 底板
        var 底板 = UI工具.创建<Image>(根, "底板", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        底板.color = 游戏主题.面板;
        底板.raycastTarget = true;
        var 底色块 = 底板.gameObject.AddComponent<Outline>();
        底色块.effectColor = 游戏主题.金色;
        底色块.effectDistance = new Vector2(1.5f, -1.5f);
        UI工具.设锚(底板.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(面板宽, 240f + 槽数() * (行高 + 行距)));

        // 标题
        标题 = UI工具.创建文本(底板.transform, "标题", "存档", 30f, TextAlignmentOptions.Center);
        标题.color = 游戏主题.金色;
        UI工具.设锚((RectTransform)标题.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -34f), new Vector2(面板宽 - 48f, 44f));

        // 提示行（操作结果 / 当前模式说明）
        提示 = UI工具.创建文本(底板.transform, "提示", "", 18f, TextAlignmentOptions.Center);
        提示.color = 游戏主题.暗淡;
        UI工具.设锚((RectTransform)提示.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -70f), new Vector2(面板宽 - 48f, 30f));

        // 列表容器
        列表 = UI工具.创建物体(底板.transform, "列表", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        UI工具.设锚(列表, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -100f), new Vector2(面板宽 - 40f, 槽数() * (行高 + 行距)));

        // 关闭
        建按钮(底板.transform, "关闭按钮", "关闭", 游戏主题.按钮底, () => 关闭(),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(180f, 44f));

        重建();
    }

    private static int 槽数() => 存档规格.手动槽数 + 1;   // 自动 + 手动

    // ================= 刷新（每次操作后重来） =================

    private void 重建()
    {
        if (标题 == null) return;
        标题.text = 读取模式 ? "读取存档" : "保存 / 读取";
        提示.text = 读取模式
            ? "选一个槽读取。（自动存档 是游戏自己写的兜底档）"
            : "选一个槽保存；「自动存档」槽由游戏自己写，手动保存不会覆盖它。";
        if (提示.color != 游戏主题.暗淡) 提示.color = 游戏主题.暗淡;
        // ★ 刀64：门禁的原因优先显示（战斗中进来 / 未开局）——
        //   按钮会跟着变灰（见 建槽行），这里把"为什么灰"写清楚，别让玩家以为是 bug。
        string 门禁原因 = 读取模式 ? 存档门禁.读前检查() : 存档门禁.存前检查();
        if (门禁原因 != "") { 提示.text = 门禁原因; 提示.color = 游戏主题.危险; }
        清空列表();
        待确认槽 = -1;
        待确认动作 = "";

        var 存档 = ServiceRegistry.Get<SaveService>();
        List<存档摘要> 全部 = 存档 != null ? 存档.列出() : new List<存档摘要>();
        float y = 0f;
        foreach (var 摘 in 全部)
        {
            建槽行(摘, y);
            y -= 行高 + 行距;
        }
    }

    private void 清空列表()
    {
        // 先 SetParent(null) 再 Destroy：Destroy 是**帧末**才生效的，同一帧里连调两次 重建()
        // 会让旧行还挂在列表上 → 列表翻倍。摘出去就不会。
        for (int i = 列表.childCount - 1; i >= 0; i--)
        {
            var 子 = 列表.GetChild(i).gameObject;
            子.transform.SetParent(null, false);
            Destroy(子);
        }
    }

    private void 建槽行(存档摘要 摘, float 顶距)
    {
        const float 行宽 = 面板宽 - 40f;
        var 行 = UI工具.创建<Image>(列表, "行", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        行.color = 游戏主题.背景;
        行.raycastTarget = true;
        UI工具.设锚(行.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, 顶距), new Vector2(行宽, 行高));

        // 三个按钮的右区起点（锚在行的左中，所以是正数；3×84 + 2×10 间隔 + 12 右边距）
        const float 按钮区起点 = 行宽 - 3f * 84f - 2f * 10f - 12f;

        // 左：槽名 + 摘要
        var 左 = UI工具.创建文本(行.transform, "摘要", 槽位文本(摘), 19f, TextAlignmentOptions.TopLeft);
        左.color = 摘.有档 && string.IsNullOrEmpty(摘.损坏原因) ? 游戏主题.文字 : 游戏主题.暗淡;
        UI工具.设锚((RectTransform)左.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, -12f), new Vector2(按钮区起点 - 24f, 行高 - 20f));

        // 右：三个按钮
        // ★ 刀64：按钮可用性也要过门禁 —— 面板是运行期叠层，它开着的时候战斗可能在背后打起来，
        //   所以"能点"这件事必须在**每次重建**时按当前状态重算（不只在打开面板时算一次）。
        bool 门禁可写 = 存档门禁.可存;
        bool 门禁可读 = 存档门禁.可读;
        bool 可读 = 门禁可读 && 摘.有档 && string.IsNullOrEmpty(摘.损坏原因);
        bool 可写 = 门禁可写 && 摘.槽 != 存档规格.自动槽号;   // 自动槽不由玩家手动写（避免玩家把兜底档弄脏）
        var 锚 = new Vector2(0f, 0.5f);
        var pivot = new Vector2(0f, 0.5f);
        建按钮(行.transform, "读取", 待确认文本(摘.槽, "读"), 游戏主题.按钮底,
            () => 点读取(摘), 锚, pivot, new Vector2(按钮区起点, 0f), new Vector2(84f, 40f), 可读);
        建按钮(行.transform, "保存", 待确认文本(摘.槽, "写"), 游戏主题.按钮底,
            () => 点保存(摘), 锚, pivot, new Vector2(按钮区起点 + 94f, 0f), new Vector2(84f, 40f), 可写 && !读取模式);
        建按钮(行.transform, "删除", 待确认文本(摘.槽, "删"), 游戏主题.危险,
            () => 点删除(摘), 锚, pivot, new Vector2(按钮区起点 + 188f, 0f), new Vector2(84f, 40f), 摘.有档);
    }

    private string 待确认文本(int 槽, string 动作)
        => (待确认槽 == 槽 && 待确认动作 == 动作) ? "再点一次" : (动作 == "读" ? "读取" : 动作 == "写" ? "保存" : "删除");

    private static string 槽位文本(存档摘要 摘)
    {
        string 名 = 存档规格.槽名(摘.槽);
        if (!摘.有档) return $"{名}\n—— 空 ——";
        if (!string.IsNullOrEmpty(摘.损坏原因)) return $"{名}\n⚠ 读不了：{摘.损坏原因}";
        string 职业 = string.IsNullOrEmpty(摘.职业) ? "无职业" : 摘.职业;
        string 时间 = 存档文件.现实时间文本(摘.保存时间);
        string 位置 = string.IsNullOrEmpty(摘.位置) ? "未知" : 摘.位置;
        return $"{名}   {摘.角色名} · {职业} · Lv{摘.等级} · 第 {摘.游戏天数} 天 · {位置}\n{时间}（v{摘.版本}）";
    }

    // ================= 三个动作 =================

    private void 点读取(存档摘要 摘)
    {
        // ★ 刀64：门禁每次点击都查 —— 面板开着的时候战斗可能在背后打起来，
        //   "打开面板时查过一次"不够（按钮变灰是第一层，这里是第二层）。
        string 拒读 = 存档门禁.读前检查();
        if (拒读 != "") { 提示.text = 拒读; 提示.color = 游戏主题.危险; 音效管理器.实例?.播放失败(); 重建(); return; }
        if (!(待确认槽 == 摘.槽 && 待确认动作 == "读"))
        {
            待确认槽 = 摘.槽; 待确认动作 = "读";
            提示.text = $"读取「{存档规格.槽名(摘.槽)}」会**丢弃当前未保存的进度**。再点一次「再点一次」确认。";
            提示.color = 游戏主题.危险;
            重建按钮文字();
            return;
        }
        关闭();
        var 玩家服务 = ServiceRegistry.Get<PlayerService>();
        if (玩家服务 == null) return;
        玩家服务.读档(摘.槽);
        // 读档后必须重建世界（v51 口径：世界结构 = 世界种子 的纯函数）。
        // 切面板不用我们管：进入大世界 会发 打开大地图事件，面板管理器 自己路由到 大世界面板
        // （顺带把 常驻UI / HUD 打开）。位置恢复是 刀65 的事。
        ServiceRegistry.Get<大世界探索服务>()?.打开默认世界();
        ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.系统, $"已读取「{存档规格.槽名(摘.槽)}」。"));
    }

    private void 点保存(存档摘要 摘)
    {
        // ★ 刀64：门禁（未开局 / 战斗中）—— 与侧边栏、自动存档器同一份判据，见 存档门禁
        string 拒存 = 存档门禁.存前检查();
        if (拒存 != "") { 提示.text = 拒存; 提示.color = 游戏主题.危险; 音效管理器.实例?.播放失败(); 重建(); return; }
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null) { 提示.text = "取不到玩家档案。"; 提示.color = 游戏主题.危险; 音效管理器.实例?.播放失败(); return; }
        if (摘.有档 && !(待确认槽 == 摘.槽 && 待确认动作 == "写"))
        {
            待确认槽 = 摘.槽; 待确认动作 = "写";
            提示.text = $"「{存档规格.槽名(摘.槽)}」已有存档，保存会**覆盖**它。再点一次「再点一次」确认。";
            提示.color = 游戏主题.危险;
            重建按钮文字();
            return;
        }
        var 存档 = ServiceRegistry.Get<SaveService>();
        if (存档 == null) return;
        if (存档.保存(摘.槽, 玩家))
        {
            音效管理器.实例?.播放成功();
            ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.系统, $"已保存到「{存档规格.槽名(摘.槽)}」。"));
            重建();
            提示.text = $"已保存到「{存档规格.槽名(摘.槽)}」。";
            提示.color = 游戏主题.成功;
        }
        else
        {
            音效管理器.实例?.播放失败();
            提示.text = "保存失败（看 Console）。";
            提示.color = 游戏主题.危险;
        }
    }

    private void 点删除(存档摘要 摘)
    {
        if (!(待确认槽 == 摘.槽 && 待确认动作 == "删"))
        {
            待确认槽 = 摘.槽; 待确认动作 = "删";
            提示.text = $"删除「{存档规格.槽名(摘.槽)}」不可撤销（存档目录里会留一份 .bak）。再点一次确认。";
            提示.color = 游戏主题.危险;
            重建按钮文字();
            return;
        }
        ServiceRegistry.Get<SaveService>()?.删除(摘.槽);
        音效管理器.实例?.播放成功();
        重建();
        提示.text = $"已删除「{存档规格.槽名(摘.槽)}」。";
        提示.color = 游戏主题.成功;
    }

    // 二次确认只改按钮文字，不整块重建（重建会把 待确认 状态清掉）
    private void 重建按钮文字()
    {
        var 存档 = ServiceRegistry.Get<SaveService>();
        if (存档 == null) return;
        int 序 = 0;
        foreach (var 摘 in 存档.列出())
        {
            if (序 >= 列表.childCount) break;
            var 行 = 列表.GetChild(序);
            设按钮文字(行, "读取", 待确认文本(摘.槽, "读"));
            设按钮文字(行, "保存", 待确认文本(摘.槽, "写"));
            设按钮文字(行, "删除", 待确认文本(摘.槽, "删"));
            序++;
        }
    }

    private static void 设按钮文字(Transform 行, string 名, string 文字)
    {
        var 子 = 行.Find(名);
        if (子 == null) return;
        var 文本 = 子.GetComponentInChildren<TMP_Text>();
        if (文本 != null) 文本.text = 文字;
    }

    // ================= 关闭 / 输入 =================

    private void 关闭()
    {
        当前 = null;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (当前 == this) 当前 = null;
    }

    // ================= 控件工厂 =================

    private static void 建按钮(Transform 父, string 名, string 文字, Color 底色, Action 点击,
        Vector2 锚, Vector2 pivot, Vector2 位置, Vector2 尺寸, bool 可用 = true)
    {
        var 图 = UI工具.创建<Image>(父, 名, 锚, pivot);
        图.color = 底色;
        图.raycastTarget = true;
        UI工具.设锚(图.rectTransform, 锚, pivot, 位置, 尺寸);
        var 按钮 = 图.gameObject.AddComponent<Button>();
        按钮.targetGraphic = 图;
        按钮.interactable = 可用;
        if (可用) 按钮.onClick.AddListener(() => 点击());
        var 文本 = UI工具.创建文本(图.transform, "文字", 文字, 18f, TextAlignmentOptions.Center);
        文本.color = 可用 ? 游戏主题.文字 : new Color(游戏主题.暗淡.r, 游戏主题.暗淡.g, 游戏主题.暗淡.b, 0.55f);
        UI工具.铺满((RectTransform)文本.transform);
        音效管理器.实例?.注册按钮(按钮);
    }
}
