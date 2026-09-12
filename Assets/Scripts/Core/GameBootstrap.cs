using UnityEngine;

    // 组合根：装配全部服务（幂等）。数据校验失败时记日志并置 装配失败，停止装配后续服务。
    // M1 起挂在场景「框架引导」物体上；M4 起接管游戏主流程。
    // DefaultExecutionOrder=-100：保证 Awake 装配先于所有面板（默认 0），否则面板 Awake 取 EventBus 会因未装配抛异常。
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        private static bool 已装配;
        public static bool 装配失败 { get; private set; }

        private void Awake()
        {
            装配();
            确保时间驱动();   // 世界时间 自推进 + 每小时 生存结算（饱食/水分/伤病/天气）
        }

        // 世界时间管理器 驱动 挂 本物体（场景 常驻「框架引导」）：装配后 自动 挂载——时间 才 会 走。
        // 全局 判重：场景 任意 位置 已 有 驱动 则 跳过（防 双 时钟 双倍 推进）。
        private void 确保时间驱动()
        {
            if (FindFirstObjectByType<世界时间管理器.驱动>() == null)
                gameObject.AddComponent<世界时间管理器.驱动>();
        }

        // 装配顺序：事件总线 → 数据服务 → 存档服务 → 玩家/对话/背包/任务/战斗服务
        public static void 装配()
        {
            if (已装配) return;
            已装配 = true;

            var 事件 = new EventBus();
            ServiceRegistry.Register(事件);
            var 数据 = new DataService(事件);
            ServiceRegistry.Register(数据);

            ServiceRegistry.Register(new SaveService());

            // 数据校验失败则阻止游戏进入（输出完整错误清单，不再装配业务服务）
            if (数据.校验错误.Count > 0)
            {
                装配失败 = true;
                foreach (var 错误 in 数据.校验错误)
                    Debug.LogError($"[GameBootstrap] 数据校验失败: {错误}");
                Debug.LogError($"[GameBootstrap] ❌ 数据校验未通过（{数据.校验错误.Count} 处）→ **已中止装配业务服务**。" +
                               "下面若出现「未注册服务: XXX」/「HUD 刷新异常」都是这次中止的下游，先修上面这些数据错误再运行。");
                return;
            }
            foreach (var 警告 in 数据.校验警告)
                Debug.LogWarning($"[GameBootstrap] 数据校验警告: {警告}");

            // —— 业务服务 ——
            var 玩家 = new PlayerService(事件, 数据, ServiceRegistry.Get<SaveService>());
            玩家.初始化();
            ServiceRegistry.Register(玩家);

            // —— 世界时间管理器（统一 时间 推进/结算/跳时；驱动 由 确保时间驱动 挂载）——
            ServiceRegistry.Register(new 世界时间管理器(事件));

            // —— 容器（塔科夫式嵌套容器）——
            var 容器 = new 容器服务
            {
                物品数据解析 = 标识 => 数据.物品.TryGetValue(标识, out var 物) ? 物 : null,
                家具定义解析 = 标识 => 数据.家具.TryGetValue(标识, out var 家) ? 家 : null,   // 家具容器（冰箱 等：识别 是容器/尺寸/允许放入）
            };
            容器.接线解析器(玩家.档案);   // PlayerService 已初始化，补接网格解析器
            ServiceRegistry.Register(容器);

            // —— 搜索容器（塔科夫式搜刮：随机生成 + 会话缓存）——
            var 搜索 = new 搜索服务(数据, 容器);
            搜索.接线解析器(玩家.档案);
            ServiceRegistry.Register(搜索);

            // —— 安全屋（家具 建造/升级 + 房间网格；收音机 情报/天气预知）——
            ServiceRegistry.Register(new 安全屋管理器(事件, 数据));

            // —— 制作（工作台/灶台/医疗站：按配方 制作 物品）——
            ServiceRegistry.Register(new 工作台制作服务(事件, 数据));

            // —— 对话引擎（剧情驱动）——
            var 对话 = new DialogueService(事件, 数据, 玩家);
            ServiceRegistry.Register(对话);

            ServiceRegistry.Register(new InventoryService(事件, 玩家));
            ServiceRegistry.Register(new QuestService(事件, 数据, 玩家));
            var 战斗服务 = new BattleService(事件, 数据, 玩家);
            ServiceRegistry.Register(战斗服务);
            ServiceRegistry.Register(new 房间探索服务(事件, 数据, 玩家, 战斗服务, ServiceRegistry.Get<搜索服务>()));
            ServiceRegistry.Register(new 区域探索服务(事件, 数据, 玩家));   // 区域层：一屏网格 + 若干建筑（点楼门进楼）
            ServiceRegistry.Register(new 技能服务(事件, 数据, 玩家));
            ServiceRegistry.Register(new 书籍服务(事件, 数据));   // 书籍 阅读（技能书/配方书；依赖 技能服务——运行时 取）
            ServiceRegistry.Register(new 天赋服务(事件, 玩家));   // 机制型天赋（致命伤害/制作完成 事件响应）
            var 词缀 = new 词缀服务(数据);   // 装备随机词条生成（掉落/装备实例）
            ServiceRegistry.Register(词缀);
            ServiceRegistry.Register(new 大世界探索服务(事件, 数据, 玩家));   // 大世界层：100×100 格子网格（区域副本坐在它上面）

            // 自动存档器：订阅跨天/主线/战斗结束/返回主菜单 自动保存（须在业务服务之后注册）
            ServiceRegistry.Register(new 自动存档器(事件, ServiceRegistry.Get<SaveService>(), 玩家));

            Debug.Log($"[GameBootstrap] 核心服务装配完成：剧情 {数据.剧情.Count} / 敌人 {数据.敌人.Count} / 物品 {数据.物品.Count} / 技能 {数据.技能.Count} / 任务 {数据.任务.Count} / 大世界 {数据.世界.Count}");
            // UI 由场景「UI管理器」组件装配与驱动（主菜单/剧情/设施面板均由它切换显示）
        }
    }
