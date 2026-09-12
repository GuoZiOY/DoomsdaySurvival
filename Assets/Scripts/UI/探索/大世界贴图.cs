using UnityEngine;

// ============================================================
// 大世界贴图：运行时程序化生成**大世界层**（100×100 格子城）用到的"简单美术"。
// 与 区域贴图 一个路子：零外部资源，配色全走 网格面板配色（一处改全局变），Sprite 静态缓存（只生成一次）；
// 令牌、圆环、路径点线、障碍剪影这些"格子层通用图案"直接复用 区域贴图 / 房间贴图，不重复画。
//
// 只画三张新的（大世界独有的东西）：
//   地面  = 城市地面（比区域街道更冷、更空）
//   区域块 = 一片街区占地的**单格**砖纹（多格块逐格铺这张，非矩形掩码也自然拼对）
//   营地   = 安全屋占地单格（一块暖绿，和区域块一眼区分开）
// 真图优先：美术给图后放 Resources/沙盒贴图/<同名>.png 即自动用（与 区域贴图 同一条约定）。
// ============================================================
public static class 大世界贴图
{
    private static Sprite 地面缓存;
    private static Sprite 区域块缓存;
    private static Sprite 营地缓存;

    // ================= 城市地面 =================

    public static Sprite 地面()
    {
        if (地面缓存 != null) return 地面缓存;
        var 真图 = Resources.Load<Sprite>("沙盒贴图/大世界地面");
        if (真图 != null) return 地面缓存 = 真图;
        const int 边长 = 64;
        var 纹理 = 新纹理(边长, 边长);
        var 随机 = new System.Random(20260913);
        for (int y = 0; y < 边长; y++)
            for (int x = 0; x < 边长; x++)
            {
                float 斑 = Mathf.PerlinNoise(x * 0.045f, y * 0.045f);
                float 粒 = (float)随机.NextDouble();
                float t = Mathf.Clamp01(斑 * 0.65f + (粒 - 0.5f) * 0.35f);
                var 色 = Color.Lerp(网格面板配色.世界地面色, 网格面板配色.世界地面斑色, t);
                if (随机.NextDouble() < 0.015) 色 = 乘(色, 0.68f);   // 坑/碎屑
                纹理.SetPixel(x, y, 色);
            }
        return 地面缓存 = 成图(纹理);
    }

    // ================= 区域副本占地（单格砖纹） =================

    // 一片街区在世界上是**多格占地**：逐格铺这张，块内看不出格子、只在四边描外轮廓（见 大世界网格面板.建块边）。
    // 中间那条浅缝是"砖缝感"：让大块不至于是一片死板的纯色。
    public static Sprite 区域块()
    {
        if (区域块缓存 != null) return 区域块缓存;
        const int 边长 = 32;
        var 纹理 = 新纹理(边长, 边长);
        var 体 = 网格面板配色.区域块色;
        var 缝 = 网格面板配色.区域块缝色;
        var 随机 = new System.Random(20260914);
        for (int y = 0; y < 边长; y++)
            for (int x = 0; x < 边长; x++)
            {
                var 色 = 体;
                if (x == 0 || y == 0) 色 = 缝;                                  // 砖缝（相邻格拼起来是连续网格）
                else if (随机.NextDouble() < 0.06) 色 = 乘(体, 0.88f);           // 零星斑驳
                // 顶部一条窄亮带 = 楼顶受光（一块街区看起来有厚度）
                if (y >= 边长 - 3) 色 = 乘(体, 1.18f);
                纹理.SetPixel(x, y, 色);
            }
        return 区域块缓存 = 成图(纹理);
    }

    // ================= 营地（安全屋占地，单格） =================

    public static Sprite 营地()
    {
        if (营地缓存 != null) return 营地缓存;
        const int 边长 = 32;
        var 纹理 = 新纹理(边长, 边长);
        var 体 = 网格面板配色.营地体色;
        var 缝 = 网格面板配色.营地缝色;
        for (int y = 0; y < 边长; y++)
            for (int x = 0; x < 边长; x++)
            {
                var 色 = 体;
                if (x == 0 || y == 0) 色 = 缝;
                if (y >= 边长 - 3) 色 = 乘(体, 1.18f);
                纹理.SetPixel(x, y, 色);
            }
        return 营地缓存 = 成图(纹理);
    }

    // ================= 复用（区域层 / 房间层已有的通用图案） =================

    public static Sprite 门() => 区域贴图.入口();
    public static Sprite 障碍(string 名称) => 区域贴图.障碍(名称);
    public static Sprite 墙顶() => 房间贴图.墙顶();
    public static Sprite 圆盘() => 房间贴图.圆盘();
    public static Sprite 剪影() => 房间贴图.剪影();
    public static Sprite 圆环() => 房间贴图.圆环();
    public static Sprite 圆点() => 房间贴图.圆点();

    // ================= 工具 =================

    private static Texture2D 新纹理(int 宽, int 高)
    {
        var 纹理 = new Texture2D(宽, 高, TextureFormat.RGBA32, false);
        纹理.wrapMode = TextureWrapMode.Clamp;
        纹理.filterMode = FilterMode.Bilinear;
        return 纹理;
    }

    private static Sprite 成图(Texture2D 纹理)
    {
        纹理.Apply(false, false);
        return Sprite.Create(纹理, new Rect(0f, 0f, 纹理.width, 纹理.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Color 乘(Color 色, float k) => new Color(色.r * k, 色.g * k, 色.b * k, 色.a);
}
