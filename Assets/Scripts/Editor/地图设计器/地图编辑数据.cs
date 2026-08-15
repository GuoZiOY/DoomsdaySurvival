using System.IO;
using UnityEditor;
using UnityEngine;

// 地图编辑数据：地图设计器的内存副本。持有 地图根，负责 加载/保存(自动备份)/坐标换算/增删节点/连接操作。
public sealed class 地图编辑数据
{
    public const string 文件路径 = "Assets/Resources/Data/map.json";

    public static 地图编辑数据 实例 { get; set; }           // 窗口打开时 new 赋值，场景绘制/窗口共用

    public 地图根 数据 { get; private set; }                // 内存副本（编辑全改它，保存才写文件）
    public bool 有未保存修改 { get; set; }
    private readonly string 路径;                           // 实际读写路径（测试可注入临时路径，避免污染真实 map.json）

    // —— 编辑状态 ——
    public string 当前层 { get; set; } = "大地图";           // "大地图" 或 城镇标识
    public string 选中标识 { get; set; } = "";
    public bool 连接模式 { get; set; }                      // true=点两个节点连线
    public string 连接起点 { get; set; } = "";              // 连接模式下第一次点选的节点

    public 地图编辑数据() : this(文件路径) { }
    public 地图编辑数据(string 自定义路径)
    {
        路径 = 自定义路径;
        Load();
        实例 = this;
    }

    // 从 JSON 读取；文件不存在则建空根
    public void Load()
    {
        数据 = File.Exists(路径)
            ? JsonUtility.FromJson<地图根>(File.ReadAllText(路径))
            : new 地图根 { 地点 = new 地图地点[0] };
        有未保存修改 = false;
    }

    // 保存：旧文件复制成 .bak，再覆盖写回，刷新资产
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

    // 小地图层 → 对应城镇地点；大地图层返回 null
    public 地图地点 当前城镇 =>
        当前层 == "大地图" ? null : System.Array.Find(数据.地点, p => p.标识 == 当前层);

    // 新增节点：大地图加 地图地点；小地图给当前城镇加 地图节点
    public void 新增节点()
    {
        if (当前层 == "大地图")
        {
            var 列表 = new System.Collections.Generic.List<地图地点>(数据.地点 ?? new 地图地点[0])
            { new 地图地点 { 标识 = "新地点", 名称 = "新地点", x = 50, y = 50 } };
            数据.地点 = 列表.ToArray();
            选中标识 = "新地点";
        }
        else
        {
            var 镇 = 当前城镇;
            if (镇 == null) return;
            var 列表 = new System.Collections.Generic.List<地图节点>(镇.小地图 ?? new 地图节点[0])
            { new 地图节点 { 标识 = "新节点", 名称 = "新节点", 类型 = "空地", x = 50, y = 50 } };
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

    // 断开连接：从双方连接数组移除
    public void 断开连接(string a, string b)
    {
        移除当前层(a, b);
        移除当前层(b, a);
        有未保存修改 = true;
    }

    // 连接模式点击：第一次存起点，第二次建立连接并复位
    public void 处理连接点击(string 标识)
    {
        if (string.IsNullOrEmpty(连接起点)) { 连接起点 = 标识; return; }
        建立连接(连接起点, 标识);
        连接起点 = "";
    }

    // —— 图编辑器查询/写入（窗口画布用）——

    // 当前层所有节点标识
    public string[] 当前层标识()
    {
        if (当前层 == "大地图")
            return System.Array.ConvertAll(数据.地点 ?? new 地图地点[0], p => p.标识);
        var 镇 = 当前城镇;
        return 镇?.小地图 == null ? new string[0] : System.Array.ConvertAll(镇.小地图, n => n.标识);
    }

    // 节点名称
    public string 节点名称(string 标识)
    {
        if (当前层 == "大地图")
        {
            var 地点 = System.Array.Find(数据.地点, p => p.标识 == 标识);
            return 地点?.名称 ?? 标识;
        }
        var 镇 = 当前城镇;
        if (镇?.小地图 == null) return 标识;
        var 节点 = System.Array.Find(镇.小地图, n => n.标识 == 标识);
        return 节点?.名称 ?? 标识;
    }

    // 节点坐标
    public Vector2 节点坐标(string 标识)
    {
        if (当前层 == "大地图")
        {
            var 地点 = System.Array.Find(数据.地点, p => p.标识 == 标识);
            return 地点 != null ? new Vector2(地点.x, 地点.y) : Vector2.zero;
        }
        var 镇 = 当前城镇;
        if (镇?.小地图 == null) return Vector2.zero;
        var 节点 = System.Array.Find(镇.小地图, n => n.标识 == 标识);
        return 节点 != null ? new Vector2(节点.x, 节点.y) : Vector2.zero;
    }

    // 设置节点坐标（钳制 0-100，标记未保存）
    public void 设置节点坐标(string 标识, Vector2 坐标)
    {
        var 钳制 = new Vector2(Mathf.Clamp(坐标.x, 0f, 100f), Mathf.Clamp(坐标.y, 0f, 100f));
        if (当前层 == "大地图")
        {
            var 地点 = System.Array.Find(数据.地点, p => p.标识 == 标识);
            if (地点 == null) return;
            地点.x = 钳制.x; 地点.y = 钳制.y;
        }
        else
        {
            var 镇 = 当前城镇;
            if (镇?.小地图 == null) return;
            var 节点 = System.Array.Find(镇.小地图, n => n.标识 == 标识);
            if (节点 == null) return;
            节点.x = 钳制.x; 节点.y = 钳制.y;
        }
        有未保存修改 = true;
    }

    // 节点连接数组
    public string[] 节点连接(string 标识)
    {
        if (当前层 == "大地图")
        {
            var 地点 = System.Array.Find(数据.地点, p => p.标识 == 标识);
            return 地点?.连接 ?? new string[0];
        }
        var 镇 = 当前城镇;
        if (镇?.小地图 == null) return new string[0];
        var 节点 = System.Array.Find(镇.小地图, n => n.标识 == 标识);
        return 节点?.连接 ?? new string[0];
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
        var 镇 = 当前城镇;
        if (镇 == null || 镇.小地图 == null) return null;
        var 节点 = System.Array.Find(镇.小地图, n => n.标识 == 标识);
        return 节点?.连接;
    }

    private void 设当前层连接(string 标识, string[] 连接)
    {
        if (当前层 == "大地图")
        {
            var 地点 = System.Array.Find(数据.地点, p => p.标识 == 标识);
            if (地点 != null) 地点.连接 = 连接;
            return;
        }
        var 镇 = 当前城镇;
        if (镇 == null || 镇.小地图 == null) return;
        var 节点 = System.Array.Find(镇.小地图, n => n.标识 == 标识);
        if (节点 != null) 节点.连接 = 连接;
    }

    // 从连接数组移除指定标识
    private static string[] 移除(string[] 数组, string 标识)
    {
        var 列表 = new System.Collections.Generic.List<string>(数组 ?? new string[0]);
        列表.Remove(标识);
        return 列表.ToArray();
    }
}
