namespace Financas.Api.Dominio;

/// <summary>Marca entidades cujas alterações vão para a trilha de auditoria e que nunca podem ser excluídas.</summary>
public interface IAuditavel
{
    Guid Id { get; }
}
