# 资源热更新 · Addressables 最小迁移清单

> 现状：`items.json`（`"图片"` 字符串）+ `Resources.LoadAll<Sprite>` 按名匹配。本文是"将来要资源热更时"的最小改造路径清单，**现在无需执行**。核心：数据层（items.json）几乎不动，只把"取图那一步"从 Resources 换成 Addressables，并让图标资源可远程下发。

## 0. 先确认前提

- Addressables 做的是**资源热更**（换图片/音频/items 配置），**不是代码热更**。
- 单机文字 RPG、素材少时不必现在上；等出现「要单独更新物品图标/数值」或「要瘦身包体」的需求再迁。

## 1. 安装

- 菜单 `Window → Package Manager → Unity Registry → Addressables` 安装。
- `Window → Asset Management → Addressables Groups` 打开管理面板。

## 2. 把物品图标迁到 Addressables

- 把 `Assets/Resources/物品图标/` 整体**移到**一个 Addressables Group（建议新建 `物品图标` 组）；或右键资源 → `Move to Addressables Group`。
- **注意**：迁走后 `Resources.LoadAll` 就不能用了（不再在 Resources 目录），取图逻辑要同步改。

## 3. 给资源定"地址"（Address）

每个图标一个地址（逻辑名），例如：

```
物品图标_木棍
物品图标_木琴
物品图标_狼牙棒
...
```

地址 = 我们的 `items.json` 里 `"图片"` 值加统一前缀（`item_` 或 `图标_`）。这样 `"图片":"木棍"` 语义不变，只查表方式变了。

> 也可用 **AssetReferenceSprite**（Inspector 里拖、GUID 强引用、拖错即报错），代价是 `物品数据.图片` 从 string 换成引用字段。二者选一：字符串地址（轻、数据仍 JSON 集中）或 AssetReference（编辑器强类型）。

## 4. 改造取图逻辑（`物品图标服务`）

现（Resources）：
```csharp
public static class 物品图标服务
{
    private static Sprite[] 切片;
    public static Sprite 获取(string 图片引用)
    {
        if (string.IsNullOrEmpty(图片引用)) return null;
        return 从切片找(图片引用) ?? Resources.Load<Sprite>($"物品图标/{图片引用}");
    }
    ...
}
```

改（Addressables，异步）：
```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine;

public static class 物品图标服务
{
    private const string 前缀 = "物品图标_";
    private static readonly Dictionary<string, Sprite> 缓存 = new();

    public static async Task<Sprite> 获取(string 图片引用)
    {
        if (string.IsNullOrEmpty(图片引用)) return null;
        if (缓存.TryGetValue(图片引用, out var 已有)) return 已有;   // 内存引用计数（本例缓存常驻）
        var handle = Addressables.LoadAssetAsync<Sprite>($"{前缀}{图片引用}");
        var spr = await handle.Task;
        if (spr != null) 缓存[图片引用] = spr;
        return spr;
    }
}
```

面板调用改异步（`物品网格面板.创建物品`）：
```csharp
async void 挂图标(Image 内容图, string 图片引用)
{
    var spr = await 物品图标服务.获取(图片引用);
    if (spr != null) { 内容图.sprite = spr; 内容图.color = Color.white; 内容图.preserveAspect = true; }
}
// 创建物品 里：
挂图标(内容图, 物品.图片);
```
> 用协程也可（不用 async/await），但 uGUI 面板加载小图 async 足够。

## 5. 构建配置（关键：做到热更）

- Addressables 面板右上角 **Profile** 建**远程组**（Remote）。
- 把物品图标 Group 的 **Build & Load Paths** 设为远程（`http://你的服务器/...`）。
- 构建时：`Groups → Build → Build Player Content` → 生成 `catalog` + `bundle` 上传服务器。
- 运行时 `Addressables.UpdateCatalogs()`（启动时拉最新 Catalog）→ 有新版本就自动用新资源。

```
旧版本 App
   │  启动
   ▼
拉服务器 Catalog ──比本地旧──▶ 下载新 bundle（物品图标）
   ▼
用新图（不改 App、不重装）
```

## 6. 注意事项

- **异步**：`LoadAssetAsync` 不阻塞主线程，但引入 async/协程，面板创建逻辑会变异步，注意空引用时序。
- **引用计数**：`LoadAssetAsync` 返回 handle，用完 `Release()`；若要常驻用缓存（如上 `缓存`），则加载一次持有，避免反复释放。Addressables 会**自动卸载引用归零**的资源——别双重管理。
- **内存**：资源卸载策略可调（`Release` / 常驻 / 自动），对照"引用计数"理解，避免缓存了却还 repeat 加载。
- **故障**：远程拉不到时 `handle.Task` 抛异常，要 try/catch 回退为占位色块。
- **目录变更**：Addressables 主包（含默认 catalog）仍需随版本发；热更的是**资源 bundle** 及**增量 catalog**。代码不变。

## 7. 建议的当前状态

保持 `items.json + Resources` 不动。把 `"图片"` 字段**语义上当作"资源引用标识"**（不绑死路径），`物品图标服务.获取` 是唯一取图入口——将来只改这一个文件（+ 资源迁移 + 构建配置），数据层零重构。这就是"预留接口、延迟决策"。
