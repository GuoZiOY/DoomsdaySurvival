using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 小菜单工具：按需实例化「一行菜单条目」——调用方不需要手动引用任何按钮。
// 三级回落（连预制体都没有也能用）：
//   ① 调用方给的「菜单按钮」预制体（外观你做主）
//   ② 面板基类.按钮预制体（面板管理器 的共享按钮预制体）
//   ③ 代码兜底建行（Image + Button + TMP）
// 预制体里没有 Button 也能点：自动挂 菜单条目点击（IPointerClickHandler）。
public static class 小菜单工具
{
    public static Button 建条目(GameObject 预制体, RectTransform 父, string 文本, System.Action 点击,
        float 兜底宽 = 180f, float 兜底高 = 44f)
    {
        if (父 == null || 点击 == null) return null;

        if (预制体 != null)
        {
            var 物体 = Object.Instantiate(预制体, 父, false);
            物体.SetActive(true);
            var 字 = 物体.GetComponentInChildren<TMP_Text>();
            if (字 != null) 字.text = 文本;
            var 按钮 = 物体.GetComponent<Button>();
            if (按钮 == null) 按钮 = 物体.GetComponentInChildren<Button>();
            if (按钮 != null)
            {
                按钮.onClick.AddListener(() => 点击());
                音效管理器.实例?.注册按钮(按钮);
                return 按钮;
            }
            物体.AddComponent<菜单条目点击>().初始化(点击);
            return null;
        }

        if (面板基类.按钮预制体 != null)
        {
            面板基类.创建行(父, 文本, 点击, 移除布局: true, 文字居中: true);
            return null;
        }

        var 兜底物体 = new GameObject("菜单条目", typeof(RectTransform), typeof(Image), typeof(Button));
        兜底物体.transform.SetParent(父, false);
        var 矩形 = 兜底物体.GetComponent<RectTransform>();
        矩形.sizeDelta = new Vector2(兜底宽, 兜底高);
        var 图 = 兜底物体.GetComponent<Image>();
        图.color = 游戏主题.按钮底;
        var 文字物体 = new GameObject("文字", typeof(RectTransform), typeof(TextMeshProUGUI));
        文字物体.transform.SetParent(兜底物体.transform, false);
        var 文字矩形 = 文字物体.GetComponent<RectTransform>();
        文字矩形.anchorMin = Vector2.zero; 文字矩形.anchorMax = Vector2.one;
        文字矩形.offsetMin = new Vector2(8f, 0f); 文字矩形.offsetMax = new Vector2(-8f, 0f);
        var 兜底字 = 文字物体.GetComponent<TextMeshProUGUI>();
        兜底字.text = 文本;
        兜底字.color = 游戏主题.文字;
        兜底字.alignment = TextAlignmentOptions.Center;
        兜底字.fontSize = 24f;
        兜底字.raycastTarget = false;
        var 兜底按钮 = 兜底物体.GetComponent<Button>();
        兜底按钮.onClick.AddListener(() => 点击());
        音效管理器.实例?.注册按钮(兜底按钮);
        return 兜底按钮;
    }

    // 清空条目父级（每次重排菜单先清旧行）
    public static void 清条目(RectTransform 父)
    {
        if (父 == null) return;
        for (int i = 父.childCount - 1; i >= 0; i--) Object.Destroy(父.GetChild(i).gameObject);
    }
}

// 菜单条目点击兜底：预制体里没有 Button 时用（照样能点）
public sealed class 菜单条目点击 : MonoBehaviour, IPointerClickHandler
{
    private System.Action 点击;
    public void 初始化(System.Action 点击) { this.点击 = 点击; }
    public void OnPointerClick(PointerEventData 事件) => 点击?.Invoke();
}
