using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 房间自检（Editor）：用「真实加载的 JSON 模板」批量跑房间生成，校验不变量（同 房间验证 那份断言，跑在 Unity 里）。
// 用途一：菜单 工具/房间/结构自检…（改完 房间模板.json / 搜索_地图类型.json 后先跑一遍再进游戏）。
// 用途二：命令行 -executeMethod RoomSelfCheck.Run（批处理回归）。
public static class RoomSelfCheck
{
    [MenuItem("末日/房间/结构自检（200 种子 × 每个模板）")]
    public static void Run() => 自检(200, true);

    [MenuItem("末日/房间/结构自检（30 种子 · 快速）")]
    public static void RunFast() => 自检(30, true);

    public static void 自检(int 种子数, bool 记日志)
    {
        var 数据 = new DataService(new EventBus());
        if (数据.房间模板.Count == 0)
        {
            Debug.LogError("[自检] 没有房间模板（Assets/Resources/Data/房间模板.json）");
            return;
        }

        int 失败 = 0, 总数 = 0, 保底 = 0, 容器总 = 0, 敌人总 = 0;
        foreach (var 模板 in 数据.房间模板.Values)
            for (int 种子 = 1; 种子 <= 种子数; 种子++)
            {
                var 甲 = 房间生成器.生成(种子, 模板, (地图, 房间) => 取容器表(数据, 地图, 房间), id => 取敌人组(数据, id));
                var 乙 = 房间生成器.生成(种子, 模板, (地图, 房间) => 取容器表(数据, 地图, 房间), id => 取敌人组(数据, id));
                if (甲 == null) { 失败++; Debug.LogError($"[自检] 生成 null：{模板.标识} 种子{种子}"); continue; }
                总数++;
                if (甲.走了保底布局) 保底++;
                容器总 += 甲.容器总数();
                敌人总 += 甲.敌人总数();

                if (甲.指纹() != 乙.指纹()) 失败 += 报($"{模板.标识} 种子{种子}：确定性失败（同种子两次不一致）");
                if (!甲.可通行(甲.入口列, 甲.入口行)) 失败 += 报($"{模板.标识} 种子{种子}：入口格不可通行");
                if (甲.连通分量数() != 1) 失败 += 报($"{模板.标识} 种子{种子}：可站格有孤岛");

                var 可达 = 甲.可达集();
                foreach (var 容器 in 甲.取类型(房间实体类型.容器))
                    if (!有相邻可达格(甲, 可达, 容器))
                        失败 += 报($"{模板.标识} 种子{种子}：容器搜不到（{容器.定义标识}@{容器.列},{容器.行}）");
                foreach (var 敌 in 甲.取类型(房间实体类型.敌人))
                    if (!有相邻可达格(甲, 可达, 敌))
                        失败 += 报($"{模板.标识} 种子{种子}：敌人打不着（{敌.定义标识}@{敌.列},{敌.行}）");

                // 结构重叠 / 越界
                for (int i = 0; i < 甲.实体.Count; i++)
                {
                    var a = 甲.实体[i];
                    if (a == null || a.类型 == 房间实体类型.墙 || a.是玩家) continue;
                    if (a.列 < 0 || a.行 < 0 || a.列 + a.宽 > 甲.列 || a.行 + a.高 > 甲.行)
                        失败 += 报($"{模板.标识} 种子{种子}：实体越界（{a.定义标识}）");
                    for (int j = i + 1; j < 甲.实体.Count; j++)
                    {
                        var b = 甲.实体[j];
                        if (b == null || b.类型 == 房间实体类型.墙 || b.是玩家) continue;
                        if (a.重叠(b)) 失败 += 报($"{模板.标识} 种子{种子}：实体重叠（{a.定义标识} / {b.定义标识}）");
                    }
                }

                // 寻路抽样
                var 流 = new System.Random(种子);
                for (int i = 0; i < 30; i++)
                {
                    int 起列 = 流.Next(甲.列), 起行 = 流.Next(甲.行);
                    int 终列 = 流.Next(甲.列), 终行 = 流.Next(甲.行);
                    if (!甲.可通行(起列, 起行) || !甲.可通行(终列, 终行)) continue;
                    var 路 = 房间寻路.寻路(甲, (起列, 起行), (终列, 终行));
                    if (路 == null) { 失败 += 报($"{模板.标识} 种子{种子}：可达却寻路失败"); continue; }
                    for (int k = 1; k < 路.Count; k++)
                    {
                        int 步 = Math.Abs(路[k].列 - 路[k - 1].列) + Math.Abs(路[k].行 - 路[k - 1].行);
                        if (步 != 1 || !甲.可通行(路[k].列, 路[k].行)) { 失败 += 报($"{模板.标识} 种子{种子}：路径非法"); break; }
                    }
                }

                // 视野
                var 可见 = 房间视野.可见集(甲, 甲.入口列, 甲.入口行, 甲.视野边长);
                if (可见.Count == 0 || !可见.Contains(房间数据.编码(甲.入口列, 甲.入口行)))
                    失败 += 报($"{模板.标识} 种子{种子}：入口视野异常");
            }

        string 汇总 = $"[自检] 房间层：生成 {总数} 次（{数据.房间模板.Count} 模板 × {种子数} 种子），" +
                      $"保底布局 {保底}，平均容器 {(总数 == 0 ? 0 : (float)容器总 / 总数):F1} / 敌人 {(总数 == 0 ? 0 : (float)敌人总 / 总数):F1}，失败 {失败}";
        if (失败 == 0) Debug.Log(汇总);
        else Debug.LogError(汇总);
        if (记日志 && 失败 > 0 && Application.isBatchMode) EditorApplication.Exit(1);
    }

    private static int 报(string 文本)
    {
        Debug.LogError($"[自检] {文本}");
        return 1;
    }

    private static List<搜索容器> 取容器表(DataService 数据, string 地图类型, string 房间标识)
    {
        var 结果 = new List<搜索容器>();
        if (!数据.搜索地图类型.TryGetValue(地图类型 ?? "", out var 类型) || 类型.房间 == null) return 结果;
        foreach (var 房 in 类型.房间)
        {
            if (房?.容器 == null) continue;
            if (!string.IsNullOrEmpty(房间标识) && 房.标识 != 房间标识) continue;
            foreach (var 容器 in 房.容器)
                if (容器 != null) 结果.Add(容器);
        }
        return 结果;
    }

    private static List<string> 取敌人组(DataService 数据, string 标识)
    {
        var 结果 = new List<string>();
        if (string.IsNullOrEmpty(标识) || !数据.敌人组.TryGetValue(标识, out var 组) || 组.敌人 == null) return 结果;
        foreach (var 项 in 组.敌人)
            for (int i = 0; i < Mathf.Max(1, 项.数量); i++)
                结果.Add(项.敌人);
        return 结果;
    }

    private static bool 有相邻可达格(房间数据 房, HashSet<int> 可达, 房间实体 e)
    {
        for (int r = e.行; r < e.行 + e.高; r++)
            for (int c = e.列; c < e.列 + e.宽; c++)
            {
                if (可达.Contains(房间数据.编码(c - 1, r))) return true;
                if (可达.Contains(房间数据.编码(c + 1, r))) return true;
                if (可达.Contains(房间数据.编码(c, r - 1))) return true;
                if (可达.Contains(房间数据.编码(c, r + 1))) return true;
            }
        return false;
    }
}
