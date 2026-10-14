using System.Text.Json.Serialization;
using Financas.Api.Dados;
using Financas.Api.Dominio;
using Financas.Api.Funcionalidades.Auditoria;
using Financas.Api.Funcionalidades.Cartoes;
using Financas.Api.Funcionalidades.Categorias;
using Financas.Api.Funcionalidades.Contas;
using Financas.Api.Funcionalidades.Lancamentos;
using Financas.Api.Funcionalidades.Planejamento;
using Financas.Api.Funcionalidades.Previsao;
using Financas.Api.Funcionalidades.Regras;
using Financas.Api.Funcionalidades.Relatorios;
using Financas.Api.Funcionalidades.Resumo;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Infraestrutura;

/// <summary>Registro de tudo o que o sistema usa: banco, login, serviços de cada funcionalidade e a rotina diária.</summary>
public static class Servicos
{
    public static WebApplicationBuilder AdicionarFinancas(this WebApplicationBuilder builder)
    {
        var s = builder.Services;
        var caminhoBanco = builder.Configuration["Banco:Caminho"] ?? "dados/financas.db";
        var pastaDados = Path.GetDirectoryName(Path.GetFullPath(caminhoBanco))!;
        Directory.CreateDirectory(pastaDados);

        // Chaves do cookie de login junto dos dados: recriar o container não desloga ninguém.
        s.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(pastaDados, "chaves")))
            .SetApplicationName("financas");

        s.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={caminhoBanco}"));
        s.AddSingleton<IRelogio, RelogioSistema>();
        s.AddScoped<Backup>();

        AdicionarFuncionalidades(s);
        s.AddHostedService<RotinaDiaria>();

        s.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        // JSON malformado também passa pelo tratador de erros (resposta com mensagem, não 400 vazio).
        s.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

        s.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = "financas";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Strict;
                o.SlidingExpiration = true;
                o.ExpireTimeSpan = TimeSpan.FromDays(180);
                o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
            });
        s.AddAuthorization();
        return builder;
    }

    private static void AdicionarFuncionalidades(IServiceCollection s)
    {
        s.AddScoped<SaldoService>();
        s.AddScoped<RecargaDoVale>();
        s.AddScoped<ContaService>();
        s.AddScoped<CategoriaService>();
        s.AddScoped<GeradorDeOcorrencias>();
        s.AddScoped<RegraService>();
        s.AddScoped<RegraConsultas>();
        s.AddScoped<LancamentoService>();
        s.AddScoped<LancamentoConsultas>();
        s.AddScoped<CartaoService>();
        s.AddScoped<PlanejamentoService>();
        s.AddScoped<DiasDePagamento>();
        s.AddScoped<PreferenciasPrevisao>();
        s.AddScoped<PrevisaoService>();
        s.AddScoped<ConfiguracaoPrevisaoService>();
        s.AddScoped<QuadroPrevisaoService>();
        s.AddScoped<PeriodoSalvoService>();
        s.AddScoped<ResumoService>();
        s.AddScoped<RelatorioService>();
        s.AddScoped<AuditoriaService>();
    }
}
