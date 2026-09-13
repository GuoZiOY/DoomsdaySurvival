// ============================================================
// UnityEngine 桩（**只为让纯逻辑代码能在 dotnet 下编译运行**）
// 原则：这里只放"值类型/无行为"的那几个（Color / ColorUtility / Mathf / Debug），
//       一旦发现某个游戏类"非 Unity 不可"才能跑，那就说明它该被拆（Domain 应零 UnityEngine）。
// 为什么需要：`品质.cs` 的 `品质工具.颜色()` 返回 `Color`、`富文本标签()` 用 `ColorUtility` ——
//   这两个是**表现层的东西长在 Domain 上**（审计报告 01 的 A3 已记为 🔴）。
//   本桩是临时通行证：等哪天把颜色挪去 UI 层，`品质.cs` 就能像 `战斗单位.cs` 一样零桩编译。
// ============================================================
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1f, 1f, 1f, 1f);
        public static Color black => new Color(0f, 0f, 0f, 1f);
    }

    public static class ColorUtility
    {
        // Unity 的实现：把 0~1 的通道转成两位十六进制（四舍五入）
        public static string ToHtmlStringRGB(Color c)
        {
            int R = (int)Math.Round(Math.Clamp(c.r, 0f, 1f) * 255f);
            int G = (int)Math.Round(Math.Clamp(c.g, 0f, 1f) * 255f);
            int B = (int)Math.Round(Math.Clamp(c.b, 0f, 1f) * 255f);
            return R.ToString("X2") + G.ToString("X2") + B.ToString("X2");
        }
    }

    public static class Mathf
    {
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static int RoundToInt(float v) => (int)Math.Round(v);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);
        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static float Abs(float v) => Math.Abs(v);
        public static int Abs(int v) => Math.Abs(v);
    }

    public static class Debug
    {
        public static void Log(object m) => Console.WriteLine("[Log] " + m);
        public static void LogWarning(object m) => Console.WriteLine("[Warn] " + m);
        public static void LogError(object m) => Console.WriteLine("[Error] " + m);
    }

    // 可播种随机（UnityEngine.Random 的替身）：**同一颗种子给同一串数** ——
    // 战斗里"不可复现"的根就是原来直接用 UnityEngine.Random（全局静态、无法播种）。本桩让它可复现。
    public static class Random
    {
        private static System.Random 源 = new System.Random(12345);
        public static void InitState(int 种子) => 源 = new System.Random(种子);
        public static float value => (float)源.NextDouble();
        public static int Range(int 最小, int 最大) => 最大 <= 最小 ? 最小 : 源.Next(最小, 最大);
        public static float Range(float 最小, float 最大) => 最小 + (float)源.NextDouble() * (最大 - 最小);
    }
}

// ============================================================
// `玩家档案` 的最小桩（**只为让 `战斗单位.从玩家投影` 能编译**）
// ⚠ 这是本验证器里唯一一处"验副本"的地方，代价写在明处：
//   · 它只包含 从玩家投影 用到的成员；真实 玩家档案 改成员名 → 本验证器不会发现（游戏程序集编译会发现）；
//   · 所以本批**不**对 从玩家投影 下断言（只保证它编译得过）。
//   要不要把它换成真身？需要先把 `Events/导航事件.cs` 里那个 `面板基类` 字段摘掉（事件不该拖 UI）——
//   那是"分层收敛"那一刀的事，见 docs/优化实施进度.md §二 档2。
// ============================================================
public sealed class 已学技能记录
{
    public string 标识;
    public int 熟练等级;
}

public sealed class 玩家档案
{
    public int 最大生命 = 100, 生命 = 100;
    public int 最大行动点 = 50, 行动点 = 50;
    public int 近战伤害 = 10, 枪械伤害 = 8, 总防御 = 5, 速度 = 10, 敏捷 = 5;
    public float 暴击概率 = 0.1f, 闪避概率 = 0.05f;
    public int 抗性百分比;
    public List<string> 战斗技能槽 = new List<string>();
    public List<已学技能记录> 已学技能 = new List<已学技能记录>();
    public string 装备标识(string 槽位) => null;
    public Func<string, 武器种类> 武器种类解析;
    // v51 刀35：`战斗单位.命中率公式` 会问装备要"命中/暴击…副属性"（装备本体 + 配件）。真实实现在
    //   `Domain/容器/装备管理器.cs`（依赖闭包太大：物品表 + 网格 + 全部管理器）→ 这里是**最小替身**，
    //   恒返回 0（等价于"光身进场"）。代价写在明处：本验证器不验"装备加成进战斗"这条链。
    public 假装备管理 装备管理 = new 假装备管理();
}

public sealed class 假装备管理
{
    public int 副属性(加成类型 类) => 0;
    public int 配件加成(加成类型 类) => 0;
}

// 注：`伤病类型` 原来在这里抄了一份（因为 玩家档案.cs 没链接进来）—— 刀30 把它搬进 Data\数据模型.cs 后
//     本验证器直接链接到真身了，副本已删（数据模型.cs 已在 csproj 里）。

