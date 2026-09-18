using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 设置面板（全局覆盖层）：全局音量 / 音乐音量 / 音效音量（各带一个数字文本）+ 返回主菜单。
// 主菜单「设置」与侧边栏「设置」都打开它。
// ★ 开关收口在 `面板管理器`（`打开设置()`）：调用方只是快捷方式，**自己不再持有本组件的引用**。
//   独立显隐（不走面板路由）：打开 = SetActive(true) + 同步滑条；关闭 = SetActive(false)。
//
// 滑条量程：三个都是 **0~10 整数**（在场景里设 Min 0 / Max 10 + 勾 `Whole Numbers`）。
//   · 全局音量 → `音效管理器.设置音量(值 / 10f)`（它收 0~1 的倍数，直接进 `AudioListener.volume`；0 = 静音）
//   · 音乐音量 → `设置背景音乐音量(值)`（它本来就是 0~10 整数）
//   · 音效音量 → `设置通用音效音量(值)`
public sealed class 设置面板 : MonoBehaviour
{
    [SerializeField] private Slider 全局音量;
    [SerializeField] private Slider 音乐音量;
    [SerializeField] private Slider 音效音量;
    [SerializeField] private TMP_Text 全局音量值;
    [SerializeField] private TMP_Text 音乐音量值;
    [SerializeField] private TMP_Text 音效音量值;
    [SerializeField] private Button 返回主菜单按钮;
    // 可选的「关闭」出口：**留着**是因为游戏内（侧边栏）打开设置时，"返回主菜单"会离开当前这一局 ——
    //   没有关闭按钮就没有别的出口了（本面板没有"点面板外关闭"的机制）。不接这个引用位 = 没有关闭按钮。
    [SerializeField] private Button 关闭按钮;

    void Awake()
    {
        接滑条(全局音量, 全局音量值, 值 => 音效管理器.实例?.设置音量(值 / 10f));
        接滑条(音乐音量, 音乐音量值, 值 => 音效管理器.实例?.设置背景音乐音量(值));
        接滑条(音效音量, 音效音量值, 值 => 音效管理器.实例?.设置通用音效音量(值));
        关闭按钮?.onClick.AddListener(关闭);
        if (返回主菜单按钮 != null)
        {
            返回主菜单按钮.onClick.AddListener(返回主菜单);
            音效管理器.实例?.注册按钮(返回主菜单按钮);   // 幂等：悬停/点击反馈沿用项目既有口径
        }
    }

    // 打开：先把三个滑条同步成**当前实际值**（`SetValueWithoutNotify` —— 同步这一步不要再写回去），再激活
    public void 打开()
    {
        同步滑条();
        gameObject.SetActive(true);
    }

    // 关闭：失活
    public void 关闭() => gameObject.SetActive(false);

    // 返回主菜单：走 `面板管理器.回主菜单()` —— 它会顺手把本覆盖层收掉（见 面板管理器.收起覆盖层）
    private void 返回主菜单() => 面板管理器.实例?.回主菜单();

    // 一个滑条 = 一个数字文本 + 一个"把值写进服务"的动作。文本允许为空（没搭就不显示数字）。
    private static void 接滑条(Slider 滑条, TMP_Text 数字, Action<int> 应用)
    {
        if (滑条 == null) return;
        滑条.onValueChanged.AddListener(v => { int 值 = 取整(v); 面板基类.设文本(数字, 值.ToString()); 应用(值); });
    }

    private void 同步滑条()
    {
        var 音 = 音效管理器.实例;
        if (音 == null) return;
        设滑条(全局音量, 全局音量值, 取整(音效管理器.当前音量 * 10f));
        设滑条(音乐音量, 音乐音量值, 音.背景音乐音量当前);
        设滑条(音效音量, 音效音量值, 音.通用音效音量当前);
    }

    // 同步滑条 + 数字文本；`SetValueWithoutNotify` = 不触发回调（否则会把刚读到的值再写回服务一遍）
    private static void 设滑条(Slider 滑条, TMP_Text 数字, int 值)
    {
        if (滑条 != null) 滑条.SetValueWithoutNotify(值);
        面板基类.设文本(数字, 值.ToString());
    }

    private static int 取整(float 值) => Mathf.Clamp(Mathf.RoundToInt(值), 0, 10);
}
