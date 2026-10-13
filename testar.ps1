# Roda todos os testes: unitários (.NET) e ponta a ponta (Playwright), tudo em Docker.
#   .\testar.ps1          -> tudo
#   .\testar.ps1 -SoE2E   -> só os testes de tela
param([switch]$SoE2E)

$raiz = $PSScriptRoot

if (-not $SoE2E) {
    Write-Host "`n== Testes de regras de negócio (.NET) ==" -ForegroundColor Cyan
    docker run --rm -v "${raiz}/backend:/src" -v financas-nuget:/root/.nuget -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Financas.slnx --nologo -v q
    if ($LASTEXITCODE -ne 0) { Write-Host "Falhou nos testes .NET" -ForegroundColor Red; exit $LASTEXITCODE }
}

Write-Host "`n== Testes de tela (Playwright) ==" -ForegroundColor Cyan
docker compose -f "$raiz/docker-compose.e2e.yml" up --build --abort-on-container-exit --exit-code-from testes
$codigo = $LASTEXITCODE
docker compose -f "$raiz/docker-compose.e2e.yml" down --remove-orphans | Out-Null

if ($codigo -eq 0) {
    Write-Host "`nTudo passou." -ForegroundColor Green
} else {
    Write-Host "`nAlgum teste falhou. Relatório: e2e/relatorio/index.html (ou: cd e2e; npx playwright show-report relatorio)" -ForegroundColor Red
}
exit $codigo
