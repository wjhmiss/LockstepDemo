param(
    [string]$SourceRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$TargetRoot = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")).Path ".." "ET"),
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$SourceRoot = (Resolve-Path $SourceRoot).Path
$TargetRoot = (Resolve-Path $TargetRoot).Path

$sourceGit = Join-Path $SourceRoot ".git"
$targetGit = Join-Path $TargetRoot ".git"

if (-not (Test-Path $sourceGit)) {
    throw "SourceRoot is not a git repository: $SourceRoot"
}

if (-not (Test-Path $targetGit)) {
    throw "TargetRoot is not a git repository: $TargetRoot"
}

if ($SourceRoot -eq $TargetRoot) {
    throw "SourceRoot and TargetRoot must be different."
}

# Open-source export whitelist. Only these paths are synchronized from WOW to ET.
# Test/Tests directories and Packages/cn.etetet.test are intentionally not
# excluded here; ../ET/.gitignore controls whether they can be committed.
$SyncItems = @(
    "Assets",
    "Book",
    "Packages",
    "ProjectSettings",
    "Scripts",
    "Directory.Build.props",
    "ET.sln.DotSettings",
    "LICENSE",
    "README.md",
    "AGENTS.md"
)

function Invoke-Rsync {
    param(
        [string]$Source,
        [string]$Target,
        [bool]$IsDirectory
    )

    $rsync = Get-Command "rsync" -ErrorAction SilentlyContinue
    if ($null -eq $rsync) {
        throw "rsync is required for whitelist sync on this machine."
    }

    $sourceArg = $Source
    $targetArg = $Target

    if ($IsDirectory) {
        $sourceArg = "$Source/"
        $targetArg = "$Target/"
        if (-not $DryRun) {
            New-Item -ItemType Directory -Force $Target | Out-Null
        }
    } else {
        $targetParent = Split-Path -Parent $Target
        if (-not $DryRun) {
            New-Item -ItemType Directory -Force $targetParent | Out-Null
        }
    }

    $args = @("-a", "--delete")
    if ($DryRun) {
        $args += "--dry-run"
    }
    $args += @($sourceArg, $targetArg)

    & $rsync.Source @args
    if ($LASTEXITCODE -ne 0) {
        throw "rsync failed for $Source"
    }
}

foreach ($item in $SyncItems) {
    $source = Join-Path $SourceRoot $item
    $target = Join-Path $TargetRoot $item

    if (-not (Test-Path $source)) {
        Write-Warning "Skip missing source path: $item"
        continue
    }

    $isDirectory = (Get-Item $source).PSIsContainer
    Write-Host "Sync $item"
    Invoke-Rsync -Source $source -Target $target -IsDirectory $isDirectory
}

$trackedPrivate = git -C $TargetRoot ls-files |
    Where-Object {
        $_ -match "^Packages/cn\.etetet\.test/" -or
        $_ -match "^Packages/cn\.etetet\.[^/]+/.*/Tests?/"
    }

if ($trackedPrivate) {
    Write-Error "Open-source repository still tracks private test paths:`n$($trackedPrivate -join "`n")"
}

Write-Host ""
Write-Host "Export finished. Review target changes with:"
Write-Host "  git -C `"$TargetRoot`" status --short"
