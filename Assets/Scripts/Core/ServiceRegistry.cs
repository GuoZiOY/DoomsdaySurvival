using System;
using System.Collections.Generic;

    // 服务注册表：显式依赖的装配中心。清除() 供装配重载。
    public static class ServiceRegistry
    {
        // 服务表：类型 -> 实例
        private static readonly Dictionary<Type, object> 服务表 = new Dictionary<Type, object>();

        public static void Register<T>(T 服务)
        {
            if (服务表.ContainsKey(typeof(T)))
                throw new InvalidOperationException($"[ServiceRegistry] 服务已注册: {typeof(T).Name}");
            服务表[typeof(T)] = 服务;
        }

        public static T Get<T>()
        {
            if (服务表.TryGetValue(typeof(T), out var 服务)) return (T)服务;
            throw new InvalidOperationException($"[ServiceRegistry] 未注册服务: {typeof(T).Name}");
        }

        public static bool 已注册<T>() => 服务表.ContainsKey(typeof(T));

        public static void 清除() => 服务表.Clear();
    }
