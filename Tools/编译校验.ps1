# ============================================================
# 编译校验：不打开 Unity 就把游戏程序集编一遍（csc + Unity 生成的 rsp 里的 244 个引用）
# 用途：重构/改代码后先在这里过一遍，别等 Unity 焦点时才报错。
# 用法（在仓库根）：
#   pwsh -File Tools\编译校验.ps1            # 只编"游戏程序集"（Assets/Scripts，排除 Editor）
#   pwsh -File Tools\编译校验.ps1 -含Editor   # 连 Editor 程序集（需要 UnityEditor 的 rsp）
# 说明：引用与 define 全部取自 Unity 自己生成的 Assembly-CSharp.rsp（改包/换 Unity 版本后它会自动更新）
# ============================================================
param([switch]$含Editor)

$ErrorActionPreference = "Stop"
$根 = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$artifacts = Join-Path $根 "Library\Bee\artifacts\1900b0aP.dag"

function 取选项([string]$rsp路径) {
    if (-not (Test-Path $rsp路径)) { throw "找不到 Unity 生成的 rsp：$rsp路径（先让 Unity 编译一次）" }
    # 只留编译选项：去掉 -out / -refout / -target / -additionalfile 与"源码行"
    Get-Content $rsp路径 | Where-Object { $_ -match '^-(define|r|nowarn|langversion|nullable|unsafe|nostdlib|utf8output|preferreduilang)' }
}

function 取源([string]$目录, [switch]$排除Editor) {
    Get-ChildItem $目录 -Recurse -Filter *.cs |
        Where-Object { -not $排除Editor -or $_.FullName -notmatch '\\Editor\\' } |
        ForEach-Object { '"' + $_.FullName.Replace('\', '/') + '"' }
}

$csc = Get-ChildItem "$env:ProgramFiles\dotnet\sdk" -Directory |
    ForEach-Object { Join-Path $_.FullName "Roslyn\bincore\csc.dll" } |
    Where-Object { Test-Path $_ } | Select-Object -Last 1
if (-not $csc) { throw "找不到 Roslyn csc（dotnet SDK 里应有 Roslyn\bincore\csc.dll）" }

$参数 = New-Object System.Collections.Generic.List[string]
$参数.Add("-target:library")
$参数.Add("-nologo")
$参数.Add("-out:$env:TEMP\编译校验-游戏.dll")
$参数.AddRange([string[]](取选项 (Join-Path $artifacts "Assembly-CSharp.rsp")))
$参数.AddRange([string[]](取源 (Join-Path $根 "Assets\Scripts") -排除Editor))

$rsp2 = Join-Path $env:TEMP "编译校验.rsp"
$参数 | Set-Content $rsp2 -Encoding utf8
Write-Output ("[编译校验] 游戏程序集：{0} 个源文件" -f (($参数 | Where-Object { $_ -like '"*' }).Count))
& dotnet $csc "@$rsp2"
$码 = $LASTEXITCODE
if ($码 -eq 0) { Write-Output "[编译校验] 游戏程序集 exit 0 ✅" } else { Write-Output "[编译校验] 游戏程序集 exit $码 ❌" }

if ($含Editor) {
    # ★ 修（刀61）：Editor 的 rsp 在**另一个 dag 目录**里（`1900b0aE.dag`，运行时那个是 `1900b0aP.dag`），
    #   而这里原来只在 $artifacts（= 运行时那个 dag）下找 → 永远找不到 → **静默跳过 Editor**，
    #   于是"我编过 Editor 程序集了"这句话一直是假的（Editor 脚本从没被离线校验过）。
    #   现在整个 artifacts 下递归找，并且**找不到要大声报**（不再静默跳过）。
    $editorRsp = Get-ChildItem (Join-Path $根 "Library\Bee\artifacts") -Recurse -Filter "Assembly-CSharp-Editor.rsp" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $editorRsp) { Write-Output "[编译校验] ❌ 没找到 Assembly-CSharp-Editor.rsp —— Editor 脚本这次**没有被校验**（先在 Unity 里编译一次生成它）"; exit $码 }
    $参数2 = New-Object System.Collections.Generic.List[string]
    $参数2.Add("-target:library")
    $参数2.Add("-nologo")
    $参数2.Add("-out:$env:TEMP\编译校验-Editor.dll")
    # ★ 修（刀61）：Unity 的 Editor rsp 里 `-r:` 指向的是**它上次编译**的 Assembly-CSharp.dll（旧的），
    #   于是"刚加进运行时的新成员"在 Editor 这一遍里看不见 → 报一堆假错（CS1061 / CS0122）。
    #   这里把那一条换成**本脚本刚编出来的**游戏程序集 → Editor 校验才真的在校验当前源码。
    $参数2.AddRange([string[]](取选项 $editorRsp.FullName | Where-Object { $_ -notmatch 'Assembly-CSharp(\.ref)?\.dll' }))
    $参数2.Add("-r:$env:TEMP\编译校验-游戏.dll")
    $参数2.AddRange([string[]](取源 (Join-Path $根 "Assets\Scripts") -排除Editor:$false | Where-Object { $_ -match '/Editor/' }))
    $rsp3 = Join-Path $env:TEMP "编译校验-Editor.rsp"
    $参数2 | Set-Content $rsp3 -Encoding utf8
    Write-Output ("[编译校验] Editor 程序集：{0} 个源文件" -f (($参数2 | Where-Object { $_ -like '"*' }).Count))
    & dotnet $csc "@$rsp3"
    if ($LASTEXITCODE -eq 0) { Write-Output "[编译校验] Editor 程序集 exit 0 ✅" } else { Write-Output "[编译校验] Editor 程序集 exit $LASTEXITCODE ❌"; if ($码 -eq 0) { $码 = $LASTEXITCODE } }
}
exit $码
