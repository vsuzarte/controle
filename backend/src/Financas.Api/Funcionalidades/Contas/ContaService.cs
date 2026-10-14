using Financas.Api.Dados;
using Financas.Api.Dominio;
using Financas.Api.Funcionalidades.Cartoes;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Contas;

/// <summary>Cadastro de contas: bancos, carteira, cartões de crédito e vales.</summary>
public class ContaService(AppDbContext db, IRelogio relogio, SaldoService saldos, CartaoService cartoes)
{
    public async Task<List<ContaView>> Listar()
    {
        var movimentos = await saldos.Saldos(relogio.Hoje);
        var limitesUsados = await cartoes.LimitesUsados();
        var contas = await db.Contas.OrderBy(c => c.Tipo).ThenBy(c => c.Nome).ToListAsync();
        return contas.Select(c => new ContaView(
                c.Id, c.Nome, c.Tipo, c.SaldoInicial, c.Cor, c.DiaFechamento, c.DiaVencimento, c.Ativa, c.Limite,
                c.SaldoInicial + movimentos.GetValueOrDefault(c.Id),
                c.EhCartao ? limitesUsados.GetValueOrDefault(c.Id) : null))
            .ToList();
    }

    public async Task<Conta> Criar(ContaDto dto)
    {
        var c = new Conta();
        Aplicar(c, dto);
        db.Contas.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    public async Task<Conta> Editar(Guid id, ContaDto dto)
    {
        var c = await db.Contas.FindAsync(id) ?? throw new RegraNegocioException("Conta não encontrada.");
        if (c.Tipo != dto.Tipo && await db.Lancamentos.AnyAsync(l => l.ContaId == id || l.ContaDestinoId == id))
            throw new RegraNegocioException("Não é possível mudar o tipo de uma conta com lançamentos.");
        Aplicar(c, dto);
        await db.SaveChangesAsync();
        return c;
    }

    /// <summary>Bancos mudam o limite com frequência: ajuste rápido, com motivo, registrado na auditoria.</summary>
    public async Task<Conta> AjustarLimite(Guid id, LimiteDto dto)
    {
        var c = await db.Contas.FindAsync(id) ?? throw new RegraNegocioException("Cartão não encontrado.");
        if (!c.EhCartao) throw new RegraNegocioException("Limite só existe para cartão de crédito.");
        c.Limite = Validar.Limite(dto.Limite);
        db.DefinirAuditoria("Ajuste de limite", dto.Motivo);
        await db.SaveChangesAsync();
        return c;
    }

    private static void Aplicar(Conta c, ContaDto dto)
    {
        c.Nome = Validar.Texto(dto.Nome, "Nome");
        c.Tipo = dto.Tipo;
        c.SaldoInicial = decimal.Round(dto.SaldoInicial, 2);
        c.Cor = Validar.Opcional(dto.Cor);
        c.Ativa = dto.Ativa;
        if (dto.Tipo == TipoConta.CartaoCredito)
        {
            if (dto.DiaFechamento is not (>= 1 and <= 31) || dto.DiaVencimento is not (>= 1 and <= 31))
                throw new RegraNegocioException("Informe os dias de fechamento e vencimento do cartão (1 a 31).");
            c.DiaFechamento = dto.DiaFechamento;
            c.DiaVencimento = dto.DiaVencimento;
            c.Limite = Validar.Limite(dto.Limite);
        }
        else
        {
            c.DiaFechamento = null;
            c.DiaVencimento = null;
            c.Limite = null;
        }
    }
}
