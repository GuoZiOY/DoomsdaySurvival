
    // 网格服务：包装玩家档案，操作后发布 背包变化事件
    public sealed class InventoryService
    {
        private readonly EventBus 事件;
        private readonly PlayerService 玩家服务;
        private 玩家档案 档案 => 玩家服务.档案;   // 动态取当前档案

        public InventoryService(EventBus 事件, PlayerService 玩家服务) { this.事件 = 事件; this.玩家服务 = 玩家服务; }

        public void 添加(string 标识, int 数量 = 1) { 档案.添加物品(标识, 数量); 事件.发布(new 背包变化事件(标识, 数量, 变化原因.获得)); }
        public bool 移除(string 标识, int 数量 = 1) { if (档案.移除物品(标识, 数量)) { 事件.发布(new 背包变化事件(标识, -数量, 变化原因.消耗)); return true; } return false; }
        public int 数量(string 标识) => 档案.物品数量(标识);
        public bool 持有(string 标识) => 档案.持有物品(标识);
    }
