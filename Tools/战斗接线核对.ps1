# ============================================================
# 战斗接线核对：把"结构 / 接线"层面的不变量离线钉住（不打开 Unity 就能跑）
# 为什么需要：刀17（档1 #2/#3/#4/#9）修的全是**接线**问题 —— 战斗时钟挂在 UI 面板的 Update 上、
#             `结束战斗` 里内联 `返回()`、`返回()` 先清结果节点再分派、技能可用性判定散在 UI。
#             这几类改动**不会让编译失败**，**不会让任何现有验证器变红**（四个生成验证器 + 两个数据
#             核对都不碰 Services/UI），只会在运行期表现成"战斗突然冻结 / 面板切不回去 /
#             按钮点了没反应"。所以只能靠这条脚本盯着它们别再被改回去。
# 边界（请勿误读）：本脚本**只证明接线没被改回去**，它不证明运行期行为正确。
#             真正的行为验收要进 Unity 打三场：房间 / 区域 / 大世界 各 胜·败·逃，
#             确认"面板都切回、尸体都落地、胜利后不需要点 [继续]"。
# 用法（仓库根）：& ".\Tools\战斗接线核对.ps1"
# 编码纪律：一律 [IO.File]::ReadAllText + 显式 UTF8（PS 5.1 的 Get-Content 会把 UTF-8 中文按 GBK 读）
# 已知粗糙处（有意从简，够用即可）：去注释用 `-replace '//.*$'`，若将来某行字符串里出现 `//`，
#             那一行的后半句会被当成注释丢掉 —— 只会漏报、不会误报（真漏了会在别处撞出来）。
# ============================================================
$ErrorActionPreference = "Stop"
$根 = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$脚本目录 = Join-Path $根 "Assets\Scripts"
$script:失败 = 0

function 报错([string]$文本) { Write-Output ("  ✗ " + $文本); $script:失败++ }
function 提示([string]$文本) { Write-Output ("      · " + $文本) }

function 读行([string]$相对路径) {
    $p = Join-Path $根 $相对路径
    if (-not (Test-Path $p)) { 报错 ("源代码文件不存在：" + $相对路径); return $null }
    return ([System.IO.File]::ReadAllText($p, [System.Text.Encoding]::UTF8) -split "`r?`n")
}

function 去注释([string[]]$行) {
    return @($行 | ForEach-Object { $_ -replace '//.*$', '' })
}

# 找包含某模式的第一行（-1 = 没找到）。模式是正则，调用方自行转义括号。
function 找行([string[]]$行, [string]$模式) {
    for ($i = 0; $i -lt $行.Count; $i++) { if ($行[$i] -match $模式) { return $i } }
    return -1
}

# 从"签名行"起按大括号配平取函数体（含签名行、含结束的 `}`）。配平失败 → $null。
# 花括号在字符串字面量里不参与配平（本仓库的 `$"...{x}..."` 两边都有花括号，即使参与也不影响配平）。
function 取函数体([string[]]$行, [int]$签名行) {
    if ($签名行 -lt 0) { return $null }
    $深 = 0; $起过 = $false; $出 = New-Object System.Collections.Generic.List[string]
    for ($i = $签名行; $i -lt $行.Count; $i++) {
        $出.Add($行[$i])
        $文本 = ($行[$i] -replace '//.*$', '')
        $串内 = $false
        for ($j = 0; $j -lt $文本.Length; $j++) {
            $c = $文本[$j]
            if ($c -eq [char]34) { $串内 = -not $串内; continue }
            if ($串内) { continue }
            if ($c -eq '{') { $深++; $起过 = $true }
            elseif ($c -eq '}') { $深--; if ($起过 -and $深 -le 0) { return $出.ToArray() } }
        }
    }
    return $null
}

# 函数体内是否出现某 token（已去注释）
function 体内有([string[]]$体, [string]$模式) {
    foreach ($l in $体) { if (($l -replace '//.*$', '') -match $模式) { return $true } }
    return $false
}

Write-Output "=== 战斗接线核对（刀17 建立的接线不变量）==="

# ---------- 1. 战斗时钟归属：只许 世界时间管理器.驱动 推进战斗 ----------
# 为什么钉：原来时钟在 `战斗沙盒面板.Update`，面板隐藏 = SetActive(false) → Update 停 → 战斗静默冻结。
Write-Output "[1] 战斗时钟归属（只许 世界时间管理器.驱动 调 推进战斗；UI 不许驱动战斗）"
$定义处 = @(); $调用处 = @()
foreach ($f in (Get-ChildItem $脚本目录 -Recurse -Filter *.cs)) {
    $相对 = $f.FullName.Substring($根.Length + 1)
    $行 = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8) -split "`r?`n"
    $去 = 去注释 $行
    for ($i = 0; $i -lt $去.Count; $i++) {
        if ($去[$i] -match 'void\s+推进战斗\s*\(') { $定义处 += ($相对 + ":" + ($i + 1)) }
        if ($去[$i] -match '\.\s*推进战斗\s*\(') { $调用处 += ($相对 + ":" + ($i + 1)) }
    }
}
if ($定义处.Count -ne 1) { 报错 ("推进战斗 的定义应为 1 处，实际 {0} 处：{1}" -f $定义处.Count, ($定义处 -join "、")) }
else { Write-Output ("  定义：{0}" -f $定义处[0]) }
if ($调用处.Count -eq 0) { 报错 "全仓库没有任何地方调用 推进战斗 —— 战斗永远不会推进（驱动没接上）" }
foreach ($c in $调用处) {
    Write-Output ("  调用：{0}" -f $c)
    if ($c -notlike "Assets\Scripts\Services\世界\世界时间管理器.cs:*") {
        报错 ("推进战斗 的调用跑出了驱动（{0}）—— 战斗时钟不许再挂回 UI/别处" -f $c)
    }
}

# ---------- 2. 自动回派必须有驱动方 ----------
# 为什么钉：结束战斗 只登记"待回派"，落地靠驱动每帧调 处理自动回派()；
#           这一行被删掉 → 战斗结束后没有任何东西切回原面板（面板停在一场已经打完的战斗上）。
Write-Output "[2] 自动回派（结束战斗 登记 → 驱动下一帧落地）"
$回派定义 = @(); $回派调用 = @()
foreach ($f in (Get-ChildItem $脚本目录 -Recurse -Filter *.cs)) {
    $相对 = $f.FullName.Substring($根.Length + 1)
    $行 = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8) -split "`r?`n"
    $去 = 去注释 $行
    for ($i = 0; $i -lt $去.Count; $i++) {
        if ($去[$i] -match 'void\s+处理自动回派\s*\(') { $回派定义 += ($相对 + ":" + ($i + 1)) }
        elseif ($去[$i] -match '\.\s*处理自动回派\s*\(') { $回派调用 += ($相对 + ":" + ($i + 1)) }
    }
}
if ($回派定义.Count -ne 1) { 报错 ("处理自动回派 的定义应为 1 处，实际 {0} 处" -f $回派定义.Count) }
if ($回派调用.Count -eq 0) { 报错 "没有任何地方调用 处理自动回派 —— 战斗结束后面板切不回去（静默卡死）" }
else { Write-Output ("  调用：{0}" -f ($回派调用 -join "、")) }
$驱动文件 = "Assets\Scripts\Services\世界\世界时间管理器.cs"
if (-not ($回派调用 | Where-Object { $_ -like "$驱动文件*" })) { 报错 ("处理自动回派 没有在驱动（{0}）里被调用" -f $驱动文件) }

# ---------- 3. 结算与回派拆开 + 幂等锁在分派之后 ----------
Write-Output "[3] 结算与回派拆开（结束战斗 只登记；分派成功才清结果节点）"
# ★ v51 刀25：BattleService 已按分节拆成多个 partial 文件（同一个类），本守门人的判据跨这些文件
#   → 拼接全部 BattleService*.cs 再查（拼接顺序对"函数体提取"没有影响）。
$战目录 = "Assets\Scripts\Services\战斗"
$战文件 = Get-ChildItem (Join-Path $根 $战目录) -Filter "BattleService*.cs" | Sort-Object Name
$战 = @()
foreach ($f in $战文件) { $战 += 读行 (Join-Path $战目录 $f.Name) }
Write-Output ("  BattleService 分部文件 {0} 个：{1}" -f $战文件.Count, (($战文件 | ForEach-Object { $_.Name }) -join "、"))
$结束行 = 找行 $战 'private\s+void\s+结束战斗\s*\('
$结束体 = 取函数体 $战 $结束行
if ($null -eq $结束体) { 报错 "取不到 结束战斗 的函数体（签名改名了？脚本要同步更新）" }
else {
    if (体内有 $结束体 '返回\s*\(\s*\)') { 报错 "结束战斗 里又出现了 返回() 调用 —— 结算与切层必须拆开（切层失败会连累结算）" }
    if (-not (体内有 $结束体 '自动回派待办\s*=')) { 报错 "结束战斗 没有登记 自动回派待办 —— 格子层战斗会停在结算画面上" }
}
$返回行 = 找行 $战 'public\s+void\s+返回\s*\(\s*\)'
if ($返回行 -lt 0) { 报错 "BattleService.返回() 不见了（面板 [继续] 按钮的入口）" }
$回派行 = 找行 $战 'private\s+void\s+回派\s*\('
$回派体 = 取函数体 $战 $回派行
if ($null -eq $回派体) { 报错 "取不到 回派 的函数体 —— [4] 的幂等修正就没落地" }
else {
    # 幂等必须靠"分派成功才清"，不许回到"第一件事就清空结果节点"
    if (-not (体内有 $回派体 '已落地\s*\)\s*结果节点\s*=')) { 报错 "回派 里没有「分派成功才清结果节点」（幂等锁又跑回分派之前了？）" }
    if (-not (体内有 $回派体 'catch')) { 报错 "回派 没有 try/catch —— 目标层抛异常会穿回 推进战斗 的中途" }
}

# ---------- 4. 技能可用性判定只有一份（服务层） ----------
Write-Output "[4] 技能「能不能按」的判定只有一份（BattleService.可释放技能，UI 只消费）"
$可释处 = @()
foreach ($f in (Get-ChildItem $脚本目录 -Recurse -Filter *.cs)) {
    $相对 = $f.FullName.Substring($根.Length + 1)
    $行 = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8) -split "`r?`n"
    $去 = 去注释 $行
    for ($i = 0; $i -lt $去.Count; $i++) { if ($去[$i] -match 'bool\s+可释放技能\s*\(') { $可释处 += ($相对 + ":" + ($i + 1)) } }
}
if ($可释处.Count -ne 1) { 报错 ("可释放技能 的定义应为 1 处，实际 {0} 处：{1}" -f $可释处.Count, ($可释处 -join "、")) }
else { Write-Output ("  定义：{0}" -f $可释处[0]) }
$面板 = 读行 "Assets\Scripts\UI\战斗\战斗沙盒面板.cs"
if ($面板 -and -not ((去注释 $面板) | Where-Object { $_ -match '\.\s*可释放技能\s*\(' })) {
    报错 "战斗沙盒面板 没有调用 可释放技能 —— 技能槽又变成「按下去才知道放不出来」了"
}
$槽 = 读行 "Assets\Scripts\UI\战斗\技能槽.cs"
if ($槽) {
    foreach ($l in (去注释 $槽)) {
        if ($l -match '精力|弹药|消耗物品|冷却剩余\s*\(') {
            报错 ("技能槽 里出现了资源判定（{0}）—— 判定只能留在服务层，UI 自己算就会和服务不一致" -f $l.Trim())
        }
    }
}

# ---------- 5. "档案.生命/行动点"的战斗内写入点清单（静默结算错的守门人） ----------
# 为什么钉：结算回写 的口径是"生命/行动点以战斗单位为准" ——
#           任何在战斗中直接改 档案.生命/行动点 的地方（升级半状态、失败惩罚半状态）
#           都必须把同一份恢复**同步进战斗单位**，否则会被反向覆盖，静默、无报错、日志照旧。
Write-Output "[5] 档案.生命 / 档案.行动点 的写入点（必须都在 结算失败惩罚 / 结算回写，且都已同步战斗单位）"
$许可区间 = @()
foreach ($名 in @('结算失败惩罚', '结算回写')) {
    $s = 找行 $战 ("private\s+void\s+" + $名 + "\s*\(")
    $b = 取函数体 $战 $s
    if ($null -eq $b) { 报错 ("取不到 {0} 的函数体" -f $名) }
    else { $许可区间 += ,@($s, ($s + $b.Count - 1)) }
}
$写入点 = @()
for ($i = 0; $i -lt $战.Count; $i++) {
    $去 = $战[$i] -replace '//.*$', ''
    if ($去 -match '档案\s*\.\s*(生命|行动点)\s*=[^=]') { $写入点 += $i }
}
foreach ($i in $写入点) {
    $在区间 = $false
    foreach ($r in $许可区间) { if ($i -ge $r[0] -and $i -le $r[1]) { $在区间 = $true } }
    Write-Output ("  {0}: {1}" -f ($i + 1), $战[$i].Trim())
    if (-not $在区间) {
        报错 ("第 {0} 行在 结算失败惩罚/结算回写 之外改 档案.生命/行动点 —— 请确认它同时改了战斗单位（否则会被结算回写反向覆盖）" -f ($i + 1))
    }
}
if ($写入点.Count -ne 4) {
    提示 ("写入点从基准的 4 处变成 {0} 处 —— 若新增/删除了写入点，请重新确认「同步战斗单位」这条不变量" -f $写入点.Count)
}

Write-Output ""
# ============================================================
# [6] 战斗的掷点必须走 随机源（v51 刀33 / 档2 #26）
# 为什么：战斗原来直接掷 UnityEngine.Random（全局静态、不可播种，序列还会被特效/UI 搅动），
#   `战斗单位` 里还自己 `new System.Random()`（按时间播种 → 一场战斗无法复现、且每次分配）。
#   改完之后"一场战斗 = 初始状态 + 种子"：报 bug 能带种子离线重放、胜率能离线批量量。
#   这类回退**不会让编译失败、不会让任何验证器变红**（域层自己掷随机一样能跑），
#   只会静默地让"可复现"这个能力消失 —— 所以只能靠这条盯着。
# ============================================================
Write-Output "[6] 掷点唯一入口：Services/战斗 不许出现 UnityEngine.Random；Domain/战斗 不许自己掷"
$战斗服务目录 = Join-Path $脚本目录 "Services\战斗"
$域战斗目录 = Join-Path $脚本目录 "Domain\战斗"
if (-not (Test-Path (Join-Path $域战斗目录 "随机源.cs"))) { 报错 "Domain/战斗/随机源.cs 不存在（掷点入口被删了？）" }

$服务掷点 = New-Object System.Collections.Generic.List[string]
foreach ($f in (Get-ChildItem $战斗服务目录 -Filter *.cs)) {
    $行们 = [System.IO.File]::ReadAllLines($f.FullName, [System.Text.Encoding]::UTF8)
    for ($i = 0; $i -lt $行们.Count; $i++) {
        $去 = $行们[$i] -replace '//.*$', ''
        if ($去 -match '\bRandom\s*\.\s*(value|Range|InitState)') { $服务掷点.Add(("{0}:{1} {2}" -f $f.Name, ($i + 1), $行们[$i].Trim())) }
    }
}
if ($服务掷点.Count -eq 0) { Write-Output "  ✓ Services/战斗：0 处 UnityEngine.Random（8 处掷点都走 随机源）" }
else { foreach ($x in $服务掷点) { 报错 ("{0} —— 直接掷 UnityEngine.Random：不可播种/不可复现，请改走 随机源.值()/范围()" -f $x) } }

$域掷点 = New-Object System.Collections.Generic.List[string]
foreach ($f in (Get-ChildItem $域战斗目录 -Filter *.cs)) {
    if ($f.Name -eq "随机源.cs") { continue }   # 唯一允许持有 System.Random 的文件
    $行们 = [System.IO.File]::ReadAllLines($f.FullName, [System.Text.Encoding]::UTF8)
    for ($i = 0; $i -lt $行们.Count; $i++) {
        $去 = $行们[$i] -replace '//.*$', ''
        if ($去 -match 'new\s+(System\.)?Random\s*\(' -or $去 -match '\bRandom\s*\.\s*(value|Range)') { $域掷点.Add(("{0}:{1} {2}" -f $f.Name, ($i + 1), $行们[$i].Trim())) }
    }
}
if ($域掷点.Count -eq 0) { Write-Output "  ✓ Domain/战斗：0 处自掷随机（掷点在服务层，域层只吃参数）" }
else { foreach ($x in $域掷点) { 报错 ("{0} —— 域层自己掷随机：无法同种子复现，请把掷点当参数传进来" -f $x) } }

$服务 = 读行 "Assets\Scripts\Services\战斗\BattleService.cs"
if ($null -ne $服务) {
    $有字段 = $false; $有派生 = $false
    foreach ($l in $服务) {
        $去 = $l -replace '//.*$', ''
        if ($去 -match '随机源\s+随机\s*;') { $有字段 = $true }
        if ($去 -match '随机\s*=\s*new\s+随机源\(') { $有派生 = $true }
    }
    if (-not $有字段) { 报错 "BattleService 少了「随机源 随机;」字段（掷点入口没了）" }
    if (-not $有派生) { 报错 "BattleService 开局没有 new 随机源 —— 种子没接上，可复现能力等于没有" }
    if ($有字段 -and $有派生) { Write-Output "  ✓ BattleService：随机源 字段 + 开局派生种子（世界种子 + 场次）" }
}

Write-Output ""
if ($script:失败 -eq 0) {
    Write-Output "✅ 战斗接线核对通过（时钟归属 / 自动回派 / 结算与回派拆开 / 可用性判定唯一 / 生命行动点写入点 / 掷点唯一入口）"
    Write-Output "   ⚠ 这只证明**接线没被改回去**；运行期行为仍需进 Unity 打三场（房间·区域·大世界 各 胜/败/逃）"
    exit 0
}
Write-Output ("❌ 战斗接线核对失败：{0} 处" -f $script:失败); exit 1
