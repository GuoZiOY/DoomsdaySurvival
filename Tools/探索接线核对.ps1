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

# ---------- 6. 探索层必须复用基类的两个"省图/省组件"开关 ----------
# 为什么钉：探索层的地表层逐格盖满且不透明，被盖住的底格是**看不见的图**（区域层 704 张）；
#           而探索层的 允许开始拖拽 恒 false，实体框上的 物品拖拽 组件**永远不会做任何事**（区域层几百个）。
#           两条都是"白建"，但它们一旦被改回默认（true），不会报错、只是白花 —— 所以钉在这里。
Write-Output "[6] 探索网格面板 必须覆写 需要底格 / 需要拖拽组件"
$探索面板 = 读行 (Join-Path $探索UI "探索网格面板.cs")
if ($null -eq $探索面板) { 报错 "读不到 探索网格面板.cs" }
else {
    $底格行 = 找行 $探索面板 'override\s+bool\s+需要底格'
    if ($底格行 -lt 0) { 报错 "探索网格面板 没有覆写 需要底格 —— 底格会逐格全建（区域层 704 张看不见的图）" }
    else {
        $体 = 取函数体 $探索面板 $底格行
        if ($null -eq $体) { 报错 "取不到 需要底格 的函数体" }
        elseif (-not (体内有 $体 '界外')) {
            报错 "需要底格 的覆写里没有「界外」—— 界外格的地表是故意不铺的，底格一去掉那块就变成底盘色"
        }
    }
    if ((找行 $探索面板 'override\s+bool\s+需要拖拽组件') -lt 0) { 报错 "探索网格面板 没有覆写 需要拖拽组件 —— 每格实体框会白挂一个永不生效的拖拽组件" }
}

# ---------- 7. 基类必须真的用上这两个开关 ----------
Write-Output "[7] 基类 重建网格结构 / 挂接交互 必须用上这两个开关"
$渲染 = 读行 (Join-Path $根 "Assets\Scripts\UI\背包\网格面板基类\网格面板基类.渲染.cs")
$重建体 = 取函数体 $渲染 (找行 $渲染 'private\s+void\s+重建网格结构\s*\(')
if ($null -eq $重建体) { 报错 "取不到 重建网格结构 的函数体" }
elseif (-not (体内有 $重建体 '需要底格\s*\(')) { 报错 "重建网格结构 建底格时没问 需要底格(列,行) —— 那个开关等于没接" }
$挂接体 = 取函数体 $渲染 (找行 $渲染 'protected\s+void\s+挂接交互\s*\(')
if ($null -eq $挂接体) { 报错 "取不到 挂接交互 的函数体" }
else {
    if (-not (体内有 $挂接体 '需要拖拽组件')) { 报错 "挂接交互 没有按 需要拖拽组件 早退" }
    if (-not (体内有 $挂接体 'AddComponent<物品拖拽>')) { 报错 "挂接交互 里连 物品拖拽 都不挂了 —— 背包/容器/家具的拖拽会整个失效" }
}

# ---------- 8. 交互层只能是"一整张命中面" ----------
# 为什么钉：v51 刀20 把"每格一张透明图 + 一个 探索格点击 组件"换成了整层一张面 + 坐标换算。
#   改回去不会报错、也不会画错，只是区域层又白建 704 个对象 —— 所以钉在这里。
Write-Output "[8] 交互层必须是一整张命中面（不许退回逐格命中图）"
if (-not (体内有 $图层 'private\s+void\s+建交互面\s*\(')) { 报错 "探索图层 里没有 建交互面()" }
$重建体2 = 取函数体 $图层 (找行 $图层 'private\s+void\s+重建\s*\(')
if ($null -eq $重建体2) { 报错 "取不到 重建() 的函数体" }
elseif (-not (体内有 $重建体2 '建交互面\s*\(\s*\)')) { 报错 "重建() 没有调用 建交互面() —— 交互层会没有命中目标（点了没反应）" }
$逐格回潮 = 0
foreach ($f in (Get-ChildItem $探索UI -Filter *.cs)) {
    $去 = 去注释 (读行 $f.FullName)
    for ($i = 0; $i -lt $去.Count; $i++) {
        if ($去[$i] -match 'AddComponent<\s*探索格点击\s*>') { 报错 ("{0}:{1} 又挂回了 探索格点击（逐格组件那条路）" -f $f.Name, ($i + 1)); $逐格回潮++ }
        if ($去[$i] -match '池交互|交互格') { 报错 ("{0}:{1} 又出现了逐格交互图（池交互 / 交互格）" -f $f.Name, ($i + 1)); $逐格回潮++ }
    }
}
if (Test-Path (Join-Path $探索UI "探索格点击.cs")) { 报错 "探索格点击.cs 又回来了（它已被 探索交互面 取代）" }
if (-not (Test-Path (Join-Path $探索UI "探索交互面.cs"))) { 报错 "探索交互面.cs 不见了" }

# ---------- 9. 平铺格线：开关接上 + Tiled + 按画布参考值校准 ----------
# 为什么钉：`Image.type = Tiled` 的格子尺寸 = 贴图边长 ÷ (sprite.pixelsPerUnit ÷ 画布.referencePixelsPerUnit × 乘数)。
#   少写 pixelsPerUnitMultiplier 那一行，画布参考值不是 100 时线会整体错位（不报错，只是"网格线对不上格"）。
Write-Output "[9] 平铺格线（探索层开关 + Tiled + 画布参考值校准）"
$探索面板2 = 读行 (Join-Path $探索UI "探索网格面板.cs")
if ((找行 $探索面板2 'override\s+bool\s+格线用平铺') -lt 0) { 报错 "探索网格面板 没有覆写 格线用平铺 —— 区域层又会逐段建 1462 张线图" }
if (-not (体内有 $渲染 '格线用平铺')) { 报错 "基类 画分隔线 没认 格线用平铺 这个开关" }
$平铺体 = 取函数体 $渲染 (找行 $渲染 'private\s+void\s+画平铺格线\s*\(')
if ($null -eq $平铺体) { 报错 "取不到 画平铺格线() 的函数体" }
else {
    if (-not (体内有 $平铺体 'Image\.Type\.Tiled')) { 报错 "画平铺格线 没把 Image.type 设成 Tiled" }
    if (-not (体内有 $平铺体 'pixelsPerUnitMultiplier')) { 报错 "画平铺格线 没按画布参考值校准 pixelsPerUnitMultiplier —— 参考值不是 100 时线会错位" }
    if (-not (体内有 $平铺体 'referencePixelsPerUnit')) { 报错 "画平铺格线 没读画布 referencePixelsPerUnit" }
}
$贴图体 = 取函数体 $渲染 (找行 $渲染 'private\s+static\s+Sprite\s+线格贴图取\s*\(')
if ($null -eq $贴图体) { 报错 "取不到 线格贴图取() 的函数体" }
elseif (-not (体内有 $贴图体 'SpriteMeshType\.FullRect')) { 报错 "线格贴图 没用 FullRect 建 —— Tiled 会因紧包围盒错位" }

# ---------- 10. 实体框骨架必须留在基类 ----------
# 为什么钉：v51 刀21 把"建物体 → 框根透明 → 玩家不吃点击 → CanvasGroup → 描边 → 定位 → 高光层 → 挂交互"
#   这段骨架从三个面板上提到 探索网格面板（原来 25 行 × 3 份逐字相同）。
#   派生要是又整段重写一遍，就会重新长出三份会各自漂的骨架 —— 而且不会有任何报错。
Write-Output "[10] 实体框骨架留在基类（派生只写 需要实体描边 / 建实体外观）"
$探索面板3 = 去注释 (读行 (Join-Path $探索UI "探索网格面板.cs"))
$骨架行 = 找行 $探索面板3 'override\s+物品框\s+创建实体框'
if ($骨架行 -lt 0) { 报错 "探索网格面板 里找不到 创建实体框 —— 骨架没了？" }
elseif ($探索面板3[$骨架行] -notmatch 'sealed') {
    报错 "探索网格面板.创建实体框 没有 sealed —— 派生可以整段重写，骨架会重新长出三份（见本节开头）"
}
foreach ($名 in @('需要实体描边', '建实体外观')) {
    if ((找行 $探索面板3 ("protected\s+abstract\s+[^\r\n]*\b" + $名 + "\s*\(")) -lt 0) { 报错 ("基类没有把 {0} 声明成 abstract 钩子" -f $名) }
}
foreach ($f in (Get-ChildItem $探索UI -Filter "*网格面板.cs")) {
    if ($f.Name -eq "探索网格面板.cs") { continue }
    $去 = 去注释 (读行 $f.FullName)
    $重写 = 找行 $去 'override\s+物品框\s+创建实体框'
    if ($重写 -ge 0) { 报错 ("{0}:{1} 又整段重写了 创建实体框（骨架必须留在基类）" -f $f.Name, ($重写 + 1)) }
    foreach ($名 in @('需要实体描边', '建实体外观')) {
        if ((找行 $去 ("override\s+[^\r\n]*\b" + $名 + "\s*\(")) -lt 0) { 报错 ("{0} 没有覆写 {1} —— 这一层的实体外观会变成空白" -f $f.Name, $名) }
    }
    # 区域层与大世界层的 更新实体框 / 整体档 曾是**逐行相同的两份**（刀21b 收进基类）→ 不许再各自长出来
    if ($f.Name -in @('区域网格面板.cs', '大世界网格面板.cs')) {
        foreach ($名 in @('更新实体框', '整体档')) {
            $行号 = 找行 $去 ("override\s+void\s+" + $名 + "\s*\(" + "|private\s+视野档\s+" + $名 + "\s*\(")
            if ($行号 -ge 0) { 报错 ("{0}:{1} 又自己实现了 {2}（这两层原来逐行相同，已收进基类）" -f $f.Name, ($行号 + 1), $名) }
        }
    }
}

Write-Output ""
if ($script:失败 -eq 0) {
    Write-Output "✅ 探索接线核对通过（雾输入写入点 / 去重键重置 / 池重排强制刷雾 / 路径从尾分配 / CanvasGroup 缓存 / 底格与拖拽开关 / 单面交互层 / 平铺格线 / 实体框骨架在基类）"
    Write-Output "   ⚠ 只证明接线没被改回去；走路时「雾跟不跟手、路径会不会停在旧路线」仍要进 Unity 看一眼"
    exit 0
}
Write-Output ("❌ 探索接线核对失败：{0} 处" -f $script:失败); exit 1
