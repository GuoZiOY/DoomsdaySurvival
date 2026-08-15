# M1 核心骨架 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 搭建积木化框架的地基——事件总线、服务注册表、纯 C# 领域模型、数据服务、存档服务、组合根。**无自动化测试**：验证方式 = 编译零错误 + GameBootstrap 装配日志 + 后续里程碑行为观察。

**Architecture:** 新框架代码直接放进 `Assets/Scripts/` 下新增子目录 `Core/ Data/ Domain/ Services/ Events/`（后置里程碑加 `Facilities/ UI/`），与旧代码（`核心/ 数据/ 系统/ 界面/`）**同处默认程序集 Assembly-CSharp**，靠子命名空间 `奇幻文字RPG.Core / .Data / .Domain / .Services / .Events` 分层隔离。**不做任何 asmdef**——新旧代码并存互不干扰，旧游戏照常运行，直至 M4 切换场景后删除旧代码。领域层（Domain）纯 C# 零 UnityEngine 依赖，逻辑只发事件、UI（后置里程碑）只订阅事件。

**DOTween 可用性：** 新旧代码同在 Assembly-CSharp，而 DOTween 在 `Assets/Plugins/`（firstpass 程序集）——预定义程序集自动引用 firstpass，故**新代码可直接 `using DG.Tweening;`**。UI 反馈动画（面板显隐/切换、按钮点击弹性）在 M2+ 用 DOTween + 插件的 `DOTweenModuleUIToolkit`（项目已含）补间 VisualElement；M1 纯逻辑层用不到，但可用。

**Tech Stack:** C#（Unity 6000.4.8f1，C# 9 语法内）、JSON（JsonUtility，字段名保持中文与旧档兼容）、DOTween（仅 UI 层用）。

## Global Constraints

- Unity 6000.4.8f1，项目根 `E:\UNITY GAME\文字奇幻rpg`。
- 命名空间：新框架用**独立根命名空间 `文字RPG.Core / .Data / .Domain / .Services / .Events`**（旧代码占着 `奇幻文字RPG` 根，避免 ~20 个同名类型遮蔽冲突）。**M6 删除旧代码后，新框架转成「无 namespace」**（全局命名空间 + 文件夹分层，参照 类银河恶魔城 项目风格）——一次性移除全部 `namespace` 声明即可。
- **领域层零 UnityEngine 依赖**：`Domain/` 下代码只允许 `using System.*`。
- 数据仍为 JSON，位于 `Assets/Resources/Data/`，字段名沿用现有 schema（中文）。
- 本机**无 git 仓库**：无 VCS，各任务末尾「提交」步骤改为「记录变更」（列改动文件即可）。
- **无自动化测试**：各任务验证 = 编译零错误（`read_console` 无 error）+ 控制台日志观察；GameBootstrap 的任务在场景内 Play 一次验证装配日志。
- 每次新建/编辑脚本后等待 Unity 编译完成（`mcpforunity://editor/state` → `is_compiling==false`）再验证。

---

### Task 1: 事件定义 + 事件总线 EventBus

**Files:**
- Create: `Assets/Scripts/Events/事件定义.cs`
- Create: `Assets/Scripts/Core/EventBus.cs`

**Interfaces:**
- Consumes: 无。
- Produces:
  - 命名空间 `奇幻文字RPG.Events`：`日志类型` 枚举、`变化原因` 枚举；`生命变化事件`、`魔力变化事件`、`金币变化事件`、`经验变化事件`、`背包变化事件`、`任务进度事件`、`任务完成事件`、`日志事件`、`战斗结束事件`（均 `readonly struct`）。
  - `奇幻文字RPG.Core.EventBus`：`void 订阅<T>(Action<T>)`、`void 取消订阅<T>(Action<T>)`、`void 发布<T>(T)`（单订阅者异常不影响其他、不中断发布）。

- [ ] **Step 1: 写事件定义**

创建 `Assets/Scripts/Events/事件定义.cs`：
```csharp
namespace 奇幻文字RPG.Events
{
    /// <summary>日志类型：决定前缀与颜色。</summary>
    public enum 日志类型 { 系统, 剧情, 操作, 反馈, 反馈坏, 内心 }

    /// <summary>背包变化原因。</summary>
    public enum 变化原因 { 获得, 失去, 消耗, 出售, 购买 }

    // —— 玩家状态 ——

    public readonly struct 生命变化事件
    {
        public readonly int 当前; public readonly int 最大; public readonly int 变化量;
        public 生命变化事件(int 当前, int 最大, int 变化量) { this.当前 = 当前; this.最大 = 最大; this.变化量 = 变化量; }
    }

    public readonly struct 魔力变化事件
    {
        public readonly int 当前; public readonly int 最大; public readonly int 变化量;
        public 魔力变化事件(int 当前, int 最大, int 变化量) { this.当前 = 当前; this.最大 = 最大; this.变化量 = 变化量; }
    }

    public readonly struct 金币变化事件
    {
        public readonly int 当前; public readonly int 变化量;
        public 金币变化事件(int 当前, int 变化量) { this.当前 = 当前; this.变化量 = 变化量; }
    }

    public readonly struct 经验变化事件
    {
        public readonly int 等级; public readonly int 当前经验; public readonly int 升级所需; public readonly bool 升级了;
        public 经验变化事件(int 等级, int 当前经验, int 升级所需, bool 升级了)
        { this.等级 = 等级; this.当前经验 = 当前经验; this.升级所需 = 升级所需; this.升级了 = 升级了; }
    }

    // —— 背包 ——

    public readonly struct 背包变化事件
    {
        public readonly string 物品标识; public readonly int 数量; public readonly 变化原因 原因;
        public 背包变化事件(string 物品标识, int 数量, 变化原因 原因) { this.物品标识 = 物品标识; this.数量 = 数量; this.原因 = 原因; }
    }

    // —— 任务 ——

    public readonly struct 任务进度事件
    {
        public readonly string 任务标识; public readonly int 进度; public readonly int 目标; public readonly bool 已完成;
        public 任务进度事件(string 任务标识, int 进度, int 目标, bool 已完成)
        { this.任务标识 = 任务标识; this.进度 = 进度; this.目标 = 目标; this.已完成 = 已完成; }
    }

    public readonly struct 任务完成事件
    {
        public readonly string 任务标识; public readonly string 奖励文本;
        public 任务完成事件(string 任务标识, string 奖励文本) { this.任务标识 = 任务标识; this.奖励文本 = 奖励文本; }
    }

    // —— 日志 ——

    public readonly struct 日志事件
    {
        public readonly 日志类型 类型; public readonly string 文本;
        public 日志事件(日志类型 类型, string 文本) { this.类型 = 类型; this.文本 = 文本; }
    }

    // —— 战斗 ——

    public readonly struct 战斗结束事件
    {
        public readonly bool 胜利; public readonly string 敌人标识;
        public 战斗结束事件(bool 胜利, string 敌人标识) { this.胜利 = 胜利; this.敌人标识 = 敌人标识; }
    }
}
```

- [ ] **Step 2: 写 EventBus**

创建 `Assets/Scripts/Core/EventBus.cs`：
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace 奇幻文字RPG.Core
{
    /// <summary>类型化事件总线：订阅/发布/解绑；发布时单订阅者异常不影响其他、不中断发布。</summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _订阅表 = new Dictionary<Type, List<Delegate>>();

        public void 订阅<T>(Action<T> 处理)
        {
            if (!_订阅表.TryGetValue(typeof(T), out var 列表))
            {
                列表 = new List<Delegate>();
                _订阅表[typeof(T)] = 列表;
            }
            列表.Add(处理);
        }

        public void 取消订阅<T>(Action<T> 处理)
        {
            if (_订阅表.TryGetValue(typeof(T), out var 列表))
                列表.Remove(处理);
        }

        public void 发布<T>(T 事件)
        {
            if (!_订阅表.TryGetValue(typeof(T), out var 列表)) return;
            var 快照 = 列表.ToArray();   // 快照：允许订阅者在回调里改订阅表
            foreach (var 处理 in 快照)
            {
                try { ((Action<T>)处理)(事件); }
                catch (Exception 异常) { Debug.LogError($"[EventBus] 订阅者异常: {异常}"); }
            }
        }
    }
}
```

- [ ] **Step 3: 验证**

等待编译完成，`read_console(types=["error"], count=10)` → Expected: 无 error。
行为验证留待 M2+（UI 订阅事件）。

- [ ] **Step 4: 记录变更**

改动文件：`Events/事件定义.cs`、`Core/EventBus.cs`。

---

### Task 2: 服务注册表 ServiceRegistry

**Files:**
- Create: `Assets/Scripts/Core/ServiceRegistry.cs`

**Interfaces:**
- Consumes: 无。
- Produces: `奇幻文字RPG.Core.ServiceRegistry` 静态类：`static void Register<T>(T)`、`static T Get<T>()`、`static bool 已注册<T>()`、`static void 清除()`（测试/装配重载用）。

- [ ] **Step 1: 写实现**

创建 `Assets/Scripts/Core/ServiceRegistry.cs`：
```csharp
using System;
using System.Collections.Generic;

namespace 奇幻文字RPG.Core
{
    /// <summary>服务注册表：显式依赖的装配中心。清除() 供装配重载。</summary>
    public static class ServiceRegistry
    {
        private static readonly Dictionary<Type, object> _服务 = new Dictionary<Type, object>();

        public static void Register<T>(T 服务)
        {
            if (_服务.ContainsKey(typeof(T)))
                throw new InvalidOperationException($"[ServiceRegistry] 服务已注册: {typeof(T).Name}");
            _服务[typeof(T)] = 服务;
        }

        public static T Get<T>()
        {
            if (_服务.TryGetValue(typeof(T), out var 服务)) return (T)服务;
            throw new InvalidOperationException($"[ServiceRegistry] 未注册服务: {typeof(T).Name}");
        }

        public static bool 已注册<T>() => _服务.ContainsKey(typeof(T));

        public static void 清除() => _服务.Clear();
    }
}
```

- [ ] **Step 2: 验证**

等待编译完成，`read_console(types=["error"], count=10)` → Expected: 无 error。

- [ ] **Step 3: 记录变更**

改动文件：`Core/ServiceRegistry.cs`。

---

### Task 3: 数据模型（奇幻文字RPG.Data）

**Files:**
- Create: `Assets/Scripts/Data/数据模型.cs`

**Interfaces:**
- Consumes: 无。
- Produces（全部 `[Serializable]` 纯 C#，字段名与旧 `数据/数据模型.cs` 完全一致以便直接反序列化现有 JSON）：`剧情节点/剧情选项/剧情效果/剧情根`、`敌人数据/敌人根`、`物品数据/物品根`、`技能数据/技能根`、`任务数据/任务根`、`地图地点/地图根`、`区域数据/遭遇项/发现项/资源项/区域根`、`设施定义/设施根`。

- [ ] **Step 1: 写数据模型**

创建 `Assets/Scripts/Data/数据模型.cs`：
```csharp
using System;

namespace 奇幻文字RPG.Data
{
    // ================= 剧情 =================

    [Serializable]
    public class 剧情效果
    {
        public int 生命;
        public int 魔力;
        public int 金币;
        public int 经验;
        public string 获得物品;
        public string 失去物品;
    }

    [Serializable]
    public class 剧情选项
    {
        public string 文本;
        public string 目标;
        public string 需要物品;
        public 剧情效果 效果;
    }

    [Serializable]
    public class 剧情节点
    {
        public string 标识;
        public string 文本;
        public 剧情效果 效果;
        public string 战斗;
        public string 面板;
        public 剧情选项[] 选项;
    }

    [Serializable]
    public class 剧情根 { public 剧情节点[] 节点; }

    // ================= 敌人 =================

    [Serializable]
    public class 敌人数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public int 生命;
        public int 攻击;
        public int 防御;
        public int 金币奖励;
        public int 经验奖励;
        public string 掉落物品;
        public float 掉落概率;
    }

    [Serializable]
    public class 敌人根 { public 敌人数据[] 敌人; }

    // ================= 物品 =================

    [Serializable]
    public class 物品数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public string 类型;
        public int 恢复量;
        public int 攻击加成;
        public int 防御加成;
        public int 价格;
    }

    [Serializable]
    public class 物品根 { public 物品数据[] 物品; }

    // ================= 技能 =================

    [Serializable]
    public class 技能数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public int 消耗魔力;
        public string 类型;
        public int 数值;
        public int 价格;
    }

    [Serializable]
    public class 技能根 { public 技能数据[] 技能; }

    // ================= 任务 =================

    [Serializable]
    public class 任务数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public string 目标类型;
        public string 目标标识;
        public int 目标数量;
        public int 奖励金币;
        public int 奖励经验;
        public string 奖励物品;
    }

    [Serializable]
    public class 任务根 { public 任务数据[] 任务; }

    // ================= 地图 =================

    [Serializable]
    public class 地图地点
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public float x;
        public float y;
        public string 目标;
        public string 解锁物品;
        public string[] 连接;
    }

    [Serializable]
    public class 地图根 { public 地图地点[] 地点; }

    // ================= 区域 =================

    [Serializable]
    public class 遭遇项 { public string 敌人; public int 权重; }

    [Serializable]
    public class 发现项 { public string 节点; public int 权重; }

    [Serializable]
    public class 资源项 { public 剧情效果 效果; public string 文本; public int 权重; }

    [Serializable]
    public class 区域数据
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public int 危险度;
        public 遭遇项[] 遭遇;
        public 发现项[] 发现;
        public 资源项[] 资源;
        public string[] 无事文本;
    }

    [Serializable]
    public class 区域根 { public 区域数据[] 区域; }

    // ================= 设施 =================

    [Serializable]
    public class 设施定义
    {
        public string 标识;
        public string 名称;
        public string 描述;
        public string 逻辑类型;
        public string 视图;
        public string 数据;
        public string[] 地点;
    }

    [Serializable]
    public class 设施根 { public 设施定义[] 设施; }
}
```

- [ ] **Step 2: 验证**

等待编译完成，`read_console(types=["error"], count=10)` → Expected: 无 error（注意与旧 `数据/数据模型.cs` 的类名不在同一命名空间，无冲突）。

- [ ] **Step 3: 记录变更**

改动文件：`Data/数据模型.cs`。

---

### Task 4: 领域模型 — 玩家档案 PlayerProfile

**Files:**
- Create: `Assets/Scripts/Domain/玩家档案.cs`

**Interfaces:**
- Consumes: 无（纯 C#，零 UnityEngine）。
- Produces: `奇幻文字RPG.Domain.玩家档案`（`[Serializable]`，字段名与旧 `玩家状态` 一致）、`物品堆叠`、`任务进度`。注入属性 `武器攻击解析`/`防具防御解析`（`Func<string,int>`，装配层注入后计算 `总攻击`/`总防御`）。

- [ ] **Step 1: 写实现**

创建 `Assets/Scripts/Domain/玩家档案.cs`：
```csharp
using System;
using System.Collections.Generic;

namespace 奇幻文字RPG.Domain
{
    [Serializable]
    public class 物品堆叠
    {
        public string 标识;
        public int 数量;
        public 物品堆叠() { }
        public 物品堆叠(string 标识, int 数量) { this.标识 = 标识; this.数量 = 数量; }
    }

    [Serializable]
    public class 任务进度
    {
        public string 标识;
        public int 数量;
        public bool 已完成;
    }

    /// <summary>玩家档案：纯 C# 领域模型（零 UnityEngine），可整体序列化存档。</summary>
    [Serializable]
    public class 玩家档案
    {
        // —— 注入：装备加成解析器（由装配层接到 DataService）——
        [NonSerialized] public Func<string, int> 武器攻击解析;
        [NonSerialized] public Func<string, int> 防具防御解析;

        // —— 属性 ——
        public int 等级 = 1;
        public int 经验 = 0;
        public int 最大生命 = 20;
        public int 生命 = 20;
        public int 攻击 = 5;
        public int 防御 = 2;
        public int 金币 = 3;
        public int 最大魔力 = 20;
        public int 魔力 = 20;
        public float 游戏分钟数 = 420f;
        public string 武器标识 = "生锈短剑";
        public string 防具标识 = "";
        public string 当前节点 = "";
        public List<物品堆叠> 背包 = new List<物品堆叠>();
        public List<string> 已学技能 = new List<string>();
        public List<任务进度> 任务 = new List<任务进度>();

        public int 升级所需经验 => 等级 * 25;

        private static int 夹(int 值, int 最小, int 最大) => 值 < 最小 ? 最小 : (值 > 最大 ? 最大 : 值);

        // ---------- 背包 ----------

        public void 添加物品(string 标识, int 数量 = 1)
        {
            if (string.IsNullOrEmpty(标识)) return;
            foreach (var 堆叠 in 背包)
                if (堆叠.标识 == 标识) { 堆叠.数量 += 数量; return; }
            背包.Add(new 物品堆叠(标识, 数量));
        }

        public bool 移除物品(string 标识, int 数量 = 1)
        {
            if (string.IsNullOrEmpty(标识)) return false;
            for (int i = 0; i < 背包.Count; i++)
            {
                var 堆叠 = 背包[i];
                if (堆叠.标识 == 标识 && 堆叠.数量 >= 数量)
                {
                    堆叠.数量 -= 数量;
                    if (堆叠.数量 <= 0) 背包.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public int 物品数量(string 标识)
        {
            if (string.IsNullOrEmpty(标识)) return 0;
            foreach (var 堆叠 in 背包) if (堆叠.标识 == 标识) return 堆叠.数量;
            return 0;
        }

        public bool 持有物品(string 标识) => 物品数量(标识) > 0;

        // ---------- 数值 ----------

        public void 恢复生命(int 数值) => 生命 = 夹(生命 + 数值, 0, 最大生命);
        public void 受到伤害(int 数值) => 生命 = Math.Max(0, 生命 - 数值);
        public void 恢复魔力(int 数值) => 魔力 = 夹(魔力 + 数值, 0, 最大魔力);

        public bool 消耗魔力(int 数值)
        {
            if (魔力 < 数值) return false;
            魔力 -= 数值;
            return true;
        }

        // ---------- 装备 ----------

        public int 总攻击 => 攻击 + (武器攻击解析?.Invoke(武器标识) ?? 0);
        public int 总防御 => 防御 + (防具防御解析?.Invoke(防具标识) ?? 0);

        // ---------- 技能 ----------

        public bool 掌握技能(string 标识) => 已学技能.Contains(标识);

        public bool 学习技能(string 标识)
        {
            if (掌握技能(标识)) return false;
            已学技能.Add(标识);
            return true;
        }

        // ---------- 任务 ----------

        public bool 添加任务(string 标识)
        {
            foreach (var 进度 in 任务) if (进度.标识 == 标识) return false;
            任务.Add(new 任务进度 { 标识 = 标识, 数量 = 0, 已完成 = false });
            return true;
        }

        /// <summary>推进「击败」类任务计数；目标匹配由服务层调用前判定。</summary>
        public List<任务进度> 记录击败(string 敌人标识, int 目标数量 = 1)
        {
            var 完成 = new List<任务进度>();
            foreach (var 进度 in 任务)
            {
                if (进度.已完成) continue;
                if (进度.数量 < 目标数量)
                {
                    进度.数量++;
                    if (进度.数量 >= 目标数量) { 进度.已完成 = true; 完成.Add(进度); }
                }
            }
            return 完成;
        }

        /// <summary>推进「获得物品」类任务计数。</summary>
        public List<任务进度> 记录获得(string 物品标识, int 目标数量 = 1)
        {
            var 完成 = new List<任务进度>();
            foreach (var 进度 in 任务)
            {
                if (进度.已完成) continue;
                if (进度.数量 < 目标数量)
                {
                    进度.数量++;
                    if (进度.数量 >= 目标数量) { 进度.已完成 = true; 完成.Add(进度); }
                }
            }
            return 完成;
        }

        // ---------- 经验 ----------

        public bool 获得经验(int 数值)
        {
            经验 += 数值;
            bool 升级了 = false;
            while (经验 >= 升级所需经验)
            {
                经验 -= 升级所需经验;
                等级++;
                最大生命 += 6;
                攻击 += 1;
                防御 += 1;
                生命 = 最大生命;
                升级了 = true;
            }
            return 升级了;
        }
    }
}
```

- [ ] **Step 2: 验证**

等待编译完成，`read_console(types=["error"], count=10)` → Expected: 无 error。行为验证留待 M3+（服务层接入游戏）。

- [ ] **Step 3: 记录变更**

改动文件：`Domain/玩家档案.cs`。

---

### Task 5: 领域模型 — 战斗规则 + 效果结算

**Files:**
- Create: `Assets/Scripts/Domain/战斗规则.cs`
- Create: `Assets/Scripts/Domain/效果结算.cs`

**Interfaces:**
- Consumes: `奇幻文字RPG.Events.*`、`奇幻文字RPG.Data.剧情效果`、`奇幻文字RPG.Domain.玩家档案`、`奇幻文字RPG.Core.EventBus`。
- Produces:
  - `奇幻文字RPG.Domain.战斗规则`（纯函数）：`int 普通伤害(int 攻, int 防, int 随机掷)`、`int 技能伤害(int 总攻, int 倍率, int 防, int 随机掷)`、`int 防御后伤害(int 原伤害)`。
  - `奇幻文字RPG.Domain.效果结算`：`static void 应用(EventBus, 玩家档案, 剧情效果)`（对生命/魔力/金币/经验/获得/失去物品生效并发布对应事件）。

- [ ] **Step 1: 写战斗规则**

创建 `Assets/Scripts/Domain/战斗规则.cs`：
```csharp
using System;

namespace 奇幻文字RPG.Domain
{
    /// <summary>战斗数值规则：纯函数，伤害公式 max(1, 攻 - 防/2 + 随机掷)。</summary>
    public static class 战斗规则
    {
        public static int 普通伤害(int 攻方攻击, int 守方防御, int 随机掷)
            => Math.Max(1, 攻方攻击 - 守方防御 / 2 + 随机掷);

        public static int 技能伤害(int 总攻击, int 倍率, int 守方防御, int 随机掷)
            => Math.Max(1, 总攻击 * 倍率 - 守方防御 / 2 + 随机掷);

        public static int 防御后伤害(int 原伤害) => Math.Max(1, 原伤害 / 2);
    }
}
```

- [ ] **Step 2: 写效果结算**

创建 `Assets/Scripts/Domain/效果结算.cs`：
```csharp
using 奇幻文字RPG.Core;
using 奇幻文字RPG.Data;
using 奇幻文字RPG.Events;

namespace 奇幻文字RPG.Domain
{
    /// <summary>剧情效果结算：对玩家档案生效并发布对应事件（逻辑不直接碰 UI）。</summary>
    public static class 效果结算
    {
        public static void 应用(EventBus 事件, 玩家档案 玩家, 剧情效果 效果)
        {
            if (效果 == null) return;

            if (效果.生命 != 0)
            {
                玩家.恢复生命(效果.生命);
                事件.发布(new 生命变化事件(玩家.生命, 玩家.最大生命, 效果.生命));
            }

            if (效果.魔力 != 0)
            {
                玩家.恢复魔力(效果.魔力);
                事件.发布(new 魔力变化事件(玩家.魔力, 玩家.最大魔力, 效果.魔力));
            }

            if (效果.金币 != 0)
            {
                玩家.金币 += 效果.金币;
                事件.发布(new 金币变化事件(玩家.金币, 效果.金币));
            }

            if (效果.经验 != 0)
            {
                bool 升级了 = 玩家.获得经验(效果.经验);
                事件.发布(new 经验变化事件(玩家.等级, 玩家.经验, 玩家.升级所需经验, 升级了));
            }

            if (!string.IsNullOrEmpty(效果.获得物品))
            {
                玩家.添加物品(效果.获得物品);
                事件.发布(new 背包变化事件(效果.获得物品, 1, 变化原因.获得));
            }

            if (!string.IsNullOrEmpty(效果.失去物品))
            {
                if (玩家.移除物品(效果.失去物品))
                    事件.发布(new 背包变化事件(效果.失去物品, -1, 变化原因.失去));
            }
        }
    }
}
```

- [ ] **Step 3: 验证**

等待编译完成，`read_console(types=["error"], count=10)` → Expected: 无 error。

- [ ] **Step 4: 记录变更**

改动文件：`Domain/战斗规则.cs`、`Domain/效果结算.cs`。

---

### Task 6: 数据服务 DataService（加载 + 校验）

**Files:**
- Create: `Assets/Scripts/Data/DataService.cs`

**Interfaces:**
- Consumes: `奇幻文字RPG.Core.EventBus`、`奇幻文字RPG.Data.*` 模型、`奇幻文字RPG.Events.日志类型`。
- Produces: `奇幻文字RPG.Data.DataService`：构造 `DataService(EventBus)`（自动加载 + 校验）；公开字典 `剧情/敌人/物品/技能/任务/地图/区域/设施`；`List<string> 校验错误`；`void 重新校验()`。

- [ ] **Step 1: 写实现**

创建 `Assets/Scripts/Data/DataService.cs`：
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using 奇幻文字RPG.Core;
using 奇幻文字RPG.Events;

namespace 奇幻文字RPG.Data
{
    /// <summary>数据服务：加载 Resources/Data/*.json 并缓存为字典；启动全量跨引用校验。</summary>
    public sealed class DataService
    {
        private readonly EventBus _事件;

        public Dictionary<string, 剧情节点> 剧情 { get; private set; } = new Dictionary<string, 剧情节点>();
        public Dictionary<string, 敌人数据> 敌人 { get; private set; } = new Dictionary<string, 敌人数据>();
        public Dictionary<string, 物品数据> 物品 { get; private set; } = new Dictionary<string, 物品数据>();
        public Dictionary<string, 技能数据> 技能 { get; private set; } = new Dictionary<string, 技能数据>();
        public Dictionary<string, 任务数据> 任务 { get; private set; } = new Dictionary<string, 任务数据>();
        public Dictionary<string, 地图地点> 地图 { get; private set; } = new Dictionary<string, 地图地点>();
        public Dictionary<string, 区域数据> 区域 { get; private set; } = new Dictionary<string, 区域数据>();
        public Dictionary<string, 设施定义> 设施 { get; private set; } = new Dictionary<string, 设施定义>();

        public List<string> 校验错误 { get; } = new List<string>();

        public DataService(EventBus 事件)
        {
            _事件 = 事件;
            加载全部();
            重新校验();
        }

        private void 加载全部()
        {
            加载("story", 剧情, (剧情根 根) => 根.节点);
            加载("enemies", 敌人, (敌人根 根) => 根.敌人);
            加载("items", 物品, (物品根 根) => 根.物品);
            加载("skills", 技能, (技能根 根) => 根.技能);
            加载("quests", 任务, (任务根 根) => 根.任务);
            加载("map", 地图, (地图根 根) => 根.地点);
            加载("regions", 区域, (区域根 根) => 根.区域);
            加载("facilities", 设施, (设施根 根) => 根.设施);   // 允许缺失
        }

        private void 加载<T, TRoot>(string 文件, Dictionary<string, T> 目标, Func<TRoot, T[]> 提取) where T : class
        {
            var 资产 = Resources.Load<TextAsset>($"Data/{文件}");
            if (资产 == null) { _事件.发布(new 日志事件(日志类型.系统, $"[数据] 缺失 Data/{文件}.json")); return; }
            var 根 = JsonUtility.FromJson<TRoot>(资产.text);
            if (根 == null) return;
            foreach (var 项 in 提取(根))
            {
                var 标识 = 获取标识(项);
                if (!string.IsNullOrEmpty(标识)) 目标[标识] = 项;
            }
        }

        private static string 获取标识<T>(T 项)
        {
            var 字段 = typeof(T).GetField("标识");
            return 字段?.GetValue(项) as string;
        }

        public void 重新校验()
        {
            校验错误.Clear();

            // 剧情：选项目标 / 战斗引用 / 效果物品
            foreach (var (标识, 节点) in 剧情)
            {
                if (节点.选项 != null)
                    foreach (var 选项 in 节点.选项)
                    {
                        if (string.IsNullOrEmpty(选项.目标)) continue;
                        if (选项.目标.StartsWith("设施:"))
                        {
                            var 设施标识 = 选项.目标.Substring(3);
                            if (!设施.ContainsKey(设施标识)) 校验错误.Add($"剧情[{标识}] → 设施[{设施标识}] 不存在");
                        }
                        else if (!选项.目标.StartsWith("战斗:") && !选项.目标.StartsWith("探索:") &&
                                 !选项.目标.StartsWith("区域:") && 选项.目标 != "__结束" &&
                                 !剧情.ContainsKey(选项.目标) && !选项.目标.StartsWith("任务:"))
                        {
                            校验错误.Add($"剧情[{标识}] → 节点[{选项.目标}] 不存在");
                        }
                    }
                if (!string.IsNullOrEmpty(节点.战斗))
                {
                    var 部分 = 节点.战斗.Split(':');
                    if (部分.Length >= 2 && !敌人.ContainsKey(部分[1]))
                        校验错误.Add($"剧情[{标识}] 战斗敌人[{部分[1]}] 不存在");
                }
            }

            // 地图：目标 / 连接
            foreach (var (标识, 地点) in 地图)
            {
                if (!string.IsNullOrEmpty(地点.目标) && !地点.目标.StartsWith("探索:") && !剧情.ContainsKey(地点.目标))
                    校验错误.Add($"地图[{标识}] → 节点[{地点.目标}] 不存在");
                if (地点.连接 != null)
                    foreach (var 相邻 in 地点.连接)
                        if (!地图.ContainsKey(相邻)) 校验错误.Add($"地图[{标识}] → 连接[{相邻}] 不存在");
            }

            // 区域：遭遇敌人 / 发现节点
            foreach (var (标识, 区) in 区域)
            {
                if (区.遭遇 != null)
                    foreach (var 遭遇 in 区.遭遇)
                        if (!string.IsNullOrEmpty(遭遇.敌人) && !敌人.ContainsKey(遭遇.敌人))
                            校验错误.Add($"区域[{标识}] → 敌人[{遭遇.敌人}] 不存在");
                if (区.发现 != null)
                    foreach (var 发现 in 区.发现)
                        if (!string.IsNullOrEmpty(发现.节点) && !剧情.ContainsKey(发现.节点))
                            校验错误.Add($"区域[{标识}] → 节点[{发现.节点}] 不存在");
            }
        }
    }
}
```

- [ ] **Step 2: 验证**

等待编译完成，`read_console(types=["error"], count=10)` → Expected: 无 error。真实加载验证留待 Task 8（GameBootstrap 装配日志输出各数据计数）。

- [ ] **Step 3: 记录变更**

改动文件：`Data/DataService.cs`。

---

### Task 7: 存档服务 SaveService

**Files:**
- Create: `Assets/Scripts/Services/SaveService.cs`

**Interfaces:**
- Consumes: `奇幻文字RPG.Domain.玩家档案`。
- Produces: `奇幻文字RPG.Services.SaveService`：`void 保存(玩家档案)`、`存档数据 读取()`、`bool 有存档()`、`void 删除()`；`[Serializable] class 存档数据 { 玩家档案 玩家; long 保存时间; }`。PlayerPrefs 键 `fantasy_text_rpg_save_v2`（与旧 v1 隔离）。

- [ ] **Step 1: 写实现**

创建 `Assets/Scripts/Services/SaveService.cs`：
```csharp
using System;
using UnityEngine;
using 奇幻文字RPG.Domain;

namespace 奇幻文字RPG.Services
{
    /// <summary>极简存档：PlayerPrefs 存 JSON 快照（v2 键，与旧框架 v1 隔离）。</summary>
    public sealed class SaveService
    {
        private const string 存档键 = "fantasy_text_rpg_save_v2";

        [Serializable]
        public class 存档数据
        {
            public 玩家档案 玩家;
            public long 保存时间;
        }

        public void 保存(玩家档案 玩家)
        {
            var 数据 = new 存档数据
            {
                玩家 = 玩家,
                保存时间 = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            PlayerPrefs.SetString(存档键, JsonUtility.ToJson(数据));
            PlayerPrefs.Save();
        }

        public bool 有存档() => PlayerPrefs.HasKey(存档键);

        public 存档数据 读取()
        {
            if (!有存档()) return null;
            try { return JsonUtility.FromJson<存档数据>(PlayerPrefs.GetString(存档键)); }
            catch (Exception 异常)
            {
                Debug.LogError($"[SaveService] 读取存档失败: {异常.Message}");
                return null;
            }
        }

        public void 删除() => PlayerPrefs.DeleteKey(存档键);
    }
}
```

- [ ] **Step 2: 验证**

等待编译完成，`read_console(types=["error"], count=10)` → Expected: 无 error。行为验证留待 M3+（存档接入菜单）。

- [ ] **Step 3: 记录变更**

改动文件：`Services/SaveService.cs`。

---

### Task 8: 组合根 GameBootstrap + 装配验证

**Files:**
- Create: `Assets/Scripts/Core/GameBootstrap.cs`

**Interfaces:**
- Consumes: 前述全部服务。
- Produces: `文字RPG.Core.GameBootstrap`：MonoBehaviour（场景 M2 挂载）；静态 `装配()`（幂等，装配 EventBus/DataService/SaveService，数据校验失败记日志并置 `装配失败`）；静态 `bool 装配失败`。

- [ ] **Step 1: 写实现**

创建 `Assets/Scripts/Core/GameBootstrap.cs`：
```csharp
using UnityEngine;
using 奇幻文字RPG.Data;
using 奇幻文字RPG.Services;

namespace 奇幻文字RPG.Core
{
    /// <summary>组合根：装配全部服务（幂等）。数据校验失败时记日志并置 装配失败。</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private static bool _已装配;
        public static bool 装配失败 { get; private set; }

        private void Awake() => 装配();

        public static void 装配()
        {
            if (_已装配) return;
            _已装配 = true;

            var 事件 = new EventBus();
            ServiceRegistry.Register(事件);

            var 数据 = new DataService(事件);
            ServiceRegistry.Register(数据);

            ServiceRegistry.Register(new SaveService());

            if (数据.校验错误.Count > 0)
            {
                装配失败 = true;
                foreach (var 错误 in 数据.校验错误)
                    Debug.LogError($"[GameBootstrap] 数据校验失败: {错误}");
            }
            else
            {
                Debug.Log($"[GameBootstrap] 核心服务装配完成：剧情 {数据.剧情.Count} / 敌人 {数据.敌人.Count} / 物品 {数据.物品.Count} / 区域 {数据.区域.Count} / 技能 {数据.技能.Count} / 任务 {数据.任务.Count} / 地点 {数据.地图.Count} / 设施 {数据.设施.Count}");
            }
        }
    }
}
```

- [ ] **Step 2: 场景挂载并 Play 验证**

在场景中新建空物体「框架引导」，挂 `GameBootstrap` 组件（`manage_gameobject(action="create", name="框架引导")` + `manage_components(action="add", target="框架引导", component_type="文字RPG.Core.GameBootstrap")`）。

然后 Play 一次（`manage_editor(action="play")`），等几秒后读控制台：
```
read_console(types=["log"], count=20, filter_text="GameBootstrap")
```
Expected: 出现 `[GameBootstrap] 核心服务装配完成：剧情 29 / 敌人 6 / 物品 8 / 区域 2 / 技能 3 / 任务 2 / 地点 4 / 设施 0`（无「数据校验失败」error）。（已实测：剧情 29）
再 `manage_editor(action="stop")` 退出 Play。

> 注：M1 阶段旧游戏仍在场景运行（其 游戏管理器 逻辑照旧）；「框架引导」物体只做新框架装配验证，不干扰旧游戏。若旧游戏与新装配同时触发冲突（例如双方都注册同名事件处理），以旧游戏表现为准，M4 场景切换后彻底隔离。

- [ ] **Step 3: 记录变更**

改动文件：`Core/GameBootstrap.cs`、场景（新增「框架引导」物体）。

---

## M1 完成判定

- [ ] 全部 8 个任务编译零错误，控制台无相关 error。
- [x] `GameBootstrap.装配()` Play 一次输出「核心服务装配完成」日志，数据计数正确（剧情 29 / 敌人 6 / 物品 8 / 区域 2 / 技能 3 / 任务 2 / 地点 4），无「数据校验失败」。（已实测通过）
- [ ] 旧游戏（Assembly-CSharp 旧代码）仍可运行，未被破坏。
- [ ] 新旧代码共存于同一程序集，靠命名空间分层。

## 后续里程碑（另立计划）

- **M2 UI 框架**：ViewManager/控件库/HUD/剧情视图（UI Toolkit + DOTween 反馈动画，Editor 搭建）。
- **M3 服务层**：背包/经济/任务/战斗服务 + 接入玩家档案。
- **M4 设施迁移**：商店/训练场/任务板/酒店 → 设施；替换剧情特殊目标；场景切换到 GameBootstrap。
- **M5 新设施示范**：仓库 + 种植。
- **M6 打磨**：主题 USS/迁移检查器/删旧代码。
