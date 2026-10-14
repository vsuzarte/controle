using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Servicos;

public class LancamentoService(AppDbContext db, IRelogio relogio, RegraService regras)
{
    private DateOnly Hoje => relogio.Hoje;

    public async Task<List<Lancamento>> Criar(NovoLancamentoDto dto)
    {
        var descricao = Validar.Texto(dto.Descricao, "Descrição");
        var valor = Validar.ValorPositivo(dto.Valor);
        var conta = await ContaAtiva(dto.ContaId);

        if (dto.Parcelas is > 1)
        {
            if (dto.Tipo != TipoLancamento.Despesa)
                throw new RegraNegocioException("Somente despesas podem ser parceladas.");
            var regra = await regras.Criar(new RegraDto(
                ModoRegra.Parcelado, TipoLancamento.Despesa, descricao, dto.CategoriaId ?? Guid.Empty, conta.Id,
                dto.FormaPagamento, valor, false, dto.Parcelas, Frequencia.Mensal, 1, dto.Data, null, dto.Observacao));
            return await db.Lancamentos.Where(l => l.RegraId == regra.Id).OrderBy(l => l.Sequencia).ToListAsync();
        }

        var l = new Lancamento
        {
            Tipo = dto.Tipo,
            Descricao = descricao,
            Valor = valor,
            Data = dto.Data,
            ContaId = conta.Id,
            FormaPagamento = dto.FormaPagamento,
            Observacao = Validar.Opcional(dto.Observacao),
        };

        await AplicarContaCategoria(l, conta, dto.ContaDestinoId, dto.CategoriaId, dto.FaturaMes);

        if (conta.EhCartao)
        {
            // No cartão a compra já aconteceu; só fica "prevista" se for agendada para o futuro.
            l.Status = l.Data <= Hoje ? StatusLancamento.Realizado : StatusLancamento.Previsto;
        }
        else
        {
            l.Status = dto.Status ?? (l.Data <= Hoje ? StatusLancamento.Realizado : StatusLancamento.Previsto);
            if (l.Status == StatusLancamento.Cancelado)
                throw new RegraNegocioException("Não é possível criar um lançamento já cancelado.");
            if (l.Status == StatusLancamento.Realizado && l.Data > Hoje)
                throw new RegraNegocioException("Um lançamento realizado não pode ter data futura. Marque como previsto.");
        }
        l.Programado = l.Status == StatusLancamento.Previsto;

        db.Lancamentos.Add(l);
        var criados = new List<Lancamento> { l };
        if (dto.ContaComplementoId is { } complementoId && conta.EhBeneficio && l.Tipo == TipoLancamento.Despesa)
            criados.AddRange(await Completar(l, conta, complementoId));
        await db.SaveChangesAsync();
        return criados;
    }

    /// <summary>
    /// Compra maior que o saldo do vale: o vale paga o que tem e o resto vira um segundo lançamento
    /// na outra conta (mesma descrição e categoria). Gravado junto, na mesma transação.
    /// </summary>
    private async Task<List<Lancamento>> Completar(Lancamento compra, Conta vale, Guid complementoId)
    {
        var outra = await ContaAtiva(complementoId);
        if (outra.EhBeneficio || outra.Id == vale.Id)
            throw new RegraNegocioException("Escolha outra conta (que não seja vale) para completar a compra.");

        var saldo = Math.Max(0, await SaldoDaConta(vale, compra.Data));
        if (compra.Valor <= saldo) return [];
        var falta = compra.Valor - saldo;
        if (saldo == 0)
        {
            // Vale zerado: a compra inteira vai para a outra conta.
            compra.ContaId = outra.Id;
            await AplicarContaCategoria(compra, outra, null, compra.CategoriaId, null);
            return [];
        }
        compra.Valor = saldo;
        var resto = new Lancamento
        {
            Tipo = compra.Tipo,
            Status = outra.EhCartao && compra.Data <= Hoje ? StatusLancamento.Realizado : compra.Status,
            Descricao = compra.Descricao,
            Valor = falta,
            Data = compra.Data,
            ContaId = outra.Id,
            FormaPagamento = compra.FormaPagamento,
            Observacao = $"Complemento da compra no {vale.Nome}",
            Programado = compra.Programado,
        };
        await AplicarContaCategoria(resto, outra, null, compra.CategoriaId, null);
        db.Lancamentos.Add(resto);
        return [resto];
    }

    private async Task<decimal> SaldoDaConta(Conta conta, DateOnly ate)
    {
        var movimentos = await db.Lancamentos
            .Where(l => l.Status == StatusLancamento.Realizado && l.Data <= ate && (l.ContaId == conta.Id || l.ContaDestinoId == conta.Id))
            .Select(l => new { l.Tipo, l.Valor, l.ContaId })
            .ToListAsync();
        return conta.SaldoInicial + movimentos.Sum(m =>
            m.Tipo == TipoLancamento.Receita ? m.Valor
            : m.Tipo == TipoLancamento.Despesa ? -m.Valor
            : m.ContaId == conta.Id ? -m.Valor : m.Valor);
    }

    public async Task<Lancamento> Editar(Guid id, EditarLancamentoDto dto)
    {
        var l = await Obter(id);
        if (l.Status == StatusLancamento.Cancelado)
            throw new RegraNegocioException("Lançamento cancelado não pode ser editado.");

        var conta = l.ContaId == dto.ContaId ? await db.Contas.FindAsync(dto.ContaId) : await ContaAtiva(dto.ContaId);
        l.Descricao = Validar.Texto(dto.Descricao, "Descrição");
        l.Valor = l.EstornoDeId is null ? Validar.ValorPositivo(dto.Valor) : -Validar.ValorPositivo(Math.Abs(dto.Valor));
        l.Data = dto.Data;
        l.ContaId = conta!.Id;
        l.FormaPagamento = dto.FormaPagamento;
        l.Observacao = Validar.Opcional(dto.Observacao);
        await AplicarContaCategoria(l, conta, dto.ContaDestinoId, dto.CategoriaId, dto.FaturaMes);

        if (l.Status == StatusLancamento.Realizado && l.Data > Hoje && !conta.EhCartao)
            throw new RegraNegocioException("Um lançamento realizado não pode ter data futura.");

        db.DefinirAuditoria("Correção", dto.Motivo);
        await db.SaveChangesAsync();
        return l;
    }

    /// <summary>
    /// Transforma um lançamento previsto em realizado. É o mesmo registro: o pagamento de uma
    /// conta/parcela/salário nunca gera um segundo lançamento, então não há como duplicar.
    /// </summary>
    public async Task<Lancamento> Confirmar(Guid id, ConfirmarDto dto)
    {
        var l = await Obter(id);
        if (l.Status != StatusLancamento.Previsto)
            throw new RegraNegocioException("Somente lançamentos previstos podem ser confirmados.");
        if (dto.Data > Hoje)
            throw new RegraNegocioException("A data do pagamento/recebimento não pode ser futura.");

        if (dto.ContaId is { } contaId && contaId != l.ContaId)
        {
            var conta = await ContaAtiva(contaId);
            if (l.Tipo == TipoLancamento.Transferencia && conta.Id == l.ContaDestinoId)
                throw new RegraNegocioException("Conta de origem e destino devem ser diferentes.");
            if (conta.EhBeneficio && l.Tipo == TipoLancamento.Despesa && l.Programado)
                throw new RegraNegocioException("Contas não podem ser pagas com o vale.");
            l.ContaId = conta.Id;
            l.FaturaMes = conta.EhCartao ? Calendario.FaturaDaCompra(conta, dto.Data) : null;
        }
        l.Valor = Validar.ValorPositivo(dto.Valor);
        l.Juros = Validar.Juros(dto.Juros, l.Valor);
        l.Data = dto.Data;
        if (dto.FormaPagamento is not null) l.FormaPagamento = dto.FormaPagamento;
        l.Status = StatusLancamento.Realizado;

        db.DefinirAuditoria("Confirmação");
        await db.SaveChangesAsync();
        return l;
    }

    /// <summary>Volta um compromisso confirmado por engano para previsto.</summary>
    public async Task<Lancamento> Reabrir(Guid id, string? motivo)
    {
        var l = await Obter(id);
        if (l.Status != StatusLancamento.Realizado || !l.Programado)
            throw new RegraNegocioException("Só é possível reabrir compromissos programados que foram confirmados.");
        l.Status = StatusLancamento.Previsto;
        db.DefinirAuditoria("Reabertura", motivo);
        await db.SaveChangesAsync();
        return l;
    }

    public async Task<Lancamento> Cancelar(Guid id, string? motivo)
    {
        var l = await Obter(id);
        if (l.Status == StatusLancamento.Cancelado)
            throw new RegraNegocioException("Lançamento já está cancelado.");
        var justificativa = Validar.Texto(motivo, "Motivo do cancelamento");

        var estornos = await db.Lancamentos.AnyAsync(e => e.EstornoDeId == id && e.Status != StatusLancamento.Cancelado);
        if (estornos)
            throw new RegraNegocioException("Este lançamento tem estornos ativos. Cancele os estornos primeiro.");

        l.Status = StatusLancamento.Cancelado;
        l.MotivoCancelamento = justificativa;
        db.DefinirAuditoria("Cancelamento", justificativa);
        await db.SaveChangesAsync();
        return l;
    }

    /// <summary>
    /// Registra uma devolução real (total ou parcial) de um lançamento realizado. O estorno é um novo
    /// lançamento com valor negativo, mesma categoria e conta, vinculado ao original.
    /// </summary>
    public async Task<Lancamento> Estornar(Guid id, EstornoDto dto)
    {
        var original = await Obter(id);
        if (original.Status != StatusLancamento.Realizado || original.EstornoDeId is not null)
            throw new RegraNegocioException("Só é possível estornar lançamentos realizados.");
        if (original.Tipo == TipoLancamento.Transferencia)
            throw new RegraNegocioException("Transferências não são estornadas; cancele ou faça a transferência inversa.");
        if (dto.Data > Hoje)
            throw new RegraNegocioException("A data do estorno não pode ser futura.");

        var valor = Validar.ValorPositivo(dto.Valor);
        var jaEstornado = (await db.Lancamentos
            .Where(e => e.EstornoDeId == id && e.Status != StatusLancamento.Cancelado)
            .Select(e => e.Valor).ToListAsync()).Sum(v => -v);
        if (valor > original.Valor - jaEstornado)
            throw new RegraNegocioException($"O estorno excede o valor disponível ({original.Valor - jaEstornado:N2}).");

        var conta = (await db.Contas.FindAsync(original.ContaId))!;
        var estorno = new Lancamento
        {
            Tipo = original.Tipo,
            Status = StatusLancamento.Realizado,
            Descricao = $"Estorno: {original.Descricao}",
            Valor = -valor,
            Data = dto.Data,
            ContaId = original.ContaId,
            CategoriaId = original.CategoriaId,
            FormaPagamento = original.FormaPagamento,
            Programado = original.Programado,
            EstornoDeId = original.Id,
            Observacao = Validar.Opcional(dto.Motivo),
            FaturaMes = conta.EhCartao ? Calendario.FaturaDaCompra(conta, dto.Data) : null,
        };
        db.Lancamentos.Add(estorno);
        db.DefinirAuditoria("Estorno", dto.Motivo);
        await db.SaveChangesAsync();
        return estorno;
    }

    public async Task<Lancamento> Obter(Guid id) =>
        await db.Lancamentos.FindAsync(id) ?? throw new RegraNegocioException("Lançamento não encontrado.");

    private async Task<Conta> ContaAtiva(Guid id)
    {
        var conta = await db.Contas.FindAsync(id) ?? throw new RegraNegocioException("Conta não encontrada.");
        if (!conta.Ativa) throw new RegraNegocioException($"A conta '{conta.Nome}' está desativada.");
        return conta;
    }

    private async Task AplicarContaCategoria(Lancamento l, Conta conta, Guid? contaDestinoId, Guid? categoriaId, DateOnly? faturaMes)
    {
        if (l.Tipo == TipoLancamento.Transferencia)
        {
            if (conta.EhCartao)
                throw new RegraNegocioException("Transferências não podem sair de um cartão de crédito.");
            if (contaDestinoId is null || contaDestinoId == conta.Id)
                throw new RegraNegocioException("Informe uma conta de destino diferente da origem.");
            var destino = await db.Contas.FindAsync(contaDestinoId.Value) ?? throw new RegraNegocioException("Conta de destino não encontrada.");
            if (conta.EhBeneficio || destino.EhBeneficio)
                throw new RegraNegocioException("O saldo do vale não pode ser transferido; ele só é usado em compras.");
            l.ContaDestinoId = destino.Id;
            l.CategoriaId = null;
            l.FaturaMes = destino.EhCartao ? Calendario.InicioMes(faturaMes ?? Calendario.FaturaDaCompra(destino, l.Data)) : null;
            return;
        }

        l.ContaDestinoId = null;
        if (categoriaId is null) throw new RegraNegocioException("Informe a categoria.");
        var categoria = await db.Categorias.FindAsync(categoriaId.Value) ?? throw new RegraNegocioException("Categoria não encontrada.");
        var tipoEsperado = l.Tipo == TipoLancamento.Receita ? TipoCategoria.Receita : TipoCategoria.Despesa;
        if (categoria.Tipo != tipoEsperado)
            throw new RegraNegocioException($"A categoria '{categoria.Nome}' é de {categoria.Tipo.ToString().ToLower()}.");
        if (!categoria.Ativa && l.CategoriaId != categoria.Id)
            throw new RegraNegocioException($"A categoria '{categoria.Nome}' está desativada.");
        l.CategoriaId = categoria.Id;

        if (conta.EhCartao)
        {
            l.FormaPagamento = FormaPagamento.CartaoCredito;
            l.FaturaMes = Calendario.InicioMes(faturaMes ?? Calendario.FaturaDaCompra(conta, l.Data));
        }
        else
        {
            l.FaturaMes = null;
        }
    }
}

internal static class Validar
{
    public static string Texto(string? s, string campo) =>
        string.IsNullOrWhiteSpace(s) ? throw new RegraNegocioException($"Preencha o campo {campo}.") : s.Trim();

    public static string? Opcional(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static decimal? Limite(decimal? v)
    {
        if (v is null) return null;
        var arredondado = decimal.Round(v.Value, 2, MidpointRounding.AwayFromZero);
        return arredondado < 0 ? throw new RegraNegocioException("O limite não pode ser negativo.") : arredondado;
    }

    /// <summary>Juros/multa embutidos num pagamento: maior que zero e menor que o valor pago.</summary>
    public static decimal? Juros(decimal? juros, decimal valorPago)
    {
        if (juros is null || juros == 0) return null;
        var j = decimal.Round(juros.Value, 2, MidpointRounding.AwayFromZero);
        if (j < 0) throw new RegraNegocioException("Os juros não podem ser negativos.");
        if (j >= valorPago) throw new RegraNegocioException("Os juros devem ser menores que o valor pago.");
        return j;
    }

    public static decimal ValorPositivo(decimal v)
    {
        var arredondado = decimal.Round(v, 2, MidpointRounding.AwayFromZero);
        if (arredondado <= 0) throw new RegraNegocioException("O valor deve ser maior que zero.");
        if (arredondado > 99_999_999m) throw new RegraNegocioException("Valor muito alto.");
        return arredondado;
    }
}
