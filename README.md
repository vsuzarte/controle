# Finanças — controle financeiro pessoal

Sistema local, para uma pessoa, que roda num único container Docker no PC e é acessível pelo celular na rede de casa.

- **Stack:** Vue 3 + Vuetify (frontend) · ASP.NET Core .NET 10 (API) · SQLite (um arquivo) · Docker.
- **Um processo só:** a API também serve o site. Sem servidor de banco separado.

## Subir

```bash
cp .env.example .env      # defina SENHA (recomendado)
docker compose up -d --build
```

Abra <http://localhost:8080>. Depois de qualquer mudança no código, rode `docker compose up -d --build` de novo.

### Ligar junto com o PC

1. Docker Desktop → *Settings → General → Start Docker Desktop when you sign in*.
2. O container já tem `restart: unless-stopped`: sobe sozinho quando o Docker sobe.

### Acessar pelo celular (mesma rede Wi-Fi)

1. Descubra o IP do PC: `ipconfig` → "Endereço IPv4" (ex.: `192.168.0.15`).
   Vale fixar esse IP no roteador (reserva DHCP) para não mudar.
2. Libere a porta no firewall do Windows (PowerShell **como administrador**, uma vez):
   ```powershell
   New-NetFirewallRule -DisplayName "Financas 8080" -Direction Inbound -Protocol TCP -LocalPort 8080 -Action Allow -Profile Private
   ```
   A rede de casa precisa estar marcada como **Privada** no Windows.
3. No celular: `http://192.168.0.15:8080` → menu do navegador → **Adicionar à tela inicial**.

**Fora de casa?** Instale o [Tailscale](https://tailscale.com) (gratuito) no PC e no celular: acesso de qualquer
lugar por uma rede privada, sem abrir porta no roteador. Com `tailscale serve 8080` ainda ganha HTTPS.
Não exponha a porta diretamente na internet.

## Dados e backup

| Pasta | Conteúdo |
|---|---|
| `dados/financas.db` | O banco inteiro (um arquivo SQLite). |
| `dados/chaves/` | Chaves do cookie de login. |
| `backups/` | Cópia diária automática (`financas-AAAA-MM-DD.db`), mantém as 30 últimas. |

- Para ter backup fora do PC, troque `./backups` no `docker-compose.yml` por uma pasta do OneDrive/Google Drive.
- **Restaurar:** `docker compose down` → copie o backup escolhido para `dados/financas.db` (apague `financas.db-wal`/`-shm` se existirem) → `docker compose up -d`.
- O arquivo pode ser aberto no [DB Browser for SQLite](https://sqlitebrowser.org) para consultas. Valores estão em **centavos** (inteiros).

## Como o sistema pensa

**Tudo é lançamento**, com status `Previsto → Realizado` (ou `Cancelado`).

- **Contas do mês** (salário, aluguel, luz, assinaturas) geram lançamentos *previstos* para os próximos 3 meses.
- **Compras parceladas** geram uma parcela prevista por mês. A diferença de centavos vai na 1ª parcela.
- **Pagar ou receber é confirmar o previsto**: é o mesmo registro, então não existe pagamento duplicado.
  Ao lançar algo novo, o formulário avisa se existe um previsto parecido.
- **Cartão de crédito é uma conta.** Cada compra entra na categoria certa e na fatura do mês.
  Pagar a fatura é uma *transferência* do banco para o cartão, então o gasto não é contado duas vezes.
  Compras a partir do dia do fechamento entram na fatura seguinte.
  Contas do mês no cartão ou em débito automático, com valor fixo, são confirmadas sozinhas na data.
- **Planejamento do mês** tem três blocos que não se sobrepõem:
  1. Receitas.
  2. Contas fixas e parcelas: protegidas, mudam só na origem.
  3. Orçamento de gastos variáveis por categoria, sugerido a partir do mês anterior (ou da média de 3 meses).
     Usa só despesas *não programadas*, para não somar a mesma conta fixa duas vezes.
  Itens removidos ou ajustados nunca voltam sozinhos.
- **Nada é apagado.** Um trigger no banco bloqueia `DELETE`. Erros são *cancelados* (com motivo),
  devoluções são *estornos* vinculados ao original, e toda alteração vai para a auditoria
  (antes/depois, data e motivo) na mesma transação.

**Saldo disponível** = contas (sem cartões), só com o que já foi realizado até hoje.
**Saldo projetado no fim do mês** = saldo disponível + a receber − contas a pagar − faturas a vencer − orçamento variável restante.

## Testes

Rode sempre antes de subir uma mudança. Tudo roda em Docker, sem instalar nada:

```powershell
.\testar.ps1          # regras de negócio (.NET) + telas (Playwright)
.\testar.ps1 -SoE2E   # só as telas
```

- **Regras de negócio** (`backend/tests`): duplicidade, auditoria, bloqueio de exclusão, cartão, estorno, previsão.
- **Telas** (`e2e/testes`, Playwright): sobe uma cópia isolada do sistema (`docker-compose.e2e.yml`) com
  **banco próprio, zerado antes de cada teste**, data fixa em 15/10/2026 e senha `e2e`. Roda no computador
  e num celular simulado (Pixel 7). O banco de verdade nunca é tocado.
- Falhou? O relatório com print, vídeo e passo a passo fica em `e2e/relatorio/index.html`.

Para **olhar o ambiente de testes** com dados de exemplo (http://localhost:8090, senha `e2e`):

```powershell
docker compose -f docker-compose.e2e.yml up -d --build app
node e2e/dados-exemplo.mjs
```

Ao criar uma tela ou regra nova, crie o teste junto em `e2e/testes`, usando os atalhos de `apoio.ts`
(`api.lancar`, `api.contaDoMes`, `digitarValor`, `escolher`, `salvar`…).

## Desenvolvimento

```text
backend/src/Financas.Api    API: Dominio/, Dados/ (EF Core + migrations), Servicos/ (regras), Endpoints/
backend/tests/Financas.Tests Testes das regras de negócio (SQLite em memória com as migrations reais)
frontend/src                 Vue 3 + Vuetify + Chart.js: views/ (telas), components/, estado.ts, api.ts
e2e/                         Testes de tela (Playwright), dados de exemplo e capturas de tela
```

Sem .NET/Node instalados (ou com a instalação quebrada), dá para usar só o Docker:

```bash
# testes
docker run --rm -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Financas.slnx
# nova migration
docker run --rm -v "$PWD/backend:/src" -w /src/src/Financas.Api mcr.microsoft.com/dotnet/sdk:10.0 \
  sh -c "dotnet tool install -g dotnet-ef && ~/.dotnet/tools/dotnet-ef migrations add NomeDaMudanca -o Dados/Migrations"
```

Com o SDK local funcionando: `dotnet run --project backend/src/Financas.Api` (porta 5000/8080) e
`npm run dev` em `frontend/` (Vite com proxy para `/api` em `localhost:8080`).

As migrations rodam sozinhas quando a API sobe.
