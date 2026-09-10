using UnityEngine;

// ============================================================
// 房间贴图：运行时程序化生成房间层用到的全部"简单美术"（无美术资源阶段的表现来源）。
// 为什么放代码里生成：不依赖任何外部图；配色仍走 网格面板配色（一处改全局变）。
// 以后美术给了真图：在 Resources/沙盒贴图/ 下放同名 PNG（地板/墙顶/柜子/冰箱/纸箱/货架/尸体），
// 本类会优先用真图，找不到才程序化生成 —— 图一放进去就自动接管。
// 全部 Sprite 静态缓存（只生成一次）。
// ============================================================
public static class 房间贴图
{
    private const string 资源目录 = "沙盒贴图/";
    private const float 每单位像素 = 100f;

    private static Sprite 地板缓存, 墙顶缓存, 柜子缓存, 冰箱缓存, 纸箱缓存, 货架缓存, 尸体缓存;
    private static Sprite 圆盘缓存, 圆环缓存, 剪影缓存, 圆点缓存;

    // ================= 地表 / 墙 =================

    // 水泥地：低频斑块（Perlin）+ 高频颗粒 + 板缝
    public static Sprite 地板()
    {
        if (地板缓存 != null) return 地板缓存;
        var 真图 = Resources.Load<Sprite>(资源目录 + "地板");
        if (真图 != null) return 地板缓存 = 真图;
        const int 边长 = 64;
        var 纹理 = 新纹理(边长, 边长);
        var 随机 = new System.Random(20260911);
        for (int y = 0; y < 边长; y++)
            for (int x = 0; x < 边长; x++)
            {
                float 斑 = Mathf.PerlinNoise(x * 0.07f, y * 0.07f);
                float 粒 = (float)随机.NextDouble();
                float 缝 = x % 16 == 0 || y % 16 == 0 ? 0.82f : 1f;
                float t = Mathf.Clamp01(斑 * 0.75f + (粒 - 0.5f) * 0.25f) * 缝;
                纹理.SetPixel(x, y, Color.Lerp(网格面板配色.房间地板色, 网格面板配色.房间地板斑色, t));
            }
        return 地板缓存 = 成图(纹理);
    }

    // 墙顶：底色 + 竖向纹理 + 顶部一条更深的压边（有"墙"的体量感）
    public static Sprite 墙顶()
    {
        if (墙顶缓存 != null) return 墙顶缓存;
        var 真图 = Resources.Load<Sprite>(资源目录 + "墙顶");
        if (真图 != null) return 墙顶缓存 = 真图;
        const int 边长 = 64;
        var 纹理 = 新纹理(边长, 边长);
        var 随机 = new System.Random(77123);
        var 深 = 乘(网格面板配色.房间墙顶色, 0.78f);
        var 更深 = 乘(网格面板配色.房间墙顶色, 0.62f);
        for (int y = 0; y < 边长; y++)
            for (int x = 0; x < 边长; x++)
            {
                var 色 = 网格面板配色.房间墙顶色;
                if (x % 14 < 3) 色 = 深;                                  // 竖纹
                if (随机.NextDouble() < 0.05) 色 = 乘(色, 0.94f);          // 颗粒
                if (y >= 边长 - 4) 色 = 更深;                              // 顶面压边
                纹理.SetPixel(x, y, 色);
            }
        return 墙顶缓存 = 成图(纹理);
    }

    // ================= 容器剪影（按名称挑样子） =================

    public static Sprite 容器(string 名称)
    {
        string 名 = 名称 ?? "";
        if (名.Contains("冰箱") || 名.Contains("冷柜")) return 冰箱();
        if (名.Contains("箱") || 名.Contains("堆")) return 纸箱();
        if (名.Contains("架")) return 货架();
        return 柜子();
    }

    // 柜子：圆角深木色块 + 黑描边 + 抽屉线 + 把手
    private static Sprite 柜子()
    {
        if (柜子缓存 != null) return 柜子缓存;
        var 真图 = Resources.Load<Sprite>(资源目录 + "柜子");
        if (真图 != null) return 柜子缓存 = 真图;
        const int 边长 = 96;
        var 纹理 = 新纹理(边长, 边长);
        var 体 = 网格面板配色.容器体色;
        var 深 = 乘(体, 0.72f);
        for (int y = 0; y < 边长; y++)
            for (int x = 0; x < 边长; x++)
            {
                int 边 = 6;
                bool 内 = x >= 边 && y >= 边 && x < 边长 - 边 && y < 边长 - 边;
                bool 描边 = !内 && x >= 边 - 3 && y >= 边 - 3 && x < 边长 - 边 + 3 && y < 边长 - 边 + 3;
                var 色 = 内 ? 体 : (描边 ? Color.black : new Color(0, 0, 0, 0));
                if (内)
                {
                    if (y == 边长 / 2 || y == 边长 / 2 + 1) 色 = 深;       // 抽屉缝
                    if (x >= 边长 - 24 && x <= 边长 - 16 && y >= 边长 / 2 - 8 && y <= 边长 / 2 + 8) 色 = 深;   // 把手
                }
                纹理.SetPixel(x, y, 色);
            }
        return 柜子缓存 = 成图(纹理);
    }

    // 冰箱：偏白的圆角块 + 把手（与柜子明显区分）
    private static Sprite 冰箱()
    {
        if (冰箱缓存 != null) return 冰箱缓存;
        var 真图 = Resources.Load<Sprite>(资源目录 + "冰箱");
        if (真图 != null) return 冰箱缓存 = 真图;
        const int 边长 = 96;
        var 纹理 = 新纹理(边长, 边长);
        var 体 = new Color(0.78f, 0.79f, 0.80f, 1f);
        var 深 = new Color(0.58f, 0.59f, 0.61f, 1f);
        for (int y = 0; y < 边长; y++)
            for (int x = 0; x < 边长; x++)
            {
                int 边 = 10;
                bool 内 = x >= 边 && y >= 边 && x < 边长 - 边 && y < 边长 - 边;
                bool 描边 = !内 && x >= 边 - 3 && y >= 边 - 3 && x < 边长 - 边 + 3 && y < 边长 - 边 + 3;
                var 色 = 内 ? 体 : (描边 ? Color.black : new Color(0, 0, 0, 0));
                if (内 && y == 边长 / 3) 色 = 深;                          // 上下门缝
                // 把手：右侧竖条
                if (内 && x >= 边长 - 22 && x <= 边长 - 17 && ((y > 边长 / 3 + 6 && y < 边长 - 16) || (y > 16 && y < 边长 / 3 - 6))) 色 = 深;
                纹理.SetPixel(x, y, 色);
            }
        return 冰箱缓存 = 成图(纹理);
    }

    // 纸箱：牛皮色方块 + 十字胶带
    private static Sprite 纸箱()
    {
        if (纸箱缓存 != null) return 纸箱缓存;
        var 真图 = Resources.Load<Sprite>(资源目录 + "纸箱");
        if (真图 != null) return 纸箱缓存 = 真图;
        const int 边长 = 96;
        var 纹理 = 新纹理(边长, 边长);
        var 体 = new Color(0.55f, 0.42f, 0.27f, 1f);
        var 带 = new Color(0.68f, 0.56f, 0.40f, 1f);
        for (int y = 0; y < 边长; y++)
            for (int x = 0; x < 边长; x++)
            {
                int 边 = 8;
                bool 内 = x >= 边 && y >= 边 && x < 边长 - 边 && y < 边长 - 边;
                bool 描边 = !内 && x >= 边 - 3 && y >= 边 - 3 && x < 边长 - 边 + 3 && y < 边长 - 边 + 3;
                var 色 = 内 ? 体 : (描边 ? Color.black : new Color(0, 0, 0, 0));
                if (内 && (Mathf.Abs(x - 边长 / 2) <= 3 || Mathf.Abs(y - 边长 / 2) <= 3)) 色 = 带;   // 十字胶带
                纹理.SetPixel(x, y, 色);
            }
        return 纸箱缓存 = 成图(纹理);
    }

    // 货架：三横线格架（中空，看得出是架子）
    private static Sprite 货架()
    {
        if (货架缓存 != null) return 货架缓存;
        var 真图 = Resources.Load<Sprite>(资源目录 + "货架");
        if (真图 != null) return 货架缓存 = 真图;
        const int 边长 = 96;
        var 纹理 = 新纹理(边长, 边长);
        var 体 = 网格面板配色.容器高柜色;
        var 架 = 乘(体, 1.35f);
        for (int y = 0; y < 边长; y++)
            for (int x = 0; x < 边长; x++)
            {
                int 边 = 7;
                bool 内 = x >= 边 && y >= 边 && x < 边长 - 边 && y < 边长 - 边;
                bool 描边 = !内 && x >= 边 - 3 && y >= 边 - 3 && x < 边长 - 边 + 3 && y < 边长 - 边 + 3;
                var 色 = 内 ? 体 : (描边 ? Color.black : new Color(0, 0, 0, 0));
                if (内)
                {
                    int 段 = (边长 - 边 * 2) / 4;
                    if ((y - 边) % 段 == 0) 色 = 架;                        // 三层隔板
                    if (x - 边 < 4 || 边长 - 边 - x < 4) 色 = 架;           // 两侧立柱
                }
                纹理.SetPixel(x, y, 色);
            }
        return 货架缓存 = 成图(纹理);
    }

    // 尸体：躺着的暗色剪影 + 淡红渍
    public static Sprite 尸体()
    {
        if (尸体缓存 != null) return 尸体缓存;
        var 真图 = Resources.Load<Sprite>(资源目录 + "尸体");
        if (真图 != null) return 尸体缓存 = 真图;
        const int 边长 = 96;
        var 纹理 = 新纹理(边长, 边长);
        var 体 = 网格面板配色.尸体体色;
        var 渍 = new Color(0.28f, 0.12f, 0.12f, 0.55f);
        for (int y = 0; y < 边长; y++)
            for (int x = 0; x < 边长; x++)
            {
                float dx = x - 边长 / 2f, dy = y - 边长 / 2f;
                float 躯干 = dx * dx / (34f * 34f) + dy * dy / (17f * 17f);      // 椭圆：躺着的躯干
                float 头 = (x - 22f) * (x - 22f) / (12f * 12f) + (y - 边长 / 2f) * (y - 边长 / 2f) / (12f * 12f);
                float 渍距 = dx * dx / (42f * 42f) + dy * dy / (26f * 26f);
                var 色 = 渍距 <= 1f ? 渍 : new Color(0, 0, 0, 0);
                if (躯干 <= 1f) 色 = 体;
                if (头 <= 1f) 色 = 乘(体, 0.85f);
                纹理.SetPixel(x, y, 色);
            }
        return 尸体缓存 = 成图(纹理);
    }

    // ================= 令牌（圆盘 / 圆环 / 剪影） =================

    // 实心圆盘（白，用 Image 上色）
    public static Sprite 圆盘()
    {
        if (圆盘缓存 != null) return 圆盘缓存;
        const int 边长 = 96;
        var 纹理 = 新纹理(边长, 边长);
        float 半径 = 边长 / 2f - 2f;
        for (int y = 0; y < 边长; y++)
            for (int x = 0; x < 边长; x++)
            {
                float d = Mathf.Sqrt((x - 边长 / 2f) * (x - 边长 / 2f) + (y - 边长 / 2f) * (y - 边长 / 2f));
                float a = Mathf.Clamp01(半径 - d);        // 边缘 1px 抗锯齿
                纹理.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        return 圆盘缓存 = 成图(纹理);
    }

    // 圆环（白，用 Image 上色）
    public static Sprite 圆环()
    {
        if (圆环缓存 != null) return 圆环缓存;
        const int 边长 = 96;
        var 纹理 = 新纹理(边长, 边长);
        float 外 = 边长 / 2f - 2f, 内 = 外 - 5f;
        for (int y = 0; y < 边长; y++)
            for (int x = 0; x < 边长; x++)
            {
                float d = Mathf.Sqrt((x - 边长 / 2f) * (x - 边长 / 2f) + (y - 边长 / 2f) * (y - 边长 / 2f));
                float a = Mathf.Clamp01(外 - d) * Mathf.Clamp01(d - 内);
                纹理.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
            }
        return 圆环缓存 = 成图(纹理);
    }

    // 令牌剪影（玩家与敌人共用）：头（圆）+ 肩躯干（方）——与样张一致：两者只是底盘颜色不同，剪影同一款。
    public static Sprite 剪影()
    {
        if (剪影缓存 != null) return 剪影缓存;
        const int 边长 = 96;
        var 纹理 = 新纹理(边长, 边长);
        var 剪影色 = new Color(0.10f, 0.12f, 0.14f, 0.88f);
        float cx = 边长 / 2f;
        for (int y = 0; y < 边长; y++)
            for (int x = 0; x < 边长; x++)
            {
                bool 头 = (x - cx) * (x - cx) / (9f * 9f) + (y - 63f) * (y - 63f) / (9f * 9f) <= 1f;
                bool 躯 = x >= cx - 11 && x <= cx + 11 && y >= 30 && y <= 46;
                纹理.SetPixel(x, y, 头 || 躯 ? 剪影色 : new Color(0, 0, 0, 0));
            }
        return 剪影缓存 = 成图(纹理);
    }
    // 路径点线上的小圆点
    public static Sprite 圆点()
    {
        if (圆点缓存 != null) return 圆点缓存;
        const int 边长 = 24;
        var 纹理 = 新纹理(边长, 边长);
        for (int y = 0; y < 边长; y++)
            for (int x = 0; x < 边长; x++)
            {
                float d = Mathf.Sqrt((x - 边长 / 2f) * (x - 边长 / 2f) + (y - 边长 / 2f) * (y - 边长 / 2f));
                纹理.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(边长 / 2f - 1f - d)));
            }
        return 圆点缓存 = 成图(纹理);
    }

    // ================= 工具 =================

    private static Texture2D 新纹理(int 宽, int 高)
    {
        var 纹理 = new Texture2D(宽, 高, TextureFormat.RGBA32, false);
        纹理.filterMode = FilterMode.Bilinear;
        纹理.wrapMode = TextureWrapMode.Clamp;
        return 纹理;
    }

    private static Sprite 成图(Texture2D 纹理)
    {
        纹理.Apply();
        return Sprite.Create(纹理, new Rect(0f, 0f, 纹理.width, 纹理.height), new Vector2(0.5f, 0.5f), 每单位像素);
    }

    private static Color 乘(Color 色, float 系数)
        => new Color(色.r * 系数, 色.g * 系数, 色.b * 系数, 色.a);
}
