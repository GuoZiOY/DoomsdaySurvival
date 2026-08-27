using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 信息展示面板（场景手动搭建 UI）：右键菜单「详情」呼出，独立展示物品完整信息（替代同面板详情文本）。
//   暴露参数：面板根（初始隐藏）+ 详情文本（TMP，富文本单文本即可）+ 关闭按钮（可选）。
//   显示物品：堆叠（背包/容器内）；显示槽位：已装备（构造临时堆叠，含词缀/耐久）。
public sealed class 信息面板 : MonoBehaviour
{
    public static 信息面板 实例;   // 场景挂载自动登记

    [SerializeField] private RectTransform 面板根;   // 面板根（初始隐藏；显示时置顶 + 居中）
    [SerializeField] private TMP_Text 详情文本;      // 物品完整详情（物品工具.构建详情 输出）
    [SerializeField] private Button 关闭按钮;        // 可选：关闭面板

    void Awake()
    {
        实例 = this;
        if (面板根 != null) 面板根.gameObject.SetActive(false);
        if (关闭按钮 != null) 关闭按钮.onClick.AddListener(关闭);
    }

    // 显示 背包/容器内 物品堆叠 的详情
    public void 显示(物品堆叠 堆叠, 背包服务 服务 = null)
    {
        if (面板根 == null || 堆叠 == null) return;
        var 档案 = ServiceRegistry.Get<PlayerService>().档案;
        var 数据 = ServiceRegistry.Get<DataService>();
        if (详情文本 != null) 详情文本.text = 物品工具.构建详情(档案, 数据, 堆叠, 服务 ?? 档案.背包服务);
        面板根.gameObject.SetActive(true);
        面板根.SetAsLastSibling();   // 置顶
        居中定位();
    }

    // 显示 已装备槽位 的详情（构造临时堆叠：标识/耐久/词缀）
    public void 显示槽位(string 槽位)
    {
        var 档案 = ServiceRegistry.Get<PlayerService>().档案;
        if (档案 == null) return;
        var 记录 = 档案.装备.Find(e => e.槽位 == 槽位);
        if (记录 == null || string.IsNullOrEmpty(记录.标识)) return;
        显示(new 物品堆叠(记录.标识, 1) { 当前耐久 = 记录.当前耐久, 词缀 = 记录.词缀 }, 档案.背包服务);
    }

    public void 关闭()
    {
        if (面板根 != null) 面板根.gameObject.SetActive(false);
    }

    // 定位：屏幕中央（pivot 任意都居中）
    private void 居中定位()
    {
        var 画布 = 面板根.GetComponentInParent<Canvas>();
        var 画布根 = 画布 != null ? (RectTransform)画布.transform : null;
        if (画布根 == null) return;
        var 中心 = 画布根.TransformPoint(new Vector3(画布根.rect.xMin + 画布根.rect.width * 0.5f, 画布根.rect.yMin + 画布根.rect.height * 0.5f, 0f));
        float 宽 = 面板根.rect.width * 面板根.lossyScale.x;
        float 高 = 面板根.rect.height * 面板根.lossyScale.y;
        面板根.position = 中心 + 面板根.right * (宽 * (面板根.pivot.x - 0.5f)) - 面板根.up * (高 * (面板根.pivot.y - 0.5f));
    }
}
