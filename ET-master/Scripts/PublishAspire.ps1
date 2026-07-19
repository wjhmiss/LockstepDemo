# ET Aspire 启动脚本
$env:StartConfig = "Release"
$env:SceneName = "WOW"

$workDir = $(Get-Location)

dotnet run --project Packages/cn.etetet.aspire/DotNet~/ET.Aspire.csproj --publisher manifest --output-path $workDir/aspire-manifest.json