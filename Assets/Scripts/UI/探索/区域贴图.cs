using System.Collections.Generic;
using UnityEngine;

// ============================================================
// 区域贴图：运行时程序化生成**区域层**用到的"简单美术"（街道 / 楼体 / 障碍）。
// 与 房间贴图 一个路子：不依赖任何外部图，配色全部走 网格面板配色（一处改全局变）；
// 令牌、路径点线、圆环这些"格子层通用图案"直接复用 房间贴图 的那几张，不重复画。
// 全部 Sprite 静态缓存（只生成一次）。
// ============================================================
public static class 区域贴图
{
    private static Sprite 地面缓存;
    private static Sprite 楼体格缓存;
    private static readonly Dictionary<string, Sprite> 障碍缓存 = new Dictionary<string, Sprite>();

    // ================= 街道地面 =================

    // 沥青：低频斑块 + 颗粒 + 裂缝（比房间的水泥地更暗、更脏）
    public static Sprite 地面()
    {
        if (地面缓存 != null) return 地面缓存;
        var 真图 = Resources.Load<Sprite>("沙盒贴图/区域地面");
        if (真图 != null) return 地面缓存 = 真图;
        const int 边长 = 64;
        var 纹理 = 新纹理(边长, 边长);
        var 随机 = new System.Random(20260912);
        for (int y = 0; y < 边长; y++)
            for (int x = 0; x < 边长; x++)
            {
                float 斑 = Mathf.PerlinNoise(x * 0.06f, y * 0.06f);
                float 粒 = (float)随机.NextDouble();
                float t = Mathf.Clamp01(斑 * 0.7f + (粒 - 0.5f) * 0.3f);
                var 色 = Color.Lerp(网格面板配色.区域地面色, 网格面板配色.区域地面斑色, t);
                if (随机.NextDouble() < 0.02) 色 = 乘(色, 0.7f);   // 裂缝/补丁
                纹理.SetPixel(x, y, 色);
            }
        return 地面缓存 = 成图(纹理);
    }

    // ================= 楼体 =================

    // 单格楼体立面：底色 + 上下两条楼板线 + 两扇窗（**不带楼顶压边** —— 楼顶由面板按"朝外的北边"描线）
    // 楼体一律**逐格铺这张**（矩形楼铺出来是一整面，L / [ / 凸 / 凹 也自然拼对）；描边由 区域网格面板.建楼边 管。
    public static Sprite 楼体格()
    {
        if (楼体格缓存 != null) return 楼体格缓存;
        const int 边长 = 32;
        var 纹理 = 新纹理(边长, 边长);
        var 体 = 网格面板配色.建筑体色;
        var 板 = 乘(体, 0.78f);
        var 窗 = 网格面板配色.建筑窗色;
        int 半 = 边长 / 2;
        for (int y = 0; y < 边长; y++)
            for (int x = 0; x < 边长; x++)
            {
                var 色 = 体;
                if (y == 0 || y == 边长 - 1) 色 = 板;                        // 楼板线（相邻格拼起来正好一层）
                int 局x = x % 半;
                bool 是窗 = 局x > 半 / 4 && 局x < 半 - 半 / 4 && y > 边长 / 4 && y < 边长 * 3 / 4;
                if (是窗) 色 = 窗;
                纹理.SetPixel(x, y, 色);
            }
        return 楼体格缓存 = 成图(纹理);
    }

    // 楼门：直接复用房间层的门贴图（同一种"楼道口"，朝向 下 = 门开在下边）
    public static Sprite 入口() => 房间贴图.门(0, false);

    // ================= 街道障碍 =================

    // 障碍剪影：按名称挑样子（车 / 堆），格子层只认"底色 + 名称"
    public static Sprite 障碍(string 名称)
    {
        string 名 = 名称 ?? "";
        bool 车 = 名.Contains("车") || 名.Contains("报亭") || 名.Contains("亭");
        string 键 = 车 ? "车" : "堆";
        if (障碍缓存.TryGetValue(键, out var 有)) return 有;

        const int 边长 = 64;
        var 纹理 = 新纹理(边长, 边长);
        var 体 = 网格面板配色.障碍体色;
        var 深 = 网格面板配色.障碍深色;
        for (int y = 0; y < 边长; y++)
            for (int x = 0; x < 边长; x++)
            {
                bool 涂 = false;
                if (车)
                {
                    // 车身（下半）+ 车顶（上半收窄）+ 两个轮子
                    涂 = y >= 18 && y < 40 && x >= 3 && x < 61;
                    if (y >= 40 && y < 52 && x >= 16 && x < 48) 涂 = true;
                    bool 轮 = (y >= 10 && y < 20) && ((x >= 12 && x < 24) || (x >= 40 && x < 52));
                    var 色 = 涂 ? 体 : (轮 ? 深 : Color.clear);
                    if (轮 && y >= 14) 色 = 乘(深, 1.2f);
                    纹理.SetPixel(x, y, 色);
                }
                else
                {
                    // 砖堆：几块大小不一的方块（确定性伪随机，图只生成一次）
                    int 块 = (x / 7) * 13 + (y / 9) * 7;
                    bool 有块 = ((块 * 37) % 11) > 3;
                    涂 = 有块 && x >= 4 && x < 60 && y >= 6 && y < 46;
                    纹理.SetPixel(x, y, 涂 ? (y < 26 ? 体 : 乘(体, 0.85f)) : Color.clear);
                }
            }
        return 障碍缓存[键] = 成图(纹理);
    }

    // ================= 复用房间层的通用图案 =================

    public static Sprite 圆盘() => 房间贴图.圆盘();
    public static Sprite 剪影() => 房间贴图.剪影();
    public static Sprite 圆环() => 房间贴图.圆环();
    public static Sprite 圆点() => 房间贴图.圆点();
    public static Sprite 墙顶() => 房间贴图.墙顶();   // 区域边界（街道尽头的废墟墙）

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
