using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 配方行：制作面板 的 配方行（场景 手动 搭 模板：物品图 + 名称 + 选中背景——极简；详细 内容 放 详情文本）。
// 显示：物品图标（物品图标服务 加载；无 图 = 品质 混合 色块 兜底）+ 物品名（选中 时 整体 金色）。
// 图标 完整 显示：contain 等比缩放（不变形、不裁剪、完整 看到 整件 物品）——目标 区域 = 物品图 rect（模板 摆）。
//   cover（放大 铺满 + 裁剪）会 裁掉 横条 武器 两端 → 不用；要 更大：把 物品图 rect 调 大（横向 区域 适合 武器）。
// 选中态：**金黄色背景高亮 + 名称金色**（v51 刀40）。交互：点 行 → 选中（IPointerClickHandler，无需 Button 组件）。
public sealed class 配方行 : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image 物品图;        // 物品 图标（物品图标服务 加载）
    [SerializeField] private TMP_Text 名称;       // 物品名（选中 时 金色）
    [SerializeField] private Image 选中背景;      // 行 背景（选中 = 金黄 高亮；未选中 = 面板 底 色）

    private static readonly Color 背景正常色 = 游戏主题.面板;
    // 选中 高亮 = **金黄色**（v51 刀40 用户要求）：
    //   原来是 游戏主题.选中色（钢蓝 0.42/0.66/0.82，那是给"地图节点选中"定的），
    //   放在制作面板里和"金色 = 当前/强调"这套视觉语言不一致 → 换成金色低透明底。
    // RGB 直接从 游戏主题.金色 取（不写字面量）：主题调色时这里自动跟着变。
    private static readonly Color 背景选中色 = new Color(游戏主题.金色.r, 游戏主题.金色.g, 游戏主题.金色.b, 0.18f);

    private UnityEngine.Events.UnityAction 点击回调;

    // 点击 行（IPointerClickHandler）：uGUI 事件 沿 层级 冒泡——点 物品图/名称 也 触发
    public void OnPointerClick(PointerEventData 事件)
    {
        音效管理器.实例?.播放成功();   // 点击 配方行 → 点击 音效
        点击回调?.Invoke();
    }

    // 绑定 内容：制作面板 调用（每次 刷新 新建 行 → 新 实例 无 旧 监听）
    public void 绑定(string 物品标识, string 名称文本, bool 选中, UnityEngine.Events.UnityAction 点击)
    {
        点击回调 = 点击;
        // 物品 图标：contain 等比缩放（完整 显示 不变形 不裁剪）；无 图 = 品质 混合 色块 兜底。
        // 只 改 sizeDelta（缩放）——pivot/anchoredPosition 一律 不动：保持 模板 里 摆 的 位置（顶右 居中 等）。
        if (物品图 != null)
        {
            // 目标 区域 = 物品图 rect（模板 里 摆 的 尺寸；建议 横向 区域 如 宽 100 × 高 60——武器 3:1 完整 且 大）
            float 目标宽 = 物品图.rectTransform.rect.width;
            float 目标高 = 物品图.rectTransform.rect.height;
            var 物品 = ServiceRegistry.Get<DataService>().物品.TryGetValue(物品标识, out var 物) ? 物 : null;
            var 图标 = 物品 != null ? 物品图标服务.获取(物品.标识) : null;
            if (图标 != null)
            {
                物品图.sprite = 图标;
                物品图.color = Color.white;
                物品图.preserveAspect = false;
                var 盒 = 精灵内容包围盒.获取(图标);   // position=内容中心(归一化)，size=内容占比(归一化)
                float 画布宽 = 图标.bounds.size.x, 画布高 = 图标.bounds.size.y;
                float 内容宽盒 = 画布宽 * 盒.width, 内容高盒 = 画布高 * 盒.height;
                float 放大 = Mathf.Min(目标宽 / 内容宽盒, 目标高 / 内容高盒);   // contain：完整 容纳，不 裁剪
                物品图.rectTransform.sizeDelta = new Vector2(画布宽 * 放大, 画布高 * 放大);
                // pivot / anchoredPosition 不动：图标 以 模板 位置（pivot 中心）为 基准 完整 显示
            }
            else
            {
                // 无 图（数据 未 配 图片）：品质 混合 色块 兜底（物品网格面板 同款），占满 原 区域（sizeDelta 不动）
                物品图.sprite = null;
                物品图.color = 底色(物品);
                物品图.rectTransform.sizeDelta = new Vector2(目标宽, 目标高);
            }
        }
        // 名称（选中 金色 高亮）
        if (名称 != null) 名称.text = 选中 ? $"<color={游戏主题.金色色值}>{名称文本}</color>" : 名称文本;
        // 选中 背景（高亮）
        if (选中背景 != null) 选中背景.color = 选中 ? 背景选中色 : 背景正常色;
    }

    // 品质 混合 底色（无 图 兜底；null = 默认 物品 底色）
    private static Color 底色(物品数据 物品)
    {
        if (物品 == null) return 网格面板配色.物品底色;
        return Color.Lerp(网格面板配色.物品底色, 品质工具.颜色(物品.品质档), 0.55f);
    }
}
