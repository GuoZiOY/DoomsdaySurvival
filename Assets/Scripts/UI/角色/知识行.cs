using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// 知识行：知识子面板「左列表」的行模板组件（挂在模板行根节点上，场景里默认 SetActive(false)）。
//   四样：名称（页面已按品质着色、加书名号）/ 等级（页面给的 `· II`）/ 选中高亮 / 未掌握置灰。
//   后两个都是**预置节点**，本类只切显隐（怎么亮、怎么灰全归场景）。
public sealed class 知识行 : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_Text 名称;
    [SerializeField] private TMP_Text 等级;
    [SerializeField] private GameObject 选中高亮;
    [SerializeField] private GameObject 未掌握置灰;
    [SerializeField] private Button 按钮;          // 可选：模板行用 Button 做点击面时也接同一路

    private const string 名未掌握置灰 = "未掌握置灰";   // 引用位空着时按名兜底（只找，不建）
    private GameObject 灰缓存;
    private bool 找过灰;

    private UnityEngine.Events.UnityAction 点击回调;

    // 点击沿层级冒泡：点到行内任何子节点都算点这一行。
    public void OnPointerClick(PointerEventData 事件) => 点击回调?.Invoke();

    public void 绑定(string 名称文本, string 等级文本, bool 选中, bool 未掌握, UnityEngine.Events.UnityAction 点击)
    {
        点击回调 = 点击;
        面板基类.设文本(名称, 名称文本);
        面板基类.设文本(等级, 等级文本);
        if (选中高亮 != null) 选中高亮.SetActive(选中);

        // 未掌握置灰是**内容态**（量只随重建行变），所以放在这里；点行只翻选中态（见 `绑定选中态`）。
        var 灰 = 取未掌握置灰();
        if (灰 != null) 灰.SetActive(未掌握);

        if (按钮 != null)
        {
            按钮.onClick.RemoveAllListeners();
            按钮.onClick.AddListener(触发);
            音效管理器.实例?.注册按钮(按钮);   // 幂等：悬停/点击反馈沿用项目既有口径
        }
    }

    // 只切选中态（点行时用，不重复写行内容）。
    public void 绑定选中态(bool 选中)
    {
        if (选中高亮 != null) 选中高亮.SetActive(选中);
    }

    private void 触发() => 点击回调?.Invoke();

    // 未掌握置灰取用：引用位优先 → 按名兜底。`找过灰` = 记住"找过了、没有"（`绑定` 每行每次刷新都要跑）。
    private GameObject 取未掌握置灰()
    {
        if (灰缓存 == null && !找过灰)
        {
            找过灰 = true;
            灰缓存 = 未掌握置灰 != null ? 未掌握置灰 : 找子(transform, 名未掌握置灰)?.gameObject;
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
