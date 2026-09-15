using UnityEngine;
using UnityEngine.UI;

// 侧边栏面板：右侧常驻功能按钮栏（始终显示，不在 面板管理器.常驻UI 里）。
// 现在只剩 4 颗：设置 / 音量 / 存档 / 暂停。
//
// ★ 已停用 3 颗（用户 2026-09-15：「任务、取消、角色 都不需要了」）—— 停用带来的**真实影响**记在这里：
//   · **取消**：它是**全局回退的唯一入口**（全项目没有任何键位绑定回退）。
//       删掉后不受影响的是：安全屋（自带「出门」按钮）· 区域（走到「街口」）· 房间（走到大门/楼梯）·
//       持有面板（自带「返回」）· 角色创建（自带「返回主菜单」）· 对话（自带推进）。
//       受影响的是：**大世界失去了「一键回安全屋」** —— 回家只能走到营地门口格。
//       那条路本来就是设计里的正路（`大世界探索服务.抵达格` 发 `打开营地事件`），
//       所以这不是功能缺失，是少一个便捷按钮。
//       注：`面板基类.取消文本` 这个协议**仍然有效**（各面板照旧覆写它），只是不再有地方显示。
//   · **角色**：这颗按钮是 `打开角色面板事件` 的**唯一发布方**。删掉之后 `角色面板` 不可达
//       （`面板管理器.角色` 引用位与那条事件订阅仍然留着 —— 那是"能力"，只是没有 UI 入口了）。
//       `持有面板`（F1）已覆盖 装备区 + 装具区 + 仓库；角色面板只剩属性/技能侧。
//   · **任务**：任务面板**本来就没建**（`面板管理器.任务` 是个 `面板基类 待建` 引用位），
//       而用户 2026-09-13 已拍板「剧情 / 任务不做」→ 这颗按钮点了没有任何反应，是纯噪声。
//
// ★ 为什么字段还留着、而不是直接删：
//   **代码改不了场景**。这三个 Button 物体还在 `Assets/Scenes/SampleScene.unity` 里，
//   一旦删掉字段就再也没人能关掉它们（会变成"永远可见、点了没反应"—— 比现在更糟）。
//   所以在 Awake 里 `SetActive(false)`。**你在 Editor 里把那三个物体删掉之后，这三行字段可以一起删。**
public sealed class 侧边栏面板 : MonoBehaviour
{
    [SerializeField] private Button 设置按钮, 音量按钮, 存档按钮, 暂停按钮;

    // —— 已停用：只用来在 Awake 里关掉（理由见文件头）。场景里删掉这三个物体后可以删这三行。——
    [SerializeField] private Button 取消按钮, 角色按钮, 任务按钮;

    [SerializeField] private 设置面板 设置面板;   // 全局设置覆盖层（音量滑条；主菜单与游戏中共用）

    // 暂停图标（暂停符号 / 继续符号）
    [SerializeField] private Image 暂停图;
    [SerializeField] private Sprite 暂停图标, 继续图标;
    // 音量图标（开 / 关）
    [SerializeField] private Image 音量图;
    [SerializeField] private Sprite 音量开图标, 音量关图标;

    void Awake()
    {
        停用不用的按钮();

        var 事件 = ServiceRegistry.Get<EventBus>();
        事件?.订阅<面板切换事件>(_ => 刷新显隐());
        // 战斗开始/结束 刷新按钮显隐（战斗中禁存档，见 刷新显隐）
        事件?.订阅<战斗开始事件>(_ => 刷新显隐());
        事件?.订阅<战斗结束事件>(_ => 刷新显隐());
        设置按钮?.onClick.AddListener(() => 设置面板?.切换());
        音量按钮?.onClick.AddListener(() => { 音效管理器.实例?.切换静音(); 刷新音量(); });
        存档按钮?.onClick.AddListener(存档);
        暂停按钮?.onClick.AddListener(暂停切换);
        刷新显隐();
        刷新音量();
    }

    // 把用户已不需要的三颗按钮关掉（场景物体由 Unity 保留，这里只让它不可见、不可点）
    private void 停用不用的按钮()
    {
        取消按钮?.gameObject.SetActive(false);
        角色按钮?.gameObject.SetActive(false);
        任务按钮?.gameObject.SetActive(false);
    }

    // 存档：打开存档面板（选槽保存/读取/删除）。
    // ★ 刀64：允许条件收敛到 **存档门禁**（"唯一真相"）—— 未开局 / 战斗中 都在那里判，
    //   文案也统一从那里来（含用户口径「战斗时不需要存档读档」）。
    //   本按钮的显隐（刷新显隐）是第一层"体验"，门禁才是第二层"正确性"——两层都要在。
    private void 存档()
    {
        string 拒 = 存档门禁.存前检查();
        if (拒 != "")
        {
            音效管理器.实例?.播放失败();
            ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.警告, 拒));
            return;
        }
        存档面板.打开(false);
    }

    // 暂停：Time.timeScale 0↔1（游戏世界冻结）；图标切换 暂停/继续
    private void 暂停切换()
    {
        bool 暂停中 = Time.timeScale == 0f;
        Time.timeScale = 暂停中 ? 1f : 0f;
        if (暂停图 != null) 暂停图.sprite = 暂停中 ? 暂停图标 : 继续图标;
    }

    // 主菜单隐藏 暂停/存档；战斗中隐藏 存档。
    //   · ★ v51 刀15 战斗中**禁"存档"**：档案里不含战斗状态，战斗期又已经推进了游戏分钟，
    //     于是"在必败的战斗里存档 → 读档"= 免费逃跑，且读档后时间与状态不一致。
    //     （刀64 起代码层还有 `存档门禁` 兜着；按钮显隐只是第一层。）
    //   · 已停用的三颗不在这里管 —— 它们由 Awake 一次性关掉，之后不再被打开。
    private void 刷新显隐()
    {
        bool 主菜单 = 面板管理器.实例?.当前显示面板 is 主菜单面板;
        bool 战斗中 = ServiceRegistry.Get<BattleService>()?.战斗中 ?? false;
        if (暂停按钮 != null) 暂停按钮.gameObject.SetActive(!主菜单);
        if (存档按钮 != null) 存档按钮.gameObject.SetActive(!主菜单 && !战斗中);
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
