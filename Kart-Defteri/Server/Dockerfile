FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Server/Ledger.Server.csproj Server/
COPY Shared/Models.cs Shared/
RUN dotnet restore Server/Ledger.Server.csproj
COPY Server/Program.cs Server/
RUN dotnet publish Server/Ledger.Server.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out/ .
ENTRYPOINT ["dotnet", "Ledger.Server.dll"]
