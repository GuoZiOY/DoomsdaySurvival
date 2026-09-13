using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 家具行：建造面板 的 家具行（场景 手动 搭 模板：名称/描述/信息/操作按钮——字体 布局 模板 里 排，所见即所得）。
// 名称 = 名称（等级），满级 时 名称 追加「·已满级」（满级 无 独立 文本/按钮）。
// 操作按钮 = 建造/升级 共用 一个：未建 = 「建造」；可升级 = 「升级」；满级 = 隐藏。
// 信息 = 占格 + 所需材料（建造/升级）。
// 场景搭建：行模板 物体 挂 本组件，拖好 各 引用（名称/描述/信息/操作按钮）。
public sealed class 家具行 : MonoBehaviour
{
    [SerializeField] private TMP_Text 名称;      // 名称（等级；满级 追加 ·已满级）
    [SerializeField] private TMP_Text 描述;      // 描述
    [SerializeField] private TMP_Text 信息;      // 信息 摘要（占格/材料）
    [SerializeField] private GameObject 操作按钮;   // 建造/升级 共用（未建=建造；可升级=升级；满级=隐藏）

    private static readonly Color 按钮正常色 = new Color(0.2f, 0.22f, 0.26f, 1f);
    private static readonly Color 按钮禁用色 = new Color(0.35f, 0.35f, 0.38f, 0.6f);

    // 绑定 内容：建造面板 调用（每次 刷新 新建 行 → 新 实例 无 旧 监听）
    // 操作按钮 状态：未建 = 「建造」；0 级 破损 = 「修复」；可升级 = 「升级」；满级 = 隐藏。
    // 材料不足：按钮 灰显，点击 只 播 失败 音效 + 提示（不 执行 操作）
    public void 绑定(string 名称文本, string 描述文本, string 信息文本, bool 可建造, bool 可修复, bool 可升级, bool 材料足够,
        UnityEngine.Events.UnityAction 建造回调, UnityEngine.Events.UnityAction 修复回调, UnityEngine.Events.UnityAction 升级回调)
    {
        if (名称 != null) 名称.text = 名称文本;
        if (描述 != null) 描述.text = 描述文本;
        if (信息 != null) 信息.text = 信息文本;   // 材料 不足 时 材料 部分 红色 由 建造面板 用 rich text 拼好
        if (操作按钮 == null) return;
        bool 可操作 = 可建造 || 可修复 || 可升级;
        操作按钮.SetActive(可操作);
        if (!可操作) return;
        var 图 = 操作按钮.GetComponent<Image>();
        if (图 != null) 图.color = 材料足够 ? 按钮正常色 : 按钮禁用色;
        var 按钮 = 操作按钮.GetComponent<Button>();
        var 文本 = 操作按钮.GetComponentInChildren<TMP_Text>();
        UnityEngine.Events.UnityAction 回调;
        if (可建造) { if (文本 != null) 文本.text = "建造"; 回调 = 建造回调; }
        else if (可修复) { if (文本 != null) 文本.text = "修复"; 回调 = 修复回调; }
        else { if (文本 != null) 文本.text = "升级"; 回调 = 升级回调; }
        if (按钮 != null)
        {
            按钮.onClick.RemoveAllListeners();
            按钮.onClick.AddListener(材料足够 ? 回调 : 材料不足反馈);
        }
    }

    // 材料 不足：失败 音效 + 日志 提示（不 执行 建造/升级）
    private void 材料不足反馈()
    {
        音效管理器.实例?.播放失败();
        ServiceRegistry.Get<EventBus>()?.发布(new 日志事件(日志类型.警告, "材料不足，无法执行。"));
    }
}
