using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 制作面板：安全屋 制作 家具（工作台/灶台/医疗站）共用——完全由 安全屋面板 管控（子面板，非 面板管理器 独立面板）。
// 场景手动搭建（与 建造面板 同模式）：制作面板 物体（挂 本组件，初始 inactive，放 安全屋面板 下）
//   + 标题（家具名·制作）+ 内容区（配方行 容器）+ 配方行模板（挂 配方行：物品图/名称/选中背景）
//   + 详情文本（选中 配方 详情）+ 制作按钮——全部 引用位 拖入。
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

    void Awake()
    {
        // 背包变化（扣材料/得产物）→ 面板 激活 时 刷新（材料 数量/可制作 状态 更新）
        ServiceRegistry.Get<EventBus>()?.订阅<背包变化事件>(_ =>
        {
            if (gameObject.activeSelf) 刷新();
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
        刷新();
    }

    // 关闭（安全屋面板 调用：隐藏面板/回退 时）
    public void 关闭()
    {
        gameObject.SetActive(false);
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
        详情文本.text = $"【{物品名(配方.产物)} ×{配方.产物数量}】\n\n{配方.描述}\n\n材料：{制作.材料文本(配方)}\n耗时：{配方.制作时间分} 分钟　精力：{配方.消耗精力}{图纸}";
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
        if (string.IsNullOrEmpty(选中配方标识)) return;
        bool 成功 = 制作.尝试制作(选中配方标识);
        if (!成功) 音效管理器.实例?.播放失败();   // 无法 制作 → 失败 音效（成功 = 点击 音效 + 服务 内 家具放下）
        刷新();   // 成功/失败 都 重建（材料/状态 变化）
    }

    private string 物品名(string 标识) => 数据.物品.TryGetValue(标识, out var 物) ? 物.标识 : 标识;
    private string 家具名(string 标识) => 数据.家具.TryGetValue(标识, out var 家具) ? 家具.名称 : 标识;
}
