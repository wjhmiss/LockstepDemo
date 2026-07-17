Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$packagesRoot = Join-Path $repoRoot 'Packages'
$packageFiles = Get-ChildItem -Path $packagesRoot -Recurse -Filter *.cs -File
$startConfigFiles = Get-ChildItem -Path (Join-Path $packagesRoot 'cn.etetet.startconfig') -Recurse -Filter *.cs -File

$targetCategories = @(
    'EquipmentConfigCategory',
    'ItemConfigCategory',
    'MapConfigCategory',
    'MapTransferRuleConfigCategory',
    'MapUnitConfigCategory',
    'NumericTypeConfigCategory',
    'QuestConfigCategory',
    'QuestObjectiveConfigCategory',
    'TextConfigCategory',
    'UnitConfigCategory',
    'StartProcessConfigCategory',
    'StartMachineConfigCategory',
    'StartSceneConfigCategory',
    'StartZoneConfigCategory',
    'BuffConfigCategory',
    'SpellConfigCategory'
)

$whitelistPaths = @(
    'Packages/cn.etetet.aspire/DotNet~/Program.cs'
)

$allowInstancePattern = '\[AllowInstance\]\s*[\r\n\s]*public\s+partial\s+class\s+(?<name>' + (($targetCategories -join '|')) + ')\b'
$oldPropertyPatterns = @(
    'public\s+StartMachineConfig\s+StartMachineConfig\b',
    'public\s+StartProcessConfig\s+StartProcessConfig\b',
    'public\s+StartZoneConfig\s+StartZoneConfig\b'
)
$hiddenGlobalPatterns = @(
    'World\.Instance\.GetSingleton<StartMachineConfigCategory>',
    'World\.Instance\.GetSingleton<StartProcessConfigCategory>',
    'World\.Instance\.GetSingleton<StartZoneConfigCategory>'
)
$requiredHelperPatterns = @(
    'GetStartMachineConfig\s*\(\s*this\s+StartProcessConfig\s+\w+\s*,\s*Fiber\s+\w+\s*\)',
    'GetInnerIP\s*\(\s*this\s+StartProcessConfig\s+\w+\s*,\s*Fiber\s+\w+\s*\)',
    'GetOuterIP\s*\(\s*this\s+StartProcessConfig\s+\w+\s*,\s*Fiber\s+\w+\s*\)',
    'GetInnerIPInnerPort\s*\(\s*this\s+StartProcessConfig\s+\w+\s*,\s*Fiber\s+\w+\s*\)',
    'GetAddress\s*\(\s*this\s+StartProcessConfig\s+\w+\s*,\s*Fiber\s+\w+\s*\)',
    'GetStartProcessConfig\s*\(\s*this\s+StartSceneConfig\s+\w+\s*,\s*Fiber\s+\w+\s*\)',
    'GetStartZoneConfig\s*\(\s*this\s+StartSceneConfig\s+\w+\s*,\s*Fiber\s+\w+\s*\)',
    'GetAddress\s*\(\s*this\s+StartSceneConfig\s+\w+\s*,\s*Fiber\s+\w+\s*\)',
    'GetInnerIPOuterPort\s*\(\s*this\s+StartSceneConfig\s+\w+\s*,\s*Fiber\s+\w+\s*\)',
    'GetOuterIPOuterPort\s*\(\s*this\s+StartSceneConfig\s+\w+\s*,\s*Fiber\s+\w+\s*\)'
)

function Normalize-RepoPath([string]$path) {
    $relative = [System.IO.Path]::GetRelativePath($repoRoot, $path)
    return $relative.Replace('\', '/')
}

function Write-CheckResult([string]$name, [System.Collections.ICollection]$items) {
    if ($items.Count -eq 0) {
        Write-Host "[PASS] $name"
        return
    }

    Write-Host "[FAIL] $name ($($items.Count))"
    foreach ($item in $items) {
        Write-Host "  - $item"
    }
}

$allowInstanceHits = New-Object System.Collections.Generic.List[string]
foreach ($file in $packageFiles) {
    $content = Get-Content $file.FullName -Raw
    $matches = [regex]::Matches($content, $allowInstancePattern)
    foreach ($match in $matches) {
        $allowInstanceHits.Add("$(Normalize-RepoPath $file.FullName) :: $($match.Groups['name'].Value)")
    }
}

$instanceHits = New-Object System.Collections.Generic.List[string]
foreach ($category in $targetCategories) {
    $pattern = "\b$category\.Instance\b"
    foreach ($match in Select-String -Path $packageFiles.FullName -Pattern $pattern) {
        $repoPath = Normalize-RepoPath $match.Path
        if ($whitelistPaths -contains $repoPath) {
            continue
        }
        $instanceHits.Add("${repoPath}:$($match.LineNumber) :: $($match.Line.Trim())")
    }
}

$hiddenGlobalHits = New-Object System.Collections.Generic.List[string]
foreach ($pattern in $hiddenGlobalPatterns) {
    foreach ($match in Select-String -Path $packageFiles.FullName -Pattern $pattern) {
        $hiddenGlobalHits.Add("$(Normalize-RepoPath $match.Path):$($match.LineNumber) :: $($match.Line.Trim())")
    }
}

$oldApiHits = New-Object System.Collections.Generic.List[string]
foreach ($pattern in $oldPropertyPatterns) {
    foreach ($match in Select-String -Path $startConfigFiles.FullName -Pattern $pattern) {
        $oldApiHits.Add("$(Normalize-RepoPath $match.Path):$($match.LineNumber) :: $($match.Line.Trim())")
    }
}

$startConfigContent = ($startConfigFiles | ForEach-Object { Get-Content $_.FullName -Raw }) -join [Environment]::NewLine
$missingHelpers = New-Object System.Collections.Generic.List[string]
foreach ($pattern in $requiredHelperPatterns) {
    if (-not [regex]::IsMatch($startConfigContent, $pattern)) {
        $missingHelpers.Add($pattern)
    }
}

Write-Host 'Fiber config singleton migration audit'
Write-Host "Repo root: $repoRoot"
Write-Host ''

Write-CheckResult 'S1 target [AllowInstance] definitions should be zero' $allowInstanceHits
Write-CheckResult 'S2 target .Instance usages outside whitelist should be zero' $instanceHits
Write-CheckResult 'S3 hidden-global startconfig accesses should be zero' $hiddenGlobalHits
Write-CheckResult 'S4 legacy startconfig property definitions should be zero' $oldApiHits
Write-CheckResult 'S4 required explicit Fiber helper signatures should exist' $missingHelpers

$hasFailure =
    $allowInstanceHits.Count -gt 0 -or
    $instanceHits.Count -gt 0 -or
    $hiddenGlobalHits.Count -gt 0 -or
    $oldApiHits.Count -gt 0 -or
    $missingHelpers.Count -gt 0

if ($hasFailure) {
    exit 1
}

exit 0
