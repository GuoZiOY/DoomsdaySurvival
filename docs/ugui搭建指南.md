# 文字奇幻RPG · uGUI 搭建指南

> 本指南供你在 Unity 场景中手动搭建游戏 UI（uGUI）。代码链路已就绪，你只需按**精确命名**搭建静态结构，代码会自动找到并驱动它。

---

## 0. 代码链路怎么工作

```
游戏逻辑（服务层）──发布事件──>  UI管理器（场景中的组件）──按名字找元素──> 你搭好的界面
   剧情/战斗/探索/设施/日志         订阅事件 → 切面板/填文字/生成按钮
```

- **你负责**：搭静态 UI（面板、布局、配色、预制体）
- **代码负责**：找元素、切面板、填动态内容（剧情正文、选项按钮、日志条目、商品/技能/任务列表）
- 所有面板名字**必须和本指南完全一致**，代码用 `画布.Find("路径")` 查找，拼错就找不到（控制台报警告）

---

## 1. 整体层级（严格按此搭建）

```
画布  (Canvas + CanvasScaler, Screen Space - Overlay, 参考 1920×1080)
│
├── HUD条  (RectTransform，可加 Image 做底)
│   ├── 生命    (TMP_Text)
│   ├── 魔力    (TMP_Text)
│   ├── 金币    (TMP_Text)
│   ├── 时间    (TMP_Text)
│   └── 地点    (TMP_Text)
│
├── 主菜单面板  (GameObject，默认【显示】)
│   ├── 标题       (TMP_Text)
│   ├── 新游戏按钮 (Button)
│   └── 继续按钮   (Button)
│
├── 主视窗面板  (GameObject，默认【隐藏】)
│   ├── 标题       (TMP_Text)          ← 剧情/战斗/探索/结局 共用标题
│   ├── 正文       (TMP_Text)          ← 剧情/战斗消息/探索文本/结局正文
│   ├── 选项区     (空物体 + VerticalLayoutGroup + ContentSizeFitter) ← 动态按钮
│   └── 返回主菜单 (Button，默认【隐藏】) ← 结局时显示
│
├── 日志面板  (GameObject，常驻【显示】)
│   └── 日志滚动  (ScrollRect)
│       └── 日志内容  (空物体 + VerticalLayoutGroup + ContentSizeFitter)
│
├── 商店面板  (GameObject，默认【隐藏】)
│   ├── 标题       (TMP_Text)
│   ├── 商品列表   (空物体 + VerticalLayoutGroup)
│   └── 返回按钮   (Button)
│
├── 训练场面板  (GameObject，默认【隐藏】)
│   ├── 标题       (TMP_Text)
│   ├── 技能列表   (空物体 + VerticalLayoutGroup)
│   └── 返回按钮   (Button)
│
└── 任务板面板  (GameObject，默认【隐藏】)
    ├── 标题       (TMP_Text)
    ├── 任务列表   (空物体 + VerticalLayoutGroup)
    └── 返回按钮   (Button)
```

**面板切换逻辑**：代码一次只显示一个面板（主菜单/主视窗/商店/训练场/任务板），其余隐藏。日志面板常驻。

---

## 2. 逐步搭建

### 第 1 步：Canvas
1. 菜单 `GameObject > UI > Canvas`（会自动带 CanvasScaler、GraphicRaycaster）
2. CanvasScaler：UI Scale Mode = **Scale With Screen Size**，参考 1920×1080
3. 场景中已有的 Main Camera 保留（或不用都行，uGUI 用 Overlay 不依赖相机）
4. 确保场景有 **EventSystem**（uGUI 按钮点击需要；若没有，`GameObject > UI > EventSystem` 建一个）

### 第 2 步：按钮预制体（最关键，先做这个）
1. 在 `Assets` 下建文件夹 `Prefab`
2. `GameObject > UI > Button - TextMeshPro` 创建一个按钮 → 拖到 `Assets/Prefab/按钮.prefab`
3. 改名：
   - 根节点名随意（如 `按钮`）
   - **子物体（Text）改名为「文字」** ← 代码找这个子物体填字
4. 给根节点加组件：**LayoutElement**（这样放列表里能撑满宽度）
5. 样式随你调：Image 换背景图（可用 `Assets/Resources/界面/像素按钮边框.png`，Image Type=Sliced）、文字字体/颜色/字号
6. 完成后删掉场景里那个临时按钮（留 Prefab 资产即可）

### 第 3 步：HUD 条
1. `GameObject > UI > Panel` 改名 `HUD条`（Panel 自带 Image 做底），作为 画布 子物体，放顶部
2. 在 HUD条 下创建 5 个 `TextMeshPro` 子物体，**分别命名**：`生命` `魔力` `金币` `时间` `地点`
3. 用 HorizontalLayoutGroup 或手动摆开，文字内容随意（代码会覆盖）
4. 布局建议：左上 生命（红色）、魔力（白）、金币（金色）、时间（灰）、地点

### 第 4 步：主菜单面板
1. `GameObject > UI > Panel` 改名 `主菜单面板`
2. 子物体：`标题`(TextMeshPro) + `新游戏按钮`(用按钮预制体生成实例或自建 Button) + `继续按钮`
3. 按钮直接拖你做的 `按钮.prefab` 实例进来改名即可

### 第 5 步：主视窗面板
1. `GameObject > UI > Panel` 改名 `主视窗面板`，**默认取消激活**（Inspector 左上勾选去掉）
2. 子物体：
   - `标题` (TextMeshPro)
   - `正文` (TextMeshPro，设 Word Wrapping，给个滚动容器也行)
   - `选项区`：`GameObject > UI > Empty` 改名 `选项区`，加 `VerticalLayoutGroup`（childControlHeight/childForceExpandWidth = true）+ `ContentSizeFitter`（VerticalFit=PreferredSize）
   - `返回主菜单`：Button，**默认隐藏**

### 第 6 步：日志面板
1. `GameObject > UI > Panel` 改名 `日志面板`（放左侧）
2. 子物体 `日志滚动`：`GameObject > UI > Scroll View` 改名 `日志滚动`（自带 Mask+ScrollRect）
3. 把 Scroll View 里的 `Viewport/Content` 改名 `日志内容`，并给 `日志内容` 加 `VerticalLayoutGroup` + `ContentSizeFitter`（VerticalFit=PreferredSize）
   - 删除 Scroll View 自带的两个滚动条（或用，随意）
   - 注意：代码找 `日志面板/日志滚动/日志内容`，层级别变

### 第 7 步：三个设施面板（商店/训练场/任务板）
每个都这样建：
1. `GameObject > UI > Panel` 改名 `商店面板`（或 `训练场面板`/`任务板面板`），默认隐藏
2. 子物体：
   - `标题` (TextMeshPro)
   - `商品列表`（商店）/ `技能列表`（训练场）/ `任务列表`（任务板）：`Empty + VerticalLayoutGroup + ContentSizeFitter`
   - `返回按钮`：Button

### 第 8 步：挂 UI管理器
1. 场景建空物体 `UI管理器`（`GameObject > Create Empty`）
2. 挂组件 `UI管理器`（在 `Assets/Scripts/UI/UI管理器.cs`）
3. 把 `按钮.prefab` 拖进 Inspector 的 **「按钮预制体」** 槽位（灰字写的那个）

---

## 3. 命名核对表（代码查找的精确路径）

| 代码查找 | 必须存在 |
|---|---|
| `HUD条/生命` `HUD条/魔力` `HUD条/金币` `HUD条/时间` `HUD条/地点` | 5 个 TMP_Text |
| `主视窗面板/标题` `/正文` `/选项区` `/返回主菜单` | 标题+正文 TMP_Text，选项区容器，返回按钮 |
| `日志面板/日志滚动/日志内容` | 滚动容器 |
| `主菜单面板` + `/新游戏按钮` `/继续按钮` | 面板 + 2 按钮 |
| `商店面板` + `/标题` `/商品列表` `/返回按钮` | 面板 + 标题 + 列表 + 返回 |
| `训练场面板` + `/标题` `/技能列表` `/返回按钮` | 同上 |
| `任务板面板` + `/标题` `/任务列表` `/返回按钮` | 同上 |

> 缺任何一个，控制台会报 `[UI管理器] ...` 警告，对应功能不显示。搭完先确认没有这些警告。

---

## 4. 配色与风格建议（暖暗余烬风）

- 背景/面板底色：近黑 `#0b0b0d`，面板 `#151519`，边框发丝线 `#2e2e34`
- 文字：羊皮纸白 `#d8d3c8`；标题金色 `#d9a441`；伤害红 `#c7473d`；奖励绿 `#7fae6a`；系统灰 `#8a8578`
- 像素素材（已在项目里）：`Assets/Resources/界面/` 下的 `像素面板边框.png`、`像素按钮边框.png`、`像素分隔线.png`、`Hills Free` 背景图
  - 用法：Image 的 Source Image 选它，**Image Type = Sliced**（切九宫格，拉伸不糊）
- 动态生成的元素（日志条目/选项按钮/列表行）由代码按上述配色上色，你的静态布局负责打底

---

## 5. 游戏流程预期

1. Play → 显示**主菜单面板**
2. 点「新游戏」→ 显示**主视窗面板**（标题"剧情"+ 序章正文 + 选项按钮）
3. 点选项 → 剧情推进 / 进战斗（主视窗变"战斗"，攻击/技能/道具/逃跑按钮）
4. 进设施（`设施:商店` 等）→ 对应**设施面板**，列表列出商品/技能/任务，点行执行，点「返回按钮」回剧情
5. 日志面板始终滚动记录所有事件

---

## 6. 常见问题

- **按钮点了没反应** → 场景没有 EventSystem，或按钮预制体没挂 Button/没接代码
- **主菜单不显示 / 全空白** → 检查各面板是否按名字搭好、UI管理器 是否挂上、按钮预制体是否拖进槽位
- **文字是方框/乱码** → TMP 字体问题：用 `TextMeshPro` 组件，确保项目 TMP 字体资产（Silver SDF）已设默认，或给 TMP 文本指定中文字体
- **控制台 `[UI管理器]` 警告** → 某个路径没找到，对照第 3 节核对表补上
- **想加新设施** → 在 场景搭一个新面板 + 在 `UI管理器.打开设施` 的 switch 里加一个 case

---

*代码文件参考：`Assets/Scripts/UI/UI管理器.cs`（UI 驱动）、`Assets/Scripts/UI/游戏主题.cs`（配色）、`Assets/Scripts/Events/导航事件.cs`（面板切换事件）、`Assets/Scripts/Services/*`（游戏逻辑，无需改动）*
