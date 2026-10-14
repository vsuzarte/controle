using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Auditoria;

/// <summary>Histórico de alterações (mais recentes primeiro), de tudo ou de um registro.</summary>
public class AuditoriaService(AppDbContext db)
{
    public Task<List<AuditLog>> Listar(Guid? entidadeId, int? limite)
    {
        var q = db.AuditLogs.AsQueryable();
        if (entidadeId is { } id) q = q.Where(a => a.EntidadeId == id);
        return q.OrderByDescending(a => a.Id).Take(Math.Clamp(limite ?? 100, 1, 500)).ToListAsync();
    }
}
