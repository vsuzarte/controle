using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Dados;

/// <summary>
/// Banco SQLite. O mapeamento de cada tabela fica em <c>Mapeamentos/</c>; a trilha de auditoria,
/// em <c>AppDbContext.Auditoria.cs</c>.
/// </summary>
public partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Conta> Contas => Set<Conta>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();
    public DbSet<Regra> Regras => Set<Regra>();
    public DbSet<OrcamentoItem> OrcamentoItens => Set<OrcamentoItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PeriodoSalvo> PeriodosSalvos => Set<PeriodoSalvo>();
    public DbSet<Configuracao> Configuracoes => Set<Configuracao>();
    public DbSet<PagamentoPlanejado> PagamentosPlanejados => Set<PagamentoPlanejado>();
    public DbSet<Compra> Compras => Set<Compra>();
    public DbSet<ItemCompra> ItensCompra => Set<ItemCompra>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Dinheiro gravado como inteiro em centavos: exato e ordenável no SQLite.
        builder.Properties<decimal>().HaveConversion<CentavosConverter>();
        builder.Properties<Enum>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder b) =>
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
