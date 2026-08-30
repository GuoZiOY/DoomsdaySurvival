using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// 物品网格面板.渲染 —— 分部类（物品网格面板）：物品 实体框 渲染（品质底/图标 cover/数量角标/耐久）。
// 通用 渲染 框架（底格/分隔线/实体框表/增量刷新/定位）在 网格面板基类.cs。
// ============================================================
public sealed partial class 物品网格面板
{
    // 物品：两层结构 —— ① 物品框（全尺寸 Image = 品质底层色 + 黑描边，点击/拖拽挂这里）→ ② 内容层（内缩 Image = 深色占位块，将来贴美术图）。
    // 返回 物品框（增量刷新：键=堆叠实例，复用更新；null = 数据缺失不创建）
    private 物品框 创建物品(物品堆叠 堆叠)
    {
        if (!数据.物品.TryGetValue(堆叠.标识, out var 物品)) return null;
        var 框 = new 物品框();
        var (宽, 高) = 服务.物品占格(堆叠);
        // ① 物品框 根：全尺寸贴格（品质底层色 + 黑描边 + cover 裁剪）
        var 物体 = new GameObject($"物品_{物品.名称}", typeof(RectTransform), typeof(Image));
        物体.transform.SetParent(物品层, false);
        框.根 = 物体.GetComponent<RectTransform>();
        框.框图 = 物体.GetComponent<Image>();
        框.框图.color = 品质底层色(堆叠);
        var 边框 = 物体.AddComponent<Outline>();
        边框.effectColor = 物品描边色;
        边框.effectDistance = 物品描边距离;
        定位(框.根, 堆叠.列, 堆叠.行, 宽, 高);
        物体.AddComponent<RectMask2D>();
        // ② 高光 / 内容 / 文本角标
        框.高光层 = 创建高光层(物体.transform);
        创建内容层(物体.transform, 堆叠, 物品, out var 内容矩形);
        框.内容层 = 内容矩形;
        创建文本角标(物体.transform, 堆叠, 物品, 框);
        // ③ 点击（非按钮） + 拖拽
        挂接交互(框, 堆叠, 内容矩形);
        return 框;
    }

    // ③ 内容层：内缩（少 2×边距）色块；有图 → trim 透明留白 + 智能 cover（等比放大铺满，RectMask2D 居中裁剪）
    private void 创建内容层(Transform 父, 物品堆叠 堆叠, 物品数据 物品, out RectTransform 内容矩形)
    {
        var 内容图 = UI工具.创建图(父, "内容", null, 网格面板配色.物品底色, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        var 未旋转 = 服务.形状解析?.Invoke(堆叠.标识) ?? new 物品形状(1, 1);
        float 内容宽 = 未旋转.宽 * 格尺寸 - 网格面板配色.物品边距 * 2f;
        float 内容高 = 未旋转.高 * 格尺寸 - 网格面板配色.物品边距 * 2f;
        Vector2 内容中心 = new Vector2(0.5f, 0.5f);
        var 图标 = 物品图标服务.获取(物品.图片);
        if (图标 != null)
        {
            内容图.sprite = 图标;
            内容图.color = Color.white;
            内容图.preserveAspect = false;
            var 盒 = 精灵内容包围盒.获取(图标);
            float 画布宽 = 图标.bounds.size.x, 画布高 = 图标.bounds.size.y;
            float 内容宽盒 = 画布宽 * 盒.width, 内容高盒 = 画布高 * 盒.height;
            float 放大 = Mathf.Max(内容宽 / 内容宽盒, 内容高 / 内容高盒);
            内容宽 = 画布宽 * 放大;
            内容高 = 画布高 * 放大;
            内容中心 = new Vector2(盒.x, 盒.y);
        }
        内容矩形 = 内容图.rectTransform;
        内容矩形.pivot = 内容中心;
        内容矩形.anchoredPosition = Vector2.zero;
        内容矩形.sizeDelta = new Vector2(内容宽, 内容高);
        内容矩形.localRotation = Quaternion.Euler(0f, 0f, 堆叠.旋转 ? 90f : 0f);
    }

    // 文本角标：名称（居中，美术资源缺失时标识物品）/ 耐久（格底，损坏变红）/ 数量（右下顶角）。
    // 数量 总是创建（≤1 隐藏、>1 显示）——否则 合并/拆回 数量>1 无法补显
    private void 创建文本角标(Transform 父, 物品堆叠 堆叠, 物品数据 物品, 物品框 框)
    {
        var 名称 = UI工具.创建文本(父, "名称", 物品.名称, Mathf.Clamp(格尺寸 * 0.22f, 14f, 44f), TextAlignmentOptions.Center);
        UI工具.铺满(名称.rectTransform);
        名称.enableWordWrapping = true;
        框.名称 = 名称;
        int 耐久上限 = 档案.有效最大耐久(堆叠.标识);
        if (耐久上限 > 0)
        {
            var 耐 = UI工具.创建文本(父, "耐久", 堆叠.当前耐久 <= 0 ? "损坏" : $"{堆叠.当前耐久}/{耐久上限}", 29f, TextAlignmentOptions.Bottom);
            耐.color = 堆叠.当前耐久 <= 0 ? new Color(1f, 0.5f, 0.4f) : new Color(0.92f, 0.92f, 0.92f);
            var 耐矩 = 耐.rectTransform;
            耐矩.anchorMin = new Vector2(0, 0);
            耐矩.anchorMax = new Vector2(1, 0);
            耐矩.pivot = new Vector2(0.5f, 0);
            耐矩.anchoredPosition = new Vector2(0, 2f);
            耐矩.sizeDelta = new Vector2(-8f, 26f);
            框.耐久 = 耐;
            框.上次耐久 = 耐.text;
        }
        var 数 = UI工具.创建文本(父, "数量", 堆叠.数量.ToString(), 40f, TextAlignmentOptions.TopRight);
        UI工具.设锚(数.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-5f, -2f), new Vector2(70f, 34f));
        数.gameObject.SetActive(堆叠.数量 > 1);
        框.数量 = 数;
        框.上次数量 = 堆叠.数量;
    }

    // 更新已有物品框（增量）：位置/尺寸/旋转/品质色/数量/耐久 变化才改；不重建任何组件
    private void 更新物品框(物品框 框, 物品堆叠 堆叠)
    {
        if (框 == null || 框.根 == null) return;
        var (宽, 高) = 服务.物品占格(堆叠);
        框.根.anchoredPosition = new Vector2(格x(堆叠.列, 堆叠.行), -堆叠.行 * 格尺寸);
        float 新宽 = 宽 * 格尺寸, 新高 = 高 * 格尺寸;
        if (Mathf.Abs(框.根.sizeDelta.x - 新宽) > 0.01f || Mathf.Abs(框.根.sizeDelta.y - 新高) > 0.01f)
            框.根.sizeDelta = new Vector2(新宽, 新高);
        if (框.内容层 != null)
        {
            var 旋转 = Quaternion.Euler(0f, 0f, 堆叠.旋转 ? 90f : 0f);
            if (框.内容层.localRotation != 旋转) 框.内容层.localRotation = 旋转;
        }
        if (框.框图 != null)
        {
            var 色 = 品质底层色(堆叠);
            if (框.框图.color != 色)
			框.框图.color = 色;
        }
        if (框.数量 != null)
        {
            if (堆叠.数量 > 1)
            {
                if (!框.数量.gameObject.activeSelf)
				框.数量.gameObject.SetActive(true);
                if (框.上次数量 != 堆叠.数量)
                {
                    框.数量.text = 堆叠.数量.ToString();
                    框.上次数量 = 堆叠.数量;
                }
            }
            else if (框.数量.gameObject.activeSelf)
            {
                框.数量.gameObject.SetActive(false);
                框.上次数量 = 0;
            }
        }
        if (框.耐久 != null)
        {
            int 上限 = 档案.有效最大耐久(堆叠.标识);
            if (上限 > 0)
            {
                string 文本 = 堆叠.当前耐久 <= 0 ? "损坏" : $"{堆叠.当前耐久}/{上限}";
                if (框.上次耐久 != 文本)
                {
                    框.耐久.text = 文本;
                    框.上次耐久 = 文本;
                }
                if (!框.耐久.gameObject.activeSelf)
					框.耐久.gameObject.SetActive(true);
                框.耐久.color = 堆叠.当前耐久 <= 0 ? new Color(1f, 0.5f, 0.4f) : new Color(0.92f, 0.92f, 0.92f);
            }
            else if (框.耐久.gameObject.activeSelf)
            {
                框.耐久.gameObject.SetActive(false);
                框.上次耐久 = null;
            }
        }
    }

    // ===== 物品 品质 色（物品框 渲染 专用） =====

    // 品质底层色（物品框）：全部物品按有效品质整块着色（半透明）；普通 = 纯白 A20
    private Color 品质底层色(物品堆叠 堆叠)
    {
        if (堆叠 == null || !数据.物品.TryGetValue(堆叠.标识, out var 物品)) return new Color(0f, 0f, 0f, 0f);
        品质 档 = 有效品质(堆叠, 物品);
        if (档 == 品质.普通) return new Color(0f, 0f, 0f, 0f);
        var 色 = Color.Lerp(网格面板配色.物品底色, 品质工具.颜色(档), 0.55f);
        色.a = 网格面板配色.品质底色透明;
        return 色;
    }

    // 拖拽代理底色：非普通 = 品质底层色；普通（纯白 A20 太淡）→ 内容层色（不透明，跟手可见）
    private Color 物品品质底(物品堆叠 堆叠)
    {
        var 层色 = 品质底层色(堆叠);
        return 层色.a <= 0.1f ? 网格面板配色.物品底色 : 层色;
    }

    // 有效品质：堆叠品质覆盖（合成提升）优先，否则取物品模板品质
    private static 品质 有效品质(物品堆叠 堆叠, 物品数据 模板)
        => !string.IsNullOrEmpty(堆叠.品质) ? 数据解析.枚举<品质>(堆叠.品质) : 模板.品质档;
}
