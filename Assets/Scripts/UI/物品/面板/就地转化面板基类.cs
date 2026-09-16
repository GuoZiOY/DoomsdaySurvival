using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 就地转化面板基类：**就地转化 家具**（种植箱 播种 / 晾晒架 晾晒）共用 的 面板 骨架 —— 网格 + 进度条 + 状态文本。
// 就地转化 = 家具 容器 里 的 物品 按 物品数据（转化用途 / 生长时间 / 成熟产物）随 游戏 时间 就地 变 成 产物：
//   晾晒架：生肉 12 游戏时 → 肉干 · 兽皮 24 → 皮革 ｜ 种植箱：土豆种子 24 → 土豆 · 蔬菜种子 48 → 蔬菜。
//   推进 = 世界时间管理器.生长推进（逐帧、挂机 也 走、按 家具 等级 加速 0/25/50%）。本面板 不 推进，只 显示 + 放取，
//   所以 进度 靠 0.5s 轮询 刷新 就 够（惰性 初始化/多件 各 自 计时/成熟 换 物品 全 在 推进器 那边）。
// 子类 只 给 两样东西：预制体 路径（两件 家具 的 容器 3×3 / 4×2，面板 尺寸 不同 → 各自 一个 预制体）与 文案。
// 面板 大小 以 预制体 为准（代码 不 覆盖 —— 与 容器面板 那种"按网格 自适应" 不同，这里 壳 是 手搭 的）。
// 场景搭建：预制体 挂 子类 组件 + 网格 对象 挂 就地转化网格；引用 面板根/标题文本/关闭按钮/转化网格/进度滑条/进度文本/状态文本。
public abstract class 就地转化面板基类 : 浮动面板基类
{
    // —— 预制体 引用（Inspector 拖好；字段名 = 两个 预制体 里 的 键，改名 必须 同步 改 预制体） ——
    [SerializeField] private RectTransform 面板根;
    [SerializeField] private TextMeshProUGUI 标题文本;
    [SerializeField] private Button 关闭按钮;
    [SerializeField] private 就地转化网格 转化网格;   // 家具 容器 的 网格（单株定根 由 本面板 按 家具功能类型 设）
    [SerializeField] private Slider 进度滑条;          // 进度（0→1；平均 —— 多件 各 自 计时，可空）
    [SerializeField] private TMP_Text 进度文本;        // 最早 一件 的 剩余 倒计时（可空）
    [SerializeField] private TMP_Text 状态文本;        // 进行中 / 可以收了 / 空着

    protected override RectTransform 根矩形 => 面板根;

    // —— 子类 覆写：文案（默认 = 种植箱 那套；晾晒架 覆写成 晾晒 的说法） ——
    protected virtual string 进行中文案 => "生长中…";
    protected virtual string 完成文案 => "可以收获了";
    protected virtual string 空闲文案 => "等待播种";
    protected virtual string 倒计时后缀 => "成熟";

    private System.Action<背包变化事件> 背包变化订阅;   // 背包变化（放料/收成品）→ 刷 状态
    private float 上次状态刷新;                          // 轮询 节流
    private bool 是晾晒;                                 // 初始化 时 按 家具数据.功能类型 定（文案 与 网格 都 看它）

    // Awake：关闭按钮 绑定 + 背包变化 订阅（子类 不必 再 写）
    protected virtual void Awake()
    {
        if (关闭按钮 != null)
        {
            关闭按钮.onClick.AddListener(关闭);
            音效管理器.实例?.注册按钮(关闭按钮);
        }
        背包变化订阅 = _ => 刷新状态();
        ServiceRegistry.Get<EventBus>()?.订阅(背包变化订阅);
    }

    // 轮询：转化 由 世界时间管理器 推进（挂机 也 走）——低频 刷新 显示（0.5s）
    protected virtual void Update()
    {
        if (Time.unscaledTime - 上次状态刷新 < 0.5f) return;
        上次状态刷新 = Time.unscaledTime;
        刷新状态();
    }

    // 初始化：绑 家具 容器 网格 + 标题；把 "种植 还是 晾晒" 告诉 网格（单株定根）
    protected override void 初始化面板(物品堆叠 家具, RectTransform 挂载父)
    {
        当前容器 = 家具;
        var 数据 = ServiceRegistry.Get<DataService>();
        var (定义标识, 等级) = 家具工具.解码(家具.标识);
        string 名称 = 定义标识;
        if (数据 != null && 数据.家具.TryGetValue(定义标识, out var 定义))
        {
            名称 = 定义.名称;
            是晾晒 = 定义.功能类型 == "晾晒";
        }
        var 容器服务 = ServiceRegistry.Get<容器服务>();
        容器服务.初始化容器(家具);   // 确保 内部 网格 存在
        // 标题 = 家具名 + 等级：直接 用 实例 标识 会 显示 成 "晾晒架_2"（内部 编码 漏到 界面 上）；
        //   等级 决定 加速（0/25/50%），所以 值得 写出来。
        if (标题文本 != null) 标题文本.text = 等级 > 1 ? $"{名称} {等级}级" : 名称;
        转化网格.数据源 = 容器服务.打开(家具);
        转化网格.所属容器 = 家具;
        转化网格.单株定根 = !是晾晒;   // 种植 = 单株 + 定根；晾晒 = 多件 + 随时 可 取下
        转化网格.立即刷新();            // 渲染 自动 设 网格容器 尺寸（网格宽+外扩×2 / 网格高+外扩×2）
        刷新状态();
    }

    // 刷新 状态/进度（打开 时 + 背包 变化 时 + 0.5s 轮询）：
    //   转化 中 的 件数 → 平均 进度 + 最早 一件 的 剩余；没有 转化中 的 但 有 成品 → 可以收了
    private void 刷新状态()
    {
        var 家具 = 当前容器;
        if (家具 == null) return;
        var 数据 = ServiceRegistry.Get<DataService>();
        if (数据 == null) return;
        var (定义标识, _) = 家具工具.解码(家具.标识);
        if (!数据.家具.TryGetValue(定义标识, out var 定义)) return;
        // 本 家具 的 成品 集合（成熟产物）：容器 里 出现 它 就 算 "可以收了"（肉干/皮革 之于 晾晒架、土豆/蔬菜 之于 种植箱）
        var 成品 = new System.Collections.Generic.HashSet<string>();
        foreach (var kv in 数据.物品)
        {
            var 物 = kv.Value;
            if (物 == null || string.IsNullOrEmpty(物.成熟产物)) continue;
            if (物.转化用途 == 定义.功能类型) 成品.Add(物.成熟产物);
        }
        int 转化中 = 0;
        float 累计比例 = 0f, 最早剩余 = float.MaxValue;
        bool 有成品 = false;
        if (家具.容器物品 != null)
        {
            foreach (var 堆叠 in 家具.容器物品)
            {
                if (堆叠 == null || string.IsNullOrEmpty(堆叠.标识)) continue;
                if (!数据.物品.TryGetValue(堆叠.标识, out var 模板)) continue;
                if (家具工具.可就地转化(模板, 定义))
                {
                    // 可转化物：生长分钟<=0 只 可能 是 刚 放进去（下一帧 由 推进器 补 满）——防御 分支
                    if (堆叠.生长分钟 > 0)
                    {
                        转化中++;
                        累计比例 += Mathf.Clamp01(1f - 堆叠.生长分钟 / (模板.生长时间 * 60f));
                        if (堆叠.生长分钟 < 最早剩余) 最早剩余 = 堆叠.生长分钟;
                    }
                }
                else if (堆叠.数量 > 0 && 成品.Contains(堆叠.标识))
                    有成品 = true;
            }
        }
        if (转化中 > 0)
        {
            if (进度滑条 != null) 进度滑条.value = Mathf.Clamp01(累计比例 / 转化中);
            if (进度文本 != null) 进度文本.text = 倒计时(最早剩余);
        }
        else
        {
            if (进度滑条 != null) 进度滑条.value = 0f;
            if (进度文本 != null) 进度文本.text = "";
        }
        if (状态文本 != null)
        {
            if (转化中 > 0) 状态文本.text = 进行中文案;
            else if (有成品) 状态文本.text = 完成文案;
            else 状态文本.text = 空闲文案;
        }
    }

    // 倒计时 文本：剩 hh:mm <后缀>（剩余 分钟 向上 取整；不 显示 秒）
    private string 倒计时(float 剩余分钟)
    {
        int 剩余 = Mathf.Max(0, Mathf.CeilToInt(剩余分钟));
        return $"剩 {剩余 / 60:00}:{剩余 % 60:00} {倒计时后缀}";
    }

    // 销毁：取消 背包变化 订阅
    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (背包变化订阅 != null)
        {
            ServiceRegistry.Get<EventBus>()?.取消订阅(背包变化订阅);
            背包变化订阅 = null;
        }
    }

    // 关闭：数据源 置空（基类 关闭 调 清理内容）
    protected override void 清理内容()
    {
        if (转化网格 != null) { 转化网格.数据源 = null; 转化网格.所属容器 = null; }
    }
}
