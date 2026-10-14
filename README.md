# Finanças — controle financeiro pessoal

Sistema para uma pessoa controlar o próprio dinheiro: contas do mês, cartões, vale-refeição, previsão até o
próximo salário, planejamento e relatórios. Roda num único container Docker no seu PC e abre no navegador —
no próprio PC ou no celular, na rede de casa.

- **Stack:** Vue 3 + Vuetify (frontend) · ASP.NET Core .NET 10 (API) · SQLite (um arquivo) · Docker.
- **Um processo só:** a API também serve o site. Sem servidor de banco separado.
- **Seus dados ficam no seu PC**, na pasta `dados/`. Nada vai para a internet.
- **Cada instalação é independente:** quem instala começa com um banco vazio, só com categorias e contas de exemplo.

## Instalar (passo a passo)

Só é preciso o **Docker**. Não precisa instalar .NET nem Node: tudo é compilado dentro do Docker.

### 1. Instale o Docker

- **Windows:** [Docker Desktop](https://www.docker.com/products/docker-desktop/). Na instalação, deixe marcado
  *Use WSL 2*. Reinicie o PC se ele pedir e abra o Docker Desktop uma vez, até aparecer *Engine running*.
- **Linux:** Docker Engine + plugin `docker compose`.
- **macOS:** Docker Desktop.

Para conferir, abra um terminal (no Windows, o PowerShell) e rode `docker compose version`.

### 2. Pegue o código

```bash
git clone https://github.com/vsuzarte/controle.git
cd controle
```

Sem git: no GitHub, **Code → Download ZIP**, descompacte e abra um terminal dentro da pasta `controle`.

> O repositório pode ser privado: nesse caso, peça acesso ao dono (ele adiciona você como colaborador) ou
> peça o ZIP. O ZIP do GitHub e o `git clone` nunca trazem dados de ninguém: `dados/` e `backups/` não vão para o git.

### 3. Defina a senha

Copie o arquivo de exemplo e edite o `.env` (Bloco de Notas serve):

```bash
cp .env.example .env
```

```ini
SENHA=uma-senha-sua
PORTA=8080
```

- **Use senha.** O sistema fica acessível para qualquer aparelho da sua rede (é assim que o celular entra).
  Com `SENHA` vazia, ninguém precisa de senha para ver suas finanças.
- **PORTA:** troque (por exemplo, `8081`) se a 8080 já estiver em uso no seu PC.
- O `.env` fica só no seu PC (não vai para o git).

### 4. Suba o sistema

```bash
docker compose up -d --build
```

A primeira vez demora alguns minutos (baixa as imagens e compila). Depois, abra <http://localhost:8080>
(ou a porta que você escolheu) e entre com a senha. O banco é criado sozinho, vazio.

### 5. Primeiros cadastros

1. **Cadastros → Contas:** seu banco, carteira, cartões de crédito (com dia de fechamento, vencimento e limite)
   e vale-refeição/alimentação, cada um com o saldo de hoje. Os exemplos que não usar podem ser desativados (nada é apagado no sistema).
2. **Contas do mês:** salário (com o dia em que cai, por exemplo *5º dia útil*), aluguel, luz, escola,
   assinaturas... O salário marcado como *dia de pagamento* é o que divide a **Previsão** em períodos.
3. Use **Lançar** (ou a tecla **N**) para os gastos do dia a dia.

## Uso no dia a dia

### Ligar junto com o PC

1. Docker Desktop → *Settings → General → Start Docker Desktop when you sign in*.
2. O container já tem `restart: unless-stopped`: sobe sozinho quando o Docker sobe.

O Docker Desktop roda dentro do usuário do Windows que o abriu. Outro usuário do mesmo PC também acessa
<http://localhost:8080> enquanto esse usuário estiver logado (use *Trocar usuário*, não *Sair*).
Não rode dois containers com a mesma pasta `dados/`: um banco, um container.

### Acessar pelo celular (mesma rede Wi-Fi)

1. Descubra o IP do PC: `ipconfig` → "Endereço IPv4" (ex.: `192.168.0.15`).
   Vale fixar esse IP no roteador (reserva DHCP) para não mudar.
2. Libere a porta no firewall do Windows (PowerShell **como administrador**, uma vez; troque 8080 se mudou a porta):
   ```powershell
   New-NetFirewallRule -DisplayName "Financas 8080" -Direction Inbound -Protocol TCP -LocalPort 8080 -Action Allow -Profile Private
   ```
   A rede de casa precisa estar marcada como **Privada** no Windows.
3. No celular: `http://192.168.0.15:8080` → menu do navegador → **Adicionar à tela inicial**.

**Fora de casa?** Instale o [Tailscale](https://tailscale.com) (gratuito) no PC e no celular: acesso de qualquer
lugar por uma rede privada, sem abrir porta no roteador. Com `tailscale serve 8080` ainda ganha HTTPS.
Não exponha a porta diretamente na internet.

### Atualizar para uma versão nova

```bash
git pull
docker compose up -d --build
```

Seus dados continuam em `dados/`. Se a versão nova mudar a estrutura do banco, o app grava antes uma cópia
completa em `backups/antes-da-atualizacao-<data>.db`; se a cópia falhar, ele não sobe (nada é alterado).
As mudanças de estrutura só acrescentam colunas — nada é removido nem renomeado.

## Dados e backup

| Pasta | Conteúdo |
|---|---|
| `dados/financas.db` | O banco inteiro (um arquivo SQLite). |
| `dados/chaves/` | Chaves do cookie de login. |
| `backups/` | Cópia diária automática (`financas-AAAA-MM-DD.db`), mantém as 30 últimas. |

- Para ter backup fora do PC, troque `./backups` no `docker-compose.yml` por uma pasta do OneDrive/Google Drive.
- **Restaurar:** `docker compose down` → copie o backup escolhido para `dados/financas.db` (apague `financas.db-wal`/`-shm` se existirem) → `docker compose up -d`.
- O arquivo pode ser aberto no [DB Browser for SQLite](https://sqlitebrowser.org) para consultas. Valores estão em **centavos** (inteiros).

### Levar para outro PC (com os seus dados)

1. No PC antigo: `docker compose stop` (garante que o banco está todo gravado no arquivo).
2. Copie a pasta `dados/` inteira (e `backups/`, se quiser o histórico de cópias).
3. No PC novo: siga a instalação acima, mas **antes do passo 4** coloque a pasta `dados/` copiada dentro de `controle/`.
4. `docker compose up -d --build` e confira se está tudo lá.
5. Não deixe os dois rodando: os dados se separariam em duas versões diferentes.

**Para dar o sistema a outra pessoa**, mande o código (clone ou ZIP), **nunca** a sua pasta `controle` copiada
inteira: ela tem `dados/`, `backups/` e o `.env` com a sua senha.

### Desinstalar

`docker compose down` e apague a pasta `controle` (isso apaga os dados também — guarde um backup antes, se quiser).

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

Rode antes de subir uma mudança. Tudo roda em Docker, sem instalar nada:

```powershell
.\testar.ps1        # unitários: regras de negócio (.NET) + lógica das telas (Vitest) + checagem de tipos
.\testar.ps1 -E2E   # também os testes de tela (Playwright) — opcionais, mais demorados
```

- **Backend** (`backend/tests`, xUnit): duplicidade, auditoria, bloqueio de exclusão, cartão e limite, estorno,
  previsão por salário, dia útil, juros, vale, relatórios. Usa SQLite em memória com as migrations reais.
- **Frontend** (`frontend/src/*.test.ts`, Vitest): a lógica das telas (vencimentos, juros, vale, limite, formatos).
- **Telas** (`e2e/testes`, Playwright): fora da rotina. Sobem uma cópia isolada do sistema
  (`docker-compose.e2e.yml`) com banco próprio, data fixa em 15/10/2026 e senha `e2e`. Podem estar desatualizados.

Para **demonstrar o sistema** com dados fictícios (http://localhost:8090, senha `e2e`, "hoje" fixo em 15/10/2026):
~6 meses de histórico, dois cartões com limite, vale-refeição, salário + adiantamento, contas do mês, parcelas,
juros, estorno, uma conta atrasada há dois meses, uma conta movida no quadro e um período salvo. Rodar de novo
recomeça do zero. É um banco separado: o sistema de verdade (porta 8080) não é tocado.

```powershell
docker compose -f docker-compose.e2e.yml up -d --build app
node e2e/dados-exemplo.mjs
```

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
