# ============================================================
# 目录核对：守住"目录结构"这件事本身（不打开 Unity 就能跑）
# 为什么需要（v51 刀23 目录调整时立的）：
#   ① **`.cs` 与 `.cs.meta` 必须一一配对** —— 场景/预制体里 60+ 处 [SerializeField] 引用靠的是
#      meta 里的 **GUID**；搬文件漏了 meta，Unity 会重新发一个 GUID，那个组件就变成 Missing Script。
#      这类事故**不会让编译失败**，只在打开场景时表现为"某个面板全是空的"。
#   ② 共用网格框架（网格面板基类 及其分部/配色/拖拽）必须在 **UI/网格/** —— 它同时被
#      探索层（房间/区域/大世界）与物品层（物品/家具网格面板）继承，放在任何一个功能目录下都是错的。
#   ③ Domain / Services 的**本层不许有 .cs**（全部进子系统子目录）—— 这是这次目录调整的成果，
#      不加断言的话，下次加文件又会随手丢在根上（Domain 根上曾经堆了 11 个）。
# 边界：只查结构与配对，查不了"这个文件放这里合不合适"（那要靠人看）。
# 用法（仓库根）：& ".\Tools\目录核对.ps1"
# 编码纪律：一律 [IO.File]::ReadAllText + 显式 UTF8
# ============================================================
$ErrorActionPreference = "Stop"
$根 = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$脚本根 = Join-Path $根 "Assets\Scripts"
$script:失败 = 0
function 报错([string]$文本) { Write-Output ("  ✗ " + $文本); $script:失败++ }

Write-Output "=== 目录核对 ==="

# ---------- 1. .cs 与 .cs.meta 一一配对 ----------
Write-Output "[1] .cs / .cs.meta 配对（防搬目录漏 meta → 场景引用变 Missing Script）"
$所有文件 = Get-ChildItem $脚本根 -Recurse -File
$cs = @($所有文件 | Where-Object { $_.Name -like "*.cs" })
$csMeta = @($所有文件 | Where-Object { $_.Name -like "*.cs.meta" })
$缺meta = @()
foreach ($f in $cs) { if (-not (Test-Path ($f.FullName + ".meta"))) { $缺meta += $f } }
$孤儿meta = @()
foreach ($m in $csMeta) {
    $真身 = $m.FullName.Substring(0, $m.FullName.Length - 5)   # 去掉 .meta
    if (-not (Test-Path $真身)) { $孤儿meta += $m }
}
foreach ($f in $缺meta) { 报错 ("缺 .meta：" + $f.FullName.Substring($根.Length + 1) + " —— 它的 GUID 会变，场景里的引用会变成 Missing Script") }
foreach ($m in $孤儿meta) { 报错 ("孤儿 .meta（对应的 .cs 不存在）：" + $m.FullName.Substring($根.Length + 1)) }
Write-Output ("  .cs {0} 个 / .cs.meta {1} 个；缺 meta {2} 个、孤儿 meta {3} 个" -f $cs.Count, $csMeta.Count, $缺meta.Count, $孤儿meta.Count)
if ($cs.Count -eq 0) { 报错 "一个 .cs 都没扫到 —— 路径错了？" }

# ---------- 2. 共用网格框架必须在 UI/网格/ ----------
Write-Output "[2] 共用网格框架必须在 UI/网格/（它被探索层与物品层共同继承）"
$网格框架 = Join-Path $脚本根 "UI\网格\网格面板基类.cs"
if (-not (Test-Path $网格框架)) { 报错 "UI/网格/网格面板基类.cs 不存在 —— 共用框架又被挪回某个功能目录了？" }
$框架件 = @("网格面板基类.交互.cs", "网格面板基类.拖拽.cs", "网格面板基类.渲染.cs", "网格面板配色.cs", "网格面板拖拽.cs", "精灵内容包围盒.cs")
foreach ($f in $框架件) { if (-not (Test-Path (Join-Path $脚本根 "UI\网格\$f"))) { 报错 ("UI/网格/ 少了共用件：" + $f) } }
foreach ($坏 in (Get-ChildItem (Join-Path $脚本根 "UI\物品") -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "网格面板基类*" })) {
    报错 ("UI/物品 下仍有网格基类文件：" + $坏.Name + "（应只在 UI/网格/）")
}

# ---------- 3. Domain / Services 本层不许有 .cs ----------
Write-Output "[3] Domain / Services 的**本层**不许有 .cs（全部进子系统子目录）"
foreach ($层 in @("Domain", "Services")) {
    $散 = @(Get-ChildItem (Join-Path $脚本根 $层) -File -Filter *.cs -ErrorAction SilentlyContinue)
    foreach ($f in $散) { 报错 ("{0}/ 本层散着 {1} —— 请放进子系统子目录（本次目录调整的目标就是消灭这种平铺）" -f $层, $f.Name) }
    Write-Output ("  {0}/ 本层 .cs：{1} 个" -f $层, $散.Count)
}

# ---------- 4. 顶层分层必须齐全 ----------
Write-Output "[4] 六个顶层分层目录必须齐全"
foreach ($层 in @("Core", "Data", "Domain", "Services", "Events", "UI")) {
    if (-not (Test-Path (Join-Path $脚本根 $层))) { 报错 ("顶层目录不见了：" + $层) }
}

Write-Output ""
if ($script:失败 -eq 0) {
    Write-Output "✅ 目录核对通过（cs/meta 配对 · 共用网格框架位置 · Domain/Services 本层无散件 · 顶层分层齐全）"
    Write-Output "   ⚠ 只查结构与配对；「这个文件放这里合不合适」仍要人看"
    exit 0
}
Write-Output ("❌ 目录核对失败：{0} 处" -f $script:失败); exit 1
