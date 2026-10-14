using System.Text.Json;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Financas.Api.Dados;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Conta> Contas => Set<Conta>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();
    public DbSet<Regra> Regras => Set<Regra>();
    public DbSet<OrcamentoItem> OrcamentoItens => Set<OrcamentoItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>Operação e motivo da próxima gravação, registrados na auditoria.</summary>
    private string? _operacao;
    private string? _motivo;

    public void DefinirAuditoria(string operacao, string? motivo = null)
    {
        _operacao = operacao;
        _motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Dinheiro gravado como inteiro em centavos: exato e ordenável no SQLite.
        builder.Properties<decimal>().HaveConversion<CentavosConverter>();
        builder.Properties<Enum>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Conta>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(80);
            e.Ignore(x => x.EhCartao);
            e.Ignore(x => x.EhBeneficio);
            e.Ignore(x => x.EhDisponivel);
        });

        b.Entity<Categoria>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(80);
            e.HasIndex(x => new { x.Nome, x.Tipo }).IsUnique();
        });

        b.Entity<Lancamento>(e =>
        {
            e.Property(x => x.Descricao).HasMaxLength(200);
            e.Ignore(x => x.Valido);
            e.HasOne(x => x.Conta).WithMany().HasForeignKey(x => x.ContaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ContaDestino).WithMany().HasForeignKey(x => x.ContaDestinoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Regra).WithMany().HasForeignKey(x => x.RegraId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Lancamento>().WithMany().HasForeignKey(x => x.EstornoDeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Data);
            e.HasIndex(x => x.FaturaMes);
            // SQLite trata NULLs como distintos: lançamentos avulsos não colidem.
            e.HasIndex(x => new { x.RegraId, x.Sequencia }).IsUnique();
        });

        b.Entity<Regra>(e =>
        {
            e.Property(x => x.Descricao).HasMaxLength(200);
            e.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Conta).WithMany().HasForeignKey(x => x.ContaId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<OrcamentoItem>(e =>
        {
            e.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.Mes, x.CategoriaId }).IsUnique();
        });

        b.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => new { x.Entidade, x.EntidadeId });
            e.HasIndex(x => x.DataHora);
        });
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

    private static readonly JsonSerializerOptions Json = new() { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    private static string Serializar(EntityEntry entry, bool atual) =>
        JsonSerializer.Serialize(entry.Properties.ToDictionary(
            p => p.Metadata.Name,
            p => atual ? p.CurrentValue : p.OriginalValue), Json);
}

public class CentavosConverter() : ValueConverter<decimal, long>(
    v => (long)decimal.Round(v * 100m, 0, MidpointRounding.AwayFromZero),
    v => v / 100m);
