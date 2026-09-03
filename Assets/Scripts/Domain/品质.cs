using UnityEngine;

// 品质：世界上几乎所有东西（物品/技能/装备/材料）的品质档位
public enum 品质 { 普通, 优秀, 稀有, 史诗, 英雄, 传奇 }

// 品质工具：倍率 / 颜色 / 富文本标签
public static class 品质工具
{
    // 品质效果倍率
    public static float 倍率(品质 档)
    {
        switch (档)
        {
            case 品质.普通: return 1.0f;
            case 品质.优秀: return 1.2f;
            case 品质.稀有: return 1.5f;
            case 品质.史诗: return 1.8f;
            case 品质.英雄: return 2.2f;
            case 品质.传奇: return 3.0f;
            default: return 1.0f;
        }
    }

    // 品质颜色（UI 名着色）
    public static Color 颜色(品质 档)
    {
        switch (档)
        {
            case 品质.普通: return new Color(0.85f, 0.85f, 0.85f);
            case 品质.优秀: return new Color(0.50f, 0.68f, 0.42f);
            case 品质.稀有: return new Color(0.29f, 0.55f, 0.84f);
            case 品质.史诗: return new Color(0.62f, 0.37f, 0.84f);
            case 品质.英雄: return new Color(0.85f, 0.56f, 0.29f);
            case 品质.传奇: return new Color(0.84f, 0.27f, 0.27f);
            default: return Color.white;
        }
    }

    // 品质名
    public static string 品质名(品质 档)
    {
        switch (档)
        {
            case 品质.普通: return "普通";
            case 品质.优秀: return "优秀";
            case 品质.稀有: return "稀有";
            case 品质.史诗: return "史诗";
            case 品质.英雄: return "英雄";
            case 品质.传奇: return "传奇";
            default: return "";
        }
    }

    // 富文本标签（如 <color=#7fae6a>[优秀]</color>），列表行/日志用
    public static string 标签(品质 档)
    {
        var 色 = ColorUtility.ToHtmlStringRGB(颜色(档));
        return $"<color=#{色}>[{品质名(档)}]</color>";
    }

    // 名称着色（如 <color=#4a8cd4>铁剑</color>）：日志/文本里 物品名 按品质染对应色（不带 [标签]）
    public static string 名称着色(品质 档, string 名称)
    {
        var 色 = ColorUtility.ToHtmlStringRGB(颜色(档));
        return $"<color=#{色}>{名称}</color>";
    }
}
