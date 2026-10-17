# Builds dist\NarrateEverythingInstaller.exe: one exe that installs this mod version (the mod
# and its data files, the BepInEx mod loader, prism.dll and its licences, the readme).
#
#   scripts\Package-Installer.ps1 [-Version 1.0.1] [-Game <folder>] [-SkipTests]
#
# The version defaults to the one in installer\Cargo.toml.
#
# The installer is the Rust program in installer\, built on the Rubber Magpie installer
# (github.com/Rubber-Magpie/magpie-installer). It needs Rust (the x64 MSVC toolchain) and
# Visual Studio 2022 Build Tools with the C++ workload and its Clang component (its CMake,
# Ninja and Clang are used). The first build compiles wxWidgets from source and takes a
# while. The mod is built here; BepInEx and its doorstop files are taken from the game
# folder, which must have BepInEx installed.
param(
    [string]$Version = "",
    [string]$Game = "",
    [switch]$SkipTests
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$dist = "$root\dist"
# Build folders are kept out of the project.
$work = "$env:LOCALAPPDATA\NarrateEverythingBuild"
$stage = "$work\stage"
$env:PATH = "$env:USERPROFILE\.cargo\bin;$env:PATH"
$env:DOTNET_NOLOGO = "1"
if (-not $Version) {
    $Version = ([regex]::Match((Get-Content -Raw "$root\installer\Cargo.toml"), '(?m)^version = "([^"]+)"')).Groups[1].Value
}

if (-not $Game) { $Game = (& "$PSScriptRoot\Find-GameDir.ps1") }
if (-not $Game -or -not (Test-Path -LiteralPath (Join-Path $Game "Date Everything.exe"))) {
    throw "Date Everything was not found. Pass -Game <folder> or set DATE_EVERYTHING_DIR."
}
if (-not (Test-Path -LiteralPath "$Game\BepInEx\core\BepInEx.dll")) {
    throw "BepInEx is not installed in $Game; it is taken from there."
}

if (Test-Path $stage) { [IO.Directory]::Delete($stage, $true) }
New-Item -ItemType Directory -Force $stage, $dist | Out-Null

function Run($what, [scriptblock]$cmd) {
    # (cargo reports progress on stderr, which "Stop" would treat as a failure.)
    $ErrorActionPreference = "Continue"
    $out = & $cmd 2>&1
    $ErrorActionPreference = "Stop"
    if ($LASTEXITCODE -ne 0) {
        throw "$what failed:`n" + (($out | Select-Object -Last 40 | ForEach-Object { "$_" }) -join "`n")
    }
    Write-Output "$what OK."
}

# The mod, built here (not copied from the game folder), with its data files.
Run "Mod build" { dotnet build "$root\DateEverythingAccess.csproj" -c Release -v q -nologo "-p:GameDir=$Game" -p:SkipCopyToPlugins=true }
$plugins = "$stage\BepInEx\plugins"
New-Item -ItemType Directory -Force $plugins | Out-Null
$built = "$root\bin\Release\net472"
foreach ($f in "DateEverythingAccess.dll", "navigable_region.bake.json") { Copy-Item "$built\$f" $plugins }
Copy-Item "$root\card_pose_descriptions.*.json" $plugins

# Speech: prism.dll beside the game's exe, its licences beside it.
Copy-Item "$root\lib\prism\prism.dll" $stage
Copy-Item "$root\lib\prism\LICENSES" "$stage\Prism-Licenses" -Recurse
Copy-Item "$root\lib\prism\NOTICE.txt", "$root\lib\prism\VERSION.txt" "$stage\Prism-Licenses"

# BepInEx: its loader files and core, not what it makes while the game runs (logs, cache,
# config, other mods).
foreach ($f in ".doorstop_version", "doorstop_config.ini", "winhttp.dll") { Copy-Item -LiteralPath "$Game\$f" $stage }
New-Item -ItemType Directory -Force "$stage\BepInEx\core" | Out-Null
Copy-Item "$Game\BepInEx\core\*" "$stage\BepInEx\core" -Recurse

Copy-Item "$root\README.txt" "$stage\NarrateEverything-README.txt"
Copy-Item "$root\installer\THIRD-PARTY-NOTICES.txt" "$stage\NarrateEverything-THIRD-PARTY-NOTICES.txt"

$files = Get-ChildItem $stage -Recurse -File
Write-Output ("Staged {0} files, {1:N1} MB." -f $files.Count, (($files | Measure-Object Length -Sum).Sum / 1MB))

# The installer around it.
$toolchain = "+stable-x86_64-pc-windows-msvc"
$vs = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools"
$env:PATH = "$vs\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin;$vs\Common7\IDE\CommonExtensions\Microsoft\CMake\Ninja;$env:PATH"
$env:LIBCLANG_PATH = "$vs\VC\Tools\Llvm\x64\bin"
$env:CARGO_TARGET_DIR = "$work\target"
$env:NE_STAGE = $stage
$env:NE_VERSION = $Version
Push-Location "$root\installer"
try {
    if (-not $SkipTests) { Run "Installer tests" { cargo $toolchain test --release } }
    Run "Installer" { cargo $toolchain build --release }
} finally { Pop-Location }
Copy-Item "$work\target\release\NarrateEverythingInstaller.exe" "$dist\" -Force
$mb = [math]::Round((Get-Item "$dist\NarrateEverythingInstaller.exe").Length / 1MB, 1)
Write-Output "Built $dist\NarrateEverythingInstaller.exe ($mb MB), version $Version."
