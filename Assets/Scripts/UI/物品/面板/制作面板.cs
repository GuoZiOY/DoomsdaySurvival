using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 制作面板：制作 家具（工作台/灶台/医疗站）的 浮动 操作面板（浮动面板基类 子类）——
// Canvas 顶层 浮动，可与 持有面板（背包）共存：材料 从 背包 拖入 输入区、产物 拖回 背包。
// 场景搭建：Resources/Prefab/制作面板.prefab（挂 本组件）+ 浮动壳（面板根/标题/关闭）
//   + 配方列表（选配方）+ 详情 + 制作按钮 + 输入网格（只收 当前 配方 材料）+ 输出网格（产物）。
// 会话 存储：输入/输出 = 面板 会话 网格服务（创建 时 建；材料 拖入 = 真实 移入 输入）
// 制作 流程：右键 家具「打开」→ 制作面板.创建（绑定 家具 实例）→ 选配方 → 拖材料 入 输入
//           → 点 制作（从 输入 扣 → 动画 → 产物 入 输出）；关闭 = 输入 剩料 返还 背包。
// 制作动画：进度条 + 时间 流逝（HUD 时钟 同步 走）。
public sealed class 制作面板 : 浮动面板基类
{
    private const string 预制体路径常量 = "Prefab/制作面板";   // Resources 路径（预制体 静态 搭建）

    [SerializeField] private RectTransform 面板根;   // 浮动壳：面板根（拖拽/置顶/限屏）
    [SerializeField] private TMP_Text 标题;         // 标题：家具名 · 制作（场景 搭）
    [SerializeField] private RectTransform 内容区;  // 配方行 容器（场景 搭）
    [SerializeField] private GameObject 配方行模板; // 配方行 模板（挂 配方行：物品图/名称/选中背景）
    [SerializeField] private TMP_Text 详情文本;     // 选中 配方 详情（场景 搭）
    [SerializeField] private Button 制作按钮;       // 制作 按钮（场景 搭；interactable = 可制作）
    [SerializeField] private Button 关闭按钮;       // 关闭 按钮（场景 搭；点击 = 关闭 面板）
    [SerializeField] private 制作输入网格 输入网格;   // 材料 输入 区（只收 当前 配方 材料）
    [SerializeField] private 制作输出网格 输出网格;   // 产物 输出 区（禁 拖入；产物 可 拖走）
    [SerializeField] private Slider 数量滑条;       // 一次 制作 份数（1..当前 最多；可空 = 固定 1 份）
    [SerializeField] private Button 减数量按钮;      // 份数 -1（可空）
    [SerializeField] private Button 加数量按钮;      // 份数 +1（可空）
    [SerializeField] private TMP_InputField 数量输入; // 直接 输入 制作 份数（可空）
    [SerializeField] private GameObject 进度根;     // 进度 条 父节点（控制 总体 显隐；可空 = 逐个 控 滑条/文本）
    [SerializeField] private Slider 进度滑条;       // 制作 进度 条（手动 搭：0→1；制作 中 只读）
    [SerializeField] private TMP_Text 进度文本;     // 制作 中 文本（手动 搭："制作中… X%（推进 X 分钟）"）

    protected override string 预制体路径 => 预制体路径常量;
    protected override RectTransform 根矩形 => 面板根;

    private 工作台制作服务 制作 => ServiceRegistry.Get<工作台制作服务>();
    private DataService 数据 => ServiceRegistry.Get<DataService>();

    private string 家具标识;        // 家具 实例 解码："工作台"/"灶台"/"医疗站"
    private string 选中配方标识;
    private int 制作数量 = 1;       // 一次 制作 份数（输入 材料 够 做 几 份 做 几 份）

    // 会话 网格（输入/输出 的 数据源：创建 建，关闭 清）
    private 网格服务 输入会话, 输出会话;

    // ===== 制作动画 状态（进度条 UI = 手动 搭：Slider 进度滑条 + 文本 进度文本） =====
    private bool 制作中;
    private 工作台制作服务.制作上下文 当前制作;
    private float 已推分钟;         // 动画 期间 已 推进 的 游戏 分钟（取消 时 回滚）

    // 背包变化 订阅 句柄（动态 面板：销毁 时 必须 取消 订阅，否则 残留 订阅 撞 已销毁 实例）
    private System.Action<背包变化事件> 背包变化订阅;

    // 静态 工厂：家具 右键「打开」→ 制作面板.创建（绑定 工作台/灶台/医疗站 家具 实例）
    public static 制作面板 创建(RectTransform 挂载父, 物品堆叠 家具实例)
        => 创建<制作面板>(挂载父, 家具实例, 预制体路径常量);

    void Awake()
    {
        // 背包变化（扣材料/得产物）→ 面板 激活 时 刷新（材料 数量/可制作 状态 更新）
        背包变化订阅 = _ =>
        {
            if (gameObject.activeSelf && !制作中) 刷新();
        };
        ServiceRegistry.Get<EventBus>()?.订阅(背包变化订阅);
        // 制作 按钮：绑定 一次（点击 时 读 当前 选中 配方）+ 注册 点击 音效（自动 挂钩）
        if (制作按钮 != null)
        {
            制作按钮.onClick.AddListener(点击制作);
            音效管理器.实例?.注册按钮(制作按钮);   // 点击 制作 按钮 → 点击 音效（本帧 失败 时 自动 跳过）
        }
        // 数量 控件：滑条 / 输入框 / 减 / 加（可空——没搭 = 固定 1 份）
        if (数量滑条 != null)
        {
            数量滑条.wholeNumbers = true;
            数量滑条.onValueChanged.AddListener(值 => 设数量((int)值));
        }
        if (数量输入 != null) 数量输入.onEndEdit.AddListener(输入提交);
        if (减数量按钮 != null) 减数量按钮.onClick.AddListener(() => 设数量(制作数量 - 1));
        if (加数量按钮 != null) 加数量按钮.onClick.AddListener(() => 设数量(制作数量 + 1));
        // 关闭 按钮：绑定 一次（点击 = 关闭 面板——浮动 面板 关闭 = 销毁 + 剩料 返还）
        if (关闭按钮 != null)
        {
            关闭按钮.onClick.AddListener(() =>
            {
                音效管理器.实例?.播放成功();
                关闭();   // 基类 关闭：清理内容（返还 剩料）→ 销毁
            });
        }
        隐藏进度UI();   // 进度 条 初始 隐藏（制作 时 才 显示）
    }

    // 初始化（基类 创建 调用）：家具 实例 → 家具 标识 + 会话 网格 + 刷新
    protected override void 初始化面板(物品堆叠 家具实例, RectTransform 挂载父)
    {
        当前容器 = 家具实例;
        if (家具实例 == null) return;
        var (定义标识, _) = 家具工具.解码(家具实例.标识);
        家具标识 = 定义标识;
        // 面板 宽高 不 覆盖：预制体 定 多少 就 多少（手搭 预制体）
        取消制作动画();
        创建会话网格();
        刷新();
    }

    // 关闭 安全 保护（基类 关闭 前 调）：制作 中 = 取消（回滚 精力/时间）+ 输入 剩料 与 输出 成品 自动 返还 玩家。
    // 玩家 侧 全满 放 不 回 → 拒绝 关闭（物品 留 会话 不 丢），提示 先 腾 空间。
    protected override bool 可否关闭()
    {
        取消制作动画();   // 制作 中 关闭 = 取消（材料 结算 才 扣，取消 时 未 扣——剩料 完整 在 输入 区）
        bool 输入全回 = 返还会话物品(输入会话);
        bool 输出全回 = 返还会话物品(输出会话);
        if (!输入全回 || !输出全回)
        {
            ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.警告, "背包/仓库已满，部分材料或成品未能放回——请先腾出空间再关闭制作面板。"));
            音效管理器.实例?.播放失败();
            // 数据源 未 断（清理会话网格 只在 真正 关闭 时）：刷新 网格 显示 回 会话 的 物品
            if (输入网格 != null) 输入网格.立即刷新();
            if (输出网格 != null) 输出网格.立即刷新();
            return false;
        }
        return true;
    }

    // 清理 内容（基类 关闭 通过 可否关闭 后、销毁 前 调）：残留 兜底 清 会话（正常 路径 已 全 返还）
    protected override void 清理内容()
    {
        取消制作动画();
        返还会话物品(输入会话);   // 兜底：若 有 绕过 可否关闭 的 销毁 路径，也 不 丢 物
        返还会话物品(输出会话);
        清理会话网格();
    }

    // 销毁：取消 背包变化 订阅（动态 面板 必须 解绑——否则 残留 订阅 撞 已销毁 实例）
    protected override void OnDestroy()
    {
        base.OnDestroy();   // 基类：解除 防重 登记
        if (背包变化订阅 != null)
        {
            ServiceRegistry.Get<EventBus>()?.取消订阅(背包变化订阅);
            背包变化订阅 = null;
        }
    }

    // 建 会话 网格（输入/输出 数据源：空 网格服务）
    private void 创建会话网格()
    {
        输入会话 = 新会话网格(4, 4);
        输出会话 = 新会话网格(4, 4);
        if (输入网格 != null)
        {
            输入网格.数据源 = 输入会话;
            输入网格.允许材料判定 = 当前允许材料;
            输入网格.立即刷新();
        }
        if (输出网格 != null)
        {
            输出网格.数据源 = 输出会话;
            输出网格.立即刷新();
        }
    }

    // 会话 网格服务（可放置 用 默认 形状 解析；列/行 固定）
    private static 网格服务 新会话网格(int 列, int 行)
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        var 服务 = new 网格服务 { 网格列 = 列, 网格行 = 行 };
        if (玩家 != null)
        {
            服务.形状解析 = 玩家.形状解析;
            服务.堆叠上限解析 = 玩家.堆叠上限解析;
            服务.有效最大耐久解析 = 标识 => 玩家.有效最大耐久(标识);
            服务.重量解析 = 玩家.重量解析;
        }
        return 服务;
    }

    // 输入 允许 材料 判定：当前 选中 配方 的 材料（未选 = 拒收）
    private bool 当前允许材料(string 标识)
    {
        if (string.IsNullOrEmpty(选中配方标识) || !数据.配方.TryGetValue(选中配方标识, out var 配方) || 配方.材料 == null) return false;
        foreach (var 材 in 配方.材料)
            if (材 != null && 材.物品 == 标识) return true;
        return false;
    }

    // 会话 物品 全量 自动 返还 玩家 持有（输入 剩料 / 输出 成品 共用）：跨 穿戴容器 → 仓库 兜底。
    // 返回 是否 全部 放回；放 不 回（玩家 全满）的 堆叠 保留 在 会话 网格（不 丢）。
    private bool 返还会话物品(网格服务 会话)
    {
        if (会话?.网格物品 == null) return true;
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null) return false;
        bool 全回 = true;
        for (int i = 会话.网格物品.Count - 1; i >= 0; i--)
        {
            var 堆叠 = 会话.网格物品[i];
            if (堆叠 == null || 堆叠.列 < 0) continue;
            // 从 会话 移除 → 统一 放入 玩家 持有（穿戴/仓库）
            会话.网格物品.RemoveAt(i);
            堆叠.列 = -1; 堆叠.行 = -1;
            int 实放 = 玩家.持有管理.放入堆叠(堆叠);   // 优先：整体 堆叠 实例（带 词缀/容器 数据）
            if (实放 <= 0)
            {
                实放 = 玩家.持有管理.放入物品(堆叠.标识, 堆叠.数量);   // 兜底：按标识 放（可 部分）
                if (实放 < 堆叠.数量)
                {
                    // 玩家 侧 放 不 下 全部：剩余 回 会话（不 丢）
                    堆叠.数量 -= 实放;
                    if (!会话.放入网格堆叠(堆叠)) { 会话.网格物品.Add(堆叠); }   // 兜底：列表 保留
                    全回 = false;
                }
            }
        }
        ServiceRegistry.Get<EventBus>()?.发布(new 背包变化事件("", 0, 变化原因.获得));
        return 全回;
    }

    // 清理 会话 网格（关闭）：数据源 置空
    private void 清理会话网格()
    {
        if (输入网格 != null) { 输入网格.数据源 = null; }
        if (输出网格 != null) { 输出网格.数据源 = null; }
        输入会话 = null;
        输出会话 = null;
    }

    // 制作 中 取消：回滚 扣费（材料/精力/已推 时间 返还），隐藏 进度 UI
    private void 取消制作动画()
    {
        if (制作中 && 当前制作 != null) 回滚制作();
        制作中 = false;
        当前制作 = null;
        已推分钟 = 0f;
        隐藏进度UI();
    }

    // 隐藏 制作 进度 UI：优先 父节点（进度根）整体 隐藏；未 搭 父节点 → 逐个 隐藏 滑条/文本
    private void 隐藏进度UI()
    {
        if (进度根 != null) { 进度根.SetActive(false); return; }
        if (进度滑条 != null) 进度滑条.gameObject.SetActive(false);
        if (进度文本 != null) 进度文本.gameObject.SetActive(false);
    }

    // 回滚 已扣 精力/已推 时间（制作 中途 取消——面板 关闭/回退 时）。
    // 会话 制作：材料 在 结算 才 扣（取消 时 未 扣 材料，输入 区 剩料 由 返还会话物品 处理）
    private void 回滚制作()
    {
        if (当前制作?.配方 == null) return;
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null) return;
        var 配方 = 当前制作.配方;
        玩家.生存管理.恢复行动点(配方.消耗精力 * Mathf.Max(1, 当前制作.数量));
        玩家.游戏分钟数 = Mathf.Max(0f, 玩家.游戏分钟数 - 已推分钟);
        ServiceRegistry.Get<世界时间管理器>()?.同步整点基准();   // 时间 回退 后 同步 整点 基准（防 错位）
        ServiceRegistry.Get<EventBus>()?.发布(new 背包变化事件("", 0, 变化原因.获得));
    }

    private void 刷新()
    {
        if (string.IsNullOrEmpty(家具标识) || 制作 == null) return;
        if (标题 != null) 标题.text = $"{家具名(家具标识)} · 制作";
        if (内容区 == null || 配方行模板 == null) return;
        面板基类.清空(内容区);
        var 配方列表 = 制作.配方列表(家具标识);
        if (配方列表.Length == 0)
        {
            面板基类.创建标签(内容区, "暂无配方");
        }
        foreach (var 配方 in 配方列表)
        {
            var 标识 = 配方.标识;
            bool 选中 = 标识 == 选中配方标识;
            var 行 = 面板基类.创建模板<配方行>(内容区, 配方行模板);
            if (行 == null) continue;
            行.绑定(配方.产物, 物品名(配方.产物), 选中, () => 选中配方(标识));   // 配方行 只 显示 名称（数量 在 详情）
        }
        刷新详情();
    }

    private void 选中配方(string 标识)
    {
        if (制作中) return;   // 制作 中：锁定 配方 选择
        if (选中配方标识 == 标识) return;
        // 切 配方：清 输入 区（旧 配方 材料 不 适用）→ 返还 背包；放 不 回（全满）→ 不 切，防 丢
        if (!返还会话物品(输入会话))
        {
            ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.警告, "背包/仓库已满，无法取回输入区材料——请先腾出空间。"));
            音效管理器.实例?.播放失败();
            if (输入网格 != null) 输入网格.立即刷新();
            return;
        }
        选中配方标识 = 标识;
        制作数量 = 1;   // 数量 重置
        if (输入网格 != null) 输入网格.立即刷新();   // 允许 材料 判定 更新
        刷新();   // 整面板 重建（行 选中 标记 + 详情 刷新）
    }

    // 详情 文本 + 数量 控件 + 制作 按钮 状态（会话 校验：材料 看 输入 区，非 全局 背包）
    private void 刷新详情()
    {
        if (详情文本 == null) return;
        if (string.IsNullOrEmpty(选中配方标识) || !数据.配方.TryGetValue(选中配方标识, out var 配方))
        {
            详情文本.text = "点击左侧配方查看详情。";
            设制作按钮状态(false);
            同步数量控件(null);
            return;
        }
        同步数量控件(配方);   // 数量 上限（材料/精力）→ 文本 用 当前 数量 显示
        string 图纸 = string.IsNullOrEmpty(配方.解锁图纸) ? "" : $"\n<color=#d9a441>（需图纸：{物品名(配方.解锁图纸)}）</color>";
        int 倍率 = 制作.破损倍率(家具标识);
        string 耗时 = 倍率 > 1 ? $"{配方.制作时间分 * 倍率 * 制作数量} 分钟（破损 ×2）" : $"{配方.制作时间分 * 制作数量} 分钟";
        // 详情（四行：标题/描述/材料/消耗；富文本：标题 金色粗体、标签 灰、图纸 金色）
        详情文本.text = $"<color=#d9a441><b>[{物品名(配方.产物)}] x {配方.产物数量 * 制作数量}</b></color>\n\n{配方.描述}\n\n<color=#888888>材料： </color>{制作.材料文本(配方, 制作数量)}\n<color=#888888>消耗： </color>耗时 {耗时}； 精力 {配方.消耗精力 * 制作数量}{图纸}";
        设制作按钮状态(制作.会话未满足原因(家具标识, 配方, 输入会话, 制作数量).Length == 0);
    }

    // 数量 上限 = min(输入 材料 能 做 几 份, 精力 能 撑 几 份)；同步 滑条/输入框/按钮；没 搭 控件 = 固定 1
    private void 同步数量控件(配方数据 配方)
    {
        int 上限 = 1;
        if (配方 != null && 输入会话 != null)
        {
            上限 = 制作.输入最多份数(配方, 输入会话);
            var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
            if (玩家 != null && 配方.消耗精力 > 0) 上限 = Mathf.Min(上限, 玩家.行动点 / 配方.消耗精力);
            上限 = Mathf.Max(1, 上限);
        }
        // 编辑 中 的 输入框 不 打断（失焦/回车 提交 时 才 写回）
        bool 输入聚焦 = 数量输入 != null && 数量输入.isFocused;
        if (!输入聚焦) 制作数量 = Mathf.Clamp(制作数量, 1, 上限);
        if (数量滑条 != null)
        {
            数量滑条.interactable = !制作中;
            数量滑条.minValue = 1;
            数量滑条.maxValue = 上限;
            数量滑条.value = 制作数量;   // 会 触发 onValueChanged → 设数量（同值 无 副作用）
        }
        if (!输入聚焦 && 数量输入 != null) 数量输入.text = 制作数量.ToString();
        if (数量输入 != null) 数量输入.interactable = !制作中;
        if (减数量按钮 != null) 减数量按钮.interactable = !制作中 && 制作数量 > 1;
        if (加数量按钮 != null) 加数量按钮.interactable = !制作中 && 制作数量 < 上限;
    }

    // 输入框 提交（回车/失焦）：解析 数量 → 夹 上限 → 回写（同步 滑条/按钮）
    private void 输入提交(string 文本)
    {
        if (制作中) { 同步数量控件(选中配方()); return; }
        int n;
        if (!int.TryParse(文本, out n)) n = 制作数量;   // 非 数字 → 回 原 值
        n = Mathf.Clamp(n, 1, 当前上限());
        制作数量 = n;
        刷新详情();   // 详情 按 新 数量 重算 + 同步 控件
    }

    private 配方数据 选中配方()
        => !string.IsNullOrEmpty(选中配方标识) && 数据.配方.TryGetValue(选中配方标识, out var 配方) ? 配方 : null;

    // 当前 数量 上限（输入框 提交 用；无 配方 = 1）
    private int 当前上限()
    {
        var 配方 = 选中配方();
        if (配方 == null || 输入会话 == null) return 1;
        int 上限 = 制作.输入最多份数(配方, 输入会话);
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 != null && 配方.消耗精力 > 0) 上限 = Mathf.Min(上限, 玩家.行动点 / 配方.消耗精力);
        return Mathf.Max(1, 上限);
    }

    // 数量 变更（滑条 / 加减 按钮）
    private void 设数量(int 新数量)
    {
        if (制作中) return;   // 制作 中：锁定 数量
        制作数量 = Mathf.Max(1, 新数量);
        刷新详情();   // 详情 材料/耗时 按 新 数量 重算（同步数量控件 会 夹 上限）
    }

    // 制作 按钮 状态：恒 可点（无法 制作 时 点击 → 失败 音效）；视觉 用 文字 颜色 区分（灰 = 不可 制作）
    private void 设制作按钮状态(bool 可制作)
    {
        if (制作按钮 == null) return;
        制作按钮.interactable = true;
        var 文字 = 制作按钮.GetComponentInChildren<TMP_Text>();
        if (文字 != null) 文字.color = 可制作 ? Color.white : 游戏主题.暗淡;
    }

    private void 点击制作()
    {
        if (制作中 || string.IsNullOrEmpty(选中配方标识)) return;
        // 会话 制作：材料 从 输入 区 扣（输入 网格 校验 足够）；数量 = 当前 滑条/加减 值
        var 上下文 = 制作.开始制作_会话(选中配方标识, 输入会话, 制作数量);
        if (上下文 == null)
        {
            音效管理器.实例?.播放失败();
            刷新();
            return;
        }
        当前制作 = 上下文;
        制作中 = true;
        已推分钟 = 0f;
        准备进度UI();
        开始播放动画(上下文);
        刷新();
    }

    // ===== 制作动画（进度条 = 手动 搭：Slider 进度滑条 0→1 + TMP_Text 进度文本） =====

    // 初始化 进度 UI（制作 开始 时）：父节点 显示 + 滑条 归 0 + 文本 显示 总量；引用 未 搭 = 无 视觉，仅 时间 流逝
    private void 准备进度UI()
    {
        if (进度根 != null) 进度根.SetActive(true);   // 父节点 控 总体 显隐（滑条/文本 为 其 子物体）
        else
        {
            if (进度滑条 != null) 进度滑条.gameObject.SetActive(true);
            if (进度文本 != null) 进度文本.gameObject.SetActive(true);
        }
        if (进度滑条 != null)
        {
            进度滑条.interactable = false;   // 进度 条：只读（不 让 玩家 拖）
            进度滑条.minValue = 0f;
            进度滑条.maxValue = 1f;
            进度滑条.value = 0f;
        }
        if (进度文本 != null)
        {
            进度文本.text = "制作中… 0%";
        }
    }

    private void 开始播放动画(工作台制作服务.制作上下文 上下文)
    {
        // 动画 时长：制作时间分 ÷ 10 现实秒（用户 设定：每 10 游戏分钟 = 1 现实秒）
        float 动画秒 = Mathf.Max(0.5f, 上下文.总分钟 / 10f);
        StartCoroutine(播放动画(动画秒, 上下文));
    }

    private IEnumerator 播放动画(float 动画秒, 工作台制作服务.制作上下文 上下文)
    {
        float 已过 = 0f;
        float 上次进度 = 0f;
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        var 时间管理器 = ServiceRegistry.Get<世界时间管理器>();   // 统一 时间 推进（跳时：只 推 时间 + 跨天；防 重复 结算）
        // 动画 期间：游戏 时间 随 进度 推进（视觉 同步 HUD 时钟）——时间 推进 全部 在 动画 内 完成
        while (已过 < 动画秒)
        {
            已过 += Time.deltaTime;
            float 进度 = Mathf.Clamp01(已过 / 动画秒);
            if (进度滑条 != null) 进度滑条.value = 进度;
            if (进度文本 != null) 进度文本.text = $"制作中… {Mathf.RoundToInt(进度 * 100f)}%";
            if (玩家 != null && 进度 > 上次进度)
            {
                float 段分钟 = 上下文.总分钟 * (进度 - 上次进度);
                已推分钟 += 段分钟;
                时间管理器?.跳时(段分钟);   // 推进 时间 + 跨天 检查 + 同步 整点 基准（制作 时间 不 重复 结算）
            }
            上次进度 = 进度;
            yield return null;
        }
        if (进度滑条 != null) 进度滑条.value = 1f;
        if (进度文本 != null) 进度文本.text = "制作完成…";
        // 结算（会话）：从 输入 扣 材料 → 产物 入 输出 网格（时间 已 在 动画 中 推进 完毕）
        bool 成功 = 制作.结算制作_会话(上下文, 输出会话);
        if (!成功) 音效管理器.实例?.播放失败();
        制作中 = false;
        当前制作 = null;
        已推分钟 = 0f;
        隐藏进度UI();
        刷新();   // 成功/失败 都 重建（材料/状态 变化）
    }

    private string 物品名(string 标识) => 数据.物品.TryGetValue(标识, out var 物) ? 物.标识 : 标识;
    private string 家具名(string 标识) => 数据.家具.TryGetValue(标识, out var 家具) ? 家具.名称 : 标识;
}
