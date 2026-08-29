using System.Collections.Generic;
using UnityEngine;

// 精灵内容包围盒：扫描精灵图内 非透明像素 的包围盒（"吃掉" 透明留白）。
// cover 放大 以 实际内容 为基准 → 小物品 图标 真正 铺满 格子，不再 四周 大片 空白。
// 返回 归一化 矩形（相对 精灵 画布）：position.x/y = 内容中心（UI 坐标，y 自下而上），width/height = 内容 尺寸 比例。
// 全画布 无留白 = (0.5, 0.5, 1, 1)；纹理 不可读 / 无内容 = 同值 兜底（回退 原 cover 行为）。
// 注意：精灵 纹理 需 开启 Read/Write Enabled（isReadable），否则 扫描 失败 走 兜底。
public static class 精灵内容包围盒
{
    private static readonly Dictionary<Sprite, Rect> 缓存 = new Dictionary<Sprite, Rect>();

    public static Rect 获取(Sprite 精灵)
    {
        if (精灵 == null) return new Rect(0.5f, 0.5f, 1f, 1f);
        if (缓存.TryGetValue(精灵, out var 已有)) return 已有;
        var 结果 = 扫描(精灵);
        缓存[精灵] = 结果;
        return 结果;
    }

    private static Rect 扫描(Sprite 精灵)
    {
        var 纹理 = 精灵.texture;
        if (纹理 == null || !纹理.isReadable) return new Rect(0.5f, 0.5f, 1f, 1f);
        var 区域 = 精灵.textureRect;
        if (区域.width <= 0 || 区域.height <= 0) return new Rect(0.5f, 0.5f, 1f, 1f);
        // 只扫 精灵 区域 内的像素（图集 时 不 扫 整张 纹理）；textureRect 是 float → 显式转 int
        var 像素 = 纹理.GetPixels32();
        int 纹理宽 = 纹理.width;
        int 区域x0 = (int)区域.xMin, 区域y0 = (int)区域.yMin, 区域x1 = (int)区域.xMax, 区域y1 = (int)区域.yMax;
        int x0 = 区域x1, y0 = 区域y1, x1 = 区域x0, y1 = 区域y0;
        bool 有内容 = false;
        for (int y = 区域y0; y < 区域y1; y++)
        {
            int 行 = y * 纹理宽;
            for (int x = 区域x0; x < 区域x1; x++)
            {
                if (像素[行 + x].a > 8)   // alpha 阈值：>8/255 算 有内容（忽略 边缘 微透明）
                {
                    有内容 = true;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            }
        }
        if (!有内容) return new Rect(0.5f, 0.5f, 1f, 1f);
        float 宽 = 区域.width, 高 = 区域.height;
        float 盒宽 = (x1 - x0 + 1f) / 宽;
        float 盒高 = (y1 - y0 + 1f) / 高;
        // 内容 中心：Unity 纹理像素坐标 原点在左下（y 向上，与 GetPixels32 索引一致）→ 直接按纹理比例，
        // 与 UI 的 pivot（y 向上、0 在底部）同方向，无需翻转。
        float 中心x = ((x0 + x1) * 0.5f - 区域.xMin) / 宽;
        float 中心y = ((y0 + y1) * 0.5f - 区域.yMin) / 高;
        return new Rect(中心x, 中心y, 盒宽, 盒高);
    }
}
