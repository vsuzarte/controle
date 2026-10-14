using System.Security.Claims;
using Financas.Api.Dados;
using Financas.Api.Dominio;
using Financas.Api.Servicos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Endpoints;

public static class Api
{
    public record LoginDto(string Senha);

    public static void Mapear(WebApplication app)
    {
        var senha = app.Configuration["Senha"];
        var exigeLogin = !string.IsNullOrWhiteSpace(senha);

        app.MapGet("/health", async (AppDbContext db) =>
            await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503));

        var auth = app.MapGroup("/api/auth");
        // "hoje" vem do servidor: o navegador usa o mesmo dia do backend (fuso e data fixa dos testes).
        auth.MapGet("/status", (HttpContext ctx, IRelogio relogio) =>
            new { exigeLogin, autenticado = !exigeLogin || ctx.User.Identity?.IsAuthenticated == true, hoje = relogio.Hoje });

        // Só no ambiente E2E: recria o banco do zero para cada teste começar limpo.
        if (app.Configuration.GetValue<bool>("E2E"))
        {
            app.MapPost("/api/e2e/reset", async (AppDbContext db) =>
            {
                await db.Database.EnsureDeletedAsync();
                await db.Database.MigrateAsync();
                await DadosIniciais.Aplicar(db);
                return Results.Ok();
            });
        }
        auth.MapPost("/login", async (LoginDto dto, HttpContext ctx) =>
        {
            if (!exigeLogin) return Results.Ok();
            if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(dto.Senha ?? ""), System.Text.Encoding.UTF8.GetBytes(senha!)))
            {
                await Task.Delay(800);
                return Results.Json(new { erro = "Senha incorreta." }, statusCode: 401);
            }
            var identidade = new ClaimsIdentity([new Claim(ClaimTypes.Name, "dono")], CookieAuthenticationDefaults.AuthenticationScheme);
            await ctx.SignInAsync(new ClaimsPrincipal(identidade),
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(180) });
            return Results.Ok();
        });
        auth.MapPost("/logout", async (HttpContext ctx) => { await ctx.SignOutAsync(); return Results.Ok(); });

        var api = app.MapGroup("/api");
        if (exigeLogin) api.RequireAuthorization();

        MapearCadastros(api);
        MapearLancamentos(api);
        MapearRegras(api);
        MapearCartoes(api);
        MapearPlanejamento(api);

        api.MapGet("/resumo", (ResumoService s) => s.Obter());
        // Previsão do ciclo de salário que contém a data (padrão: hoje).
        api.MapGet("/previsao", (PrevisaoService s, DateOnly? data, DateOnly? de, DateOnly? ate) => s.Obter(data, de, ate));
        MapearPeriodosSalvos(api);
        api.MapGet("/previsao/config", (PrevisaoService s) => s.VerConfig());
        api.MapPut("/previsao/config", (ConfigPrevisaoDto dto, PrevisaoService s) => s.SalvarConfig(dto));
        api.MapGet("/relatorios/comparativo", (RelatorioService s, IRelogio relogio, string? mes, int? meses) =>
            s.Comparativo(string.IsNullOrEmpty(mes) ? relogio.Hoje : Calendario.ParseMes(mes), meses ?? 6));

        api.MapGet("/auditoria", async (AppDbContext db, Guid? entidadeId, int? limite) =>
        {
            var q = db.AuditLogs.AsQueryable();
            if (entidadeId is { } id) q = q.Where(a => a.EntidadeId == id);
            return await q.OrderByDescending(a => a.Id).Take(Math.Clamp(limite ?? 100, 1, 500)).ToListAsync();
        });
    }

    private static void MapearCadastros(RouteGroupBuilder api)
    {
        api.MapGet("/contas", async (AppDbContext db, ResumoService resumo, CartaoService cartoes, IRelogio relogio) =>
        {
            var saldos = await resumo.Saldos(relogio.Hoje);
            var limitesUsados = await cartoes.LimitesUsados();
            var contas = await db.Contas.OrderBy(c => c.Tipo).ThenBy(c => c.Nome).ToListAsync();
            return contas.Select(c => new
            {
                c.Id, c.Nome, c.Tipo, c.SaldoInicial, c.Cor, c.DiaFechamento, c.DiaVencimento, c.Ativa, c.Limite,
                Saldo = c.SaldoInicial + saldos.GetValueOrDefault(c.Id),
                LimiteUsado = c.EhCartao ? limitesUsados.GetValueOrDefault(c.Id) : (decimal?)null,
            });
        });
        // Bancos mudam o limite com frequência: ajuste rápido, com motivo, registrado na auditoria.
        api.MapPut("/contas/{id:guid}/limite", async (Guid id, LimiteDto dto, AppDbContext db) =>
        {
            var c = await db.Contas.FindAsync(id) ?? throw new RegraNegocioException("Cartão não encontrado.");
            if (!c.EhCartao) throw new RegraNegocioException("Limite só existe para cartão de crédito.");
            c.Limite = Validar.Limite(dto.Limite);
            db.DefinirAuditoria("Ajuste de limite", dto.Motivo);
            await db.SaveChangesAsync();
            return new { c.Id, c.Limite };
        });
        api.MapPost("/contas", async (ContaDto dto, AppDbContext db) =>
        {
            var c = new Conta();
            AplicarConta(c, dto);
            db.Contas.Add(c);
            await db.SaveChangesAsync();
            return c;
        });
        api.MapPut("/contas/{id:guid}", async (Guid id, ContaDto dto, AppDbContext db) =>
        {
            var c = await db.Contas.FindAsync(id) ?? throw new RegraNegocioException("Conta não encontrada.");
            if (c.Tipo != dto.Tipo && await db.Lancamentos.AnyAsync(l => l.ContaId == id || l.ContaDestinoId == id))
                throw new RegraNegocioException("Não é possível mudar o tipo de uma conta com lançamentos.");
            AplicarConta(c, dto);
            await db.SaveChangesAsync();
            return c;
        });

        api.MapGet("/categorias", (AppDbContext db) =>
            db.Categorias.OrderBy(c => c.Tipo).ThenBy(c => c.Grupo).ThenBy(c => c.Nome).ToListAsync());
        api.MapPost("/categorias", async (CategoriaDto dto, AppDbContext db) =>
        {
            var c = new Categoria();
            await AplicarCategoria(c, dto, db);
            db.Categorias.Add(c);
            await db.SaveChangesAsync();
            return c;
        });
        api.MapPut("/categorias/{id:guid}", async (Guid id, CategoriaDto dto, AppDbContext db) =>
        {
            var c = await db.Categorias.FindAsync(id) ?? throw new RegraNegocioException("Categoria não encontrada.");
            if (c.Tipo != dto.Tipo && await db.Lancamentos.AnyAsync(l => l.CategoriaId == id))
                throw new RegraNegocioException("Não é possível mudar o tipo de uma categoria já usada.");
            await AplicarCategoria(c, dto, db);
            await db.SaveChangesAsync();
            return c;
        });
    }

    private static void AplicarConta(Conta c, ContaDto dto)
    {
        c.Nome = Validar.Texto(dto.Nome, "Nome");
        c.Tipo = dto.Tipo;
        c.SaldoInicial = decimal.Round(dto.SaldoInicial, 2);
        c.Cor = Validar.Opcional(dto.Cor);
        c.Ativa = dto.Ativa;
        if (dto.Tipo == TipoConta.CartaoCredito)
        {
            if (dto.DiaFechamento is not (>= 1 and <= 31) || dto.DiaVencimento is not (>= 1 and <= 31))
                throw new RegraNegocioException("Informe os dias de fechamento e vencimento do cartão (1 a 31).");
            c.DiaFechamento = dto.DiaFechamento;
            c.DiaVencimento = dto.DiaVencimento;
            c.Limite = Validar.Limite(dto.Limite);
        }
        else
        {
            c.DiaFechamento = null;
            c.DiaVencimento = null;
            c.Limite = null;
        }
    }

    private static async Task AplicarCategoria(Categoria c, CategoriaDto dto, AppDbContext db)
    {
        var nome = Validar.Texto(dto.Nome, "Nome");
        if (await db.Categorias.AnyAsync(x => x.Id != c.Id && x.Nome == nome && x.Tipo == dto.Tipo))
            throw new RegraNegocioException($"Já existe a categoria '{nome}'.");
        c.Nome = nome;
        c.Tipo = dto.Tipo;
        c.Grupo = Validar.Opcional(dto.Grupo);
        c.Cor = Validar.Opcional(dto.Cor);
        c.Icone = Validar.Opcional(dto.Icone);
        c.Ativa = dto.Ativa;
    }

    private static void MapearLancamentos(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/lancamentos");

        g.MapGet("/", async (AppDbContext db, RegraService regras, string? mes, Guid? contaId, Guid? categoriaId,
            TipoLancamento? tipo, StatusLancamento? status, string? busca) =>
        {
            var q = db.Lancamentos.Include(l => l.Conta).Include(l => l.ContaDestino)
                .Include(l => l.Categoria).Include(l => l.Regra).AsQueryable();
            if (!string.IsNullOrEmpty(mes))
            {
                var inicio = Calendario.ParseMes(mes);
                var fim = Calendario.FimMes(inicio);
                await regras.Sincronizar(fim);
                q = q.Where(l => l.Data >= inicio && l.Data <= fim);
            }
            if (contaId is { } c) q = q.Where(l => l.ContaId == c || l.ContaDestinoId == c);
            if (categoriaId is { } cat) q = q.Where(l => l.CategoriaId == cat);
            if (tipo is { } t) q = q.Where(l => l.Tipo == t);
            if (status is { } s) q = q.Where(l => l.Status == s);
            if (!string.IsNullOrWhiteSpace(busca)) q = q.Where(l => EF.Functions.Like(l.Descricao, $"%{busca.Trim()}%"));
            var lista = await q.OrderByDescending(l => l.Data).ThenByDescending(l => l.CriadoEm).Take(2000).ToListAsync();
            return lista.Select(LancamentoView.De);
        });

        // Previstos próximos da mesma categoria: usado pelo formulário para sugerir
        // "confirmar o previsto" em vez de criar um lançamento duplicado.
        g.MapGet("/pendentes", async (AppDbContext db, IRelogio relogio, Guid categoriaId) =>
        {
            var de = relogio.Hoje.AddDays(-45);
            var ate = relogio.Hoje.AddDays(20);
            var lista = await db.Lancamentos.Include(l => l.Conta).Include(l => l.Categoria).Include(l => l.Regra)
                .Where(l => l.CategoriaId == categoriaId && l.Status == StatusLancamento.Previsto && l.Data >= de && l.Data <= ate)
                .OrderBy(l => l.Data).ToListAsync();
            return lista.Select(LancamentoView.De);
        });

        g.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var l = await db.Lancamentos.Include(x => x.Conta).Include(x => x.ContaDestino)
                .Include(x => x.Categoria).Include(x => x.Regra).SingleOrDefaultAsync(x => x.Id == id)
                ?? throw new RegraNegocioException("Lançamento não encontrado.");
            return LancamentoView.De(l);
        });

        g.MapPost("/", async (NovoLancamentoDto dto, LancamentoService s) => (await s.Criar(dto)).Select(l => l.Id));
        g.MapPut("/{id:guid}", async (Guid id, EditarLancamentoDto dto, LancamentoService s) => (await s.Editar(id, dto)).Id);
        g.MapPost("/{id:guid}/confirmar", async (Guid id, ConfirmarDto dto, LancamentoService s) => (await s.Confirmar(id, dto)).Id);
        g.MapPost("/{id:guid}/reabrir", async (Guid id, MotivoDto dto, LancamentoService s) => (await s.Reabrir(id, dto.Motivo)).Id);
        g.MapPost("/{id:guid}/cancelar", async (Guid id, MotivoDto dto, LancamentoService s) => (await s.Cancelar(id, dto.Motivo)).Id);
        g.MapPost("/{id:guid}/estornar", async (Guid id, EstornoDto dto, LancamentoService s) => (await s.Estornar(id, dto)).Id);
    }

    private static void MapearRegras(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/regras");
        g.MapGet("/", async (AppDbContext db, IRelogio relogio) =>
        {
            var regras = await db.Regras.Include(r => r.Categoria).Include(r => r.Conta)
                .OrderByDescending(r => r.Ativa).ThenBy(r => r.Modo).ThenBy(r => r.Descricao).ToListAsync();
            var hoje = relogio.Hoje;
            var lancs = await db.Lancamentos.Where(l => l.RegraId != null)
                .Select(l => new { l.RegraId, l.Status, l.Data, l.Valor }).ToListAsync();
            return regras.Select(r =>
            {
                var meus = lancs.Where(l => l.RegraId == r.Id).ToList();
                var proximo = meus.Where(l => l.Status == StatusLancamento.Previsto).OrderBy(l => l.Data).FirstOrDefault();
                return new
                {
                    r.Id, r.Modo, r.Tipo, r.Descricao, r.CategoriaId, Categoria = r.Categoria!.Nome, CategoriaCor = r.Categoria.Cor,
                    r.ContaId, Conta = r.Conta!.Nome, r.FormaPagamento, r.Valor, r.ValorVariavel, r.Parcelas,
                    r.Frequencia, r.Intervalo, r.DataInicio, r.DataFim, r.Ativa, r.Observacao,
                    Vencimento = r.Vencimento ?? RegraVencimento.DiaFixo, r.DiaUtilNumero,
                    IniciaPeriodo = r.Tipo == TipoLancamento.Receita && (r.IniciaPeriodo ?? PrevisaoService.EhSalario(r.Categoria.Nome)),
                    Realizadas = meus.Count(l => l.Status == StatusLancamento.Realizado),
                    Pendentes = meus.Count(l => l.Status == StatusLancamento.Previsto),
                    ProximaData = proximo?.Data,
                    ProximoValor = proximo?.Valor,
                    Atrasadas = meus.Count(l => l.Status == StatusLancamento.Previsto && l.Data < hoje),
                };
            });
        });
        g.MapPost("/", async (RegraDto dto, RegraService s) => (await s.Criar(dto)).Id);
        g.MapPut("/{id:guid}", async (Guid id, EditarRegraDto dto, RegraService s) => (await s.Editar(id, dto)).Id);
        g.MapPost("/{id:guid}/desativar", async (Guid id, DesativarRegraDto dto, RegraService s) => (await s.Desativar(id, dto)).Id);
        g.MapPost("/{id:guid}/reativar", async (Guid id, RegraService s) => (await s.Reativar(id)).Id);
    }

    /// <summary>Intervalos de datas salvos para a previsão. São preferência de tela: podem ser editados e excluídos.</summary>
    private static void MapearPeriodosSalvos(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/previsao/periodos");
        g.MapGet("/", (AppDbContext db) => db.PeriodosSalvos.OrderBy(p => p.De).ThenBy(p => p.Nome).ToListAsync());
        g.MapPost("/", async (PeriodoSalvoDto dto, AppDbContext db) =>
        {
            var p = new PeriodoSalvo();
            AplicarPeriodo(p, dto);
            db.PeriodosSalvos.Add(p);
            await db.SaveChangesAsync();
            return p;
        });
        g.MapPut("/{id:guid}", async (Guid id, PeriodoSalvoDto dto, AppDbContext db) =>
        {
            var p = await db.PeriodosSalvos.FindAsync(id) ?? throw new RegraNegocioException("Período não encontrado.");
            AplicarPeriodo(p, dto);
            await db.SaveChangesAsync();
            return p;
        });
        g.MapDelete("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var p = await db.PeriodosSalvos.FindAsync(id) ?? throw new RegraNegocioException("Período não encontrado.");
            db.PeriodosSalvos.Remove(p);
            await db.SaveChangesAsync();
            return Results.Ok();
        });
    }

    private static void AplicarPeriodo(PeriodoSalvo p, PeriodoSalvoDto dto)
    {
        if (dto.Ate < dto.De) throw new RegraNegocioException("A data final do período deve ser depois da inicial.");
        if (dto.Ate > dto.De.AddYears(1)) throw new RegraNegocioException("Escolha um período de até um ano.");
        p.Nome = Validar.Texto(dto.Nome, "Nome");
        p.De = dto.De;
        p.Ate = dto.Ate;
    }

    private static void MapearCartoes(RouteGroupBuilder api)
    {
        api.MapGet("/cartoes/todos", async (CartaoService s, RegraService regras) =>
        {
            await regras.Sincronizar();
            return await s.Todos();
        });
        var g = api.MapGroup("/cartoes/{id:guid}/faturas");
        g.MapGet("/", async (Guid id, CartaoService s, RegraService regras) =>
        {
            await regras.Sincronizar();
            return (await s.Faturas(id)).Select(f => f with { Lancamentos = [], Pagamentos = [] });
        });
        g.MapGet("/{mes}", async (Guid id, string mes, CartaoService s, RegraService regras) =>
        {
            var m = Calendario.ParseMes(mes);
            await regras.Sincronizar(Calendario.FimMes(m.AddMonths(1)));
            return await s.Fatura(id, m);
        });
        g.MapPost("/{mes}/pagar", async (Guid id, string mes, PagarFaturaDto dto, CartaoService s) =>
            (await s.Pagar(id, Calendario.ParseMes(mes), dto)).Id);
    }

    private static void MapearPlanejamento(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/planejamento/{mes}");
        g.MapGet("/", (string mes, PlanejamentoService s) => s.Obter(Calendario.ParseMes(mes)));
        g.MapPost("/sugerir", async (string mes, BaseSugestao? @base, PlanejamentoService s) =>
            new { criados = await s.Sugerir(Calendario.ParseMes(mes), @base ?? BaseSugestao.MesAnterior) });
        g.MapPut("/categorias/{categoriaId:guid}", async (string mes, Guid categoriaId, ValorOrcamentoDto dto, PlanejamentoService s) =>
            (await s.DefinirValor(Calendario.ParseMes(mes), categoriaId, dto)).Id);
        g.MapPost("/categorias/{categoriaId:guid}/dispensar", async (string mes, Guid categoriaId, MotivoDto dto, PlanejamentoService s) =>
            (await s.Dispensar(Calendario.ParseMes(mes), categoriaId, dto.Motivo)).Id);
        g.MapPost("/categorias/{categoriaId:guid}/restaurar", async (string mes, Guid categoriaId, PlanejamentoService s) =>
            (await s.Restaurar(Calendario.ParseMes(mes), categoriaId)).Id);
    }
}
