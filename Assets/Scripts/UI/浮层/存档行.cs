using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 存档行：存档面板「槽位列表」的行模板组件（挂在模板行根节点上，场景里默认 SetActive(false)）。
//   存档名（槽名，"自动存档"/"手动存档 1"…）+ 信息块（名称/职业/等级/最后游玩时间 四行）+ 读取 / 保存 / 删除。
//   可用性与按钮文案**全部由页面算好传进来**（门禁 与 二次确认 都在页面：这一层只管显示与转发点击）。
public sealed class 存档行 : MonoBehaviour
{
    [SerializeField] private TMP_Text 存档名;      // 槽名（一行）
    [SerializeField] private TMP_Text 摘要;        // 信息块（四行，见 `存档面板.信息文本`）
    [SerializeField] private Button 读取按钮, 保存按钮, 删除按钮;
    [SerializeField] private TMP_Text 读取文字, 保存文字, 删除文字;   // 三个按钮上的字（二次确认时要改成"再点一次"）

    public void 绑定(string 存档名文本, string 摘要文本, bool 可读, bool 可写, bool 可删,
                     string 读取文案, string 保存文案, string 删除文案,
                     Action 读取, Action 保存, Action 删除)
    {
        面板基类.设文本(存档名, 存档名文本);
        面板基类.设文本(摘要, 摘要文本);
        面板基类.设文本(读取文字, 读取文案);
        面板基类.设文本(保存文字, 保存文案);
        面板基类.设文本(删除文字, 删除文案);
        接按钮(读取按钮, 可读, 读取);
        接按钮(保存按钮, 可写, 保存);
        接按钮(删除按钮, 可删, 删除);
    }

    // `可用` 用 interactable 表达（状态，不是外观：变灰的样子归 Button 的过渡/预制体）。
    private static void 接按钮(Button 按钮, bool 可用, Action 点击)
    {
        if (按钮 == null) return;
        按钮.onClick.RemoveAllListeners();
        按钮.interactable = 可用;
        if (!可用) return;
        按钮.onClick.AddListener(() => 点击?.Invoke());
        音效管理器.实例?.注册按钮(按钮);   // 幂等：悬停/点击反馈沿用项目既有口径
    }
}
