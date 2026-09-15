using System;
using System.Collections.Generic;
using UnityEngine;

// 存档服务（v52 刀64）：磁盘文件 + 多槽位（自动 1 个 + 手动 3 个）。
//
// 本类只做三件事：**组装载荷 → 交给 存档文件 落盘**；**读文本 → 校验版本 → 迁移 → 交给调用方**；列槽位摘要。
//   · 载荷的形状 / 版本检查 / 迁移链 / 指纹 → Domain/存档/存档模型.cs（纯 C#，离线验证器直接 <Compile Include>）
//   · 磁盘读写 / 原子写 / 备份       → Services/存档/存档文件.cs
// 这条分工就是评估 06 §3 说的"存档链路离线覆盖为零"的解药：能离线测的那一半被搬到了没有 UnityEngine 的那一半。
//
// ★ v5 相对 v4 修掉的三件事（都是真 bug，不是重构）：
//   ① 载荷里多了 `整点基准` —— 老档没有这个字段，读档后 `世界时间管理器.推进()` 会从 0 补算
//      `游戏天数 × 24` 次整点生存结算 → **读档即死**（饱食/水分归零 + 掉满血 + 仓库全腐坏）。
//      现在：读档侧 世界时间管理器.读档后对齐() 会用它。
//   ② 版本号**真的被检查**了。老 `读取()` 只用 try/catch 包 JsonUtility，而同结构变了的档
//      JsonUtility 不抛异常（未知字段忽略、缺失补默认）→ 坏档以"关键字段全是 0"静默进游戏。
//      现在：比本作新的版本直接拒收 + 明确报原因（进 存档摘要.损坏原因，UI 看得见）。
//   ③ 一个键 → 一个文件。PlayerPrefs 在 Windows 是注册表（卸载即丢、无法备份、单槽覆盖）。
public sealed class SaveService
{
    // ================= 保存 =================

    // 写一个槽。自动存档 走 槽 = 存档规格.自动槽号（**不覆盖**任何手动槽）。
    public bool 保存(int 槽, 玩家档案 玩家, bool 自动 = false)
    {
        if (!存档规格.合法槽(槽)) { Debug.LogError($"[存档] 槽位非法: {槽}"); return false; }
        if (玩家 == null) return false;
        var 数据 = 组装(玩家, 自动, 存档规格.槽名(槽));
        string 文本;
        try { 文本 = JsonUtility.ToJson(数据); }
        catch (Exception 异常) { Debug.LogError($"[存档] 序列化失败: {异常.Message}"); return false; }
        if (!存档文件.写(槽, 文本)) return false;
        Debug.Log($"[存档] 已写入 {存档规格.槽名(槽)}（{文本.Length} 字节）→ {存档文件.槽路径(槽)}");
        return true;
    }

    // 整点基准 从 世界时间管理器 取（那个字段的家在那儿）。取不到（未装配）→ 0，
    // 读档时 读档后对齐() 会把它退化成"当前分钟"，是安全等价选择。
    private static float 取整点基准()
        => ServiceRegistry.已注册<世界时间管理器>() ? ServiceRegistry.Get<世界时间管理器>().整点基准 : 0f;

    // ================= 读取 =================

    public bool 有存档(int 槽) => 存档文件.存在(槽);

    // 手动槽里最新的那一个。**私有**：对外只给 最近可读槽()（它会在没有手动档时退到自动档）——
    // 公开一个"只有内部用"的查询会变成门禁眼里的死 API（Tools/存档接线核对 段[5] 就是这么抓到它的）。
    // 为什么跳过自动槽：自动存档是"防丢"的兜底，不该抢在玩家自己选的档前面。
    // 但**只有自动档时**（玩家从没手动存过）也让他能继续 —— 否则自动存档等于白存。
    private int 最近手动槽()
    {
        int 最好 = -1; long 最新 = long.MinValue;
        for (int 槽 = 1; 槽 <= 存档规格.手动槽数; 槽++)
        {
            var 摘 = 摘要(槽);
            if (!摘.有档 || !string.IsNullOrEmpty(摘.损坏原因)) continue;
            if (摘.保存时间 > 最新) { 最新 = 摘.保存时间; 最好 = 槽; }
        }
        return 最好;
    }

    public int 最近可读槽()
    {
        int 槽 = 最近手动槽();
        if (槽 > 0) return 槽;
        var 自动 = 摘要(存档规格.自动槽号);
        return (自动.有档 && string.IsNullOrEmpty(自动.损坏原因)) ? 存档规格.自动槽号 : -1;
    }

    public bool 有任何可读存档() => 最近可读槽() >= 0;

    // 读一个槽。坏档 / 版本不符 / 文件不在 → null（并记日志）。**不抛**给调用方。
    public 存档数据 读取(int 槽)
    {
        string 文本 = 存档文件.读(槽);
        if (string.IsNullOrEmpty(文本)) return null;
        return 解析(文本, 槽);
    }

    public void 删除(int 槽)
    {
        if (!存档规格.合法槽(槽)) return;
        存档文件.删(槽);
    }

    // ================= 槽位摘要（存档面板 列表用） =================

    public List<存档摘要> 列出()
    {
        var 结果 = new List<存档摘要>();
        foreach (int 槽 in 存档规格.全部槽()) 结果.Add(摘要(槽));
        return 结果;
    }

    // 一个槽的摘要。**故意只读头**（不需要把整份档案反序列化出来就能显示一行）——
    // 代价是仍要把整个文件读进来（JsonUtility 没有"只解析一部分"），文件小的时候无所谓。
    public 存档摘要 摘要(int 槽)
    {
        var 摘 = new 存档摘要 { 槽 = 槽, 有档 = 存档文件.存在(槽), 自动 = 槽 == 存档规格.自动槽号 };
        if (!摘.有档) return 摘;
        string 文本 = 存档文件.读(槽);
        if (string.IsNullOrEmpty(文本)) { 摘.损坏原因 = "文件读不出来"; return 摘; }
        try
        {
            var 探 = JsonUtility.FromJson<存档探测>(文本);
            if (探 == null) { 摘.损坏原因 = "内容不是本游戏的存档"; return 摘; }
            摘.版本 = 探.头 != null ? 探.头.版本 : 探.版本;
            string 拒绝 = 存档版本.检查(摘.版本);
            if (拒绝 != "") { 摘.损坏原因 = 拒绝; return 摘; }
            if (探.头 != null)
            {
                摘.角色名 = 探.头.角色名; 摘.职业 = 探.头.职业; 摘.等级 = 探.头.等级;
                摘.游戏天数 = 探.头.游戏天数; 摘.位置 = 探.头.位置;
                摘.保存时间 = 探.头.保存时间; 摘.自动 = 探.头.自动;
            }
            else 摘.保存时间 = 探.保存时间;   // v4 旧档：只有保存时间可显示（角色名要整份解析，列表里不值当）
        }
        catch (Exception 异常) { 摘.损坏原因 = "解析失败：" + 异常.Message; }
        return 摘;
    }

    // ================= v4 旧档迁移（一次性） =================

    // 把 PlayerPrefs 里那份 v4 单键档搬进 存档 1。
    // 触发条件：旧键存在 **且 槽1 还没有文件** —— 绝不覆盖玩家已有的新档。
    // 迁移完**故意不删旧键**：新格式万一出问题，旧档还在注册表里能捞回来（.bak 是同一套思路）。
    public bool 迁移旧档()
    {
        if (!PlayerPrefs.HasKey(存档规格.旧档键)) return false;
        if (存档文件.存在(1)) return false;
        string 文本 = PlayerPrefs.GetString(存档规格.旧档键, "");
        if (string.IsNullOrEmpty(文本)) return false;
        var 数据 = 解析(文本, 1);
        if (数据?.玩家 == null) { Debug.LogWarning($"[存档] v4 旧档解析失败，跳过迁移（旧键 {存档规格.旧档键} 保留）。"); return false; }
        var 头 = 数据.头 ??= new 存档头();
        if (string.IsNullOrEmpty(头.角色名)) 头.角色名 = 数据.玩家.角色名;
        if (头.等级 <= 0) 头.等级 = 数据.玩家.等级 < 1 ? 1 : 数据.玩家.等级;
        if (头.游戏天数 <= 0) 头.游戏天数 = 数据.玩家.游戏天数 + 1;
        if (string.IsNullOrEmpty(头.位置)) 头.位置 = "安全屋";
        if (头.保存时间 <= 0) 头.保存时间 = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (string.IsNullOrEmpty(头.职业)) 头.职业 = 数据.玩家.职业;
        头.自动 = false;
        string 新文本;
        try { 新文本 = JsonUtility.ToJson(数据); }
        catch (Exception 异常) { Debug.LogError($"[存档] v4 旧档迁移序列化失败: {异常.Message}"); return false; }
        if (!存档文件.写(1, 新文本)) return false;
        Debug.Log($"[存档] 已把 v4 旧档（PlayerPrefs 键 {存档规格.旧档键}）迁移到 存档 1。旧键保留作兜底。");
        return true;
    }

    // ================= 内部：解析 + 版本检查 + 迁移 =================

    // ================= ★ 存档往返自检（游戏内，用**真的** JsonUtility） =================
    //
    // 为什么必须有这个、而且必须在这里：
    //   `JsonUtility` 是 Unity 专属的，离线验证器（Tools/存档验证）根本编译不到它 ——
    //   那半边（"序列化出来的东西真能原样读回来"）**只能**在 Unity 里验。
    //   它是本刀唯一一处"验真代码"的往返断言：用真的 SaveService 组装载荷、真的 JsonUtility 序列化、
    //   真的 存档文件 原子写盘、再原样读回来比指纹。**不碰 0~3 号槽**（用独立的 _自检.json）。
    //   反过来说清楚边界：本方法**不**验版本迁移的语义（那是离线验证器的事）、不验数值手感。
    public string 往返自检()
    {
        var 玩家 = ServiceRegistry.已注册<PlayerService>() ? ServiceRegistry.Get<PlayerService>().档案 : null;
        if (玩家 == null) return "失败了：没有玩家档案（先开局再自检）。";

        存档数据 原始 = 组装(玩家, 自动: false, 槽位名: "_自检");
        string 前 = 存档指纹.取(玩家);
        if (string.IsNullOrEmpty(前)) return "失败了：指纹算不出来。";

        // ① 纯内存往返：只验 JsonUtility 本身
        string 文本;
        try { 文本 = JsonUtility.ToJson(原始); }
        catch (Exception 异常) { return $"失败了：JsonUtility 序列化抛异常 — {异常.Message}"; }
        if (string.IsNullOrEmpty(文本)) return "失败了：JsonUtility 序列化出空串。";

        存档数据 内存回来;
        try { 内存回来 = JsonUtility.FromJson<存档数据>(文本); }
        catch (Exception 异常) { return $"失败了：JsonUtility 反序列化抛异常 — {异常.Message}"; }
        if (内存回来?.玩家 == null) return "失败了：读回来的载荷里没有角色数据。";
        string 内存指纹 = 存档指纹.取(内存回来.玩家);
        if (内存指纹 != 前) return 差异报告("内存往返", 前, 内存指纹);

        // ② 磁盘往返：再验 存档文件 的原子写 / 读 / UTF-8（中文标识全靠这一步别被编码弄坏）
        const string 自检文件 = "_自检.json";
        if (!存档文件.写文本(自检文件, 文本)) return "失败了：写测试文件失败（看 Console 的路径与异常）。";
        string 读回文本 = 存档文件.读文本(自检文件);
        存档文件.删文本(自检文件);
        if (string.IsNullOrEmpty(读回文本)) return "失败了：测试文件写成功了却读不回来。";
        if (读回文本.Length != 文本.Length) return $"失败了：磁盘往返长度不一致（写 {文本.Length} / 读 {读回文本.Length}）—— 编码出问题了。";
        存档数据 磁盘回来;
        try { 磁盘回来 = JsonUtility.FromJson<存档数据>(读回文本); }
        catch (Exception 异常) { return $"失败了：磁盘往返反序列化抛异常 — {异常.Message}"; }
        if (磁盘回来?.玩家 == null) return "失败了：磁盘往返读回来的载荷里没有角色数据。";
        string 磁盘指纹 = 存档指纹.取(磁盘回来.玩家);
        if (磁盘指纹 != 前) return 差异报告("磁盘往返", 前, 磁盘指纹);

        // ③ 版本检查（顺带：读回来的载荷必须被 存档版本.检查 判为可读）
        string 拒绝 = 存档版本.检查(磁盘回来.头?.版本 ?? 0);
        if (拒绝 != "") return $"失败了：自检产出的载荷自己过不了版本检查 — {拒绝}";

        return $"通过：内存往返 + 磁盘往返 指纹一致（{文本.Length} 字节），版本 v{磁盘回来.头?.版本}。";
    }

    // 指纹不一致时给出"第一个不同的位置"——比"不一致"三个字有用得多
    private static string 差异报告(string 环节, string 前, string 后)
    {
        int 长 = Math.Min(前.Length, 后.Length);
        int i = 0;
        while (i < 长 && 前[i] == 后[i]) i++;
        string 上 = i >= 20 ? 前.Substring(i - 20, 20) : 前.Substring(0, i);
        string 下 = i >= 20 ? 后.Substring(i - 20, 20) : 后.Substring(0, i);
        string 前段 = i + 20 < 前.Length ? 前.Substring(i, 20) : 前.Substring(i);
        string 后段 = i + 20 < 后.Length ? 后.Substring(i, 20) : 后.Substring(i);
        return $"失败了：{环节} 指纹不一致（第 {i} 字符起）——\n  存：…{上}【{前段}】\n  读：…{下}【{后段}】";
    }

    // 组装载荷（保存与自检共用一份，避免"自检走的是另一条路"）
    private 存档数据 组装(玩家档案 玩家, bool 自动, string 槽位名)
    {
        var _ = 槽位名;   // 目前只用于可读性；留参数是为了将来把"存于哪个槽"写进头
        return new 存档数据
        {
            头 = new 存档头
            {
                版本 = 存档规格.当前版本,
                保存时间 = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                角色名 = 玩家.角色名,
                职业 = 玩家.职业,
                等级 = 玩家.等级 < 1 ? 1 : 玩家.等级,
                游戏天数 = 玩家.游戏天数 + 1,   // 显示口径："第 1 天"= 游戏天数 0
                游戏分钟数 = 玩家.游戏分钟数,
                位置 = "安全屋",                 // ★ 刀65（位置 + 整趟战局）才会写真实位置
                自动 = 自动,
            },
            玩家 = 玩家,
            整点基准 = 取整点基准(),
        };
    }

    private 存档数据 解析(string 文本, int 槽)
    {
        存档数据 数据;
        try { 数据 = JsonUtility.FromJson<存档数据>(文本); }
        catch (Exception 异常)
        {
            Debug.LogError($"[存档] {存档规格.槽名(槽)} 解析失败（文件损坏？）：{异常.Message}");
            return null;
        }
        if (数据?.玩家 == null)
        {
            Debug.LogError($"[存档] {存档规格.槽名(槽)} 里没有角色数据（不是本游戏的档？）");
            return null;
        }

        // 版本探测：v5 的版本号在 头.版本；v4 在根上的 版本（那一版没有 头）。见 存档探测 的注释。
        int 版本 = 0;
        try
        {
            var 探 = JsonUtility.FromJson<存档探测>(文本);
            if (探 != null) 版本 = 探.头 != null ? 探.头.版本 : 探.版本;
        }
        catch { 版本 = 0; }

        string 拒绝 = 存档版本.检查(版本);
        if (拒绝 != "")
        {
            Debug.LogError($"[存档] {存档规格.槽名(槽)} 读不了：{拒绝}");
            return null;
        }

        数据.头 ??= new 存档头();
        数据.头.版本 = 版本;   // 迁移链据它判断"从哪一档开始升"
        foreach (var 步骤 in 存档版本.迁移(数据)) Debug.Log($"[存档] 迁移 {存档规格.槽名(槽)}：{步骤}");
        return 数据;
    }
}
