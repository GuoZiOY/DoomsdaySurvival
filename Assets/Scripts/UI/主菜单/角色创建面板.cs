using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 角色创建面板（末日《最后87天》开局核心，单页分栏）。
// 动态创建：职业列表（数据驱动，加职业不改场景）、天赋（正/负容器动态生成行）。
// 静态引用：属性 5 行（[−]值[+]）、角色名/预览/详情/剩余文本/底部按钮——场景手动搭建。
// 交互：职业点选高亮 + 详情；属性 [−][+] 分配 10 自由点；天赋预算制；严格校验后确认。
public sealed class 角色创建面板 : 面板基类
{
    // ===== 属性行引用（5 行，索引 = 属性类型枚举：体质0 力量1 智慧2 敏捷3 意志4） =====
    [System.Serializable]
    public class 属性行引用
    {
        public Button 减按钮;    // [−]
        public TMP_Text 值文本;  // 当前已加点数（或总点数）
        public Button 加按钮;    // [+]
    }

    // —— 顶部 ——
    [SerializeField] private TMP_InputField 角色名输入;
    [SerializeField] private TMP_Text 预览五维;

    // —— 左栏：职业（静态按钮数组，场景摆好 6 个按钮，代码按序绑定职业） ——
    [SerializeField] private Button[] 职业按钮;   // 6 个职业按钮（按钮文字场景自定）
    [SerializeField] private TMP_Text 职业详情;

    // —— 中栏：属性 ——
    [SerializeField] private TMP_Text 剩余点数文本;
    [SerializeField] private 属性行引用[] 属性行;   // 5 行静态

    // —— 右栏：天赋（动态创建） ——
    [SerializeField] private TMP_Text 剩余预算文本;
    [SerializeField] private RectTransform 正面天赋区;
    [SerializeField] private RectTransform 负面天赋区;
    [SerializeField] private GameObject 天赋行模板;   // 天赋行模板（可选；不设则用 按钮预制体）
    [SerializeField] private int 建议天赋数 = 3;      // 建议上限（仅引导提示 + 随机数量基准，不强制限制；玩家可多选）

    // —— 底部 ——
    [SerializeField] private Button 返回按钮, 随机按钮, 重置按钮, 确认按钮;   // 返回按钮 → 回主菜单

    // ===== 构筑状态 =====
    private string 选中职业 = "";
    private string 角色名 = "无名幸存者";
    private readonly Dictionary<属性类型, int> 分配 = new Dictionary<属性类型, int>();
    private int 剩余自由点 = 10;
    private readonly List<string> 已选天赋 = new List<string>();
    private int 天赋预算 = 10;

    private DataService 数据 => ServiceRegistry.Get<DataService>();
    private PlayerService 玩家服务 => ServiceRegistry.Get<PlayerService>();

    // ===== 面板生命周期 =====

    // 绑定一次（Awake）：所有按钮 onClick 只注册一次，不再清绑。
    // 音效管理器 Start() 挂的 播放成功 监听因此天然保留；刷新() 只更新数据不碰监听器。
    void Awake()
    {
        绑定按钮();
    }

    protected override void 刷新(object 上下文)
    {
        重置构筑();
        渲染();
    }

    // 背景 模糊（塔科夫式）：显示时 模糊 背景，隐藏时 恢复（与 背包 等 面板 一致）
    public override void 显示面板(object 上下文 = null, bool 上下互切 = false, bool 返回方向 = false)
    {
        背景模糊层.显示模糊();
        base.显示面板(上下文, 上下互切, 返回方向);
    }

    public override void 隐藏面板(bool 上下互切 = false, bool 返回方向 = false)
    {
        背景模糊层.隐藏模糊();
        base.隐藏面板(上下互切, 返回方向);
    }

    public override bool 回退()
    {
        if (面板管理器.实例 != null) 面板管理器.实例.回主菜单();
        return true;
    }

    public override string 取消文本 => "返回主菜单";

    // ===== 按钮绑定（只执行一次） =====

    private void 绑定按钮()
    {
        // 职业按钮：按数组顺序绑定职业标识（按钮文字由场景静态写好）
        var 职业标识表 = new List<string>(数据.职业.Keys);
        for (int i = 0; i < (职业按钮?.Length ?? 0); i++)
        {
            int 索引 = i;
            if (职业按钮[i] == null) continue;
            职业按钮[i].onClick.AddListener(() =>
            {
                if (索引 < 职业标识表.Count) 选择职业(职业标识表[索引]);
            });
        }
        // 属性行
        for (int i = 0; i < (属性行?.Length ?? 0); i++)
        {
            var 类型 = (属性类型)i;   // 索引 = 枚举顺序
            var 行 = 属性行[i];
            if (行?.减按钮 != null) 行.减按钮.onClick.AddListener(() => 调整点数(类型, -1));
            if (行?.加按钮 != null) 行.加按钮.onClick.AddListener(() => 调整点数(类型, 1));
        }
        // 角色名
        if (角色名输入 != null) 角色名输入.onValueChanged.AddListener(v => 角色名 = string.IsNullOrEmpty(v) ? "无名幸存者" : v);
        // 底部
        if (返回按钮 != null) 返回按钮.onClick.AddListener(() => 回退());   // 返回主菜单（与右键/侧边栏取消同语义）
        if (随机按钮 != null) 随机按钮.onClick.AddListener(随机角色);
        if (重置按钮 != null) 重置按钮.onClick.AddListener(() => { 重置构筑(); 渲染(); });
        if (确认按钮 != null) 确认按钮.onClick.AddListener(确认开始);
    }

    // ===== 状态操作 =====

    private void 重置构筑()
    {
        选中职业 = ""; 角色名 = "无名幸存者";
        分配.Clear(); 剩余自由点 = 10;
        已选天赋.Clear(); 天赋预算 = 10;
        if (角色名输入 != null) 角色名输入.text = "";
    }

    // ===== 渲染 =====

    private void 渲染()
    {
        渲染预览();
        渲染职业();
        渲染属性();
        渲染天赋();
        渲染底部();
    }

    // 总览文本：角色名 / 职业 / 最终五维 / 天赋（含职业天赋，随构筑实时刷新）
    private void 渲染预览()
    {
        int[] 最终 = 计算最终五维();
        string 职业名 = 数据.职业.TryGetValue(选中职业, out var 职业) ? 职业.名称 : "未选择";
        var 天赋名表 = new List<string>();
        foreach (var 标识 in 已选天赋)
            if (数据.天赋.TryGetValue(标识, out var 天赋)) 天赋名表.Add(天赋.名称);
        // 职业天赋自动生效，一并显示（若未在 已选天赋 中）
        if (!string.IsNullOrEmpty(选中职业) && 数据.职业.TryGetValue(选中职业, out var 职) && !string.IsNullOrEmpty(职.天赋))
            if (!已选天赋.Contains(职.天赋) && 数据.天赋.TryGetValue(职.天赋, out var 职业天赋)) 天赋名表.Add(职业天赋.名称);
        string 天赋文本 = 天赋名表.Count > 0 ? string.Join("、", 天赋名表) : "无";
        string 文本 = $"角色：{角色名}\n职业：{职业名}\n体质 {最终[0]}  力量 {最终[1]}  智慧 {最终[2]}  敏捷 {最终[3]}  意志 {最终[4]}\n天赋：{天赋文本}";
        设文本(预览五维, 文本);
    }

    // 计算最终五维（索引：0体质 1力量 2智慧 3敏捷 4意志）
    // 基础 = 职业分布（直接给定五维，总和25；未选职业=普通人全5），自由加点与天赋效果叠加其上
    private int[] 计算最终五维()
    {
        int[] 值 = { 5, 5, 5, 5, 5 };   // 普通人：全 5（总和 25）
        if (数据.职业.TryGetValue(选中职业, out var 职业) && 职业.属性分布 != null)
        {
            // 职业分布：直接覆盖五维值
            foreach (var 项 in 职业.属性分布)
                值[属性索引(项.属性)] = 项.点数;
        }
        foreach (var (类型, 点数) in 分配)
            值[(int)类型] += 点数;
        foreach (var 标识 in 已选天赋)
            if (数据.天赋.TryGetValue(标识, out var 天赋) && 天赋.效果 != null)
                foreach (var 效果 in 天赋.效果)
                    if (效果 != null && 是五维(效果.目标))
                        值[属性索引(效果.目标)] += (int)效果.数值;
        return 值;
    }

    // 左栏：职业（静态按钮高亮 + 详情）
    private void 渲染职业()
    {
        var 职业标识表 = new List<string>(数据.职业.Keys);
        for (int i = 0; i < (职业按钮?.Length ?? 0); i++)
        {
            if (职业按钮[i] == null) continue;
            bool 选中 = i < 职业标识表.Count && 职业标识表[i] == 选中职业;
            设选中缩放(职业按钮[i], 选中);
        }
        刷新职业详情();
    }

    private void 刷新职业详情()
    {
        if (职业详情 == null) return;
        if (!数据.职业.TryGetValue(选中职业, out var 职业)) { 设文本(职业详情, "请选择你的身份……"); return; }
        string 技能 = 数据.技能.TryGetValue(职业.初始技能, out var 技) ? 技.名称 : 职业.初始技能;
        string 装备 = "";
        if (职业.初始装备 != null)
            foreach (var 项 in 职业.初始装备)
            {
                string 名 = 数据.物品.TryGetValue(项.标识, out var 物) ? 物.标识 : 项.标识;
                装备 += 名 + " ";
            }
        // 职业描述不含五维分布（五维看中栏属性列表）
        设文本(职业详情, $"{职业.名称}\n{职业.描述}\n技能：{技能}\n装备：{装备}");
    }

    // ===== 中栏：属性（静态行，值 = 职业分布 + 自由加点） =====

    private void 渲染属性()
    {
        设文本(剩余点数文本, $"{剩余自由点}");
        for (int i = 0; i < (属性行?.Length ?? 0); i++)
        {
            var 类型 = (属性类型)i;
            int 已加 = 分配.TryGetValue(类型, out var v) ? v : 0;
            int 职业值 = 职业分布值(类型);
            var 行 = 属性行[i];
            // 显示 "职业数值 + 自由数值"（如 5+3）；点加号变 5+4
            if (行?.值文本 != null) 行.值文本.text = $"{职业值}+{已加}";
            if (行?.加按钮 != null) 行.加按钮.interactable = 剩余自由点 > 0;
            if (行?.减按钮 != null) 行.减按钮.interactable = 已加 > 0;
        }
    }

    // 某属性的职业分布值（未选职业或该属性无分布 = 普通人 5）
    private int 职业分布值(属性类型 类型)
    {
        if (!数据.职业.TryGetValue(选中职业, out var 职业) || 职业.属性分布 == null) return 5;
        foreach (var 项 in 职业.属性分布)
            if (项.属性 == 类型名(类型)) return 项.点数;
        return 5;
    }

    // 场景职业行调用：选中职业（静态行按钮已绑定，此处公开供手动接线用）
    public void 选择职业(string 标识)
    {
        选中职业 = 标识;
        渲染();
    }

    // 场景行按钮调用：调整点数（+1 / -1）
    public void 调整点数(属性类型 类型, int 增量)
    {
        int 当前 = 分配.TryGetValue(类型, out var v) ? v : 0;
        if (增量 > 0)
        {
            if (剩余自由点 <= 0) { 音效管理器.实例?.播放失败(); return; }
            分配[类型] = 当前 + 1; 剩余自由点--;
        }
        else
        {
            if (当前 <= 0) { 音效管理器.实例?.播放失败(); return; }
            分配[类型] = 当前 - 1; 剩余自由点++;
        }
        渲染();
    }

    // ===== 右栏：天赋（动态创建） =====

    private void 渲染天赋()
    {
        // 天赋剩余预算：只显示数值；负数时红色富文本
        string 预算文本 = 天赋预算.ToString();
        if (天赋预算 < 0) 预算文本 = $"<color={游戏主题.危险色值}>{预算文本}</color>";
        设文本(剩余预算文本, 预算文本);
        渲染天赋列表(正面天赋区, 正面: true);
        渲染天赋列表(负面天赋区, 正面: false);
    }

    private void 渲染天赋列表(RectTransform 容器, bool 正面)
    {
        if (容器 == null) return;
        清空(容器);
        foreach (var 天赋 in 数据.天赋.Values)
        {
            bool 是正 = 天赋.点数 > 0;
            if (是正 != 正面) continue;
            string 标识 = 天赋.标识;
            bool 已选 = 已选天赋.Contains(标识);
            // 天赋行只显示名称（选中加 ✓）；点数/描述不显示（预算看顶部剩余预算文本）
            string 文字 = (已选 ? "✓ " : "") + 天赋.名称;
            if (天赋行模板 != null)
            {
                var 行 = 创建模板<Button>(容器, 天赋行模板);
                if (行 == null) continue;
                var 文本 = 行.GetComponentInChildren<TMP_Text>();
                if (文本 != null) 文本.text = 文字;
                行.onClick.AddListener(() => 切换天赋(标识));
                音效管理器.实例?.注册按钮(行);   // 模板克隆按钮需手动注册音效
            }
            else
            {
                bool 可点 = 已选 || (正面 ? 天赋预算 >= 天赋.点数 : true);
                创建行(容器, 文字, () => 切换天赋(标识), false, false);
                // 动态行可点性：无模板时行按钮由 创建行 生成，此处不再单独设 interactable
            }
        }
    }

    private void 切换天赋(string 标识)
    {
        if (!数据.天赋.TryGetValue(标识, out var 天赋)) return;
        bool 已选 = 已选天赋.Contains(标识);
        if (已选) { 已选天赋.Remove(标识); 天赋预算 += 天赋.点数; }
        else if (天赋.点数 > 0 ? 天赋预算 >= 天赋.点数 : true)
        {
            // 引导（不强制）：超过建议数量时提示一次（选第 建议数+1 个时），玩家仍可继续选
            if (已选天赋.Count == 建议天赋数)
                ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.反馈坏,
                    $"天赋越多，活下去越难——建议最多 {建议天赋数} 个（仅建议，可继续选择）。"));
            已选天赋.Add(标识); 天赋预算 -= 天赋.点数;
        }
        else { 音效管理器.实例?.播放失败(); return; }
        渲染();
    }

    // ===== 底部 =====

    private void 渲染底部()
    {
        bool 可确认 = !string.IsNullOrEmpty(选中职业) && 剩余自由点 == 0 && 天赋预算 >= 0;
        if (确认按钮 != null) 确认按钮.interactable = 可确认;
    }

    // 随机角色：随机职业 + 随机分配（满10）+ 随机天赋（预算内）
    public void 随机角色()
    {
        重置构筑();
        var 职业表 = new List<string>(数据.职业.Keys);
        if (职业表.Count > 0) 选中职业 = 职业表[Random.Range(0, 职业表.Count)];
        var 类型表 = System.Enum.GetValues(typeof(属性类型));
        int 守卫 = 0;
        while (剩余自由点 > 0 && 守卫++ < 100)
        {
            var 类型 = (属性类型)类型表.GetValue(Random.Range(0, 类型表.Length));
            分配[类型] = 分配.TryGetValue(类型, out var v) ? v + 1 : 1;
            剩余自由点--;
        }
        // 随机天赋：默认只随机几个（1~建议天赋数），预算内抽选（正面买不起则跳过，负面可选）
        var 候选 = new List<string>(数据.天赋.Keys);
        int 目标数 = Random.Range(1, 建议天赋数 + 1);
        守卫 = 0;
        while (候选.Count > 0 && 已选天赋.Count < 目标数 && 守卫++ < 30)
        {
            var 标识 = 候选[Random.Range(0, 候选.Count)];
            if (!数据.天赋.TryGetValue(标识, out var 天赋)) { 候选.Remove(标识); continue; }
            if (已选天赋.Contains(标识)) { 候选.Remove(标识); continue; }
            if (天赋.点数 > 0 && 天赋预算 < 天赋.点数) { 候选.Remove(标识); continue; }
            已选天赋.Add(标识); 天赋预算 -= 天赋.点数;
            候选.Remove(标识);
        }
        角色名 = "无名幸存者";
        渲染();
    }

    // 确认开始：写构筑参数 → 新游戏 → 进入地图
    private void 确认开始()
    {
        if (string.IsNullOrEmpty(选中职业) || 剩余自由点 != 0 || 天赋预算 < 0) return;
        玩家服务.待选职业 = 选中职业;
        玩家服务.待选天赋.Clear();
        玩家服务.待选天赋.AddRange(已选天赋);
        if (数据.职业.TryGetValue(选中职业, out var 职业) && !string.IsNullOrEmpty(职业.天赋))
            if (!玩家服务.待选天赋.Contains(职业.天赋)) 玩家服务.待选天赋.Add(职业.天赋);
        foreach (var (类型, 点数) in 分配) 玩家服务.开局分配[类型] = 点数;
        玩家服务.角色名 = 角色名;
        玩家服务.新游戏();
        ServiceRegistry.Get<地图服务>().打开大地图("营地");
    }

    // ===== 工具 =====

    private static bool 是五维(string 目标) =>
        目标 == "体质" || 目标 == "力量" || 目标 == "智慧" || 目标 == "敏捷" || 目标 == "意志";

    private static int 属性索引(string 名)
    {
        switch (名)
        {
            case "体质": return 0;
            case "力量": return 1;
            case "智慧": return 2;
            case "敏捷": return 3;
            case "意志": return 4;
            default: return 0;
        }
    }

    private static string 类型名(属性类型 类型)
    {
        switch (类型)
        {
            case 属性类型.体质: return "体质";
            case 属性类型.力量: return "力量";
            case 属性类型.智慧: return "智慧";
            case 属性类型.敏捷: return "敏捷";
            case 属性类型.意志: return "意志";
            default: return "";
        }
    }
}
