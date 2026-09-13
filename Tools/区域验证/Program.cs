using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

// ============================================================
// 区域验证：离线跑「区域生成器」—— 读真 JSON，逐颗种子断言不变量，打一张 ASCII 示例 + 汇总。
// 用法：dotnet run --project Tools/区域验证 -c Release -- 200 示例
// 断言（破了就是 bug）：
//   ① 确定性：同区域 + 同种子生成两次，结构指纹一致；
//   ② 建筑数量 = 展开清单数与地块数的较小值；不越界、不重叠、互不相邻（间距 ≥ 1）；
//   ③ 每个建筑的入口格：界内 + 可通行 + 从区域入口可达；
//   ④ 可站格连通分量 = 1；区域入口格可通行；
//   ⑤ 障碍/街上敌人不压入口/街口/楼门（四邻留空）；敌人不占死路（至少一个邻格可达）；
//   ⑥ 街口格（区域出口）：界内 + 可通行 + 从区域入口可达（否则走不出去）。
// ============================================================
public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        int 种子数 = 200;
        bool 打示例 = false;
        foreach (var a in args)
        {
            if (int.TryParse(a, out int n) && n > 0) 种子数 = n;
            if (a == "示例") 打示例 = true;
        }

        var 根 = 找仓库根();
        if (根 == null) { Console.WriteLine("找不到仓库根"); return 2; }
        var 选项 = new JsonSerializerOptions { IncludeFields = true, AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip };

        var 区域根 = 读<区域模板根>(Path.Combine(根, "Assets/Resources/Data/区域模板.json"), 选项);
        var 房间根 = 读<房间模板根>(Path.Combine(根, "Assets/Resources/Data/房间模板.json"), 选项);
        var 建筑根 = 读<建筑模板根>(Path.Combine(根, "Assets/Resources/Data/建筑模板.json"), 选项);
        if (区域根?.区域 == null || 区域根.区域.Length == 0) { Console.WriteLine("区域模板.json 没读到区域"); return 2; }

        var 房间名 = new Dictionary<string, string>();
        var 建表 = new Dictionary<string, 建筑模板>();
        if (建筑根?.建筑 != null)
            foreach (var 建 in 建筑根.建筑)
                if (建 != null) 建表[建.标识] = 建;
        if (房间根?.房间 != null)
            foreach (var 房 in 房间根.房间)
                if (房 != null) 房间名[房.标识] = string.IsNullOrEmpty(房.名称) ? 房.标识 : 房.名称;
        if (建筑根?.建筑 != null)
            foreach (var 建 in 建筑根.建筑)
                if (建 != null) 房间名[建.标识] = string.IsNullOrEmpty(建.名称) ? 建.标识 : 建.名称;
        string 取名(string 标识) => 房间名.TryGetValue(标识 ?? "", out var v) ? v : (标识 ?? "");
        建筑模板 取建(string 标识) => 建表.TryGetValue(标识 ?? "", out var 建) ? 建 : null;

        Console.WriteLine($"=== 区域层验证：{种子数} 颗种子 × {区域根.区域.Length} 个区域 ===");
        Console.WriteLine($"区域模板 {区域根.区域.Length} / 房间模板 {房间名.Count}（建筑名由此解析）");
        Console.WriteLine();

        int 总房间数 = 0, 失败 = 0, 保底 = 0;
        var 汇总 = new List<string>();

        // —— 建筑外形自检（配方 → 掩码）：一眼看出 L 型长什么样、连不连通 ——
        Console.WriteLine("—— 建筑外形自检（配方 → 掩码）——");
        int 外形坏 = 0;
        foreach (var 建 in 建筑根.建筑)
        {
            if (建?.外形 == null) continue;
            var 掩 = 建筑外形.生成(建.外形, out int 宽, out int 高);
            int 格 = 0;
            foreach (var b in 掩) if (b) 格++;
            bool 好 = 建筑外形.连通(掩, 宽, 高) && 格 > 0;
            Console.WriteLine($"  {建.名称}：形状 {建.外形.形状} {宽}×{高}（朝向 {建.外形.朝向}，镜像 {(建.外形.左右镜像 ? "是" : "否")}）→ 楼体 {格} 格{(好 ? "" : "  ✗ 掩码不连通/为空")}");
            foreach (var 行 in 建筑外形.画(掩, 宽, 高)) Console.WriteLine("      " + 行);
            if (!好) 外形坏++;
        }
        if (外形坏 == 0) Console.WriteLine("  ✓ 外形都合法且连通");
        失败 += 外形坏;
        Console.WriteLine();

        // —— 门口保护自检（刀32）——
        // 为什么需要：生成期与验证期**共用** 门口保护.相邻 之后，"把谓词改坏"会同时让两边变宽 ——
        //   验证器再也测不出 Generator 的漂移（实测：把 相邻 改成恒 false，大世界验证会炸，区域验证却全绿）。
        //   所以谓词本身必须有一条**独立**的自检：这里钉死它的真值表（门口格自己 + 紧邻八格 = 真；隔 2 格 = 假）。
        Console.WriteLine("—— 门口保护自检（四邻真值表）——");
        {
            int 谓词坏 = 0;
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                    if (!门口保护.相邻(10 + dc, 10 + dr, 10, 10)) { 谓词坏++; Console.WriteLine($"  ✗ 相邻(偏移 {dc},{dr}) 应为真"); }
            int[][] 远的 = { new[] { 2, 0 }, new[] { 0, 2 }, new[] { 2, 2 }, new[] { -2, 0 }, new[] { 0, -2 }, new[] { -2, -2 } };
            foreach (var d in 远的)
                if (门口保护.相邻(10 + d[0], 10 + d[1], 10, 10)) { 谓词坏++; Console.WriteLine($"  ✗ 相邻(偏移 {d[0]},{d[1]}) 应为假（隔 2 格不算四邻）"); }
            if (谓词坏 == 0) Console.WriteLine("  ✓ 9 格为真 / 6 个隔 2 格的为假（切比雪夫距离 ≤1 口径正确）");
            失败 += 谓词坏;
        }
        Console.WriteLine();

        foreach (var 区模板 in 区域根.区域)
        {
            if (区模板 == null) continue;
            var 清单 = 区域生成器.展开建筑(区模板);
            int 建筑数 = 0, 障碍数 = 0, 地块数 = 0, 敌数 = 0;
            for (int 种子 = 1; 种子 <= 种子数; 种子++)
            {
                var 区 = 区域生成器.生成(种子, 区模板, 取名, 取建);
                总房间数++;
                if (区 == null) { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：生成失败"); continue; }

                // ① 确定性
                var 区2 = 区域生成器.生成(种子, 区模板, 取名, 取建);
                if (区2 == null || 区.指纹() != 区2.指纹())
                {
                    失败++;
                    string f1 = 区?.指纹() ?? "", f2 = 区2?.指纹() ?? "";
                    var p1 = f1.Split(';');
                    var p2 = f2.Split(';');
                    string 差 = $"（段数 {p1.Length} vs {p2.Length}）";
                    for (int i = 0; i < Math.Min(p1.Length, p2.Length); i++)
                        if (p1[i] != p2[i]) { 差 = $"第 {i} 段：{p1[i]}  ←→  {p2[i]}"; break; }
                    Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：同种子两次生成不一致 —— {差}");
                }

                // ② 建筑：数量 / 越界 / 重叠 / 间距
                var 楼们 = 区.取类型(网格实体类型.建筑);
                var 块们 = 区域生成器.算地块(区);
                int 期望栋 = Math.Min(清单.Count, 块们.Count);
                if (楼们.Count != 期望栋) { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：建筑 {楼们.Count} ≠ 期望 {期望栋}"); }
                for (int i = 0; i < 楼们.Count; i++)
                {
                    var a = 楼们[i];
                    if (a.列 < 1 || a.行 < 1 || a.列 + a.宽 - 1 > 区.列 - 2 || a.行 + a.高 - 1 > 区.行 - 2)
                    { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：建筑越界 {a.标识}@{a.列},{a.行}"); }
                    for (int j = i + 1; j < 楼们.Count; j++)
                        if (a.间距(楼们[j]) < 1)
                        { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：建筑贴在一起 {a.标识} / {楼们[j].标识}"); }
                }

                // ③④ 入口格 + 连通（区域生成器.校验 已含这两条，这里再单独报清楚）
                var 可达 = 区.可达集();
                if (!区.可通行(区.入口列, 区.入口行)) { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：区域入口格不可通行"); }
                if (区.连通分量数() > 1) { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：可站格有孤岛（分量 {区.连通分量数()}）"); }

                // ⑥ 街口格（区域出口）：走不出去 = 玩家卡在副本里
                var 街口 = 区域生成器.出口格(区);
                if (!区.界内(街口.列, 街口.行) || !区.可通行(街口.列, 街口.行) || !可达.Contains(网格数据.编码(街口.列, 街口.行)))
                { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：街口格({街口.列},{街口.行}) 不可站/不可达"); }
                else if (区.格上实体(街口.列, 街口.行)?.类型 != 网格实体类型.门)
                { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：街口格({街口.列},{街口.行}) 上没有门实体"); }
                // 街口左右是**边界墙**（那是设计的一部分，不是"堵"），所以只查我们撒的东西：
                // 障碍/敌人不许落在街口四邻（否则会出现"出门就顶着废车/丧尸"）。
                // 四邻判据与生成期**同一份**（门口保护.相邻，刀32 收敛）：原来这里手写 Math.Abs ≤1
                foreach (var 物 in 区.实体)
                {
                    if (物 == null || !物.占格) continue;
                    if (物.类型 != 网格实体类型.障碍 && 物.类型 != 网格实体类型.敌人) continue;
                    if (门口保护.相邻(街口.列, 街口.行, 物.列, 物.行))
                    { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：{物.标识} 堵在街口旁（{物.列},{物.行}）"); }
                }
                // 入口格四邻同理（生成期 可撒占格物 保护了它，验证期原来**漏查**了这一条 —— 刀32 补上）
                foreach (var 物 in 区.实体)
                {
                    if (物 == null || !物.占格) continue;
                    if (物.类型 != 网格实体类型.障碍 && 物.类型 != 网格实体类型.敌人) continue;
                    if (门口保护.相邻(区.入口列, 区.入口行, 物.列, 物.行))
                    { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：{物.标识} 堵在入口旁（{物.列},{物.行}）"); }
                }
                // 接线自检：入口格自己必须落在"要保护的门"集合里（这条防的是"调用方把门集传空了"——
                //   谓词对、却没人把门喂给它，是最容易悄悄发生的漂移）
                var 自检门集 = new List<(int 列, int 行)> { (区.入口列, 区.入口行), 街口 };
                if (!门口保护.碰门口(区, 自检门集, 区.入口列, 区.入口行))
                { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：入口格自己不在保护范围内（门集没喂进 门口保护）"); }

                // ⑦ 街上敌人：数量 / 不占死路。
                //    ⚠ 敌人自己是**占格物**，所以不能查"它那一格可不可达"（永远不可达）——
                //      要查的是"它周围有没有可达格"（这条断言在 Tools/大世界验证 上栽过一次）。
                int 期望敌 = 0;
                if (区模板.敌人 != null) foreach (var 项 in 区模板.敌人) if (项 != null) 期望敌 += Math.Max(0, 项.数量);
                var 敌们 = 区.取类型(网格实体类型.敌人);
                if (敌们.Count != 期望敌) { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：街上敌人 {敌们.Count} ≠ 期望 {期望敌}（撒不下 = 街区太挤，或留空规则太紧）"); }
                foreach (var 敌 in 敌们)
                {
                    if (敌.列 < 1 || 敌.行 < 1 || 敌.列 > 区.列 - 2 || 敌.行 > 区.行 - 2)
                    { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：敌人越界 {敌.标识}@{敌.列},{敌.行}"); }
                    bool 有邻 = false;
                    if (可达.Contains(网格数据.编码(敌.列, 敌.行 - 1))) 有邻 = true;
                    if (可达.Contains(网格数据.编码(敌.列, 敌.行 + 1))) 有邻 = true;
                    if (可达.Contains(网格数据.编码(敌.列 - 1, 敌.行))) 有邻 = true;
                    if (可达.Contains(网格数据.编码(敌.列 + 1, 敌.行))) 有邻 = true;
                    if (!有邻) { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：敌人 {敌.标识}@{敌.列},{敌.行} 四周都到不了（永远打不起来）"); }
                }
                foreach (var 楼 in 楼们)
                {
                    // 非矩形楼：掩码必须连通（一栋楼不能是两块飞地）
                    if (!建筑外形.连通(楼.形状掩码, 楼.宽, 楼.高))
                    { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：{楼.标识} 的占地掩码不连通"); }
                    var 门 = 区域生成器.建筑入口格(楼);
                    if (!楼.覆盖(门.列, 门.行))
                    { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：{楼.标识} 的门口格({门.列},{门.行}) 不在楼体上"); }
                    if (!区.界内(门.列, 门.行) || !区.可通行(门.列, 门.行) || !可达.Contains(网格数据.编码(门.列, 门.行)))
                    { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：{楼.标识} 的入口格({门.列},{门.行}) 不可达/不可站"); }
                    // ⑤ 楼门四邻不能有障碍/敌人（别一出门就顶着废车或丧尸）
                    //    判据走 门口保护.相邻（与生成期同一份实现，刀32 收敛）
                    foreach (var 障碍 in 区.取类型(网格实体类型.障碍))
                        if (门口保护.相邻(门.列, 门.行, 障碍.列, 障碍.行))
                        { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：障碍挡在 {楼.标识} 门口"); }
                    foreach (var 敌 in 敌们)
                        if (门口保护.相邻(门.列, 门.行, 敌.列, 敌.行))
                        { 失败++; Console.WriteLine($"✗ 区域[{区模板.标识}] 种子{种子}：敌人挡在 {楼.标识} 门口"); }
                }
                if (!区域生成器.校验(区)) 保底++;

                建筑数 += 楼们.Count;
                障碍数 += 区.取类型(网格实体类型.障碍).Count;
                敌数 += 敌们.Count;
                地块数 += 块们.Count;

                if (打示例 && 种子 == 1) 打一屏(区, 楼们, 区模板);
            }

            汇总.Add($"区域[{区模板.标识}] {区模板.列}×{区模板.行}：平均建筑 {(double)建筑数 / 种子数:F1} / 地块 {(double)地块数 / 种子数:F1}"
                   + $" / 障碍 {(double)障碍数 / 种子数:F1} / 街上敌人 {(double)敌数 / 种子数:F1}（清单 {清单.Count} 栋）");
        }

        Console.WriteLine();
        foreach (var 行 in 汇总) Console.WriteLine(行);
        Console.WriteLine();
        Console.WriteLine($"生成区域 {总房间数} 个（校验不通过 {保底} 个）");
        Console.WriteLine($"失败 {失败} 处（{总房间数} 个区域）");
        return 失败 == 0 ? 0 : 1;
    }

    // ================= 一屏 ASCII =================

    private static void 打一屏(网格数据 区, List<网格实体> 楼们, 区域模板 模板)
    {
        var 敌们 = 区.取类型(网格实体类型.敌人);
        Console.WriteLine();
        Console.WriteLine($"【示例】{区.名称} {区.列}×{区.行} 建筑 {楼们.Count} 障碍 {区.取类型(网格实体类型.障碍).Count} 敌人 {敌们.Count}（{模板.描述}）");
        var 门格 = new HashSet<int>();
        foreach (var 楼 in 楼们)
        {
            var 门 = 区域生成器.建筑入口格(楼);
            门格.Add(网格数据.编码(门.列, 门.行));
        }
        var 街口 = 区域生成器.出口格(区);
        for (int r = 0; r < 区.行; r++)
        {
            var sb = new StringBuilder("  ");
            for (int c = 0; c < 区.列; c++)
            {
                var e = 区.格上实体(c, r);
                char 符 = '·';
                if (e == null) 符 = ' ';
                else if (e.是玩家) 符 = '@';
                else if (e.类型 == 网格实体类型.墙) 符 = '#';
                else if (e.类型 == 网格实体类型.建筑) 符 = 'B';
                else if (e.类型 == 网格实体类型.障碍) 符 = 'o';
                else if (e.类型 == 网格实体类型.敌人) 符 = 'e';
                else if (e.类型 == 网格实体类型.门) 符 = 'E';
                if (门格.Contains(网格数据.编码(c, r))) 符 = '+';
                sb.Append(符);
            }
            Console.WriteLine(sb.ToString());
        }
        Console.WriteLine($"  图例：@ 你 / B 楼 / + 楼门 / E 街口(走上去出门) / e 街上敌人 / o 障碍 / # 边界墙");
        Console.WriteLine($"  街口格({街口.列},{街口.行}) —— 就在入口正下方，出这一片走它");
        foreach (var 楼 in 楼们)
        {
            var 门 = 区域生成器.建筑入口格(楼);
            Console.WriteLine($"  {楼.标识}「{楼.名称}」{楼.宽}×{楼.高}@{楼.列},{楼.行} → 入口格({门.列},{门.行})");
        }
        foreach (var 敌 in 敌们) Console.WriteLine($"  敌人 {敌.定义标识}@{敌.列},{敌.行}");
    }

    // ================= 数据 =================

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
