param(
    [string]$AppPath = "Release/ET.app"  # .app 路径
)

# 使用 open -n 启动一个新的实例
Start-Process "open" -ArgumentList "-n", $AppPath

Write-Host "已启动一个新实例: $AppPath"

# 示例用法
# pwsh ./Scripts/launch.ps1 -AppPath "Release/ET.app"