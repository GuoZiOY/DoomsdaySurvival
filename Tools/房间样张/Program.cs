using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Text;
using System.Text.Json;

// ============================================================
// 房间样张：把「房间层第 1 刀」的一屏画成 PNG —— 用真数据生成真房间，再按游戏的配色与规矩画出来。
// 用途：在 Unity 铺开之前先确认观感（地表材质 / 墙 / 容器 / 令牌 / 迷雾 / 路径 六层叠起来顺不顺眼）。
// 用法：dotnet run --project Tools/房间样张   → docs/样张/房间样张-*.png
//
// 说明：这里画的是"效果预览"，不是游戏本体渲染；配色与《网格面板配色》房间段一一对应，
//       图层顺序与《房间图层》一致：地表 → 墙 → 物件 → 迷雾 → 路径 → 实体（玩家在最上）。
// ============================================================
public static class Program
{
    // —— 与 Assets/Scripts/UI/背包/网格面板基类/网格面板配色.cs 的"房间段"对应（改那边记得改这里）——
    static readonly Color 地板色 = Color.FromArgb(37, 37, 42);
    static readonly Color 地板斑色 = Color.FromArgb(55, 55, 62);
    static readonly Color 墙顶色 = Color.FromArgb(102, 97, 92);
    static readonly Color 墙身色 = Color.FromArgb(66, 64, 60);
    static readonly Color 容器体色 = Color.FromArgb(112, 88, 62);
    static readonly Color 容器高柜色 = Color.FromArgb(87, 71, 56);
    static readonly Color 尸体色 = Color.FromArgb(76, 76, 83);
    static readonly Color 敌人色 = Color.FromArgb(158, 51, 51);
    static readonly Color 玩家色 = Color.FromArgb(61, 184, 158);
    static readonly Color 迷雾未探索 = Color.FromArgb(5, 5, 8);
    static readonly Color 路径格色 = Color.FromArgb(92, 158, 230);
    static readonly Color 路径线色 = Color.FromArgb(153, 204, 255);
    static readonly Color 路径终色 = Color.FromArgb(255, 224, 107);
    static readonly Color 文字色 = Color.FromArgb(228, 228, 232);
    static readonly Color 暗字色 = Color.FromArgb(150, 150, 156);
    static readonly float[] 迷雾阶梯 = { 0f, 0.30f, 0.62f, 0.85f };

    const int 格 = 88;          // 单格像素（样张用；游戏里基类统一 90）
    const int 左边距 = 40, 上边距 = 104, 下边距 = 40;

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var 数据 = 读数据();
        if (数据 == null) return 2;
        var 输出目录 = Path.Combine(找仓库根(), "docs/样张");
        Directory.CreateDirectory(输出目录);

        var 样本 = new (string 模板, int 种子, string 文件名)[]
        {
            ("便利店_门厅", 7, "房间样张-便利店门厅.png"),
            ("居民房_客厅", 3, "房间样张-居民房客厅.png"),
        };
        foreach (var (模板标识, 种子, 文件名) in 样本)
        {
            if (!数据.房间模板.TryGetValue(模板标识, out var 模板)) { Console.WriteLine($"缺模板 {模板标识}"); continue; }
            var 房 = 房间生成器.生成(种子, 模板, 数据.取容器表, 数据.取敌人组);
            if (房 == null) { Console.WriteLine($"生成失败 {模板标识}"); continue; }
            画一屏(房, Path.Combine(输出目录, 文件名));
            Console.WriteLine($"已生成 {文件名}（{房.列}×{房.行}，容器 {房.容器总数()}，敌人 {房.敌人总数()}）");
        }
        return 0;
    }

    // ================= 画一屏 =================

    static void 画一屏(房间数据 房, string 路径)
    {
        int 宽 = 左边距 * 2 + 房.列 * 格;
        int 高 = 上边距 + 房.行 * 格 + 下边距 + 40;
        var 图 = new Bitmap(宽, 高);
        using var g = Graphics.FromImage(图);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Color.FromArgb(14, 14, 18));
        using var 字体 = new Font("Microsoft YaHei", 19f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var 小字体 = new Font("Microsoft YaHei", 16f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var 信息字体 = new Font("Microsoft YaHei", 20f, FontStyle.Regular, GraphicsUnit.Pixel);

        var 可见 = 房间视野.可见集(房, 房.入口列, 房.入口行, 房.视野边长);

        // 信息条
        using (var 笔 = new SolidBrush(文字色))
            g.DrawString($"{房.名称} · 危险 {危险点(房.危险度)}      行动 78/100      第 12 天 14:20      已搜 0/{房.容器总数()}      敌 {房.敌人总数()}",
                信息字体, 笔, 左边距, 44);

        // ① 地表（程序化水泥地：Perlin 斑块 + 颗粒 + 板缝）
        画地表(g, 房);

        // ② 墙（转角闭合：只在邻居不是墙的那条边描线）
        画墙(g, 房);

        // ③ 物件（容器/尸体；只有"探索过"的才画 —— 对应游戏里的迷雾记忆）
        foreach (var e in 房.实体)
        {
            if (e == null || e.类型 == 房间实体类型.墙 || e.是玩家 || e.类型 == 房间实体类型.敌人) continue;
            if (!已探索(房, 可见, e.列, e.行)) continue;
            画容器(g, 房, e, 字体, 小字体);
        }
        // 敌人：只在"当前可见"时画
        foreach (var e in 房.类型为(房间实体类型.敌人))
        {
            if (!可见.Contains(房间数据.编码(e.列, e.行))) continue;
            画敌人(g, e, 小字体);
        }

        // ④ 迷雾（可见 → 透明；已探索 → 压暗；未探索 → 黑板 + 阶梯柔化）
        画迷雾(g, 房, 可见);

        // ⑤ 路径（从玩家走到某个容器的真实 A* 结果）
        画路径(g, 房);

        // ⑥ 玩家令牌（永远在最上，不受迷雾影响）
        画玩家(g, 房, 小字体);

        // 提示行
        using (var 笔 = new SolidBrush(暗字色))
            g.DrawString("点地面移动 · 右键容器搜索 · 方向键逐格走 · 取消 = 离开", 小字体, 笔, 左边距, 上边距 + 房.行 * 格 + 16);

        图.Save(路径, ImageFormat.Png);
    }

    static void 画地表(Graphics g, 房间数据 房)
    {
        var 随机 = new Random(20260911);
        using var 刷 = new SolidBrush(地板色);
        using var 斑刷 = new SolidBrush(地板斑色);
        for (int r = 0; r < 房.行; r++)
            for (int c = 0; c < 房.列; c++)
            {
                int x = 左边距 + c * 格, y = 上边距 + r * 格;
                g.FillRectangle(刷, x, y, 格, 格);
                // 斑块：每格 3~5 个小噪声块（打破死板同色）
                for (int i = 0; i < 4; i++)
                {
                    int w = 随机.Next(10, 34), h = 随机.Next(8, 26);
                    int px = x + 随机.Next(2, 格 - w - 2), py = y + 随机.Next(2, 格 - h - 2);
                    using var 淡斑 = new SolidBrush(Color.FromArgb(随机.Next(14, 34), 斑刷.Color));
                    g.FillRectangle(淡斑, px, py, w, h);
                }
                // 板缝：每格一条更深的细线（地砖感）
                using var 缝 = new Pen(Color.FromArgb(70, 0, 0, 0), 1f);
                g.DrawLine(缝, x, y, x + 格, y);
                g.DrawLine(缝, x, y, x, y + 格);
            }
    }

    static void 画墙(Graphics g, 房间数据 房)
    {
        using var 顶刷 = new SolidBrush(墙顶色);
        using var 边笔 = new Pen(Color.FromArgb(150, 0, 0, 0), 2f);
        bool 是墙(int c, int r)
        {
            if (!房.界内(c, r)) return true;
            var e = 房.格上实体(c, r);
            return e != null && e.类型 == 房间实体类型.墙;
        }
        for (int r = 0; r < 房.行; r++)
            for (int c = 0; c < 房.列; c++)
            {
                if (!是墙(c, r)) continue;
                int x = 左边距 + c * 格, y = 上边距 + r * 格;
                g.FillRectangle(顶刷, x, y, 格, 格);
                using var 竖纹 = new Pen(Color.FromArgb(46, 0, 0, 0), 3f);
                for (int i = 8; i < 格; i += 14) g.DrawLine(竖纹, x + i, y + 4, x + i, y + 格 - 4);
                // 只在"邻居不是墙"的边描线（转角自然闭合、内边不重复画）
                if (!是墙(c, r - 1)) g.DrawLine(边笔, x, y, x + 格, y);
                if (!是墙(c, r + 1)) g.DrawLine(边笔, x, y + 格, x + 格, y + 格);
                if (!是墙(c - 1, r)) g.DrawLine(边笔, x, y, x, y + 格);
                if (!是墙(c + 1, r)) g.DrawLine(边笔, x + 格, y, x + 格, y + 格);
            }
    }

    static void 画容器(Graphics g, 房间数据 房, 房间实体 e, Font 字体, Font 小字体)
    {
        int x = 左边距 + e.列 * 格 + 5, y = 上边距 + e.行 * 格 + 5;
        int w = e.宽 * 格 - 10, h = e.高 * 格 - 10;
        using var 体刷 = new SolidBrush(e.挡视线 ? 容器高柜色 : 容器体色);
        using var 描笔 = new Pen(Color.FromArgb(190, 0, 0, 0), 2f);
        g.FillRectangle(体刷, x, y, w, h);
        g.DrawRectangle(描笔, x, y, w, h);
        // 剪影细节：按名称给"抽屉线 / 十字胶带 / 三层货架"
        using var 细笔 = new Pen(Color.FromArgb(120, 0, 0, 0), 2f);
        string 名 = e.名称 ?? "";
        if (名.Contains("架"))
        {
            for (int i = 1; i <= 3; i++) g.DrawLine(细笔, x + 6, y + h * i / 4, x + w - 6, y + h * i / 4);
        }
        else if (名.Contains("箱") || 名.Contains("堆"))
        {
            g.DrawLine(细笔, x + 4, y + h / 2, x + w - 4, y + h / 2);
            g.DrawLine(细笔, x + w / 2, y + 4, x + w / 2, y + h - 4);
        }
        else
        {
            g.DrawLine(细笔, x + 5, y + h / 2, x + w - 5, y + h / 2);
            using var 把刷 = new SolidBrush(Color.FromArgb(150, 0, 0, 0));
            g.FillRectangle(把刷, x + w - 14, y + h / 2 - 8, 5, 16);
        }
        using var 字刷 = new SolidBrush(Color.FromArgb(215, 245, 240, 235));
        var 尺寸 = g.MeasureString(名, 字体);
        g.DrawString(名, 字体, 字刷, x + (w - 尺寸.Width) / 2f, y + (h - 尺寸.Height) / 2f);
    }

    static void 画敌人(Graphics g, 房间实体 e, Font 小字体)
    {
        int cx = 左边距 + e.列 * 格 + 格 / 2, cy = 上边距 + e.行 * 格 + 格 / 2;
        int 半径 = 格 / 2 - 12;
        using var 体刷 = new SolidBrush(敌人色);
        using var 描笔 = new Pen(Color.FromArgb(220, 0, 0, 0), 2f);
        g.FillEllipse(体刷, cx - 半径, cy - 半径, 半径 * 2, 半径 * 2);
        g.DrawEllipse(描笔, cx - 半径, cy - 半径, 半径 * 2, 半径 * 2);
        using var 剪影 = new SolidBrush(Color.FromArgb(190, 30, 10, 10));
        g.FillEllipse(剪影, cx - 9, cy - 16, 18, 18);
        g.FillRectangle(剪影, cx - 11, cy + 2, 22, 16);
        // 头顶血条
        using var 血刷 = new SolidBrush(Color.FromArgb(205, 66, 66));
        g.FillRectangle(血刷, cx - 半径, cy - 半径 - 12, 半径 * 2, 6);
    }

    static void 画迷雾(Graphics g, 房间数据 房, HashSet<int> 可见)
    {
        for (int r = 0; r < 房.行; r++)
            for (int c = 0; c < 房.列; c++)
            {
                int x = 左边距 + c * 格, y = 上边距 + r * 格;
                if (可见.Contains(房间数据.编码(c, r))) continue;
                if (已探索(房, 可见, c, r))
                {
                    using var 暗 = new SolidBrush(Color.FromArgb(115, 0, 0, 0));
                    g.FillRectangle(暗, x, y, 格, 格);
                    continue;
                }
                int 距 = 到可见的距离(房, 可见, c, r);
                int 档 = Math.Min(距, 迷雾阶梯.Length - 1);
                using var 黑 = new SolidBrush(Color.FromArgb((int)(迷雾阶梯[档] * 255), 迷雾未探索));
                g.FillRectangle(黑, x, y, 格, 格);
            }
    }

    static void 画路径(Graphics g, 房间数据 房)
    {
        // 取最近的容器，画"走过去"的真实 A* 路径
        房间实体 目标 = null;
        int 最近 = int.MaxValue;
        foreach (var e in 房.类型为(房间实体类型.容器))
        {
            int 距 = Math.Abs(e.列 - 房.入口列) + Math.Abs(e.行 - 房.入口行);
            if (距 >= 最近) continue;
            最近 = 距;
            目标 = e;
        }
        if (目标 == null) return;
        var 路 = 房间寻路.寻路到相邻(房, (房.入口列, 房.入口行), 目标);
        if (路 == null || 路.Count == 0) return;
        using var 格刷 = new SolidBrush(Color.FromArgb(76, 路径格色));
        foreach (var (c, r) in 路)
            g.FillRectangle(格刷, 左边距 + c * 格, 上边距 + r * 格, 格, 格);
        using var 点刷 = new SolidBrush(路径线色);
        for (int i = 0; i + 1 < 路.Count; i++)
        {
            int x = 左边距 + (int)((路[i].列 + 路[i + 1].列) * 0.5f * 格) + 格 / 2;
            int y = 上边距 + (int)((路[i].行 + 路[i + 1].行) * 0.5f * 格) + 格 / 2;
            g.FillEllipse(点刷, x - 4, y - 4, 8, 8);
        }
        var 终 = 路[路.Count - 1];
        using var 终笔 = new Pen(路径终色, 3f);
        int tx = 左边距 + 终.列 * 格 + 格 / 2, ty = 上边距 + 终.行 * 格 + 格 / 2;
        g.DrawEllipse(终笔, tx - 10, ty - 10, 20, 20);
        g.DrawLine(终笔, tx - 16, ty, tx + 16, ty);
        g.DrawLine(终笔, tx, ty - 16, tx, ty + 16);
    }

    static void 画玩家(Graphics g, 房间数据 房, Font 小字体)
    {
        int cx = 左边距 + 房.入口列 * 格 + 格 / 2, cy = 上边距 + 房.入口行 * 格 + 格 / 2;
        int 半径 = 格 / 2 - 10;
        using var 影 = new SolidBrush(Color.FromArgb(90, 0, 0, 0));
        g.FillEllipse(影, cx - 半径, cy - 半径 + 4, 半径 * 2, 半径 * 2);
        using var 体刷 = new SolidBrush(玩家色);
        g.FillEllipse(体刷, cx - 半径, cy - 半径, 半径 * 2, 半径 * 2);
        using var 环笔 = new Pen(Color.FromArgb(230, 216, 255, 245), 3f);
        g.DrawEllipse(环笔, cx - 半径, cy - 半径, 半径 * 2, 半径 * 2);
        using var 剪影 = new SolidBrush(Color.FromArgb(200, 20, 40, 36));
        g.FillEllipse(剪影, cx - 8, cy - 15, 16, 16);
        g.FillRectangle(剪影, cx - 10, cy + 2, 20, 15);
        using var 字刷 = new SolidBrush(Color.FromArgb(240, 255, 255, 255));
        g.DrawString("你", 小字体, 字刷, cx - 9, cy + 半径 - 4);
    }

    // ================= 工具 =================

    static bool 已探索(房间数据 房, HashSet<int> 可见, int 列, int 行)
        => 可见.Contains(房间数据.编码(列, 行)) || 邻近可见(房, 可见, 列, 行);

    // 样张用简化记忆：可见格及其周围 3 格算"探索过"（游戏里由 已探索 集合负责）
    static bool 邻近可见(房间数据 房, HashSet<int> 可见, int 列, int 行)
    {
        for (int r = 行 - 2; r <= 行 + 2; r++)
            for (int c = 列 - 2; c <= 列 + 2; c++)
                if (可见.Contains(房间数据.编码(c, r))) return true;
        return false;
    }

    static int 到可见的距离(房间数据 房, HashSet<int> 可见, int 列, int 行)
    {
        for (int 距 = 1; 距 <= 5; 距++)
            for (int r = 行 - 距; r <= 行 + 距; r++)
                for (int c = 列 - 距; c <= 列 + 距; c++)
                    if (Math.Max(Math.Abs(c - 列), Math.Abs(r - 行)) == 距 && 可见.Contains(房间数据.编码(c, r)))
                        return 距;
        return 5;
    }

    static string 危险点(int 危险度)
    {
        var sb = new StringBuilder();
        for (int i = 1; i <= 5; i++) sb.Append(i <= 危险度 ? '●' : '○');
        return sb.ToString();
    }

    // ================= 数据 =================

    sealed class 测试数据
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

        public List<string> 取敌人组(string 标识)
        {
            var 结果 = new List<string>();
            if (string.IsNullOrEmpty(标识) || !敌人组.TryGetValue(标识, out var 组) || 组.敌人 == null) return 结果;
            foreach (var 项 in 组.敌人)
                for (int i = 0; i < Math.Max(1, 项.数量); i++)
                    结果.Add(项.敌人);
            return 结果;
        }
    }

    static 测试数据 读数据()
    {
        var 根 = 找仓库根();
        if (根 == null) { Console.WriteLine("找不到仓库根"); return null; }
        var 选项 = new JsonSerializerOptions { IncludeFields = true, AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip };
        var 数据 = new 测试数据();
        var 搜索 = 读<搜索地图类型根>(Path.Combine(根, "Assets/Resources/Data/搜索_地图类型.json"), 选项);
        if (搜索?.地图类型 != null)
            foreach (var 项 in 搜索.地图类型)
                if (项 != null) 数据.搜索地图类型[项.标识] = 项;
        var 遭遇 = 读<敌人组根>(Path.Combine(根, "Assets/Resources/Data/encounters.json"), 选项);
        if (遭遇?.敌人组 != null)
            foreach (var 项 in 遭遇.敌人组)
                if (项 != null) 数据.敌人组[项.标识] = 项;
        var 房间 = 读<房间模板根>(Path.Combine(根, "Assets/Resources/Data/房间模板.json"), 选项);
        if (房间?.房间 != null)
            foreach (var 项 in 房间.房间)
                if (项 != null) 数据.房间模板[项.标识] = 项;
        return 数据.房间模板.Count > 0 ? 数据 : null;
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

// 便捷扩展：按类型筛实体（样张里用着顺手）
public static class 房间实体扩展
{
    public static List<房间实体> 类型为(this 房间数据 房, 房间实体类型 类型) => 房.取类型(类型);
}
