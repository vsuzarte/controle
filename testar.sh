#!/usr/bin/env sh
# Roda todos os testes: unitários (.NET) e ponta a ponta (Playwright), tudo em Docker.
set -e
cd "$(dirname "$0")"
if [ "$1" != "--so-e2e" ]; then
  echo "== Testes de regras de negócio (.NET) =="
  docker run --rm -v "$PWD/backend:/src" -v financas-nuget:/root/.nuget -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Financas.slnx --nologo -v q
fi
echo "== Testes de tela (Playwright) =="
set +e
docker compose -f docker-compose.e2e.yml up --build --abort-on-container-exit --exit-code-from testes
codigo=$?
docker compose -f docker-compose.e2e.yml down --remove-orphans >/dev/null
[ $codigo -eq 0 ] && echo "Tudo passou." || echo "Falhou. Relatório: e2e/relatorio/index.html"
exit $codigo
