using System.Text.Json;
using System.Text.Json.Serialization;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Financas.Api.Dados;

/// <summary>
/// Trilha de auditoria: toda criação ou alteração de uma entidade <see cref="IAuditavel"/> gera um registro
/// (antes/depois, operação e motivo) na mesma transação. Excluir registros financeiros é proibido.
/// </summary>
public partial class AppDbContext
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Operação e motivo da próxima gravação, registrados na auditoria.</summary>
    private string? _operacao;
    private string? _motivo;

    public void DefinirAuditoria(string operacao, string? motivo = null)
    {
        _operacao = operacao;
        _motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepararAuditoria();
        try { return base.SaveChanges(acceptAllChangesOnSuccess); }
        finally { _operacao = null; _motivo = null; }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        PrepararAuditoria();
        try { return await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct); }
        finally { _operacao = null; _motivo = null; }
    }

    /// <summary>
    /// Gera os registros de auditoria das entidades alteradas. Como entram no mesmo SaveChanges,
    /// são gravados na mesma transação que a alteração financeira.
    /// </summary>
    private void PrepararAuditoria()
    {
        ChangeTracker.DetectChanges();
        var agora = DateTime.UtcNow;
        var logs = new List<AuditLog>();

        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditLog && entry.State is EntityState.Modified or EntityState.Deleted)
                throw new RegraNegocioException("Registros de auditoria não podem ser alterados nem excluídos.");

            if (entry.Entity is not IAuditavel auditavel) continue;

            switch (entry.State)
            {
                case EntityState.Deleted:
                    throw new RegraNegocioException("Registros financeiros não podem ser excluídos. Use cancelar ou desativar.");

                case EntityState.Added:
                    logs.Add(new AuditLog
                    {
                        Entidade = entry.Metadata.ClrType.Name,
                        EntidadeId = auditavel.Id,
                        Operacao = _operacao ?? "Criação",
                        Depois = Serializar(entry, atual: true),
                        Motivo = _motivo,
                        DataHora = agora,
                    });
                    break;

                case EntityState.Modified:
                    if (entry.Entity is Lancamento l) l.AtualizadoEm = agora;
                    var alteradas = entry.Properties
                        .Where(p => p.IsModified && p.Metadata.Name != nameof(Lancamento.AtualizadoEm)
                                    && !Equals(p.OriginalValue, p.CurrentValue))
                        .ToList();
                    if (alteradas.Count == 0) break;
                    logs.Add(new AuditLog
                    {
                        Entidade = entry.Metadata.ClrType.Name,
                        EntidadeId = auditavel.Id,
                        Operacao = _operacao ?? "Alteração",
                        Antes = JsonSerializer.Serialize(alteradas.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue), Json),
                        Depois = JsonSerializer.Serialize(alteradas.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue), Json),
                        Motivo = _motivo,
                        DataHora = agora,
                    });
                    break;
            }
        }

        AuditLogs.AddRange(logs);
    }

    private static string Serializar(EntityEntry entry, bool atual) =>
        JsonSerializer.Serialize(entry.Properties.ToDictionary(
            p => p.Metadata.Name,
            p => atual ? p.CurrentValue : p.OriginalValue), Json);
}
