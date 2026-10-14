namespace Financas.Api.Funcionalidades.Compras;

public record CompraResumoView(Guid Id, string Loja, DateTime Emissao, decimal Total, int Itens, string Conta);
