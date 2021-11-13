FROM mcr.microsoft.com/dotnet/core/runtime:2.1
WORKDIR /app

COPY build_output/ ./

CMD ["dotnet ProtocolGateway.Host.Console.dll --env"]