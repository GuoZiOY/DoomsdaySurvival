using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 地图设计器窗口：真正的可视化节点图编辑器。画布内完整显示地图，直接拖拽节点改位置、连线、缩放平移、改属性、增删、保存。
public sealed class 地图设计器窗口 : EditorWindow
{
    private string[] 层选项;
    private Vector2 滚动;

    // —— 画布视图状态 ——
    private float 缩放 = 1f;          // 滚轮缩放
    private Vector2 偏移;             // 拖空白平移
    private string 拖动节点;          // 正在拖动的节点标识（null=没在拖节点）
    private bool 平移中;              // 空白拖拽平移中
    private Vector2 拖动起点;         // 鼠标按下位置
    private Vector2 平移前偏移;       // 平移前的偏移

    private const float 节点宽 = 110f, 节点高 = 46f;

    [MenuItem("窗口/地图设计器")]
    public static void 打开() => GetWindow<地图设计器窗口>("地图设计器");

    void OnEnable()
    {
        // 顶部工具栏需要足够横向空间：窗口默认开宽些，且可自由拉拽调宽
        minSize = new Vector2(760f, 460f);
        地图编辑数据.实例 = new 地图编辑数据();
    }

    void OnDisable()
    {
        var 数据 = 地图编辑数据.实例;
        if (数据 != null && 数据.有未保存修改)
            if (EditorUtility.DisplayDialog("地图设计器", "有未保存的修改，退出前保存到 map.json？", "保存", "放弃"))
                数据.Save();
    }

    void OnGUI()
    {
        var 数据 = 地图编辑数据.实例;
        if (数据 == null) { 数据 = new 地图编辑数据(); }
        地图编辑数据.实例 = 数据;

        工具栏(数据);
        EditorGUILayout.Space();
        属性表单(数据);
        EditorGUILayout.Space();

        // —— 图区：占满剩余空间 ——
        var 区域 = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(区域, new Color(0.09f, 0.09f, 0.11f));
        画网格(区域);
        画连接(区域, 数据);
        画节点(区域, 数据);
        处理画布输入(区域, 数据);

        EditorGUILayout.LabelField("左键拖节点移动 · 滚轮缩放 · 拖空白平移 · 连接模式下点两节点连线 · 右键节点删除", EditorStyles.centeredGreyMiniLabel);
    }

    // —— 顶部：层下拉 + 连接模式 + 增删保存 ——
    private void 工具栏(地图编辑数据 数据)
    {
        EditorGUILayout.BeginHorizontal();
        刷新层选项(数据);
        var 当前索引 = System.Array.IndexOf(层选项, 数据.当前层);
        var 选 = EditorGUILayout.Popup("编辑层", Mathf.Max(0, 当前索引), 层选项, GUILayout.MinWidth(160));
        if (选 >= 0 && 层选项[选] != 数据.当前层)
        {
            数据.当前层 = 层选项[选];
            数据.选中标识 = "";
            数据.连接起点 = "";
            缩放 = 1f; 偏移 = Vector2.zero;
        }
        数据.连接模式 = EditorGUILayout.ToggleLeft("连接模式", 数据.连接模式, GUILayout.Width(90));
        if (GUILayout.Button("新增节点")) 数据.新增节点();
        if (GUILayout.Button("删除选中")) 数据.删除选中();
        GUILayout.FlexibleSpace();
        GUI.enabled = 数据.有未保存修改;
        if (GUILayout.Button("保存到 map.json")) 数据.Save();
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    // —— 选中节点的属性表单 ——
    private void 属性表单(地图编辑数据 数据)
    {
        if (string.IsNullOrEmpty(数据.选中标识)) return;
        EditorGUILayout.LabelField($"—— {数据.节点名称(数据.选中标识)} 属性 ——", EditorStyles.boldLabel);
        滚动 = EditorGUILayout.BeginScrollView(滚动, GUILayout.MaxHeight(160));
        EditorGUI.BeginChangeCheck();
        if (数据.当前层 == "大地图")
        {
            var 地点 = System.Array.Find(数据.当前地点列表, p => p.标识 == 数据.选中标识);
            if (地点 == null) return;
            地点.名称 = EditorGUILayout.TextField("名称", 地点.名称);
            地点.类型 = EditorGUILayout.TextField("类型(城镇/荒野)", 地点.类型);
            地点.描述 = EditorGUILayout.TextField("描述", 地点.描述);
            地点.入口节点 = EditorGUILayout.TextField("入口节点(城镇)", 地点.入口节点);
            地点.区域 = EditorGUILayout.TextField("区域(荒野)", 地点.区域);
            地点.目标 = EditorGUILayout.TextField("目标", 地点.目标);
            地点.解锁物品 = EditorGUILayout.TextField("解锁物品", 地点.解锁物品);
            连接编辑(地点.连接, 数据);
        }
        else
        {
            var 镇 = 数据.当前城镇;
            if (镇?.小地图 == null) return;
            var 节点 = System.Array.Find(镇.小地图, n => n.标识 == 数据.选中标识);
            if (节点 == null) return;
            节点.名称 = EditorGUILayout.TextField("名称", 节点.名称);
            节点.类型 = EditorGUILayout.TextField("类型(入口/设施/剧情/空地)", 节点.类型);
            if (节点.类型 == "设施") 节点.设施 = EditorGUILayout.TextField("设施标识", 节点.设施);
            if (节点.类型 == "剧情") 节点.目标 = EditorGUILayout.TextField("剧情目标", 节点.目标);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("位置");
            节点.x = EditorGUILayout.FloatField(节点.x);
            节点.y = EditorGUILayout.FloatField(节点.y);
            EditorGUILayout.EndHorizontal();
            连接编辑(节点.连接, 数据);
        }
        if (EditorGUI.EndChangeCheck()) 数据.有未保存修改 = true;
        EditorGUILayout.EndScrollView();
    }

    // 连接多选：勾选即建/断连接
    private void 连接编辑(string[] 当前连接, 地图编辑数据 数据)
    {
        foreach (var 候选 in 数据.当前层标识())
        {
            if (候选 == 数据.选中标识) continue;
            bool 已连 = System.Array.IndexOf(当前连接 ?? new string[0], 候选) >= 0;
            bool 新 = EditorGUILayout.Toggle("连接 " + 候选, 已连);
            if (新 != 已连) { if (新) 数据.建立连接(数据.选中标识, 候选); else 数据.断开连接(数据.选中标识, 候选); }
        }
    }

    private void 刷新层选项(地图编辑数据 数据)
    {
        var 列表 = new List<string> { "大地图" };
        foreach (var 地点 in 数据.数据.地点)
            if (地点.类型 == "城镇") 列表.Add(地点.标识);
        层选项 = 列表.ToArray();
    }

    // —— 画布坐标换算（0-100 ↔ 窗口像素）——
    private float 基准比例(Rect 区域) => Mathf.Min(区域.width, 区域.height) / 100f * 0.8f;
    private Vector2 坐标到屏幕(Rect 区域, Vector2 坐标)
    {
        var 比例 = 缩放 * 基准比例(区域);
        return 区域.center + new Vector2((坐标.x - 50f) * 比例, (坐标.y - 50f) * 比例) + 偏移;
    }
    private Vector2 屏幕到坐标(Rect 区域, Vector2 屏幕)
    {
        var 比例 = 缩放 * 基准比例(区域);
        return new Vector2(50f + (屏幕.x - 区域.center.x - 偏移.x) / 比例, 50f + (屏幕.y - 区域.center.y - 偏移.y) / 比例);
    }

    // 网格背景（每 10 单位一条线）
    private void 画网格(Rect 区域)
    {
        var 网格色 = new Color(0.14f, 0.14f, 0.17f);
        for (int i = 0; i <= 10; i++)
        {
            var 纵 = 坐标到屏幕(区域, new Vector2(i * 10f, 0f));
            var 横 = 坐标到屏幕(区域, new Vector2(0f, i * 10f));
            EditorGUI.DrawRect(new Rect(纵.x - 0.5f, 区域.y, 1f, 区域.height), 网格色);
            EditorGUI.DrawRect(new Rect(区域.x, 横.y - 0.5f, 区域.width, 1f), 网格色);
        }
    }

    // 画连接线（去重：只在标识较大方向画一次）
    private void 画连接(Rect 区域, 地图编辑数据 数据)
    {
        Handles.color = new Color(0.55f, 0.5f, 0.4f);
        foreach (var 标识 in 数据.当前层标识())
        {
            var a = 坐标到屏幕(区域, 数据.节点坐标(标识));
            foreach (var 邻 in 数据.节点连接(标识))
            {
                if (string.CompareOrdinal(标识, 邻) > 0) continue;
                var b = 坐标到屏幕(区域, 数据.节点坐标(邻));
                Handles.DrawLine(a, b);
            }
        }
    }

    // 画节点（选中钢蓝/连接模式黄/其余金色边框）
    private void 画节点(Rect 区域, 地图编辑数据 数据)
    {
        foreach (var 标识 in 数据.当前层标识())
        {
            var 中心 = 坐标到屏幕(区域, 数据.节点坐标(标识));
            var 矩形 = new Rect(中心.x - 节点宽 / 2, 中心.y - 节点高 / 2, 节点宽, 节点高);
            var 边框色 = 标识 == 数据.选中标识 ? 游戏主题.选中色
                : 数据.连接模式 ? new Color(1f, 0.9f, 0.3f)
                : 游戏主题.金色;
            EditorGUI.DrawRect(矩形, 边框色);
            var 内 = new Rect(矩形.x + 2, 矩形.y + 2, 矩形.width - 4, 矩形.height - 4);
            EditorGUI.DrawRect(内, new Color(0.16f, 0.16f, 0.19f));
            GUI.Label(内, 数据.节点名称(标识), 居中样式);
        }
    }

    private static GUIStyle 居中样式
    {
        get
        {
            var 样式 = new GUIStyle(EditorStyles.label);
            样式.alignment = TextAnchor.MiddleCenter;
            样式.wordWrap = true;
            样式.clipping = TextClipping.Clip;
            return 样式;
        }
    }

    // 命中节点：返回鼠标所在节点标识；不在任何节点上返回 null
    private string 命中节点(Rect 区域, 地图编辑数据 数据, Vector2 鼠标)
    {
        foreach (var 标识 in 数据.当前层标识())
        {
            var 中心 = 坐标到屏幕(区域, 数据.节点坐标(标识));
            if (Mathf.Abs(鼠标.x - 中心.x) <= 节点宽 / 2 && Mathf.Abs(鼠标.y - 中心.y) <= 节点高 / 2)
                return 标识;
        }
        return null;
    }

    // —— 画布输入：拖节点 / 拖空白平移 / 滚轮缩放 / 连接模式点连 / 右键删除 ——
    private void 处理画布输入(Rect 区域, 地图编辑数据 数据)
    {
        var 事件 = Event.current;
        if (!区域.Contains(事件.mousePosition)) return;

        if (事件.type == EventType.MouseDown && 事件.button == 0)
        {
            var 命中 = 命中节点(区域, 数据, 事件.mousePosition);
            if (命中 != null)
            {
                if (数据.连接模式) { 数据.处理连接点击(命中); 事件.Use(); return; }
                数据.选中标识 = 命中;
                拖动节点 = 命中;
                平移中 = false;
            }
            else
            {
                // 空白：取消选中 + 准备平移
                if (!数据.连接模式) 数据.选中标识 = "";
                拖动节点 = null;
                平移中 = true;
                拖动起点 = 事件.mousePosition;
                平移前偏移 = 偏移;
            }
            事件.Use();
        }
        else if (事件.type == EventType.MouseDrag)
        {
            if (拖动节点 != null)
            {
                数据.设置节点坐标(拖动节点, 屏幕到坐标(区域, 事件.mousePosition));
                事件.Use();
            }
            else if (平移中)
            {
                偏移 = 平移前偏移 + 事件.mousePosition - 拖动起点;
                事件.Use();
            }
        }
        else if (事件.type == EventType.MouseUp)
        {
            拖动节点 = null;
            平移中 = false;
            // 点击(未拖动)空白也响应
            事件.Use();
        }
        else if (事件.type == EventType.ScrollWheel)
        {
            var 旧缩放 = 缩放;
            缩放 = Mathf.Clamp(缩放 * (1f - 事件.delta.y * 0.02f), 0.3f, 5f);
            // 以光标为中心缩放
            var 光标 = 事件.mousePosition;
            偏移 = 光标 - 区域.center - (光标 - 区域.center - 偏移) * (缩放 / 旧缩放);
            事件.Use();
        }
        else if (事件.type == EventType.MouseDown && 事件.button == 1)
        {
            var 命中 = 命中节点(区域, 数据, 事件.mousePosition);
            if (命中 != null)
            {
                var 菜单 = new GenericMenu();
                菜单.AddItem(new GUIContent("删除节点"), false, () => { 数据.选中标识 = 命中; 数据.删除选中(); });
                菜单.AddItem(new GUIContent("清除该节点连接"), false, () => { 数据.选中标识 = 命中; 清空连接(数据, 命中); });
                菜单.ShowAsContext();
                事件.Use();
            }
        }
    }

    // 清除某节点的全部连接
    private void 清空连接(地图编辑数据 数据, string 标识)
    {
        foreach (var 对方 in 数据.当前层标识())
            if (对方 != 标识) 数据.断开连接(标识, 对方);
    }
}
