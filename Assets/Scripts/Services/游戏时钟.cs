using UnityEngine;

// 游戏时钟：推进游戏内时间（1 现实秒 = 2 游戏分钟），游戏分钟数变化时发布 时间变化事件。
// 挂在场景（由「界面搭建工具」创建），UI 订阅事件刷新显示——逻辑与 UI 解耦。
public sealed class 游戏时钟 : MonoBehaviour
{
    private float 上次分钟 = -1f;

    private void Update()
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null) return;
        玩家.游戏分钟数 += Time.deltaTime * 2f;
        int 当前分钟 = (int)玩家.游戏分钟数;
        if (当前分钟 != (int)上次分钟)
        {
            上次分钟 = 当前分钟;
            ServiceRegistry.Get<EventBus>()?.发布(new 时间变化事件(玩家.游戏分钟数));
        }
    }
}
