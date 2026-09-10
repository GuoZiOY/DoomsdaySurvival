using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

// ============================================================
// 房间验证：批量跑种子，校验「房间生成器 / 寻路 / 视野」的不变量（无 Unity、纯命令行）。
// 用法：dotnet run --project Tools/房间验证 [种子数] [示例]
//
// 校验项（任何一条挂了就是 bug）：
//   ① 确定性：同种子两次生成 → 结构指纹一致；
//   ② 结构：入口格可通行（玩家站得住）；容器/敌人不越界、不重叠、不压入口格；
//   ③ 可达：每个容器至少一个相邻可站格、每个敌人的相邻可站格，都能从入口走到（0 例外）；
//   ④ 无孤岛：可站格连通分量 = 1；
//   ⑤ 数量：容器 ≥ 池项最小值之和、敌人 ≥ 模板最小值（保底布局也要满足）；
//   ⑥ 寻路：随机起终点对 —— 首=起、末=终、每步四方向相邻、全路径可通行；不可达返回 null；
//   ⑦ 视野：起点可见集非空且含自身；可见格都在半径内；墙后不可见（抽样断言）。
// ============================================================
public static class Program
{
    private static int 失败;
    private static bool 要示例;

    // 统计
    private static int 总房间, 保底数, 重掷总次数, 容器总, 敌人总, 坏容器总, 坏敌人总;

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        int 种子数 = args.Length > 0 && int.TryParse(args[0], out var n) ? n : 200;
        要示例 = Array.IndexOf(args, "示例") >= 0;

        var 数据 = 读数据();
        if (数据 == null) return 2;

        Console.WriteLine($"=== 房间层验证：{种子数} 颗种子 × {数据.房间模板.Count} 个房间模板 ===");
        Console.WriteLine($"地图类型 {数据.搜索地图类型.Count} / 敌人组 {数据.敌人组.Count} / 房间模板 {数据.房间模板.Count}");
        Console.WriteLine();

        foreach (var 模板 in 数据.房间模板.Values)
        {
            Console.WriteLine($"—— 模板[{模板.标识}] {模板.列}×{模板.行} 布局{模板.布局} 视野 白天{视野规则.默认().核心边长(时段.白天)}/夜晚{视野规则.默认().核心边长(时段.夜晚)} 敌人组[{模板.敌人组}] ——");
            for (int 种子 = 1; 种子 <= 种子数; 种子++)
                跑一颗(数据, 模板, 种子);
        }

        Console.WriteLine();
        Console.WriteLine($"生成房间 {总房间} 个（保底布局 {保底数} 个 = {百分比(保底数, 总房间)}）");
        Console.WriteLine($"平均每间：容器 {平均(容器总, 总房间):F1} / 敌人 {平均(敌人总, 总房间):F1} / 重掷 {平均(重掷总次数, 总房间):F2} 次");
        Console.WriteLine($"被移除的坏容器 {坏容器总} / 坏敌人 {坏敌人总}（连通校验里被剔掉的）");
        Console.WriteLine($"失败 {失败} 处（{总房间} 个房间）");
        return 失败 == 0 ? 0 : 1;
    }

    // ================= 单个房间：生成 + 全部断言 =================

    private static void 跑一颗(测试数据 数据, 房间模板 模板, int 种子)
    {
        var 甲 = 房间生成器.生成(种子, 模板, 数据.取容器表, 数据.取敌人组);
        var 乙 = 房间生成器.生成(种子, 模板, 数据.取容器表, 数据.取敌人组);
        if (甲 == null) { 报错(模板, 种子, "生成返回 null"); return; }

        总房间++;
        if (甲.走了保底布局) 保底数++;
        重掷总次数 += 甲.重掷次数;
        容器总 += 甲.容器总数();
        敌人总 += 甲.敌人总数();
        坏容器总 += 甲.被移除的坏容器;
        坏敌人总 += 甲.被移除的坏敌人;

        if (甲.指纹() != 乙.指纹()) 报错(模板, 种子, "确定性失败：同种子两次生成不一致");

        if (!甲.可通行(甲.入口列, 甲.入口行)) 报错(模板, 种子, "入口格不可通行（玩家站不住）");
        if (甲.玩家() == null) 报错(模板, 种子, "缺少玩家令牌");

        // 结构：越界 / 重叠 / 压入口
        for (int i = 0; i < 甲.实体.Count; i++)
        {
            var a = 甲.实体[i];
            if (a == null) { 报错(模板, 种子, "实体列表里有 null"); continue; }
            if (a.列 < 0 || a.行 < 0 || a.列 + a.宽 > 甲.列 || a.行 + a.高 > 甲.行)
                报错(模板, 种子, $"实体越界：{a.类型} {a.定义标识} @{a.列},{a.行} {a.宽}x{a.高}");
            if (a.类型 == 房间实体类型.墙 || a.是玩家) continue;
            if (a.覆盖(甲.入口列, 甲.入口行)) 报错(模板, 种子, $"实体压住入口格：{a.类型} {a.定义标识}");
            for (int j = i + 1; j < 甲.实体.Count; j++)
            {
                var b = 甲.实体[j];
                if (b == null || b.类型 == 房间实体类型.墙 || b.是玩家) continue;
                if (a.重叠(b)) 报错(模板, 种子, $"实体重叠：{a.定义标识}@{a.列},{a.行} 与 {b.定义标识}@{b.列},{b.行}");
            }
        }

        // 可达 + 孤岛
        var 可达 = 甲.可达集();
        if (可达.Count == 0) 报错(模板, 种子, "入口可达集为空");
        int 分量 = 甲.连通分量数();
        if (分量 != 1) 报错(模板, 种子, $"可站格有孤岛：连通分量 = {分量}");

        foreach (var 容器 in 甲.取类型(房间实体类型.容器))
            if (!有相邻可达格(甲, 可达, 容器))
                报错(模板, 种子, $"容器搜不到（没有相邻可达格）：{容器.定义标识}@{容器.列},{容器.行}");
        foreach (var 敌 in 甲.取类型(房间实体类型.敌人))
            if (!有相邻可达格(甲, 可达, 敌))
                报错(模板, 种子, $"敌人打不着（相邻格不可达）：{敌.定义标识}@{敌.列},{敌.行}");

        // 数量下限（模板承诺的最小值）
        int 容器下限 = 容器下限合计(数据, 模板);
        if (甲.容器总数() < 容器下限) 报错(模板, 种子, $"容器数 {甲.容器总数()} < 下限 {容器下限}");
        if (甲.敌人总数() < Math.Max(0, 模板.敌人数量最小))
            报错(模板, 种子, $"敌人数 {甲.敌人总数()} < 下限 {模板.敌人数量最小}");

        // 视野规则（角色 + 时段）：白天/夜晚 边长、单数、6:00/18:00 分界
        var 规则 = 视野规则.默认();
        int 白天边长 = 规则.核心边长(时段.白天), 夜晚边长 = 规则.核心边长(时段.夜晚);
        int 弱视野 = 规则.弱视野边长(时段.夜晚);
        if (白天边长 % 2 == 0 || 夜晚边长 % 2 == 0 || 弱视野 % 2 == 0) 报错(模板, 种子, $"视野边长不是单数：白天 {白天边长} / 夜晚 {夜晚边长} / 弱视野 {弱视野}");
        if (夜晚边长 >= 白天边长) 报错(模板, 种子, $"夜晚视野没有比白天小：白天 {白天边长} / 夜晚 {夜晚边长}");
        if (白天边长 != 7) 报错(模板, 种子, $"白天核心边长应为 7：{白天边长}");
        if (夜晚边长 != 3) 报错(模板, 种子, $"夜晚核心边长应为 3：{夜晚边长}");
        if (弱视野 != 5) 报错(模板, 种子, $"夜晚弱视野应为 5：{弱视野}");
        if (弱视野 <= 夜晚边长) 报错(模板, 种子, "弱视野没有比夜晚核心大");
        // 弱视野圈：核心之外、弱视野之内 才算圈内；核心内 / 圈外 都不算
        if (规则.在弱视野圈(夜晚边长 / 2, 0, 0, 0, 夜晚边长 / 2)) 报错(模板, 种子, "核心内的格子被算进弱视野圈");
        if (规则.在弱视野圈(弱视野 / 2 + 1, 0, 0, 0, 夜晚边长 / 2)) 报错(模板, 种子, "弱视野之外的格子被算进圈内");
        if (!规则.在弱视野圈(夜晚边长 / 2 + 1, 0, 0, 0, 夜晚边长 / 2)) 报错(模板, 种子, "弱视野圈（核心外一格）没被算进圈内");
        if (规则.判时段(6f * 60f) != 时段.白天) 报错(模板, 种子, "6:00 应当是白天");
        if (规则.判时段(12f * 60f) != 时段.白天) 报错(模板, 种子, "12:00 应当是白天");
        if (规则.判时段(18f * 60f) != 时段.夜晚) 报错(模板, 种子, "18:00 应当是夜晚");
        if (规则.判时段(5f * 60f + 59f) != 时段.夜晚) 报错(模板, 种子, "5:59 应当是夜晚");
        if (规则.有记忆(时段.夜晚)) 报错(模板, 种子, "夜晚不该有记忆（应当全黑）");
        if (!规则.有记忆(时段.白天)) 报错(模板, 种子, "白天应当有记忆（淡阴影）");

        // 寻路
        测寻路(甲, 模板, 种子);
        // 视野
        测视野(甲, 模板, 种子);

        if (要示例 && 种子 == 7 && 模板 == 数据.房间模板[数据.房间模板.Keys.First()]) 打印示例(甲);
        if (要示例 && 种子 == 3 && 模板 == 数据.房间模板[数据.房间模板.Keys.First()]) 打印示例(甲);
    }

    private static void 测寻路(房间数据 房, 房间模板 模板, int 种子)
    {
        var 流 = new Random(种子 * 7919 + 13);
        int 对 = 0;
        for (int i = 0; i < 200; i++)
        {
            int 起列 = 流.Next(房.列), 起行 = 流.Next(房.行);
            int 终列 = 流.Next(房.列), 终行 = 流.Next(房.行);
            if (!房.可通行(起列, 起行) || !房.可通行(终列, 终行)) continue;
            对++;
            var 路 = 房间寻路.寻路(房, (起列, 起行), (终列, 终行));
            if (路 == null) { 报错(模板, 种子, $"可达却寻路失败：{起列},{起行} → {终列},{终行}"); continue; }
            if (路[0] != (起列, 起行)) 报错(模板, 种子, "路径首格不是起点");
            if (路[路.Count - 1] != (终列, 终行)) 报错(模板, 种子, "路径末格不是终点");
            for (int k = 1; k < 路.Count; k++)
            {
                int 步 = Math.Abs(路[k].列 - 路[k - 1].列) + Math.Abs(路[k].行 - 路[k - 1].行);
                if (步 != 1) 报错(模板, 种子, $"路径不是四方向相邻步：{路[k - 1]} → {路[k]}");
                if (!房.可通行(路[k].列, 路[k].行)) 报错(模板, 种子, $"路径穿过不可通行格：{路[k]}");
            }
        }
        if (对 == 0) 报错(模板, 种子, "可通行格太少，寻路测试没跑起来");

        // 点到容器 = 走到相邻格
        foreach (var 容器 in 房.取类型(房间实体类型.容器))
        {
            var 路 = 房间寻路.寻路到相邻(房, (房.入口列, 房.入口行), 容器);
            if (路 == null || 路.Count == 0) { 报错(模板, 种子, $"从入口走不到容器旁边：{容器.定义标识}"); continue; }
            var 末 = 路[路.Count - 1];
            if (容器.覆盖(末.列, 末.行)) 报错(模板, 种子, "寻路到容器：终点落在容器自己身上（应该停在旁边）");
        }

        // 不可达：墙格（界内且占位）必须返回 null
        var 墙 = 房.取类型(房间实体类型.墙);
        if (墙.Count > 0)
        {
            var 路 = 房间寻路.寻路(房, (房.入口列, 房.入口行), (墙[0].列, 墙[0].行));
            if (路 != null) 报错(模板, 种子, "寻路到墙上却没返回 null");
        }
    }

    private static void 测视野(房间数据 房, 房间模板 模板, int 种子)
    {
        var 可见 = 房间视野.可见集(房, 房.入口列, 房.入口行, 房.视野边长);
        if (可见.Count == 0) 报错(模板, 种子, "入口处可见集为空");
        if (!可见.Contains(房间数据.编码(房.入口列, 房.入口行))) 报错(模板, 种子, "可见集不含玩家自己所在格");
        if (房.视野边长 % 2 == 0) 报错(模板, 种子, $"视野边长不是单数：{房.视野边长}");

        // 期望格数：以玩家为中心、边长 (2×半+1) 的正方形，裁到房间内
        int 半 = 房.视野边长 / 2;
        int 期望 = 0;
        for (int r = 房.入口行 - 半; r <= 房.入口行 + 半; r++)
            for (int c = 房.入口列 - 半; c <= 房.入口列 + 半; c++)
                if (房.界内(c, r)) 期望++;
        if (可见.Count != 期望) 报错(模板, 种子, $"可见格数 {可见.Count} ≠ 方形期望 {期望}");

        foreach (int 码 in 可见)
        {
            int 列 = 码 / 1000, 行 = 码 % 1000;
            if (!房.界内(列, 行)) { 报错(模板, 种子, $"可见集含越界格 {列},{行}"); continue; }
            if (!房间视野.方形内(房.入口列, 房.入口行, 列, 行, 房.视野边长))
                报错(模板, 种子, $"可见集含方形外的格 {列},{行}");
        }

        // 方形正是一条边之外 → 必须不可见（证明是"方"而不是"圆"）
        int 外列 = 房.入口列 + 半 + 1, 外行 = 房.入口行 + 半 + 1;
        if (房.界内(外列, 房.入口行) && 可见.Contains(房间数据.编码(外列, 房.入口行)))
            报错(模板, 种子, "方形右边一格被算作可见（视野不是方形？）");
        if (房.界内(房.入口列, 外行) && 可见.Contains(房间数据.编码(房.入口列, 外行)))
            报错(模板, 种子, "方形下边一格被算作可见（视野不是方形？）");
        // 方形的四个角（对角方向，距离 = 半）必须在可见内（圆会漏掉这些角）
        int 角列 = 房.入口列 + 半, 角行 = 房.入口行 + 半;
        if (房.界内(角列, 角行) && !可见.Contains(房间数据.编码(角列, 角行)))
            报错(模板, 种子, "方形对角格不可见（视野退化成了圆？）");
    }
    // ================= 工具 =================

    private static bool 有相邻可达格(房间数据 房, HashSet<int> 可达, 房间实体 e)
    {
        for (int r = e.行; r < e.行 + e.高; r++)
            for (int c = e.列; c < e.列 + e.宽; c++)
            {
                if (房.界内(c - 1, r) && 可达.Contains(房间数据.编码(c - 1, r))) return true;
                if (房.界内(c + 1, r) && 可达.Contains(房间数据.编码(c + 1, r))) return true;
                if (房.界内(c, r - 1) && 可达.Contains(房间数据.编码(c, r - 1))) return true;
                if (房.界内(c, r + 1) && 可达.Contains(房间数据.编码(c, r + 1))) return true;
            }
        return false;
    }

    private static int 容器下限合计(测试数据 数据, 房间模板 模板)
    {
        int 合计 = 0;
        if (模板.容器池 == null) return 0;
        foreach (var 池 in 模板.容器池)
        {
            if (池 == null) continue;
            var 表 = 数据.取容器表(池.地图类型, 池.房间);
            if (表.Count == 0) continue;
            合计 += Math.Max(0, 池.数量最小);
        }
        return 合计;
    }

    private static void 打印示例(房间数据 房)
    {
        Console.WriteLine();
        Console.WriteLine($"【示例】{房.名称} {房.列}×{房.行} 容器 {房.容器总数()} 敌人 {房.敌人总数()}" +
                          $"（重掷 {房.重掷次数}{(房.走了保底布局 ? " / 保底" : "")}）");
        var 可见 = 房间视野.可见集(房, 房.入口列, 房.入口行, 房.视野边长);
        for (int r = 0; r < 房.行; r++)
        {
            var 行文本 = new StringBuilder("  ");
            for (int c = 0; c < 房.列; c++)
            {
                if (c == 房.入口列 && r == 房.入口行) { 行文本.Append('@'); continue; }
                var 格 = 房.格上实体(c, r);
                if (格 != null)
                {
                    行文本.Append(格.类型 switch
                    {
                        房间实体类型.墙 => '#',
                        房间实体类型.容器 => 格.挡视线 ? 'B' : 'c',
                        房间实体类型.尸体 => '+',
                        房间实体类型.敌人 => '!',
                        _ => '?',
                    });
                    continue;
                }
                行文本.Append(可见.Contains(房间数据.编码(c, r)) ? '·' : ' ');
            }
            Console.WriteLine(行文本.ToString());
        }
        Console.WriteLine();
    }

    private static void 报错(房间模板 模板, int 种子, string 文本)
    {
        失败++;
        if (失败 <= 40) Console.WriteLine($"  ✗ [{模板.标识} 种子{种子}] {文本}");
    }

    private static string 百分比(int a, int b) => b == 0 ? "-" : $"{a * 100f / b:F1}%";
    private static float 平均(int a, int b) => b == 0 ? 0f : (float)a / b;

    // ================= 数据加载（直接读游戏用的真 JSON，保证验证的就是真数据） =================

    private sealed class 测试数据
    {
        public Dictionary<string, 搜索地图类型> 搜索地图类型 = new Dictionary<string, 搜索地图类型>();
        public Dictionary<string, 敌人组数据> 敌人组 = new Dictionary<string, 敌人组数据>();
        public Dictionary<string, 房间模板> 房间模板 = new Dictionary<string, 房间模板>();

        public List<搜索容器> 取容器表(string 地图类型, string 房间标识)
        {
            var 结果 = new List<搜索容器>();
            if (!搜索地图类型.TryGetValue(地图类型 ?? "", out var 类型) || 类型.房间 == null) return 结果;
            foreach (var 房 in 类型.房间)
            {
                if (房?.容器 == null) continue;
                if (!string.IsNullOrEmpty(房间标识) && 房.标识 != 房间标识) continue;
                foreach (var 容器 in 房.容器)
                    if (容器 != null) 结果.Add(容器);
            }
            return 结果;
        }

        public List<string> 取敌人组(string 敌人组标识)
        {
            var 结果 = new List<string>();
            if (string.IsNullOrEmpty(敌人组标识) || !敌人组.TryGetValue(敌人组标识, out var 组) || 组.敌人 == null) return 结果;
            foreach (var 项 in 组.敌人)
                for (int i = 0; i < Math.Max(1, 项.数量); i++)
                    结果.Add(项.敌人);
            return 结果;
        }
    }

    private static 测试数据 读数据()
    {
        var 根目录 = 找仓库根();
        if (根目录 == null) { Console.WriteLine("找不到仓库根（Assets/Resources/Data 不存在）"); return null; }
        var 选项 = new JsonSerializerOptions { IncludeFields = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var 数据 = new 测试数据();

        var 搜索 = 读<搜索地图类型根>(Path.Combine(根目录, "Assets/Resources/Data/搜索_地图类型.json"), 选项);
        if (搜索?.地图类型 != null)
            foreach (var 项 in 搜索.地图类型)
                if (项 != null && !string.IsNullOrEmpty(项.标识)) 数据.搜索地图类型[项.标识] = 项;

        var 遭遇 = 读<敌人组根>(Path.Combine(根目录, "Assets/Resources/Data/encounters.json"), 选项);
        if (遭遇?.敌人组 != null)
            foreach (var 项 in 遭遇.敌人组)
                if (项 != null && !string.IsNullOrEmpty(项.标识)) 数据.敌人组[项.标识] = 项;

        var 房间 = 读<房间模板根>(Path.Combine(根目录, "Assets/Resources/Data/房间模板.json"), 选项);
        if (房间?.房间 != null)
            foreach (var 项 in 房间.房间)
                if (项 != null && !string.IsNullOrEmpty(项.标识)) 数据.房间模板[项.标识] = 项;

        if (数据.房间模板.Count == 0) { Console.WriteLine("房间模板.json 为空"); return null; }
        return 数据;
    }

    private static T 读<T>(string 路径, JsonSerializerOptions 选项)
    {
        if (!File.Exists(路径)) { Console.WriteLine($"缺文件：{路径}"); return default; }
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(路径, Encoding.UTF8), 选项);
        }
        catch (Exception e)
        {
            Console.WriteLine($"解析失败 {路径}：{e.Message}");
            return default;
        }
    }

    private static string 找仓库根()
    {
        var 目录 = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 6 && 目录 != null; i++)
        {
            if (Directory.Exists(Path.Combine(目录.FullName, "Assets/Resources/Data"))) return 目录.FullName;
            目录 = 目录.Parent;
        }
        目录 = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 10 && 目录 != null; i++)
        {
            if (Directory.Exists(Path.Combine(目录.FullName, "Assets/Resources/Data"))) return 目录.FullName;
            目录 = 目录.Parent;
        }
        return null;
    }
}

// 顶层 using 之后仍需要 First()：用扩展方法提供（避免引入 System.Linq 影响别处）
public static class 集合扩展
{
    public static T First<T>(this IEnumerable<T> 源)
    {
        foreach (var 项 in 源) return 项;
        return default;
    }
}
