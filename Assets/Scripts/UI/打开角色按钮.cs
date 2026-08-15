using UnityEngine;
using UnityEngine.UI;

// HUD 上的「角色」按钮：点击发布 打开角色面板事件（面板管理器 路由）
public sealed class 打开角色按钮 : MonoBehaviour
{
    [SerializeField] private Button 按钮;

    void Awake()
    {
        按钮?.onClick.AddListener(() => ServiceRegistry.Get<EventBus>().发布(new 打开角色面板事件()));
    }
}
