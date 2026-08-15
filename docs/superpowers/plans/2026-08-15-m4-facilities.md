# M4 设施迁移 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 落地用户核心诉求「加设施 = 三块积木（定义 JSON + 设施逻辑类 + UXML 视图）」。本次交付：设施框架（设施基类/工厂/注册表/地点视图）+ 商店设施（完整示范）+ 剧情接设施（特殊目标 → `设施:`）+ 战斗视图。游戏在新技术栈上真正可玩（序章→酒馆商店→战斗）。

**Architecture:** 设施 = `设施控制器`（继承 ViewController，兼做视图控制器与设施逻辑）。设施工厂按 `逻辑类型` 反射实例化；设施注册表按标识/地点索引；剧情选 `设施:xxx` → DialogueService → FacilityRegistry.打开(标识, 返回节点) → ViewManager.打开实例（复用设施实例，不新建）。商店数据来自 DataService（卖所有 `价格>0 且非任务` 的物品）。战斗视图订阅 BattleService 事件。

**Tech Stack:** C#（`文字RPG.Facilities` / `文字RPG.UI`）、JSON（facilities.json 新建；story.json 局部改造）、UI Toolkit。

## Global Constraints

- 设施在 `文字RPG.Facilities`；视图 UXML 在 `Assets/Resources/UI/facilities/`。
- **三块积木**：定义（facilities.json 一行）+ `商店设施.cs`（继承 `设施`）+ `商店.uxml`。新增设施不改核心。
- 逻辑永不调 UI：设施发事件，视图订阅；设施返回用 `ViewManager.返回()` + `DialogueService.进入节点(返回节点)`。
- 全中文命名 + `//` 行内注释 + 无 `_` 前缀。
- 无自动化测试；验证 = 编译零错误 + Play 观察。
- **story.json 改造会暂时破坏旧游戏**（旧剧情引擎不认识 `设施:` 目标）——可接受，旧代码 M4C 删除。
- 本计划范围：商店 + 剧情接设施 + 战斗视图。训练场/任务板/酒店/探索/场景切换删旧 = M4B。

---

### Task 1: 设施框架（基类 + 工厂 + 注册表 + 打开实例）

**Files:**
- Create: `Assets/Scripts/Facilities/设施.cs`
- Create: `Assets/Scripts/Facilities/FacilityContext.cs`
- Create: `Assets/Scripts/Facilities/设施工厂.cs`
- Create: `Assets/Scripts/Facilities/设施注册表.cs`
- Modify: `Assets/Scripts/UI/ViewManager.cs`（加 `打开实例(ViewController, string)`）
- Modify: `Assets/Scripts/Core/GameBootstrap.cs`（装配设施工厂）

**Interfaces:**
- Consumes: `ViewController`、`ViewManager`、`DataService`、`EventBus`、`PlayerService`、`DialogueService`。
- Produces:
  - `文字RPG.Facilities.FacilityContext { 玩家档案 玩家; EventBus 事件; DataService 数据; DialogueService 对话; string 返回节点; }`
  - `文字RPG.Facilities.设施 : ViewController { string 标识; string 名称; string 视图标识; FacilityContext 上下文; void 返回(); }`
  - `文字RPG.Facilities.设施工厂`：`static void 装配全部(DataService)`（反射实例化每个定义）
  - `文字RPG.Facilities.设施注册表`：`static void 注册/获取/打开(string 标识, string 返回节点)`

- [ ] **Step 1: 写 FacilityContext + 设施基类**

创建 `Assets/Scripts/Facilities/FacilityContext.cs`：
```csharp
using 文字RPG.Data;
using 文字RPG.Domain;
using 文字RPG.Core;
using 文字RPG.Services;

namespace 文字RPG.Facilities
{
    // 设施上下文：进入设施时注入的共享数据
    public sealed class FacilityContext
    {
        public 玩家档案 玩家;
        public EventBus 事件;
        public DataService 数据;
        public DialogueService 对话;
        public string 返回节点;   // 退出设施后回到的剧情节点
    }
}
```

创建 `Assets/Scripts/Facilities/设施.cs`：
```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;
using 文字RPG.UI;

namespace 文字RPG.Facilities
{
    // 设施基类：既是视图控制器，也是设施逻辑。三块积木之一（逻辑积木）。
    // 绑定视图 时用 上下文 数据填充面板；返回() 回到剧情节点。
    public abstract class 设施 : ViewController
    {
        public string 标识 { get; internal set; }
        public string 名称 { get; internal set; }
        public string 视图标识 { get; internal set; }

        protected FacilityContext 上下文;

        // 设施进入：由 ViewManager.打开实例 先初始化(根,事件)，随后本方法注入上下文并绑定设施数据
        public void 进入(FacilityContext 上下文)
        {
            this.上下文 = 上下文;
            绑定设施();
        }

        // 子类：从 上下文 读数据填充面板（如商店商品列表）
        protected virtual void 绑定设施() { }

        // 返回：关视图，回到剧情节点（重新显示选项）
        public void 返回()
        {
            ViewManager.返回();
            if (上下文 != null && 上下文.对话 != null && !string.IsNullOrEmpty(上下文.返回节点))
                上下文.对话.进入节点(上下文.返回节点);
        }
    }
}
```

> 注：设施实例由 `ViewManager.打开实例` 复用（同一设施单例），不每次新建。

- [ ] **Step 2: 写 ViewManager.打开实例**

在 `ViewManager` 追加：
```csharp
// 打开视图：复用给定控制器实例（设施用，避免每次新建）
public static void 打开实例(ViewController 控制器, string 视图标识)
{
    if (宿主 == null) { Debug.LogError("[ViewManager] 未初始化"); return; }
    var 模板 = Resources.Load<VisualTreeAsset>($"UI/{视图标识}");
    if (模板 == null) { Debug.LogError($"[ViewManager] 找不到 UXML: {视图标识}"); return; }
    var 元素 = 模板.CloneTree();
    宿主.Add(元素);
    控制器.初始化(元素, ServiceRegistry.Get<EventBus>());
    栈.Push(控制器);
}
```

- [ ] **Step 3: 写设施工厂 + 注册表**

创建 `Assets/Scripts/Facilities/设施注册表.cs`：
```csharp
using System;
using System.Collections.Generic;
using 文字RPG.Core;
using 文字RPG.Data;
using 文字RPG.Services;
using 文字RPG.UI;

namespace 文字RPG.Facilities
{
    // 设施注册表：标识 -> 设施实例；地点 -> 设施标识列表；打开() 处理进入流程
    public static class 设施注册表
    {
        private static readonly Dictionary<string, 设施> 设施表 = new Dictionary<string, 设施>();
        public static readonly Dictionary<string, List<string>> 地点设施 = new Dictionary<string, List<string>>();

        public static void 注册(设施 实例, 设施定义 定义)
        {
            设施表[定义.标识] = 实例;
            if (定义.地点 == null) return;
            foreach (var 地点 in 定义.地点)
            {
                if (!地点设施.TryGetValue(地点, out var 列表)) { 列表 = new List<string>(); 地点设施[地点] = 列表; }
                if (!列表.Contains(定义.标识)) 列表.Add(定义.标识);
            }
        }

        public static 设施 获取(string 标识) => 设施表.TryGetValue(标识, out var 设施) ? 设施 : null;

        public static List<string> 地点设施列表(string 地点标识) => 地点设施.TryGetValue(地点标识, out var 列表) ? 列表 : new List<string>();

        // 打开设施：注入上下文 → 打开实例视图
        public static void 打开(string 标识, string 返回节点)
        {
            var 设施 = 获取(标识);
            if (设施 == null) { UnityEngine.Debug.LogError($"[设施注册表] 设施不存在: {标识}"); return; }
            设施.进入(new FacilityContext
            {
                玩家 = ServiceRegistry.Get<PlayerService>().档案,
                事件 = ServiceRegistry.Get<EventBus>(),
                数据 = ServiceRegistry.Get<DataService>(),
                对话 = ServiceRegistry.Get<DialogueService>(),
                返回节点 = 返回节点
            });
            ViewManager.打开实例(设施, 设施.视图标识);
        }
    }
}
```

创建 `Assets/Scripts/Facilities/设施工厂.cs`：
```csharp
using System;
using UnityEngine;
using 文字RPG.Data;

namespace 文字RPG.Facilities
{
    // 设施工厂：按 逻辑类型 反射实例化每个设施定义。新增设施不改工厂代码。
    public static class 设施工厂
    {
        public static void 装配全部(DataService 数据)
        {
            foreach (var 定义 in 数据.设施.Values)
            {
                var 类型 = Type.GetType($"文字RPG.Facilities.{定义.逻辑类型}, Assembly-CSharp");
                if (类型 == null) { Debug.LogError($"[设施工厂] 找不到设施类型: {定义.逻辑类型}"); continue; }
                if (Activator.CreateInstance(类型) is not 设施 实例) { Debug.LogError($"[设施工厂] {定义.逻辑类型} 不是设施子类"); continue; }
                实例.标识 = 定义.标识;
                实例.名称 = 定义.名称;
                实例.视图标识 = 定义.视图;
                设施注册表.注册(实例, 定义);
            }
        }
    }
}
```

> 注：`逻辑类型` 为简单类名（如 `商店设施`），工厂按 `文字RPG.Facilities.{简单名}` + Assembly-CSharp 反射。若类在其它命名空间，改 `类型.GetType(定义.逻辑类型 + ", Assembly-CSharp")` 并让 JSON 写全名。

- [ ] **Step 4: 装配设施工厂**

在 `GameBootstrap.装配()` 末尾（对话装配后）追加：
```csharp
// 设施框架：工厂装配全部设施（定义在 facilities.json）
设施工厂.装配全部(数据);
```

- [ ] **Step 5: 验证**

刷新编译零错误（此时 facilities.json 尚无设施，工厂空跑无副作用）。

- [ ] **Step 6: 记录变更**

改动文件：`Facilities/设施.cs`、`FacilityContext.cs`、`设施工厂.cs`、`设施注册表.cs`、`UI/ViewManager.cs`、`Core/GameBootstrap.cs`。

---

### Task 2: 商店设施（三块积木完整示范）+ 地点视图

**Files:**
- Create: `Assets/Resources/Data/facilities.json`（商店/训练场/任务板/酒店 定义）
- Create: `Assets/Scripts/Facilities/商店设施.cs`
- Create: `Assets/Resources/UI/facilities/商店.uxml`
- Create: `Assets/Resources/UI/facilities/地点.uxml`
- Create: `Assets/Scripts/UI/地点控制器.cs`（地点视图：设施入口卡片墙）
- Modify: `Assets/Scripts/UI/ViewRegistry.cs`（注册 地点 视图）

**Interfaces:**
- Consumes: `设施` 基类、`InventoryService`、`DataService`、事件。
- Produces: `商店设施 : 设施`——`绑定设施()` 填商品列表；`处理购买(物品标识)`；卖 `价格>0 且 类型!="任务"` 的物品。`地点控制器`——展示某地点的设施入口（`设施注册表.地点设施列表`），点击调 `设施注册表.打开(标识, 当前节点)`。

- [ ] **Step 1: 写 facilities.json**

创建 `Assets/Resources/Data/facilities.json`：
```json
{
  "设施": [
    { "标识": "商店", "名称": "酒馆商店", "描述": "购买补给与装备，也收旧货。", "逻辑类型": "商店设施", "视图": "facilities/商店", "地点": ["灰烬镇"] }
  ]
}
```
> 训练场/任务板/酒店 留待 M4B（先只注册商店，避免未实现设施类型报错）。

- [ ] **Step 2: 写商店 UXML**

创建 `Assets/Resources/UI/facilities/商店.uxml`：
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:VisualElement name="商店" class="面板" style="flex-grow: 1;">
        <ui:Label name="标题" class="label label-金" style="font-size: 32px; margin-bottom: 8px;" />
        <ui:ScrollView style="flex-grow: 1;">
            <ui:VisualElement name="商品列表" style="flex-direction: column;" />
        </ui:ScrollView>
        <ui:Button name="返回按钮" text="返回酒馆" class="按钮 按钮-次" style="margin-top: 12px;" />
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 3: 写商店设施**

创建 `Assets/Scripts/Facilities/商店设施.cs`：
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using 文字RPG.Core;
using 文字RPG.Data;
using 文字RPG.Events;
using 文字RPG.Services;

namespace 文字RPG.Facilities
{
    // 商店设施：三块积木之「逻辑积木」。卖所有 价格>0 且非任务 的物品。
    public sealed class 商店设施 : 设施
    {
        private VisualElement 商品列表;

        protected override void 绑定视图()
        {
            根.Q<Label>("标题").text = "—— 酒馆 · 补给 ——";
            商品列表 = 根.Q<VisualElement>("商品列表");
            根.Q<Button>("返回按钮").clicked += 返回;

            事件.订阅<金币变化事件>(_ => 重建商品列表());   // 金钱变化刷新可购状态
            重建商品列表();
        }

        protected override void 绑定设施() { }

        // 重建商品行（克隆 controls/物品条目 模板）
        private void 重建商品列表()
        {
            商品列表.Clear();
            foreach (var 物品 in 上下文.数据.物品.Values)
            {
                if (物品.价格 <= 0 || 物品.类型 == "任务") continue;
                var 模板 = Resources.Load<VisualTreeAsset>("UI/controls/物品条目");
                var 行 = 模板.CloneTree();
                行.Q<Label>("物品名").text = $"{物品.名称}（{物品.描述}）  {物品.价格}金";
                行.Q<Label>("物品数量").text = "购买";
                var 标识 = 物品.标识;
                行.RegisterCallback<ClickEvent>(_ => 处理购买(标识));
                商品列表.Add(行);
            }
        }

        private void 处理购买(string 物品标识)
        {
            if (!上下文.数据.物品.TryGetValue(物品标识, out var 物品)) return;
            var 玩家 = 上下文.玩家;
            if (玩家.金币 < 物品.价格)
            {
                事件.发布(new 日志事件(文字RPG.Events.日志类型.反馈坏, $"金币不足（需要 {物品.价格}）。"));
                return;
            }
            玩家.金币 -= 物品.价格;
            if (物品.类型 == "武器") 玩家.武器标识 = 物品.标识;
            else if (物品.类型 == "防具") 玩家.防具标识 = 物品.标识;
            else 玩家.添加物品(物品.标识);
            事件.发布(new 金币变化事件(玩家.金币, -物品.价格));
            事件.发布(new 日志事件(文字RPG.Events.日志类型.反馈, $"购买 {物品.名称}（-{物品.价格} 金币）"));
        }
    }
}
```
> 注：`事件.订阅` 用基类 `ViewController.订阅<T>`（自动解绑）。`返回()` 由基类实现。

- [ ] **Step 4: 写地点视图**

创建 `Assets/Resources/UI/facilities/地点.uxml`：
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:VisualElement name="地点" class="面板" style="flex-grow: 1;">
        <ui:Label name="标题" class="label label-金" style="font-size: 32px; margin-bottom: 12px;" />
        <ui:ScrollView style="flex-grow: 1;">
            <ui:VisualElement name="入口列表" style="flex-direction: column; gap: 8px;" />
        </ui:ScrollView>
    </ui:VisualElement>
</ui:UXML>
```

创建 `Assets/Scripts/UI/地点控制器.cs`：
```csharp
using UnityEngine;
using UnityEngine.UIElements;
using 文字RPG.Core;
using 文字RPG.Facilities;
using 文字RPG.Services;

namespace 文字RPG.UI
{
    // 地点视图：展示该地点的设施入口（数据驱动），点击打开设施
    public sealed class 地点控制器 : ViewController
    {
        private VisualElement 入口列表;

        protected override void 绑定视图()
        {
            根.Q<Label>("标题").text = "—— 灰烬镇 ——";
            入口列表 = 根.Q<VisualElement>("入口列表");
            var 当前节点 = ServiceRegistry.Get<PlayerService>().档案.当前节点;

            foreach (var 标识 in 设施注册表.地点设施列表("灰烬镇"))
            {
                var 设施 = 设施注册表.获取(标识);
                if (设施 == null) continue;
                var 模板 = Resources.Load<VisualTreeAsset>("UI/controls/按钮");
                var 按钮 = 模板.CloneTree();
                按钮.Q<Label>("文字").text = 设施.名称;
                var 目标 = 标识;
                按钮.RegisterCallback<ClickEvent>(_ => 设施注册表.打开(目标, 当前节点));
                入口列表.Add(按钮);
            }
        }
    }
}
```

- [ ] **Step 5: 注册地点视图 + 剧情接入口**

在 `UI引导` 的注册处追加：`ViewRegistry.注册("地点", "facilities/地点", typeof(地点控制器));`
在 `story.json` 的「灰烬镇」节点选项里把「前往酒馆『醉猫』」改为 `设施:商店`（Step 见 Task 3 剧情改造）。

- [ ] **Step 6: 验证**

刷新编译零错误。Play 观察：灰烬镇选项 → 「前往酒馆」→ 商店视图（商品列表含 治疗药水/木剑/铁剑/皮甲/链甲 + 返回按钮）；点购买 → 金币扣减、HUD 刷新；返回 → 回到灰烬镇选项。

- [ ] **Step 7: 记录变更**

改动文件：`Resources/Data/facilities.json`、`Facilities/商店设施.cs`、`Resources/UI/facilities/商店.uxml`、`地点.uxml`、`UI/地点控制器.cs`、`UI/ViewRegistry.cs`、`UI/UI引导.cs`。

---

### Task 3: 剧情接设施 + 战斗视图

**Files:**
- Modify: `Assets/Resources/Data/story.json`（特殊目标 → `设施:`；去 `__出售` 等）
- Modify: `Assets/Scripts/Services/DialogueService.cs`（`设施:` 目标 → `设施注册表.打开`）
- Create: `Assets/Resources/UI/facilities/战斗.uxml`
- Create: `Assets/Scripts/UI/战斗控制器.cs`
- Modify: `Assets/Scripts/Core/GameBootstrap.cs`（`设施:` 目标由 DialogueService 处理）

**Interfaces:**
- Consumes: `BattleService`、`设施注册表`、事件。
- Produces: 剧情可经 `设施:商店` 进商店；战斗选项经 `战斗:敌人:胜利节点` 打开战斗视图。

- [ ] **Step 1: 改 DialogueService 处理设施目标**

在 `处理选项` 中，把 `设施:` 分支改为：
```csharp
if (目标.StartsWith("设施:"))
{
    设施注册表.打开(目标.Substring(3), 玩家.档案.当前节点);
    return;
}
```
（`设施注册表` 需 `using 文字RPG.Facilities;`）

- [ ] **Step 2: 改 story.json 特殊目标**

将以下选项目标替换（旧剧情引擎遗留指令 → `设施:`）：
- `酒馆_主` 的「购买补给」→ `设施:商店`；「出售物品」`__出售` → `设施:商店`（商店含出售展示，M4B 完善）；「购买防具」`酒馆_防具` → `设施:商店`
- 删除/保留 `酒馆_购买`、`酒馆_防具` 节点（保留无害，入口不再指向）
- `训练场` 的 `学习:xxx` → 暂保留（M4B 做训练场设施）；「接取悬赏」`任务:xxx` 暂保留
- 「前往酒馆『醉猫』」`酒馆_主` → 保留（酒馆_主 仍指向 设施:商店）

> 本任务只把商店相关目标接到 `设施:商店`；训练/任务 M4B 处理。

- [ ] **Step 3: 写战斗视图**

创建 `Assets/Resources/UI/facilities/战斗.uxml`：
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:VisualElement name="战斗" class="面板" style="flex-grow: 1;">
        <ui:Label name="状态行" class="label label-危险" style="margin-bottom: 8px;" />
        <ui:Label name="消息" class="label" style="flex-grow: 1; white-space: normal;" />
        <ui:VisualElement name="行动区" style="flex-direction: column; gap: 8px; margin-top: 12px;" />
    </ui:VisualElement>
</ui:UXML>
```

创建 `Assets/Scripts/UI/战斗控制器.cs`：
```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;
using 文字RPG.Core;
using 文字RPG.Domain;
using 文字RPG.Services;

namespace 文字RPG.UI
{
    // 战斗视图：接 BattleService。行动区按钮（攻击/技能/逃跑）驱动回合。
    public sealed class 战斗控制器 : ViewController
    {
        private Label 状态行, 消息;
        private VisualElement 行动区;

        protected override void 绑定视图()
        {
            状态行 = 根.Q<Label>("状态行");
            消息 = 根.Q<Label>("消息");
            行动区 = 根.Q<VisualElement>("行动区");
            显示主行动();
        }

        private void 显示主行动()
        {
            行动区.Clear();
            添加行动按钮("攻击", () => ServiceRegistry.Get<BattleService>().玩家攻击());
            添加行动按钮("逃跑", () => ViewManager.返回());
        }

        private void 添加行动按钮(string 文本, Action 点击)
        {
            var 模板 = Resources.Load<VisualTreeAsset>("UI/controls/按钮");
            var 按钮 = 模板.CloneTree();
            按钮.Q<Label>("文字").text = 文本;
            UI反馈.挂载按钮反馈(按钮);
            按钮.RegisterCallback<ClickEvent>(_ => 点击());
            行动区.Add(按钮);
        }
    }
}
```

> 注：战斗视图为 M4 简化版（攻击/逃跑）。技能/道具/敌人回合渲染/胜利结算 M4B 完善。DialogueService 的 `战斗:` 目标需改为打开战斗视图（当前 M3 是占位跳胜利节点）——本任务改为：`战斗:` → 打开战斗视图，胜利后回胜利节点（由 BattleService 发 战斗结束事件 驱动，M4B 完善闭环）。

- [ ] **Step 4: 注册战斗视图 + 装配**

`UI引导` 注册：`ViewRegistry.注册("战斗", "facilities/战斗", typeof(战斗控制器));`
`GameBootstrap.装配()` 无需额外（BattleService 已装配）。

- [ ] **Step 5: 验证**

刷新编译零错误。Play：灰烬镇 → 酒馆商店购买/返回；序章 →「追上去」开战斗 → 攻击/逃跑（M4 简化，胜利跳节点逻辑占位）；控制台无新框架报错。

- [ ] **Step 6: 记录变更**

改动文件：`Resources/Data/story.json`、`Services/DialogueService.cs`、`Resources/UI/facilities/战斗.uxml`、`UI/战斗控制器.cs`、`UI/UI引导.cs`。

---

## M4 完成判定

- [x] 设施框架就绪：`设施` 基类 + 工厂（反射）+ 注册表（标识/地点索引）+ `ViewManager.打开实例`。
- [x] 商店设施三块积木落地：facilities.json 一行 + `商店设施.cs` + `商店.uxml`；新增设施不改核心。
- [x] 剧情经 `设施:商店` 进商店，购买/返回可用，HUD 联动。
- [x] 战斗视图打开（攻击/逃跑），BattleService 驱动（胜利→胜利节点，逃跑→返回节点）。
- [x] 编译零错误；Play 新框架无报错（装配日志 `设施 1`）；旧游戏既有报错忽略。
- [x] 无代码生成 UI；逻辑永不调 UI。

**实施要点**：
1. `DialogueService.处理选项` 接 `设施:`（→设施注册表.打开）、`战斗:`（→BattleService.开始战斗+开战斗视图）、`__地点`（→地点视图）。
2. `BattleService` 暴露 `胜利节点/返回节点`，战斗控制器据此回剧情。
3. story.json `酒馆_主` 的 购买补给/购买防具/出售物品 → `设施:商店`。
4. 训练场(学习:)/任务(任务:)/探索/区域 目标仍为占位，M4B 实现。

## M4B（本计划之外，另立）

训练场设施 / 任务板设施 / 酒店设施 / 探索视图 / 商店出售 / 战斗完整回合(技能/道具/敌人回合/胜利结算) / 地点地图 / 场景切换删旧代码 / 主菜单与结局界面。
