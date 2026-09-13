using System;
using System.Collections.Generic;

    // 网格服务：通用网格容器逻辑（纯 C#，零 UnityEngine 依赖）——主背包/仓库/容器内部/穿戴容器/安全屋家具 全部复用本服务承载网格数据与算法。
    // 职责：持有 网格尺寸 + 本网格物品列表（网格物品 字段）；网格放置/换位/区域交换/堆叠规则/放入移除 全部内聚于此。
    // 命名注：类名与 网格物品 字段即"网格"语义——仓库视图 的 网格物品 = 仓库物品，容器视图 的 网格物品 = 容器内部物品，
    //       安全屋 家具网格 的 网格物品 = 玩家.家具。旧名"背包/背包服务"沿袭 主背包 叫法（已 弃）。
    // 用法：各 视图（主背包/仓库/容器/穿戴/家具）各自 持有 一个 网格服务 实例（注入 解析器 + 网格物品 列表）；
    //       玩家档案 暴露 档案.网格服务（主背包 网格）引用；外部 视图 用 各自 网格服务，不经 玩家档案 转发。
    [Serializable]
    public class 网格服务
    {
        // —— 数据 ——
        public int 网格列 = 5;       
        public int 网格行 = 10;
        public List<物品堆叠> 网格物品 = new List<物品堆叠>();   // 本网格物品列表（主背包=主背包物品；仓库视图=仓库物品；容器视图=容器内部物品；家具网格=玩家.家具）

        // —— 注入解析器（装配层接到 DataService；标识 -> 数值）。委托不可序列化，随 玩家档案 接线时注入 ——
        [NonSerialized] public Func<string, 物品形状> 形状解析;        // 标识 -> 物品形状（宽×高）
        [NonSerialized] public Func<string, int> 堆叠上限解析;        // 标识 -> 堆叠上限（0/缺省 = 不可堆叠）
        [NonSerialized] public Func<string, int> 有效最大耐久解析;    // 标识 -> 有效最大耐久（0=无耐久）
        [NonSerialized] public Func<string, int> 重量解析;            // 标识 -> 物品重量
        // 容器内部形状：格(列,行) 是否可用（塔科夫式多矩形拼合；null = 整矩形全部可用）。
        // 由 容器服务 打开/穿戴容器视图 时从模板 容器形状 注入；主背包/仓库 为 null（整矩形）。
        [NonSerialized] public Func<int, int, bool> 格可用;
        // 容器内部形状的"块归属"：格(列,行) → 所属块索引（塔科夫式独立口袋——物品必须完全落在同一块内，不能跨块）。
        // null = 无块概念（整矩形）；配合 格可用 使用：块索引 >= 0 即该格可用。
        [NonSerialized] public Func<int, int, int> 格所属块;
        // 形状块几何（画块轮廓用）：容器服务 注入形状时同步；null = 无形状（整矩形，画普通网格线）
        [NonSerialized] public System.Collections.Generic.List<容器形状块> 形状块;

        // 该格是否可用（形状内/整矩形）
        public bool 该格可用(int 列, int 行)
        {
            if (格可用 == null) return true;
            if (列 < 0 || 行 < 0 || 列 >= 网格列 || 行 >= 网格行) return false;
            return 格可用(列, 行);
        }

        // 该格所属块索引（无块概念 = -2 哨兵；空洞 = -1；正常块 >= 0）
        public int 该格块(int 列, int 行)
        {
            if (格所属块 == null) return -2;   // 无块概念（整矩形）
            if (列 < 0 || 行 < 0 || 列 >= 网格列 || 行 >= 网格行) return -1;
            return 格所属块(列, 行);
        }

        // 形状校验（可放置 共用）：物品覆盖格 全部可用 且（有块概念时）全部属于同一块
        // 修复：墙格（安全屋 格所属块 = -2）此前被当成"无块概念"放行——未拆墙 也能 把 家具 摆 上 墙 / 跨 墙；
        //       现 先 按 格可用 过滤（墙/空洞/掩码洞 全 不可放；整矩形 恒 可用）——拆墙 后 格所属块 变 房间 块 才 放行
        private bool 覆盖格合法(int 列, int 行, int 宽, int 高)
        {
            int 块 = -2;
            for (int r = 行; r < 行 + 高; r++)
                for (int c = 列; c < 列 + 宽; c++)
                {
                    if (!该格可用(c, r)) return false;   // 墙/空洞/掩码空洞：不可放（整矩形 恒 可用）
                    int 此块 = 该格块(c, r);
                    if (此块 >= 0)   // 有块概念：所有覆盖格必须同一块（独立口袋，不能跨块）
                    {
                        if (块 == -2) 块 = 此块;
                        else if (块 != 此块) return false;
                    }
                }
            return true;
        }

        // ================= 网格放置 =================

        // 检查某物品能否放在 (列,行)（不越界、不重叠、覆盖格全部可用）；排除 = 自身堆叠（移动/换位校验用，忽略其占格）
        public bool 可放置(string 标识, int 列, int 行, bool 旋转, 物品堆叠 排除 = null)
        {
            if (形状解析 == null) return false;
            var 形状 = 形状解析(标识);
            int 宽 = 旋转 ? 形状.高 : 形状.宽;
            int 高 = 旋转 ? 形状.宽 : 形状.高;
            if (列 < 0 || 行 < 0 || 列 + 宽 > 网格列 || 行 + 高 > 网格行) return false;
            // 形状校验：物品覆盖的每一格都必须在可用区域内（且同一块内，不跨口袋）
            if (!覆盖格合法(列, 行, 宽, 高)) return false;
            foreach (var 堆叠 in 网格物品)
            {
                if (堆叠 == null || 堆叠 == 排除 || 堆叠.列 < 0) continue;
                if (占据(堆叠, 列, 行, 宽, 高)) return false;
            }
            return true;
        }

        // 与 可放置 相同，但可同时忽略两个堆叠（用于换位判定：两物品互换位置时双方都不算占格）
        private bool 可放置忽略两个(string 标识, int 列, int 行, bool 旋转, 物品堆叠 排除A, 物品堆叠 排除B)
        {
            if (形状解析 == null) return false;
            var 形状 = 形状解析(标识);
            int 宽 = 旋转 ? 形状.高 : 形状.宽;
            int 高 = 旋转 ? 形状.宽 : 形状.高;
            if (列 < 0 || 行 < 0 || 列 + 宽 > 网格列 || 行 + 高 > 网格行) return false;
            if (!覆盖格合法(列, 行, 宽, 高)) return false;
            foreach (var 堆叠 in 网格物品)
            {
                if (堆叠 == null || 堆叠 == 排除A || 堆叠 == 排除B || 堆叠.列 < 0) continue;
                if (占据(堆叠, 列, 行, 宽, 高)) return false;
            }
            return true;
        }

        // 两物品是否重叠（占用格子相交）
        private bool 占据(物品堆叠 已有, int 列, int 行, int 宽, int 高)
        {
            if (形状解析 == null) return false;
            var 形状 = 形状解析(已有.标识);
            int 已宽 = 已有.旋转 ? 形状.高 : 形状.宽;
            int 已高 = 已有.旋转 ? 形状.宽 : 形状.高;
            return 列 < 已有.列 + 已宽 && 列 + 宽 > 已有.列 && 行 < 已有.行 + 已高 && 行 + 高 > 已有.行;
        }

        // 移动/旋转已入格物品到指定坐标（目标格被占则不移动，返回 false）
        public bool 移动堆叠(物品堆叠 堆叠, int 列, int 行, bool 旋转)
        {
            if (堆叠 == null || string.IsNullOrEmpty(堆叠.标识) || 堆叠.列 < 0 || 形状解析 == null) return false;
            if (堆叠.列 == 列 && 堆叠.行 == 行 && 堆叠.旋转 == 旋转) return true;   // 原位无操作
            if (!可放置(堆叠.标识, 列, 行, 旋转, 堆叠)) return false;
            堆叠.列 = 列; 堆叠.行 = 行; 堆叠.旋转 = 旋转;
            return true;
        }

        // 换位预测（拖拽投影的绿/红判定用）：两物品互换位置后是否都放得下（不改变状态）
        public bool 可换位(物品堆叠 甲, 物品堆叠 乙)
        {
            if (甲 == null || 乙 == null || 甲 == 乙) return false;
            if (甲.列 < 0 || 乙.列 < 0 || 形状解析 == null) return false;
            // 换位 = 两物品都离开原位互换：判定时需同时忽略 甲、乙（否则放对方格子时会与对方重叠而误判失败）
            return 可放置忽略两个(乙.标识, 甲.列, 甲.行, 乙.旋转, 甲, 乙)
                && 可放置忽略两个(甲.标识, 乙.列, 乙.行, 甲.旋转, 甲, 乙);
        }

        // 换位（交换两物品的位置与旋转）：互换后两件都必须放得下才执行
        public bool 换位(物品堆叠 甲, 物品堆叠 乙)
        {
            if (!可换位(甲, 乙)) return false;
            int 列 = 甲.列; 甲.列 = 乙.列; 乙.列 = 列;
            int 行 = 甲.行; 甲.行 = 乙.行; 乙.行 = 行;
            bool 转 = 甲.旋转; 甲.旋转 = 乙.旋转; 乙.旋转 = 转;
            return true;
        }

        // ================= 区域交换（整体换位） =================

        // 该格被哪个物品覆盖（左上角或身体格都算）；无 = null。规则：形状×旋转决定覆盖
        public 物品堆叠 该格物品(int 列, int 行)
        {
            if (形状解析 == null) return null;
            foreach (var 堆叠 in 网格物品)
            {
                if (堆叠 == null || 堆叠.列 < 0) continue;
                var 形状 = 形状解析(堆叠.标识);
                int 宽 = 堆叠.旋转 ? 形状.高 : 形状.宽;
                int 高 = 堆叠.旋转 ? 形状.宽 : 形状.高;
                if (列 >= 堆叠.列 && 列 < 堆叠.列 + 宽 && 行 >= 堆叠.行 && 行 < 堆叠.行 + 高) return 堆叠;
            }
            return null;
        }

        // 物品实际占格（按当前旋转）：旋转则宽高互换。规则：形状×旋转 → 占格
        public (int 宽, int 高) 物品占格(物品堆叠 堆叠)
        {
            if (堆叠 == null || 形状解析 == null) return (1, 1);
            var 形状 = 形状解析(堆叠.标识);
            return 堆叠.旋转 ? (形状.高, 形状.宽) : (形状.宽, 形状.高);
        }

        // 寻找第一个可放置空格（遍历所有起点；供"拖到容器物品上自动存入"与"拆分"使用）。
        // 注意：不排除 自身堆叠——源在网格中时其占格视为已占用，返回的是真空格（否则会把源自己的位置当空位，拆出物品重叠原地）。
        // 存入容器场景：源（拖拽物品）不在目标容器网格中，无影响。
        public (int 列, int 行)? 寻找可放置格(物品堆叠 堆叠)
        {
            if (堆叠 == null) return null;
            var (宽, 高) = 物品占格(堆叠);
            for (int 行 = 0; 行 <= 网格行 - 高; 行++)
                for (int 列 = 0; 列 <= 网格列 - 宽; 列++)
                    if (可放置(堆叠.标识, 列, 行, 堆叠.旋转)) return (列, 行);
            return null;
        }

        // 寻找可放置格（智能旋转版）：先按 当前旋转 找空位；放不下 → 自动试 旋转 90° 再找。
        // 返回 (空位, 是否需旋转)——存入容器 快捷收入 用：横放 2×1 放不进 竖口 弹挂 时 自动 竖放。
        // 注意：只 查询 不 改 堆叠.旋转；调用方 决定 是否 写回。
        public (int 列, int 行)? 寻找可放置格智能旋转(物品堆叠 堆叠, out bool 需旋转)
        {
            需旋转 = false;
            if (堆叠 == null) return null;
            var (宽, 高) = 物品占格(堆叠);   // 当前旋转
            for (int 行 = 0; 行 <= 网格行 - 高; 行++)
                for (int 列 = 0; 列 <= 网格列 - 宽; 列++)
                    if (可放置(堆叠.标识, 列, 行, 堆叠.旋转)) return (列, 行);
            // 当前旋转 放不下 → 试 旋转 90°（宽高互换）
            int 转宽 = 高, 转高 = 宽;
            for (int 行 = 0; 行 <= 网格行 - 转高; 行++)
                for (int 列 = 0; 列 <= 网格列 - 转宽; 列++)
                    if (可放置(堆叠.标识, 列, 行, !堆叠.旋转)) { 需旋转 = true; return (列, 行); }
            return null;
        }

        // 拆分堆叠：从 源 拆出 拆出数量 份为独立新堆叠（同标识/旋转/耐久/品质/配件），自动找空位放下。
        // 要求 1 <= 拆出数量 < 源.数量；无空位返回 null（不拆分）。
        public 物品堆叠 拆分(物品堆叠 源, int 拆出数量)
        {
            if (源 == null || 拆出数量 <= 0 || 拆出数量 >= 源.数量) return null;
            var 空位 = 寻找可放置格(源);
            if (空位 == null) return null;
            var 新 = new 物品堆叠(源.标识, 拆出数量)
            {
                旋转 = 源.旋转,
                当前耐久 = 源.当前耐久,
                品质 = 源.品质,
                配件 = 源.配件 != null ? new List<配件条>(源.配件) : null,
                列 = 空位.Value.列,
                行 = 空位.Value.行,
            };
            源.数量 -= 拆出数量;
            网格物品.Add(新);
            return 新;
        }

        // 放入已有堆叠实例到 指定格（装备卸下/拖拽放置用；目标格被占或不可放返回 false，不改变堆叠）
        public bool 放入指定格(物品堆叠 堆叠, int 列, int 行)
        {
            if (堆叠 == null) return false;
            if (!可放置(堆叠.标识, 列, 行, 堆叠.旋转)) return false;
            堆叠.列 = 列;
            堆叠.行 = 行;
            网格物品.Add(堆叠);
            return true;
        }

        // 已占用格数（所有入格物品的面积和）
        public int 已用格数()
        {
            int 已用 = 0;
            if (形状解析 == null) return 已用;
            foreach (var 堆叠 in 网格物品)
                if (堆叠 != null && 堆叠.列 >= 0)
                {
                    var (宽, 高) = 物品占格(堆叠);
                    已用 += 宽 * 高;
                }
            return 已用;
        }

        // 收集与 (列,行,宽,高) 相交的网格物品
        public List<物品堆叠> 区域内物品(int 列, int 行, int 宽, int 高)
        {
            var 结果 = new List<物品堆叠>();
            if (形状解析 == null) return 结果;
            foreach (var 堆叠 in 网格物品)
            {
                if (堆叠 == null || 堆叠.列 < 0) continue;
                var 形状 = 形状解析(堆叠.标识);
                int 物宽 = 堆叠.旋转 ? 形状.高 : 形状.宽;
                int 物高 = 堆叠.旋转 ? 形状.宽 : 形状.高;
                if (列 < 堆叠.列 + 物宽 && 列 + 宽 > 堆叠.列 && 行 < 堆叠.行 + 物高 && 行 + 高 > 堆叠.行) 结果.Add(堆叠);
            }
            return 结果;
        }

        // 可放置，但忽略 忽略集（移走的物品）+ 忽略A（自身原位）
        private bool 可放置忽略多个(string 标识, int 列, int 行, bool 旋转, List<物品堆叠> 忽略集, 物品堆叠 忽略A)
        {
            if (形状解析 == null) return false;
            var 形状 = 形状解析(标识);
            int 宽 = 旋转 ? 形状.高 : 形状.宽;
            int 高 = 旋转 ? 形状.宽 : 形状.高;
            if (列 < 0 || 行 < 0 || 列 + 宽 > 网格列 || 行 + 高 > 网格行) return false;
            if (!覆盖格合法(列, 行, 宽, 高)) return false;
            foreach (var 堆叠 in 网格物品)
            {
                if (堆叠 == null || 堆叠 == 忽略A || 堆叠.列 < 0) continue;
                if (忽略集 != null && 忽略集.Contains(堆叠)) continue;
                if (占据(堆叠, 列, 行, 宽, 高)) return false;
            }
            return true;
        }

        // 把 物品集 全部摆进 (区域列,区域行,区域宽,区域高) 区域：① 先试整体平移(保持相对位置) ② 放不下再回溯。返回实际位置；无解 null
        // 禁区矩形 (禁列,禁行,禁宽,禁高)：目标物品不能摆进该区域（用于 A 新位置占格，避免搬回原位时撞 A）。禁宽<=0 表示无禁区。
        private List<(物品堆叠, int, int)> 布局摆进(List<物品堆叠> 物品集, int 区域列, int 区域行, int 区域宽, int 区域高, 物品堆叠 忽略,
            int 禁列 = -1, int 禁行 = -1, int 禁宽 = 0, int 禁高 = 0)
        {
            if (物品集 == null || 物品集.Count == 0) return new List<(物品堆叠, int, int)>();
            var 平移 = 平移布局(物品集, 区域列, 区域行, 区域宽, 区域高, 忽略, 禁列, 禁行, 禁宽, 禁高);
            if (平移 != null) return 平移;   // 符合直觉：尽量保持相对位置整体平移
            物品集.Sort((a, b) => 堆叠面积(b).CompareTo(堆叠面积(a)));   // 回溯：面积大的先放
            var 已放 = new List<物品堆叠>();
            var 结果 = new List<(物品堆叠, int, int)>();
            return 递归摆进(物品集, 区域列, 区域行, 区域宽, 区域高, 忽略, 已放, 结果, 禁列, 禁行, 禁宽, 禁高) ? 结果 : null;
        }

        // 换位安置（固定位置互换）：目标物品整体搬回 A 原位区（保持相对位置）。
        // 搬得下（大换小）→ 换；搬不下（小换大）→ 不允许。暂不做小换大。
        // 禁区 = A 新位置（目标区）：目标物品搬回原位时不能占 A 即将落位的格子（否则与 A 重叠）。
        private List<(物品堆叠, int, int)> 区域安置(List<物品堆叠> 物品集, 物品堆叠 A, int 原宽, int 原高, 物品堆叠 忽略,
            int 禁列 = -1, int 禁行 = -1, int 禁宽 = 0, int 禁高 = 0)
        {
            if (物品集 == null || 物品集.Count == 0) return new List<(物品堆叠, int, int)>();
            return 布局摆进(物品集, A.列, A.行, 原宽, 原高, 忽略, 禁列, 禁行, 禁宽, 禁高);
        }

        // 整体平移：以 物品集 最小列/行为参考，把相对位置平移到 区域；全部放得下才返回，否则 null
        private List<(物品堆叠, int, int)> 平移布局(List<物品堆叠> 物品集, int 区域列, int 区域行, int 区域宽, int 区域高, 物品堆叠 忽略,
            int 禁列 = -1, int 禁行 = -1, int 禁宽 = 0, int 禁高 = 0)
        {
            int minCol = int.MaxValue, minRow = int.MaxValue;
            foreach (var s in 物品集) { if (s.列 < minCol) minCol = s.列; if (s.行 < minRow) minRow = s.行; }
            var 结果 = new List<(物品堆叠, int, int)>();
            foreach (var s in 物品集)
            {
                int nc = 区域列 + (s.列 - minCol);
                int nr = 区域行 + (s.行 - minRow);
                var 形状 = 形状解析(s.标识);
                int w = s.旋转 ? 形状.高 : 形状.宽;
                int h = s.旋转 ? 形状.宽 : 形状.高;
                if (nc < 区域列 || nr < 区域行 || nc + w > 区域列 + 区域宽 || nr + h > 区域行 + 区域高) return null;   // 越出原位区
                if (禁宽 > 0 && 与禁区重叠(nc, nr, w, h, 禁列, 禁行, 禁宽, 禁高)) return null;   // 落入 A 新位置
                if (与已摆重叠(s, nc, nr, 结果)) return null;   // 相对位置内部不自叠
                结果.Add((s, nc, nr));
            }
            foreach (var (s, nc, nr) in 结果)
                if (!可放置忽略多个(s.标识, nc, nr, s.旋转, 物品集, 忽略)) return null;   // 与 非目标物品 重叠
            return 结果;
        }

        // 矩形 (列,行,宽,高) 是否与禁区矩形重叠
        private bool 与禁区重叠(int 列, int 行, int 宽, int 高, int 禁列, int 禁行, int 禁宽, int 禁高)
            => 列 < 禁列 + 禁宽 && 列 + 宽 > 禁列 && 行 < 禁行 + 禁高 && 行 + 高 > 禁行;

        private int 堆叠面积(物品堆叠 堆叠)
        {
            var 形状 = 形状解析(堆叠.标识);
            return (堆叠.旋转 ? 形状.高 : 形状.宽) * (堆叠.旋转 ? 形状.宽 : 形状.高);
        }

        private bool 递归摆进(List<物品堆叠> 物品集, int 区域列, int 区域行, int 区域宽, int 区域高, 物品堆叠 忽略, List<物品堆叠> 已放, List<(物品堆叠, int, int)> 结果,
            int 禁列 = -1, int 禁行 = -1, int 禁宽 = 0, int 禁高 = 0)
        {
            if (已放.Count >= 物品集.Count) return true;
            var 物品 = 物品集[已放.Count];   // 按 已放 计数 顺序 处理（调用前已 面积 降序）
            var 形状 = 形状解析(物品.标识);
            int 宽 = 物品.旋转 ? 形状.高 : 形状.宽;
            int 高 = 物品.旋转 ? 形状.宽 : 形状.高;
            var 未摆 = new List<物品堆叠>();   // 未摆放的目标（仍在目标区）：忽略；已摆放的按新位置占位（用 结果 检查）
            foreach (var s in 物品集) if (!已放.Contains(s)) 未摆.Add(s);
            for (int r = 区域行; r + 高 <= 区域行 + 区域高; r++)
                for (int c = 区域列; c + 宽 <= 区域列 + 区域宽; c++)
                {
                    if (禁宽 > 0 && 与禁区重叠(c, r, 宽, 高, 禁列, 禁行, 禁宽, 禁高)) continue;   // 落入 A 新位置
                    if (!可放置忽略多个(物品.标识, c, r, 物品.旋转, 未摆, 忽略)) continue;
                    if (与已摆重叠(物品, c, r, 结果)) continue;   // 已摆放目标在 A 原位的新位置占位，避免互相重叠
                    已放.Add(物品);
                    结果.Add((物品, c, r));
                    if (递归摆进(物品集, 区域列, 区域行, 区域宽, 区域高, 忽略, 已放, 结果, 禁列, 禁行, 禁宽, 禁高)) return true;
                    已放.RemoveAt(已放.Count - 1);
                    结果.RemoveAt(结果.Count - 1);
                }
            return false;
        }

        // 新物品 (列,行) 是否与 已摆放 目标（结果里的新位置）重叠
        private bool 与已摆重叠(物品堆叠 a, int 列, int 行, List<(物品堆叠, int, int)> 已摆)
        {
            var 形状a = 形状解析(a.标识);
            int aw = a.旋转 ? 形状a.高 : 形状a.宽;
            int ah = a.旋转 ? 形状a.宽 : 形状a.高;
            foreach (var (b, 列b, 行b) in 已摆)
            {
                var 形状b = 形状解析(b.标识);
                int bw = b.旋转 ? 形状b.高 : 形状b.宽;
                int bh = b.旋转 ? 形状b.宽 : 形状b.高;
                if (列 < 列b + bw && 列 + aw > 列b && 行 < 行b + bh && 行 + ah > 行b) return true;
            }
            return false;
        }

        // 区域交换预测：拖 A 到 (目标列,目标行)，按 目标旋转 落位——空区=移动可行；目标区物品能整体搬回 A 原位才可真换位
        public bool 区域可互换(物品堆叠 A, int 目标列, int 目标行, bool 目标旋转)
        {
            if (A == null || A.列 < 0 || 形状解析 == null) return false;
            var 形状 = 形状解析(A.标识);
            int 宽 = 目标旋转 ? 形状.高 : 形状.宽;
            int 高 = 目标旋转 ? 形状.宽 : 形状.高;
            var 目标 = 区域内物品(目标列, 目标行, 宽, 高).FindAll(b => b != A);
            if (目标.Count == 0) return 可放置(A.标识, 目标列, 目标行, 目标旋转, A);   // 空区：移动
            if (!可放置忽略多个(A.标识, 目标列, 目标行, 目标旋转, 目标, A)) return false;   // A 能否进目标区
            int 原宽 = A.旋转 ? 形状.高 : 形状.宽;   // A 原位区 尺寸（按 A 当前旋转）
            int 原高 = A.旋转 ? 形状.宽 : 形状.高;
            var 布局 = 区域安置(目标, A, 原宽, 原高, A, 目标列, 目标行, 宽, 高);   // 禁区=A 新位置：目标物品搬回原位时不能占 A 将落位的格子
            if (布局 == null) return false;
            return 区域互换后安全(A, 目标列, 目标行, 目标旋转, 目标, 布局);   // 与 区域互换 的"布局安全()"判据一致——投影绿=执行必成功
        }

        // 模拟"区域互换"执行后是否全网格安全（临时改位置再回滚，不改任何状态）。
        // 投影判定必须与执行判定完全一致：布局摆得进 ≠ 摆完安全，否则投影绿但放下失败。
        private bool 区域互换后安全(物品堆叠 A, int 目标列, int 目标行, bool 目标旋转, List<物品堆叠> 目标, List<(物品堆叠, int, int)> 布局)
        {
            var 备份 = new List<(物品堆叠, int, int, bool)>();
            备份.Add((A, A.列, A.行, A.旋转));
            foreach (var 物品 in 目标) 备份.Add((物品, 物品.列, 物品.行, 物品.旋转));
            A.列 = 目标列; A.行 = 目标行; A.旋转 = 目标旋转;
            foreach (var (物品, 列, 行) in 布局) { 物品.列 = 列; 物品.行 = 行; }
            bool 安全 = 布局安全();
            foreach (var (物品, 列, 行, 旋转) in 备份) { 物品.列 = 列; 物品.行 = 行; 物品.旋转 = 旋转; }
            return 安全;
        }

        // 区域交换：A 按 目标旋转 落 目标区；原目标区物品 全部 搬回 A 原位区（不改姿态）。执行前布局，失败/不安全则回滚
        public bool 区域互换(物品堆叠 A, int 目标列, int 目标行, bool 目标旋转)
        {
            if (A == null || A.列 < 0 || 形状解析 == null) return false;
            var 形状 = 形状解析(A.标识);
            int 宽 = 目标旋转 ? 形状.高 : 形状.宽;
            int 高 = 目标旋转 ? 形状.宽 : 形状.高;
            var 目标 = 区域内物品(目标列, 目标行, 宽, 高).FindAll(b => b != A);
            if (目标.Count == 0) return 移动堆叠(A, 目标列, 目标行, 目标旋转);   // 空区：移动
            if (!可放置忽略多个(A.标识, 目标列, 目标行, 目标旋转, 目标, A)) return false;
            int 原宽 = A.旋转 ? 形状.高 : 形状.宽;
            int 原高 = A.旋转 ? 形状.宽 : 形状.高;
            var 布局 = 区域安置(目标, A, 原宽, 原高, A, 目标列, 目标行, 宽, 高);   // 禁区=A 新位置：目标物品搬回原位时不能占 A 将落位的格子
            if (布局 == null) return false;
            // 备份 参与者(含 形状/位置) 用于回滚
            var 备份 = new List<(物品堆叠, int, int, bool)>();
            备份.Add((A, A.列, A.行, A.旋转));
            foreach (var 物品 in 目标) 备份.Add((物品, 物品.列, 物品.行, 物品.旋转));
            A.列 = 目标列; A.行 = 目标行; A.旋转 = 目标旋转;   // A 落 目标区
            foreach (var (物品, 列, 行) in 布局) { 物品.列 = 列; 物品.行 = 行; }   // 目标物品 搬回 A 原位
            if (布局安全()) return true;   // 全网格校验：不越界、两两不重叠
            foreach (var (物品, 列, 行, 旋转) in 备份) { 物品.列 = 列; 物品.行 = 行; 物品.旋转 = 旋转; }   // 回滚
            return false;
        }

        // 校验整个网格布局：所有物品不越界、两两不重叠（区域交换后安全验证）
        public bool 布局安全()
        {
            if (形状解析 == null) return false;
            var 物品 = new List<物品堆叠>();
            foreach (var s in 网格物品) if (s != null && s.列 >= 0) 物品.Add(s);
            for (int i = 0; i < 物品.Count; i++)
            {
                var a = 物品[i];
                var 形状a = 形状解析(a.标识);
                int aw = a.旋转 ? 形状a.高 : 形状a.宽;
                int ah = a.旋转 ? 形状a.宽 : 形状a.高;
                if (a.列 < 0 || a.行 < 0 || a.列 + aw > 网格列 || a.行 + ah > 网格行) return false;
                // 形状校验：物品覆盖格必须全部可用且同一块内（容器内部形状空洞不可放、不可跨口袋）
                if (!覆盖格合法(a.列, a.行, aw, ah)) return false;
                for (int j = i + 1; j < 物品.Count; j++)
                {
                    var b = 物品[j];
                    var 形状b = 形状解析(b.标识);
                    int bw = b.旋转 ? 形状b.高 : 形状b.宽;
                    int bh = b.旋转 ? 形状b.宽 : 形状b.高;
                    if (a.列 < b.列 + bw && a.列 + aw > b.列 && a.行 < b.行 + bh && a.行 + ah > b.行) return false;   // 重叠
                }
            }
            return true;
        }

        // ================= 堆叠规则 =================

        // 该标识是否可堆叠（堆叠上限 > 1 才可合并；武器/防具/任务品 上限 1 = 每格独立一件）
        public bool 可堆叠(string 标识) => 堆叠上限(标识) > 1;

        // 该标识的堆叠上限（0/缺省 = 不可堆叠）
        public int 堆叠上限(string 标识) => 堆叠上限解析?.Invoke(标识) ?? 0;

        // 两堆叠能否合并：同标识 + 可堆叠 + 无配件（配件是装备实例，不可并入）+ 品质覆盖相同（空则忽略）+ 目标未满
        public bool 可合并(物品堆叠 目标, 物品堆叠 来源)
        {
            if (目标 == null || 来源 == null || 目标 == 来源) return false;
            if (目标.标识 != 来源.标识) return false;
            if (目标.配件 != null && 目标.配件.Count > 0) return false;
            if (来源.配件 != null && 来源.配件.Count > 0) return false;
            if (!string.IsNullOrEmpty(目标.品质) || !string.IsNullOrEmpty(来源.品质))
                if (目标.品质 != 来源.品质) return false;
            if (!可堆叠(目标.标识)) return false;
            return 目标.数量 < 堆叠上限(目标.标识);
        }

        // 合并：来源并入目标（受上限限制），返回实际并入数量；来源耗尽则移除。来源剩余留在原格（拖拽源未移动）。
        public int 合并堆叠(物品堆叠 目标, 物品堆叠 来源)
        {
            if (!可合并(目标, 来源)) return 0;
            int 空位 = 堆叠上限(目标.标识) - 目标.数量;
            int 并入 = Math.Min(空位, 来源.数量);
            if (并入 <= 0) return 0;
            目标.数量 += 并入;
            来源.数量 -= 并入;
            if (来源.数量 <= 0) 网格物品.Remove(来源);
            return 并入;
        }

        // 放入网格（填已有堆叠优先，满额再开新堆叠；不可堆叠每次新开一件）。返回实际放入数量（0 = 一个都放不下）。
        public int 放入网格(string 标识, int 数量 = 1)
        {
            if (string.IsNullOrEmpty(标识) || 数量 <= 0) return 0;
            int 原数量 = 数量;
            int 上限 = 堆叠上限(标识);
            // ① 先填已入格的未满堆叠（仅 列>=0、无配件）
            foreach (var 堆叠 in 网格物品)
            {
                if (堆叠 == null || 堆叠.列 < 0 || 堆叠.标识 != 标识) continue;
                if (堆叠.配件 != null && 堆叠.配件.Count > 0) continue;
                int 空位 = 上限 - 堆叠.数量;
                if (空位 <= 0) continue;
                int 并入 = Math.Min(空位, 数量);
                堆叠.数量 += 并入;
                数量 -= 并入;
                if (数量 <= 0) return 原数量;
            }
            // ② 剩余开新堆叠（可堆叠：每堆 ≤上限；不可堆叠：每堆 1 件）
            while (数量 > 0)
            {
                int 本次 = 上限 > 1 ? Math.Min(上限, 数量) : 1;
                var 新堆叠 = new 物品堆叠(标识, 本次) { 当前耐久 = 有效最大耐久解析?.Invoke(标识) ?? 0 };   // 装备初始化完整耐久
                bool 放下 = false;
                for (int 行 = 0; 行 < 网格行 && !放下; 行++)
                    for (int 列 = 0; 列 < 网格列 && !放下; 列++)
                        if (可放置(标识, 列, 行, false))
                        {
                            新堆叠.列 = 列; 新堆叠.行 = 行;
                            网格物品.Add(新堆叠);
                            放下 = true;
                        }
                if (!放下) return 原数量 - 数量;   // 网格满了：返回实际放入数（部分已放入）
                数量 -= 本次;
            }
            return 原数量;
        }

        // 放入一个已有堆叠实例（含配件的装备/掉落物/换装回包/旧存档迁移）：找空位放置并写入坐标；网格满返回 false（不改变该堆叠）
        public bool 放入网格堆叠(物品堆叠 堆叠)
        {
            if (堆叠 == null || string.IsNullOrEmpty(堆叠.标识) || 堆叠.数量 <= 0 || 形状解析 == null) return false;
            if (堆叠.列 >= 0) return true;   // 已在网格中
            bool 已在网格 = 网格物品.Contains(堆叠);   // 旧存档迁移的堆叠已在列表里（列=-1），勿重复添加
            for (int 行 = 0; 行 < 网格行; 行++)
                for (int 列 = 0; 列 < 网格列; 列++)
                    if (可放置(堆叠.标识, 列, 行, false))
                    {
                        堆叠.列 = 列; 堆叠.行 = 行;
                        if (!已在网格) 网格物品.Add(堆叠);
                        return true;
                    }
            return false;
        }

        // 从网格移除（数量耗尽则删除）
        public void 从网格移除(string 标识, int 数量 = 1)
        {
            var 堆叠 = 找堆叠(标识);
            if (堆叠 == null) return;
            堆叠.数量 -= 数量;
            if (堆叠.数量 <= 0) 网格物品.Remove(堆叠);
        }

        private 物品堆叠 找堆叠(string 标识)
        {
            foreach (var 堆叠 in 网格物品) if (堆叠.标识 == 标识 && 堆叠.数量 > 0) return 堆叠;
            return null;
        }

        // ================= 网格基础操作 =================

        // 添加物品（网格版）：等价于 放入网格 —— 先并入已有未满堆叠，再开新堆叠；返回实际放入数量
        public int 添加物品(string 标识, int 数量 = 1) => 放入网格(标识, 数量);

        public void 添加堆叠(物品堆叠 堆叠)
        {
            if (堆叠 == null || string.IsNullOrEmpty(堆叠.标识)) return;
            网格物品.Add(堆叠);
        }

        public bool 移除物品(string 标识, int 数量 = 1)
        {
            if (string.IsNullOrEmpty(标识)) return false;
            for (int i = 0; i < 网格物品.Count; i++)
            {
                var 堆叠 = 网格物品[i];
                if (堆叠.标识 == 标识 && 堆叠.数量 >= 数量)
                {
                    堆叠.数量 -= 数量;
                    if (堆叠.数量 <= 0) 网格物品.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public int 物品数量(string 标识)
        {
            if (string.IsNullOrEmpty(标识)) return 0;
            foreach (var 堆叠 in 网格物品) if (堆叠.标识 == 标识) return 堆叠.数量;
            return 0;
        }

        public bool 持有物品(string 标识) => 物品数量(标识) > 0;

        // 背包装备 → 网格尺寸（默认背包 50 格 10×5 / 战术背包 5×4 / 登山包 6×5 / 腰包 4×2）
        public (int 列, int 行) 背包网格尺寸(string 包标识 = null)
        {
            if (string.IsNullOrEmpty(包标识)) return (5, 10);   // 默认背包 50 格（5列×10行，测试）
            if (包标识.Contains("腰包")) return (4, 2);
            if (包标识.Contains("战术")) return (5, 4);
            if (包标识.Contains("登山")) return (6, 5);
            return (5, 10);
        }

        public void 应用背包装备(string 包标识)
        {
            var (列, 行) = 背包网格尺寸(包标识);
            网格列 = 列; 网格行 = 行;
        }
    }
