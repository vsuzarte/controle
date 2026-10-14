namespace Financas.Api.Funcionalidades.Resumo;

/// <summary>Vale-refeição/alimentação: saldo, próxima recarga e até quando dura no ritmo dos últimos 30 dias.</summary>
public record BeneficioView(Guid Id, string Nome, string? Cor, decimal Saldo, DateOnly? ProximaRecarga, decimal? ValorRecarga,
    decimal GastoPorDia, DateOnly? DuraAte);
