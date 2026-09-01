using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 制作面板：安全屋 制作 家具（工作台/灶台/医疗站）共用——完全由 安全屋面板 管控（子面板，非 面板管理器 独立面板）。
// 场景手动搭建（与 建造面板 同模式）：制作面板 物体（挂 本组件，初始 inactive，放 安全屋面板 下）
//   + 标题（家具名·制作）+ 内容区（配方行 容器）+ 配方行模板（挂 配方行：物品图/名称/选中背景）
//   + 详情文本（选中 配方 详情）+ 制作按钮——全部 引用位 拖入。
// 制作动画：点制作 → 扣材料/精力 → 播放进度条（时长 = 制作时间分 ÷ 10 现实秒；破损 ×2）+ 时间流逝文本
//           → 动画 期间 游戏 时间 随 进度 推进（HUD 时钟 同步 走）→ 完成后 结算（扣饱食/水分 + 产物 入包）。
//           制作 中 锁定 制作按钮 与 配方行；关闭/回退 = 取消 制作（回滚 材料/精力/已推 时间）。
// 打开：右键 家具「打开」→ 安全屋面板.打开制作(家具标识) → 本面板.打开(标识)：显示 + 注入 家具类型 + 刷新。
public sealed class 制作面板 : MonoBehaviour
{
    [SerializeField] private TMP_Text 标题;         // 标题：家具名 · 制作（场景 搭）
    [SerializeField] private RectTransform 内容区;  // 配方行 容器（场景 搭）
    [SerializeField] private GameObject 配方行模板; // 配方行 模板（挂 配方行：物品图/名称/选中背景）
    [SerializeField] private TMP_Text 详情文本;     // 选中 配方 详情（场景 搭）
    [SerializeField] private Button 制作按钮;       // 制作 按钮（场景 搭；interactable = 可制作）
    [SerializeField] private Button 关闭按钮;       // 关闭 按钮（场景 搭；点击 = 关闭 制作面板，回 营地）

    private 工作台制作服务 制作 => ServiceRegistry.Get<工作台制作服务>();
    private DataService 数据 => ServiceRegistry.Get<DataService>();

    private string 家具标识;        // 打开 时 注入："工作台"/"灶台"/"医疗站"
    private string 选中配方标识;

    // ===== 制作动画 状态（代码 动态 建 进度条，不 依赖 场景 手动 搭） =====
    private bool 制作中;
    private 工作台制作服务.制作上下文 当前制作;
    private Image 进度条;           // 进度条（Fill 型：fillAmount 0→1）
    private TMP_Text 制作中文本;    // "制作中…（推进 X 分钟）"
    private GameObject 动画根;      // 进度条 + 文本 的 容器（制作 时 显示，完成 隐藏）
    private float 已推分钟;         // 动画 期间 已 推进 的 游戏 分钟（取消 时 回滚）

    void Awake()
    {
        // 背包变化（扣材料/得产物）→ 面板 激活 时 刷新（材料 数量/可制作 状态 更新）
        ServiceRegistry.Get<EventBus>()?.订阅<背包变化事件>(_ =>
        {
            if (gameObject.activeSelf && !制作中) 刷新();
        });
        // 制作 按钮：绑定 一次（点击 时 读 当前 选中 配方）+ 注册 点击 音效（自动 挂钩）
        if (制作按钮 != null)
        {
            制作按钮.onClick.AddListener(点击制作);
            音效管理器.实例?.注册按钮(制作按钮);   // 点击 制作 按钮 → 点击 音效（本帧 失败 时 自动 跳过）
        }
        // 关闭 按钮：绑定 一次（点击 = 关闭 制作面板，回 营地 视图）
        if (关闭按钮 != null)
        {
            关闭按钮.onClick.AddListener(() =>
            {
                音效管理器.实例?.播放成功();
                关闭();
            });
        }
    }

    // 打开（安全屋面板 调用）：注入 家具类型 + 显示 + 刷新
    public void 打开(string 家具标识)
    {
        this.家具标识 = 家具标识;
        gameObject.SetActive(true);
        取消制作动画();
        刷新();
    }

    // 关闭（安全屋面板 调用：隐藏面板/回退 时）
    public void 关闭()
    {
        取消制作动画();   // 制作 中 关闭：回滚 已扣 材料/精力/已推 时间
        gameObject.SetActive(false);
    }

    // 制作 中 取消：回滚 扣费（材料/精力/已推 时间 返还），隐藏 动画
    private void 取消制作动画()
    {
        if (制作中 && 当前制作 != null) 回滚制作();
        制作中 = false;
        当前制作 = null;
        已推分钟 = 0f;
        if (动画根 != null) 动画根.SetActive(false);
    }

    // 回滚 已扣 材料/精力/已推 时间（制作 中途 取消——面板 关闭/回退 时）
    private void 回滚制作()
    {
        if (当前制作?.配方 == null) return;
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null) return;
        var 配方 = 当前制作.配方;
        if (配方.材料 != null)
            foreach (var 材 in 配方.材料)
                玩家.持有管理.放入物品(材.物品, 材.数量);
        玩家.生存管理.恢复行动点(配方.消耗精力);
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
            行.绑定(配方.产物, $"{物品名(配方.产物)} ×{配方.产物数量}", 选中, () => 选中配方(标识));
        }
        刷新详情();
    }

    private void 选中配方(string 标识)
    {
        if (制作中) return;   // 制作 中：锁定 配方 选择
        选中配方标识 = 标识;
        刷新();   // 整面板 重建（行 选中 标记 + 详情 刷新）
    }

    // 详情 文本 + 制作 按钮 状态（可制作 = 未满足原因 为空）
    private void 刷新详情()
    {
        if (详情文本 == null) return;
        if (string.IsNullOrEmpty(选中配方标识) || !数据.配方.TryGetValue(选中配方标识, out var 配方))
        {
            详情文本.text = "点击左侧配方查看详情。";
            设制作按钮状态(false);
            return;
        }
        string 图纸 = string.IsNullOrEmpty(配方.解锁图纸) ? "" : $"\n（需图纸：{物品名(配方.解锁图纸)}）";
        int 倍率 = 制作.破损倍率(家具标识);
        string 耗时 = 倍率 > 1 ? $"{配方.制作时间分 * 倍率} 分钟（破损 ×2）" : $"{配方.制作时间分} 分钟";
        float 消耗 = 制作.消耗预期(配方);
        详情文本.text = $"【{物品名(配方.产物)} ×{配方.产物数量}】\n\n{配方.描述}\n\n材料：{制作.材料文本(配方)}\n耗时：{耗时}　精力：{配方.消耗精力}{图纸}\n饱食/水分：-{消耗:F1}";
        设制作按钮状态(制作.未满足原因(家具标识, 配方).Length == 0);
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
        var 上下文 = 制作.开始制作(选中配方标识);   // 校验 + 扣 材料/精力；失败 null（已 提示）
        if (上下文 == null)
        {
            音效管理器.实例?.播放失败();
            刷新();
            return;
        }
        当前制作 = 上下文;
        制作中 = true;
        已推分钟 = 0f;
        确保动画元素();
        开始播放动画(上下文);
        刷新();
    }

    // ===== 制作动画（进度条 + 时间 流逝） =====

    private void 确保动画元素()
    {
        if (动画根 != null) return;
        动画根 = new GameObject("制作动画", typeof(RectTransform));
        动画根.transform.SetParent(transform, false);
        动画根.transform.SetAsLastSibling();
        var 根矩 = 动画根.GetComponent<RectTransform>();
        UI工具.设锚(根矩, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 120f));
        // 底条（灰）
        var 底 = UI工具.创建<Image>(动画根.transform, "底条", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        底.raycastTarget = false;
        底.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        UI工具.设锚(底.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 18f));
        // 进度（Fill：fillAmount 0→1；锚 居中）
        进度条 = UI工具.创建<Image>(动画根.transform, "进度", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        进度条.raycastTarget = false;
        进度条.color = 网格面板配色.放置可色;
        进度条.type = Image.Type.Filled;
        进度条.fillMethod = Image.FillMethod.Horizontal;
        进度条.fillOrigin = 0;
        进度条.fillAmount = 0f;
        UI工具.设锚(进度条.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 18f));
        // 制作中 文本
        制作中文本 = UI工具.创建文本(动画根.transform, "文本", "", 22f, TextAlignmentOptions.Center);
        UI工具.设锚(制作中文本.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -45f), new Vector2(460f, 30f));
        动画根.SetActive(false);
    }

    private void 开始播放动画(工作台制作服务.制作上下文 上下文)
    {
        动画根.SetActive(true);
        // 动画 时长：制作时间分 ÷ 10 现实秒（用户 设定：每 10 游戏分钟 = 1 现实秒）
        float 动画秒 = Mathf.Max(0.5f, 上下文.总分钟 / 10f);
        if (制作中文本 != null) 制作中文本.text = $"制作中…（推进 {上下文.总分钟} 分钟）";
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
            if (进度条 != null) 进度条.fillAmount = 进度;
            if (玩家 != null && 进度 > 上次进度)
            {
                float 段分钟 = 上下文.总分钟 * (进度 - 上次进度);
                已推分钟 += 段分钟;
                时间管理器?.跳时(段分钟);   // 推进 时间 + 跨天 检查 + 同步 整点 基准（制作 时间 不 重复 结算）
            }
            上次进度 = 进度;
            yield return null;
        }
        if (进度条 != null) 进度条.fillAmount = 1f;
        // 结算：扣 饱食/水分 + 补 副作用（伤病/疲劳/寒潮）+ 产物 入包（时间 已 在 动画 中 推进 完毕）
        bool 成功 = 制作.结算制作(上下文);
        if (!成功) 音效管理器.实例?.播放失败();
        制作中 = false;
        当前制作 = null;
        已推分钟 = 0f;
        if (动画根 != null) 动画根.SetActive(false);
        刷新();   // 成功/失败 都 重建（材料/状态 变化）
    }

    private string 物品名(string 标识) => 数据.物品.TryGetValue(标识, out var 物) ? 物.标识 : 标识;
    private string 家具名(string 标识) => 数据.家具.TryGetValue(标识, out var 家具) ? 家具.名称 : 标识;
}
