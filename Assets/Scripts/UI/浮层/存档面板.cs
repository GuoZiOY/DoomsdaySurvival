using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 存档面板（**手搭版**）：槽位列表 + 保存 / 读取 / 删除。
//   场景里摆好整块（遮罩 / 底板 / 标题 / 提示 / 列表 / 关闭按钮 + 一个挂 `存档行` 的模板行），
//   本类只做三件事：写标题与提示、按槽克隆行、把按钮的可用性与文案算好交给 `存档行`。
//   入口：`打开(bool 读取模式)` —— 侧边栏「存档」(false) 与 主菜单「继续」(true) 各拖一份本组件进来。
//   面板根默认 **SetActive(false)**；打开 = 激活并置顶，关闭 = 失活（不走 `Destroy`，场景物体要复用）。
public sealed class 存档面板 : MonoBehaviour
{
    [SerializeField] private TMP_Text 标题;
    [SerializeField] private TMP_Text 提示;
    [SerializeField] private RectTransform 列表;
    [SerializeField] private Button 关闭按钮;
    [SerializeField] private GameObject 槽行模板;              // 挂 `存档行`；场景里默认 SetActive(false)

    private bool 读取模式;                                      // true = 主菜单「继续」进来（默认动作是读取）
    private int 待确认槽 = -1;                                  // 二次确认：读档/覆盖/删除都不可逆，第一次点只改成"再点一次"
    private string 待确认动作 = "";
    private readonly List<存档行> 行池 = new List<存档行>();    // 只增不减（槽位数固定）

    void Awake() { 关闭按钮?.onClick.AddListener(关闭); }

    public void 打开(bool 读取模式)
    {
        this.读取模式 = 读取模式;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();   // 全屏叠层：压住别的浮动面板
        重建();
    }

    public void 关闭() => gameObject.SetActive(false);

    // ================= 刷新（每次操作后重来） =================

    private void 重建()
    {
        if (标题 == null) return;
        标题.text = 读取模式 ? "读取存档" : "保存 / 读取";
        提示.text = 读取模式
            ? "选一个槽读取。（自动存档 是游戏自己写的兜底档）"
            : "选一个槽保存；「自动存档」槽由游戏自己写，手动保存不会覆盖它。";
        提示.color = 游戏主题.暗淡;
        // 门禁的原因优先显示（战斗中进来 / 未开局）：按钮会跟着变灰（见 `绑行们`），
        //   这里把"为什么灰"写清楚，别让玩家以为是 bug。
        string 门禁原因 = 读取模式 ? 存档门禁.读前检查() : 存档门禁.存前检查();
        if (门禁原因 != "") { 提示.text = 门禁原因; 提示.color = 游戏主题.危险; }
        待确认槽 = -1;
        待确认动作 = "";
        绑行们();
    }

    // 把当前槽位逐个绑到行上。
    //   ⚠ 门禁与可用性**每次重建都按当前状态重算**（不缓存）：面板是覆盖层，它开着的时候
    //     战斗可能在背后打起来 —— "打开面板时算过一次"不够。
    private void 绑行们()
    {
        var 存档 = ServiceRegistry.Get<SaveService>();
        var 全部 = 存档 != null ? 存档.列出() : new List<存档摘要>();
        for (int i = 0; i < 全部.Count; i++)
        {
            var 摘 = 全部[i];
            var 行 = 取行(i);
            if (行 == null) return;                       // 模板没挂 `存档行`：停手
            行.transform.SetSiblingIndex(i);
            行.gameObject.SetActive(true);

            bool 可读 = 存档门禁.可读 && 摘.有档 && string.IsNullOrEmpty(摘.损坏原因);
            bool 可写 = 存档门禁.可存 && 摘.槽 != 存档规格.自动槽号 && !读取模式;   // 自动槽不由玩家手动写（别把兜底档弄脏）
            bool 可删 = 摘.有档;
            行.绑定(
                存档名文本: 存档规格.槽名(摘.槽),
                摘要文本: 信息文本(摘),
                可读: 可读, 可写: 可写, 可删: 可删,
                读取文案: 待确认文本(摘.槽, "读"),
                保存文案: 待确认文本(摘.槽, "写"),
                删除文案: 待确认文本(摘.槽, "删"),
                读取: () => 点读取(摘),
                保存: () => 点保存(摘),
                删除: () => 点删除(摘));
        }
        收行(全部.Count);
    }

    private 存档行 取行(int 序)
    {
        while (行池.Count <= 序)
        {
            var 组件 = 面板基类.创建模板<存档行>(列表, 槽行模板);
            if (组件 == null) return null;
            行池.Add(组件);
        }
        return 行池[序];
    }

    private void 收行(int 起)
    {
        for (int i = 起; i < 行池.Count; i++) if (行池[i] != null) 行池[i].gameObject.SetActive(false);
    }

    private string 待确认文本(int 槽, string 动作)
        => (待确认槽 == 槽 && 待确认动作 == 动作) ? "再点一次" : (动作 == "读" ? "读取" : 动作 == "写" ? "保存" : "删除");

    // 信息块（四行，用户定稿）：名称 / 职业 / 等级 / 最后游玩时间。
    //   空槽与坏档各写一句就够 —— 摆四行空标签（"等级："后面空着）比不写更难看。
    private static string 信息文本(存档摘要 摘)
    {
        if (!摘.有档) return "—— 空 ——";
        if (!string.IsNullOrEmpty(摘.损坏原因)) return $"⚠ 读不了：{摘.损坏原因}";
        string 职业 = string.IsNullOrEmpty(摘.职业) ? "无职业" : 摘.职业;
        return $"名称：{摘.角色名}\n职业：{职业}\n等级：{摘.等级}\n最后游玩时间：{存档文件.现实时间文本(摘.保存时间)}";
    }

    // ================= 三个动作 =================

    private void 点读取(存档摘要 摘)
    {
        // 门禁**每次点击都查**：面板开着的时候战斗可能在背后打起来（按钮变灰是第一层，这里是第二层）。
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
        // 先读档、**成功了才关面板** —— 失败时要留在面板上把原因说清楚，而不是黑屏让人猜。
        var 玩家服务 = ServiceRegistry.Get<PlayerService>();
        if (玩家服务 == null) return;
        var 载入 = 玩家服务.读档(摘.槽);
        if (载入 == null)
        {
            // 被门禁拒（战斗中）/ 读不出来：**不动当前进度**，PlayerService 已播报原因
            提示.text = "读档没成功 —— 已保持当前进度不变（原因见日志）。";
            提示.color = 游戏主题.危险;
            音效管理器.实例?.播放失败();
            重建();
            return;
        }
        关闭();
        // 有战局快照就**回到你离开的那一层那一格**（含翻过的柜子/开过的门/敌人位置）。
        // 快照装不了时再看存档记的"层"——**在家存的档（层=安全屋）不建世界，直接回安全屋面板**：
        //   与"回安全屋会把大世界那层也收掉"是同一口径（在家 = 三层都没有活着的世界）。
        // 老档（没有战局快照）才走老路：用 世界种子 重建同一座废城，落营地门口。
        // 面板切换不用我们管：进入X() / 打开营地事件 各自会让面板管理器路由。
        if (!战局恢复.装回(载入.战局))
        {
            bool 在家 = 载入.战局 != null && 载入.战局.层 == 存档战局.层安全屋;
            if (在家) ServiceRegistry.Get<EventBus>()?.发布(new 打开营地事件());
            else ServiceRegistry.Get<大世界探索服务>()?.打开默认世界();
        }
        ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.系统, $"已读取「{存档规格.槽名(摘.槽)}」。"));
    }

    private void 点保存(存档摘要 摘)
    {
        // 门禁（未开局 / 战斗中）—— 与侧边栏、自动存档器同一份判据
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

    // 二次确认只改按钮文案：直接重绑行（池化，代价极小）。
    //   ⚠ **不能走 `重建()`** —— 那会把 `待确认槽/动作` 清掉，玩家永远确认不了。
    private void 重建按钮文字() => 绑行们();
}
