dotnet publish -c Release -f netcoreapp2.1 -o $PSScriptRoot\build_output $PSScriptRoot\ProtocolGateway.Host.Kube.csproj
docker build --tag azure-iot-pg:0.1 $PSScriptRoot