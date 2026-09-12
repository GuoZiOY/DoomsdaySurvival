# ============================================================
# 数据引用核对：把 DataService 的"交叉校验"里**能离线跑的那部分**拎出来（不打开 Unity 就能查）
# 为什么需要：Tools/房间验证 / 建筑验证 / 区域验证 覆盖的是**生成**不变量；
#             "搜索表里写的物品标识到底存不存在""容器池指的 地图类型/房间 有没有"这类
#             **数据引用**只在 DataService 里查（要进 Unity 才知道）。改数据 JSON 时最容易错的就是这个。
# 用法（仓库根）：& ".\Tools\数据引用核对.ps1"
# 编码纪律：一律 [IO.File]::ReadAllText/WriteAllText + 显式 UTF8 ——
#           PS 5.1 的 Get-Content 会把 UTF-8 中文按 GBK 读（本项目踩过一次，见 docs 大世界网格与副本设计.md §15.3.1）
# ============================================================
$ErrorActionPreference = "Stop"
$根 = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$数据 = Join-Path $根 "Assets\Resources\Data"
$script:失败 = 0

function 读JSON([string]$相对路径) {
    $p = Join-Path $数据 $相对路径
    if (-not (Test-Path $p)) { Write-Output ("  [缺文件] {0}" -f $相对路径); return $null }
    try { return ([System.IO.File]::ReadAllText((Resolve-Path $p), [System.Text.Encoding]::UTF8) | ConvertFrom-Json) }
    catch { Write-Output ("  [JSON 解析失败] {0}：{1}" -f $相对路径, $_.Exception.Message); $script:失败++; return $null }
}

function 报错([string]$文本) { Write-Output ("  ✗ " + $文本); $script:失败++ }

Write-Output "=== 数据引用核对 ==="

# ---------- 1. 收集全部物品标识（物品目录下所有 json，含子目录） ----------
$物品标识 = New-Object System.Collections.Generic.HashSet[string]
foreach ($f in (Get-ChildItem (Join-Path $数据 "物品") -Recurse -Filter *.json)) {
    try { $j = ([System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8) | ConvertFrom-Json) }
    catch { 报错 ("物品文件 JSON 解析失败：" + $f.Name + "：" + $_.Exception.Message); continue }
    foreach ($it in $j.物品) { if ($it.标识) { [void]$物品标识.Add([string]$it.标识) } }
}
Write-Output ("物品标识 {0} 个" -f $物品标识.Count)

# ---------- 2. 敌人组（encounters）----------
$敌人 = New-Object System.Collections.Generic.HashSet[string]
$ej = 读JSON "enemies.json"
if ($ej) { foreach ($e in $ej.敌人) { if ($e.标识) { [void]$敌人.Add([string]$e.标识) } } }
$敌人组 = New-Object System.Collections.Generic.HashSet[string]
$ec = 读JSON "encounters.json"
if ($ec -and $ec.敌人组) { foreach ($g in $ec.敌人组) { if ($g.标识) { [void]$敌人组.Add([string]$g.标识) } } }
Write-Output ("敌人 {0} 个 / 敌人组 {1} 个" -f $敌人.Count, $敌人组.Count)

# ---------- 3. 搜索_地图类型：容器池引用 + 搜索表物品存在 ----------
$地图类型 = 读JSON "搜索_地图类型.json"
$类型房 = New-Object System.Collections.Generic.HashSet[string]   # "地图类型/房间"
if ($地图类型) {
    foreach ($t in $地图类型.地图类型) {
        if (-not $t.标识) { 报错 "地图类型 缺少 标识"; continue }
        if (-not $t.房间) { 报错 ("地图类型[{0}] 没有 房间" -f $t.标识); continue }
        foreach ($r in $t.房间) {
            if (-not $r.标识) { 报错 ("地图类型[{0}] 有房间缺 标识" -f $t.标识); continue }
            [void]$类型房.Add(($t.标识 + "/" + $r.标识))
            if (-not $r.容器 -or $r.容器.Count -eq 0) { 报错 ("{0}/{1} 没有 容器" -f $t.标识, $r.标识); continue }
            foreach ($c in $r.容器) {
                if (-not $c.标识) { 报错 ("{0}/{1} 有容器缺 标识" -f $t.标识, $r.标识); continue }
                if ($c.容器列 -lt 1 -or $c.容器行 -lt 1) { 报错 ("容器[{0}] 尺寸非法 {1}×{2}" -f $c.标识, $c.容器列, $c.容器行) }
                if (-not $c.搜索表 -or $c.搜索表.Count -eq 0) { 报错 ("容器[{0}] 搜索表为空" -f $c.标识); continue }
                foreach ($e in $c.搜索表) {
                    if (-not $e.物品标识) { 报错 ("容器[{0}] 搜索表有条目缺 物品标识" -f $c.标识); continue }
                    if (-not $物品标识.Contains([string]$e.物品标识)) { 报错 ("容器[{0}] 搜索表指向不存在的物品：{1}" -f $c.标识, $e.物品标识) }
                    if ($e.数量最小 -lt 1 -or $e.数量最大 -lt $e.数量最小) { 报错 ("容器[{0}] 的 {1} 数量区间非法 {2}~{3}" -f $c.标识, $e.物品标识, $e.数量最小, $e.数量最大) }
                }
            }
        }
    }
}
Write-Output ("地图类型 {0} 个 / 可引用的 类型/房间 {1} 组" -f $(if ($地图类型) { $地图类型.地图类型.Count } else { 0 }), $类型房.Count)

# ---------- 4. 房间模板：容器池引用 + 敌人组 + 棋盘 + 门锁钥匙 + 门内互指 ----------
$房间模板 = 读JSON "房间模板.json"
$房间标识 = New-Object System.Collections.Generic.HashSet[string]
if ($房间模板) { foreach ($r in $房间模板.房间) { if ($r.标识) { [void]$房间标识.Add([string]$r.标识) } } }
if ($房间模板) {
    foreach ($r in $房间模板.房间) {
        if (-not $r.标识) { 报错 "房间模板 缺少 标识"; continue }
        foreach ($p in $r.容器池) {
            $键 = ([string]$p.地图类型 + "/" + [string]$p.房间)
            if (-not $类型房.Contains($键)) { 报错 ("房间[{0}] 容器池指向不存在的 地图类型/房间：{1}" -f $r.标识, $键) }
            if ($p.数量最小 -lt 0 -or $p.数量最大 -lt $p.数量最小) { 报错 ("房间[{0}] 容器池 {1} 数量区间非法 {2}~{3}" -f $r.标识, $键, $p.数量最小, $p.数量最大) }
        }
        if ($r.敌人组 -and -not $敌人组.Contains([string]$r.敌人组)) { 报错 ("房间[{0}] 敌人组[{1}] 不存在" -f $r.标识, $r.敌人组) }
        if ($r.敌人数量最小 -gt $r.敌人数量最大) { 报错 ("房间[{0}] 敌人数量区间反了" -f $r.标识) }
        if (-not $r.门) { continue }
        foreach ($d in $r.门) {
            if ($d.锁) {
                if (-not $物品标识.Contains([string]$d.锁)) { 报错 ("房间[{0}] 门锁指向不存在的物品：{1}" -f $r.标识, $d.锁) }
                else {
                    # 钥匙必须是"任务品/钥匙"，否则玩家拿到也不认识（提示用；不算错）
                    $是钥匙 = $false
                    foreach ($f in (Get-ChildItem (Join-Path $数据 "物品") -Recurse -Filter *.json)) {
                        if ($f.Name -ne "items_任务品.json") { continue }
                        $j = ([System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8) | ConvertFrom-Json)
                        foreach ($it in $j.物品) { if ($it.标识 -eq $d.锁 -and $it.种类 -eq "钥匙") { $是钥匙 = $true } }
                    }
                    if (-not $是钥匙) { Write-Output ("  ⚠ 房间[{0}] 门锁[{1}] 不是 任务品/钥匙 类（玩家可能不认识它）" -f $r.标识, $d.锁) }
                }
            }
        }
    }
}
Write-Output ("房间模板 {0} 个" -f $房间标识.Count)

# ---------- 5. 建筑模板：房间存在 + 楼梯间尺寸 + 门只在本层 + 大门层数 ----------
$建筑模板 = 读JSON "建筑模板.json"
$建筑标识 = New-Object System.Collections.Generic.HashSet[string]
if ($建筑模板) { foreach ($b in $建筑模板.建筑) { if ($b.标识) { [void]$建筑标识.Add([string]$b.标识) } } }
if ($建筑模板) {
    foreach ($b in $建筑模板.建筑) {
        if (-not $b.标识) { 报错 "建筑模板 缺少 标识"; continue }
        if (-not $b.楼层) { 报错 ("建筑[{0}] 没有楼层" -f $b.标识); continue }
        $大门层 = 0
        for ($i = 0; $i -lt $b.楼层.Count; $i++) {
            $fl = $b.楼层[$i]
            if (-not $fl.房间 -or $fl.房间.Count -eq 0) { 报错 ("建筑[{0}]·{1}F 没有房间" -f $b.标识, ($i + 1)); continue }
            if ($fl.大门) { $大门层++ }
            foreach ($rid in $fl.房间) {
                if (-not $房间标识.Contains([string]$rid)) { 报错 ("建筑[{0}]·{1}F 房间[{2}] 不存在" -f $b.标识, ($i + 1), $rid); continue }
                $rt = $房间模板.房间 | Where-Object { $_.标识 -eq $rid } | Select-Object -First 1
                if ($fl.房间[0] -eq $rid -and ($rt.列 -ne $b.列 -or $rt.行 -ne $b.行)) {
                    报错 ("建筑[{0}]·{1}F 首间房[{2}] 尺寸 {3}×{4} ≠ 建筑 {5}×{6}（楼梯会上下错位）" -f $b.标识, ($i + 1), $rid, $rt.列, $rt.行, $b.列, $b.行)
                }
                if ($rt.门) {
                    foreach ($d in $rt.门) {
                        if (-not $d.通向) { continue }
                        if ($fl.房间 -notcontains $d.通向) { 报错 ("建筑[{0}]·{1}F 房间[{2}] 的门通向[{3}]，但它不在本层（跨层只能走楼梯）" -f $b.标识, ($i + 1), $rid, $d.通向) }
                    }
                }
            }
        }
        if ($大门层 -eq 0) { Write-Output ("  ⚠ 建筑[{0}] 没有任何一层写 大门 = true（进了楼出不去）" -f $b.标识) }
        if ($大门层 -gt 1) { Write-Output ("  ⚠ 建筑[{0}] 有 {1} 层写了 大门 = true（一栋楼只该有一扇正门）" -f $b.标识, $大门层) }
    }
}
Write-Output ("建筑模板 {0} 个" -f $建筑标识.Count)

# ---------- 6. 区域模板：建筑存在 + 棋盘 + 尺寸 ----------
$区域模板 = 读JSON "区域模板.json"
$棋盘 = New-Object System.Collections.Generic.HashSet[string]
$qj = 读JSON "战斗棋盘.json"
if ($qj) { foreach ($q in $qj.棋盘) { if ($q.标识) { [void]$棋盘.Add([string]$q.标识) } } }
if ($区域模板) {
    foreach ($a in $区域模板.区域) {
        if (-not $a.标识) { 报错 "区域模板 缺少 标识"; continue }
        if (-not $a.建筑 -or $a.建筑.Count -eq 0) { 报错 ("区域[{0}] 没有建筑" -f $a.标识); continue }
        if ($a.建筑.Count -gt 4) { Write-Output ("  ⚠ 区域[{0}] 写了 {1} 栋建筑，但一屏只有 4 个地块（多出来的不摆）" -f $a.标识, $a.建筑.Count) }
        foreach ($it in $a.建筑) {
            $定 = if ($it.建筑模板) { $it.建筑模板 } else { $it.房间模板 }
            if (-not $定) { 报错 ("区域[{0}] 建筑项 既没写 建筑模板 也没写 房间模板" -f $a.标识); continue }
            if ($it.建筑模板 -and -not $建筑标识.Contains([string]$it.建筑模板)) { 报错 ("区域[{0}] 建筑模板[{1}] 不存在" -f $a.标识, $it.建筑模板) }
            if ($it.房间模板 -and -not $房间标识.Contains([string]$it.房间模板)) { 报错 ("区域[{0}] 房间模板[{1}] 不存在" -f $a.标识, $it.房间模板) }
        }
        if ($a.战斗棋盘 -and -not $棋盘.Contains([string]$a.战斗棋盘)) { 报错 ("区域[{0}] 战斗棋盘[{1}] 不存在" -f $a.标识, $a.战斗棋盘) }
    }
}
Write-Output ("区域模板 {0} 个 / 棋盘 {1} 张" -f $(if ($区域模板) { $区域模板.区域.Count } else { 0 }), $棋盘.Count)

# ---------- 7. 大世界：区域模板存在 + 占地形状认得出 + 敌人定义存在 ----------
$世界 = 读JSON "世界.json"
if ($世界) {
    foreach ($w in $世界.世界) {
        if (-not $w.标识) { 报错 "世界 缺少 标识"; continue }
        if ($w.区域) {
            foreach ($a in $w.区域) {
                if (-not $a.区域模板) { 报错 ("世界[{0}] 有区域项缺 区域模板" -f $w.标识); continue }
                if (-not ($区域模板.区域 | Where-Object { $_.标识 -eq $a.区域模板 })) { 报错 ("世界[{0}] 区域模板[{1}] 不存在" -f $w.标识, $a.区域模板) }
                if (-not $a.占地) { 报错 ("世界[{0}] 区域[{1}] 缺 占地" -f $w.标识, $a.区域模板); continue }
                $形 = [string]$a.占地.形状
                $合法 = @(([string][char]0x77E9 + [string][char]0x5F62), "L", ([string][char]0x51F8), ([string][char]0x51F9))   # 矩形 / L / 凸 / 凹
                if ($合法 -notcontains $形) { 报错 ("世界[{0}] 区域[{1}] 占地形状[{2}] 认不出（只能 矩形/L/凸/凹）" -f $w.标识, $a.区域模板, $形) }
                if ($a.解锁) {
                    foreach ($u in $a.解锁) {
                        if ($u.条件 -ne "天数" -and $u.条件 -ne "物品") { 报错 ("世界[{0}] 区域[{1}] 解锁条件[{2}] 非法（只能是 天数/物品）" -f $w.标识, $a.区域模板, $u.条件) }
                        if ($u.条件 -eq "物品" -and -not $物品标识.Contains([string]$u.标识)) { 报错 ("世界[{0}] 区域[{1}] 解锁物品[{2}] 不存在" -f $w.标识, $a.区域模板, $u.标识) }
                        if ($u.条件 -eq "天数" -and [int]$u.数值 -le 0) { 报错 ("世界[{0}] 区域[{1}] 解锁天数 没写正的 数值" -f $w.标识, $a.区域模板) }
                    }
                }
            }
        }
        if ($w.敌人) {
            foreach ($e in $w.敌人) {
                if (-not $e.定义标识) { 报错 ("世界[{0}] 有敌人项缺 定义标识" -f $w.标识); continue }
                if (-not $敌人.Contains([string]$e.定义标识)) { 报错 ("世界[{0}] 敌人[{1}] 不存在（enemies.json 里没有）" -f $w.标识, $e.定义标识) }
            }
        }
        if ($w.战斗棋盘 -and -not $棋盘.Contains([string]$w.战斗棋盘)) { 报错 ("世界[{0}] 战斗棋盘[{1}] 不存在" -f $w.标识, $w.战斗棋盘) }
    }
}
Write-Output ("世界 {0} 个" -f $(if ($世界) { $世界.世界.Count } else { 0 }))

Write-Output ""
if ($script:失败 -eq 0) { Write-Output "✅ 数据引用核对通过（搜索表物品 / 容器池 / 敌人组 / 门锁钥匙 / 楼层房间 / 区域建筑 / 世界区域与敌人 全部存在）"; exit 0 }
Write-Output ("❌ 数据引用核对失败：{0} 处" -f $script:失败); exit 1
