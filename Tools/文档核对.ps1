# ============================================================
# 文档核对：活文档里写的仓库路径，是否还对得上现状（不打开 Unity 就能跑）
# 为什么需要：刀23 重建了 Assets/Scripts 目录、刀30 又搬了 建筑外形.cs —— 每次都靠"人肉记得改文档"。
#   实测：21 份活文档里有 77 处路径对不上，其中 13 处是**真漂移**（文件还在、只是换了目录，读者按路径去找会扑空）。
#   这条脚本把它变成红绿：漂移必须修（-修 可自动改写），"找不到同名文件"只提示（可能是打算新增/已删）。
#
# 判据（写在明处，避免误报）：
#   · 只扫 `Assets/…、Tools/…、docs/…` 且带扩展名（cs/csproj/json/ps1/unity/asset/meta/md）的反引号路径；
#   · 路径**存在** → 绿；
#   · 路径不存在，但**同名文件**在仓库里唯一存在 → ✗ 漂移（报出建议路径；`-修` 就地改写）；
#   · 路径不存在且全仓库无同名文件 → ⚠ 提示（判不了是"打算新增"还是"已删"，不失败）；
#   · 历史快照文档（当刀的实现说明 / 旧→新对照表 / docs\评估 六份审计）整份豁免 —— 它们记的就是当时的文件名；
#   · 行内豁免：某行写了 `[路径豁免]` 就跳过该行 —— 文档里**故意**引用错路径时（举例 / 负向测试 / 历史记录）用它。
# 用法（仓库根）：& ".\Tools\文档核对.ps1"        # 只查
#                 & ".\Tools\文档核对.ps1" -修     # 查 + 就地改写可修的那些（改完再跑一次应当全绿）
# 负向测试（已验证会响，2026-09-13）：把 工作移交报告.md 里的一处 `Data/建筑外形.cs` 改成
#   `Assets/Scripts/Domain/建筑外形.cs`（同名文件其实在 Data）→ 立刻报
#   「✗ docs/工作移交报告.md:311 写的是 Assets/Scripts/Domain/建筑外形.cs，但同名文件现在在 Assets/Scripts/Data/建筑外形.cs」；
#   手工还原后重新绿，`git diff` 确认无注入残留。
#   ⚠ 自己踩过的坑：第一次注入写的是**裸路径** `Domain/建筑外形.cs`（不带 Assets/Tools/docs 前缀）→ 压根不在扫描范围，假绿。
#     本脚本只扫「带前缀的仓库路径」，负向测试必须带前缀，否则测的是"没扫到"而不是"扫到没报"。
# 编码纪律：一律 [IO.File]::ReadAllText + 显式 UTF8，改写时保留原 BOM 状态
# ============================================================
param([switch]$修)

$ErrorActionPreference = "Stop"
$根 = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$文档根 = Join-Path $根 "docs"
$script:失败 = 0
$script:行内豁免 = 0
function 报错([string]$t) { Write-Output ("  ✗ " + $t); $script:失败++ }

# 历史快照文档：整份豁免（记的是"当时"的文件名，改了反而抹掉历史）
# ★ 文档精简后（5.4 MB → 359 KB）这些快照连同 `docs/归档/`、`docs/评估/` 一起删了 —— 豁免表清空。
#   清空而不是留着：**空豁免 = 门禁对全部现存文档生效**；留着陈旧条目只会让下一次漂移悄悄溜过。
$历史快照 = @{}
# 目录级豁免（同上：`docs/评估/` 六份审计已删，不再需要目录豁免）
$目录豁免 = @{}
# 单路径豁免：写清理由，输出里会打印出来（可见，不静默）
$路径豁免 = @{
    'Assets/Scripts/Services/地图服务.cs' = '刀17 已整体删除的服务；它出现在"**删**"那一行的表格里，是记录而不是现状'
}

$模式 = '(?:Assets|Tools|docs)[/\\][^\s`"''）)】,，。；;：:]*?\.(?:cs|csproj|json|ps1|unity|asset|meta|md)'

function 相对([string]$全路径) { $全路径.Substring($根.Length + 1).Replace('\', '/') }

# 同名文件索引（只建一次：basename → 现存相对路径列表）
Write-Output "=== 文档核对（活文档路径 vs 现状）==="
$索引 = @{}
foreach ($d in @((Join-Path $根 'Assets'), (Join-Path $根 'Tools'), $文档根)) {
    if (-not (Test-Path $d)) { continue }
    foreach ($f in (Get-ChildItem $d -Recurse -File)) {
        if (-not $索引.ContainsKey($f.Name)) { $索引[$f.Name] = New-Object System.Collections.Generic.List[string] }
        $索引[$f.Name].Add((相对 $f.FullName))
    }
}
Write-Output ("同名文件索引 {0} 个 basename（Assets + Tools + docs）" -f $索引.Count)

$全部文档 = Get-ChildItem $文档根 -Recurse -Filter *.md
$跳过 = @(); $查 = @()
foreach ($f in $全部文档) {
    $短 = $f.Name
    if ($历史快照.ContainsKey($短)) { $跳过 += [pscustomobject]@{ 文档 = (相对 $f.FullName); 理由 = $历史快照[$短] }; continue }
    $顶层 = $f.Directory.Name
    if ($f.DirectoryName -ne $文档根 -and $目录豁免.ContainsKey($顶层)) { continue }   # 目录级豁免：直接不扫
    $查 += $f
}
$已豁免目录 = ($目录豁免.Keys | ForEach-Object { "docs\$_\*" }) -join '、'
Write-Output ("扫 {0} 份；整份豁免 {1} 份；目录豁免 {2}" -f $查.Count, $跳过.Count, $已豁免目录)
foreach ($s in $跳过) { Write-Output ("  · [豁免] {0} —— {1}" -f $s.文档, $s.理由) }

$漂移 = New-Object System.Collections.Generic.List[object]
$找不到 = New-Object System.Collections.Generic.List[object]
$命中豁免 = New-Object System.Collections.Generic.List[object]

foreach ($f in $查) {
    $文档 = 相对 $f.FullName
    $字节 = [System.IO.File]::ReadAllBytes($f.FullName)
    $有BOM = ($字节.Length -ge 3 -and $字节[0] -eq 239 -and $字节[1] -eq 187 -and $字节[2] -eq 191)
    $行们 = [System.IO.File]::ReadAllLines($f.FullName, [System.Text.Encoding]::UTF8)
    $改过 = $false
    for ($i = 0; $i -lt $行们.Count; $i++) {
        # 行内豁免：文档里**故意引用**一个错路径时（举例 / 负向测试 / 历史记录），在该行写 `[路径豁免]` 即可跳过。
        # 为什么要这个开关：否则只能去 $路径豁免 里屏蔽**整条路径** —— 那样这条路径以后真漂移了就再也拦不住。
        if ($行们[$i] -match '\[路径豁免\]') { $script:行内豁免++; continue }
        foreach ($m in [regex]::Matches($行们[$i], $模式)) {
            $原 = $m.Value
            $规范化 = $原.Replace('\', '/')
            if (Test-Path (Join-Path $根 ($规范化 -replace '/', '\'))) { continue }
            if ($路径豁免.ContainsKey($规范化)) { $命中豁免.Add([pscustomobject]@{ 文档 = $文档; 行 = ($i + 1); 路径 = $规范化; 理由 = $路径豁免[$规范化] }); continue }
            $名 = Split-Path $规范化 -Leaf
            # 纪律：不能写成 `$同 = if(...){@(...)}else{@()}` —— PowerShell 会把单元素数组**摊平**成标量，
            # 于是 $同[0] 取到首字符（第一次跑就打印出 "A"）。必须用显式 if 语句给数组赋值。
            $同 = @()
            if ($索引.ContainsKey($名)) { $同 = @($索引[$名]) }
            if ($同.Count -eq 1) {
                $漂移.Add([pscustomobject]@{ 文档 = $文档; 行 = ($i + 1); 旧 = $规范化; 新 = $同[0] })
                if ($修) {
                    $行们[$i] = $行们[$i].Replace($原, $同[0])
                    $改过 = $true
                }
            } else {
                $找不到.Add([pscustomobject]@{ 文档 = $文档; 行 = ($i + 1); 路径 = $规范化; 同名 = $同.Count })
            }
        }
    }
    if ($修 -and $改过) { [System.IO.File]::WriteAllLines($f.FullName, $行们, (New-Object System.Text.UTF8Encoding($有BOM))) }
}

Write-Output ""
Write-Output "[1] 路径漂移（文件还在、只是换了目录 —— 读者按文档去找会扑空）"
if ($漂移.Count -eq 0) { Write-Output "  0 处 ✅" }
foreach ($d in $漂移) {
    if ($修) { Write-Output ("  · [已改写] {0}:{1} {2} → {3}" -f $d.文档, $d.行, $d.旧, $d.新) }
    else { 报错 ("{0}:{1} 写的是 {2}，但同名文件现在在 {3}（加 -修 可自动改）" -f $d.文档, $d.行, $d.旧, $d.新) }
}

Write-Output ""
Write-Output "[2] 全仓库找不到同名文件（⚠ 只提示：可能是「打算新增」的设计稿，也可能是「已删」的记录）"
if ($找不到.Count -eq 0) { Write-Output "  0 处" }
foreach ($x in ($找不到 | Select-Object -First 12)) {
    Write-Output ("  · {0}:{1} {2}（同名文件 {3} 个）" -f $x.文档, $x.行, $x.路径, $x.同名)
}
if ($找不到.Count -gt 12) { Write-Output ("  …… 还有 {0} 处（只列前 12）" -f ($找不到.Count - 12)) }

Write-Output ""
Write-Output "[3] 单路径豁免（写清理由、打印出来，不静默放过）"
if ($命中豁免.Count -eq 0) { Write-Output "  0 处" }
foreach ($y in $命中豁免) { Write-Output ("  · {0}:{1} {2} —— {3}" -f $y.文档, $y.行, $y.路径, $y.理由) }
Write-Output ("  行内豁免（那行写了 [路径豁免]）{0} 行" -f $script:行内豁免)

Write-Output ""
if ($script:失败 -eq 0) {
    Write-Output ("✅ 文档核对通过（漂移 {0} 处 · 同名找不到 {1} 处已列在 [2]）" -f $漂移.Count, $找不到.Count)
    Write-Output "   ⚠ 只查「路径对不对」；文档说的**内容**是否仍然正确，仍要人读"
    exit 0
}
Write-Output ("❌ 文档核对失败：{0} 处漂移（跑 & "".\Tools\文档核对.ps1"" -修 可自动改）" -f $script:失败)
exit 1
