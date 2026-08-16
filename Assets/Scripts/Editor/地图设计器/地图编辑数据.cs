using System.IO;
using UnityEditor;
using UnityEngine;

// 地图编辑数据：地图设计器的内存副本。持有 地图根，支持三层：大地图 / 城镇小地图 / 节点内部（"内部:<城镇>:<节点>"）。
// 内部归 地图节点.内部 所有（任何节点可挂 NPC+功能物 图）。
public sealed class 地图编辑数据
{
    public const string 文件路径 = "Assets/Resources/Data/map.json";

    public static 地图编辑数据 实例 { get; set; }

    public 地图根 数据 { get; private set; }
    public bool 有未保存修改 { get; set; }
    private readonly string 路径;

    // —— 编辑状态 ——
    public string 当前层 { get; set; } = "大地图";   // "大地图" / 城镇标识 / "内部:<城镇>:<节点>"
    public string 选中标识 { get; set; } = "";
    public bool 连接模式 { get; set; }
    public string 连接起点 { get; set; } = "";

    public 地图编辑数据() : this(文件路径) { }
    public 地图编辑数据(string 自定义路径)
    {
        路径 = 自定义路径;
        Load();
        实例 = this;
    }

    // 从 map.json 读取；文件不存在则建空根
    public void Load()
    {
        数据 = File.Exists(路径)
            ? JsonUtility.FromJson<地图根>(File.ReadAllText(路径))
            : new 地图根 { 地点 = new 地图地点[0] };
        有未保存修改 = false;
    }

    // 保存：备份 + 写回 + 刷新
    public void Save()
    {
        if (File.Exists(路径)) File.Copy(路径, 路径 + ".bak", true);
        File.WriteAllText(路径, JsonUtility.ToJson(数据, true));
        AssetDatabase.Refresh();
        有未保存修改 = false;
    }

    // 0-100 → 画布内 anchoredPosition（与运行时 地图渲染.归一化 同公式）
    public static Vector2 坐标到像素(RectTransform 画布, float x, float y)
    {
        var 尺寸 = 画布.rect.size;
        return new Vector2((x / 100f - 0.5f) * 尺寸.x, (y / 100f - 0.5f) * 尺寸.y);
    }

    // 画布内像素 → 0-100（逆换算）
    public static Vector2 像素到坐标(RectTransform 画布, Vector2 像素)
    {
        var 尺寸 = 画布.rect.size;
        return new Vector2((像素.x / 尺寸.x + 0.5f) * 100f, (像素.y / 尺寸.y + 0.5f) * 100f);
    }

    // 大地图层 → 全部地点数组
    public 地图地点[] 当前地点列表 => 数据.地点;

    // 小地图层 → 对应城镇地点；非小地图层返回 null
    public 地图地点 当前城镇 =>
        当前层 == "大地图" || 当前层.StartsWith("内部:") ? null : System.Array.Find(数据.地点, p => p.标识 == 当前层);

    // 内部层 → 当前正在编辑内部的 地图节点
    public 地图节点 当前内部节点()
    {
        if (!当前层.StartsWith("内部:")) return null;
        var 部分 = 当前层.Substring(3).Split(':');
        if (部分.Length < 2) return null;
        var 镇 = System.Array.Find(数据.地点, p => p.标识 == 部分[0]);
        if (镇?.小地图 == null) return null;
        return System.Array.Find(镇.小地图, n => n.标识 == 部分[1]);
    }

    // 新增节点：大地图=地点；小地图=节点；内部=内部节点
    public void 新增节点()
    {
        if (当前层 == "大地图")
        {
            var 列表 = new System.Collections.Generic.List<地图地点>(数据.地点 ?? new 地图地点[0])
            { new 地图地点 { 标识 = "新地点", 名称 = "新地点", x = 50, y = 50 } };
            数据.地点 = 列表.ToArray();
            选中标识 = "新地点";
        }
        else if (当前层.StartsWith("内部:"))
        {
            var 节点 = 当前内部节点();
            if (节点 == null) return;
            var 列表 = new System.Collections.Generic.List<设施内部节点>(节点.内部 ?? new 设施内部节点[0])
            { new 设施内部节点 { 标识 = "新节点", 名称 = "新节点", 类型 = "NPC", x = 50, y = 50 } };
            节点.内部 = 列表.ToArray();
            选中标识 = "新节点";
        }
        else
        {
            var 镇 = 当前城镇;
            if (镇 == null) return;
            var 列表 = new System.Collections.Generic.List<地图节点>(镇.小地图 ?? new 地图节点[0])
            { new 地图节点 { 标识 = "新节点", 名称 = "新节点", 类型 = "空", x = 50, y = 50 } };
            镇.小地图 = 列表.ToArray();
            选中标识 = "新节点";
        }
        有未保存修改 = true;
    }

    // 删除选中节点：从层数组移除 + 清理其它节点连接里的引用
    public void 删除选中()
    {
        if (当前层 == "大地图")
        {
            var 列表 = new System.Collections.Generic.List<地图地点>(数据.地点 ?? new 地图地点[0]);
            if (列表.RemoveAll(p => p.标识 == 选中标识) == 0) return;
            数据.地点 = 列表.ToArray();
            foreach (var 地点 in 数据.地点) 地点.连接 = 移除(地点.连接, 选中标识);
        }
        else if (当前层.StartsWith("内部:"))
        {
            var 节点 = 当前内部节点();
            if (节点 == null) return;
            var 列表 = new System.Collections.Generic.List<设施内部节点>(节点.内部 ?? new 设施内部节点[0]);
            if (列表.RemoveAll(n => n.标识 == 选中标识) == 0) return;
            节点.内部 = 列表.ToArray();
            foreach (var n in 节点.内部) n.连接 = 移除(n.连接, 选中标识);
        }
        else
        {
            var 镇 = 当前城镇;
            if (镇 == null) return;
            var 列表 = new System.Collections.Generic.List<地图节点>(镇.小地图 ?? new 地图节点[0]);
            if (列表.RemoveAll(p => p.标识 == 选中标识) == 0) return;
            镇.小地图 = 列表.ToArray();
            foreach (var 节点 in 镇.小地图) 节点.连接 = 移除(节点.连接, 选中标识);
        }
        选中标识 = "";
        有未保存修改 = true;
    }

    // 建立连接：A、B 连接数组互加、去重（双向）
    public void 建立连接(string a, string b)
    {
        if (a == b) return;
        加连接(a, b);
        加连接(b, a);
        有未保存修改 = true;
    }

    // 断开连接
    public void 断开连接(string a, string b)
    {
        移除当前层(a, b);
        移除当前层(b, a);
        有未保存修改 = true;
    }

    // 连接模式点击
    public void 处理连接点击(string 标识)
    {
        if (string.IsNullOrEmpty(连接起点)) { 连接起点 = 标识; return; }
        建立连接(连接起点, 标识);
        连接起点 = "";
    }

    // —— 图编辑器查询/写入（窗口画布用，三层通用）——

    public string[] 当前层标识()
    {
        if (当前层 == "大地图")
            return System.Array.ConvertAll(数据.地点 ?? new 地图地点[0], p => p.标识);
        if (当前层.StartsWith("内部:"))
        {
            var 节点 = 当前内部节点();
            return 节点?.内部 == null ? new string[0] : System.Array.ConvertAll(节点.内部, n => n.标识);
        }
        var 镇 = 当前城镇;
        return 镇?.小地图 == null ? new string[0] : System.Array.ConvertAll(镇.小地图, n => n.标识);
    }

    public string 节点名称(string 标识)
    {
        if (当前层 == "大地图")
        {
            var 地点 = System.Array.Find(数据.地点, p => p.标识 == 标识);
            return 地点?.名称 ?? 标识;
        }
        if (当前层.StartsWith("内部:"))
        {
            var 节点 = 内部节点(标识);
            return 节点?.名称 ?? 标识;
        }
        var 镇 = 当前城镇;
        if (镇?.小地图 == null) return 标识;
        var 小节点 = System.Array.Find(镇.小地图, n => n.标识 == 标识);
        return 小节点?.名称 ?? 标识;
    }

    public Vector2 节点坐标(string 标识)
    {
        if (当前层 == "大地图")
        {
            var 地点 = System.Array.Find(数据.地点, p => p.标识 == 标识);
            return 地点 != null ? new Vector2(地点.x, 地点.y) : Vector2.zero;
        }
        if (当前层.StartsWith("内部:"))
        {
            var 节点 = 内部节点(标识);
            return 节点 != null ? new Vector2(节点.x, 节点.y) : Vector2.zero;
        }
        var 镇 = 当前城镇;
        if (镇?.小地图 == null) return Vector2.zero;
        var 小节点 = System.Array.Find(镇.小地图, n => n.标识 == 标识);
        return 小节点 != null ? new Vector2(小节点.x, 小节点.y) : Vector2.zero;
    }

    public void 设置节点坐标(string 标识, Vector2 坐标)
    {
        var 钳制 = new Vector2(Mathf.Clamp(坐标.x, 0f, 100f), Mathf.Clamp(坐标.y, 0f, 100f));
        if (当前层 == "大地图")
        {
            var 地点 = System.Array.Find(数据.地点, p => p.标识 == 标识);
            if (地点 == null) return;
            地点.x = 钳制.x; 地点.y = 钳制.y;
        }
        else if (当前层.StartsWith("内部:"))
        {
            var 节点 = 内部节点(标识);
            if (节点 == null) return;
            节点.x = 钳制.x; 节点.y = 钳制.y;
        }
        else
        {
            var 镇 = 当前城镇;
            if (镇?.小地图 == null) return;
            var 小节点 = System.Array.Find(镇.小地图, n => n.标识 == 标识);
            if (小节点 == null) return;
            小节点.x = 钳制.x; 小节点.y = 钳制.y;
        }
        有未保存修改 = true;
    }

    public string[] 节点连接(string 标识)
    {
        if (当前层 == "大地图")
        {
            var 地点 = System.Array.Find(数据.地点, p => p.标识 == 标识);
            return 地点?.连接 ?? new string[0];
        }
        if (当前层.StartsWith("内部:"))
        {
            var 节点 = 内部节点(标识);
            return 节点?.连接 ?? new string[0];
        }
        var 镇 = 当前城镇;
        if (镇?.小地图 == null) return new string[0];
        var 小节点 = System.Array.Find(镇.小地图, n => n.标识 == 标识);
        return 小节点?.连接 ?? new string[0];
    }

    // 取当前内部层的一个 设施内部节点（属性表单用）
    public 设施内部节点 内部节点(string 标识)
    {
        var 节点 = 当前内部节点();
        if (节点?.内部 == null) return null;
        return System.Array.Find(节点.内部, n => n.标识 == 标识);
    }

    // —— 私有工具 ——

    private void 加连接(string 谁, string 目标)
    {
        var 列表 = new System.Collections.Generic.List<string>(当前层连接(谁) ?? new string[0]);
        if (!列表.Contains(目标)) 列表.Add(目标);
        设当前层连接(谁, 列表.ToArray());
    }

    private void 移除当前层(string 谁, string 目标)
    {
        var 列表 = new System.Collections.Generic.List<string>(当前层连接(谁) ?? new string[0]);
        列表.Remove(目标);
        设当前层连接(谁, 列表.ToArray());
    }

    private string[] 当前层连接(string 标识)
    {
        if (当前层 == "大地图")
        {
            var 地点 = System.Array.Find(数据.地点, p => p.标识 == 标识);
            return 地点?.连接;
        }
        if (当前层.StartsWith("内部:"))
        {
            var 节点 = 内部节点(标识);
            return 节点?.连接;
        }
        var 镇 = 当前城镇;
        if (镇 == null || 镇.小地图 == null) return null;
        var 小节点 = System.Array.Find(镇.小地图, n => n.标识 == 标识);
        return 小节点?.连接;
    }

    private void 设当前层连接(string 标识, string[] 连接)
    {
        if (当前层 == "大地图")
        {
            var 地点 = System.Array.Find(数据.地点, p => p.标识 == 标识);
            if (地点 != null) 地点.连接 = 连接;
            return;
        }
        if (当前层.StartsWith("内部:"))
        {
            var 节点 = 内部节点(标识);
            if (节点 != null) 节点.连接 = 连接;
            return;
        }
        var 镇 = 当前城镇;
        if (镇 == null || 镇.小地图 == null) return;
        var 小节点 = System.Array.Find(镇.小地图, n => n.标识 == 标识);
        if (小节点 != null) 小节点.连接 = 连接;
    }

    // 从连接数组移除指定标识
    private static string[] 移除(string[] 数组, string 标识)
    {
        var 列表 = new System.Collections.Generic.List<string>(数组 ?? new string[0]);
        列表.Remove(标识);
        return 列表.ToArray();
    }
}
