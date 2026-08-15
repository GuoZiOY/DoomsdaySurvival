using System;
using System.Collections.Generic;
using UnityEngine;

    // 类型化事件总线：订阅/发布/解绑；发布时单订阅者异常不影响其他、不中断发布。
    public sealed class EventBus
    {
        // 订阅表：事件类型 -> 处理器列表
        private readonly Dictionary<Type, List<Delegate>> 订阅表 = new Dictionary<Type, List<Delegate>>();

        public void 订阅<T>(Action<T> 处理)
        {
            if (!订阅表.TryGetValue(typeof(T), out var 列表))
            {
                列表 = new List<Delegate>();
                订阅表[typeof(T)] = 列表;
            }
            列表.Add(处理);
        }

        public void 取消订阅<T>(Action<T> 处理)
        {
            if (订阅表.TryGetValue(typeof(T), out var 列表))
                列表.Remove(处理);
        }

        public void 发布<T>(T 事件)
        {
            if (!订阅表.TryGetValue(typeof(T), out var 列表)) return;
            var 快照 = 列表.ToArray();   // 快照：允许订阅者在回调里改订阅表
            foreach (var 处理 in 快照)
            {
                try { ((Action<T>)处理)(事件); }
                catch (Exception 异常) { Debug.LogError($"[EventBus] 订阅者异常: {异常}"); }
            }
        }
    }
