using UnityEngine;

// 物品图标服务：按 物品数据.图片 引用加载 Sprite（JSON 只存引用字符串，真实图由 Resources 提供）。
// 两种手动挂图方式（任选其一，引用名 都填在 items.json 的 "图片" 字段）：
//   ① 整张精灵图：把 武器精灵图.png 放 Assets/Resources/物品图标/，Sprite Editor 切成子精灵并命名 = 物品标识；
//      运行时 Resources.LoadAll<Sprite>("物品图标/武器精灵图") 按子精灵名匹配。
//   ② 单文件：把 木棍.png 等放 Assets/Resources/物品图标/，运行时 Resources.Load<Sprite>("物品图标/木棍")。
// 找不到（未挂图 / 引用为空）→ 返回 null，内容层回退为原色块（不影响逻辑）。
public static class 物品图标服务
{
    private const string 精灵图路径 = "物品图标/武器精灵图";   // 整张精灵图在 Resources 下的路径（文件名固定）
    private static Sprite[] 切片;   // 缓存整图的子精灵

    // 获取某物品图片的 Sprite（无图/未找到 → null）
    public static Sprite 获取(string 图片引用)
    {
        if (string.IsNullOrEmpty(图片引用)) return null;
        return 从切片找(图片引用) ?? Resources.Load<Sprite>($"物品图标/{图片引用}");
    }

    // 从整张精灵图的子精灵里按名匹配
    private static Sprite 从切片找(string 名)
    {
        if (切片 == null)
        {
            try { 切片 = Resources.LoadAll<Sprite>(精灵图路径); }
            catch { 切片 = null; }
        }
        if (切片 == null || 切片.Length == 0) return null;
        foreach (var s in 切片)
            if (s != null && s.name == 名) return s;
        return null;
    }
}
