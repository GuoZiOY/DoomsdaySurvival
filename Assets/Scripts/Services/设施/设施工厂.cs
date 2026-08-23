using UnityEngine;

// 设施工厂：按 facilities.json 的 逻辑类型 反射实例化设施逻辑类并注入环境。
// 加新设施 = 写一个 设施逻辑 子类 + 加一条 facilities.json，路由器与面板基类零改动。
public static class 设施工厂
{
    public static 设施逻辑 创建(string 设施标识)
    {
        var 数据 = ServiceRegistry.Get<DataService>();
        if (!数据.设施.TryGetValue(设施标识, out var 定义))
        {
            Debug.LogError($"[设施工厂] 未定义的设施: {设施标识}");
            return null;
        }
        if (string.IsNullOrEmpty(定义.逻辑类型))
        {
            Debug.LogError($"[设施工厂] 设施 {设施标识} 未配置 逻辑类型");
            return null;
        }
        // Type.GetType 简单名会在调用程序集（Assembly-CSharp）内查找
        var 类型 = System.Type.GetType(定义.逻辑类型);
        if (类型 == null)
        {
            Debug.LogError($"[设施工厂] 找不到逻辑类: {定义.逻辑类型}");
            return null;
        }
        var 实例 = (设施逻辑)System.Activator.CreateInstance(类型);
        实例.装配(定义.标识, 定义.名称, ServiceRegistry.Get<PlayerService>().档案, 数据, ServiceRegistry.Get<EventBus>());
        // 制作设施：从 定义.数据 注入制作类型（装备/食物/药剂）
        if (实例 is 制作设施 制作 && !string.IsNullOrEmpty(定义.数据)) 制作.类型 = 定义.数据;
        return 实例;
    }
}
