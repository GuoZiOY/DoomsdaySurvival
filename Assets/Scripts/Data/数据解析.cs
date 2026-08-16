// 数据解析：Unity JsonUtility 不支持把枚举名(尤其中文)反序列化为枚举（只认整数，失败默认0）。
// 因此数据模型里所有枚举字段改为「字符串字段 + 计算枚举属性」，经此统一转换。
// 例：JSON "类别": "增益" → 字符串字段 类别 → 计算属性 类别枚举 = 数据解析.枚举<技能类别>(类别)
public static class 数据解析
{
    public static T 枚举<T>(string 名) where T : struct
    {
        if (string.IsNullOrEmpty(名)) return default(T);
        try { return (T)System.Enum.Parse(typeof(T), 名); }
        catch { return default(T); }
    }
}
