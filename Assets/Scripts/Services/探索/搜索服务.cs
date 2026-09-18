using System;
using System.Collections.Generic;
using UnityEngine;

    // 搜索服务：塔科夫式搜刮容器（纯 C# 逻辑，零 MonoBehaviour）。
    // 打开 搜索容器 → 首次按 搜索表 权重随机生成物品（复用 网格服务 网格算法，散落放置不堆角）；
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
        private readonly Dictionary<string, 网格服务> 已生成 = new Dictionary<string, 网格服务>();
        public IReadOnlyCollection<网格服务> 已生成视图 => 已生成.Values;

        // 已搜索物品：容器标识 → 已搜完的 堆叠 引用（与缓存视图同生命周期——重开容器 已搜过的 物品 不再搜索）。
        // 键 = 容器标识（搜索容器.标识 或 嵌套物品容器.标识 统一）
        private readonly Dictionary<string, HashSet<物品堆叠>> 已搜索 = new Dictionary<string, HashSet<物品堆叠>>();

        // 战斗 尸体 容器：战利品 供 搜索（每场 战斗 唯一 标识；房间层里由玩家走到尸体格上搜）
        private int 尸体序号;
        private readonly Dictionary<string, 搜索容器> 尸体定义 = new Dictionary<string, 搜索容器>();

        // 容器实例：实例 id → 定义（同一「货架」在一间房里可能有多个实例，必须按实例注册——
        // 拿容器定义标识当键会让两个「货架」共享同一份物资，并让「已搜索」标记串台）。
        // 实例 id 由 房间探索服务 生成（房间标识#序号），本局内稳定。
        private readonly Dictionary<string, 搜索容器> 实例定义 = new Dictionary<string, 搜索容器>();

        // 注册 容器实例（重复 id 保留首个；返回 true = 该实例可用）
        public bool 注册实例容器(搜索容器 定义)
        {
            if (定义 == null || string.IsNullOrEmpty(定义.标识)) return false;
            if (实例定义.ContainsKey(定义.标识)) return true;
            实例定义[定义.标识] = 定义;
            return true;
        }

        public bool 已注册实例(string 实例标识)
            => !string.IsNullOrEmpty(实例标识) && 实例定义.ContainsKey(实例标识);

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
        public 网格服务 打开(搜索容器 定义)
        {
            if (定义 == null || string.IsNullOrEmpty(定义.标识)) return null;
            if (已生成.TryGetValue(定义.标识, out var 已有)) return 已有;
            if (定义.容器列 <= 0 || 定义.容器行 <= 0) return null;
            var 视图 = new 网格服务();
            视图.网格物品 = new List<物品堆叠>();
            视图.网格列 = 定义.容器列;
            视图.网格行 = 定义.容器行;
            注入解析器(视图);
            注入形状(视图, 定义);
            生成物品(定义, 视图);
            已生成[定义.标识] = 视图;
            return 视图;
        }

        // ================= ★ 刀65：战局快照（取） =================

    // 把"这一趟翻出来的东西"整体导出：每个容器实例 → 已生成的内容 + 哪些物品已经搜过。
    // 为什么必须存：内容是无种子 `System.Random` 生成的（本文件 :14），**不可复现**；
    //   不存的话读档后你会看到"柜子上标着已翻过、里面却全是新物资"。
    public List<容器战局条> 取战局()
    {
        var 表 = new List<容器战局条>();
        foreach (var kv in 已生成)
        {
            if (string.IsNullOrEmpty(kv.Key) || kv.Value == null) continue;
            var 条 = new 容器战局条 { 实例标识 = kv.Key, 视图 = kv.Value };
            if (已搜索.TryGetValue(kv.Key, out var 集) && 集 != null)
                foreach (var 堆 in 集)
                    if (堆 != null) 条.已搜物品.Add(物品键(堆));
            表.Add(条);
        }
        return 表;
    }

    // "已搜过"的键：`标识@列,行`。一个容器内每件物品占着各自的格 → (标识,列,行) 唯一；
    // 而且玩家把某件拿走之后这条键自然找不到对应物品（`装回战局` 会跳过）→ 记录自动失效，不会串。
    private static string 物品键(物品堆叠 堆) => $"{堆.标识}@{堆.列},{堆.行}";

    private static 物品堆叠 找堆叠(网格服务 视图, string 键)
    {
        if (视图?.网格物品 == null) return null;
        foreach (var 堆 in 视图.网格物品)
            if (堆 != null && 物品键(堆) == 键) return 堆;
        return null;
    }

    // ★ 刀65：把战局装回来（与 取战局 成对）。
    // 顺序要求：调用方必须**先**让 `房间探索服务.重新登记实例()` 跑过 ——
    //   `查找容器(实例标识)` 靠 `实例定义` 才能拿到容器的形状，没登记就只能退回整矩形。
    public void 装回战局(List<容器战局条> 表)
    {
        if (表 == null) return;
        foreach (var 条 in 表)
        {
            if (条 == null || 条.视图 == null || string.IsNullOrEmpty(条.实例标识)) continue;
            注入解析器(条.视图);                       // 解析器是 [NonSerialized]，读回来必须重注
            var 定义 = 查找容器(条.实例标识);
            if (定义 != null) 注入形状(条.视图, 定义);   // 容器形状同理
            已生成[条.实例标识] = 条.视图;
            var 集 = new HashSet<物品堆叠>();
            if (条.已搜物品 != null)
                foreach (var 键 in 条.已搜物品)
                {
                    var 堆 = 找堆叠(条.视图, 键);
                    if (堆 != null) 集.Add(堆);          // 找不到 = 那件早被拿走了 → 记录自然失效
                }
            已搜索[条.实例标识] = 集;
        }
    }

    // 撤离/结束战局：清空全部已生成容器（下次打开重新随机）
        // 注意：必须连「已搜索 标记 / 实例注册 / 尸体」一起清——它们都是战局态，
        //       只清 已生成 会留下跨 raid 的脏标记与内存泄漏（旧实现的问题，此处修掉）。
        public void 清空战局()
        {
            已生成.Clear();
            已搜索.Clear();
            实例定义.Clear();
            尸体定义.Clear();
            尸体序号 = 0;
        }

        // 注册 尸体 容器（战斗 胜利 战利品）：物品 按 顺序 智能 旋转 放入；返回 标识（空 → ""）
        // **允许空尸体**（物品 空 = 一具什么都没有的尸体）：房间层"打赢必定在原地留一具可搜尸体"靠这条，
        // 否则没掉落的敌人打死了什么都不留（旧行为）。
        public string 注册尸体容器(string 名称, int 列, int 行, List<物品堆叠> 物品, float 搜索时间 = 4f)
        {
            if (列 <= 0 || 行 <= 0) return "";
            if (物品 == null) 物品 = new List<物品堆叠>();
            string 标识 = $"尸体_{++尸体序号}";
            var 视图 = new 网格服务 { 网格列 = 列, 网格行 = 行, 网格物品 = new List<物品堆叠>() };
            注入解析器(视图);
            foreach (var 堆叠 in 物品)
            {
                if (堆叠 == null || string.IsNullOrEmpty(堆叠.标识)) continue;
                // ★ 与 `生成物品` 同一条口径：**放进网格前必须按 `堆叠上限` 拆堆叠**。
                //   原来整堆直接塞一格 —— 而 `战利品入尸` 已经先按标识合并过，
                //   `掉落列表` 里写"铁皮×4 / 金属零件×4 / 生肉×2"这类（数量 > 上限）就会造出
                //   "一格 4 件铁皮"的非法堆叠。改 `堆叠上限` 口径那天，这条会当场生效（实测 15 处）。
                var 原件 = 堆叠;
                int 单堆上限 = Math.Max(1, 视图.堆叠上限(原件.标识));
                int 剩余 = 原件.数量 > 0 ? 原件.数量 : 1;
                bool 首件 = true;
                while (剩余 > 0)
                {
                    int 本次 = Math.Min(单堆上限, 剩余);
                    物品堆叠 一份;
                    if (首件)
                    {
                        原件.数量 = 本次;
                        一份 = 原件;   // 首件沿用原对象：配件/耐久/已装填/品质 都是实例状态，不能丢
                    }
                    else
                    {
                        一份 = new 物品堆叠(原件.标识, 本次)
                        {
                            旋转 = 原件.旋转,
                            当前耐久 = 原件.当前耐久,
                            已装填 = 原件.已装填,
                            品质 = 原件.品质,
                            配件 = 原件.配件 != null ? new List<配件条>(原件.配件) : null,
                        };
                    }
                    if (数据.物品.TryGetValue(原件.标识, out var 模板) && 模板.是容器) 容器.初始化容器(一份);   // 箱中箱
                    var 空位 = 视图.寻找可放置格智能旋转(一份, out bool 需旋转);
                    if (空位 == null) break;   // 放不下 → 这件剩下的部分丢掉
                    一份.旋转 = 需旋转;
                    一份.列 = 空位.Value.列;
                    一份.行 = 空位.Value.行;
                    视图.网格物品.Add(一份);
                    剩余 -= 本次;
                    首件 = false;
                }
            }
            // 空尸体也照样登记（不给"没掉落就什么都不留"留后门）
            尸体定义[标识] = new 搜索容器 { 标识 = 标识, 名称 = 名称, 容器列 = 列, 容器行 = 行, 搜索时间 = 搜索时间 };
            已生成[标识] = 视图;   // 命中 缓存：打开 不再 随机 生成
            return 标识;
        }

        // ================= 随机生成 =================

        // 按搜索表权重随机填充；物品 按 空间顺序 放置（左→右、上→下 第一个空位，智能旋转）——
        // 塔科夫式 紧凑排列（与 自动搜索 顺序 一致：搜完 这件 下一件 就是 旁边 那件）
        // ⚠ 抽到的 `数量` 必须按 物品的 `堆叠上限` **拆成多个堆叠**再逐个放（见下方 ★ 注释）：
        //   堆叠规则只有一份口径 —— `网格服务.放入网格()`；这里不许再自行"把数量塞进一格"。
        private void 生成物品(搜索容器 定义, 网格服务 视图)
        {
            if (定义.搜索表 == null || 定义.搜索表.Length == 0) return;
            int 尝试上限 = Math.Max(定义.容器列 * 定义.容器行 * 2, 12);
            int 已生成件 = 0;
            for (int i = 0; i < 尝试上限; i++)
            {
                var 条目 = 按权重随机(定义.搜索表);
                if (条目 == null || string.IsNullOrEmpty(条目.物品标识)) continue;
                if (!数据.物品.TryGetValue(条目.物品标识, out var 模板)) continue;
                int 剩余 = Math.Max(1, 随机.Next(条目.数量最小, Math.Max(条目.数量最小, 条目.数量最大) + 1));
                // ★ 修 bug：抽到的数量必须**按 `堆叠上限` 拆成若干个堆叠**再逐个放置。
                //   原实现是一句 `new 物品堆叠(标识, 数量)` 把整数量塞进一格 ——
                //   对"不可堆叠"的物品（`堆叠上限` 0/缺省 = 每格恒 1 件：香料 / 水 / 罐头 / 面包 / 种子…）
                //   这等于凭空造出"一格 5 件"的非法堆叠，与 `网格服务.放入网格()` 的口径
                //   （`上限 > 1 ? 每堆 ≤上限 : 每堆 1 件`，见 网格服务.cs:517~520）自相矛盾 ——
                //   同一件铁皮，从配方/拾取来是 1 件 1 格、从搜刮来却 3 件 1 格。
                //   这里只补"拆堆叠"，搜索特有的「智能旋转 + 箱中箱初始化」照旧保留。
                int 单堆上限 = Math.Max(1, 视图.堆叠上限(条目.物品标识));   // 0/缺省 = 不可堆叠 → 每堆 1 件
                bool 收尾 = false;
                while (剩余 > 0)
                {
                    var 堆叠 = new 物品堆叠(条目.物品标识, Math.Min(单堆上限, 剩余))
                    {
                        当前耐久 = 有效最大耐久解析?.Invoke(条目.物品标识) ?? 0,   // 装备初始化完整耐久
                    };
                    if (模板.是容器) 容器.初始化容器(堆叠);   // 箱中箱：容器物品 初始化 容器列表（双击 可在 搜索面板 内 打开）
                    // 顺序放置：寻找可放置格（智能旋转：当前旋转 放不下 自动 转 90°）——左上 → 右下
                    var 空位 = 视图.寻找可放置格智能旋转(堆叠, out bool 需旋转);
                    if (空位 == null) break;   // 放不下 → 这件剩下的部分丢掉（同 放入网格 "返回实际放入数" 的口径）
                    堆叠.旋转 = 需旋转;
                    堆叠.列 = 空位.Value.列;
                    堆叠.行 = 空位.Value.行;
                    视图.网格物品.Add(堆叠);
                    剩余 -= 堆叠.数量;
                    已生成件++;
                    // 概率收尾：已生成 ≥2 件后每件 45% 结束（留空位，不塞满——物品 稀疏 一些）
                    // 注意：`已生成件` 数的是**堆叠数**（玩家眼里"几件东西"），拆堆叠后 1 个条目可能算多件
                    if (已生成件 >= 2 && 随机.NextDouble() < 0.45) { 收尾 = true; break; }
                }
                if (收尾) break;
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

        // 按 容器标识 反查 定义（实例 优先 → 尸体 → 静态表扫描；找不到返回 null）
        public 搜索容器 查找容器(string 容器标识)
        {
            if (string.IsNullOrEmpty(容器标识)) return null;
            if (实例定义.TryGetValue(容器标识, out var 实例)) return 实例;
            if (尸体定义.TryGetValue(容器标识, out var 尸体)) return 尸体;
            foreach (var 类型 in 数据.搜索地图类型.Values)
                if (类型.房间 != null)
                    foreach (var 房间 in 类型.房间)
                        if (房间.容器 != null)
                            foreach (var 容器 in 房间.容器)
                                if (容器.标识 == 容器标识) return 容器;
            return null;
        }

        // ================= 内部 =================

        private void 注入解析器(网格服务 服务)
        {
            服务.形状解析 = 形状解析;
            服务.堆叠上限解析 = 堆叠上限解析;
            服务.有效最大耐久解析 = 有效最大耐久解析;
            服务.重量解析 = 重量解析;
        }

        // 注入搜索容器形状（整矩形 / 拼合块）——与 容器服务.注入形状 同语义（独立口袋：物品必须完全落在同一块内）
        private void 注入形状(网格服务 服务, 搜索容器 定义)
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
