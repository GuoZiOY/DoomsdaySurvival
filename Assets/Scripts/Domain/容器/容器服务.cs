using System;
using System.Collections.Generic;

    // 容器服务：塔科夫式嵌套容器（纯 C# 领域逻辑，零 UnityEngine 依赖）。
    // 容器 = 一个物品（物品堆叠），内部持有 List<物品堆叠> 容器物品 —— 与 网格服务.网格物品 完全同构，
    // 因此容器内部复用 网格服务 的网格算法（可放置/换位/堆叠/布局安全）。
    // 打开容器 = 把 容器物品 包成一个 网格服务 视图；主背包与各容器视图统一为"网格服务"，跨网格转移走同一函数。
    public sealed class 容器服务
    {
        // 注入：标识 -> 物品数据（模板查询：是容器/容器尺寸/允许类型）。装配层接到 DataService。
        [NonSerialized] public Func<string, 物品数据> 物品数据解析;
        // 注入：家具定义标识 -> 家具定义（家具容器 识别：冰箱 等 打开 网格面板 的 家具）。装配层接到 DataService。
        [NonSerialized] public Func<string, 家具数据> 家具定义解析;
        // 注入：网格解析器（与 网格服务 同款，打开容器视图时注入，容器内网格判定依赖它们）。装配层从 玩家档案 同步。
        [NonSerialized] public Func<string, 物品形状> 形状解析;
        [NonSerialized] public Func<string, int> 堆叠上限解析;
        [NonSerialized] public Func<string, int> 有效最大耐久解析;
        [NonSerialized] public Func<string, int> 重量解析;

        // 从 玩家档案 同步解析器（PlayerService 接线时调用）
        public void 接线解析器(玩家档案 档案)
        {
            形状解析 = 档案.形状解析;
            堆叠上限解析 = 档案.堆叠上限解析;
            有效最大耐久解析 = 标识 => 档案.有效最大耐久(标识);
            重量解析 = 档案.重量解析;
        }

        // ===== 家具容器（冰箱 等：家具实例 也是 容器） =====

        // 家具容器 定义（解码 家具实例标识 → 家具定义；非容器家具 = null）
        private 家具数据 家具容器定义(string 实例标识)
        {
            if (家具定义解析 == null || string.IsNullOrEmpty(实例标识)) return null;
            var (定义标识, _) = 家具工具.解码(实例标识);
            var 定义 = 家具定义解析(定义标识);
            return 定义 != null && 定义.是容器 ? 定义 : null;
        }

        private 家具数据 家具容器定义(物品堆叠 堆叠) => 堆叠 == null ? null : 家具容器定义(堆叠.标识);

        // 家具容器 内部网格尺寸（按等级：1级=定义.容器列行；n级 应用 升级 前 n-1 段 的 容器列行）
        private (int 列, int 行) 家具容器尺寸(家具数据 定义, int 等级)
        {
            int 列 = 定义.容器列, 行 = 定义.容器行;
            if (定义.升级 != null)
            {
                int 段数 = Math.Min(等级 - 1, 定义.升级.Length);
                for (int i = 0; i < 段数; i++)
                {
                    var 升 = 定义.升级[i];
                    if (升.容器列 > 0) 列 = 升.容器列;
                    if (升.容器行 > 0) 行 = 升.容器行;
                }
            }
            return (列, 行);
        }

        // 家具容器 允许放入 校验（容器允许类型 "|" 分隔多类型；可选 容器允许种类 "|" 分隔多值 二级过滤）
        private bool 家具允许放入(家具数据 定义, string 入标识)
        {
            if (物品数据解析 == null || string.IsNullOrEmpty(入标识)) return false;
            var 入 = 物品数据解析(入标识);
            if (入 == null) return false;
            if (string.IsNullOrEmpty(定义.容器允许类型)) return true;
            foreach (var 类型 in 定义.容器允许类型.Split('|'))
            {
                if (入.类型 != 类型.Trim()) continue;
                // 种类 过滤（"|" 分隔多值；空 = 不按种类过滤）：命中 任一 允许种类 即可
                if (string.IsNullOrEmpty(定义.容器允许种类)) return true;
                foreach (var 种类 in 定义.容器允许种类.Split('|'))
                    if (入.种类 == 种类.Trim()) return true;
                return false;
            }
            return false;
        }

        // 该堆叠是否是容器：物品模板 / 家具容器 / 已有容器实例内容
        public bool 是容器(物品堆叠 堆叠)
        {
            if (堆叠 == null || string.IsNullOrEmpty(堆叠.标识)) return false;
            var 模板 = 物品数据解析?.Invoke(堆叠.标识);
            if (模板 != null && 模板.是容器) return true;
            if (家具容器定义(堆叠) != null) return true;
            return 堆叠.容器物品 != null && 堆叠.容器物品.Count >= 0 && 堆叠.容器列 > 0 && 堆叠.容器行 > 0;
        }

        // 容器内部网格尺寸（实例优先，缺省用模板/家具容器）
        public (int 列, int 行) 容器尺寸(物品堆叠 堆叠)
        {
            if (堆叠.容器列 > 0 && 堆叠.容器行 > 0) return (堆叠.容器列, 堆叠.容器行);
            var 家具 = 家具容器定义(堆叠);
            if (家具 != null)
            {
                var (_, 等级) = 家具工具.解码(堆叠.标识);
                return 家具容器尺寸(家具, 等级);
            }
            var 模板 = 物品数据解析?.Invoke(堆叠.标识);
            if (模板 != null) return (模板.容器列, 模板.容器行);
            return (0, 0);
        }

        // 初始化容器实例（放入网格/装备/掉落生成/打开家具容器 时调用）：按模板/家具定义 建空容器列表
        public void 初始化容器(物品堆叠 堆叠)
        {
            if (堆叠 == null || string.IsNullOrEmpty(堆叠.标识)) return;
            var 家具 = 家具容器定义(堆叠);
            if (家具 != null)
            {
                // 分区容器（净水器 等）：容器物品 = 各分区 子容器 堆叠（子堆叠 各自 容器物品 = 分区 内容）
                if (家具.容器分区 != null && 家具.容器分区.Length > 0)
                {
                    初始化分区容器(堆叠, 家具);
                    return;
                }
                var (_, 等级) = 家具工具.解码(堆叠.标识);
                var (列, 行) = 家具容器尺寸(家具, 等级);
                if (列 <= 0 || 行 <= 0) return;
                堆叠.容器列 = 列;
                堆叠.容器行 = 行;
                堆叠.容器物品 ??= new List<物品堆叠>();
                return;
            }
            var 模板 = 物品数据解析?.Invoke(堆叠.标识);
            if (模板 == null || !模板.是容器) return;
            var (列2, 行2) = 容器尺寸(堆叠);
            if (列2 <= 0 || 行2 <= 0) return;
            堆叠.容器列 = 列2;
            堆叠.容器行 = 行2;
            堆叠.容器物品 ??= new List<物品堆叠>();
        }

        // 分区容器 初始化：按 家具定义.容器分区 建 子容器 堆叠（标识 = 父标识 + "@" + 分区名；容器列/行 = 分区 尺寸）
        private void 初始化分区容器(物品堆叠 堆叠, 家具数据 家具)
        {
            if (堆叠.容器物品 == null || 堆叠.容器物品.Count == 0)
                堆叠.容器物品 = new List<物品堆叠>();
            else return;   // 已 初始化（读档 恢复）——不 重复 建
            foreach (var 分区 in 家具.容器分区)
            {
                if (分区 == null) continue;
                堆叠.容器物品.Add(new 物品堆叠($"{堆叠.标识}@{分区.名称}", 1)
                {
                    容器列 = 分区.列 > 0 ? 分区.列 : 3,
                    容器行 = 分区.行 > 0 ? 分区.行 : 3,
                    容器物品 = new List<物品堆叠>(),
                });
            }
        }

        // 容器是否允许放入该物品（容器允许类型 校验；空 = 任意；家具容器 走 家具规则；分区 子容器 走 分区 规则）
        public bool 允许放入(物品堆叠 容器, string 标识)
        {
            if (容器 == null || string.IsNullOrEmpty(标识)) return false;
            // 分区 子容器（净水器 的 脏水区 等：标识 含 @）→ 按 分区 配置 过滤
            if (子容器分区(容器) != null) return 分区允许放入(容器, 标识);
            var 家具 = 家具容器定义(容器);
            if (家具 != null) return 家具允许放入(家具, 标识);
            var 模板 = 物品数据解析?.Invoke(容器.标识);
            if (模板 == null || !模板.是容器) return false;
            if (string.IsNullOrEmpty(模板.容器允许类型)) return true;
            var 入 = 物品数据解析?.Invoke(标识);
            return 入 != null && 入.类型 == 模板.容器允许类型;
        }

        // 允许放入（按 容器标识 查模板；装具块 校验用——装备记录 非 物品堆叠；家具标识 走 家具规则）
        public bool 允许放入(string 容器标识, string 入标识)
        {
            if (string.IsNullOrEmpty(容器标识) || string.IsNullOrEmpty(入标识)) return false;
            var 家具 = 家具容器定义(容器标识);
            if (家具 != null) return 家具允许放入(家具, 入标识);
            var 容器模板 = 物品数据解析?.Invoke(容器标识);
            if (容器模板 == null || !容器模板.是容器) return false;
            if (string.IsNullOrEmpty(容器模板.容器允许类型)) return true;
            var 入 = 物品数据解析?.Invoke(入标识);
            return 入 != null && 入.类型 == 容器模板.容器允许类型;
        }

        // 按 标识 判断 是否容器（装具块 嵌套校验用；家具容器 亦识别）
        public bool 是容器(string 标识)
        {
            if (string.IsNullOrEmpty(标识)) return false;
            var 模板 = 物品数据解析?.Invoke(标识);
            if (模板 != null && 模板.是容器) return true;
            return 家具容器定义(标识) != null;
        }

        // 打开容器：把 容器物品 包成 网格服务 视图（复用全部网格逻辑），并注入网格解析器 + 容器内部形状
        public 网格服务 打开(物品堆叠 容器)
        {
            var 服务 = new 网格服务();
            服务.网格物品 = 容器.容器物品 ?? (容器.容器物品 = new List<物品堆叠>());
            var (列, 行) = 容器尺寸(容器);
            服务.网格列 = 列;
            服务.网格行 = 行;
            注入解析器(服务);
            注入形状(服务, 容器.标识, 列, 行);
            return 服务;
        }

        // ===== 分区容器（净水器 等：容器物品 = 各分区 子容器 堆叠） =====

        // 家具 是否 分区容器（家具定义.容器分区 非空）
        public bool 是分区容器(物品堆叠 堆叠)
        {
            if (堆叠 == null) return false;
            var 家具 = 家具容器定义(堆叠);
            return 家具 != null && 家具.容器分区 != null && 家具.容器分区.Length > 0;
        }

        // 分区 数量（净水器 3 区 → 3）
        public int 分区数量(物品堆叠 容器) => 是分区容器(容器) && 容器.容器物品 != null ? 容器.容器物品.Count : 0;

        // 第 分区索引 个 子容器 堆叠（容器物品[索引]；null = 无）
        public 物品堆叠 分区子容器(物品堆叠 容器, int 索引)
        {
            if (!是分区容器(容器) || 容器.容器物品 == null || 索引 < 0 || 索引 >= 容器.容器物品.Count) return null;
            return 容器.容器物品[索引];
        }

        // 更新 分区 网格 尺寸（净水器 等 升级：按 升级需求.容器分区 改 各 子区 子容器 的 列/行；
        // 允许 类型/种类 沿用 家具定义.容器分区（反查 用），不 变）
        public void 更新分区尺寸(物品堆叠 容器, 容器分区数据[] 新分区)
        {
            if (容器?.容器物品 == null || 新分区 == null) return;
            for (int i = 0; i < 容器.容器物品.Count && i < 新分区.Length; i++)
            {
                var 子容器 = 容器.容器物品[i];
                var 分区 = 新分区[i];
                if (子容器 == null || 分区 == null) continue;
                if (分区.列 > 0) 子容器.容器列 = 分区.列;
                if (分区.行 > 0) 子容器.容器行 = 分区.行;
            }
        }

        // 该 子容器 堆叠 的 分区 配置（子容器 标识 = "父@分区名"；null = 非 子容器/找不到）
        public 容器分区数据 子容器分区(物品堆叠 子容器)
        {
            if (子容器 == null || string.IsNullOrEmpty(子容器.标识)) return null;
            int at = 子容器.标识.IndexOf('@');
            if (at <= 0) return null;
            string 父标识 = 子容器.标识.Substring(0, at);
            string 分区名 = 子容器.标识.Substring(at + 1);
            var (定义标识, _) = 家具工具.解码(父标识);
            var 家具 = 家具定义解析?.Invoke(定义标识);
            if (家具?.容器分区 == null) return null;
            foreach (var 分区 in 家具.容器分区)
                if (分区 != null && 分区.名称 == 分区名) return 分区;
            return null;
        }

        // 分区 允许放入（子容器 按其 分区 配置 过滤；父 容器 自身 仍 按 家具 允许 规则 先 拦）
        public bool 分区允许放入(物品堆叠 子容器, string 标识)
        {
            if (物品数据解析 == null || string.IsNullOrEmpty(标识)) return false;
            var 分区 = 子容器分区(子容器);
            if (分区 == null) return false;
            var 入 = 物品数据解析(标识);
            if (入 == null) return false;
            if (string.IsNullOrEmpty(分区.允许类型)) return true;
            foreach (var 类型 in 分区.允许类型.Split('|'))
            {
                if (入.类型 != 类型.Trim()) continue;
                if (string.IsNullOrEmpty(分区.允许种类)) return true;
                foreach (var 种类 in 分区.允许种类.Split('|'))
                    if (入.种类 == 种类.Trim()) return true;
                return false;
            }
            return false;
        }

        // 穿戴容器视图：把 装备记录（弹挂/腰封/背包）的 容器物品 包成 网格服务（中区网格常驻显示用）
        public 网格服务 穿戴容器视图(装备记录 记录)
        {
            var 服务 = new 网格服务();
            服务.网格物品 = 记录.容器物品 ?? (记录.容器物品 = new List<物品堆叠>());
            服务.网格列 = 记录.容器列 > 0 ? 记录.容器列 : 1;
            服务.网格行 = 记录.容器行 > 0 ? 记录.容器行 : 1;
            注入解析器(服务);
            注入形状(服务, 记录.标识, 服务.网格列, 服务.网格行);
            return 服务;
        }

        // 注入网格解析器（主背包/容器视图/穿戴容器视图 统一）
        private void 注入解析器(网格服务 服务)
        {
            服务.形状解析 = 形状解析;
            服务.堆叠上限解析 = 堆叠上限解析;
            服务.有效最大耐久解析 = 有效最大耐久解析;
            服务.重量解析 = 重量解析;
        }

        // 注入容器形状（矩形列表优先，掩码兼容）：格可用 + 格所属块（独立口袋——物品必须完全落在同一块内）+ 形状块几何（画轮廓用）
        private void 注入形状(网格服务 服务, string 标识, int 列数, int 行数)
        {
            var 模板 = 物品数据解析?.Invoke(标识);
            if (模板 == null) return;
            // ① 矩形列表（塔科夫式独立口袋）
            var 块们 = 模板.容器形状;
            if (块们 != null && 块们.Length > 0)
            {
                foreach (var 块 in 块们)
                    if (块 == null || 块.宽 <= 0 || 块.高 <= 0
                        || 块.列 < 0 || 块.行 < 0 || 块.列 + 块.宽 > 列数 || 块.行 + 块.高 > 行数)
                        return;   // 越界/尺寸不匹配 → 整矩形兜底
                服务.格所属块 = (列, 行) =>
                {
                    for (int i = 0; i < 块们.Length; i++)
                    {
                        var 块 = 块们[i];
                        if (列 >= 块.列 && 列 < 块.列 + 块.宽 && 行 >= 块.行 && 行 < 块.行 + 块.高) return i;
                    }
                    return -1;   // 空洞
                };
                服务.格可用 = (列, 行) => 服务.格所属块(列, 行) >= 0;
                服务.形状块 = new List<容器形状块>(块们);   // 画块轮廓用
                return;
            }
            // ② 兼容：字符串掩码（'1'=可用格，'0'=空洞；整矩形语义，无独立口袋）
            var 掩码 = 模板.容器形状掩码;
            if (掩码 == null || 掩码.Length == 0) return;
            if (掩码.Length != 模板.容器行 || 掩码[0]?.Length != 模板.容器列) return;
            if (列数 != 模板.容器列 || 行数 != 模板.容器行) return;
            服务.格可用 = (列, 行) =>
            {
                if (行 < 0 || 行 >= 掩码.Length) return false;
                var 行串 = 掩码[行];
                if (列 < 0 || 列 >= 行串.Length) return false;
                return 行串[列] == '1';
            };
        }

        // 容器内部已用格数
        public int 占用格数(物品堆叠 容器)
        {
            if (容器?.容器物品 == null) return 0;
            var 视图 = 打开(容器);
            return 视图.已用格数();
        }

        // ================= 跨网格转移（核心） =================
        // 从 源网格服务 把 堆叠 转移到 目标网格服务 的 (目标列,目标行)。
        // 主背包 ↔ 容器、容器 ↔ 容器 全部走这里；同面板走原有 移动/换位/合并，不经过此函数。
        // 返回 0 = 失败；>0 = 转移数量。
        public int 跨网格转移(网格服务 源, 物品堆叠 堆叠, 网格服务 目标, int 目标列, int 目标行)
        {
            if (源 == null || 目标 == null || 堆叠 == null) return 0;
            if (源 == 目标) return 0;   // 同网格由面板走原逻辑
            if (堆叠.列 < 0) return 0;   // 未入格
            // ① 目标格同标识可堆叠 → 并入目标堆叠（部分或全部转移）
            var 目标格物 = 目标.该格物品(目标列, 目标行);
            if (目标格物 != null && 目标格物 != 堆叠 && 目标.可合并(目标格物, 堆叠))
            {
                int 空位 = 目标.堆叠上限(目标格物.标识) - 目标格物.数量;
                int 并入 = Math.Min(空位, 堆叠.数量);
                if (并入 <= 0) return 0;
                目标格物.数量 += 并入;
                堆叠.数量 -= 并入;
                if (堆叠.数量 <= 0) 源.网格物品.Remove(堆叠);   // 全部并入 → 从源移除
                return 并入;
            }
            // ② 目标格可放置（排除自身）→ 源移除、目标放入指定格
            if (!目标.可放置(堆叠.标识, 目标列, 目标行, 堆叠.旋转, 堆叠)) return 0;
            源.网格物品.Remove(堆叠);
            堆叠.列 = 目标列; 堆叠.行 = 目标行;
            目标.网格物品.Add(堆叠);
            return 堆叠.数量;
        }
    }
