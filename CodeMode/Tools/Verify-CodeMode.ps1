<#
.SYNOPSIS
  验证 CodeMode 切换结果是否符合预期

.DESCRIPTION
  检查业务包中所有 AssemblyReference.asmref 文件的存在性是否符合指定 CodeMode 的预期：
  - Share 目录：3 种模式都应有 asmref
  - Client 目录：仅在 Client 和 ClientServer 模式应有 asmref
  - Server 目录：仅在 Server 和 ClientServer 模式应有 asmref

  每项检查输出 PASS / FAIL。

.PARAMETER CodeMode
  目标 CodeMode，可选值：Client / Server / ClientServer

.EXAMPLE
  .\Verify-CodeMode.ps1 -CodeMode Client
#>
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Client", "Server", "ClientServer")]
    [string]$CodeMode
)

$ErrorActionPreference = "Stop"

# 兼容 PowerShell 5.1 (powershell.exe) 和 PowerShell 7+ (pwsh.exe)
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

$ModelDirs = @("Model", "Hotfix", "ModelView", "HotfixView")
$ServerDirs = @("Share", "Client", "Server")

# CodeMode 期望规则
$CodeModeExpectations = @{
    "Client"       = @{ "Share" = $true;  "Client" = $true;  "Server" = $false }
    "Server"       = @{ "Share" = $true;  "Client" = $false; "Server" = $true  }
    "ClientServer" = @{ "Share" = $true;  "Client" = $true;  "Server" = $true  }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Verifying CodeMode: $CodeMode" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$expectations = $CodeModeExpectations[$CodeMode]
$passCount = 0
$failCount = 0

foreach ($modelDir in $ModelDirs) {
    foreach ($serverDir in $ServerDirs) {
        $targetDir = Join-Path $PackageRoot "$modelDir\$serverDir"
        $asmrefPath = Join-Path $targetDir "AssemblyReference.asmref"
        $actualExists = Test-Path $asmrefPath
        $expectedExists = $expectations[$serverDir]

        $relativePath = "$modelDir\$serverDir\AssemblyReference.asmref"

        if ($actualExists -eq $expectedExists) {
            $status = "PASS"
            $color = "Green"
            $passCount++
        } else {
            $status = "FAIL"
            $color = "Red"
            $failCount++
        }

        $expectedStr = if ($expectedExists) { "should exist" } else { "should NOT exist" }
        $actualStr = if ($actualExists) { "exists" } else { "not exists" }
        Write-Host "  [$status] $relativePath" -ForegroundColor $color
        Write-Host "         Expected: $expectedStr, Actual: $actualStr" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Result: $passCount PASS, $failCount FAIL" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Red" })
Write-Host "========================================" -ForegroundColor Cyan

if ($failCount -gt 0) {
    exit 1
}
