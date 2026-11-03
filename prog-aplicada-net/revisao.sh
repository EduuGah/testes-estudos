#!/usr/bin/env bash
# Roteiro 8 – revisão automatizada: falha cedo e explica em qual etapa parou.
# Uso: ./revisao.sh   (o mesmo script roda localmente e no GitHub Actions)
set -euo pipefail
cd "$(dirname "$0")"
etapa() { echo; echo "===== [revisão] $1"; }

etapa "1/4 restore"
dotnet restore HubPedidos.sln

etapa "2/4 build Release com analisadores (regras do .editorconfig com severidade error bloqueiam)"
dotnet build HubPedidos.sln -c Release --no-restore

etapa "3/4 formatação (dotnet format --verify-no-changes)"
dotnet format HubPedidos.sln --verify-no-changes --no-restore

etapa "4/4 testes"
mkdir -p resultados-testes
dotnet test HubPedidos.sln -c Release --no-build --logger "trx;LogFileName=resultado.trx" --results-directory resultados-testes

echo; echo "===== [revisão] APROVADA"
