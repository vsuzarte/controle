using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financas.Api.Dados.Mapeamentos;

public class ConfiguracaoMapeamento : IEntityTypeConfiguration<Configuracao>
{
    public void Configure(EntityTypeBuilder<Configuracao> e)
    {
        e.HasKey(x => x.Chave);
        e.Property(x => x.Chave).HasMaxLength(80);
        e.Property(x => x.Valor).HasMaxLength(500);
    }
}
