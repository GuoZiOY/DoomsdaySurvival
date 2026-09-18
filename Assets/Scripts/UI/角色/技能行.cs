using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// 技能行：技能子面板「已学技能列表」的行模板组件（挂在模板行根节点上，场景里默认 SetActive(false)）。
//   名称 / 置灰 / 左键（选中，详情区显示它）/ **右键（弹出 6 个技能格的换槽小菜单）**。
public sealed class 技能行 : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_Text 名称;        // 技能名（页面已加书名号；**不写着色**，颜色归场景）
    [SerializeField] private GameObject 置灰;      // 没装进技能槽时亮出
    [SerializeField] private Button 按钮;          // 可选：只用来挂悬停/点击音效（点击回调不走它，见下）

    private const string 名置灰 = "置灰";           // 引用位空着时按名兜底（只找，不建）
    private GameObject 灰缓存;
    private bool 找过灰;

    private UnityEngine.Events.UnityAction 左键回调;
    private UnityEngine.Events.UnityAction 右键回调;

    // 左键 / 右键 都从这里进（uGUI 把两种键都送到 `IPointerClickHandler`）。
    public void OnPointerClick(PointerEventData 事件)
    {
        if (事件.button == PointerEventData.InputButton.Right) 右键回调?.Invoke();
        else 左键回调?.Invoke();
    }

    public void 绑定(string 名称文本, bool 置灰中, UnityEngine.Events.UnityAction 左键, UnityEngine.Events.UnityAction 右键)
    {
        左键回调 = 左键;
        右键回调 = 右键;
        面板基类.设文本(名称, 名称文本);

        var 灰 = 取置灰();
        if (灰 != null) 灰.SetActive(置灰中);

        // ⚠ **不给 `按钮.onClick` 接线**：行根同时挂着 Button 与本体（都实现 `IPointerClickHandler`）时，
        //   一次点击会被两边各接一次（点一下"选中"又立刻"取消"）。回调统一走 `OnPointerClick`，
        //   `按钮` 只用来注册悬停/点击音效（它仍是这一行的点击面）。
        if (按钮 != null) 音效管理器.实例?.注册按钮(按钮);
    }

    // 置灰节点取用：引用位优先 → 按名兜底。`找过灰` = 记住"找过了、没有"（`绑定` 每行每次刷新都要跑）。
    private GameObject 取置灰()
    {
        if (灰缓存 == null && !找过灰)
        {
            找过灰 = true;
            灰缓存 = 置灰 != null ? 置灰 : 找子(transform, 名置灰)?.gameObject;
        }
        return 灰缓存;
    }

    private static Transform 找子(Transform 根, string 名)
    {
        if (根 == null) return null;
        for (int i = 0; i < 根.childCount; i++)
        {
            var 子 = 根.GetChild(i);
            if (子.name == 名) return 子;
            var 深 = 找子(子, 名);
            if (深 != null) return 深;
        }
        return null;
    }
}
