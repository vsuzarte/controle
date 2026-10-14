using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Financas.Api.Dominio;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Financas.Api.Funcionalidades.Autenticacao;

/// <summary>
/// Login com uma senha única (configuração <c>Senha</c>). Sem senha configurada, o sistema fica aberto.
/// O cookie dura 180 dias e é renovado com o uso.
/// </summary>
public static class AutenticacaoEndpoints
{
    public static void MapearAutenticacao(this WebApplication app, string? senha)
    {
        var exigeLogin = !string.IsNullOrWhiteSpace(senha);
        var auth = app.MapGroup("/api/auth");

        // "hoje" vem do servidor: o navegador usa o mesmo dia do backend (fuso e data fixa dos testes).
        auth.MapGet("/status", (HttpContext ctx, IRelogio relogio) =>
            new { exigeLogin, autenticado = !exigeLogin || ctx.User.Identity?.IsAuthenticated == true, hoje = relogio.Hoje });

        auth.MapPost("/login", async (LoginDto dto, HttpContext ctx) =>
        {
            if (!exigeLogin) return Results.Ok();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(dto.Senha ?? ""), Encoding.UTF8.GetBytes(senha!)))
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
    }
}
