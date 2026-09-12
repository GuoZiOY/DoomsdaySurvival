# ============================================================
# 探索接线核对：盯住"格子层 UI 优化"所依赖的几条接线不变量（不打开 Unity 就能跑）
# 为什么需要：刀18 给 探索图层 加了两处**去重短路**（雾按 视野版本 跳过、路径按签名跳过）与
#   一处池槽跳过。短路类优化最危险的不是性能，而是**该重画的时候没重画** —— 表现成"雾停在上一格"
#   "路径画的是老路线"，而且不会报任何错。这些前提本来只写在注释里，这条脚本把它们变成红灯。
# 边界：只证明接线没被改回去；真表现仍要进 Unity 看（走路时雾跟不跟手、路径会不会停在旧路线上）。
# 用法（仓库根）：& ".\Tools\探索接线核对.ps1"
# 编码纪律：一律 [IO.File]::ReadAllText + 显式 UTF8（PS 5.1 默认按 GBK 读中文）
# ============================================================
$ErrorActionPreference = "Stop"
$根 = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$服务目录 = Join-Path $根 "Assets\Scripts\Services"
$探索UI = Join-Path $根 "Assets\Scripts\UI\探索"
$script:失败 = 0

function 报错([string]$文本) { Write-Output ("  ✗ " + $文本); $script:失败++ }
function 提示([string]$文本) { Write-Output ("      · " + $文本) }
function 读行([string]$全路径) { return ([System.IO.File]::ReadAllText($全路径, [System.Text.Encoding]::UTF8) -split "`r?`n") }
function 去注释([string[]]$行) { return @($行 | ForEach-Object { $_ -replace '//.*$', '' }) }

function 找行([string[]]$行, [string]$模式) {
    for ($i = 0; $i -lt $行.Count; $i++) { if ($行[$i] -match $模式) { return $i } }
    return -1
}
# 从签名行起按花括号配平取函数体（字符串里的花括号不参与）
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
function 体内有([string[]]$体, [string]$模式) {
    foreach ($l in $体) { if (($l -replace '//.*$', '') -match $模式) { return $true } }
    return $false
}

Write-Output "=== 探索接线核对（刀18 建立的接线不变量）==="

# ---------- 1. 雾的输入只在 刷新视野() 里变 ----------
# 为什么钉：刷迷雾 现在会在"视野版本没变"时直接返回。若有人在别处改了
#           当前可见 / 已探索 / 当前时段，雾就会**停在旧状态**且不报错。
# 判据（窗口式，别当精确判据）：写入点要么落在下面三个例外函数的函数体内，
#           要么前后 25 行内必须有一次 刷新视野() 调用。
# 三个例外（写在明处，不是"漏过去"）：
#   · 刷新视野()     —— 它自己就是权威；
#   · 装回迷雾记忆()  —— 是各层"进入"流程的一段，其调用方紧随 刷新视野()（区域/房间/大世界三处已核）；
#   · 清空世界态()    —— 发生在**离场**，图层随后整层重建，不需要重画。
Write-Output "[1] 雾的输入写入点（当前可见 / 已探索 / 当前时段）附近必须有 刷新视野()"
$例外函数 = @('刷新视野', '装回迷雾记忆', '清空世界态')
$写入点 = @()
foreach ($f in (Get-ChildItem $服务目录 -Recurse -Filter *.cs)) {
    $行 = 读行 $f.FullName
    $去 = 去注释 $行
    for ($i = 0; $i -lt $去.Count; $i++) {
        # 字段/属性声明行不算"写入点"（如 `protected HashSet<int> 当前可见 = new HashSet<int>();`）
        if ($去[$i] -match '^\s*(public|protected|private|internal)\s' -and $去[$i] -match ';\s*$') { continue }
        $是写 = ($去[$i] -match '当前可见\s*=\s*[^=]') -or ($去[$i] -match '当前可见\s*\.\s*Clear') `
             -or ($去[$i] -match '已探索\s*\.\s*(Clear|Add|UnionWith)') -or ($去[$i] -match '当前时段\s*=\s*[^=]')
        if ($是写) { $写入点 += [pscustomobject]@{ 名 = $f.Name; 行 = ($i + 1); 文本 = $行[$i].Trim(); 索引 = $i; 行数组 = $行 } }
    }
}
$漏 = 0
foreach ($w in $写入点) {
    $放行 = $false
    foreach ($名 in $例外函数) {
        $起 = 找行 $w.行数组 ("(private|protected|public)\s+[^\r\n]*\b" + $名 + "\s*\(")
        if ($起 -lt 0) { continue }
        $体 = 取函数体 $w.行数组 $起
        if ($null -ne $体 -and $w.索引 -ge $起 -and $w.索引 -le ($起 + $体.Count - 1)) { $放行 = $true; break }
    }
    if ($放行) { continue }
    $近 = $false
    for ($j = [Math]::Max(0, $w.索引 - 25); $j -le [Math]::Min($w.行数组.Count - 1, $w.索引 + 25); $j++) {
        if (($w.行数组[$j] -replace '//.*$', '') -match '刷新视野\s*\(\s*\)') { $近 = $true; break }
    }
    if (-not $近) {
        报错 ("{0}:{1} 改了雾的输入，但它不在例外函数里、附近 25 行也没有 刷新视野()：{2}" -f $w.名, $w.行, $w.文本)
        $漏++
    }
}
Write-Output ("  写入点 {0} 处，可疑 {1} 处（例外函数：{2}）" -f $写入点.Count, $漏, ($例外函数 -join ' / '))
if ($写入点.Count -eq 0) { 报错 "一处雾输入写入点都没扫到 —— 判据失效了（符号改名了？脚本要同步更新）" }

# ---------- 2. 去重键必须在 重建() 里作废 ----------
# 为什么钉：重建会把雾/路径的图**全新建一遍**，此时"版本没变"的短路必须失效，
#           否则重建之后那一层雾/路径永远是空的（黑屏/没有路线）。
Write-Output "[2] 探索图层.重建() 必须把两个去重键重置"
$图层 = 读行 (Join-Path $探索UI "探索图层.cs")
$重建体 = 取函数体 $图层 (找行 $图层 'private\s+void\s+重建\s*\(')
if ($null -eq $重建体) { 报错 "取不到 探索图层.重建() 的函数体（改名了？脚本要同步更新）" }
else {
    if (-not (体内有 $重建体 '上次迷雾版本\s*=\s*int\.MinValue')) { 报错 "重建() 没有重置 上次迷雾版本 —— 重建后雾不会重画" }
    if (-not (体内有 $重建体 '上次路径签名\s*=\s*int\.MinValue')) { 报错 "重建() 没有重置 上次路径签名 —— 重建后路径不会重画" }
}

# ---------- 3. 池重排后必须强制重画雾 ----------
Write-Output "[3] 池重排（更新池）后必须 刷迷雾(true)"
$更新池体 = 取函数体 $图层 (找行 $图层 'private\s+void\s+更新池\s*\(')
if ($null -eq $更新池体) { 报错 "取不到 更新池() 的函数体" }
elseif (-not (体内有 $更新池体 '刷迷雾\s*\(\s*true\s*\)')) { 报错 "更新池() 末尾没有 刷迷雾(true) —— 池槽换了格，雾会停在上一屏" }

# ---------- 4. 路径池必须"从终点往前"分配 ----------
# 为什么钉：走路时路径每步从队首缩短一格。按"从头数"分配会让几乎每张路径图都改画到另一格
#           （离线实测：一条 30 格直线走完，平均每步 16 张 → 从尾分配 1 张）。
Write-Output "[4] 刷路径 必须从终点往前分配池槽（每步只动队尾）"
$刷路径体 = 取函数体 $图层 (找行 $图层 'private\s+void\s+刷路径\s*\(')
if ($null -eq $刷路径体) { 报错 "取不到 刷路径() 的函数体" }
else {
    if (-not (体内有 $刷路径体 'for\s*\(int\s+i\s*=\s*路\.Count\s*-\s*1;\s*i\s*>=\s*0')) { 报错 "刷路径 不再「从终点往前」分配（顺序又倒回去了？）" }
    if (体内有 $刷路径体 'for\s*\(int\s+i\s*=\s*0;\s*i\s*<\s*路\.Count\s*&&') { 报错 "刷路径 里又出现了「从头数」的分配循环（i = 0; i < 路.Count && 用 < 路径池.Count）" }
}

# ---------- 5. 实体框的 CanvasGroup 必须走缓存 ----------
Write-Output "[5] 探索三层 更新实体框 不许再 GetComponent<CanvasGroup>（必须用 框.组 缓存）"
$命中 = 0
foreach ($f in (Get-ChildItem $探索UI -Filter "*网格面板.cs")) {
    $行 = 去注释 (读行 $f.FullName)
    for ($i = 0; $i -lt $行.Count; $i++) {
        if ($行[$i] -match '根\s*\.\s*GetComponent<\s*CanvasGroup\s*>') { 报错 ("{0}:{1} 又走回了每次刷新 GetComponent<CanvasGroup>" -f $f.Name, ($i + 1)); $命中++ }
        if ($行[$i] -match '=\s*框\.组\s*;') { $命中++ }
    }
}
if ($命中 -eq 0) { 报错 "三个探索面板里都没看到 框.组 —— 缓存那条路没接上" }

Write-Output ""
if ($script:失败 -eq 0) {
    Write-Output "✅ 探索接线核对通过（雾输入写入点 / 去重键重置 / 池重排强制刷雾 / 路径从尾分配 / CanvasGroup 缓存）"
    Write-Output "   ⚠ 只证明接线没被改回去；走路时「雾跟不跟手、路径会不会停在旧路线」仍要进 Unity 看一眼"
    exit 0
}
Write-Output ("❌ 探索接线核对失败：{0} 处" -f $script:失败); exit 1
