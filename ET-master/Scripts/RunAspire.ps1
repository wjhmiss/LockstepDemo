# ET Aspire 启动脚本
$env:ASPNETCORE_URLS = "https://localhost:17142;http://localhost:15188"
$env:ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL = "https://localhost:21028"
$env:ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL = "https://localhost:22229"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"

dotnet ./Bin/ET.Aspire.dll $args