using System;
using System.IO;
using System.Text;
using UnityEngine;

// 存档磁盘 IO（Unity 专属，故留在 Services）：一个槽 = 一个 JSON 文件。
//
// 为什么不用 PlayerPrefs（v4 的做法）：
//   ① Windows 上它是**注册表**：卸载即丢、清注册表即丢、无法备份、无法 diff；
//   ② 只有一个键 = 只有一个槽，覆盖式写入，玩家没法"留一个昨天的档试试别的路"；
//   ③ 大小与编码不受控（大档会被悄悄截断/报错）。
// 文件 = 可备份、可多槽、出问题能直接打开看（排查坏档靠这个）。
//
// **原子写**：先写 `<文件>.tmp` → 再替换正式文件（旧文件自动留成 `.bak`）。
// 为什么要这么麻烦：存档最不能接受的失败是"写到一半断电 → 正式文件变成半截 JSON"，
// 那等于把玩家已有的档也毁掉。写 tmp + 替换，最坏情况是"这次没存上"，而不是"档没了"。
//
// ★ 本文件是**唯一**允许出现 `Application.persistentDataPath` 的地方
//   （门禁 Tools/存档接线核对 段[4] 守着这条）—— 路径散开就会写出两个互不相认的存档目录。
public static class 存档文件
{
    private static readonly UTF8Encoding 无BOM = new UTF8Encoding(false);

    // 存档目录：`%userprofile%/AppData/LocalLow/<公司>/<产品>/存档`（Unity persistentDataPath）。
    // 与游戏资源目录分开 → 重新安装/覆盖游戏不会碰到玩家的档。
    public static string 根目录 => Path.Combine(Application.persistentDataPath, 存档规格.目录名);

    // —— 正式槽位 ——
    public static string 槽路径(int 槽) => 路径(存档规格.文件名(槽));

    public static void 确保目录()
    {
        try { if (!Directory.Exists(根目录)) Directory.CreateDirectory(根目录); }
        catch (Exception 异常) { Debug.LogError($"[存档] 建目录失败: {根目录} — {异常.Message}"); }
    }

    public static bool 存在(int 槽) => 存在文本(存档规格.文件名(槽));
    public static string 读(int 槽) => 读文本(存档规格.文件名(槽));
    public static bool 写(int 槽, string 文本) => 写文本(存档规格.文件名(槽), 文本);
    public static void 删(int 槽) => 删文本(存档规格.文件名(槽));

    // —— 按文件名（只给离线自检这类**测试**用；正式存档一律走槽位） ——
    public static string 路径(string 文件名) => Path.Combine(根目录, 文件名);

    public static bool 存在文本(string 文件名)
    {
        try { return File.Exists(路径(文件名)); }
        catch { return false; }
    }

    // 读原始 JSON 文本。失败/不存在 → null（**不抛**：坏档不该让游戏进不去）。
    public static string 读文本(string 文件名)
    {
        var 全路径 = 路径(文件名);
        try
        {
            if (!File.Exists(全路径)) return null;
            return File.ReadAllText(全路径, Encoding.UTF8);
        }
        catch (Exception 异常)
        {
            Debug.LogError($"[存档] 读取失败: {全路径} — {异常.Message}");
            return null;
        }
    }

    // 原子写：tmp → 替换（旧文件留成 .bak）。返回是否成功。
    public static bool 写文本(string 文件名, string 文本)
    {
        确保目录();
        var 全路径 = 路径(文件名);
        var 临时 = 全路径 + ".tmp";
        var 备份 = 全路径 + ".bak";
        try
        {
            File.WriteAllText(临时, 文本, 无BOM);
            if (File.Exists(全路径))
            {
                // File.Replace 会把 旧文件 移到 备份 并 原子替换 —— 这是 .NET 里最接近"事务"的做法
                File.Replace(临时, 全路径, 备份);
            }
            else
            {
                File.Move(临时, 全路径);
            }
            return true;
        }
        catch (Exception 异常)
        {
            Debug.LogError($"[存档] 写入失败: {全路径} — {异常.Message}");
            try { if (File.Exists(临时)) File.Delete(临时); } catch { /* 清理失败无所谓 */ }
            return false;
        }
    }

    // 删除（正式文件 + tmp；**.bak 故意留下** —— 玩家点了"删除"之后如果后悔，
    // 还能在存档目录里手动把 .bak 改回来。物理删干净就真没了。）
    public static void 删文本(string 文件名)
    {
        var 全路径 = 路径(文件名);
        try
        {
            if (File.Exists(全路径)) File.Delete(全路径);
            var 临时 = 全路径 + ".tmp";
            if (File.Exists(临时)) File.Delete(临时);
        }
        catch (Exception 异常) { Debug.LogError($"[存档] 删除失败: {全路径} — {异常.Message}"); }
    }

    // 人类可读的现实时间（槽位列表显示用）。Unix 秒 → 本地时间字符串。
    public static string 现实时间文本(long unix秒)
    {
        if (unix秒 <= 0) return "—";
        try { return DateTimeOffset.FromUnixTimeSeconds(unix秒).ToLocalTime().ToString("yyyy-MM-dd HH:mm"); }
        catch { return "—"; }
    }
}
