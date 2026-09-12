# Offline check of the world-layer cell-pool range math (ASCII only, so PS 5.1 arg encoding can't mangle it).
# Mirrors 探索图层.在窗口内() and 探索图层.窗口格区间() literally.
# Property under test: every cell for which 在窗口内() is true must fall inside the pool range,
# and the range must always fit the pool capacity.
$script:cell = 90.0
$script:cols = 100
$script:rows = 100
$script:margin = 2
$script:fails = 0
$script:maxCells = 0
$script:checked = 0
$script:combos = 0

function InWindow([double]$c, [double]$r, [double]$camX, [double]$camY, [double]$winW, [double]$winH) {
    $x = $c * $script:cell + $camX
    $y = -($r * $script:cell) + $camY
    return (($x + $script:cell) -gt 0) -and ($x -lt $winW) -and ($y -gt (-$winH)) -and ($y -lt $script:cell)
}

function CheckWindow([double]$winW, [double]$winH) {
    $cell = $script:cell; $cols = $script:cols; $rows = $script:rows; $margin = $script:margin
    $cap = [Math]::Min(([Math]::Ceiling($winW / $cell) + 1 + $margin * 2) * ([Math]::Ceiling($winH / $cell) + 1 + $margin * 2), $cols * $rows)
    $script:windowMax = 0
    $gridW = $cols * $cell
    $gridH = $rows * $cell
    $spanX = $winW - $gridW
    $spanY = $gridH - $winH

    $xs = @(0.0, $spanX, [Math]::Round($spanX / 2), ($spanX / 3.7))
    $ys = @(0.0, $spanY, [Math]::Round($spanY / 2), ($spanY / 2.3))

    foreach ($camX in $xs) {
        foreach ($camY in $ys) {
            $script:combos++
            $c0 = [Math]::Max(0, [Math]::Floor(-$camX / $cell) - $margin)
            $c1 = [Math]::Min($cols - 1, [Math]::Floor(($winW - $camX) / $cell) + $margin)
            $r0 = [Math]::Max(0, [Math]::Floor($camY / $cell) - $margin)
            $r1 = [Math]::Min($rows - 1, [Math]::Floor(($camY + $winH) / $cell) + $margin)
            $need = ($c1 - $c0 + 1) * ($r1 - $r0 + 1)
            if ($need -gt $script:maxCells) { $script:maxCells = $need }
            if ($need -gt $script:windowMax) { $script:windowMax = $need }
            if ($need -gt $cap) {
                $script:fails++
                Write-Output ("FAIL capacity: win {0}x{1} cam({2},{3}) needs {4} > cap {5}" -f $winW, $winH, $camX, $camY, $need, $cap)
            }
            for ($r = 0; $r -lt $rows; $r++) {
                for ($c = 0; $c -lt $cols; $c++) {
                    if (-not (InWindow $c $r $camX $camY $winW $winH)) { continue }
                    $script:checked++
                    if ($c -lt $c0 -or $c -gt $c1 -or $r -lt $r0 -or $r -gt $r1) {
                        $script:fails++
                        Write-Output ("FAIL coverage: win {0}x{1} cam({2},{3}) cell({4},{5}) in-window but outside pool range ({6}~{7},{8}~{9})" -f $winW, $winH, $camX, $camY, $c, $r, $c0, $c1, $r0, $r1)
                    }
                }
            }
        }
    }
    Write-Output ("  window {0}x{1}: pool capacity {2}, worst-case range {3}  {4}" -f $winW, $winH, $cap, $script:windowMax, $(if ($script:windowMax -le $cap) { "OK" } else { "OVER CAPACITY" }))
}

CheckWindow 1720 960
CheckWindow 2250 1170   # 场景里 大世界面板 实际配的 视口（不是默认值 —— 真实配置必须被验到）
CheckWindow 1280 720
CheckWindow 2560 1440
CheckWindow 900 600

$cap1720 = [Math]::Min(([Math]::Ceiling(1720 / $script:cell) + 1 + 4) * ([Math]::Ceiling(960 / $script:cell) + 1 + 4), $script:cols * $script:rows)
Write-Output ("combos           = {0}  (5 window sizes x 16 camera positions)" -f $script:combos)
Write-Output ("cells checked    = {0}   (must be > 0 or the test is vacuous)" -f $script:checked)
Write-Output ("worst-case cells = {0}" -f $script:maxCells)
Write-Output ("pool capacity    = {0}  (1720x960)" -f $cap1720)
if ($script:checked -eq 0) { Write-Output "VACUOUS TEST - no cells were checked"; exit 2 }
if ($script:fails -eq 0) { Write-Output "PASS: every in-window cell is inside the pool range, and capacity always suffices"; exit 0 }
Write-Output ("FAIL: {0} problems" -f $script:fails); exit 1
