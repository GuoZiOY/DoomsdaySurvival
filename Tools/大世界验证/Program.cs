using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

// ============================================================
// 大世界验证：离线跑「大世界生成器」—— 读真 JSON，逐颗种子断言不变量，打一张降采样 ASCII 图 + 汇总。
// 用法：dotnet run --project Tools/大世界验证 -c Release -- 50 示例
// 断言（破了就是 bug）：
//   ① 确定性：同世界 + 同种子生成两次，结构指纹**逐字节一致**；
//   ② 区域占地：界内（四周留 世界规格.边距）、形状掩码连通、**互不重叠**、不压营地；
//   ③ 区域门口格在自己的占地掩码上，且**可通行 + 从入口可达**；
//   ④ 营地门口格可通行 + 从它可达；玩家唯一且站在营地门口格上；
//   ⑤ 可站格连通分量 = 1（无孤岛）；
//   ⑥ 障碍不压区域占地 / 不压营地 / 不压区域门口四邻；
//   ⑦ 数据引用：世界.json 里写的 区域模板 必须真的存在；
//   ⑧ 撤回数 = 0（连通校验本不该被触发 —— 非 0 说明"障碍切断地图"或"门口被堵"，是要查的 bug）。
// ============================================================
public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        int 种子数 = 50;
        bool 打示例 = false;
        foreach (var a in args)
        {
            if (int.TryParse(a, out int n) && n > 0) 种子数 = n;
            if (a == "示例") 打示例 = true;
        }

        var 根 = 找仓库根();
        if (根 == null) { Console.WriteLine("找不到仓库根"); return 2; }
        var 选项 = new JsonSerializerOptions { IncludeFields = true, AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip };

        var 世界根 = 读<世界根>(Path.Combine(根, "Assets/Resources/Data/世界.json"), 选项);
        var 区根 = 读<区域模板根>(Path.Combine(根, "Assets/Resources/Data/区域模板.json"), 选项);
        var 房根 = 读<房间模板根>(Path.Combine(根, "Assets/Resources/Data/房间模板.json"), 选项);
        var 建根 = 读<建筑模板根>(Path.Combine(根, "Assets/Resources/Data/建筑模板.json"), 选项);
        if (世界根?.世界 == null || 世界根.世界.Length == 0) { Console.WriteLine("世界.json 没读到世界"); return 2; }

        // 区域模板 名表（这是 世界.json 唯一的外部引用）
        var 区表 = new Dictionary<string, 区域模板>();
        if (区根?.区域 != null)
            foreach (var 区 in 区根.区域)
                if (区 != null) 区表[区.标识] = 区;
        // 显示名：建筑模板 优先，其次 房间模板（区域名由它们解析）
        var 名表 = new Dictionary<string, string>();
        if (房根?.房间 != null)
            foreach (var 房 in 房根.房间) if (房 != null) 名表[房.标识] = string.IsNullOrEmpty(房.名称) ? 房.标识 : 房.名称;
        if (建根?.建筑 != null)
            foreach (var 建 in 建根.建筑) if (建 != null) 名表[建.标识] = string.IsNullOrEmpty(建.名称) ? 建.标识 : 建.名称;
        string 取名(string 标识) => 名表.TryGetValue(标识 ?? "", out var v) ? v : (标识 ?? "");

        Console.WriteLine($"=== 大世界验证：{种子数} 颗种子 × {世界根.世界.Length} 个世界 ===");
        Console.WriteLine($"世界模板 {世界根.世界.Length} / 区域模板 {区表.Count} / 房间模板 {(房根?.房间?.Length ?? 0)} / 建筑模板 {(建根?.建筑?.Length ?? 0)}");
        Console.WriteLine();

        int 总世界数 = 0, 失败 = 0;

        // —— 区域占地自检（配方 → 掩码）：一眼看出 L 型在世界上长什么样 ——
        Console.WriteLine("—— 区域占地自检（配方 → 掩码）——");
        int 占地坏 = 0;
        foreach (var 世 in 世界根.世界)
        {
            if (世?.区域 == null) continue;
            foreach (var 项 in 世.区域)
            {
                if (项 == null) continue;
                var 形 = 项.占地 ?? new 建筑外形数据 { 形状 = 建筑外形.矩形, 宽 = 6, 高 = 5 };
                if (!建筑外形.认得出(形.形状))
                {
                    Console.WriteLine($"  ✗ 区域[{项.区域模板}] 占地形状[{形.形状}] 认不出（只能是 {建筑外形.形状清单()}）");
                    占地坏++; continue;
                }
                var 掩 = 建筑外形.生成(形, out int 宽, out int 高);
                int 格 = 0;
                foreach (var b in 掩) if (b) 格++;
                bool 好 = 建筑外形.连通(掩, 宽, 高) && 格 > 0;
                bool 引用在 = 区表.ContainsKey(项.区域模板 ?? "");
                Console.WriteLine($"  {项.区域模板}：形状 {形.形状} {宽}×{高}（朝向 {形.朝向}，镜像 {(形.左右镜像 ? "是" : "否")}）→ 占地 {格} 格"
                                + $"{(好 ? "" : "  ✗ 掩码不连通/为空")}{(引用在 ? "" : "  ✗ 区域模板不存在")}");
                foreach (var 行 in 建筑外形.画(掩, 宽, 高)) Console.WriteLine("      " + 行);
                if (!好) 占地坏++;
                if (!引用在) 占地坏++;
            }
        }
        if (占地坏 == 0) Console.WriteLine("  ✓ 占地都合法且连通，区域模板引用全部存在");
        失败 += 占地坏;
        Console.WriteLine();

        var 汇总 = new List<string>();
        foreach (var 模板 in 世界根.世界)
        {
            if (模板 == null) continue;
            double 障碍和 = 0, 撤回和 = 0, 区域和 = 0;

            for (int 种子 = 1; 种子 <= 种子数; 种子++)
            {
                var 世界 = 大世界生成器.生成(种子, 模板, 取名);
                总世界数++;
                if (世界 == null) { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：生成失败"); continue; }

                // ① 确定性：两次生成指纹逐字节一致
                var 世界2 = 大世界生成器.生成(种子, 模板, 取名);
                if (世界2 == null || 世界.指纹() != 世界2.指纹())
                {
                    失败++;
                    string f1 = 世界.指纹(), f2 = 世界2?.指纹() ?? "";
                    var p1 = f1.Split(';');
                    var p2 = f2.Split(';');
                    string 差 = $"（段数 {p1.Length} vs {p2.Length}）";
                    for (int i = 0; i < Math.Min(p1.Length, p2.Length); i++)
                        if (p1[i] != p2[i]) { 差 = $"第 {i} 段：{p1[i]}  ←→  {p2[i]}"; break; }
                    Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：同种子两次生成不一致 —— {差}");
                }

                // ② 区域占地：数量 / 越界 / 掩码连通 / 互不重叠 / 不压营地
                var 区们 = 大世界生成器.区域列表(世界);
                var 营 = 大世界生成器.营地实体(世界);
                var 可达 = 世界.可达集();   // 后面"门口可达"的断言都要用，先算（连通分量数 另算，别混）
                int 期望区 = 0;
                if (模板.区域 != null)
                    foreach (var 项 in 模板.区域)
                        if (项 != null && !string.IsNullOrEmpty(项.区域模板) && 区表.ContainsKey(项.区域模板)) 期望区++;
                if (区们.Count != 期望区)
                { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：区域 {区们.Count} ≠ 期望 {期望区}（摆不下 {世界.区域摆不下}）"); }

                for (int i = 0; i < 区们.Count; i++)
                {
                    var a = 区们[i];
                    if (a.列 < 世界规格.边距 || a.行 < 世界规格.边距 ||
                        a.列 + a.宽 > 世界.列 - 世界规格.边距 || a.行 + a.高 > 世界.行 - 世界规格.边距)
                    { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：区域越界 {a.标识}@{a.列},{a.行} {a.宽}×{a.高}"); }
                    if (!建筑外形.连通(a.形状掩码, a.宽, a.高))
                    { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：{a.标识} 的占地掩码不连通"); }
                    if (营 != null && a.重叠(营))
                    { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：{a.标识} 压在营地上"); }
                    for (int j = i + 1; j < 区们.Count; j++)
                        if (a.重叠(区们[j]))
                        { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：区域重叠 {a.标识} / {区们[j].标识}"); }

                    // ③ 区域门口格：在自己掩码上 + **可通行且从入口可达**（走上去进副本）
                    //    "可通行"不够：门口格三面是区域实体，唯一出路是下面那一格；
                    //    那格一旦被障碍占掉，门口格就是"能站却走不到"的孤岛 —— 必须断言可达，不能只断言可通行。
                    var 区门 = 大世界生成器.区域门口格(a);
                    if (!a.覆盖(区门.列, 区门.行))
                    { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：{a.标识} 的门口格({区门.列},{区门.行}) 不在占地掩码上"); }
                    else if (!世界.可通行(区门.列, 区门.行))
                    { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：{a.标识} 的门口格({区门.列},{区门.行}) 不可通行"); }
                    else if (!可达.Contains(网格数据.编码(区门.列, 区门.行)))
                    { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：{a.标识} 的门口格({区门.列},{区门.行}) 从入口不可达（被堵死了）"); }
                }

                // ④ 玩家唯一 + 站在营地门口格上；营地门口格可通行 + 可达
                var 玩家们 = 世界.取类型(网格实体类型.玩家);
                if (玩家们.Count != 1)
                { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：玩家 {玩家们.Count} 个（必须恰好 1 个）"); }
                var 营门 = 大世界生成器.营地门口格(世界);
                if (!世界.可通行(营门.列, 营门.行))
                { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：营地门口格({营门.列},{营门.行}) 不可通行"); }
                if (玩家们.Count == 1 && (玩家们[0].列 != 营门.列 || 玩家们[0].行 != 营门.行))
                { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：玩家不在营地门口格上（{玩家们[0].列},{玩家们[0].行}）"); }
                if (!可达.Contains(网格数据.编码(营门.列, 营门.行)))
                { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：营地门口格从入口不可达"); }

                // ⑤ 连通分量 = 1
                int 分量 = 世界.连通分量数();
                if (分量 > 1) { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：可站格有孤岛（分量 {分量}）"); }

                // ⑥ 障碍不压区域 / 不压营地 / 不压任何门口四邻
                var 障碍们 = 世界.取类型(网格实体类型.障碍);
                foreach (var 障 in 障碍们)
                {
                    foreach (var 区 in 区们)
                    {
                        if (区.覆盖(障.列, 障.行))
                        { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：障碍 {障.标识} 压在 {区.标识} 上"); }
                        var 区门3 = 大世界生成器.区域门口格(区);
                        if (Math.Abs(障.列 - 区门3.列) <= 1 && Math.Abs(障.行 - 区门3.行) <= 1)
                        { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：障碍 {障.标识} 堵在 {区.标识} 门口"); }
                    }
                    if (营 != null && 营.覆盖(障.列, 障.行))
                    { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：障碍 {障.标识} 压在营地上"); }
                    if (Math.Abs(障.列 - 营门.列) <= 1 && Math.Abs(障.行 - 营门.行) <= 1)
                    { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：障碍 {障.标识} 堵在营地门口"); }
                }

                // ⑧ 撤回数必须是 0：连通校验只是保险，触发即代表"地图被切断 / 门口被堵"这类真 bug
                if (世界.撤掉的障碍 > 0)
                { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：撤回了 {世界.撤掉的障碍} 个障碍（说明连通真的被破坏了）"); }

                if (!大世界生成器.校验(世界, 模板)) { 失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：大世界生成器.校验 不通过"); }

                障碍和 += 障碍们.Count;
                撤回和 += 世界.撤掉的障碍;
                区域和 += 区们.Count;

                if (打示例 && 种子 == 1) 打一屏(世界, 模板, 区们, 营, 区表);
            }

            汇总.Add($"世界[{模板.标识}] {模板.列}×{模板.行}：平均区域 {(区域和 / 种子数):F1} / 障碍 {(障碍和 / 种子数):F1} / 撤回 {(撤回和 / 种子数):F1}"
                   + $"（营地在 {模板.营地?.列},{模板.营地?.行}，障碍清单 {模板.障碍数}）");
        }

        // ================= 搜索（一次性临时建筑） =================
        // 以营地门口当玩家位置，逐种子连翻 6 次，断言：
        //   ① 确定性：同参数两次落点一致；
        //   ② 落在半径内；
        //   ③ 2×2 每一格都可通行，且不占玩家站的那一格；
        //   ④ 放下去之后：临时建筑的门口格可通行 + **从入口可达**（不能被自己砌死）；
        //   ⑤ 放下去之后连通分量仍是 1（临时建筑不许把地图切成两半）。
        // ④⑤ 是重点：区域/营地的门口格有三面被自己挡着，临时建筑挨着放就可能把别人的门堵死 ——
        //        能放判据里显式排掉了"压别人门口格"，这里是它的守门人。
        Console.WriteLine("—— 搜索自检（以营地门口为玩家位置，逐种子连翻 6 次）——");
        int 搜索失败 = 0, 翻出 = 0, 太挤 = 0;
        foreach (var 模板 in 世界根.世界)
        {
            if (模板 == null) continue;
            int 本world翻出 = 0;
            for (int 种子 = 1; 种子 <= 种子数; 种子++)
            {
                var 世界 = 大世界生成器.生成(种子, 模板, 取名);
                if (世界 == null) continue;
                var 营门 = 大世界生成器.营地门口格(世界);
                var 参 = 模板.搜索 ?? new 世界搜索参数();
                int 半径 = Math.Max(1, 参.半径);

                for (int 次 = 1; 次 <= 6; 次++)
                {
                    int 流种子 = 确定性随机.派生(种子, $"搜索{次}");
                    var a = 大世界生成器.搜索落点(世界, 营门.列, 营门.行, 半径, 流种子);
                    var b = 大世界生成器.搜索落点(世界, 营门.列, 营门.行, 半径, 流种子);
                    if (a.找到 != b.找到 || a.列 != b.列 || a.行 != b.行)
                    { 搜索失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子} 第{次}搜：落点不确定"); }
                    if (!a.找到) { 太挤++; continue; }

                    if (Math.Abs(a.列 - 营门.列) > 半径 || Math.Abs(a.行 - 营门.行) > 半径)
                    { 搜索失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：落点({a.列},{a.行}) 在半径 {半径} 之外"); }

                    for (int r = a.行; r < a.行 + 大世界生成器.临时建筑高; r++)
                        for (int c = a.列; c < a.列 + 大世界生成器.临时建筑宽; c++)
                        {
                            if (!世界.可通行(c, r))
                            { 搜索失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：落点({a.列},{a.行}) 的 ({c},{r}) 本来就不通行"); }
                            if (c == 营门.列 && r == 营门.行)
                            { 搜索失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：落点把玩家站的那一格占掉了"); }
                        }

                    var 物 = 大世界生成器.放临时建筑(世界, a.列, a.行, 次.ToString(), $"测试窝点{次}", "");
                    if (物 == null) { 搜索失败++; continue; }
                    var 门 = 大世界生成器.临时建筑门口格(物);
                    if (!世界.可通行(门.列, 门.行))
                    { 搜索失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：临时建筑门口({门.列},{门.行}) 不可通行"); }
                    else if (!世界.可达集().Contains(网格数据.编码(门.列, 门.行)))
                    { 搜索失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：临时建筑门口({门.列},{门.行}) 从入口不可达（被自己砌死了）"); }
                    if (世界.连通分量数() > 1)
                    { 搜索失败++; Console.WriteLine($"✗ 世界[{模板.标识}] 种子{种子}：放临时建筑后出现孤岛（分量 {世界.连通分量数()}）"); }
                    翻出++; 本world翻出++;
                }
            }
            汇总.Add($"世界[{模板.标识}] 搜索：{种子数} 种子 × 6 次 → 翻出 {本world翻出} 间");
        }
        Console.WriteLine($"  翻出 {翻出} 间 / 附近太挤 {太挤} 次 / 失败 {搜索失败} 处");
        失败 += 搜索失败;

        Console.WriteLine();
        foreach (var 行 in 汇总) Console.WriteLine(行);
        Console.WriteLine();
        Console.WriteLine($"生成世界 {总世界数} 个");
        Console.WriteLine($"失败 {失败} 处（{总世界数} 个世界 × 2 次生成）");
        return 失败 == 0 ? 0 : 1;
    }

    // ================= 一屏 ASCII（100×100 太大，按 4 格降采样） =================

    private static void 打一屏(网格数据 世界, 世界模板 模板, List<网格实体> 区们, 网格实体 营, Dictionary<string, 区域模板> 区表)
    {
        const int 步 = 4;
        Console.WriteLine();
        Console.WriteLine($"【示例】{世界.名称} {世界.列}×{世界.行}　区域 {区们.Count}　障碍 {世界.取类型(网格实体类型.障碍).Count}　撤回 {世界.撤掉的障碍}（{模板.描述}）");
        Console.WriteLine($"  降采样 1/{步}（每格 = 世界上 {步}×{步} 格）：# 边界　C 营地　B 区域　+ 区域门　o 障碍　@ 你　· 空地");

        var 区门集 = new HashSet<int>();
        foreach (var 区 in 区们) { var m = 大世界生成器.区域门口格(区); 区门集.Add(网格数据.编码(m.列, m.行)); }
        int 营门码 = 营 != null ? 营.门口格 : -1;
        // 玩家自己那一格单独记：玩家站在营地门口格上，而 格上实体() 先返回"覆盖该格的营地"，
        // 所以不能让 @ 靠 格上实体() 去争（争不到）——直接按坐标标最高优先级
        var 你 = 世界.玩家();
        int 你码 = 你 != null ? 网格数据.编码(你.列, 你.行) : -1;

        for (int br = 0; br < 世界.行; br += 步)
        {
            var sb = new StringBuilder("  ");
            for (int bc = 0; bc < 世界.列; bc += 步)
            {
                char 符 = '·';
                for (int r = br; r < Math.Min(br + 步, 世界.行); r++)
                    for (int c = bc; c < Math.Min(bc + 步, 世界.列); c++)
                    {
                        int 码 = 网格数据.编码(c, r);
                        var e = 世界.格上实体(c, r);
                        char 本 = '·';
                        if (e == null) 本 = '·';
                        else if (e.是玩家) 本 = '@';
                        else if (e.类型 == 网格实体类型.墙) 本 = '#';
                        else if (e.类型 == 网格实体类型.区域) 本 = 'B';
                        else if (e.类型 == 网格实体类型.障碍) 本 = 'o';
                        else if (e.类型 == 网格实体类型.建筑) 本 = 'C';
                        if (码 == 营门码) 本 = 'C';
                        if (区门集.Contains(码)) 本 = '+';
                        if (优先级(本) > 优先级(符)) 符 = 本;
                    }
                if (你码 >= 0 && bc <= 你.列 && 你.列 < bc + 步 && br <= 你.行 && 你.行 < br + 步) 符 = '@';
                sb.Append(符);
            }
            Console.WriteLine(sb.ToString());
        }

        Console.WriteLine();
        Console.WriteLine($"  营地：「安全屋（营地）」{营?.宽}×{营?.高}@({营?.列},{营?.行}) → 玩家落点 ({世界.入口列},{世界.入口行})");
        foreach (var 区 in 区们)
        {
            var 门 = 大世界生成器.区域门口格(区);
            string 解 = (区.解锁 == null || 区.解锁.Length == 0) ? "无条件" : $"{区.解锁.Length} 条";
            string 内 = 区表.TryGetValue(区.定义标识 ?? "", out var t) ? $"{t.列}×{t.行}·危险{t.危险度}" : "（区域模板缺）";
            Console.WriteLine($"  {区.标识}「{区.名称}」{区.宽}×{区.高}@({区.列},{区.行}) → 门口格({门.列},{门.行})　解锁 {解}　副本内部 {内}");
        }
    }

    private static int 优先级(char 符) => 符 switch
    {
        '@' => 6,
        '+' => 5,
        'C' => 4,
        'B' => 3,
        'o' => 2,
        '#' => 1,
        _ => 0,
    };

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
