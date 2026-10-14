using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financas.Api.Dados.Mapeamentos;

public class AuditLogMapeamento : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> e)
    {
        e.HasIndex(x => new { x.Entidade, x.EntidadeId });
        e.HasIndex(x => x.DataHora);
    }
}
