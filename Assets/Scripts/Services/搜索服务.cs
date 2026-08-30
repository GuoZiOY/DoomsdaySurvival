using System;
using System.Collections.Generic;
using UnityEngine;

    // 搜索服务：塔科夫式搜刮容器（纯 C# 逻辑，零 MonoBehaviour）。
    // 打开 搜索容器 → 首次按 搜索表 权重随机生成物品（复用 背包服务 网格算法，散落放置不堆角）；
    // 同一容器 会话内 打开 再打开 内容一致（缓存视图；撤离时 清空战局 整体销毁）。
    // 搜索时间：容器级 = 定义.搜索时间；物品级 = 条目.搜索时间（0 → 按 物品价值 推导）。
    // 解析器 与 容器服务 同源（PlayerService 装配后接线）。
    public sealed class 搜索服务
    {
        private readonly DataService 数据;
        private readonly 容器服务 容器;
        private readonly System.Random 随机 = new System.Random();

        [NonSerialized] public Func<string, 物品形状> 形状解析;
        [NonSerialized] public Func<string, int> 堆叠上限解析;
        [NonSerialized] public Func<string, int> 有效最大耐久解析;
        [NonSerialized] public Func<string, int> 重量解析;

        // 会话缓存：容器标识 → 已生成视图（撤离/清空战局 前 一直存在）
        private readonly Dictionary<string, 背包服务> 已生成 = new Dictionary<string, 背包服务>();
        public IReadOnlyCollection<背包服务> 已生成视图 => 已生成.Values;

        // 已搜索物品：容器标识 → 已搜完的 堆叠 引用（与缓存视图同生命周期——重开容器 已搜过的 物品 不再搜索）。
        // 键 = 容器标识（搜索容器.标识 或 嵌套物品容器.标识 统一）
        private readonly Dictionary<string, HashSet<物品堆叠>> 已搜索 = new Dictionary<string, HashSet<物品堆叠>>();

        public bool 已搜索物品(string 容器标识, 物品堆叠 堆叠)
        {
            if (string.IsNullOrEmpty(容器标识) || 堆叠 == null) return false;
            return 已搜索.TryGetValue(容器标识, out var 集) && 集.Contains(堆叠);
        }

        public void 标记已搜索(string 容器标识, 物品堆叠 堆叠)
        {
            if (string.IsNullOrEmpty(容器标识) || 堆叠 == null) return;
            if (!已搜索.TryGetValue(容器标识, out var 集)) 已搜索[容器标识] = 集 = new HashSet<物品堆叠>();
            集.Add(堆叠);
        }

        public 搜索服务(DataService 数据, 容器服务 容器)
        {
            this.数据 = 数据;
            this.容器 = 容器;
        }

        // 从 玩家档案 同步解析器（与 容器服务 同源；装配层调用）
        public void 接线解析器(玩家档案 档案)
        {
            形状解析 = 档案.形状解析;
            堆叠上限解析 = 档案.堆叠上限解析;
            有效最大耐久解析 = 标识 => 档案.有效最大耐久(标识);
            重量解析 = 档案.重量解析;
        }

        // 搜索容器 是否已生成（已搜过/已打开过）
        public bool 已打开(string 容器标识)
        {
            return !string.IsNullOrEmpty(容器标识) && 已生成.ContainsKey(容器标识);
        }

        // 打开搜索容器：首次 → 随机生成；再次 → 返回缓存视图。返回 null = 定义缺失。
        public 背包服务 打开(搜索容器 定义)
        {
            if (定义 == null || string.IsNullOrEmpty(定义.标识)) return null;
            if (已生成.TryGetValue(定义.标识, out var 已有)) return 已有;
            if (定义.容器列 <= 0 || 定义.容器行 <= 0) return null;
            var 视图 = new 背包服务();
            视图.背包 = new List<物品堆叠>();
            视图.网格列 = 定义.容器列;
            视图.网格行 = 定义.容器行;
            注入解析器(视图);
            注入形状(视图, 定义);
            生成物品(定义, 视图);
            已生成[定义.标识] = 视图;
            return 视图;
        }

        // 撤离/结束战局：清空全部已生成容器（下次打开重新随机）
        public void 清空战局() => 已生成.Clear();

        // ================= 随机生成 =================

        // 按搜索表权重随机填充；物品 按 空间顺序 放置（左→右、上→下 第一个空位，智能旋转）——
        // 塔科夫式 紧凑排列（与 自动搜索 顺序 一致：搜完 这件 下一件 就是 旁边 那件）
        private void 生成物品(搜索容器 定义, 背包服务 视图)
        {
            if (定义.搜索表 == null || 定义.搜索表.Length == 0) return;
            int 尝试上限 = Math.Max(定义.容器列 * 定义.容器行 * 2, 12);
            int 已生成件 = 0;
            for (int i = 0; i < 尝试上限; i++)
            {
                var 条目 = 按权重随机(定义.搜索表);
                if (条目 == null || string.IsNullOrEmpty(条目.物品标识)) continue;
                if (!数据.物品.TryGetValue(条目.物品标识, out var 模板)) continue;
                int 数量 = Math.Max(1, 随机.Next(条目.数量最小, Math.Max(条目.数量最小, 条目.数量最大) + 1));
                // 顺序放置：寻找可放置格（智能旋转：当前旋转 放不下 自动 转 90°）——左上 → 右下
                var 堆叠 = new 物品堆叠(条目.物品标识, 数量)
                {
                    当前耐久 = 有效最大耐久解析?.Invoke(条目.物品标识) ?? 0,   // 装备初始化完整耐久
                };
                if (模板.是容器) 容器.初始化容器(堆叠);   // 箱中箱：容器物品 初始化 容器列表（双击 可在 搜索面板 内 打开）
                var 空位 = 视图.寻找可放置格智能旋转(堆叠, out bool 需旋转);
                if (空位 == null) continue;   // 放不下 → 换下一件
                堆叠.旋转 = 需旋转;
                堆叠.列 = 空位.Value.列;
                堆叠.行 = 空位.Value.行;
                视图.背包.Add(堆叠);
                已生成件++;
                // 概率收尾：已生成 ≥2 件后每件 45% 结束（留空位，不塞满——物品 稀疏 一些）
                if (已生成件 >= 2 && 随机.NextDouble() < 0.45) break;
            }
        }

        // 权重随机取一条
        private 搜索条目 按权重随机(搜索条目[] 表)
        {
            int 总 = 0;
            foreach (var 条 in 表) 总 += Math.Max(1, 条.权重);
            if (总 <= 0) return null;
            int 点 = 随机.Next(总);
            foreach (var 条 in 表)
            {
                点 -= Math.Max(1, 条.权重);
                if (点 < 0) return 条;
            }
            return 表[表.Length - 1];
        }

        // 物品级 搜索时间：条目.搜索时间 > 0 用之；否则按 价值 推导（价值越高 搜得越久）
        public float 物品搜索时间(搜索条目 条目, string 物品标识)
        {
            if (条目 != null && 条目.搜索时间 > 0f) return 条目.搜索时间;
            int 价值 = 1;
            if (!string.IsNullOrEmpty(物品标识) && 数据.物品.TryGetValue(物品标识, out var 模板)) 价值 = Math.Max(1, 模板.价值);
            return Mathf.Clamp(价值 * 0.08f, 0.5f, 2.5f);   // 价值2→0.5s / 价值25→2s
        }

        // 按 容器标识 反查 定义（校验后的数据；找不到返回 null）
        public 搜索容器 查找容器(string 容器标识)
        {
            if (string.IsNullOrEmpty(容器标识)) return null;
            foreach (var 类型 in 数据.搜索地图类型.Values)
                if (类型.房间 != null)
                    foreach (var 房间 in 类型.房间)
                        if (房间.容器 != null)
                            foreach (var 容器 in 房间.容器)
                                if (容器.标识 == 容器标识) return 容器;
            return null;
        }

        // ================= 内部 =================

        private void 注入解析器(背包服务 服务)
        {
            服务.形状解析 = 形状解析;
            服务.堆叠上限解析 = 堆叠上限解析;
            服务.有效最大耐久解析 = 有效最大耐久解析;
            服务.重量解析 = 重量解析;
        }

        // 注入搜索容器形状（整矩形 / 拼合块）——与 容器服务.注入形状 同语义（独立口袋：物品必须完全落在同一块内）
        private void 注入形状(背包服务 服务, 搜索容器 定义)
        {
            var 块们 = 定义.容器形状;
            if (块们 != null && 块们.Length > 0)
            {
                foreach (var 块 in 块们)
                    if (块 == null || 块.宽 <= 0 || 块.高 <= 0
                        || 块.列 < 0 || 块.行 < 0 || 块.列 + 块.宽 > 定义.容器列 || 块.行 + 块.高 > 定义.容器行)
                        return;   // 越界/尺寸不匹配 → 整矩形兜底
                服务.格所属块 = (列, 行) =>
                {
                    for (int i = 0; i < 块们.Length; i++)
                    {
                        var 块 = 块们[i];
                        if (列 >= 块.列 && 列 < 块.列 + 块.宽 && 行 >= 块.行 && 行 < 块.行 + 块.高) return i;
                    }
                    return -1;
                };
                服务.格可用 = (列, 行) => 服务.格所属块(列, 行) >= 0;
                服务.形状块 = new List<容器形状块>(块们);
            }
            // 空 = 整矩形（格可用 默认全 true）
        }
    }
