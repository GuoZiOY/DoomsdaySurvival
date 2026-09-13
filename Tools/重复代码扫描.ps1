# ============================================================
# 重复代码扫描：找出"该提到基类/该收敛成单一实现"的重复（不打开 Unity 就能跑）
# 为什么需要：这个项目栽过几次"同一规则多份实现，改一处漏一处"（门口四邻 5 份、夹 6 份、
#   刀21 之前 创建实体框 的 25 行骨架在三个面板里各写一遍）。肉眼扫总会漏，所以让工具先列清单。
#
# 两种模式（都要看，它们抓的是不同的东西）：
#   ① 整方法克隆：行集合 Jaccard ≥ 阈值（两个方法几乎逐行相同 → 直接合并）
#   ② 共享长块：Jaccard 不到阈值，但两段里有 ≥ 块阈值 行的**连续相同块**（"共享骨架"就是这种：
#      前奏/尾巴逐字相同、中段各写各的 —— 刀21 的 创建实体框 正是这一类，模式①抓不到）
#
# 判据与边界（写在明处，别当精确结论）：
#   · 只比"方法体"，不比签名；方法体 = 签名行起、花括号配平；
#   · 归一化 = 去行注释 + 去空行 + 去只有花括号的行 + 压空白；
#   · 分桶启发式：按**第一条归一化行**分桶（同骨架常以同一行开头），桶内两两比较。
#     → 已知边界：首行不同、其余相同的整对克隆会被漏掉；模式②的"共享块"能捞回一部分。
#   · 行数差 > 40% 直接跳过；只有"共同行 ≥ 块阈值"的对才去算最长公共块（否则太慢）。
#   · 结果只是**候选清单**，不是结论：该不该合并要看两边语义是否真的一样。
#
# 用法（仓库根）：
#   & ".\Tools\重复代码扫描.ps1"
#   & ".\Tools\重复代码扫描.ps1" -根 "Assets\Scripts\UI" -最小行数 8 -相似度阈值 55 -块阈值 8
# 编码纪律：一律 [IO.File]::ReadAllText + 显式 UTF8
# ============================================================
param(
    [string]$根 = "Assets\Scripts",
    [int]$最小行数 = 12,
    [int]$相似度阈值 = 60,
    [int]$块阈值 = 10,
    [int]$最多输出 = 25
)
$ErrorActionPreference = "Stop"
$仓库根 = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$扫 = if ([System.IO.Path]::IsPathRooted($根)) { $根 } else { Join-Path $仓库根 $根 }
if (-not (Test-Path $扫)) { Write-Output ("✗ 路径不存在：" + $扫); exit 1 }

function 归一化行([string]$行) {
    $t = ($行 -replace '//.*$', '').Trim()
    if ($t -eq '' -or $t -eq '{' -or $t -eq '}' -or $t -eq '};' -or $t -eq '{ }') { return $null }
    return ($t -replace '\s+', ' ')
}

# 最长公共**连续**块（动态规划，O(n×m)；调用前先用"共同行数"把明显不相干的对滤掉）
function 最长公共块([string[]]$甲, [string[]]$乙) {
    $n = $甲.Count; $m = $乙.Count
    $上一行 = New-Object 'int[]' ($m + 1)
    $当前行 = New-Object 'int[]' ($m + 1)
    $最长 = 0; $甲末 = -1
    for ($i = 1; $i -le $n; $i++) {
        for ($j = 1; $j -le $m; $j++) {
            if ($甲[$i - 1] -ceq $乙[$j - 1]) {
                $当前行[$j] = $上一行[$j - 1] + 1
                if ($当前行[$j] -gt $最长) { $最长 = $当前行[$j]; $甲末 = $i - 1 }
            } else { $当前行[$j] = 0 }
        }
        $t = $上一行; $上一行 = $当前行; $当前行 = $t
        [Array]::Clear($当前行, 0, $当前行.Length)
    }
    $首 = if ($甲末 -ge 0) { $甲[$甲末 - $最长 + 1] } else { "" }
    return @{ 长 = $最长; 首行 = $首 }
}

# 提取方法体（签名行起、花括号配平）
function 取方法([string]$全路径) {
    $行 = [System.IO.File]::ReadAllText($全路径, [System.Text.Encoding]::UTF8) -split "`r?`n"
    $出 = @()
    for ($i = 0; $i -lt $行.Count; $i++) {
        $l = $行[$i] -replace '//.*$', ''
        # 候选签名：带访问修饰符、行尾就是 ')'，且不是表达式体（=>）也不是调用语句（;）
        if ($l -notmatch '^\s*(public|private|protected|internal)\b') { continue }
        if ($l -notmatch '\)\s*$') { continue }
        if ($l -match '=>') { continue }
        if ($l -match ';\s*$') { continue }
        $深 = 0; $过 = $false; $起 = -1; $止 = -1
        for ($j = $i; $j -lt $行.Count; $j++) {
            $t = $行[$j] -replace '//.*$', ''
            $串 = $false
            foreach ($c in $t.ToCharArray()) {
                if ($c -eq '"') { $串 = -not $串; continue }
                if ($串) { continue }
                if ($c -eq '{') { $深++; $过 = $true; if ($起 -lt 0) { $起 = $j } }
                elseif ($c -eq '}') { $深--; if ($过 -and $深 -le 0) { $止 = $j; break } }
            }
            if ($止 -ge 0) { break }
        }
        if ($止 -lt 0 -or $起 -lt 0) { continue }
        $体 = @()
        for ($j = $起; $j -le $止; $j++) { $n = 归一化行 $行[$j]; if ($n) { $体 += $n } }
        if ($体.Count -ge $最小行数) {
            $出 += [pscustomobject]@{
                文件 = $全路径.Substring($仓库根.Length + 1)
                行   = $i + 1
                签名 = ($行[$i].Trim() -replace '\s+', ' ')
                首行 = $体[0]
                体   = $体
                集   = [System.Collections.Generic.HashSet[string]]::new([string[]]$体)
            }
        }
        $i = $止   # 跳过已消费的方法体
    }
    return $出
}

Write-Output "=== 重复代码扫描 ==="
Write-Output ("范围 {0} · 最小 {1} 行 · 克隆阈值 {2}% · 共享块阈值 {3} 行 · 分桶 = 第一条归一化行" -f $根, $最小行数, $相似度阈值, $块阈值)
$全部 = @()
foreach ($f in (Get-ChildItem $扫 -Recurse -Filter *.cs)) { $全部 += 取方法 $f.FullName }
Write-Output ("扫到方法体 {0} 个（≥{1} 行）" -f $全部.Count, $最小行数)

$桶 = @{}
foreach ($m in $全部) { $k = $m.首行; if (-not $桶.ContainsKey($k)) { $桶[$k] = @() }; $桶[$k] += $m }

$克隆 = New-Object System.Collections.Generic.List[object]
$骨架 = New-Object System.Collections.Generic.List[object]
foreach ($k in $桶.Keys) {
    $组 = @($桶[$k])   # ⚠ 必须包一层 @()：单元素桶会被 PowerShell 解包成标量，$组.Count 就不是数组长度
    if ($组.Count -lt 2) { continue }
    # ⚠ 循环变量必须避开 $A/$B —— PowerShell 变量名大小写不敏感，$b 与 $B 是同一个变量（这里踩过一次）
    for ($i = 0; $i -lt $组.Count; $i++) {
        for ($j = $i + 1; $j -lt $组.Count; $j++) {
            $A = $组[$i]; $B = $组[$j]
            $大 = [Math]::Max($A.体.Count, $B.体.Count)
            if ([Math]::Abs($A.体.Count - $B.体.Count) / $大 -gt 0.4) { continue }
            $交 = 0; foreach ($l in $A.集) { if ($B.集.Contains($l)) { $交++ } }
            if ($交 -lt $块阈值) { continue }
            $并 = $A.集.Count + $B.集.Count - $交
            if ($并 -le 0) { continue }
            $相似 = [int](100.0 * $交 / $并)
            if ($相似 -ge $相似度阈值) {
                $克隆.Add([pscustomobject]@{
                    相似 = $相似; 行A = $A.体.Count; 行B = $B.体.Count
                    A = ($A.文件 + ":" + $A.行); 签A = $A.签名
                    B = ($B.文件 + ":" + $B.行); 签B = $B.签名
                })
                continue
            }
            # 整方法克隆没到阈值 → 再看有没有"一段共享的长块"（共享骨架：前奏/尾巴相同、中段各写各的）
            $块 = 最长公共块 $A.体 $B.体
            if ($块.长 -lt $块阈值) { continue }
            $骨架.Add([pscustomobject]@{
                相似 = $相似; 行A = $A.体.Count; 行B = $B.体.Count
                A = ($A.文件 + ":" + $A.行); 签A = $A.签名
                B = ($B.文件 + ":" + $B.行); 签B = $B.签名
                共享块 = $块.长; 块首行 = $块.首行
            })
        }
    }
}

Write-Output ""
Write-Output ("① 整方法克隆：{0} 组（Jaccard ≥ {1}%）" -f $克隆.Count, $相似度阈值)
$排1 = $克隆 | Sort-Object -Property @{Expression={ $_.相似 * [Math]::Min($_.行A, $_.行B) }} -Descending | Select-Object -First $最多输出
foreach ($r in $排1) {
    Write-Output ("   [{0,3}%] {1,3}行 / {2,3}行  A {3}  {4}" -f $r.相似, $r.行A, $r.行B, $r.A, $r.签A)
    Write-Output ("                            B {0}  {1}" -f $r.B, $r.签B)
}
if ($克隆.Count -eq 0) { Write-Output "   （无）" }

Write-Output ""
Write-Output ("② 共享长块：{0} 组（整方法不像、但有 ≥ {1} 行连续相同 —— 「共享骨架」型）" -f $骨架.Count, $块阈值)
$排2 = $骨架 | Sort-Object -Property 共享块 -Descending | Select-Object -First $最多输出
foreach ($r in $排2) {
    Write-Output ("   [共享 {0,2} 行 / 整方法相似 {1,3}%] A {2}  {3}" -f $r.共享块, $r.相似, $r.A, $r.签A)
    Write-Output ("        B {0}  {1}" -f $r.B, $r.签B)
    Write-Output ("        块首行：{0}" -f $r.块首行)
}
if ($骨架.Count -eq 0) { Write-Output "   （无）" }

Write-Output ""
Write-Output "提醒：这只是一份**候选清单**。该不该合并要看两边语义是否真的一样；"
Write-Output "      另外「首行不同、其余相同」的整对克隆会被漏掉（分桶启发式的已知边界）。"

# ============================================================
# ③ 已收敛的不变量：**只许有一份实现**（防漂移断言，不是候选清单 —— 这里会判失败）
# 为什么加：刀32 把「门口四邻」（原来 4 处生成期 + 2 处验证期各写一遍、作者栽过两次）收敛成
#   `Domain/网格/门口保护.cs` 一份。但"收敛"只在这一刻成立 —— 下次谁再手写一遍 Math.Abs ≤1，
#   就又回到多份实现。所以这里钉住：这个形状的判据只许出现在 门口保护.cs 里。
# 判据：`Math.Abs(…) <= 1 && Math.Abs(…)`（切比雪夫 ≤1 的手写写法），先去掉注释再数。
# ============================================================
Write-Output ""
Write-Output "③ 已收敛不变量：手写「切比雪夫 ≤1」只许出现在 门口保护.cs"
$允许 = @('Assets\Scripts\Domain\网格\门口保护.cs')
$越界 = New-Object System.Collections.Generic.List[string]
foreach ($f in (Get-ChildItem (Join-Path $仓库根 'Assets\Scripts') -Recurse -Filter *.cs)) {
    $相对 = $f.FullName.Substring($仓库根.Length + 1)
    $净 = [regex]::Replace([System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8), '(?m)//.*$', '')
    $命中 = [regex]::Matches($净, 'Math\.Abs\([^)]*\)\s*<=\s*1\s*&&').Count
    if ($命中 -gt 0 -and ($允许 -notcontains $相对)) { $越界.Add(("{0}（{1} 处）" -f $相对, $命中)) }
}
if ($越界.Count -eq 0) { Write-Output ("  ✓ 0 处越界（都走 门口保护；该文件自己 2 处：相邻 / 算间距）") }
else {
    foreach ($x in $越界) { Write-Output ("  ✗ {0} —— 手写了门口四邻/间距，应改调 门口保护.相邻 / 碰门口 / 足迹碰门口 / 算间距" -f $x) }
    Write-Output ("❌ 重复代码扫描失败：{0} 处越界" -f $越界.Count)
    exit 1
}
