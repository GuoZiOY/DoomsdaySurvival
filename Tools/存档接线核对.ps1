# ============================================================
# 存档接线核对：守住"存档链路的形状"（不打开 Unity 就能跑）
# 为什么需要（v52 刀64 立的）：
#   存档是**唯一一个"坏了就毁掉玩家进度"的系统**，而它的 bug 有个共同特征 ——
#   **不报错、不崩溃、静默发生**：
#     · `天赋冷却` 写成 Dictionary → JsonUtility 静默丢弃 → 冷却从没随过档（注释还写着"随档保存"）；
#     · 版本号读了但从不检查 → 缺字段的坏档以"关键值是 0"的形态静默进游戏；
#     · 读档路径忘了对齐整点基准 → 第一帧补算 5×24 次生存结算 = 读档即死；
#     · `搜索服务.清空战局()` 只挂在"回安全屋"事件上 → 新游戏/读档不清，跨局残留。
#   这些都不可能靠"编译通过"或"跑一次看看"发现。所以在这里立成**门禁**。
#
# 与其它两道关的分工（别互相替代）：
#   · `Tools/存档验证`（离线 net10）：**行为**断言 —— 版本真值表 / 迁移幂等 / 整点结算 / 指纹覆盖。
#     它验的是从 Assets 链接进来的真源码。
#   · 本脚本：**源码形状**断言 —— 类型选择、常量出口、调用链是否接上。
#   · 游戏内「存档往返自检」（快速测试面板）：真 JsonUtility 存→读→比指纹（Unity 专属，离线做不了）。
#
# 边界：只查"接线与形状"，查不了"数值/手感"。
# 用法（仓库根）：& ".\Tools\存档接线核对.ps1"
# 编码纪律：一律 [IO.File]::ReadAllText + 显式 UTF8
# ============================================================
$ErrorActionPreference = "Stop"
$根 = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$脚本根 = Join-Path $根 "Assets\Scripts"
$script:失败 = 0
function 报错([string]$文本) { Write-Output ("  ✗ " + $文本); $script:失败++ }
function 读([string]$相对) {
    $p = Join-Path $根 $相对
    if (-not (Test-Path $p)) { 报错 ("文件不存在：" + $相对); return "" }
    return [IO.File]::ReadAllText($p, [Text.Encoding]::UTF8)
}
# 去注释后扫描：本仓库的注释里大量出现 `Dictionary<...>` 之类的"反面教材"文字，
# 不去注释就会把"说明为什么不能用它"的注释当成"用了它"。
function 去注释([string]$文本) {
    $t = [regex]::Replace($文本, '/\*[\s\S]*?\*/', '')
    return [regex]::Replace($t, '//[^\r\n]*', '')
}
# 取一个方法的方法体（从包含签名的那一行，到**同缩进的第一个 `}`**）。
# 局限（写清楚，免得被当成分组解析器）：它不认识嵌套的同缩进 `}` ——
# 本仓库的方法体都是"8 空格缩进 + 内层再多缩进"，所以只对 8 空格缩进的方法可靠。
# 找不到签名或找不到收尾 → 返回 $null（调用方必须显式判 null 并报错，不许静默跳过）。
function 取方法体([string]$文本, [string]$签名, [int]$缩进 = 8) {
    $行们 = $文本 -split "`r?`n"
    $开始 = -1
    for ($i = 0; $i -lt $行们.Count; $i++) { if ($行们[$i].Contains($签名)) { $开始 = $i; break } }
    if ($开始 -lt 0) { return $null }
    $收尾 = (" " * $缩进) + "}"
    for ($j = $开始 + 1; $j -lt $行们.Count; $j++) {
        if ($行们[$j] -eq $收尾) { return (($行们[$开始..$j]) -join "`n") }
    }
    return $null
}

Write-Output "=== 存档接线核对 ==="

# ---------- 1. 载荷类型必须能被 JsonUtility 序列化 ----------
# JsonUtility 的硬限制：不支持 Dictionary / HashSet / ValueTuple（(int,int) 这种）/ 接口 / object；
# 委托必须 [NonSerialized]。违反**不会编译失败、不会抛异常**，只会静默丢字段 —— 正是要门禁的原因。
Write-Output "[1] 存档载荷里的成员类型必须 JsonUtility 存得下（Dictionary/HashSet/元组/裸委托一律报错）"
$载荷文件 = @(
    "Assets\Scripts\Domain\玩家\玩家档案.cs",
    "Assets\Scripts\Domain\存档\存档模型.cs",
    "Assets\Scripts\Domain\网格\网格服务.cs",
    "Assets\Scripts\Domain\网格\网格数据.cs"
)
$禁用 = @(
    @{ 名 = "Dictionary<"; 说明 = "JsonUtility 不支持 Dictionary（本仓库已有先例：迷雾记忆 用 string[] 绕开）" },
    @{ 名 = "HashSet<";    说明 = "JsonUtility 不支持 HashSet" },
    @{ 名 = "Tuple<";      说明 = "JsonUtility 不支持 Tuple" }
)
$载荷字段数 = 0
foreach ($f in $载荷文件) {
    $纯 = 去注释 (读 $f)
    # 只查**字段声明**里的类型 —— 方法签名里的 Dictionary/HashSet（如 玩家档案.读迷雾 返回 HashSet<int>）
    # 是合法的：那是运行期算法用的临时集合，不参与序列化。
    foreach ($行 in ($纯 -split "`n")) {
        $m = [regex]::Match($行, '^\s*(?:\[[^\]]*\]\s*)*(public|internal|private|protected)\s+([^\s=;()]+)\s+([\w\u4e00-\u9fff]+)\s*(=|;)')
        if ($m.Success) {
            $类型 = $m.Groups[2].Value
            foreach ($d in $禁用) {
                if ($类型.Contains($d.名)) { 报错 ("{0} 的字段 `{1}` 类型是 {2} —— {3}" -f (Split-Path $f -Leaf), $m.Groups[3].Value, $类型, $d.说明) }
            }
            # 元组字段：`(int 列, int 行)` 这类**字段类型**（不含 Func<> 内部的参数元组 —— 那种必须带 [NonSerialized]）
            if ($类型.StartsWith("(")) { 报错 ("{0} 的字段 `{1}` 是元组类型 —— 元组字段 JsonUtility 存不下，请拍平成两个 int" -f (Split-Path $f -Leaf), $m.Groups[3].Value) }
            if ($类型 -eq "object" -or $类型 -eq "dynamic") { 报错 ("{0} 的字段 `{1}` 是 {2} —— JsonUtility 存不下" -f (Split-Path $f -Leaf), $m.Groups[3].Value, $类型) }
            if ($类型.Contains("Func<") -or $类型.Contains("Action<")) {
                if (-not $行.Contains("[NonSerialized]")) { 报错 ("{0} 的委托字段 `{1}` 没有 [NonSerialized] —— 委托不可序列化，会炸/丢" -f (Split-Path $f -Leaf), $m.Groups[3].Value) }
            }
            $载荷字段数++
            continue
        }
        # 兜底：字段声明的**类型**写得太花（主正则的名字捕获没匹配上）但初始化在同一行。
        # 必须要求同一行有访问修饰符 —— 否则会把方法体里的局部变量（`var 集 = new HashSet<int>();`）
        # 误判成字段。局部集合完全合法（那是运行期算法用的临时集合，不参与序列化）。
        $m2 = [regex]::Match($行, '^\s*(?:\[[^\]]*\]\s*)*(public|internal|private|protected)\s+.*=\s*new\s+(Dictionary|HashSet|Tuple)\s*<')
        if ($m2.Success) { 报错 ("{0} 的字段用了 `= new {1}<` —— JsonUtility 存不下这种字段" -f (Split-Path $f -Leaf), $m2.Groups[2].Value) }
    }
}
Write-Output ("  扫过 {0} 个载荷文件、{1} 个字段声明（去注释后）" -f $载荷文件.Count, $载荷字段数)
if ($载荷字段数 -lt 40) { 报错 ("只扫到 {0} 个字段声明 —— 正则失效了？门禁形同虚设" -f $载荷字段数) }

# ---------- 2. 读档后必须对齐整点基准（"读档即死"的根） ----------
Write-Output "[2] 读档路径必须把整点基准对齐（不对齐 = 第一帧补算 5×24 次生存结算 = 读档即死）"
$时间管理 = 读 "Assets\Scripts\Services\世界\世界时间管理器.cs"
if (-not $时间管理.Contains("public void 读档后对齐(")) { 报错 "世界时间管理器 缺少 读档后对齐() —— 读档侧就没有可调用的对齐入口了" }
if (-not $时间管理.Contains("整点结算.应结算次数(")) { 报错 "推进() 没走 整点结算.应结算次数() —— 那么离线断言验的就不是游戏那份代码（"验的是副本"）" }
if (-not $时间管理.Contains("整点结算.需要重对齐(")) { 报错 "推进() 缺少 整点结算.需要重对齐() 的兜底 —— 某个入口再忘一次就又变成读档即死" }
$玩家服务 = 读 "Assets\Scripts\Services\玩家\PlayerService.cs"
if (-not $玩家服务.Contains("对齐世界时间(存档数据.整点基准)")) { 报错 "PlayerService.读档 没有调 对齐世界时间(存档数据.整点基准) —— 读档即死会回来" }
if (-not $玩家服务.Contains("public void 新游戏()")) { 报错 "PlayerService 里找不到 新游戏() —— 结构变了，请复查本段断言" }
if (-not $玩家服务.Contains("作废上一局世界态();")) { 报错 "PlayerService 没有清上一局世界态 —— 同进程内 新游戏/读档 会残留上一局的容器战局" }

# ---------- 3. 未开局不许推进世界时间 ----------
Write-Output "[3] 未开局（主菜单 / 角色创建）不许推进世界时间"
if (-not $时间管理.Contains("string.IsNullOrEmpty(档.当前节点)")) {
    报错 "驱动.Update 没有「未开局不推进」的守卫 —— 在标题界面干坐 10 现实分钟 = 世上过去 300 游戏分钟，还会把天气掷掉"
}

# ---------- 4. 存档只走文件、只走一个出口 ----------
Write-Output "[4] 存档只走文件（PlayerPrefs 只留迁移用），路径常量只有一个出口"
$全部cs = @(Get-ChildItem $脚本根 -Recurse -File -Filter *.cs)
$用PlayerPrefs写 = @()
$用persistent = @()
$用旧键 = @()
foreach ($f in $全部cs) {
    $纯 = 去注释 ([IO.File]::ReadAllText($f.FullName, [Text.Encoding]::UTF8))
    if ($纯.Contains("PlayerPrefs.SetString")) { $用PlayerPrefs写 += $f.Name }
    if ($纯.Contains("persistentDataPath")) { $用persistent += $f.Name }
    if ($纯.Contains("last87days_save_v4")) { $用旧键 += $f.Name }
}
if ($用PlayerPrefs写.Count -gt 0) { 报错 ("仍有文件在用 PlayerPrefs 写存档：" + ($用PlayerPrefs写 -join ", ") + "（v5 起存档是磁盘文件；PlayerPrefs 只允许在迁移里读旧键）") }
if ($用persistent.Count -ne 1 -or $用persistent[0] -ne "存档文件.cs") {
    报错 ("persistentDataPath 只允许出现在 存档文件.cs，实际：" + ($用persistent -join ", ") + " —— 路径散开就会写出两个互不相认的存档目录")
}
if ($用旧键.Count -ne 1 -or $用旧键[0] -ne "存档模型.cs") {
    报错 ("v4 旧键常量只允许定义在 存档模型.cs，实际出现在：" + ($用旧键 -join ", "))
}

# ---------- 5. SaveService 的每个 public 成员都得有人用（防死 API） ----------
# 老代码里 `SaveService.删除()` 是 0 调用方的死代码 —— 那意味着"删档/重开"这条玩家路径**从来没接出来**。
Write-Output "[5] SaveService 的每个 public 方法都必须有调用方（防「写好了但没人接」）"
$存档服务 = 读 "Assets\Scripts\Services\存档\SaveService.cs"
$方法们 = @()
foreach ($m in [regex]::Matches((去注释 $存档服务), 'public\s+[^\s=;()]+\s+([\w\u4e00-\u9fff]+)\s*\(')) { $方法们 += $m.Groups[1].Value }
$方法们 = $方法们 | Sort-Object -Unique
$调用方文本 = ""
foreach ($f in $全部cs) {
    if ($f.Name -eq "SaveService.cs") { continue }
    $调用方文本 += (去注释 ([IO.File]::ReadAllText($f.FullName, [Text.Encoding]::UTF8)))
}
$孤方法 = @()
foreach ($名 in $方法们) { if (-not $调用方文本.Contains($名)) { $孤方法 += $名 } }
foreach ($名 in $孤方法) { 报错 ("SaveService." + $名 + "() 全仓库没有调用方 —— 死代码，说明这条玩家路径没接出来") }
Write-Output ("  SaveService public 方法 {0} 个：{1}" -f $方法们.Count, ($方法们 -join " / "))

# ---------- 6. 自动存档必须写自动槽，不许覆盖手动档 ----------
Write-Output "[6] 自动存档写独立槽位（不许覆盖玩家手动存的档）"
$自动 = 读 "Assets\Scripts\Services\存档\自动存档器.cs"
if (-not $自动.Contains("存档规格.自动槽号")) { 报错 "自动存档器 没有写 存档规格.自动槽号 —— 它会覆盖玩家的手动档" }
if ($自动 -match '保存\(\s*[1-9]') { 报错 "自动存档器 里出现了写死的手动槽号 —— 自动档只能进自动槽" }

# ---------- 7. 入口唯一：存档面板只从一处打开 ----------
Write-Output "[7] 存档面板 的入口（侧边栏「存档」/ 主菜单「继续」都必须走它，不许各自直连 SaveService）"
$面板调用 = @()
foreach ($f in $全部cs) {
    if ($f.Name -eq "存档面板.cs") { continue }
    if ([IO.File]::ReadAllText($f.FullName, [Text.Encoding]::UTF8).Contains("存档面板.打开(")) { $面板调用 += $f.Name }
}
if ($面板调用.Count -lt 2) { 报错 ("存档面板.打开() 的调用方只有 {0} 个（{1}）—— 侧边栏与主菜单都该走它" -f $面板调用.Count, ($面板调用 -join ", ")) }
$主菜单 = 读 "Assets\Scripts\UI\主菜单\主菜单面板.cs"
if ($主菜单.Contains("玩家.读档()")) { 报错 "主菜单面板 还在直接调 玩家.读档() —— 绕过存档面板就选不了槽、也看不见坏档" }

# ---------- 8. 安全屋拆墙必须进档 ----------
Write-Output "[8] 安全屋「已拆墙」必须进档（不进档 = 读档后墙复活、家具卡在墙里）"
$安全屋 = 读 "Assets\Scripts\Services\探索\安全屋管理器.cs"
if (-not $安全屋.Contains("已拆墙.Add(")) { 报错 "拆除墙() 没有记录到 档案.已拆墙 —— 读档后墙会全部复活，而家具是进档的" }
if (-not $安全屋.Contains("重放拆墙()")) { 报错 "安全屋管理器 没有重放拆墙 —— 记了不重放等于没记" }
if (-not $玩家服务.Contains("档案.已拆墙")) { 报错 "PlayerService 没有对 档案.已拆墙 做空值/非法值兜底 —— 旧档读进来会是 null" }

# ---------- 9. 战斗中不许存 / 不许读（用户口径："战斗时不需要存档读档"） ----------
# 为什么单独立一段：这条规则原来**只活在 UI 里**（侧边栏把按钮藏起来），而"读"那条路一段守卫都没有。
# 光靠按钮隐藏挡不住真实路径：存档面板是运行期全屏叠层，它开着的时候世界时间照走、遭遇会在面板背后打起来。
Write-Output "[9] 战斗中不许存 / 不许读（门禁唯一真相 + 存读两侧都接 + 读档失败不再开新游戏）"
$门禁 = 读 "Assets\Scripts\Services\存档\存档门禁.cs"
if (-not $门禁.Contains("存前检查(")) { 报错 "存档门禁 缺少 存前检查() —— 存那条路就没有统一判据了" }
if (-not $门禁.Contains("读前检查(")) { 报错 "存档门禁 缺少 读前检查() —— 读那条路就没有统一判据了（这正是原来的洞）" }
if (-not $门禁.Contains("正在战斗")) { 报错 "存档门禁 里找不到「正在战斗」的判定 —— 本段就是为它立的" }
# 存前检查 必须含"未开局"那一半（读前检查**故意不含** —— 主菜单「继续」正是在未开局状态下读档的）
if (-not $门禁.Contains("已开局检查(")) { 报错 "存档门禁 的 存前检查 没有「未开局」这一半 —— 会存出坏档" }

$读档体 = 取方法体 $玩家服务 "public void 读档(int 槽)"
if ($null -eq $读档体) { 报错 "PlayerService 里找不到 读档(int 槽) —— 结构变了，请复查本段断言" }
else {
    # ★ 必须 去注释 再断言：本仓库的注释里**故意**写着反例（"要开新游戏请显式走 新游戏()"），
    #   不去注释就会把"提醒你别这么写"的注释当成"这么写了"。段[1] 出于同一理由也去注释。
    $读档体 = 去注释 $读档体
    if (-not $读档体.Contains("存档门禁.读前检查()")) { 报错 "PlayerService.读档 没有走 存档门禁.读前检查() —— 战斗中就能读档" }
    if ($读档体.Contains("新游戏()")) { 报错 "PlayerService.读档 里又出现了 新游戏() —— 那等于「读到坏档就悄悄把玩家进度换成新角色」，是本链路最危险的兜底" }
}
$侧边栏 = 读 "Assets\Scripts\UI\HUD\侧边栏面板.cs"
$侧边栏存档体 = 取方法体 $侧边栏 "private void 存档()"
if ($null -eq $侧边栏存档体) { 报错 "侧边栏面板 里找不到 存档() —— 结构变了" }
elseif (-not $侧边栏存档体.Contains("存档门禁.存前检查()")) { 报错 "侧边栏面板.存档 没有走 存档门禁.存前检查()（自己写 if 就又会漂移）" }
$面板文本 = 读 "Assets\Scripts\UI\浮层\存档面板.cs"
$点保存体 = 取方法体 $面板文本 "private void 点保存("
$点读取体 = 取方法体 $面板文本 "private void 点读取("
if ($null -eq $点保存体) { 报错 "存档面板 里找不到 点保存() —— 结构变了" }
elseif (-not $点保存体.Contains("存档门禁.存前检查()")) { 报错 "存档面板.点保存 没有走 存档门禁.存前检查()" }
if ($null -eq $点读取体) { 报错 "存档面板 里找不到 点读取() —— 结构变了" }
elseif (-not $点读取体.Contains("存档门禁.读前检查()")) { 报错 "存档面板.点读取 没有走 存档门禁.读前检查()" }
# ★ 去注释再查：本仓库的注释里**故意**写着反例（"不做「欠一次、战后再补」那套"），
#   不去注释就会把"提醒你别这么写"的注释当成"这么写了"。段[1] 与段[9] 的读档体出于同一理由都去注释。
$自动器 = 去注释 (读 "Assets\Scripts\Services\存档\自动存档器.cs")
if (-not $自动器.Contains("存档门禁.正在战斗")) { 报错 "自动存档器 没有拦战斗中落盘 —— 会写出一个时间与状态对不上的档" }
if ($自动器.Contains("欠一次")) { 报错 "自动存档器 里出现了「欠一次、战后再补」那套机制 —— 用户口径是战斗中**直接不存**，不需要补账机制" }

# ---------- 10. 战局快照：抓得到、且不许把搜索表复制进存档 ----------
# 刀65：读档要回到离开那一格 + 周围的人/物/门/柜子全照旧。
# 段[10] 守的是这条链最容易出的两类错：① 忘了抓（读档还是回安全屋）；
#   ② 抓了但没剥离 `容器定义` —— 那是指向 DataService 模板的**共享引用**，
#      不置空会把每张搜索表都复制进存档（一个柜子一张表，20 个柜子就是十几倍体积）。
Write-Output "[10] 战局快照：保存要抓、且必须剥离 容器定义（防搜索表被复制进存档）"
$存档服务文本 = 读 "Assets\Scripts\Services\存档\SaveService.cs"
if (-not $存档服务文本.Contains("战局存档.取()")) { 报错 "SaveService 组装载荷时没有调 战局存档.取() —— 存档不带位置，读档只能回安全屋" }
$网格数据文本 = 读 "Assets\Scripts\Domain\网格\网格数据.cs"
if (-not $网格数据文本.Contains("去容器定义 ? null : 容器定义")) { 报错 "网格实体.副本 没有把 容器定义 按参数剥离 —— 搜索表会被整表复制进存档" }
if (-not $网格数据文本.Contains("public 网格数据 副本(bool 去容器定义)")) { 报错 "网格数据 缺少 副本() —— 战局快照没有深副本可用（直接存当前世界 = 快照会跟着游戏继续变）" }
$格子文本 = 读 "Assets\Scripts\Services\探索\格子探索服务.cs"
if (-not $格子文本.Contains("去容器定义: true")) { 报错 "格子探索服务.取快照 没有以「去容器定义: true」造副本" }
if (-not $格子文本.Contains("public 层快照 取快照(int 层类)")) { 报错 "格子探索服务 缺少 取快照() —— 三层就没法各自抓快照" }
if (-not $格子文本.Contains("迷雾 = 迷雾位图.打包(")) { 报错 "取快照 没有带上已探索位图 —— 房间层不在档案的永久迷雾里，读档会整间重看" }
$敌AI文本 = 读 "Assets\Scripts\Domain\大世界\大世界敌人AI.cs"
if (-not $敌AI文本.Contains("[Serializable]")) { 报错 "敌人AI态 没有 [Serializable] —— 快照存不下敌人状态（读档后「正在追你的那只」会回静默）" }

Write-Output ""
if ($script:失败 -eq 0) {
    Write-Output "✅ 存档接线核对通过（载荷类型 · 读档对齐 · 未开局冻结 · 路径单一出口 · 无死 API · 自动槽独立 · 面板入口 · 拆墙进档 · 战斗中禁存读 · 战局快照）"
    Write-Output "   ⚠ 只查接线与形状；行为断言在 Tools/存档验证，真 JsonUtility 往返靠游戏内「存档往返自检」"
    exit 0
}
Write-Output ("❌ 存档接线核对失败：{0} 处" -f $script:失败); exit 1
