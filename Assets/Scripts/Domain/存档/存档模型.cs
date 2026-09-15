using System;
using System.Collections.Generic;
using System.Text;

// ============================================================
// 存档载荷（纯 C#，零 UnityEngine 依赖）—— v52 刀64。
//
// 为什么从 SaveService 里搬出来单独一份：
//   ① 离线验证器（Tools/存档验证）要能 <Compile Include> 这份载荷做"存→读→比指纹 / 旧档迁移 / 坏档不崩"
//      的断言，而 SaveService 里是 PlayerPrefs + JsonUtility（Unity 专属），搬不出来就永远零覆盖 ——
//      评估 06 §3 点名的"存档链路离线覆盖为零"正是这个结构造成的。
//   ② 载荷 = 数据，读写 = IO。混在一起没人能单独测其中一半。
//
// 内容 = 玩家档案（角色/背包/装备/仓库/家具/时间/两个种子/迷雾位图）
//      + 两个**住在管理器里、不在档案里**的运行期基准（见 存档数据.整点基准）。
// ============================================================

// 存档规格：版本 / 槽位 / 文件名。**唯一真相**，别在别处再写一遍这些常量。
public static class 存档规格
{
    // 结构变动时递增；读取时据 版本 做兼容/迁移（迁移链在 存档版本.迁移）。
    //   v4 = 单键 PlayerPrefs、只存一个 玩家档案；
    //   v5 = 磁盘文件、多槽位、带 存档头 + 整点基准。
    public const int 当前版本 = 5;

    public const int 自动槽号 = 0;      // 自动存档（自动存档器 专用，手动保存不覆盖它）
    public const int 手动槽数 = 3;      // 手动槽 1..3

    public const string 目录名 = "存档";
    public const string 文件模板 = "槽{0}.json";

    // v4 时代的 PlayerPrefs 键（只用于"一次性迁移到槽1"，迁移后**故意不删** —— 万一新格式出问题还能靠它兜底）
    public const string 旧档键 = "last87days_save_v4";

    public static bool 合法槽(int 槽) => 槽 >= 自动槽号 && 槽 <= 手动槽数;

    public static string 槽名(int 槽) => 槽 == 自动槽号 ? "自动存档" : $"存档 {槽}";

    public static string 文件名(int 槽) => string.Format(文件模板, 槽);

    // 全部槽位（自动 + 手动），顺序固定：自动在前
    public static IEnumerable<int> 全部槽()
    {
        for (int 槽 = 自动槽号; 槽 <= 手动槽数; 槽++) yield return 槽;
    }
}

// 存档头：**不进游戏逻辑、只给人看与做校验**的那一小撮字段。
// 单独一份的理由：槽位列表要显示"什么时候存的 / 存的是谁 / 第几天"，而这些不值得把整份档案反序列化一遍。
[Serializable]
public class 存档头
{
    public int 版本 = 存档规格.当前版本;
    public long 保存时间;          // Unix 秒（现实时间）
    public string 角色名 = "";
    public string 职业 = "";
    public int 等级 = 1;
    public int 游戏天数 = 1;
    public float 游戏分钟数;
    // 人类可读的"存档时在哪"。★ 刀64 还只能是"安全屋" —— 位置本身入档是 **刀65**（位置 + 整趟战局）
    // 那一刀的事。这里留字段与口径，是为了让存档面板/摘要不用跟着改第二遍。
    public string 位置 = "安全屋";
    public bool 自动;              // true = 自动存档器 写的
}

// 存档载荷：一份存档的全部内容。
[Serializable]
public class 存档数据
{
    public 存档头 头 = new 存档头();
    public 玩家档案 玩家;

    // ★ 刀64：世界时间管理器的"整点结算基准"（`世界时间管理器.上次结算分钟`）。**必须进档。**
    // 为什么：`世界时间管理器.推进()` 的 while 拿它当"已经结算到哪"的比较对象，而它原本是
    //   纯运行期字段、初值 **0**，读档路径从不重置它 —— 读一张第 5 天的档时基准=0 而时间已到 7200 分钟，
    //   同一帧里补算 120 次 `结算时间段(60,true)`：饱食/水分归零 → 按 3 点/小时扣满血 → 腐坏结算
    //   把仓库清空。**也就是"读档即死"。** 它是本刀最核心的一个字段。
    // 注：跨天标记（`上次天`）**故意不进档** —— 它完全可由 玩家.游戏分钟数 推出，
    //   多存一份只会多一个"两份数据不一致"的机会（读档时按当前天重算，见 世界时间管理器.读档后对齐）。
    public float 整点基准;
}

// 槽位摘要（存档面板列表用）：够显示一行，不含档案本体。
[Serializable]
public class 存档摘要
{
    public int 槽;
    public bool 有档;
    public int 版本;
    public string 角色名 = "";
    public string 职业 = "";
    public int 等级 = 1;
    public int 游戏天数 = 1;
    public string 位置 = "";
    public long 保存时间;
    public bool 自动;
    public string 损坏原因 = "";   // 非空 = 这个槽读不出来（坏档 / 版本不符），列表要显示出来而不是静默当空槽
}

// v4 旧档的形状（单键 PlayerPrefs 里那份 JSON）。**只为了迁移**而存在，新代码不许再用它当载荷。
[Serializable]
public class 旧档_v4
{
    public int 版本;
    public 玩家档案 玩家;
    public long 保存时间;
}

// 版本/摘要探测：只声明"判定版本 + 显示一行摘要"需要的那几个字段。
// 为什么要单独一个探测类型：`存档数据.头` 带字段初始化器（`= new 存档头()`），
// 于是把 **v4 的旧 JSON** 直接读成 `存档数据` 时 `头` **不为 null**，会被误判成 v5 而跳过迁移。
// 本类两个字段都不写初始化器 → v4 读出来 `头 == null`、`版本` 是根上那个 4，才认得出它。
[Serializable]
public class 存档探测
{
    public 存档头 头;        // v5+：结构在这里
    public int 版本;         // v4：版本号在根上（那一版没有 头）
    public long 保存时间;     // v4：同理
}

// 版本检查与迁移。纯函数 → 离线可测（Tools/存档验证 就是这么用的）。
public static class 存档版本
{
    // 可用性检查。返回 "" = 可用；否则是给玩家看的拒绝原因。
    // ★ 为什么要真的看版本号：老 `SaveService.读取()` 只把 `JsonUtility.FromJson` 包在 try/catch 里，
    //   而 JsonUtility 对"同一个键但结构变了"的档**不抛异常**（未知字段忽略、缺失字段补默认值）——
    //   于是版本号读了从不检查，一个缺 `世界种子` 的档会拿到 0 并静默进游戏（`0` 会让派生结果退化）。
    //   评估 06 §D-5 点名过这条。现在：版本比本作新 → **明确拒收**（不猜、不半读）。
    public static string 检查(int 版本)
    {
        if (版本 <= 0) return "存档没有版本号（这不是本游戏的存档？）";
        if (版本 > 存档规格.当前版本)
            return $"存档版本 v{版本} 比本作（v{存档规格.当前版本}）新，读不了（请升级游戏）";
        return "";
    }

    // 迁移链：把任意旧版载荷**就地**升到 当前版本。返回做过哪些迁移（写日志 / 写文档用）。
    // 约定：每一档只补"结构"，不猜数值 —— 缺的数值一律交给 玩家档案 的缺省值 + PlayerService.清理非法值 兜。
    public static List<string> 迁移(存档数据 数据)
    {
        var 做过 = new List<string>();
        if (数据 == null) return 做过;
        数据.头 ??= new 存档头();
        int 版本 = 数据.头.版本;

        // v4 → v5：v4 那份载荷里根本没有 存档头，也没有 整点基准。
        //   整点基准 保持 0 → 读档时 世界时间管理器.读档后对齐() 会退化为"当前分钟"（安全等价选择），
        //   所以这里**不需要**猜一个值出来。真正要补的是把 版本 顶上当前版本。
        if (版本 < 5)
        {
            数据.头.版本 = 5;
            做过.Add($"v{版本}→v5：补存档头与整点基准（缺省 0 = 读档时退化为当前时间）");
        }
        return 做过;
    }
}

// 档案指纹：一份**稳定、可读**的摘要串。
// 用途：① 离线"存→读→比指纹"的往返断言；② 排查坏档时肉眼对账。
// 只走**被序列化的字段**（[NonSerialized] 的解析器与子管理器一律不碰）——
// 这正是它有意义的原因：指纹一致 == JsonUtility 真的把该存的都存下来了。
// **不排序**：JsonUtility 保序，所以"顺序也是被断言的一部分"，排序反而会放过"列表顺序丢了"这类 bug。
public static class 存档指纹
{
    public static string 取(玩家档案 档)
    {
        if (档 == null) return "<null>";
        var sb = new StringBuilder();
        sb.Append("职").Append(档.职业).Append("|名").Append(档.角色名).Append('|');
        sb.Append("等").Append(档.等级).Append("|经").Append(档.经验).Append("|点").Append(档.自由属性点).Append('|');
        sb.Append("属").Append(档.体质).Append(',').Append(档.力量).Append(',').Append(档.智慧)
          .Append(',').Append(档.敏捷).Append(',').Append(档.意志).Append('|');
        sb.Append("命").Append(档.生命).Append("|行").Append(档.行动点).Append('|');
        sb.Append("饱").Append(F(档.饱食度)).Append("|水").Append(F(档.水分度)).Append('|');
        sb.Append("病").Append(档.疲劳).Append(',').Append(档.中毒).Append(',').Append(档.感冒)
          .Append(',').Append(档.流血).Append(',').Append(档.骨折).Append(',').Append(档.发烧).Append('|');
        sb.Append("时").Append(F(档.游戏分钟数)).Append("|天").Append(档.天气).Append("|预").Append(档.预知天气).Append('|');
        sb.Append("节").Append(档.当前节点).Append("|屋").Append(档.安全屋等级).Append('|');
        sb.Append("户种").Append(档.户型种子).Append("|世种").Append(档.世界种子).Append('|');
        sb.Append("天赋").Append(串(档.天赋)).Append('|');
        sb.Append("冷却").Append(冷却(档.天赋冷却)).Append('|');
        sb.Append("技能").Append(技能(档.已学技能)).Append('|');
        sb.Append("技槽").Append(串(档.战斗技能槽)).Append('|');
        sb.Append("配方").Append(串(档.已习得配方)).Append('|');
        sb.Append("抉择").Append(串(档.抉择记录)).Append('|');
        sb.Append("已清").Append(串(档.已清空地点)).Append('|');
        sb.Append("幸存").Append(串(档.幸存者)).Append('|');
        sb.Append("迷雾").Append(串(档.迷雾记忆)).Append('|');
        sb.Append("拆墙").Append(串(档.已拆墙)).Append('|');
        sb.Append("任务").Append(任务(档.任务)).Append('|');
        sb.Append("日常").Append(日常(档.日常)).Append("|日常日").Append(档.日常生成日).Append('|');
        sb.Append("主背").Append(物品表(档.网格服务?.网格物品)).Append('|');
        // 仓库的行列尺寸：会随"储物箱 家具等级"变化（家具效果 → 仓库行加成），是**玩家可见的存档内容**。
        // ★ 这两行是 Tools/存档验证 的"标量字段全覆盖"断言抓出来的（反射扫 玩家档案 的标量字段发现指纹漏了它们）。
        sb.Append("仓库尺寸").Append(档.仓库列).Append('x').Append(档.仓库行).Append('|');
        sb.Append("仓库").Append(物品表(档.仓库物品)).Append('|');
        sb.Append("装备").Append(装备表(档.装备)).Append('|');
        sb.Append("家具").Append(物品表(档.家具));
        return sb.ToString();
    }

    // 浮点用固定 3 位（存档里的精度级别）；避免 "0.1" 与 "0.100000001" 这类平台差异污染指纹
    private static string F(float 值) => 值.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private static string 串(List<string> 表)
    {
        if (表 == null) return "-";
        var sb = new StringBuilder();
        for (int i = 0; i < 表.Count; i++) { if (i > 0) sb.Append('\u001F'); sb.Append(表[i]); }
        return sb.ToString();
    }

    // 迷雾记忆是 string[]（不是 List）—— 见 玩家档案.迷雾记忆 的注释（JsonUtility 不支持 Dictionary）
    private static string 串(string[] 表)
    {
        if (表 == null) return "-";
        var sb = new StringBuilder();
        for (int i = 0; i < 表.Length; i++) { if (i > 0) sb.Append('\u001F'); sb.Append(表[i]); }
        return sb.ToString();
    }

    private static string 冷却(List<天赋冷却条> 表)
    {
        if (表 == null) return "-";
        var sb = new StringBuilder();
        for (int i = 0; i < 表.Count; i++)
        {
            var 条 = 表[i];
            if (i > 0) sb.Append('\u001F');
            if (条 == null) { sb.Append("<null>"); continue; }
            sb.Append(条.标识).Append('@').Append(F(条.上次游戏分钟));
        }
        return sb.ToString();
    }

    private static string 技能(List<技能掌握> 表)
    {
        if (表 == null) return "-";
        var sb = new StringBuilder();
        for (int i = 0; i < 表.Count; i++)
        {
            var 项 = 表[i];
            if (i > 0) sb.Append('\u001F');
            if (项 == null) { sb.Append("<null>"); continue; }
            sb.Append(项.标识).Append('@').Append(项.熟练等级).Append('/').Append(项.熟练度);
        }
        return sb.ToString();
    }

    private static string 任务(List<任务进度> 表)
    {
        if (表 == null) return "-";
        var sb = new StringBuilder();
        for (int i = 0; i < 表.Count; i++)
        {
            var 项 = 表[i];
            if (i > 0) sb.Append('\u001F');
            if (项 == null) { sb.Append("<null>"); continue; }
            sb.Append(项.标识).Append('@').Append(项.数量).Append('/').Append(项.已完成 ? 1 : 0);
        }
        return sb.ToString();
    }

    private static string 日常(List<日常任务> 表)
    {
        if (表 == null) return "-";
        var sb = new StringBuilder();
        for (int i = 0; i < 表.Count; i++)
        {
            var 项 = 表[i];
            if (i > 0) sb.Append('\u001F');
            if (项 == null) { sb.Append("<null>"); continue; }
            sb.Append(项.标识).Append('@').Append(项.目标标识).Append(':').Append(项.目标数量)
              .Append('/').Append(项.进度).Append('/').Append(项.已接取 ? 1 : 0).Append(项.已领取 ? 1 : 0);
        }
        return sb.ToString();
    }

    private static string 物品表(List<物品堆叠> 表)
    {
        if (表 == null) return "-";
        var sb = new StringBuilder();
        for (int i = 0; i < 表.Count; i++)
        {
            if (i > 0) sb.Append('\u001F');
            物品(sb, 表[i]);
        }
        return sb.ToString();
    }

    private static string 装备表(List<装备记录> 表)
    {
        if (表 == null) return "-";
        var sb = new StringBuilder();
        for (int i = 0; i < 表.Count; i++)
        {
            var 记 = 表[i];
            if (i > 0) sb.Append('\u001F');
            if (记 == null) { sb.Append("<null>"); continue; }
            sb.Append(记.槽位).Append('=').Append(记.标识).Append('@').Append(记.当前耐久)
              .Append('/').Append(记.已装填).Append('/').Append(记.品质).Append('/').Append(记.来源);
            配件(sb, 记.配件);
            sb.Append("容器[").Append(记.容器列).Append('x').Append(记.容器行).Append(']');
            物品表进(sb, 记.容器物品);
        }
        return sb.ToString();
    }

    private static void 物品(StringBuilder sb, 物品堆叠 堆)
    {
        if (堆 == null) { sb.Append("<null>"); return; }
        sb.Append(堆.标识).Append('#').Append(堆.数量)
          .Append('@').Append(堆.列).Append(',').Append(堆.行).Append(堆.旋转 ? "R" : "-")
          .Append('/').Append(堆.当前耐久).Append('/').Append(堆.品质)
          .Append('/').Append(F(堆.新鲜分钟)).Append('/').Append(F(堆.生长分钟))
          .Append('/').Append(F(堆.净化分钟)).Append('/').Append(F(堆.阅读分钟)).Append('/').Append(堆.失败叠加)
          .Append('/').Append(堆.已装填);
        配件(sb, 堆.配件);
        if (堆.容器列 > 0 || 堆.容器行 > 0 || 堆.容器物品 != null)
        {
            sb.Append("容器[").Append(堆.容器列).Append('x').Append(堆.容器行).Append(']');
            物品表进(sb, 堆.容器物品);
        }
    }

    private static void 物品表进(StringBuilder sb, List<物品堆叠> 表)
    {
        if (表 == null) { sb.Append("<空>"); return; }
        sb.Append('{');
        for (int i = 0; i < 表.Count; i++) { if (i > 0) sb.Append(','); 物品(sb, 表[i]); }
        sb.Append('}');
    }

    private static void 配件(StringBuilder sb, List<配件条> 表)
    {
        if (表 == null || 表.Count == 0) return;
        sb.Append('+');
        for (int i = 0; i < 表.Count; i++)
        {
            var 条 = 表[i];
            if (i > 0) sb.Append(',');
            if (条 == null) { sb.Append("<null>"); continue; }
            sb.Append(条.槽位).Append('=').Append(条.标识);
        }
    }
}
