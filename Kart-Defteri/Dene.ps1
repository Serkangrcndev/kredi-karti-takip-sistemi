$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
dotnet run --project kredi_karti_takip.csproj
exit $LASTEXITCODE
