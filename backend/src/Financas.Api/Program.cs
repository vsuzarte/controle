using System.Text.Json.Serialization;
using Financas.Api.Dados;
using Financas.Api.Dominio;
using Financas.Api.Endpoints;
using Financas.Api.Servicos;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

// Usado pelo HEALTHCHECK do Docker (a imagem não tem curl).
if (args is ["healthcheck", ..])
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    try { return (await http.GetAsync("http://localhost:8080/health")).IsSuccessStatusCode ? 0 : 1; }
    catch { return 1; }
}

var builder = WebApplication.CreateBuilder(args);

var caminhoBanco = builder.Configuration["Banco:Caminho"] ?? "dados/financas.db";
var pastaDados = Path.GetDirectoryName(Path.GetFullPath(caminhoBanco))!;
Directory.CreateDirectory(pastaDados);

// Chaves do cookie de login junto dos dados: recriar o container não desloga ninguém.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(pastaDados, "chaves")))
    .SetApplicationName("financas");

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={caminhoBanco}"));
builder.Services.AddSingleton<IRelogio, RelogioSistema>();
builder.Services.AddScoped<RegraService>();
builder.Services.AddScoped<LancamentoService>();
builder.Services.AddScoped<CartaoService>();
builder.Services.AddScoped<PlanejamentoService>();
builder.Services.AddScoped<PrevisaoService>();
builder.Services.AddScoped<ResumoService>();
builder.Services.AddScoped<RelatorioService>();
builder.Services.AddHostedService<RotinaDiaria>();

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// JSON malformado também passa pelo tratador de erros abaixo (resposta com mensagem, não 400 vazio).
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "financas";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.SlidingExpiration = true;
        o.ExpireTimeSpan = TimeSpan.FromDays(180);
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await BackupAntesDeAtualizar(db, app.Configuration, app.Logger);
    await db.Database.MigrateAsync();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    await DadosIniciais.Aplicar(db);
}

// Erros de regra de negócio viram 400 com mensagem legível; o resto vira 500 genérico.
app.UseExceptionHandler(e => e.Run(async ctx =>
{
    var erro = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, mensagem) = erro switch
    {
        RegraNegocioException r => (400, r.Message),
        BadHttpRequestException b => (400, "Requisição inválida: " + b.Message),
        DbUpdateException { InnerException: { } inner } when inner.Message.Contains("UNIQUE") => (409, "Registro duplicado."),
        _ => (500, "Erro inesperado. Veja os logs do servidor."),
    };
    if (status == 500) app.Logger.LogError(erro, "Erro não tratado");
    ctx.Response.StatusCode = status;
    await ctx.Response.WriteAsJsonAsync(new { erro = mensagem });
}));

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

Api.Mapear(app);

// SPA: qualquer rota que não seja /api cai no index.html do Vue.
app.MapFallbackToFile("index.html");

await app.RunAsync();
return 0;

// Antes de mudar a estrutura de um banco que já tem dados, guarda uma cópia completa.
// Se a cópia falhar, o app não sobe: melhor parado do que atualizar sem rede de segurança.
static async Task BackupAntesDeAtualizar(AppDbContext db, IConfiguration config, ILogger log)
{
    var pasta = config["Backup:Pasta"];
    if (string.IsNullOrWhiteSpace(pasta)) return;
    var aplicadas = await db.Database.GetAppliedMigrationsAsync();
    if (!aplicadas.Any()) return; // banco novo, nada a proteger
    var pendentes = (await db.Database.GetPendingMigrationsAsync()).ToList();
    if (pendentes.Count == 0) return;

    Directory.CreateDirectory(pasta);
    var arquivo = Path.Combine(pasta, $"antes-da-atualizacao-{DateTime.Now:yyyy-MM-dd-HHmmss}.db");
    await db.Database.ExecuteSqlAsync($"VACUUM INTO {arquivo}");
    if (!File.Exists(arquivo) || new FileInfo(arquivo).Length == 0)
        throw new InvalidOperationException($"Backup antes da atualização falhou ({arquivo}). Atualização cancelada.");
    log.LogWarning("Backup antes da atualização em {Arquivo}. Mudanças a aplicar: {Pendentes}", arquivo, string.Join(", ", pendentes));
}
