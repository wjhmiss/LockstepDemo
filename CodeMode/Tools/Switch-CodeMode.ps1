<#
.SYNOPSIS
  演示 ET All-in-One + CodeMode 架构的切换脚本

.DESCRIPTION
  模拟 ET 框架的 CodeModeChangeHelper.cs 行为：
  1. 删除业务包所有目录下的 AssemblyReference.asmref 文件
  2. 根据 CodeMode 在指定目录下生成新的 AssemblyReference.asmref

.PARAMETER CodeMode
  目标 CodeMode，可选值：Client / Server / ClientServer

.EXAMPLE
  .\Switch-CodeMode.ps1 -CodeMode Client
  .\Switch-CodeMode.ps1 -CodeMode Server
  .\Switch-CodeMode.ps1 -CodeMode ClientServer
#>
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Client", "Server", "ClientServer")]
    [string]$CodeMode
)

$ErrorActionPreference = "Stop"

# 项目根目录（脚本的上一级）
# 同时兼容 PowerShell 5.1 (powershell.exe) 和 PowerShell 7+ (pwsh.exe)
if ($PSScriptRoot) {
    $ToolsDir = $PSScriptRoot
} elseif ($MyInvocation.MyCommand.Path) {
    $ToolsDir = Split-Path -Parent $MyInvocation.MyCommand.Path
} else {
    $ToolsDir = (Get-Location).Path
}
$ProjectRoot = Split-Path -Parent $ToolsDir

# 业务包路径（分两步 Join-Path 以兼容 PS 5.1）
$PackageRoot = Join-Path $ProjectRoot "Packages"
$PackageRoot = Join-Path $PackageRoot "cn.codemode.helloworld\Scripts"

if (-not (Test-Path $PackageRoot)) {
    Write-Error "PackageRoot not found: $PackageRoot"
    exit 1
}

# ModelDir 映射到 asmdef 名称
$ModelDirToAsmdef = @{
    "Model"      = "ET.Model"
    "Hotfix"     = "ET.Hotfix"
    "ModelView"  = "ET.ModelView"
    "HotfixView" = "ET.HotfixView"
}

# CodeMode 规则表：哪些 ServerDir 需要生成 asmref
$CodeModeRules = @{
    "Client"       = @("Share", "Client")
    "Server"       = @("Share", "Server")
    "ClientServer" = @("Share", "Client", "Server")
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Switching to CodeMode: $CodeMode" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Project Root: $ProjectRoot"
Write-Host "Package Root: $PackageRoot"
Write-Host ""

# 步骤 1: 删除所有现有的 AssemblyReference.asmref
Write-Host "[Step 1] Deleting all existing AssemblyReference.asmref files..." -ForegroundColor Yellow
$deletedCount = 0
$existingFiles = Get-ChildItem -Path $PackageRoot -Filter "AssemblyReference.asmref" -Recurse -File -ErrorAction SilentlyContinue
foreach ($file in $existingFiles) {
    Write-Host "  [-] Deleted: $($file.FullName.Substring($ProjectRoot.Length + 1))"
    Remove-Item $file.FullName -Force
    $deletedCount++
}
Write-Host "  Total deleted: $deletedCount"
Write-Host ""

# 步骤 2: 根据 CodeMode 生成新的 asmref 文件
Write-Host "[Step 2] Creating new AssemblyReference.asmref files for CodeMode=$CodeMode..." -ForegroundColor Yellow
$allowedServerDirs = $CodeModeRules[$CodeMode]
$createdCount = 0

foreach ($modelDir in $ModelDirToAsmdef.Keys) {
    $asmdefName = $ModelDirToAsmdef[$modelDir]
    foreach ($serverDir in $allowedServerDirs) {
        $targetDir = Join-Path $PackageRoot "$modelDir\$serverDir"
        if (-not (Test-Path $targetDir)) {
            # 目录不存在，跳过（但不报错）
            Write-Host "  [skip] Directory not exists: $modelDir\$serverDir" -ForegroundColor DarkGray
            continue
        }
        $asmrefPath = Join-Path $targetDir "AssemblyReference.asmref"
        $content = "{ `"reference`": `"$asmdefName`" }"
        Set-Content -Path $asmrefPath -Value $content -Encoding UTF8 -NoNewline
        $relativePath = $asmrefPath.Substring($ProjectRoot.Length + 1)
        Write-Host "  [+] Created: $relativePath" -ForegroundColor Green
        Write-Host "      Content: $content" -ForegroundColor DarkGray
        $createdCount++
    }
}
Write-Host ""
Write-Host "  Total created: $createdCount"
Write-Host ""

# 步骤 3: 输出当前状态摘要
Write-Host "[Step 3] Summary..." -ForegroundColor Yellow
Write-Host "  CodeMode: $CodeMode"
Write-Host "  Allowed ServerDirs: $($allowedServerDirs -join ', ')"
Write-Host "  Deleted asmref: $deletedCount"
Write-Host "  Created asmref: $createdCount"
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  CodeMode switch completed!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
