using System.Collections.Generic;

// 货币工具：三进制货币（1金 = 100银 = 10000铜）。内部统一以最小单位「铜币」存储与运算。
// 换算：金=10000铜，银=100铜。显示省略为零的单位（"128金30银" / "15金" / "80银" / "30铜"）。
public static class 货币工具
{
    public const int 金换算 = 10000;   // 1 金 = 10000 铜
    public const int 银换算 = 100;     // 1 银 = 100 铜

    // 铜币 → (金, 银, 铜)
    public static (int 金, int 银, int 铜) 拆解(int 铜币)
    {
        int 金 = 铜币 / 金换算;
        int 余 = 铜币 % 金换算;
        return (金, 余 / 银换算, 余 % 银换算);
    }

    // (金, 银, 铜) → 铜币
    public static int 组合(int 金, int 银 = 0, int 铜 = 0) => 金 * 金换算 + 银 * 银换算 + 铜;

    // 显示文本：省略为零的单位（0 显示 "0铜"）
    public static string 文本(int 铜币)
    {
        var (金, 银, 铜) = 拆解(铜币);
        var 段 = new List<string>();
        if (金 > 0) 段.Add($"{金}金");
        if (银 > 0) 段.Add($"{银}银");
        if (铜 > 0) 段.Add($"{铜}铜");
        if (段.Count == 0) return "0铜";
        return string.Join("", 段);
    }
}
