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
        # 街上敌人（v51 刀12）：定义标识必须在 enemies.json 里，数量必须为正。
        # 注：这里只查"名字对不对"；"撒不撒得下、会不会把路堵死"由 Tools/区域验证 逐种子断言。
        foreach ($e in $a.敌人) {
            if (-not $e.定义标识) { 报错 ("区域[{0}] 有敌人项缺 定义标识" -f $a.标识); continue }
            if (-not $敌人.Contains([string]$e.定义标识)) { 报错 ("区域[{0}] 街上敌人[{1}] 不存在（enemies.json 里没有）" -f $a.标识, $e.定义标识) }
            if ([int]$e.数量 -le 0) { 报错 ("区域[{0}] 街上敌人[{1}] 数量不是正数" -f $a.标识, $e.定义标识) }
        }
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

        # ---------- 搜参的三条防刷约束（v52，设计见 docs/大世界搜索约束设计.md）----------
        # 为什么必须在这边也查一遍：这三条都是"结构合法但语义失效"那类错（名字写错、白名单漏了禁区），
        # 只有交叉核对才看得出来。DataService 里有一份同样的校验（运行时挡玩家）；这份是不开 Unity 也能看见。
        $s = $w.搜索
        if ($s) {
            if ($null -ne $s.枯竭半径 -and ([int]$s.枯竭半径 -lt 0 -or [int]$s.枯竭半径 -gt 30)) { 报错 ("世界[{0}] 搜索枯竭半径 {1} 不在 0~30" -f $w.标识, $s.枯竭半径) }
            if ($null -ne $s.枯竭概率 -and ([int]$s.枯竭概率 -lt 0 -or [int]$s.枯竭概率 -gt 100)) { 报错 ("世界[{0}] 搜索枯竭概率 {1} 不在 0~100" -f $w.标识, $s.枯竭概率) }
            if ($null -ne $s.枯竭半径 -and [int]$s.枯竭半径 -gt 0 -and $null -ne $s.出现概率 -and [int]$s.枯竭概率 -ge [int]$s.出现概率) {
                报错 ("世界[{0}] 搜索枯竭概率 {1} ≥ 出现概率 {2}：局部枯竭失效" -f $w.标识, $s.枯竭概率, $s.出现概率)
            }
            if ($null -ne $s.每多一次多花行动点 -and [int]$s.每多一次多花行动点 -lt 0) { 报错 ("世界[{0}] 搜索 每多一次多花行动点 不能是负数" -f $w.标识) }
            if (($null -eq $s.枯竭半径 -or [int]$s.枯竭半径 -le 0) -and ($null -eq $s.每多一次多花行动点 -or [int]$s.每多一次多花行动点 -le 0)) {
                Write-Output ("  ⚠ 世界[{0}] 搜索的两条防刷约束**都关着**：玩家可以站在安全屋门口一直搜" -f $w.标识)
            }

            # 禁区清单：楼型必须存在
            $禁 = New-Object System.Collections.Generic.HashSet[string]
            foreach ($z in $s.禁区楼型) { if ($z) { [void]$禁.Add([string]$z) } }
            foreach ($z in $禁) { if (-not $建筑标识.Contains($z)) { 报错 ("世界[{0}] 搜索禁区楼型[{1}] 不存在（建筑模板里没有）" -f $w.标识, $z) } }

            # 允许地图类型：类型必须存在
            $允许 = New-Object System.Collections.Generic.HashSet[string]
            foreach ($t in $s.允许地图类型) { if ($t) { [void]$允许.Add([string]$t) } }
            foreach ($t in $允许) {
                $在 = $false
                foreach ($k in $类型房) { if ($k.StartsWith($t + "/")) { $在 = $true; break } }
                if (-not $在) { 报错 ("世界[{0}] 搜索允许地图类型[{1}] 不存在（搜索_地图类型.json 里没有）" -f $w.标识, $t) }
            }

            # 白名单：楼型存在 / **不许在禁区里**（这条是白名单存在的全部理由）/ 权重为正
            $白 = New-Object System.Collections.Generic.HashSet[string]
            if ($s.临时建筑 -and $s.临时建筑.Count -gt 0) {
                foreach ($it in $s.临时建筑) {
                    if (-not $it.建筑模板) { 报错 ("世界[{0}] 搜索白名单有项缺 建筑模板" -f $w.标识); continue }
                    [void]$白.Add([string]$it.建筑模板)
                    if (-not $建筑标识.Contains([string]$it.建筑模板)) { 报错 ("世界[{0}] 搜索白名单楼型[{1}] 不存在" -f $w.标识, $it.建筑模板) }
                    if ($禁.Contains([string]$it.建筑模板)) {
                        报错 ("世界[{0}] 搜索白名单楼型[{1}] 在禁区清单里：临时建筑不许出区专属楼型（会把区域解锁门架空）" -f $w.标识, $it.建筑模板)
                    }
                    if ([int]$it.权重 -le 0) { 报错 ("世界[{0}] 搜索白名单楼型[{1}] 权重不是正数" -f $w.标识, $it.建筑模板) }
                }
                if ($允许.Count -eq 0) { 报错 ("世界[{0}] 搜索写了白名单却没写 允许地图类型（没法核对容器池）" -f $w.标识) }

                # 白名单楼型的房间容器池：只许引用 允许地图类型（挡住"把医院房间塞进便利店"这种偷偷拉专属战利品）
                foreach ($bid in $白) {
                    $b = $建筑模板.建筑 | Where-Object { $_.标识 -eq $bid } | Select-Object -First 1
                    if (-not $b -or -not $b.楼层) { continue }
                    foreach ($fl in $b.楼层) {
                        foreach ($rid in $fl.房间) {
                            $rt = $房间模板.房间 | Where-Object { $_.标识 -eq $rid } | Select-Object -First 1
                            if (-not $rt -or -not $rt.容器池) { continue }
                            foreach ($p in $rt.容器池) {
                                if (-not $p.地图类型) { continue }
                                if (-not $允许.Contains([string]$p.地图类型)) {
                                    报错 ("世界[{0}] 搜索白名单楼型[{1}] 的房间[{2}] 容器池引用了 地图类型[{3}]，但它不在 允许地图类型 里（临时建筑不许拉区专属战利品）" -f $w.标识, $bid, $rid, $p.地图类型)
                                }
                            }
                        }
                    }
                }
            } else {
                Write-Output ("  ⚠ 世界[{0}] 搜索没写 临时建筑 白名单：会走兜底（全部楼型里滤掉禁区楼型）" -f $w.标识)
            }
            Write-Output ("世界[{0}] 搜索：白名单 {1} 个 / 禁区 {2} 个 / 允许地图类型 {3} 个 / 枯竭半径 {4}" -f `
                $w.标识, $白.Count, $禁.Count, $允许.Count, $(if ($null -ne $s.枯竭半径) { $s.枯竭半径 } else { 0 }))
        }
    }
}
Write-Output ("世界 {0} 个" -f $(if ($世界) { $世界.世界.Count } else { 0 }))

# ---------- 8. 职业 / 天赋：悬空引用（**v51 刀52 起：判失败**） ----------
# 历史：这里原来是"只提示、不判失败"（用户当时拍板「职业/天赋暂时不补充，但保留」，
#   内容没补是已知状态，判失败会让工具永远红着）。**刀52 补齐后悬空引用 = 0 处**，
#   于是按项目纪律翻成硬失败：这三条在运行时**全是静默失效**，必须有人挡着不让它再回来。
#   （`DataService.重新校验` 末尾那份同样一份，仍保持"只警告不拦进游戏" —— 数据问题不该让玩家进不去。）
#   对应修复：`职业.初始技能` 4 处 + `职业.天赋` 6 处（`docs/数据缺口清单.md` §二 B）。
$技能集 = New-Object System.Collections.Generic.HashSet[string]
$sj = 读JSON "skills.json"
if ($sj) { foreach ($s in $sj.技能) { if ($s.标识) { [void]$技能集.Add([string]$s.标识) } } }
$天赋集 = New-Object System.Collections.Generic.HashSet[string]
$tj = 读JSON "天赋.json"
if ($tj) { foreach ($t in $tj.天赋) { if ($t.标识) { [void]$天赋集.Add([string]$t.标识) } } }
$职业 = 读JSON "职业.json"
$script:悬空 = 0
if ($职业) {
    foreach ($z in $职业.职业) {
        if (-not $z.标识) { 报错 "职业 缺少 标识"; continue }
        if ($z.初始技能 -and -not $技能集.Contains([string]$z.初始技能)) {
            报错 ("职业[{0}] 初始技能[{1}] 不在 skills.json（这个职业开局学不到它）" -f $z.标识, $z.初始技能); $script:悬空++
        }
        if ($z.天赋 -and -not $天赋集.Contains([string]$z.天赋)) {
            报错 ("职业[{0}] 天赋[{1}] 不在 天赋.json（会塞进 档案.天赋 但永不生效）" -f $z.标识, $z.天赋); $script:悬空++
        }
        if ($z.初始装备) {
            foreach ($e in $z.初始装备) {
                if ($e.标识 -and -not $物品标识.Contains([string]$e.标识)) {
                    报错 ("职业[{0}] 初始装备[{1}] 不在 items（会进背包，但是件没有定义的东西）" -f $z.标识, $e.标识); $script:悬空++
                }
            }
        }
    }
}
Write-Output ("职业 {0} 个 / 技能 {1} 个 / 天赋 {2} 个 / 悬空引用 {3} 处（刀52 起判失败）" -f `
    $(if ($职业) { $职业.职业.Count } else { 0 }), $技能集.Count, $天赋集.Count, $script:悬空)

# ---------- 8b. 技能 / Buff：挂载引用与属性口径（v51 刀52） ----------
# 为什么要有：刀52 一次加了 8 个技能 + 11 条 buff，全是"写错就静默失效"的引用：
#   · 技能.挂载Buff 写错 → 增益/减益技能放出去什么都没发生（战斗里只看到"用了技能"）；
#   · 技能.消耗物品 写错 → 技能永远提示"缺少消耗物"，谁也不知道是哪件东西；
#   · **buff.属性 只能写 攻击/防御/敏捷/速度** —— 这是最容易踩的坑：
#     敏捷 走的是"速度/暴击/闪避 一起动"（`战斗单位.当前暴击率/闪避率` 只吃敏捷），
#     速度 是**只加快行动条填充**的那一条（`当前速度` + `BattleService.意图间隔秒`，不加暴击/闪避）；
#     写成别的词（如"移速""攻速"）就是一条永不生效的 buff。
$buff集 = New-Object System.Collections.Generic.HashSet[string]
$bj = 读JSON "buffs.json"
if ($bj) { foreach ($b in $bj.Buffs) { if ($b.标识) { [void]$buff集.Add([string]$b.标识) } } }
$script:技能坏 = 0
if ($sj) {
    foreach ($s in $sj.技能) {
        if ($s.挂载Buff -and -not $buff集.Contains([string]$s.挂载Buff)) {
            报错 ("技能[{0}] 挂载Buff[{1}] 不在 buffs.json（放出去什么都不会发生）" -f $s.标识, $s.挂载Buff); $script:技能坏++
        }
        if ($s.消耗物品 -and -not $物品标识.Contains([string]$s.消耗物品)) {
            报错 ("技能[{0}] 消耗物品[{1}] 不在 items（技能永远提示缺消耗物）" -f $s.标识, $s.消耗物品); $script:技能坏++
        }
    }
}
if ($bj) {
    foreach ($b in $bj.Buffs) {
        if ($b.类型 -eq "增益" -or $b.类型 -eq "减益") {
            if ($b.属性 -and @("攻击", "防御", "敏捷", "速度") -notcontains [string]$b.属性) {
                报错 ("Buff[{0}] 属性[{1}] 非法（只能是 攻击/防御/敏捷/速度 —— 敏捷=速度+暴击+闪避，速度=只加快行动条）" -f $b.标识, $b.属性); $script:技能坏++
            }
            if ([int]$b.数值 -eq 0) { 报错 ("Buff[{0}] 数值为 0（挂了等于没挂）" -f $b.标识); $script:技能坏++ }
        }
    }
}
Write-Output ("技能 {0} 个 / Buff {1} 条 / 引用与属性 违规 {2} 处" -f `
    $(if ($sj) { $sj.技能.Count } else { 0 }), $buff集.Count, $script:技能坏)

# ---------- 9. 敌人 AI 行动表：`行动` 必须是 "普攻" 或 skills.json 里真实存在的技能 ----------
# 为什么要有：v51 之前 `enemies.json` **一处 行动表 都没有**（v46 宣称的"敌人读条大招、可被撞断"这条功能
#   从没在真实数据上跑过），补齐之后就轮到"技能标识写错"这类静默失效 —— 写错了 AI 会走到
#   `执行技能` 里查表失败然后落回普攻，玩家只觉得"这怪从来不放招"，不会报错。
#   判据与 战斗单位.从敌人生成 一致：`行动 != "普攻"` 的会被塞进 已学技能，所以必须真的存在。
$敌人根 = 读JSON "enemies.json"
if ($敌人根) {
    $有表 = 0; $技能数 = 0
    foreach ($e in $敌人根.敌人) {
        if (-not $e.标识) { continue }
        if (-not $e.行动表 -or $e.行动表.Count -eq 0) { continue }
        $有表++; $技能数 += $e.行动表.Count
        foreach ($a in $e.行动表) {
            if (-not $a.行动) { 报错 ("敌人[{0}] 行动表 有项缺 行动" -f $e.标识); continue }
            if ([int]$a.权重 -le 0) { 报错 ("敌人[{0}] 行动表[{1}] 权重不是正数" -f $e.标识, $a.行动) }
            if ($a.行动 -eq "普攻") { continue }
            if (-not $技能集.Contains([string]$a.行动)) {
                报错 ("敌人[{0}] 行动表 指向不存在的技能：{1}（skills.json 里没有）" -f $e.标识, $a.行动)
            }
        }
    }
    Write-Output ("敌人行动表：{0}/{1} 只配了行动表 / 共 {2} 条（行动 全部能在 skills.json 或 普攻 里找到）" -f `
        $有表, $敌人根.敌人.Count, $技能数)

    # 掉落列表（刀57）：标识必须存在 / 数量 >= 1 / 概率 (0,1]
    $掉坏 = 0; $掉条 = 0; $有掉表 = 0
    foreach ($e in $敌人根.敌人) {
        if (-not $e.标识) { continue }
        if (-not $e.掉落列表) { continue }
        $有掉表++
        foreach ($d in $e.掉落列表) {
            $掉条++
            if (-not $d.标识 -or -not $物品标识.Contains([string]$d.标识)) {
                报错 ("敌人[{0}] 掉落列表 指向不存在的物品：{1}" -f $e.标识, $d.标识); $掉坏++
            }
            if ([int]$d.数量 -lt 1) { 报错 ("敌人[{0}] 掉落列表[{1}] 数量 < 1" -f $e.标识, $d.标识); $掉坏++ }
            if ([double]$d.概率 -le 0 -or [double]$d.概率 -gt 1) {
                报错 ("敌人[{0}] 掉落列表[{1}] 概率 {2} 越界（(0,1]）" -f $e.标识, $d.标识, $d.概率); $掉坏++
            }
        }
    }
    Write-Output ("敌人掉落列表：{0} 只配了多掉落 / 共 {1} 条；违规 {2} 处" -f $有掉表, $掉条, $掉坏)
}

# ---------- 10. 楼梯那一列：首间房（楼梯间）的大门/内门 不许开在 楼梯边 上 ----------
# 为什么要有：v51 刀15 用户报的 bug —— 门有概率生成在楼梯那一列，导致楼梯上不了、门也进不去。
#   根因：`造空房` 的顺序是「加楼梯间 → 开门」，而 `开门` 取的是 内缩 0（正好是凸出去的那一行/列）。
#   那一侧的外线整个属于楼梯间（楼梯格 + 两侧墙 + 其余界外），门落在上面三种结局都是坏的。
#   判据与 DataService 的那条校验错误**是同一条规则**（这份离线不用开 Unity 就能看见）。
if ($建筑模板 -and $房间模板) {
    $冲突 = 0
    foreach ($b in $建筑模板.建筑) {
        if (-not $b.标识 -or -not $b.楼梯边) { continue }
        for ($fi = 0; $fi -lt $b.楼层.Count; $fi++) {
            $fl = $b.楼层[$fi]
            if (-not $fl.房间 -or $fl.房间.Count -eq 0) { continue }
            $首 = $fl.房间[0]                      # 每层首间房 = 楼梯间（楼梯就长在它身上）
            $rt = $房间模板.房间 | Where-Object { $_.标识 -eq $首 } | Select-Object -First 1
            if (-not $rt) { continue }
            if ($fl.大门 -and $rt.入口边 -eq $b.楼梯边) {
                报错 ("建筑[{0}]·{1}F 首间房[{2}] 大门（入口边 {3}）和楼梯开在同一条边上（排不出来）" -f $b.标识, ($fi + 1), $首, $rt.入口边)
                $冲突++
            }
            if ($rt.门) {
                foreach ($d in $rt.门) {
                    if (-not $d.通向) { continue }
                    if ($d.边 -eq $b.楼梯边) {
                        报错 ("建筑[{0}]·{1}F 首间房[{2}] 的内门开在[{3}]，而那正是 楼梯边：楼梯那一列只属于楼梯间（楼梯格+两侧墙+界外），门会压在楼梯格上或掉进界外 → 交互不了" -f $b.标识, ($fi + 1), $首, $d.边)
                        $冲突++
                    }
                }
            }
        }
    }
    Write-Output ("楼梯那一列：{0} 处冲突（首间房的 大门/内门 不许开在 楼梯边 上）" -f $冲突)
}

# ---------- 配件（改装件）的数据规则（v51 刀43）----------
# 为什么离线也要查一遍：这三条规则 DataService 里也有，但那是"进 Unity 才知道"；
#   而配件的错误后果都是**静默**的 —— 槽位写错=永远装不上、没加成=装上没用、配了耐久=看起来会用坏。
#   尤其是"配了耐久"这条：用户在游戏里看到"拆出来的配件是损坏的"就是这么来的（刀41）。
Write-Output ""
Write-Output "[配件] 槽位合法 / 至少一条加成 / 不许有耐久"
$合法配件槽 = @('枪口', '瞄具', '弹匣', '枪托', '握把', '刃口', '插板')
$加成分字段 = @('攻击加成', '防御加成', '生命加成', '负重加成', '抗性', '命中加成', '暴击加成', '闪避加成', '速度加成', '潜行加成')
$配件数 = 0; $配件坏 = 0
foreach ($f in (Get-ChildItem (Join-Path $数据 "物品") -Recurse -Filter *.json)) {
    try { $j = ([System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8) | ConvertFrom-Json) }
    catch { continue }
    foreach ($it in $j.物品) {
        if (-not $it -or $it.类型 -ne '配件') { continue }
        $配件数++
        if ($合法配件槽 -notcontains [string]$it.槽位) { 报错 ("配件[{0}] → 槽位[{1}] 非法（只能是 {2}）" -f $it.标识, $it.槽位, ($合法配件槽 -join '/')); $配件坏++ }
        $有加成 = $false
        foreach ($fld in $加成分字段) { if ($it.PSObject.Properties.Name -contains $fld -and [int]$it.$fld -ne 0) { $有加成 = $true } }
        if (-not $有加成) { 报错 ("配件[{0}] 没有任何加成（装上等于没用）" -f $it.标识); $配件坏++ }
        if (($it.PSObject.Properties.Name -contains '最大耐久') -and [int]$it.最大耐久 -gt 0) {
            报错 ("配件[{0}] 配了 最大耐久={1} —— 配件不是装备、没有耐久（用户 2026-09-13 定），请删掉" -f $it.标识, $it.最大耐久); $配件坏++
        }
    }
}
        if (($it.PSObject.Properties.Name -contains '弹匣容量加成') -and [int]$it.弹匣容量加成 -ne 0 -and [string]$it.槽位 -ne '弹匣') {
            报错 ("配件[{0}] 配了 弹匣容量加成 但槽位是[{1}] —— 只有 弹匣 槽的配件才能改容量" -f $it.标识, $it.槽位); $配件坏++
        }Write-Output ("  配件 {0} 件；违规 {1} 处" -f $配件数, $配件坏)
if ($配件数 -eq 0) { 报错 "一件配件都没扫到 —— 判据失效了（路径/字段名变了？）" }

Write-Output ""
Write-Output "[弹匣] 只做枪械：手枪/步枪/霰弹枪 可以有容量；弓弩/近战 不许配"
$枪械 = @('手枪', '步枪', '霰弹枪'); $有弹匣 = 0; $弹匣坏 = 0
foreach ($f in (Get-ChildItem (Join-Path $数据 "物品") -Recurse -Filter *.json)) {
    try { $j = ([System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8) | ConvertFrom-Json) }
    catch { continue }
    foreach ($it in $j.物品) {
        if (-not $it -or $it.类型 -ne '武器') { continue }
        if (-not ($it.PSObject.Properties.Name -contains '弹匣容量')) { continue }
        $n = [int]$it.弹匣容量
        if ($n -eq 0) { continue }
        $有弹匣++
        if ($枪械 -notcontains [string]$it.武器种类) { 报错 ("武器[{0}]（{1}）配了 弹匣容量={2} —— 弹匣只做枪械（弓弩/近战 不走弹匣）" -f $it.标识, $it.武器种类, $n); $弹匣坏++ }
        if ($n -lt 1) { 报错 ("武器[{0}] 弹匣容量={1} 非法（要么不配=无弹匣，要么 >= 1）" -f $it.标识, $n); $弹匣坏++ }
    }
}
Write-Output ("  配了弹匣的枪械 {0} 把；违规 {1} 处" -f $有弹匣, $弹匣坏)
if ($有弹匣 -eq 0) { 报错 "一把配弹匣的枪都没有 —— 判据失效了（字段名变了？）" }
if ($配件数 -eq 0) { 报错 "一件配件都没扫到 —— 判据失效了（路径/字段名变了？）" }

# ---------- 敌人特性（敌人词缀，v51 刀45）----------
# 为什么离线也要查：特性表写错**不会有任何报错**，只表现为"这只精英怪没变化"——
#   而它偏偏是玩家能一眼看出来的东西（"迅捷的感染者"跑得一样快 = 白挂）。
Write-Output ""
Write-Output "[敌人特性] 效果名合法 / 数值非零 / 权重 > 0 / 标识唯一"
$特性合法效果 = @('生命', '攻击', '防御', '速度', '暴击', '闪避', '攻击距离', '攻击间隔', '移动间隔')
$tj = 读JSON "敌人特性.json"
$特性数 = 0; $特性坏 = 0
if ($tj) {
    $见过 = New-Object System.Collections.Generic.HashSet[string]
    foreach ($t in $tj.特性) {
        $特性数++
        if (-not $见过.Add([string]$t.标识)) { 报错 ("敌人特性[{0}] 标识重复" -f $t.标识); $特性坏++ }
        if (-not $t.效果们 -or @($t.效果们).Count -eq 0) { 报错 ("敌人特性[{0}] 没有任何效果" -f $t.标识); $特性坏++; continue }
        foreach ($e in $t.效果们) {
            if ($特性合法效果 -notcontains [string]$e.效果) { 报错 ("敌人特性[{0}] → 效果[{1}] 认不出（只能是 {2}）" -f $t.标识, $e.效果, ($特性合法效果 -join '/')); $特性坏++ }
            if ([int]$e.数值 -eq 0) { 报错 ("敌人特性[{0}] → {1} 数值为 0（挂了等于没挂）" -f $t.标识, $e.效果); $特性坏++ }
        }
        if ([double]$t.权重 -le 0) { 报错 ("敌人特性[{0}] 权重 {1} 非法（>0）" -f $t.标识, $t.权重); $特性坏++ }
    }
}
Write-Output ("  敌人特性 {0} 条；违规 {1} 处" -f $特性数, $特性坏)
if ($特性数 -eq 0) { 报错 "敌人特性.json 一条都没扫到（文件缺失或字段名变了）" }

# ---------- 物品用途 / 配方门槛 / 成本比（刀54）----------
# 为什么离线也要查：这是"**每件物品都要有用途**"这条口径的门禁，靠人眼看会漏。
#   ★ 口径（踩过两次坑才定下来）：**配方材料 与 家具建造/升级材料 都是【消费方】，不是来源**。
#     来源只算：搜刮 / 配方产物 / 敌人掉落 / 职业初始 / 门锁投放 / 技能消耗物 / 种植成熟 / 腐坏产出。
#     （把"被配方当材料"当来源 → 鸡蛋 曾被误判可达；把"家具材料"当来源 → 医药箱 零来源没被逮到。）
#   ① 零来源：玩家在正式流程里摸不到
#   ② 无用途：不能直接用、不是任何配方料、不是任何家具料 —— `种类=贵重物品` 例外（交易硬通货，预留）
#   ③ 奢侈品不许进配方/家具（它们是商品，不是生产资料）
#   ④ 配方.需要工具 的标识必须存在（持有即可、不消耗）
#   ⑤ 成本比 越界（装备 0.6~1.6；弹药·材料·饮食 0.4~1.6）
Write-Output ""
Write-Output "[物品用途] 零来源 / 无用途 / 奢侈品禁入 / 需要工具 / 成本比"

$物 = @{}
foreach ($f in (Get-ChildItem (Join-Path $数据 "物品") -Recurse -Filter *.json)) {
    try { $j = ([System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8) | ConvertFrom-Json) }
    catch { continue }
    foreach ($it in $j.物品) { if ($it) { $物[[string]$it.标识] = $it } }
}
$来源 = @{}; $用途 = @{}
function 记来源([string]$键, [string]$标签) {
    if ([string]::IsNullOrEmpty($键) -or -not $物.ContainsKey($键)) { return }
    if (-not $来源.ContainsKey($键)) { $来源[$键] = New-Object System.Collections.Generic.HashSet[string] }
    [void]$来源[$键].Add($标签)
}
function 记用途([string]$键, [string]$标签) {
    if ([string]::IsNullOrEmpty($键) -or -not $物.ContainsKey($键)) { return }
    if (-not $用途.ContainsKey($键)) { $用途[$键] = New-Object System.Collections.Generic.HashSet[string] }
    [void]$用途[$键].Add($标签)
}
# ① 搜刮（顺便数"每个物品有几个搜刮落点"——只有一个落点 = 换个容器池就断供）
$搜刮数 = @{}
foreach ($m in $地图类型.地图类型) {
    foreach ($r in $m.房间) {
        foreach ($c in $r.容器) {
            foreach ($e in $c.搜索表) {
                记来源 ([string]$e.物品标识) "搜刮"
                if ($物.ContainsKey([string]$e.物品标识)) {
                    if (-not $搜刮数.ContainsKey([string]$e.物品标识)) { $搜刮数[[string]$e.物品标识] = 0 }
                    $搜刮数[[string]$e.物品标识]++
                }
            }
        }
    }
}
# ② 配方（产物 = 来源；材料/图纸/工具 = 用途）+ 成本比 + 需要工具存在性
$配方全部 = @()
foreach ($f in @("recipes_工作台.json", "recipes_灶台.json", "recipes_医疗站.json")) {
    $j = 读JSON $f
    if ($j) { foreach ($r in $j.配方) { $配方全部 += $r } }
}
$工具坏 = 0; $成本坏 = 0; $配方数 = 0
foreach ($r in $配方全部) {
    $配方数++
    记来源 ([string]$r.产物) ("制作:" + $r.标识)
    foreach ($m in $r.材料) { 记用途 ([string]$m.物品) ("配方材料:" + $r.标识) }
    if ($r.解锁图纸) { 记用途 ([string]$r.解锁图纸) ("解锁配方:" + $r.标识) }
    if ($r.需要工具) {
        foreach ($tg in $r.需要工具) {
            if (-not $物.ContainsKey([string]$tg)) { 报错 ("配方[{0}] → 需要工具[{1}] 不存在" -f $r.标识, $tg); $工具坏++ }
            else { 记用途 ([string]$tg) ("配方工具:" + $r.标识) }
        }
    }
    $成 = 0
    foreach ($m in $r.材料) { if ($物.ContainsKey([string]$m.物品)) { $成 += [int]$物[[string]$m.物品].价值 * [int]$m.数量 } }
    $份 = 1; if ($r.PSObject.Properties.Name -contains '产物数量' -and [int]$r.产物数量 -gt 0) { $份 = [int]$r.产物数量 }
    $值 = 0
    if ($物.ContainsKey([string]$r.产物)) { $值 = [int]$物[[string]$r.产物].价值 * $份 }
    if ($值 -gt 0) {
        $比 = [math]::Round($成 / $值, 2)
        $类型 = ""
        if ($物.ContainsKey([string]$r.产物)) { $类型 = [string]$物[[string]$r.产物].类型 }
        $装备类 = @('武器', '防具', '医疗', '容器', '配件') -contains $类型
        $低 = 0.4; if ($装备类) { $低 = 0.6 }
        if ($比 -lt $低 -or $比 -gt 1.6) {
            报错 ("配方[{0}] 成本比 {1} 越界（{2}：装备 0.6~1.6 / 弹药·材料·饮食 0.4~1.6）" -f $r.标识, $比, $类型)
            $成本坏++
        }
    }
}
# ③ 家具（建造/升级材料 = 用途）
$家具根 = 读JSON "家具.json"
$奢入配方家具 = 0
foreach ($fu in $家具根.家具) {
    foreach ($m in $fu.材料) { 记用途 ([string]$m.物品) ("家具建造:" + $fu.标识) }
    if ($fu.升级) { foreach ($u in $fu.升级) { if ($u.材料) { foreach ($m in $u.材料) { 记用途 ([string]$m.物品) ("家具升级:" + $fu.标识) } } } }
}
# ④ 敌人掉落 = 来源
foreach ($e in $敌人根.敌人) { 记来源 ([string]$e.掉落物品) "掉落" }
# ⑤ 职业初始装备 = 来源
foreach ($c in $职业.职业) { foreach ($e in $c.初始装备) { 记来源 ([string]$e.标识) "职业初始" } }
# ⑥ 门锁：钥匙 = 开门用途；钥匙投放（塞进本房容器/挂本房敌人）= 来源
foreach ($r in $房间模板.房间) { if ($r.门) { foreach ($d in $r.门) { 记用途 ([string]$d.锁) ("开门:" + $r.标识); 记来源 ([string]$d.锁) "钥匙投放" } } }
# ⑦ 技能消耗物 = 来源 + 用途
foreach ($s in $sj.技能) { if ($s.消耗物品) { 记来源 ([string]$s.消耗物品) ("技能:" + $s.标识); 记用途 ([string]$s.消耗物品) ("技能消耗:" + $s.标识) } }
# ⑧ 世界区域解锁条件（条件=物品）= 用途
foreach ($w in $世界.世界) { foreach ($q in $w.区域) { if ($q.解锁) { foreach ($t in $q.解锁) { if ($t.条件 -eq '物品') { 记用途 ([string]$t.标识) ("解锁区域:" + $q.区域模板) } } } } }
# ⑨ 直接使用 / 用途字段 / 就地转化 / 交易预留
$直用类型 = @('武器', '防具', '配件', '饮食', '医疗', '弹药', '容器', '书籍')
foreach ($k in $物.Keys) {
    $it = $物[$k]
    if ($直用类型 -contains [string]$it.类型) { 记用途 $k "直接使用" }
    if ($it.用途) { 记用途 $k ("用途字段:" + $it.用途) }
    if ([string]$it.种类 -eq '贵重物品') { 记用途 $k "交易预留" }
    if ($it.PSObject.Properties.Name -contains '生长时间' -and [int]$it.生长时间 -gt 0 -and $it.成熟产物) {
        记用途 $k "就地转化"
        # 可转化物 自己另有来源 → 它的成熟产物 才算可达（种子自身不算来源）
        $自有 = $false
        if ($来源.ContainsKey($k)) { foreach ($t in $来源[$k]) { if ($t -ne '就地转化') { $自有 = $true } } }
        if ($自有) { 记来源 ([string]$it.成熟产物) ("转化:" + $k) }
    }
}
记来源 "腐坏食物" "腐坏产出"

$零来源 = @(); $无用途 = @()
foreach ($k in $物.Keys) { if (-not $来源.ContainsKey($k)) { $零来源 += $k } }
foreach ($k in $物.Keys) { if (-not $用途.ContainsKey($k)) { $无用途 += $k } }
# 适用弹药（刀57）：武器写了的弹药必须存在、且确实是"弹药"类型
$弹坏 = 0; $弹数 = 0
foreach ($k in $物.Keys) {
    $it = $物[$k]
    if ($it.PSObject.Properties.Name -notcontains '适用弹药' -or -not $it.适用弹药) { continue }
    foreach ($a in $it.适用弹药) {
        $弹数++
        if (-not $物.ContainsKey([string]$a)) { 报错 ("武器[{0}] 适用弹药[{1}] 不存在" -f $k, $a); $弹坏++; continue }
        if ([string]$物[[string]$a].类型 -ne '弹药') { 报错 ("武器[{0}] 适用弹药[{1}] 不是弹药类型（{2}）" -f $k, $a, $物[[string]$a].类型); $弹坏++ }
    }
}
foreach ($k in ($零来源 | Sort-Object)) { 报错 ("物品[{0}] 零来源（搜刮/掉落/职业/门锁/技能/种植 都没有）" -f $k) }
foreach ($k in ($无用途 | Sort-Object)) { 报错 ("物品[{0}] 无用途（不能直接用、不是配方料、也不是家具料）" -f $k) }
# 单通路（刀59 定的口径，刀60 被我打回过一次 → 现在做成门禁）：
#   只要一件物品**有搜刮落点**，就必须有 **≥2 个** —— 一个容器池调整就会让它彻底断供。
#   零搜刮落点的物品不算（它们的来源是 配方/掉落/职业/门锁/种植，不属于这一条）。
$单通路 = @($搜刮数.Keys | Where-Object { $搜刮数[$_] -eq 1 } | Sort-Object)
foreach ($k in $单通路) { 报错 ("物品[{0}] 单通路（只有 1 个搜刮容器 —— 换个容器池就断供，请再补一个）" -f $k) }
# 奢侈品 不许进配方/家具（它们是商品）—— 与"无用途"的白名单成对
foreach ($r in $配方全部) { foreach ($m in $r.材料) { if ($物.ContainsKey([string]$m.物品) -and [string]$物[[string]$m.物品].种类 -eq '贵重物品') { 报错 ("奢侈品[{0}] 出现在配方[{1}] 里 —— 贵重物品只做交易硬通货，不进配方" -f $m.物品, $r.标识); $奢入配方家具++ } } }
foreach ($fu in $家具根.家具) {
    $料们 = @()
    foreach ($m in $fu.材料) { $料们 += $m }
    if ($fu.升级) { foreach ($u in $fu.升级) { if ($u.材料) { foreach ($m in $u.材料) { $料们 += $m } } } }
    foreach ($m in $料们) { if ($物.ContainsKey([string]$m.物品) -and [string]$物[[string]$m.物品].种类 -eq '贵重物品') { 报错 ("奢侈品[{0}] 出现在家具[{1}] 里 —— 贵重物品只做交易硬通货，不进家具" -f $m.物品, $fu.标识); $奢入配方家具++ } }
}
Write-Output ("  物品 {0} 件；零来源 {1} / 无用途 {2} / 奢侈品误入配方家具 {3} / 配方 {4} 条（需要工具坏 {5} · 成本比坏 {6}） / 适用弹药 {7} 条（坏 {8}）" -f $物.Count, $零来源.Count, $无用途.Count, $奢入配方家具, $配方数, $工具坏, $成本坏, $弹数, $弹坏)
if ($物.Count -eq 0) { 报错 "一件物品都没扫到 —— 判据失效了（路径/字段名变了？）" }


Write-Output ""
if ($script:失败 -eq 0) { Write-Output "✅ 数据引用核对通过（搜索表物品 / 容器池 / 敌人组 / 门锁钥匙 / 楼层房间 / 区域建筑与街上敌人 / 世界区域与敌人 / 配件规则 / 弹匣规则 / 敌人特性规则 全部存在）"; exit 0 }
Write-Output ("❌ 数据引用核对失败：{0} 处" -f $script:失败); exit 1
