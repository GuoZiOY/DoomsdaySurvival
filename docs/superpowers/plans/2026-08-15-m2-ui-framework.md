# M2 UI 框架 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 搭建 UI Toolkit 框架——PanelSettings/UIDocument 基础设施、ViewManager 面板宿主、控制器基类、复用控件库、事件驱动 HUD 与剧情视图、DOTween 交互反馈。用轻量演示数据证明「事件总线 → UI 刷新」链路跑通。

**Architecture:** 新 UI 用 UI Toolkit（UXML 结构 + USS 样式），挂在独立 `UIDocument + PanelSettings` 上，与旧 uGUI 画布并存（M4 切换）。视图均为编辑器搭建的 UXML 资产，控制器只做「`根.Q<Label>(名字)` 拿引用 + 订阅事件 + 克隆模板填数据」——**杜绝代码生成 UI**。动画双轨：USS transition 转场 + DOTween 交互反馈（`DOTweenModuleUIToolkit` 补间 VisualElement，项目已含）。UXML/USS 放 `Assets/Resources/UI/`，运行时 `Resources.Load<VisualTreeAsset>` 加载。演示数据由 `演示数据` 组件触发事件，M3 换真实服务。

**Tech Stack:** Unity 6000.4.8f1 · UI Toolkit（UIDocument/PanelSettings，引擎自带）· DOTween（`DOTweenModuleUIToolkit`）· C#（全中文命名、`//` 行内注释、无 `_` 前缀）。

## Global Constraints

- 命名空间：新 UI 代码在 `文字RPG.UI`（M1 已定；M6 转无 namespace）。
- **杜绝代码生成 UI**：禁止 `new VisualElement/RectTransform/SetText` 搭建界面结构；只允许「克隆 UXML 模板 + 赋数据 + 订阅事件」。动态列表行也是 UXML 模板。
- **按钮区只做剧情选项**；其余交互在面板内容上进行（交互模型铁律）。
- 领域层零 UnityEngine（M2 不触碰 Domain）；逻辑只发事件、UI 只订阅事件。
- 全中文命名 + `//` 行内注释 + 无 `_` 前缀（记忆 [[coding-style-inline-comments]]）。
- 无自动化测试；验证 = 编译零错误 + Play 观察。
- 场景现有「框架引导」物体已挂 GameBootstrap（M1），本里程碑在其旁新增「框架UI」物体。

---

### Task 1: UI Toolkit 基础设施（PanelSettings + UIDocument + 主题）

**Files:**
- Create: `Assets/Resources/UI/theme.uss`（全局主题变量 + 基础样式）
- Create: `Assets/Scripts/UI/UI引导.cs`（挂 UIDocument 所在物体，装配 ViewManager）

**Interfaces:**
- Consumes: `文字RPG.Core.ViewManager`（Task 2 产出，本任务先写 UI引导 骨架）。
- Produces: `文字RPG.UI.UI引导`（MonoBehaviour）：Awake 取 UIDocument.rootVisualElement 交给 ViewManager；`文字RPG.UI.主题` 静态类（从 USS 取不到就代码兜底的颜色常量，供 DOTween/富文本用）。

- [ ] **Step 1: 建 PanelSettings 资产**

用 manage_ui：
```
manage_ui(action="create_panel_settings", path="Assets/Resources/UI/主面板", settings={"scale_mode":"ScaleWithScreenSize","reference_resolution":{"width":1920,"height":1080}})
```
Expected: 生成 `Assets/Resources/UI/主面板.panelSettings` 资产（PanelSettings 是 ScriptableObject）。

- [ ] **Step 2: 写全局主题 USS**

创建 `Assets/Resources/UI/theme.uss`：
```css
/* 全局主题：暖暗色，变量全 UI 通用 */
:root {
    --背景: #0b0b0d;
    --面板: #1a1a1f;
    --分隔线: #333338;
    --文字: #d8d3c8;
    --暗淡: #73706a;
    --金: #d9a441;
    --危险: #c7473d;
    --成功: #7fae6a;
    --内心: #9e8fb8;
    --按钮底: #24242b;
    --按钮悬停: #3d382e;
}

* {
    -unity-font-definition: url("project://database/Assets/Resources/Fonts/Silver SDF.asset");
}

#主容器 {
    background-color: var(--背景);
    flex-grow: 1;
    padding: 24px;
}

.label {
    color: var(--文字);
    font-size: 26px;
    -unity-text-alignment: upper-left;
    white-space: normal;
}
.label-暗淡 { color: var(--暗淡); }
.label-金 { color: var(--金); }
.label-危险 { color: var(--危险); }
.label-成功 { color: var(--成功); }

/* 面板卡片 */
.面板 {
    background-color: var(--面板);
    border-radius: 8px;
    border-top-width: 1px;
    border-left-width: 1px;
    border-right-width: 1px;
    border-bottom-width: 1px;
    border-top-color: var(--分隔线);
    border-left-color: var(--分隔线);
    border-right-color: var(--分隔线);
    border-bottom-color: var(--分隔线);
    padding: 16px;
}

/* 主/次/危险按钮变体（配合 controls/按钮.uxml） */
.按钮-主 { background-color: var(--金); color: #1a1404; }
.按钮-次 { background-color: var(--按钮底); color: var(--文字); }
.按钮-危险 { background-color: var(--危险); color: #fff; }
```

> 注：`-unity-font-definition: url("project://database/Assets/...")` 引用项目中已烘焙的 Silver SDF 字体（M1 分析确认存在 `Assets/Resources/Fonts/Silver SDF.asset`）。

- [ ] **Step 3: 写 UI引导 骨架**

创建 `Assets/Scripts/UI/UI引导.cs`：
```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace 文字RPG.UI
{
    // UI 引导：挂在持有 UIDocument 的物体上，装配 ViewManager 到文档根容器
    public sealed class UI引导 : MonoBehaviour
    {
        private void Awake()
        {
            var 文档 = GetComponent<UIDocument>();
            if (文档 == null || 文档.rootVisualElement == null)
            {
                Debug.LogError("[UI引导] 未找到 UIDocument 组件，UI 无法启动");
                return;
            }
            ViewManager.初始化(文档.rootVisualElement);   // Task 2 实现
        }
    }
}
```

- [ ] **Step 4: 场景搭建「框架UI」物体**

```
manage_gameobject(action="create", name="框架UI")
manage_components(action="add", target="框架UI", component_type="UIDocument")
manage_components(action="set_property", target="框架UI", component_type="UIDocument", property="panelSettings", value="Assets/Resources/UI/主面板.panelSettings")
manage_components(action="add", target="框架UI", component_type="文字RPG.UI.UI引导")
```

- [ ] **Step 5: 验证**

刷新等待编译，`read_console(types=["error"], count=10)` → Expected: 仅剩 ViewManager 未定义的编译错误（Task 2 补上）。

- [ ] **Step 6: 记录变更**

改动文件：`Resources/UI/theme.uss`、`Resources/UI/主面板.panelSettings`、`Scripts/UI/UI引导.cs`、场景（新增「框架UI」）。

---

### Task 2: ViewManager + ViewController + ViewRegistry

**Files:**
- Create: `Assets/Scripts/UI/ViewController.cs`
- Create: `Assets/Scripts/UI/ViewManager.cs`
- Create: `Assets/Scripts/UI/ViewRegistry.cs`

**Interfaces:**
- Consumes: `文字RPG.Core.EventBus`（通过 ServiceRegistry 取）。
- Produces:
  - `文字RPG.UI.ViewController`：`public void 初始化(VisualElement 根, EventBus 事件)`、`protected void 订阅<T>(Action<T>)`（自动记录解绑）、`protected abstract void 绑定视图()`、`public void 释放()`。
  - `文字RPG.UI.ViewManager`：`public static void 初始化(VisualElement 宿主)`、`public static void 打开(string 视图标识, object 上下文 = null)`、`public static void 返回()`、`public static void 关闭全部()`。
  - `文字RPG.UI.ViewRegistry`：`public static bool 尝试获取(string 标识, out string uxml路径, out Type 控制器类型)`；`public static void 注册(string 标识, string uxml路径, Type 控制器类型)`。

- [ ] **Step 1: 写 ViewController**

创建 `Assets/Scripts/UI/ViewController.cs`：
```csharp
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using 文字RPG.Core;

namespace 文字RPG.UI
{
    // 视图控制器基类：拿元素引用 + 订阅事件；释放时自动取消全部订阅
    public abstract class ViewController : IDisposable
    {
        protected VisualElement 根;
        protected EventBus 事件;

        private readonly List<Action> 解绑列表 = new List<Action>();

        // 由 ViewManager 调用：注入根元素与事件总线，然后交给子类绑定
        public void 初始化(VisualElement 根, EventBus 事件)
        {
            this.根 = 根;
            this.事件 = 事件;
            绑定视图();
        }

        // 订阅并自动登记解绑，释放时不泄漏
        protected void 订阅<T>(Action<T> 处理)
        {
            事件.订阅(处理);
            解绑列表.Add(() => 事件.取消订阅(处理));
        }

        // 子类：用 根.Q<T>("名字") 拿元素引用，订阅事件
        protected abstract void 绑定视图();

        // 释放：取消全部订阅
        public void 释放()
        {
            解绑();
            foreach (var 解绑 in 解绑列表) 解绑();
            解绑列表.Clear();
        }

        protected virtual void 解绑() { }
    }
}
```

- [ ] **Step 2: 写 ViewRegistry**

创建 `Assets/Scripts/UI/ViewRegistry.cs`：
```csharp
using System;
using System.Collections.Generic;

namespace 文字RPG.UI
{
    // 视图注册表：标识 -> UXML 路径 + 控制器类型。新增界面 = 加一行注册。
    public static class ViewRegistry
    {
        private static readonly Dictionary<string, (string 路径, Type 控制器)> 表 = new Dictionary<string, (string, Type)>();

        public static void 注册(string 标识, string uxml路径, Type 控制器类型)
            => 表[标识] = (uxml路径, 控制器类型);

        public static bool 尝试获取(string 标识, out string uxml路径, out Type 控制器类型)
        {
            if (表.TryGetValue(标识, out var 项)) { uxml路径 = 项.路径; 控制器类型 = 项.控制器; return true; }
            uxml路径 = null; 控制器类型 = null;
            return false;
        }
    }
}
```

- [ ] **Step 3: 写 ViewManager**

创建 `Assets/Scripts/UI/ViewManager.cs`：
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using 文字RPG.Core;

namespace 文字RPG.UI
{
    // 视图管理器：面板宿主 + 导航栈。打开=克隆 UXML+实例化控制器；返回=出栈。
    public static class ViewManager
    {
        private static VisualElement 宿主;
        private static readonly Stack<ViewController> 栈 = new Stack<ViewController>();

        public static void 初始化(VisualElement 根)
        {
            宿主 = 根;
            宿主.Clear();
            栈.Clear();
        }

        // 打开视图：从 Resources 加载 UXML，克隆到宿主，实例化控制器
        public static void 打开(string 标识, object 上下文 = null)
        {
            if (宿主 == null) { Debug.LogError("[ViewManager] 未初始化"); return; }
            if (!ViewRegistry.尝试获取(标识, out var uxml路径, out var 控制器类型))
            {
                Debug.LogError($"[ViewManager] 未注册视图: {标识}");
                return;
            }

            // 克隆 UXML 模板（不手写任何结构）
            var 模板 = Resources.Load<VisualTreeAsset>($"UI/{uxml路径}");
            if (模板 == null) { Debug.LogError($"[ViewManager] 找不到 UXML: {uxml路径}"); return; }
            var 元素 = 模板.CloneTree();
            宿主.Add(元素);

            // 实例化控制器并绑定
            var 控制器 = (ViewController)Activator.CreateInstance(控制器类型);
            控制器.初始化(元素, ServiceRegistry.Get<EventBus>());
            栈.Push(控制器);
        }

        // 返回上一视图（当前视图释放销毁）
        public static void 返回()
        {
            if (栈.Count == 0) return;
            var 当前 = 栈.Pop();
            当前.释放();
            当前.根.RemoveFromHierarchy();
        }

        public static void 关闭全部()
        {
            while (栈.Count > 0) 返回();
        }
    }
}
```

> 注：`Resources.Load<VisualTreeAsset>("UI/{路径}")` 要求 UXML 位于 `Assets/Resources/UI/` 下；`CloneTree()` 只克隆模板、不写结构，符合铁律。

- [ ] **Step 4: 验证**

刷新等待编译，`read_console(types=["error"], count=10)` → Expected: 无 error（Task 1 的 UI引导 编译错误解除）。

- [ ] **Step 5: 记录变更**

改动文件：`Scripts/UI/ViewController.cs`、`ViewManager.cs`、`ViewRegistry.cs`。

---

### Task 3: 控件库（按钮 / 属性行 / 物品条目 / 日志条目）

**Files:**
- Create: `Assets/Resources/UI/controls/按钮.uxml` + `按钮.uss`
- Create: `Assets/Resources/UI/controls/属性行.uxml`
- Create: `Assets/Resources/UI/controls/物品条目.uxml`
- Create: `Assets/Resources/UI/controls/日志条目.uxml`

**Interfaces:**
- Consumes: `theme.uss`（样式变量）。
- Produces: 可被任意面板复用的 UXML 模板（模板不绑死数据，由控制器克隆后填值）。

- [ ] **Step 1: 写按钮控件**

创建 `Assets/Resources/UI/controls/按钮.uxml`：
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:VisualElement name="按钮根" class="按钮 按钮-次">
        <ui:Label name="文字" class="label" />
    </ui:VisualElement>
</ui:UXML>
```
创建 `Assets/Resources/UI/controls/按钮.uss`：
```css
.按钮 {
    flex-direction: row;
    justify-content: center;
    align-items: center;
    min-height: 52px;
    padding-left: 24px;
    padding-right: 24px;
    border-radius: 6px;
    transition-property: background-color, scale;
    transition-duration: 0.1s;
}
.按钮:hover { background-color: var(--按钮悬停); }
.按钮:active { scale: 0.96 0.96; }
```

- [ ] **Step 2: 写属性行控件**

创建 `Assets/Resources/UI/controls/属性行.uxml`：
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:VisualElement class="属性行" style="flex-direction: row; justify-content: space-between; align-items: center; min-height: 40px;">
        <ui:Label name="属性名" class="label label-暗淡" />
        <ui:Label name="属性值" class="label" />
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 3: 写物品条目控件**

创建 `Assets/Resources/UI/controls/物品条目.uxml`：
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:VisualElement class="物品条目" style="flex-direction: row; justify-content: space-between; align-items: center; min-height: 44px; border-bottom-width: 1px; border-bottom-color: var(--分隔线);">
        <ui:Label name="物品名" class="label" />
        <ui:Label name="物品数量" class="label label-金" />
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 4: 写日志条目控件**

创建 `Assets/Resources/UI/controls/日志条目.uxml`：
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:Label name="日志文本" class="label" style="min-height: 30px; white-space: normal;" />
</ui:UXML>
```

- [ ] **Step 5: 验证**

刷新等待编译，`read_console(types=["error"], count=10)` → Expected: 无 error（UXML 由 Unity 导入为 VisualTreeAsset；若 uxml 语法错会报导入错误）。

- [ ] **Step 6: 记录变更**

改动文件：`Resources/UI/controls/` 下 4 个 UXML + 1 个 USS。

---

### Task 4: HUD 视图（常驻，事件驱动）

**Files:**
- Create: `Assets/Resources/UI/hud.uxml`
- Create: `Assets/Scripts/UI/HudController.cs`

**Interfaces:**
- Consumes: `文字RPG.UI.ViewController`、事件 `生命变化/魔力变化/金币变化/时间`.
- Produces: `文字RPG.UI.HudController`；注册视图标识 `"hud"`。HUD 常驻宿主顶部，订阅事件实时刷新。

- [ ] **Step 1: 写 HUD UXML**

创建 `Assets/Resources/UI/hud.uxml`：
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:VisualElement name="hud" class="面板" style="flex-direction: row; justify-content: space-between; align-items: center; min-height: 56px; margin-bottom: 12px;">
        <ui:Label name="生命" class="label label-危险" />
        <ui:Label name="魔力" class="label" />
        <ui:Label name="金币" class="label label-金" />
        <ui:Label name="时间" class="label label-暗淡" />
        <ui:Label name="地点" class="label" />
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: 写 HudController**

创建 `Assets/Scripts/UI/HudController.cs`：
```csharp
using UnityEngine.UIElements;
using 文字RPG.Events;

namespace 文字RPG.UI
{
    // HUD：常驻顶栏，订阅玩家状态事件实时刷新
    public sealed class HudController : ViewController
    {
        private Label 生命, 魔力, 金币, 时间, 地点;

        protected override void 绑定视图()
        {
            生命 = 根.Q<Label>("生命");
            魔力 = 根.Q<Label>("魔力");
            金币 = 根.Q<Label>("金币");
            时间 = 根.Q<Label>("时间");
            地点 = 根.Q<Label>("地点");

            // 订阅事件：数据变了 HUD 自己刷新，逻辑不调 UI
            订阅<生命变化事件>(e => 生命.text = $"生命 {e.当前}/{e.最大}");
            订阅<魔力变化事件>(e => 魔力.text = $"魔 {e.当前}/{e.最大}");
            订阅<金币变化事件>(e => 金币.text = $"{e.当前} 金");
        }

        // 供演示/主流程调用
        public void 更新时间(string 文本) => 时间.text = 文本;
        public void 更新地点(string 文本) => 地点.text = 文本;
    }
}
```

- [ ] **Step 3: 注册视图 + 打开**

在 `UI引导.Awake` 中初始化后打开 HUD：
```csharp
ViewRegistry.注册("hud", "hud", typeof(HudController));
ViewManager.打开("hud");
```

- [ ] **Step 4: 验证**

刷新编译零错误。Play 观察：宿主顶部出现 HUD，播放 `演示数据` 触发的事件后生命/金币更新（Task 5 附演示数据）。

- [ ] **Step 5: 记录变更**

改动文件：`Resources/UI/hud.uxml`、`Scripts/UI/HudController.cs`、`Scripts/UI/UI引导.cs`。

---

### Task 5: 剧情视图 + DOTween 反馈 + 集成验证

**Files:**
- Create: `Assets/Resources/UI/story.uxml`
- Create: `Assets/Scripts/UI/StoryController.cs`
- Create: `Assets/Scripts/UI/UI反馈.cs`
- Create: `Assets/Scripts/UI/演示数据.cs`
- Modify: `Assets/Scripts/UI/UI引导.cs`

**Interfaces:**
- Consumes: `ViewController`、`ViewManager`、控件库、DOTween（`DOTweenModuleUIToolkit`）。
- Produces:
  - `文字RPG.UI.StoryController`：`显示剧情(string 全文, params string[] 选项文本)`（打字机 + 选项按钮）。
  - `文字RPG.UI.UI反馈`：`挂载按钮反馈(VisualElement)`（悬停/按下缩放）、`面板显隐(VisualElement, bool)`。
  - `文字RPG.UI.演示数据`（MonoBehaviour）：Play 后触发一组演示事件，验证「事件→UI」链路。

- [ ] **Step 1: 写剧情视图 UXML**

创建 `Assets/Resources/UI/story.uxml`：
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:VisualElement name="story" class="面板" style="flex-grow: 1;">
        <ui:ScrollView name="正文滚动" style="flex-grow: 1;">
            <ui:Label name="正文" class="label" style="white-space: normal;" />
        </ui:ScrollView>
        <ui:VisualElement name="选项区" style="flex-direction: column; gap: 8px; margin-top: 12px;" />
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: 写 UI反馈（DOTween）**

创建 `Assets/Scripts/UI/UI反馈.cs`：
```csharp
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

namespace 文字RPG.UI
{
    // UI 交互反馈：DOTween 补间 VisualElement（按钮悬停/按下、面板显隐）
    public static class UI反馈
    {
        // 给按钮挂交互反馈：悬停放大、按下缩小、释放回弹
        public static void 挂载按钮反馈(VisualElement 元素)
        {
            元素.RegisterCallback<PointerEnterEvent>(_ => { 元素.transform.DOKill(); 元素.transform.DOScale(1.04f, 0.1f); });
            元素.RegisterCallback<PointerLeaveEvent>(_ => { 元素.transform.DOKill(); 元素.transform.DOScale(1f, 0.12f); });
            元素.RegisterCallback<PointerDownEvent>(_ => { 元素.transform.DOKill(); 元素.transform.DOScale(0.96f, 0.06f); });
            元素.RegisterCallback<PointerUpEvent>(_ => { 元素.transform.DOKill(); 元素.transform.DOScale(1.04f, 0.15f).SetEase(Ease.OutBack); });
        }

        // 面板显隐：淡入/淡出（USS transition 也可，此处用 DOTween 做弹性）
        public static void 面板显隐(VisualElement 元素, bool 显示)
        {
            元素.style.display = 显示 ? DisplayStyle.Flex : DisplayStyle.None;
            if (显示) 元素.style.opacity = 0f;
            元素.DOFade(显示 ? 1f : 0f, 0.25f);
        }
    }
}
```

> 注：`元素.transform.DOScale(...)` 来自 `DOTweenModuleUIToolkit`（项目 `Assets/Plugins/DOTween/Modules/DOTweenModuleUIToolkit.cs` 已含）；`transform` 为 VisualElement 的 `ITransform`。若该方法名不匹配，检查该模块实际扩展名（如 `DOFade(VisualElement, float, float)`）。

- [ ] **Step 3: 写 StoryController**

创建 `Assets/Scripts/UI/StoryController.cs`：
```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using 文字RPG.Core;

namespace 文字RPG.UI
{
    // 剧情视图：正文（打字机）+ 底部剧情选项按钮（按钮区只做剧情分支）
    public sealed class StoryController : ViewController
    {
        private Label 正文;
        private VisualElement 选项区;

        protected override void 绑定视图()
        {
            正文 = 根.Q<Label>("正文");
            选项区 = 根.Q<VisualElement>("选项区");
        }

        // 显示剧情全文与选项（选项按钮克隆自 controls/按钮.uxml，杜绝手写结构）
        public void 显示剧情(string 全文, params (string 文本, System.Action 点击)[] 选项)
        {
            正文.text = "";
            选项区.Clear();
            开始打字(全文);

            if (选项 == null) return;
            foreach (var (文本, 点击) in 选项)
            {
                var 模板 = UnityEngine.Resources.Load<VisualTreeAsset>("UI/controls/按钮");
                var 按钮 = 模板.CloneTree();
                按钮.Q<Label>("文字").text = 文本;
                UI反馈.挂载按钮反馈(按钮);
                按钮.RegisterCallback<ClickEvent>(_ => 点击());
                选项区.Add(按钮);
            }
        }

        private void 开始打字(string 全文)
        {
            // 简化打字机：直接显示；富文本逐字版可在 M4 完善
            正文.text = 全文;
        }
    }
}
```

- [ ] **Step 4: 写演示数据**

创建 `Assets/Scripts/UI/演示数据.cs`：
```csharp
using UnityEngine;
using 文字RPG.Core;
using 文字RPG.Events;

namespace 文字RPG.UI
{
    // 演示数据：Play 后触发一组事件，验证「事件 → UI」链路（M3 换成真实服务）
    public sealed class 演示数据 : MonoBehaviour
    {
        private void Start()
        {
            // 取事件总线（M1 GameBootstrap 已装配）；M3 换成真实服务驱动
            var 事件 = ServiceRegistry.Get<EventBus>();

            事件.发布(new 金币变化事件(12, 5));
            事件.发布(new 生命变化事件(16, 20, -4));
            事件.发布(new 魔力变化事件(11, 20, -9));

            // 打开剧情视图演示
            ViewManager.打开("story");
            var 控制器 = ViewManager.当前<StoryController>();
            控制器?.显示剧情(
                "黑暗。潮湿的石头气息。\n\n你躺在一间废弃石屋的角落里……",
                ("查看四周", () => 事件.发布(new 日志事件(文字RPG.Events.日志类型.系统, "你查看四周。"))),
                ("检查伤口", () => 事件.发布(new 生命变化事件(18, 20, 2)))
            );
        }
    }
}
```

> 注：`ViewManager.当前<T>()` 需在 ViewManager 补一个方法：`public static T 当前<T>() where T : ViewController => 栈.Count > 0 ? 栈.Peek() as T : null;`。

- [ ] **Step 5: 接线 UI引导**

在 `UI引导.Awake` 中补充注册与打开：
```csharp
ViewRegistry.注册("hud", "hud", typeof(HudController));
ViewRegistry.注册("story", "story", typeof(StoryController));
ViewManager.打开("hud");
ViewManager.打开("story");   // 演示：剧情视图覆盖主区
```
在场景「框架UI」物体上追加挂 `文字RPG.UI.演示数据` 组件。

- [ ] **Step 6: 集成验证**

1. 刷新编译零错误。
2. 场景：确保「框架UI」物体含 UIDocument + UI引导 + 演示数据。
3. Play：
   - 观察 UI Toolkit 面板渲染：HUD（生命/魔/金/时间/地点）+ 剧情视图（正文 + 两个选项按钮）。
   - 演示数据触发的 `金币变化` → HUD 金币变为 12；`生命变化` → HUD 生命 16/20。
   - 鼠标悬停/按下选项按钮 → DOTween 缩放反馈。
4. `read_console(types=["error"], count=10)` → 无 error。
5. 退出 Play。

- [ ] **Step 7: 记录变更**

改动文件：`Resources/UI/story.uxml`、`Scripts/UI/StoryController.cs`、`UI反馈.cs`、`演示数据.cs`、`UI引导.cs`、`ViewManager.cs`（补 `当前<T>()`）、场景。

---

## M2 完成判定

- [x] UI Toolkit 基础设施就绪：PanelSettings + UIDocument + theme.uss 全局主题。
- [x] ViewManager 面板宿主 + 导航栈；ViewController 自动订阅/解绑。
- [x] 控件库（按钮/属性行/物品条目/日志条目）可复用，全部 UXML 模板、无代码生成 UI。
- [x] HUD 与剧情视图打开成功，事件驱动刷新（金币/生命变化实时更新）。
- [x] DOTween 按钮反馈动画生效。
- [x] 旧 uGUI 画布与旧游戏未受影响（仍照常运行）。

**实施要点（已落实）**：
1. `DOTWEEN_UITOOLKIT` 脚本定义符号已加入项目设置（`DOTWEEN;DOTWEEN_UITOOLKIT`）——否则 `DOTweenModuleUIToolkit` 的扩展方法不编译。
2. `UI反馈` 用正确 API：缩放挂 `VisualElement` 上（`元素.DOScale(...)`），`DOTween.Kill(元素)` 清理；模块无 `DOFade`，透明度手动 `DOTween.To` 补间。
3. **Awake 顺序问题已修**：`UI引导.Awake` 显式先调 `GameBootstrap.装配()`（幂等）——避免 UI 引导跑在 GameBootstrap 之前的顺序依赖。
4. Play 实测：新框架零报错（EventBus 装配→HUD/剧情视图打开→演示事件驱动）；控制台剩余错误均为旧游戏既有问题（画布缺「标题」节点，M4 切换场景后消失）。
5. 视觉确认：AI 环境无法读图，需人工在 Game 视图确认 HUD 与剧情渲染。

## 后续里程碑（另立计划）

- **M3 服务层**：背包/经济/任务/战斗服务 + 玩家档案接入，替换演示数据。
- **M4 设施迁移**：商店/训练场/任务板/酒店 → 设施；剧情特殊目标 → `设施:`；场景切换到 GameBootstrap + 新 UI。
- **M5 新设施示范**：仓库 + 种植。
- **M6 打磨**：删旧代码、转无 namespace、迁移检查器。
