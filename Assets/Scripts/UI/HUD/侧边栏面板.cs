using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 侧边栏面板：右侧常驻功能按钮栏（始终显示，不在 面板管理器.常驻UI 里）。
// 取消：当前显示面板.回退()（方案一：面板内语义；不可取消 → 错误音效）
// 设置：开关 设置面板（音量滑条）
// 音量：音效管理器 静音/恢复 切换（图标：开/关 换图）
// 存档：SaveService 立即保存一次
// 暂停：Time.timeScale 0↔1（游戏世界冻结；Button 仍可点）（图标：暂停/继续 换图）
// 角色：发布 打开角色面板事件（原 打开角色按钮 迁入）
// 任务：发布 打开任务面板事件（调出 系统任务面板：主线/支线/日常）
// 显隐：主菜单时隐藏 取消/暂停/角色/任务（设置/音量/存档 常驻可用）
public sealed class 侧边栏面板 : MonoBehaviour
{
    [SerializeField] private Button 取消按钮, 设置按钮, 音量按钮, 存档按钮, 暂停按钮, 角色按钮, 任务按钮;
    [SerializeField] private TMP_Text 取消文字;   // 取消按钮文案（随当前面板动态显示：取消/返回/离开城镇/关闭/回主菜单）
    [SerializeField] private 设置面板 设置面板;   // 全局设置覆盖层（音量滑条；主菜单与游戏中共用）

    // 暂停图标（暂停符号 / 继续符号）
    [SerializeField] private Image 暂停图;
    [SerializeField] private Sprite 暂停图标, 继续图标;
    // 音量图标（开 / 关）
    [SerializeField] private Image 音量图;
    [SerializeField] private Sprite 音量开图标, 音量关图标;

    void Awake()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件?.订阅<面板切换事件>(_ => { 刷新显隐(); 刷新取消文字(); });
        // 战斗开始/结束 刷新按钮显隐（战斗中禁止打开角色面板，避免战斗中在角色面板使用道具破坏平衡）
        事件?.订阅<战斗开始事件>(_ => 刷新显隐());
        事件?.订阅<战斗结束事件>(_ => 刷新显隐());
        取消按钮?.onClick.AddListener(取消);
        设置按钮?.onClick.AddListener(() => 设置面板?.切换());
        音量按钮?.onClick.AddListener(() => { 音效管理器.实例?.切换静音(); 刷新音量(); });
        存档按钮?.onClick.AddListener(存档);
        暂停按钮?.onClick.AddListener(暂停切换);
        角色按钮?.onClick.AddListener(() => 事件?.发布(new 打开角色面板事件()));
        任务按钮?.onClick.AddListener(() => 事件?.发布(new 打开任务面板事件()));
        刷新显隐();
        刷新音量();
        刷新取消文字();
    }

    // 取消文字随当前面板变化（如 战斗选目标=取消 / 设施=返回 / 角色=关闭 / 结局=回主菜单）
    private void Update()
    {
        var 当前 = 面板管理器.实例?.当前显示面板;
        string 文本 = 当前?.取消文本 ?? "取消";
        if (取消文字 != null && 取消文字.text != 文本) { 取消文字.text = 文本; }
    }

    // 取消 = 当前显示面板.回退()；未消费（无可取消）→ 错误音效
    private void 取消()
    {
        var 当前 = 面板管理器.实例?.当前显示面板;
        if (当前 == null || !当前.回退()) 音效管理器.实例?.播放失败();
    }

    private void 刷新取消文字()
    {
        var 当前 = 面板管理器.实例?.当前显示面板;
        if (取消文字 != null) 取消文字.text = 当前?.取消文本 ?? "取消";
    }

    // 存档：立即保存一次（PlayerPrefs 覆盖写入）；未开始冒险（当前节点为空）时禁止，避免存出坏档
    private void 存档()
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null || string.IsNullOrEmpty(玩家.当前节点))
        {
            音效管理器.实例?.播放失败();
            ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.警告, "尚未开始冒险，无可保存的进度。"));
            return;
        }
        ServiceRegistry.Get<SaveService>()?.保存(玩家);
        ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.系统, "已保存游戏。"));
    }

    // 暂停：Time.timeScale 0↔1（游戏世界冻结）；图标切换 暂停/继续
    private void 暂停切换()
    {
        bool 暂停中 = Time.timeScale == 0f;
        Time.timeScale = 暂停中 ? 1f : 0f;
        if (暂停图 != null) 暂停图.sprite = 暂停中 ? 暂停图标 : 继续图标;
    }

    // 主菜单隐藏 取消/暂停/角色；战斗中隐藏 角色（禁止战斗中打开角色面板）；暂停中切回主菜单则解除暂停并复位图标
    private void 刷新显隐()
    {
        bool 主菜单 = 面板管理器.实例?.当前显示面板 is 主菜单面板;
        bool 战斗中 = ServiceRegistry.Get<BattleService>()?.战斗中 ?? false;
        if (取消按钮 != null) 取消按钮.gameObject.SetActive(!主菜单);
        if (暂停按钮 != null) 暂停按钮.gameObject.SetActive(!主菜单);
        if (角色按钮 != null) 角色按钮.gameObject.SetActive(!主菜单 && !战斗中);
        if (任务按钮 != null) 任务按钮.gameObject.SetActive(!主菜单 && !战斗中);
        if (主菜单 && Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
            if (暂停图 != null) 暂停图.sprite = 暂停图标;
        }
    }

    private void 刷新音量()
    {
        bool 静音 = 音效管理器.实例?.已静音 ?? false;
        if (音量图 != null) 音量图.sprite = 静音 ? 音量关图标 : 音量开图标;
    }
}
