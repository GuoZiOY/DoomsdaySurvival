using UnityEngine;

    // 组合根：装配全部服务（幂等）。数据校验失败时记日志并置 装配失败，停止装配后续服务。
    // M1 起挂在场景「框架引导」物体上；M4 起接管游戏主流程。
    // DefaultExecutionOrder=-100：保证 Awake 装配先于所有面板（默认 0），否则面板 Awake 取 EventBus 会因未装配抛异常。
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        private static bool 已装配;
        public static bool 装配失败 { get; private set; }

        private void Awake() => 装配();

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
                return;
            }

            // —— 业务服务 ——
            var 玩家 = new PlayerService(事件, 数据, ServiceRegistry.Get<SaveService>());
            玩家.初始化();
            ServiceRegistry.Register(玩家);

            // —— 对话引擎（剧情驱动）——
            var 对话 = new DialogueService(事件, 数据, 玩家);
            ServiceRegistry.Register(对话);

            var 档案 = 玩家.档案;
            ServiceRegistry.Register(new InventoryService(事件, 档案));
            ServiceRegistry.Register(new QuestService(事件, 数据, 档案));
            var 战斗服务 = new BattleService(事件, 数据, 档案);
            ServiceRegistry.Register(战斗服务);
            ServiceRegistry.Register(new 探索服务(事件, 数据, 玩家, 战斗服务));
            ServiceRegistry.Register(new 技能服务(事件, 数据, 档案));
            ServiceRegistry.Register(new 地图服务(事件, 数据, 玩家, ServiceRegistry.Get<探索服务>(), 对话));

            Debug.Log($"[GameBootstrap] 核心服务装配完成：剧情 {数据.剧情.Count} / 敌人 {数据.敌人.Count} / 物品 {数据.物品.Count} / 区域 {数据.区域.Count} / 技能 {数据.技能.Count} / 任务 {数据.任务.Count} / 地点 {数据.地图.Count}");
            // UI 由场景「UI管理器」组件装配与驱动（主菜单/剧情/设施面板均由它切换显示）
        }
    }
