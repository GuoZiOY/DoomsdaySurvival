using System;
using UnityEditor;
using UnityEngine;

// 沙盒自检（Editor）：用「真实加载的 JSON 模板」批量跑世界沙盒生成，校验不变量。
// 用途一：CI/命令行 编译与数据自检——
//   Unity.exe -batchmode -quit -projectPath "<项目>" -executeMethod 沙盒自检.执行 -logFile -
//   （有编译错误时本方法不会被调用，日志里只会出现 error CS → 即编译校验）
// 用途二：改完 地区模板.json / 建筑模板.json / 搜索_地图类型.json 后，先跑一遍再进游戏。
public static class 沙盒自检
{
    private const int 种子数 = 60;

    // 编辑器菜单入口（不用关编辑器也能跑：结果看 Console 的 [自检] 行）
    [MenuItem("工具/世界沙盒/结构自检（60 种子 × 每个地区）")]
    public static void 菜单自检() => 执行();

    public static void 执行()
    {
        int 失败 = 0;
        var 事件 = new EventBus();
        var 数据 = new DataService(事件);
        foreach (var 错误 in 数据.校验错误) Debug.LogError($"[自检] 数据校验: {错误}");
        Debug.Log($"[自检] 载入：物品 {数据.物品.Count} / 敌人 {数据.敌人.Count} / 敌人组 {数据.敌人组.Count} / " +
                  $"地图类型 {数据.搜索地图类型.Count} / 建筑模板 {数据.建筑模板.Count} / 地区模板 {数据.地区模板.Count} / 棋盘 {数据.战斗棋盘.Count}");

        if (数据.地区模板.Count == 0) { Debug.LogError("[自检] 没有地区模板（Assets/Resources/Data/地区模板.json）"); 编辑器退出(1); return; }

        int 楼层总数 = 0, 容器总数 = 0, 生成次数 = 0;
        foreach (var 地区 in 数据.地区模板.Values)
        {
            for (int 种子 = 1; 种子 <= 种子数; 种子++)
            {
                生成次数++;
                var 片区 = 区域生成器.生成(种子, 地区,
                    id => 数据.建筑模板.TryGetValue(id, out var 楼) ? 楼 : null,
                    id => 数据.搜索地图类型.TryGetValue(id, out var 类型) ? 类型 : null);
                if (片区?.街道 == null) { 失败++; Debug.LogError($"[自检] 种子{种子} 地区[{地区.标识}] 生成失败"); continue; }
                if (片区.建筑.Count == 0) { 失败++; Debug.LogError($"[自检] 种子{种子} 地区[{地区.标识}] 一栋楼都没生成"); }

                var 街道可达 = 沙盒格工具.可达集(片区.街道, 片区.街道.出生列, 片区.街道.出生行);
                foreach (var (列, 行, 格) in 片区.街道.全部())
                {
                    if (格 == null || !格.有对象) continue;
                    if (格.对象 == 沙盒对象.敌人) continue;
                    if (!沙盒格工具.可达(片区.街道, 街道可达, 列, 行))
                    { 失败++; Debug.LogError($"[自检] 种子{种子} {地区.标识} 街道对象不可达 {格.对象}[{格.标识}]@({列},{行})"); }
                }

                foreach (var 楼 in 片区.建筑)
                {
                    if (楼 == null || 楼.楼层.Count == 0) { 失败++; Debug.LogError($"[自检] 种子{种子} {地区.标识} 空楼"); continue; }
                    if (楼.入口列 < 0) { 失败++; Debug.LogError($"[自检] 种子{种子} {楼.标识} 缺入口"); }
                    楼层总数 += 楼.楼层.Count;
                    foreach (var 层 in 楼.楼层)
                    {
                        if (层 == null) { 失败++; continue; }
                        var 层可达 = 沙盒格工具.可达集(层, 层.出生列, 层.出生行);
                        var 层含占位 = 沙盒格工具.可达集含占位(层, 层.出生列, 层.出生行);
                        bool 有楼梯 = false;
                        int 门数 = 0;
                        foreach (var (列, 行, 格) in 层.全部())
                        {
                            if (格 == null) continue;
                            if (格.是门) 门数++;
                            if (!格.有对象 && !格.是门) continue;
                            if (格.对象 == 沙盒对象.楼梯) 有楼梯 = true;
                            if (格.对象 == 沙盒对象.搜索点)
                            {
                                容器总数++;
                                if (!沙盒格工具.有相邻落脚点(层, 列, 行, 层含占位))
                                { 失败++; Debug.LogError($"[自检] 种子{种子} {楼.标识} {层.名称} 家具被围死 @({列},{行})"); }
                            }
                            if (!沙盒格工具.可达(层, 层可达, 列, 行))
                            { 失败++; Debug.LogError($"[自检] 种子{种子} {楼.标识} {层.名称} 对象不可达 {格.对象}[{格.标识}]@({列},{行})"); }
                            else if (格.是门 || 格.对象 == 沙盒对象.楼梯 || 格.对象 == 沙盒对象.建筑入口)
                                if (!沙盒格工具.可达(层, 层含占位, 列, 行))
                                { 失败++; Debug.LogError($"[自检] 种子{种子} {楼.标识} {层.名称} 关键物件被家具堵死 {格.对象}@({列},{行})"); }
                        }
                        if (门数 == 0) { 失败++; Debug.LogError($"[自检] 种子{种子} {楼.标识} {层.名称} 一扇门都没有"); }
                        if (楼.楼层.Count > 1 && !有楼梯) { 失败++; Debug.LogError($"[自检] 种子{种子} {楼.标识} {层.名称} 多层楼却没有楼梯"); }
                    }
                }
            }
        }
        Debug.Log($"[自检] 完成：生成 {生成次数} 次（{数据.地区模板.Count} 地区 × {种子数} 种子），楼层 {楼层总数}，容器实例 {容器总数}，失败 {失败}");
        编辑器退出(失败 == 0 && 数据.校验错误.Count == 0 ? 0 : 1);
    }

    // 命令行批处理模式：给出明确退出码（CI 用；编辑器里手动执行时无副作用）
    private static void 编辑器退出(int 码)
    {
        if (Application.isBatchMode) EditorApplication.Exit(码);
    }
}

// ASCII 入口（-executeMethod 传中文类名在部分 shell/编码下会丢失，故留一个纯英文别名）：
//   Unity.exe -batchmode -quit -projectPath "<项目>" -executeMethod SandboxSelfCheck.Run -logFile -
public static class SandboxSelfCheck
{
    public static void Run() => 沙盒自检.执行();
}
