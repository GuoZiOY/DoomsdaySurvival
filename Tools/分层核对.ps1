# ============================================================
# 分层核对：按"四层 + 组合根"的依赖方向扫越层引用（不打开 Unity 就能跑）
# 为什么需要：项目的分层（UI ← 事件 ← Services ← Domain ← Data，Core 是组合根）**只写在文档和注释里**，
#   编译器不管（0 asmdef）→ 已经真的发生过越层：Data 调 Domain、Domain 引 UnityEngine、事件拖 UI。
#   这条脚本把"口头约定"变成**红灯清单**，也是 asmdef 硬化（档3）的前置：先知道有多少违规，再决定值不值得。
#
# 判据（写在明处）：
#   · 类型→层：按文件所在目录（Core/Data/Domain/Services/Events/UI/Editor）；
#   · 逐文件取标识符（含中文），命中"禁止引用的层的类型名"就报；
#   · 同名类型在多层都有 → **跳过**（歧义，不报；脚本末尾给出歧义数量）；
#   · 已知并记过账的违规走 白名单（写清理由），只有**新违规**才让脚本失败。
# 例外（有意豁免，别当 bug）：
#   · Core（组合根 GameBootstrap 要注册所有服务）、Editor（编辑器工具）；
#   · UI 可以引用下面各层（UI → Services/Domain/Data 是允许方向）。
# 用法（仓库根）：& ".\Tools\分层核对.ps1"
# 编码纪律：一律 [IO.File]::ReadAllText + 显式 UTF8
# ============================================================
$ErrorActionPreference = "Stop"
$根 = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$脚本根 = Join-Path $根 "Assets\Scripts"
$script:失败 = 0
function 报错([string]$t) { Write-Output ("  ✗ " + $t); $script:失败++ }

# 每层禁止引用的层（Core/Editor 不列 = 豁免）
$禁 = @{
    "Data"     = @("Domain", "Services", "Events", "UI")
    "Domain"   = @("Services", "Events", "UI")
    "Services" = @("UI")
    "Events"   = @("Services", "UI")
    "UI"       = @()
    "Core"     = @()
    "Editor"   = @()
}

# 已知违规白名单：文件（相对 Assets\Scripts）→ 允许引用的"层:类型"（理由见 docs）
# 已知违规白名单（这 13 处就是**架构债清单**，每处都写了"为什么先留着 + 打算怎么修"）
# 修完一处就删一行；**白名单清零那天就是 asmdef 硬化的时机**（档3）。
$白名单 = @{
    # ① Data → Domain（反向依赖，审计 A2）：DataService 校验"占地形状"时用了 Domain 的纯几何帮助类
    #    修法候选：把 建筑外形.cs 挪到 Data（它对 建筑外形数据 DTO 做形状→掩码，域层再用就是允许方向 Domain→Data）
    'Data\DataService.cs'                          = @('Domain:建筑外形')
    # ② Data → Domain：DTO 的字段用了 Domain 的枚举（品质 / 伤病类型）
    #    修法候选：这两个**枚举**搬进 数据模型.cs（枚举本来就属于数据），品质工具（倍率/颜色）留在 Domain
    'Data\数据模型.cs'                             = @('Domain:品质', 'Domain:伤病类型')
    # ③ Domain → Events：效果结算 发了 背包变化事件（用了它带的 变化原因 枚举）
    #    修法候选：变化原因 是领域概念，搬去 Domain；事件只引用领域类型
    'Domain\玩家\效果结算.cs'                      = @('Events:变化原因')
    # ④ Domain → Services：生存管理器 直接取 世界时间管理器 同步整点基准
    #    修法候选：反过来——由 世界时间管理器 在推进后回调生存管理器（依赖倒过来）
    'Domain\玩家\生存管理器.cs'                    = @('Services:世界时间管理器')
    # ⑤ Events → UI（刀24 新发现）：打开面板事件里带着 面板基类 字段 —— 事件不该拖 UI
    #    修法候选：事件只带"要开哪个面板"的标识（枚举/字符串），由 面板管理器 翻译
    'Events\导航事件.cs'                           = @('UI:面板基类')
    # ⑥ Services → UI：音效管理器 调 UI 的 悬停反馈.注册按钮
    #    修法候选：悬停反馈 改成订阅一个 音效事件（服务只发意图）
    'Services\世界\音效管理器.cs'                  = @('UI:悬停反馈')
    # ⑦ Services → UI：自动存档器 判断"当前是不是主菜单"时直接 is 面板类型
    #    修法候选：面板管理器 发布一个语义化事件（如 进入标题界面），存档器订阅它
    'Services\存档\自动存档器.cs'                  = @('UI:面板管理器', 'UI:主菜单面板')
    # ⑧ Services → UI：战斗文本直接引用 UI 常量 游戏主题.危险色值（审计 04 #18 记过）
    #    修法候选：服务只给纯文本（伤害事件已带足够字段），上色交给 UI
    'Services\战斗\BattleService.伤害与结算.cs'    = @('UI:游戏主题')
    'Services\战斗\BattleService.回派.cs'          = @('UI:游戏主题')
    # ⑨ Services → UI：房间探索服务 直接 搜索面板.打开搜索（审计 B5：唯一一处例外，属"主动弹"）
    #    修法候选：改成发一个 打开搜索面板事件
    'Services\探索\房间探索服务.cs'                = @('UI:搜索面板')
}

# Domain 零 UnityEngine 的白名单（同样是"已知 + 修法"）
$域Unity白名单 = @{
    # 品质.cs 用 Color/ColorUtility 给品质上色（审计 A3）。修法：颜色那两行挪去 UI，Domain 只留 倍率/名称
    'Domain\玩家\品质.cs' = '品质工具.颜色/富文本标签 用了 Color —— 颜色挪去 UI 层即可恢复纯 C#'
}

function 取层([string]$相对路径) {
    $段 = $相对路径 -split '[\\/]'
    if ($段.Count -ge 1) { return $段[0] }   # 相对路径已去掉 Assets\Scripts\，所以 [0] 就是层名
    return "?"
}

Write-Output "=== 分层核对（依赖方向）==="

# ---------- 1. 类型 → 层 映射 ----------
$类型层 = @{}       # 类型名 → HashSet[层]
$歧义 = 0
$文件 = Get-ChildItem $脚本根 -Recurse -Filter *.cs
foreach ($f in $文件) {
    $相对 = $f.FullName.Substring($脚本根.Length + 1)
    $层 = 取层 $相对
    $文本 = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
    foreach ($m in [regex]::Matches($文本, '(?m)^\s*(?:public|internal|sealed|abstract|static|partial|\s)*\b(?:class|struct|enum|interface)\s+([\p{L}\p{N}_]+)')) {
        $名 = $m.Groups[1].Value
        if (-not $类型层.ContainsKey($名)) { $类型层[$名] = New-Object System.Collections.Generic.HashSet[string] }
        [void]$类型层[$名].Add($层)
    }
}
foreach ($k in $类型层.Keys) { if ($类型层[$k].Count -gt 1) { $歧义++ } }
Write-Output ("类型 {0} 个（其中跨层重名 {1} 个 → 扫描时跳过）" -f $类型层.Count, $歧义)

# ---------- 2. 逐文件扫越层引用 ----------
Write-Output "[1] 跨层引用（按各层禁止清单）"
$违规 = New-Object System.Collections.Generic.List[object]
foreach ($f in $文件) {
    $相对 = $f.FullName.Substring($脚本根.Length + 1)
    $层 = 取层 $相对
    if (-not $禁.ContainsKey($层)) { continue }
    if ($禁[$层].Count -eq 0) { continue }
    $文本 = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
    # 去注释（行注释 + 块注释），避免注释里提到别的层就误报
    $净 = [regex]::Replace($文本, '(?s)/\*.*?\*/', '')
    $净 = [regex]::Replace($净, '(?m)//.*$', '')
    # 行号：逐行扫，方便报位置
    $行们 = $净 -split "`r?`n"
    for ($i = 0; $i -lt $行们.Count; $i++) {
        foreach ($m in [regex]::Matches($行们[$i], '[\p{L}\p{N}_]+')) {
            $tok = $m.Value
            if (-not $类型层.ContainsKey($tok)) { continue }
            $层们 = $类型层[$tok]
            if ($层们.Count -gt 1) { continue }          # 歧义跳过
            $它的层 = @($层们)[0]
            if ($它的层 -eq $层) { continue }            # 自己层
            if ($禁[$层] -notcontains $它的层) { continue }  # 允许方向
            $违规.Add([pscustomobject]@{ 文件 = $相对; 行 = ($i + 1); 类型 = $tok; 它的层 = $它的层; 本层 = $层; 原文 = $行们[$i].Trim() })
        }
    }
}

# 去重（同一文件里同一类型只报一次）
$已报 = @{}
$新违规 = @(); $旧违规 = @()
foreach ($v in $违规) {
    $键 = "{0}|{1}:{2}" -f $v.文件, $v.它的层, $v.类型
    if ($已报.ContainsKey($键)) { continue }
    $已报[$键] = $true
    $允许 = $白名单.ContainsKey($v.文件) -and ($白名单[$v.文件] -contains ($v.它的层 + ":" + $v.类型))
    if ($允许) { $旧违规 += $v } else { $新违规 += $v }
}
Write-Output ("  白名单内（已记账）{0} 处；新违规 {1} 处" -f $旧违规.Count, $新违规.Count)
foreach ($v in $旧违规) { Write-Output ("  · [已知] {0}:{1} 引用了 {2} 的 {3}" -f $v.文件, $v.行, $v.它的层, $v.类型) }
foreach ($v in $新违规) {
    报错 ("{0}:{1}（{2} 层）引用了 {3} 层的类型 [{4}] —— {5}" -f $v.文件, $v.行, $v.本层, $v.它的层, $v.类型, $v.原文)
}
if ($类型层.Count -lt 50) { 报错 ("只扫到 {0} 个类型 —— 判据失效了（路径/正则错了？）" -f $类型层.Count) }

# ---------- 3. Domain 必须零 UnityEngine ----------
Write-Output "[2] Domain 必须零 UnityEngine（纯 C# 铁律）"
$domain根 = Join-Path $脚本根 "Domain"
$引了 = @()
foreach ($f in (Get-ChildItem $domain根 -Recurse -Filter *.cs)) {
    $文本 = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
    if ($文本 -match '(?m)^\s*using\s+UnityEngine') { $引了 += $f.FullName.Substring($脚本根.Length + 1) }
}
if ($引了.Count -gt 0) {
    foreach ($x in $引了) {
        if ($域Unity白名单.ContainsKey($x)) { Write-Output ("  · [已知] {0} —— {1}" -f $x, $域Unity白名单[$x]) }
        else { 报错 ("Domain 文件引了 UnityEngine（纯 C# 铁律破了）：" + $x) }
    }
} else { Write-Output "  0 处（Domain 干净）" }

# ---------- 4. 提示项（不是失败）：UI 直调 Domain 算法 ----------
Write-Output "[3] 提示（不算失败）：UI 直接 new / 调 Domain 的生成器（A4 那一类，建议走「只读几何门面」）"
$生成器 = @("区域生成器", "大世界生成器", "房间生成器", "建筑生成器", "网格寻路", "网格视野", "约束摆放")
$命中 = 0
foreach ($f in (Get-ChildItem (Join-Path $脚本根 "UI") -Recurse -Filter *.cs)) {
    $净 = [regex]::Replace([System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8), '(?m)//.*$', '')
    foreach ($g in $生成器) {
        if ($净 -match ("\b" + $g + "\b")) { $命中++; break }
    }
}
Write-Output ("  命中 {0} 个 UI 文件（想收敛就抽「只读几何门面」，见 docs 档2）" -f $命中)

Write-Output ""
if ($script:失败 -eq 0) {
    Write-Output "✅ 分层核对通过（跨层引用无新增违规 · Domain 零 UnityEngine）"
    Write-Output "   ⚠ 已知违规走白名单（脚本里写清了理由）；白名单清零那天就是 asmdef 硬化的时机"
    exit 0
}
Write-Output ("❌ 分层核对失败：{0} 处" -f $script:失败); exit 1
