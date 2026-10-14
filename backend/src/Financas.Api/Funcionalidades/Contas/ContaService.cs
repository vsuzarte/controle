using Financas.Api.Dados;
using Financas.Api.Dominio;
using Financas.Api.Funcionalidades.Cartoes;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Contas;

/// <summary>Cadastro de contas: bancos, carteira, cartões de crédito e vales.</summary>
public class ContaService(AppDbContext db, IRelogio relogio, SaldoService saldos, CartaoService cartoes, RecargaDoVale recargas)
{
    public async Task<List<ContaView>> Listar()
    {
        var movimentos = await saldos.Saldos(relogio.Hoje);
        var limitesUsados = await cartoes.LimitesUsados();
        var recargasDosVales = await recargas.Todas();
        var contas = await db.Contas.OrderBy(c => c.Tipo).ThenBy(c => c.Nome).ToListAsync();
        return contas.Select(c => new ContaView(
                c.Id, c.Nome, c.Tipo, c.SaldoInicial, c.Cor, c.DiaFechamento, c.DiaVencimento, c.Ativa, c.Limite,
                c.SaldoInicial + movimentos.GetValueOrDefault(c.Id),
                c.EhCartao ? limitesUsados.GetValueOrDefault(c.Id) : null,
                recargasDosVales.GetValueOrDefault(c.Id)))
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
        var c = await Obter(id);
        if (c.Tipo != dto.Tipo) await PodeMudarTipo(c, dto.Tipo);
        if (c.Ativa && !dto.Ativa) await ExigirSemPendencias(c, "desativar");
        Aplicar(c, dto);
        await db.SaveChangesAsync();
        return c;
    }

    /// <summary>"Excluir" uma conta é desativar: some das listas e escolhas, mas o histórico continua.</summary>
    public async Task<Conta> Desativar(Guid id, string? motivo)
    {
        var c = await Obter(id);
        if (!c.Ativa) throw new RegraNegocioException("A conta já está desativada.");
        await ExigirSemPendencias(c, "desativar");
        c.Ativa = false;
        db.DefinirAuditoria("Desativação", motivo);
        await db.SaveChangesAsync();
        return c;
    }

    public async Task<Conta> Reativar(Guid id)
    {
        var c = await Obter(id);
        c.Ativa = true;
        db.DefinirAuditoria("Reativação");
        await db.SaveChangesAsync();
        return c;
    }

    /// <summary>
    /// Mudar o tipo só trava com pendências. Cartão e vale têm regras próprias (fatura, só compras):
    /// conta com histórico válido não vira nem deixa de ser cartão/vale. Lançamentos cancelados não contam.
    /// </summary>
    private async Task PodeMudarTipo(Conta c, TipoConta novo)
    {
        await ExigirSemPendencias(c, "mudar o tipo");
        var especial = c.EhCartao || c.EhBeneficio || novo is TipoConta.CartaoCredito or TipoConta.Beneficio;
        if (especial && await db.Lancamentos.AnyAsync(l => l.Status != StatusLancamento.Cancelado && (l.ContaId == c.Id || l.ContaDestinoId == c.Id)))
            throw new RegraNegocioException(
                "Esta conta tem lançamentos e cartão/vale funcionam de outro jeito (fatura, só compras). " +
                "Crie uma conta nova com o tipo certo e desative esta.");
    }

    private async Task ExigirSemPendencias(Conta c, string acao)
    {
        var pendencias = await Pendencias(c);
        if (pendencias.Count > 0)
            throw new RegraNegocioException($"Para {acao} '{c.Nome}', resolva antes: {string.Join(", ", pendencias)}.");
    }

    /// <summary>O que ainda vai acontecer nesta conta: previstos, contas do mês que seguem gerando e fatura sem pagar.</summary>
    private async Task<List<string>> Pendencias(Conta c)
    {
        var hoje = relogio.Hoje;
        var lista = new List<string>();
        var previstos = await db.Lancamentos.CountAsync(l => l.Status == StatusLancamento.Previsto && (l.ContaId == c.Id || l.ContaDestinoId == c.Id));
        if (previstos > 0) lista.Add($"{previstos} lançamento(s) previsto(s)");
        var regras = await db.Regras.CountAsync(r => r.Ativa && r.Modo == ModoRegra.Recorrente && r.ContaId == c.Id && (r.DataFim == null || r.DataFim >= hoje));
        if (regras > 0) lista.Add(c.EhBeneficio
            ? "a recarga automática (apague o valor da recarga)"
            : $"{regras} conta(s) do mês ativa(s) (encerre em Contas do mês)");
        if (c.EhCartao && (await cartoes.Faturas(c.Id)).Any(f => f.Restante > 0)) lista.Add("fatura sem pagar");
        return lista;
    }

    private async Task<Conta> Obter(Guid id) =>
        await db.Contas.FindAsync(id) ?? throw new RegraNegocioException("Conta não encontrada.");

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
