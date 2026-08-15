# 地图设计器（可视化地图编辑工具）实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 做一个纯编辑器工具，让用户在场景视图可视化设计大世界/城镇小地图（拖节点、连线、改属性、增删节点），一键反向导出 `map.json`。

**Architecture:** 三个编辑器组件围绕一个**内存副本**协作：`地图编辑数据`（加载/保存/坐标换算/连接操作，可单测）、`地图设计器窗口`（EditorWindow：层下拉/节点列表/属性表单/保存按钮）、`地图设计器场景绘制`（SceneView 把手拖拽/连线/右键删除）。运行时完全不变，仍读 `map.json`。

**Tech Stack:** Unity 6000.4.8f1 / C# / `UnityEditor` / `com.unity.test-framework 1.6.0`（EditMode 测试）

## Global Constraints

- 纯编辑器代码，放 `Assets/Scripts/Editor/地图设计器/`（该目录已存在且为空）
- **不生成任何运行时 GameObject**；节点只在 Scene View 用 Handles 画
- 编辑位置存 **0-100 归一化坐标**，与运行时 `地图渲染.归一化` 互逆
- 保存 = 覆盖 `map.json` + 旧文件复制成 `map.json.bak`（仅一份）
- 连接视为**双向**（A、B 的 `连接` 数组互加、去重）
- 编码风格：中文命名参数/函数/注释；行内 `//` 注释；不用 `/// <summary>`；不用 `_` 前缀
- 数据类复用 `Assets/Scripts/Data/数据模型.cs` 的 `地图根/地图地点/地图节点`（无 namespace）

---

### Task 1: 测试基础设施（EditMode 测试程序集）

**Files:**
- Create: `Assets/Tests/EditMode/地图设计器.Tests.asmdef`
- Create: `Assets/Tests/EditMode/地图设计器/冒烟测试.cs`

**Interfaces:**
- Produces: 测试程序集 `地图设计器.Tests`，供 Task 2/3 追加测试。主工程无 asmdef，测试程序集引用预定义程序集 `Assembly-CSharp-Editor`（`地图编辑数据.cs` 位于 Editor 目录，编译进它）。

- [ ] **Step 1: 建测试目录与 asmdef**

`Assets/Tests/EditMode/地图设计器.Tests.asmdef` 内容：

```json
{
  "name": "地图设计器.Tests",
  "references": ["Assembly-CSharp-Editor"],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

`Assets/Tests/EditMode/地图设计器/冒烟测试.cs` 内容：

```csharp
using NUnit.Framework;

// 冒烟测试：确认测试程序集能跑起来
public class 冒烟测试
{
    [Test]
    public void 测试框架可用()
    {
        Assert.That(1 + 1, Is.EqualTo(2));
    }
}
```

- [ ] **Step 2: 等 Unity 编译完成**（AssetDatabase 刷新）

Run: Unity 控制台确认无编译错误。若报 `Assembly-CSharp-Editor` 引用不到，改用 `"references": ["Assembly-CSharp", "Assembly-CSharp-Editor"]`。

- [ ] **Step 3: 跑一次 EditMode 测试确认通过**

Run: Unity Test Runner → EditMode → 地图设计器.Tests
Expected: 冒烟测试 PASS

- [ ] **Step 4: Commit**

```bash
git add Assets/Tests/EditMode/地图设计器.Tests.asmdef Assets/Tests/EditMode/地图设计器/冒烟测试.cs
git commit -m "test: 地图设计器 测试程序集脚手架"
```

---

### Task 2: 地图编辑数据 —— 加载 / 保存（自动备份）/ 坐标换算

**Files:**
- Create: `Assets/Scripts/Editor/地图设计器/地图编辑数据.cs`
- Test: `Assets/Tests/EditMode/地图设计器/地图编辑数据测试.cs`

**Interfaces:**
- Consumes: `数据模型.cs` 的 `地图根/地图地点/地图节点`
- Produces: `public sealed class 地图编辑数据`：
  - `public static 地图编辑数据 实例`（由构造函数赋值，窗口打开时 new）
  - `public 地图根 数据`（内存副本）
  - `public const string 文件路径 = "Assets/Resources/Data/map.json"`
  - `public void Load()` / `public void Save()`
  - `public static Vector2 坐标到像素(RectTransform 画布, float x, float y)`（= 运行时 `地图渲染.归一化` 公式）
  - `public static Vector2 像素到坐标(RectTransform 画布, Vector2 像素)`（逆换算）
  - `public bool 有未保存修改`

- [ ] **Step 1: 写失败测试**

`Assets/Tests/EditMode/地图设计器/地图编辑数据测试.cs`：

```csharp
using NUnit.Framework;
using UnityEngine;

// 地图编辑数据：加载/保存/坐标换算 测试
public class 地图编辑数据测试
{
    // 坐标换算：0-100 ↔ 画布像素，与运行时 地图渲染.归一化 互逆
    [Test]
    public void 坐标与像素互逆()
    {
        var 画布 = new GameObject("画布", typeof(RectTransform)).GetComponent<RectTransform>();
        画布.sizeDelta = new Vector2(200f, 100f);
        var 像素 = 地图编辑数据.坐标到像素(画布, 60f, 40f);
        Assert.That(像素.x, Is.EqualTo(20f).Within(0.001f));  // (60/100-0.5)*200 = 20
        Assert.That(像素.y, Is.EqualTo(-10f).Within(0.001f)); // (40/100-0.5)*100 = -10
        var 坐标 = 地图编辑数据.像素到坐标(画布, 像素);
        Assert.That(坐标.x, Is.EqualTo(60f).Within(0.001f));
        Assert.That(坐标.y, Is.EqualTo(40f).Within(0.001f));
    }

    // 加载：文件存在时读入内存副本
    [Test]
    public void 加载读入地图根()
    {
        var 数据 = new 地图编辑数据();
        Assert.That(数据.数据, Is.Not.Null);
    }

    // 保存：写入后产生 .bak 备份且可再次加载
    [Test]
    public void 保存覆盖并备份()
    {
        var 数据 = new 地图编辑数据();
        var 旧备份 = System.IO.File.Exists(地图编辑数据.文件路径 + ".bak");
        if (System.IO.File.Exists(地图编辑数据.文件路径 + ".bak")) System.IO.File.Delete(地图编辑数据.文件路径 + ".bak");
        数据.数据.地点 = new[] { new 地图地点 { 标识 = "测试镇", 类型 = "城镇" } };
        数据.Save();
        Assert.That(System.IO.File.Exists(地图编辑数据.文件路径 + ".bak"), Is.True, "保存应产生 .bak 备份");
        var 重新加载 = new 地图编辑数据();
        Assert.That(重新加载.数据.地点.Length, Is.EqualTo(1));
        Assert.That(重新加载.数据.地点[0].标识, Is.EqualTo("测试镇"));
        数据.Load();
    }
}
```

- [ ] **Step 2: 跑测试确认失败**

Run: EditMode 测试 → 地图设计器.Tests → 地图编辑数据测试
Expected: FAIL（`地图编辑数据` 未定义）

- [ ] **Step 3: 写实现**

`Assets/Scripts/Editor/地图设计器/地图编辑数据.cs`：

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;

// 地图编辑数据：地图设计器的内存副本。持有 地图根，负责 加载/保存(自动备份)/坐标换算。
public sealed class 地图编辑数据
{
    public const string 文件路径 = "Assets/Resources/Data/map.json";

    public static 地图编辑数据 实例 { get; private set; }   // 窗口打开时 new 赋值，场景绘制/窗口共用

    public 地图根 数据 { get; private set; }                // 内存副本（编辑全改它，保存才写文件）
    public bool 有未保存修改 { get; set; }

    public 地图编辑数据()
    {
        Load();
        实例 = this;
    }

    // 从 map.json 读取；文件不存在则建空根
    public void Load()
    {
        数据 = File.Exists(文件路径)
            ? JsonUtility.FromJson<地图根>(File.ReadAllText(文件路径))
            : new 地图根 { 地点 = new 地图地点[0] };
        有未保存修改 = false;
    }

    // 保存：旧文件复制成 .bak，再覆盖写回，刷新资产
    public void Save()
    {
        if (File.Exists(文件路径)) File.Copy(文件路径, 文件路径 + ".bak", true);
        File.WriteAllText(文件路径, JsonUtility.ToJson(数据, true));
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
}
```

- [ ] **Step 4: 跑测试确认通过**

Run: EditMode 测试 → 地图设计器.Tests
Expected: 坐标与像素互逆 / 加载读入地图根 / 保存覆盖并备份 PASS

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Editor/地图设计器/地图编辑数据.cs Assets/Tests/EditMode/地图设计器/地图编辑数据测试.cs
git commit -m "feat(地图设计器): 地图编辑数据 加载/保存/坐标换算"
```

---

### Task 3: 地图编辑数据 —— 编辑操作（增删节点 / 连接 / 断开 / 清理悬挂引用）

**Files:**
- Modify: `Assets/Scripts/Editor/地图设计器/地图编辑数据.cs`
- Test: `Assets/Tests/EditMode/地图设计器/地图编辑数据测试.cs`

**Interfaces:**
- Consumes: Task 2 的 `地图编辑数据`
- Produces（追加到 `地图编辑数据`）：
  - `public string 当前层`（"大地图" 或 城镇标识，默认 "大地图"）
  - `public string 选中标识`
  - `public bool 连接模式` / `public string 连接起点`
  - `public 地图地点[] 当前地点列表`（大地图层 → 全部地点）
  - `public 地图地点 当前城镇`（小地图层 → 城镇地点，null 表示大地图层）
  - `public void 新增节点()`（大地图加地点 / 小地图加节点）
  - `public void 删除选中()`（删节点 + 清理其它节点连接里的引用）
  - `public void 建立连接(string a, string b)`（双向互加、去重）
  - `public void 断开连接(string a, string b)`
  - `public void 处理连接点击(string 标识)`（连接模式：点第一个存起点，点第二个建立连接）

- [ ] **Step 1: 写失败测试（追加到测试文件）**

```csharp
    // 连接：双向互加、去重
    [Test]
    public void 建立连接双向去重()
    {
        var 数据 = new 地图编辑数据();
        数据.数据.地点 = new[] { new 地图地点 { 标识 = "A", 连接 = new[] { "B" } }, new 地图地点 { 标识 = "B", 连接 = null } };
        数据.当前层 = "大地图";
        数据.建立连接("A", "B");
        var a = 数据.数据.地点[0];
        var b = 数据.数据.地点[1];
        Assert.That(a.连接, Does.Contain("B"));
        Assert.That(a.连接.Length, Is.EqualTo(1), "重复连接应去重");
        Assert.That(b.连接, Does.Contain("A"));
        Assert.That(b.连接.Length, Is.EqualTo(1));
    }

    // 断开：从双方连接里移除
    [Test]
    public void 断开连接双向移除()
    {
        var 数据 = new 地图编辑数据();
        数据.数据.地点 = new[] { new 地图地点 { 标识 = "A", 连接 = new[] { "B" } }, new 地图地点 { 标识 = "B", 连接 = new[] { "A" } } };
        数据.当前层 = "大地图";
        数据.断开连接("A", "B");
        Assert.That(数据.数据.地点[0].连接.Length, Is.EqualTo(0));
        Assert.That(数据.数据.地点[1].连接.Length, Is.EqualTo(0));
    }

    // 删除：同时清理其它节点连接里的引用
    [Test]
    public void 删除清理悬挂连接()
    {
        var 数据 = new 地图编辑数据();
        数据.数据.地点 = new[] {
            new 地图地点 { 标识 = "A", 连接 = new[] { "B", "C" } },
            new 地图地点 { 标识 = "B", 连接 = new[] { "A" } },
            new 地图地点 { 标识 = "C", 连接 = null }
        };
        数据.当前层 = "大地图";
        数据.选中标识 = "B";
        数据.删除选中();
        Assert.That(数据.数据.地点.Length, Is.EqualTo(2));
        Assert.That(数据.数据.地点[0].连接, Does.Not.Contain("B"), "删除后其它节点连接应清理");
    }

    // 小地图：新增节点进入当前城镇的小地图数组
    [Test]
    public void 小地图新增节点()
    {
        var 数据 = new 地图编辑数据();
        数据.数据.地点 = new[] { new 地图地点 { 标识 = "灰烬镇", 类型 = "城镇", 小地图 = new 地图节点[0] } };
        数据.当前层 = "灰烬镇";
        数据.新增节点();
        Assert.That(数据.当前城镇.小地图.Length, Is.EqualTo(1));
    }
```

- [ ] **Step 2: 跑测试确认失败**

Run: EditMode 测试 → 地图设计器.Tests
Expected: FAIL（新方法未定义）

- [ ] **Step 3: 写实现（追加到 `地图编辑数据.cs`）**

```csharp
    // —— 编辑状态 ——
    public string 当前层 { get; set; } = "大地图";   // "大地图" 或 城镇标识
    public string 选中标识 { get; set; } = "";
    public bool 连接模式 { get; set; }               // true=点两个节点连线
    public string 连接起点 { get; set; } = "";       // 连接模式下第一次点选的节点

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
```

- [ ] **Step 4: 跑测试确认通过**

Run: EditMode 测试 → 地图设计器.Tests
Expected: 新增 4 条测试全 PASS

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Editor/地图设计器/地图编辑数据.cs Assets/Tests/EditMode/地图设计器/地图编辑数据测试.cs
git commit -m "feat(地图设计器): 地图编辑数据 增删/连接操作"
```

---

### Task 4: 地图设计器窗口（EditorWindow）

**Files:**
- Create: `Assets/Scripts/Editor/地图设计器/地图设计器窗口.cs`

**Interfaces:**
- Consumes: `地图编辑数据`（Task 2/3）
- Produces:
  - `public static bool 是否打开`（场景绘制判断是否渲染）
  - `public static RectTransform 当前画布`（按 当前层 解析 `Canvas/大地图面板/地图区` 或 `Canvas/小地图面板/地图区`，自动激活对应面板）

- [ ] **Step 1: 写窗口代码**

`Assets/Scripts/Editor/地图设计器/地图设计器窗口.cs`：

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 地图设计器窗口：层下拉 / 节点列表 / 属性表单 / 增删保存按钮。场景拖拽由 地图设计器场景绘制 提供。
public sealed class 地图设计器窗口 : EditorWindow
{
    public static bool 是否打开 { get; private set; }
    private string[] 层选项;          // "大地图" + 各城镇标识
    private Vector2 滚动;

    [MenuItem("窗口/地图设计器")]
    public static void 打开() => GetWindow<地图设计器窗口>("地图设计器");

    void OnEnable()
    {
        是否打开 = true;
        地图编辑数据.实例 = new 地图编辑数据();
    }

    void OnDisable()
    {
        是否打开 = false;
        var 数据 = 地图编辑数据.实例;
        if (数据 != null && 数据.有未保存修改)
            if (EditorUtility.DisplayDialog("地图设计器", "有未保存的修改，退出前保存到 map.json？", "保存", "放弃"))
                数据.Save();
    }

    // 按当前层解析画布（场景绘制用）
    public static RectTransform 当前画布
    {
        get
        {
            var 数据 = 地图编辑数据.实例;
            if (数据 == null) return null;
            var 面板名 = 数据.当前层 == "大地图" ? "大地图面板" : "小地图面板";
            var 面板 = GameObject.Find("Canvas/" + 面板名);
            if (面板 != null) 面板.SetActive(true);   // 编辑时让面板可见
            var 区 = GameObject.Find("Canvas/" + 面板名 + "/地图区");
            return 区 != null ? 区.GetComponent<RectTransform>() : null;
        }
    }

    void OnGUI()
    {
        var 数据 = 地图编辑数据.实例;
        if (数据 == null) { 数据 = new 地图编辑数据(); }

        // —— 顶部：层下拉 ——
        EditorGUILayout.BeginHorizontal();
        刷新层选项(数据);
        var 选 = EditorGUILayout.Popup("编辑层", System.Math.Max(0, System.Array.IndexOf(层选项, 数据.当前层)), 层选项);
        if (选 >= 0 && 层选项[选] != 数据.当前层)
        {
            数据.当前层 = 层选项[选];
            数据.选中标识 = "";
            数据.连接起点 = "";
            当前画布?.gameObject.SetActive(true);
        }
        数据.连接模式 = EditorGUILayout.ToggleLeft("连接模式", 数据.连接模式, GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(数据.当前层 == "大地图" ? "大地图节点" : $"{数据.当前层} · 小地图节点", EditorStyles.boldLabel);

        // —— 中部：节点列表 ——
        滚动 = EditorGUILayout.BeginScrollView(滚动);
        if (数据.当前层 == "大地图")
        {
            foreach (var 地点 in 数据.当前地点列表)
                if (节点行(地点.标识, 地点.名称, 数据)) break;
        }
        else if (数据.当前城镇 != null && 数据.当前城镇.小地图 != null)
        {
            foreach (var 节点 in 数据.当前城镇.小地图)
                if (节点行(节点.标识, 节点.名称, 数据)) break;
        }
        EditorGUILayout.EndScrollView();

        // —— 选中节点的属性表单 ——
        属性表单(数据);

        // —— 底部：按钮 ——
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("新增节点")) 数据.新增节点();
        if (GUILayout.Button("删除选中")) 数据.删除选中();
        GUILayout.FlexibleSpace();
        GUI.enabled = 数据.有未保存修改;
        if (GUILayout.Button("保存到 map.json")) 数据.Save();
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    // 节点列表行：点选切换 选中标识；返回 true 表示点中（提前结束遍历）
    private bool 节点行(string 标识, string 名称, 地图编辑数据 数据)
    {
        bool 选中 = 标识 == 数据.选中标识;
        if (GUILayout.Toggle(选中, 名称)) { 数据.选中标识 = 标识; return true; }
        if (选中) 数据.选中标识 = "";   // 取消选中
        return false;
    }

    // 选中节点的属性编辑表单
    private void 属性表单(地图编辑数据 数据)
    {
        if (string.IsNullOrEmpty(数据.选中标识)) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("—— 属性 ——", EditorStyles.boldLabel);
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
            var 节点 = System.Array.Find(数据.当前城镇.小地图, n => n.标识 == 数据.选中标识);
            if (节点 == null) return;
            节点.名称 = EditorGUILayout.TextField("名称", 节点.名称);
            节点.类型 = EditorGUILayout.TextField("类型(入口/设施/剧情/空地)", 节点.类型);
            if (节点.类型 == "设施") 节点.设施 = EditorGUILayout.TextField("设施标识", 节点.设施);
            if (节点.类型 == "剧情") 节点.目标 = EditorGUILayout.TextField("剧情目标", 节点.目标);
            节点.x = EditorGUILayout.FloatField("x(0-100)", 节点.x);
            节点.y = EditorGUILayout.FloatField("y(0-100)", 节点.y);
            连接编辑(节点.连接, 数据);
        }
        if (EditorGUI.EndChangeCheck()) 数据.有未保存修改 = true;
    }

    // 连接多选：勾选即建/断连接
    private void 连接编辑(string[] 当前连接, 地图编辑数据 数据)
    {
        var 所有 = 数据.当前层 == "大地图"
            ? (System.Array.ConvertAll(数据.当前地点列表, p => p.标识))
            : (System.Array.ConvertAll(数据.当前城镇.小地图, n => n.标识));
        foreach (var 候选 in 所有)
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
}
```

- [ ] **Step 2: 编译确认无错误**

Run: Unity 编译（等待 ready_for_tools）
Expected: 无编译错误

- [ ] **Step 3: 手动验证**

Run: 菜单 `窗口 > 地图设计器` → 窗口打开；顶部下拉切「大地图 / 灰烬镇」；点节点行选中；改名称/类型；底部点「新增节点」「删除选中」
Expected: 列表跟随变化，场景里对应面板被激活

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Editor/地图设计器/地图设计器窗口.cs
git commit -m "feat(地图设计器): 编辑器窗口（层下拉/节点列表/属性/保存）"
```

---

### Task 5: 地图设计器场景绘制（Scene View 拖拽）

**Files:**
- Create: `Assets/Scripts/Editor/地图设计器/地图设计器场景绘制.cs`

**Interfaces:**
- Consumes: `地图编辑数据`（Task 2/3）、`地图设计器窗口.是否打开/当前画布`（Task 4）
- Produces: 无外部接口（`[InitializeOnLoad]` 订阅 `SceneView.duringSceneGui`）

- [ ] **Step 1: 写场景绘制代码**

`Assets/Scripts/Editor/地图设计器/地图设计器场景绘制.cs`：

```csharp
using UnityEditor;
using UnityEngine;

// 地图设计器场景绘制：窗口打开时，在场景视图画节点把手与连线。
// 普通模式：拖把手=改位置；连接模式：点 A 再点 B=连线；右键节点=删除。
[InitializeOnLoad]
public static class 地图设计器场景绘制
{
    private const float 把手半径 = 0.6f;

    static 地图设计器场景绘制()
    {
        SceneView.duringSceneGui += 绘制;
    }

    private static void 绘制(SceneView 视图)
    {
        var 数据 = 地图编辑数据.实例;
        if (数据 == null || !地图设计器窗口.是否打开) return;
        var 画布 = 地图设计器窗口.当前画布;
        if (画布 == null || 画布.rect.size.x < 1f || 画布.rect.size.y < 1f) return;

        // 画布世界矩形（GetWorldCorners: [0]=左下 [2]=右上）
        var 角点 = new Vector3[4];
        RectTransformUtility.GetWorldCorners(画布, 角点);
        var 左下 = 角点[0];
        var 宽 = (角点[2] - 角点[0]).x;
        var 高 = (角点[2] - 角点[0]).y;
        if (宽 < 0.001f || 高 < 0.001f) return;

        // 世界 ↔ 0-100
        System.Func<Vector2, Vector3> 坐标到世界 = 坐标 => 左下 + new Vector3(坐标.x / 100f * 宽, 坐标.y / 100f * 高, 0f);
        System.Func<Vector3, Vector2> 世界到坐标 = 位置 => new Vector2((位置.x - 左下.x) / 宽 * 100f, (位置.y - 左下.y) / 高 * 100f);

        // 画连线（所有当前层连接）
        Handles.color = new Color(0.55f, 0.5f, 0.4f);
        foreach (var 线 in 数据.当前层 == "大地图" ? 大地图线(数据) : 小地图线(数据))
            Handles.DrawLine(坐标到世界(线.a), 坐标到世界(线.b));

        // 画节点把手
        if (数据.当前层 == "大地图")
            foreach (var 地点 in 数据.当前地点列表)
                节点把手(数据, 地点.标识, 地点.名称, 地点.x, 地点.y, 坐标到世界, 世界到坐标, (x, y) => { 地点.x = x; 地点.y = y; });
        else if (数据.当前城镇?.小地图 != null)
            foreach (var 节点 in 数据.当前城镇.小地图)
                节点把手(数据, 节点.标识, 节点.名称, 节点.x, 节点.y, 坐标到世界, 世界到坐标, (x, y) => { 节点.x = x; 节点.y = y; });
    }

    // 单个节点：把手拖拽/连接模式点击/右键删除
    private static void 节点把手(地图编辑数据 数据, string 标识, string 名称, float x, float y,
        System.Func<Vector2, Vector3> 坐标到世界, System.Func<Vector3, Vector2> 世界到坐标, System.Action<float, float> 写坐标)
    {
        var 世界 = 坐标到世界(new Vector2(x, y));
        bool 选中 = 标识 == 数据.选中标识;
        Handles.color = 选中 ? 游戏主题.选中色 : 数据.连接模式 ? new Color(1f, 0.9f, 0.3f) : 游戏主题.金色;
        Handles.Label(世界 + Vector3.up * 0.8f, 名称, EditorStyles.miniLabel);

        if (数据.连接模式)
        {
            // 连接模式：点击连线（Handles.Button 处理点击）
            if (Handles.Button(世界, Quaternion.identity, 把手半径, 把手半径, Handles.CircleHandleCap))
                数据.处理连接点击(标识);
        }
        else
        {
            // 普通模式：拖把手改位置（Slider2D 返回拖后位置）
            var 新世界 = Handles.Slider2D(世界, Vector3.forward, Vector3.right, Vector3.up, 把手半径, Handles.CircleHandleCap, 0f);
            if (新世界 != 世界)
            {
                var 坐标 = 世界到坐标(新世界);
                写坐标(Mathf.Clamp(坐标.x, 0f, 100f), Mathf.Clamp(坐标.y, 0f, 100f));
                数据.有未保存修改 = true;
            }
            // 单击选中
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && HandleUtility.DistanceToCircle(世界, 把手半径) < 0.5f)
            {
                数据.选中标识 = 标识;
                Event.current.Use();
            }
        }

        // 右键删除（两模式通用）
        if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && HandleUtility.DistanceToCircle(世界, 把手半径) < 0.5f)
        {
            var 菜单 = new GenericMenu();
            菜单.AddItem(new GUIContent("删除节点"), false, () => { 数据.选中标识 = 标识; 数据.删除选中(); });
            菜单.AddItem(new GUIContent("清除该节点连接"), false, () => { 数据.选中标识 = 标识; 清空连接(数据, 标识); });
            菜单.ShowAsContext();
            Event.current.Use();
        }
    }

    // 清空某节点连接
    private static void 清空连接(地图编辑数据 数据, string 标识)
    {
        foreach (var 对方 in 数据.当前层 == "大地图" ? System.Array.ConvertAll(数据.当前地点列表, p => p.标识)
                                               : System.Array.ConvertAll(数据.当前城镇.小地图, n => n.标识))
            if (对方 != 标识) 数据.断开连接(标识, 对方);
    }

    private struct 线 { public Vector2 a, b; }

    private static System.Collections.Generic.IEnumerable<线> 大地图线(地图编辑数据 数据)
    {
        foreach (var 地点 in 数据.当前地点列表)
            if (地点.连接 != null)
                foreach (var 邻 in 地点.连接)
                    if (string.CompareOrdinal(地点.标识, 邻) > 0)
                    {
                        var 邻点 = System.Array.Find(数据.当前地点列表, p => p.标识 == 邻);
                        if (邻点 != null) yield return new 线 { a = new Vector2(地点.x, 地点.y), b = new Vector2(邻点.x, 邻点.y) };
                    }
    }

    private static System.Collections.Generic.IEnumerable<线> 小地图线(地图编辑数据 数据)
    {
        var 镇 = 数据.当前城镇;
        if (镇 == null || 镇.小地图 == null) yield break;
        foreach (var 节点 in 镇.小地图)
            if (节点.连接 != null)
                foreach (var 邻 in 节点.连接)
                    if (string.CompareOrdinal(节点.标识, 邻) > 0)
                    {
                        var 邻点 = System.Array.Find(镇.小地图, n => n.标识 == 邻);
                        if (邻点 != null) yield return new 线 { a = new Vector2(节点.x, 节点.y), b = new Vector2(邻点.x, 邻点.y) };
                    }
    }
}
```

- [ ] **Step 2: 编译确认无错误**

Run: Unity 编译
Expected: 无编译错误

- [ ] **Step 3: 手动验证（编辑器交互）**

Run: 打开 `窗口 > 地图设计器` → 切「灰烬镇」层 → 场景视图应看到 7 个节点把手 + 连线
- 拖一个把手 → 位置变化（窗口属性表单里 x/y 同步变）
- 勾「连接模式」→ 点 A 再点 B → 画线出现
- 右键节点 → 删除
- 点「保存到 map.json」→ 打开 `Assets/Resources/Data/map.json` 确认数据写入、存在 `.bak`
- 进 Play → 大地图/小地图按新设计渲染

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Editor/地图设计器/地图设计器场景绘制.cs
git commit -m "feat(地图设计器): 场景视图拖拽/连线/右键删除"
```

---

### Task 6: 全流程联调 + 收尾

**Files:**
- Modify: `docs/ugui搭建指南.md`（补一节「地图设计器」用法）
- Test: 手动验证清单

**Interfaces:**
- Consumes: Task 2-5 全部产物

- [ ] **Step 1: 全流程手动验证**

Run（按序）：
1. `窗口 > 地图设计器` 打开
2. 编辑大地图：拖「灰烬镇」位置，另存观察连线跟随
3. 切「灰烬镇」小地图：拖节点、连接模式连一条新线、右键删一个节点再加回（或「新增节点」）
4. 点「保存到 map.json」→ 确认 `.bak` 生成、`map.json` 更新
5. 进 Play：大地图 → 点灰烬镇 → 小地图节点按新位置渲染；双击可移动/进入
Expected: 全链路正常，无报错

- [ ] **Step 2: 写用法文档（追加到指南）**

`docs/ugui搭建指南.md` 末尾追加：

```markdown
## 8. 地图设计器（可视化编辑地图）

菜单 `窗口 > 地图设计器` 打开。运行时仍读 `map.json`，编辑器是它的可视化入口。

- **编辑层**：顶部下拉选「大地图」或某个城镇的「小地图」
- **拖节点**：场景视图直接拖把手改位置（0-100 坐标）
- **连接**：勾选「连接模式」，点节点 A 再点 B 建立双向连接；或在属性表单勾选「连接 ××」
- **属性**：点选节点后，窗口下方表单改名称/类型/设施/剧情目标等
- **增删**：底部「新增节点」「删除选中」；场景视图右键节点可删除/清连接
- **保存**：点「保存到 map.json」覆盖写入（自动备份 .bak），重进 Play 生效

> 前提：对应面板（大地图面板/小地图面板）的 `地图区` 已搭好并激活。
```

- [ ] **Step 3: 提交**

```bash
git add docs/ugui搭建指南.md
git commit -m "docs: 地图设计器使用说明"
```

---

### Task 7: 运行时地图平移缩放（拖拽移动 / 滚轮缩放）

> 独立于编辑器工具，属于**运行时**地图面板功能：节点增多后地图显示不全，玩家可拖拽平移、滚轮缩放查看。

**Files:**
- Modify: `Assets/Scripts/UI/地图渲染.cs`（加 `创建内容` / `应用视图`）
- Modify: `Assets/Scripts/UI/大地图面板.cs`（渲染进 `地图内容` 容器 + IDragHandler/IScrollHandler）
- Modify: `Assets/Scripts/UI/小地图面板.cs`（同上）
- Test: 手动验证（运行时交互）

**Interfaces:**
- Consumes: 现有 `地图渲染.归一化`、`面板基类`
- Produces:
  - `地图渲染.创建内容(RectTransform 地图区)` → 地图内容 RectTransform（全幅拉伸子容器）
  - `地图渲染.应用视图(RectTransform 内容, float 缩放, Vector2 平移)`（只变换容器）

- [ ] **Step 1: 给 `地图渲染` 加容器与视图应用**

追加到 `Assets/Scripts/UI/地图渲染.cs`：

```csharp
    // 创建/取回「地图内容」容器：节点与连线都放进它，平移缩放只变换它（不重渲染）
    public static RectTransform 创建内容(RectTransform 地图区)
    {
        var 子 = 地图区.Find("地图内容");
        if (子 != null) return 子 as RectTransform;
        var 物体 = new GameObject("地图内容", typeof(RectTransform));
        物体.transform.SetParent(地图区, false);
        var 矩形 = (RectTransform)物体.transform;
        矩形.anchorMin = Vector2.zero;
        矩形.anchorMax = Vector2.one;
        矩形.offsetMin = Vector2.zero;
        矩形.offsetMax = Vector2.zero;
        return 矩形;
    }

    // 应用平移缩放视图：只动容器变换，节点无需重画
    public static void 应用视图(RectTransform 内容, float 缩放, Vector2 平移)
    {
        if (内容 == null) return;
        内容.localScale = Vector3.one * 缩放;
        内容.anchoredPosition = 平移;
    }
```

- [ ] **Step 2: 改 `大地图面板`：渲染进容器 + 拖拽平移/滚轮缩放**

`Assets/Scripts/UI/大地图面板.cs`：

```csharp
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 大地图面板：大世界节点图的交互表现。单击节点=选中（钢蓝高亮），双击=移动/进入；拖拽平移、滚轮缩放。
public sealed class 大地图面板 : 面板基类, IDragHandler, IScrollHandler
{
    [SerializeField] private RectTransform 地图区;
    private RectTransform 地图内容;      // 节点/连线容器（平移缩放只动它）
    private float 缩放 = 1f;
    private Vector2 平移 = Vector2.zero;
    private string 选中节点;
    private readonly Dictionary<string, TMP_Text> 节点文字 = new Dictionary<string, TMP_Text>();
    private readonly 双击检测 双击 = new 双击检测();

    // 让面板自身能接收拖拽/滚轮（空白区=面板 Image 兜底）
    void Awake()
    {
        var 图像 = GetComponent<Image>();
        if (图像 != null) 图像.raycastTarget = true;
    }

    protected override void 刷新(object 上下文)
    {
        选中节点 = "";
        渲染大地图();
    }

    private void 渲染大地图()
    {
        if (地图区 == null) { Debug.LogWarning("[大地图面板] 未在 Inspector 拖入 地图区 容器"); return; }
        地图内容 = 地图渲染.创建内容(地图区);
        清空(地图内容);
        var 服务 = ServiceRegistry.Get<地图服务>();
        var 数据 = ServiceRegistry.Get<DataService>();
        节点文字.Clear();

        foreach (var 地点 in 数据.地图.Values)
        {
            if (地点.连接 == null) continue;
            foreach (var 相邻 in 地点.连接)
            {
                if (!数据.地图.TryGetValue(相邻, out var 邻点)) continue;
                if (string.CompareOrdinal(地点.标识, 相邻) > 0) continue;
                地图渲染.画线(地图内容, 地图渲染.归一化(地图区, 地点.x, 地点.y), 地图渲染.归一化(地图区, 邻点.x, 邻点.y), new Color(0.35f, 0.32f, 0.28f));
            }
        }

        foreach (var 地点 in 数据.地图.Values)
        {
            var 标识 = 地点.标识;
            var 文本 = 地图渲染.创建节点(地图内容, 地点.名称, 地图渲染.归一化(地图区, 地点.x, 地点.y), 地点.标识 == 服务.当前大节点, 标识 == 选中节点, () => 处理节点点击(标识));
            if (文本 != null) 节点文字[标识] = 文本;
        }
        地图渲染.应用视图(地图内容, 缩放, 平移);
    }

    // 拖拽平移
    public void OnDrag(PointerEventData 事件)
    {
        平移 += 事件.delta;
        地图渲染.应用视图(地图内容, 缩放, 平移);
    }

    // 滚轮缩放：以光标为锚点，保持光标下的世界点不动
    public void OnScroll(PointerEventData 事件)
    {
        var 旧缩放 = 缩放;
        缩放 = Mathf.Clamp(缩放 * (1f - 事件.scrollDelta.y * 0.1f), 0.6f, 3f);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(地图区, 事件.position, null, out var 光标))
            平移 = 光标 - (光标 - 平移) * (缩放 / 旧缩放);
        地图渲染.应用视图(地图内容, 缩放, 平移);
    }

    private void 处理节点点击(string 标识)
    {
        var 服务 = ServiceRegistry.Get<地图服务>();
        if (双击.点击(标识)) { 服务.移动(标识); return; }
        if (节点文字.TryGetValue(选中节点, out var 旧))
            旧.color = 选中节点 == 服务.当前大节点 ? 游戏主题.金色 : 游戏主题.文字;
        选中节点 = 标识;
        if (节点文字.TryGetValue(标识, out var 新)) 新.color = 游戏主题.选中色;
    }
}
```

- [ ] **Step 3: 改 `小地图面板`：同样的容器 + 平移缩放**

`Assets/Scripts/UI/小地图面板.cs`：在类声明加 `, IDragHandler, IScrollHandler`；加 `using UnityEngine.EventSystems;`；Awake 里加 `GetComponent<Image>().raycastTarget = true`（非空判断）；加字段 `地图内容/缩放/平移`；`渲染小地图` 里把 `清空(地图区)` 与所有 `画线(...地图区...)` / `创建节点(...地图区...)` 改成 `地图内容`（先 `地图内容 = 地图渲染.创建内容(地图区);`）；渲染末尾 `地图渲染.应用视图(地图内容, 缩放, 平移);`；追加两个方法：

```csharp
    // 拖拽平移
    public void OnDrag(PointerEventData 事件)
    {
        平移 += 事件.delta;
        地图渲染.应用视图(地图内容, 缩放, 平移);
    }

    // 滚轮缩放：以光标为锚点
    public void OnScroll(PointerEventData 事件)
    {
        var 旧缩放 = 缩放;
        缩放 = Mathf.Clamp(缩放 * (1f - 事件.scrollDelta.y * 0.1f), 0.6f, 3f);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(地图区, 事件.position, null, out var 光标))
            平移 = 光标 - (光标 - 平移) * (缩放 / 旧缩放);
        地图渲染.应用视图(地图内容, 缩放, 平移);
    }
```

- [ ] **Step 4: 编译确认无错误**

Run: Unity 编译
Expected: 无编译错误

- [ ] **Step 5: 手动验证（运行时）**

Run: 进 Play → 大地图：鼠标拖空白处地图跟随移动；滚轮缩放（光标处为中心）；再点节点仍可选中/双击进入。进灰烬镇小地图重复验证。
Expected: 平移/缩放正常，双击/选中不受影响，无报错

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/UI/地图渲染.cs Assets/Scripts/UI/大地图面板.cs Assets/Scripts/UI/小地图面板.cs
git commit -m "feat(地图): 运行时地图拖拽平移 + 滚轮缩放"
```

---

## Self-Review 记录

- **Spec 覆盖**：§4 窗口布局 → Task 4；§5 场景画布/拖拽/连线/右键 → Task 5；§6 数据流/备份 → Task 2/3；§7 坐标换算 → Task 2；§8 不污染场景 → Task 5（纯 Handles）；§9 错误处理 → Task 2（空根）/Task 4（画布判空）/Task 5（rect 判空）；§10 范围外 ✓
- **类型一致性**：`地图编辑数据.实例/当前层/选中标识/连接模式/连接起点/建立连接/断开连接/删除选中/新增节点/当前城镇/当前地点列表/坐标到像素/像素到坐标` 在 Task 2/3 定义，Task 4/5 全程同名引用；`地图设计器窗口.是否打开/当前画布` 在 Task 4 定义，Task 5 引用 ✓
- **占位扫描**：无 TBD/TODO；所有代码步含完整代码 ✓
- **已知取舍**：`地图编辑数据.删除选中` 原笔误 `!RemoveAll(...) > 0`（语法非法）已在计划内修正为 `RemoveAll(...) == 0` ✓
- **Task 7 追加**（2026-08-16 用户新增）：运行时地图拖拽平移 + 滚轮缩放；依赖 `地图渲染.创建内容/应用视图`、两面板加 IDragHandler/IScrollHandler；平移缩放只变换 `地图内容` 容器不重渲染；双击/选中交互不受影响 ✓
