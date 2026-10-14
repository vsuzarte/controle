#!/usr/bin/env sh
# Roda os testes unitários (backend .NET + frontend Vitest), tudo em Docker.
#   ./testar.sh        -> unitários (rotina)
#   ./testar.sh --e2e  -> também os testes de tela (Playwright), opcionais e mais demorados
set -e
cd "$(dirname "$0")"
echo "== Backend: regras de negócio (.NET) =="
docker run --rm -v "$PWD/backend:/src" -v financas-nuget:/root/.nuget -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Financas.slnx --nologo -v q
echo "== Frontend: lógica das telas e tipos (Vitest + vue-tsc) =="
docker run --rm -v "$PWD/frontend:/app" -v financas-node-modules:/app/node_modules -w /app node:24-alpine \
  sh -c "npm ci --no-audit --no-fund --loglevel=error && npx vitest run && npx vue-tsc --noEmit"
if [ "$1" = "--e2e" ]; then
  echo "== Testes de tela (Playwright) =="
  set +e
  docker compose -f docker-compose.e2e.yml up --build --abort-on-container-exit --exit-code-from testes
  codigo=$?
  docker compose -f docker-compose.e2e.yml down --remove-orphans >/dev/null
  [ $codigo -ne 0 ] && echo "Teste de tela falhou. Relatório: e2e/relatorio/index.html" && exit $codigo
fi
echo "Tudo passou."
