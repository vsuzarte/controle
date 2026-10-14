# Roda os testes unitários (backend .NET + frontend Vitest), tudo em Docker.
#   .\testar.ps1        -> unitários (rotina)
#   .\testar.ps1 -E2E   -> também os testes de tela (Playwright), opcionais e mais demorados
param([switch]$E2E)

$raiz = $PSScriptRoot

Write-Host "`n== Backend: regras de negócio (.NET) ==" -ForegroundColor Cyan
docker run --rm -v "${raiz}/backend:/src" -v financas-nuget:/root/.nuget -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Financas.slnx --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "Falhou nos testes do backend" -ForegroundColor Red; exit $LASTEXITCODE }

Write-Host "`n== Frontend: lógica das telas e tipos (Vitest + vue-tsc) ==" -ForegroundColor Cyan
docker run --rm -v "${raiz}/frontend:/app" -v financas-node-modules:/app/node_modules -w /app node:24-alpine sh -c "npm ci --no-audit --no-fund --loglevel=error && npx vitest run && npx vue-tsc --noEmit"
if ($LASTEXITCODE -ne 0) { Write-Host "Falhou nos testes do frontend" -ForegroundColor Red; exit $LASTEXITCODE }

if ($E2E) {
    Write-Host "`n== Testes de tela (Playwright) ==" -ForegroundColor Cyan
    docker compose -f "$raiz/docker-compose.e2e.yml" up --build --abort-on-container-exit --exit-code-from testes
    $codigo = $LASTEXITCODE
    docker compose -f "$raiz/docker-compose.e2e.yml" down --remove-orphans | Out-Null
    if ($codigo -ne 0) {
        Write-Host "`nAlgum teste de tela falhou. Relatório: e2e/relatorio/index.html" -ForegroundColor Red
        exit $codigo
    }
}

Write-Host "`nTudo passou." -ForegroundColor Green
exit 0
