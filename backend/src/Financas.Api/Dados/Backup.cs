using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Dados;

/// <summary>
/// Cópias completas e consistentes do banco (VACUUM INTO), feitas com o sistema rodando.
/// A pasta vem da configuração <c>Backup:Pasta</c>; sem pasta, não faz nada.
/// </summary>
public class Backup(AppDbContext db, IConfiguration config, ILogger<Backup> log)
{
    private string? Pasta => config["Backup:Pasta"];

    /// <summary>
    /// Antes de mudar a estrutura de um banco que já tem dados, guarda uma cópia completa.
    /// Se a cópia falhar, o app não sobe: melhor parado do que atualizar sem rede de segurança.
    /// </summary>
    public async Task AntesDeAtualizar()
    {
        if (string.IsNullOrWhiteSpace(Pasta)) return;
        var aplicadas = await db.Database.GetAppliedMigrationsAsync();
        if (!aplicadas.Any()) return; // banco novo, nada a proteger
        var pendentes = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pendentes.Count == 0) return;

        Directory.CreateDirectory(Pasta);
        var arquivo = Path.Combine(Pasta, $"antes-da-atualizacao-{DateTime.Now:yyyy-MM-dd-HHmmss}.db");
        await db.Database.ExecuteSqlAsync($"VACUUM INTO {arquivo}");
        if (!File.Exists(arquivo) || new FileInfo(arquivo).Length == 0)
            throw new InvalidOperationException($"Backup antes da atualização falhou ({arquivo}). Atualização cancelada.");
        log.LogWarning("Backup antes da atualização em {Arquivo}. Mudanças a aplicar: {Pendentes}", arquivo, string.Join(", ", pendentes));
    }

    /// <summary>Uma cópia por dia (<c>financas-AAAA-MM-DD.db</c>), mantendo as N mais recentes (<c>Backup:Manter</c>).</summary>
    public async Task Diario(DateOnly hoje, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Pasta)) return;
        Directory.CreateDirectory(Pasta);

        var arquivo = Path.Combine(Pasta, $"financas-{hoje:yyyy-MM-dd}.db");
        if (File.Exists(arquivo)) return;

        await db.Database.ExecuteSqlAsync($"VACUUM INTO {arquivo}", ct);
        log.LogInformation("Backup criado em {Arquivo}", arquivo);

        var manter = config.GetValue("Backup:Manter", 30);
        foreach (var antigo in Directory.GetFiles(Pasta, "financas-*.db").OrderDescending().Skip(manter))
            File.Delete(antigo);
    }
}
