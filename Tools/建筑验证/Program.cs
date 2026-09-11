using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

// ============================================================
// 建筑验证：离线跑「建筑生成器」—— 读真 JSON，逐颗种子断言楼梯与楼层的不变量，打 ASCII 示例 + 汇总。
// 用法：dotnet run --project Tools/建筑验证 -c Release -- 100 示例
// 断言（破了就是 bug）：
//   ① 每层都能生成；楼层数 = 模板声明数；
//   ② 每层**有且只有一部楼梯**，且楼梯坐标（列,行）**全楼完全相同**（上下楼才对得齐）；
//   ③ 楼梯格上原本那格墙没了（格上实体 = 楼梯）、可通行；楼梯内侧格从入口可达；
//   ④ 每层的门只在本层内（门.通向 属于该层 房间 列表）；
//   ⑤ 同建筑 + 同种子生成两次，每层结构指纹一致（确定性）；
//   ⑥ 大门只出现在写了 大门 = true 的那一层。
// ============================================================
public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        int 种子数 = 100;
        bool 打示例 = false;
        foreach (var a in args)
        {
            if (int.TryParse(a, out int n) && n > 0) 种子数 = n;
            if (a == "示例") 打示例 = true;
        }

        var 根 = 找仓库根();
        if (根 == null) { Console.WriteLine("找不到仓库根"); return 2; }
        var 选项 = new JsonSerializerOptions { IncludeFields = true, AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip };

        var 建筑根 = 读<建筑模板根>(Path.Combine(根, "Assets/Resources/Data/建筑模板.json"), 选项);
        if (建筑根?.建筑 == null || 建筑根.建筑.Length == 0) { Console.WriteLine("建筑模板.json 没读到建筑"); return 2; }
        var 房根 = 读<房间模板根>(Path.Combine(根, "Assets/Resources/Data/房间模板.json"), 选项);
        var 搜索 = 读<搜索地图类型根>(Path.Combine(根, "Assets/Resources/Data/搜索_地图类型.json"), 选项);
        var 遭遇 = 读<敌人组根>(Path.Combine(根, "Assets/Resources/Data/encounters.json"), 选项);
        // —— 标识核对要用到的两张权威表 ——
        var 棋盘根 = 读<战斗棋盘根>(Path.Combine(根, "Assets/Resources/Data/战斗棋盘.json"), 选项);
        var 区域根 = 读<区域模板根>(Path.Combine(根, "Assets/Resources/Data/区域模板.json"), 选项);

        var 房间表 = new Dictionary<string, 房间模板>();
        if (房根?.房间 != null) foreach (var 房 in 房根.房间) if (房 != null) 房间表[房.标识] = 房;
        var 搜索表 = new Dictionary<string, 搜索地图类型>();
        if (搜索?.地图类型 != null) foreach (var 项 in 搜索.地图类型) if (项 != null) 搜索表[项.标识] = 项;
        var 敌人组表 = new Dictionary<string, 敌人组数据>();
        if (遭遇?.敌人组 != null) foreach (var 项 in 遭遇.敌人组) if (项 != null) 敌人组表[项.标识] = 项;

        List<搜索容器> 取容器(string 地图类型, string 房间标识)
        {
            var 结果 = new List<搜索容器>();
            if (!搜索表.TryGetValue(地图类型 ?? "", out var 类型) || 类型.房间 == null) return 结果;
            foreach (var 房 in 类型.房间)
            {
                if (房?.容器 == null) continue;
                if (!string.IsNullOrEmpty(房间标识) && 房.标识 != 房间标识) continue;
                foreach (var 容器 in 房.容器) if (容器 != null) 结果.Add(容器);
            }
            return 结果;
        }
        List<string> 取敌人(string 标识)
        {
            var 结果 = new List<string>();
            if (string.IsNullOrEmpty(标识) || !敌人组表.TryGetValue(标识, out var 组) || 组.敌人 == null) return 结果;
            foreach (var 项 in 组.敌人)
                for (int i = 0; i < Math.Max(1, 项.数量); i++) 结果.Add(项.敌人);
            return 结果;
        }
        房间模板 取模板(string 标识) => 房间表.TryGetValue(标识 ?? "", out var 房) ? 房 : null;

        Console.WriteLine($"=== 建筑层验证：{种子数} 颗种子 × {建筑根.建筑.Length} 栋楼 ===");
        Console.WriteLine($"建筑模板 {建筑根.建筑.Length} / 房间模板 {房间表.Count} / 敌人组 {敌人组表.Count}");
        Console.WriteLine();

        int 失败 = 0;
        // ================= 先做"数据标识核对"（离线版的 DataService 跨引用校验）=================
        // 教训：一个写错的标识（比如 战斗棋盘 "街上" 而实际叫 "街头"）会让 Unity 里的 装配() 直接中止，
        //       而下游报的是一堆「未注册服务」——离线就能查出来的事，别留给 Unity。
        var 棋盘表 = new Dictionary<string, 战斗棋盘数据>();
        if (棋盘根?.棋盘 != null) foreach (var 项 in 棋盘根.棋盘) if (项 != null) 棋盘表[项.标识] = 项;
        Console.WriteLine($"—— 数据标识核对（棋盘 {棋盘表.Count} 张：{string.Join(" / ", 棋盘表.Keys)}）——");
        int 数据坏 = 0;
        void 查棋盘(string 谁, string 值)
        {
            if (string.IsNullOrEmpty(值)) return;
            if (!棋盘表.ContainsKey(值)) { 数据坏++; Console.WriteLine($"  ✗ {谁} → 战斗棋盘[{值}] 不存在"); }
        }
        foreach (var (标识, 房) in 房间表)
        {
            查棋盘($"房间模板[{标识}]", 房.战斗棋盘);
            if (房.敌人数量最大 > 0 && string.IsNullOrEmpty(房.敌人组)) { 数据坏++; Console.WriteLine($"  ✗ 房间模板[{标识}] 有敌人数量但没写 敌人组"); }
            if (!string.IsNullOrEmpty(房.敌人组) && !敌人组表.ContainsKey(房.敌人组)) { 数据坏++; Console.WriteLine($"  ✗ 房间模板[{标识}] → 敌人组[{房.敌人组}] 不存在"); }
            if (房.容器池 == null || 房.容器池.Length == 0) { 数据坏++; Console.WriteLine($"  ✗ 房间模板[{标识}] 没有容器池"); continue; }
            foreach (var 池 in 房.容器池)
            {
                if (池 == null) continue;
                if (!搜索表.ContainsKey(池.地图类型)) { 数据坏++; Console.WriteLine($"  ✗ 房间模板[{标识}] → 地图类型[{池.地图类型}] 不存在"); continue; }
                if (string.IsNullOrEmpty(池.房间)) continue;
                bool 命中 = false;
                foreach (var 房项 in 搜索表[池.地图类型].房间) if (房项 != null && 房项.标识 == 池.房间) { 命中 = true; break; }
                if (!命中) { 数据坏++; Console.WriteLine($"  ✗ 房间模板[{标识}] → 地图类型[{池.地图类型}] 里没有房间[{池.房间}]"); }
            }
        }
        foreach (var 建 in 建筑根.建筑)
        {
            if (建 == null) continue;
            查棋盘($"建筑模板[{建.标识}]", 建.战斗棋盘);
            // —— 外形（区域地图上的占地）：配方名要认得（写错了会静默当矩形，那种穿帮离线就该拦下）——
            if (建.外形 != null)
            {
                if (!建筑外形.认得出(建.外形.形状))
                { 数据坏++; Console.WriteLine($"  ✗ 建筑模板[{建.标识}] → 外形.形状[{建.外形.形状}] 不认识（能写：{建筑外形.形状清单()}）"); }
                else
                {
                    var 掩 = 建筑外形.生成(建.外形, out int 外宽, out int 外高);
                    if (外宽 < 2 || 外高 < 2) { 数据坏++; Console.WriteLine($"  ✗ 建筑模板[{建.标识}] → 外形 {外宽}×{外高} 太小（至少 2×2）"); }
                    if (!建筑外形.连通(掩, 外宽, 外高))
                    { 数据坏++; Console.WriteLine($"  ✗ 建筑模板[{建.标识}] → 外形[{建.外形.形状}] {外宽}×{外高} 的占地掩码不连通或为空（一栋楼不能是两块飞地）"); }
                    if (建.外形.朝向 % 90 != 0)
                    { 数据坏++; Console.WriteLine($"  ✗ 建筑模板[{建.标识}] → 外形.朝向[{建.外形.朝向}] 不是 90 的倍数（只认 0/90/180/270）"); }
                    if (外宽 > 12 || 外高 > 7)
                        Console.WriteLine($"  ! 建筑模板[{建.标识}] → 外形 {外宽}×{外高} 偏大（小区地块 12×7、大区 14×8，放不下会被缩到地块大小）");
                }
            }
            if (建.楼层 == null) continue;
            for (int 层 = 0; 层 < 建.楼层.Length; 层++)
            {
                var 项 = 建.楼层[层];
                if (项?.房间 == null) continue;
                for (int i = 0; i < 项.房间.Length; i++)
                {
                    if (!房间表.ContainsKey(项.房间[i])) { 数据坏++; Console.WriteLine($"  ✗ 建筑模板[{建.标识}]·{层 + 1}F → 房间模板[{项.房间[i]}] 不存在"); continue; }
                    var 房 = 房间表[项.房间[i]];
                    if (i == 0 && (房.列 != 建.列 || 房.行 != 建.行)) { 数据坏++; Console.WriteLine($"  ✗ 建筑模板[{建.标识}]·{层 + 1}F 首间房尺寸 {房.列}×{房.行} ≠ {建.列}×{建.行}（楼梯会上下错位）"); }
                    if (房.门 == null) continue;
                    foreach (var 门 in 房.门)
                    {
                        if (门 == null || string.IsNullOrEmpty(门.通向)) continue;
                        bool 在本层 = false;
                        foreach (var 同层 in 项.房间) if (同层 == 门.通向) { 在本层 = true; break; }
                        if (!在本层) { 数据坏++; Console.WriteLine($"  ✗ 建筑模板[{建.标识}]·{层 + 1}F 房间[{房.标识}] 的门通向[{门.通向}] 不在本层"); }
                    }
                }
            }
        }
        if (区域根?.区域 != null)
            foreach (var 区 in 区域根.区域)
            {
                if (区 == null) continue;
                查棋盘($"区域模板[{区.标识}]", 区.战斗棋盘);
                if (区.建筑 == null) continue;
                foreach (var 项 in 区.建筑)
                {
                    if (项 == null) continue;
                    if (!string.IsNullOrEmpty(项.建筑模板) && !有建筑(项.建筑模板))
                    { 数据坏++; Console.WriteLine($"  ✗ 区域模板[{区.标识}] → 建筑模板[{项.建筑模板}] 不存在"); }
                    if (!string.IsNullOrEmpty(项.房间模板) && !房间表.ContainsKey(项.房间模板))
                    { 数据坏++; Console.WriteLine($"  ✗ 区域模板[{区.标识}] → 房间模板[{项.房间模板}] 不存在"); }
                }
            }
        Console.WriteLine(数据坏 == 0 ? "  ✓ 数据标识全部有效" : $"  ✗ 共 {数据坏} 处数据标识错误");
        失败 += 数据坏;
        Console.WriteLine();

        int 总层数 = 0;
        var 汇总 = new List<string>();
        bool 有建筑(string 标识)
        {
            foreach (var b in 建筑根.建筑) if (b != null && b.标识 == 标识) return true;
            return false;
        }

        foreach (var 建 in 建筑根.建筑)
        {
            if (建 == null) continue;
            int 层数 = 建筑生成器.楼层数(建);
            int ladderX = -1, ladderY = -1;
            for (int 种子 = 1; 种子 <= 种子数; 种子++)
            {
                int? 楼X = null, 楼Y = null;
                for (int 层 = 0; 层 < 层数; 层++)
                {
                    var 房 = 建筑生成器.生成楼层(种子, 建, 层, 取容器, 取敌人, 取模板);
                    总层数++;
                    if (房 == null) { 失败++; Console.WriteLine($"✗ 建筑[{建.标识}] 种子{种子} 层{层 + 1}：生成失败"); continue; }

                    // ⑥ 大门只在大门那层
                    bool 有大门 = 房.大门() != null;
                    if (有大门 != 建筑生成器.有大门的层(建, 层))
                    { 失败++; Console.WriteLine($"✗ 建筑[{建.标识}] 种子{种子} 层{层 + 1}：大门 {(有大门 ? "多" : "少")} 了"); }

                    // ② 每层一部楼梯，坐标全楼相同
                    var 梯们 = 房.楼梯列表();
                    if (梯们.Count != 1) { 失败++; Console.WriteLine($"✗ 建筑[{建.标识}] 种子{种子} 层{层 + 1}：楼梯 {梯们.Count} 部（应为 1）"); continue; }
                    var 梯 = 梯们[0];
                    if (楼X == null) { 楼X = 梯.列; 楼Y = 梯.行; }
                    else if (梯.列 != 楼X || 梯.行 != 楼Y)
                    { 失败++; Console.WriteLine($"✗ 建筑[{建.标识}] 种子{种子}：层{层 + 1} 楼梯({梯.列},{梯.行}) ≠ 首层({楼X},{楼Y})——上下楼会错位"); }

                    // ③ 楼梯格：墙没了、可通行、内侧格可达
                    if (!房.可通行(梯.列, 梯.行)) { 失败++; Console.WriteLine($"✗ 建筑[{建.标识}] 种子{种子} 层{层 + 1}：楼梯格不可通行"); }
                    var 内侧 = 房.门内侧格(梯);
                    var 可达 = 房.可达集();
                    if (!房.界内(内侧.列, 内侧.行) || !可达.Contains(网格数据.编码(内侧.列, 内侧.行)))
                    { 失败++; Console.WriteLine($"✗ 建筑[{建.标识}] 种子{种子} 层{层 + 1}：楼梯内侧格({内侧.列},{内侧.行}) 不可达"); }

                    // ③' 楼梯间是"往外长出来的那一格"：除了朝内那一个口，四邻必须是墙（界外 = 网格边缘，也算围住）
                    for (int d = 0; d < 4; d++)
                    {
                        int 邻列 = 梯.列 + (d == 0 ? 1 : d == 1 ? -1 : 0);
                        int 邻行 = 梯.行 + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (邻列 == 内侧.列 && 邻行 == 内侧.行) continue;   // 朝内的口
                        if (!房.界内(邻列, 邻行)) continue;                 // 界外 = 外墙边
                        var 邻 = 房.格上实体(邻列, 邻行);
                        if (邻 == null || 邻.类型 != 网格实体类型.墙)
                        { 失败++; Console.WriteLine($"✗ 建筑[{建.标识}] 种子{种子} 层{层 + 1}：楼梯间({梯.列},{梯.行}) 的邻居 ({邻列},{邻行}) 不是墙 —— 没围起来"); }
                    }

                    // ④ 门只在本层内
                    var 楼层项 = 建.楼层[层];
                    foreach (var 门 in 房.门列表())
                    {
                        if (门.是大门 || string.IsNullOrEmpty(门.通向)) continue;
                        bool 在本层 = false;
                        foreach (var 同层 in 楼层项.房间) if (同层 == 门.通向) { 在本层 = true; break; }
                        if (!在本层) { 失败++; Console.WriteLine($"✗ 建筑[{建.标识}] 种子{种子} 层{层 + 1}：门通向[{门.通向}] 不在本层"); }
                    }

                    // ⑤ 确定性
                    var 房2 = 建筑生成器.生成楼层(种子, 建, 层, 取容器, 取敌人, 取模板);
                    if (房2 == null || 房.指纹() != 房2.指纹()) { 失败++; Console.WriteLine($"✗ 建筑[{建.标识}] 种子{种子} 层{层 + 1}：同种子两次生成不一致"); }

                    if (打示例 && 种子 == 1) 打一屏(建, 层, 房, 梯);
                }
                if (种子 == 1) { ladderX = 楼X ?? -1; ladderY = 楼Y ?? -1; }
            }
            汇总.Add($"建筑[{建.标识}]「{建.名称}」{建.列}×{建.行}：{层数} 层（楼梯 边{建.楼梯边} 位{建.楼梯位} → 种子1 落在 ({ladderX},{ladderY})）");
        }

        Console.WriteLine();
        foreach (var 行 in 汇总) Console.WriteLine(行);
        Console.WriteLine();
        Console.WriteLine($"生成楼层 {总层数} 个");
        Console.WriteLine($"失败 {失败} 处");
        return 失败 == 0 ? 0 : 1;
    }

    private static void 打一屏(建筑模板 建, int 层, 网格数据 房, 网格实体 梯)
    {
        Console.WriteLine();
        Console.WriteLine($"【示例】{建.名称} · {建筑生成器.楼层名(建, 层)}　容器 {房.容器总数()} / 敌人 {房.敌人总数()}　楼梯({梯.列},{梯.行})");
        for (int r = 0; r < 房.行; r++)
        {
            var sb = new StringBuilder("  ");
            for (int c = 0; c < 房.列; c++)
            {
                var e = 房.格上实体(c, r);
                char 符 = ' ';
                if (e == null) 符 = '·';
                else if (e.是玩家) 符 = '@';
                else if (e.类型 == 网格实体类型.墙) 符 = '#';
                else if (e.类型 == 网格实体类型.楼梯) 符 = '=';
                else if (e.类型 == 网格实体类型.门) 符 = e.是大门 ? 'D' : '+';
                else if (e.类型 == 网格实体类型.敌人) 符 = '!';
                else if (e.类型 == 网格实体类型.容器) 符 = 'c';
                else if (e.类型 == 网格实体类型.尸体) 符 = 'y';
                else if (e.类型 == 网格实体类型.界外) 符 = 'x';   // 界外（挡通行但不画成墙）
                sb.Append(符);
            }
            Console.WriteLine(sb.ToString());
        }
    }

    static T 读<T>(string 路径, JsonSerializerOptions 选项)
    {
        if (!File.Exists(路径)) { Console.WriteLine($"缺文件：{路径}"); return default; }
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(路径, Encoding.UTF8), 选项); }
        catch (Exception e) { Console.WriteLine($"解析失败 {路径}：{e.Message}"); return default; }
    }

    static string 找仓库根()
    {
        var 目录 = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 6 && 目录 != null; i++)
        {
            if (Directory.Exists(Path.Combine(目录.FullName, "Assets/Resources/Data"))) return 目录.FullName;
            目录 = 目录.Parent;
        }
        return null;
    }
}
