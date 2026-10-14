using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Regras;

/// <summary>
/// Contas do mês, salário e compras parceladas: criar, corrigir, desativar e reativar. As ocorrências previstas
/// são criadas pelo <see cref="GeradorDeOcorrencias"/>.
/// </summary>
public class RegraService(AppDbContext db, IRelogio relogio, GeradorDeOcorrencias gerador)
{
    /// <summary>Até quantos meses à frente as recorrências ficam geradas.</summary>
    public const int MesesHorizonte = 3;

    private DateOnly Hoje => relogio.Hoje;

    public async Task<Regra> Criar(RegraDto dto)
    {
        var regra = new Regra
        {
            Modo = dto.Modo,
            Tipo = dto.Tipo,
            Descricao = Validar.Texto(dto.Descricao, "Descrição"),
            Valor = Validar.ValorPositivo(dto.Valor),
            ValorVariavel = dto.ValorVariavel,
            FormaPagamento = dto.FormaPagamento,
            Frequencia = dto.Frequencia,
            Intervalo = Math.Clamp(dto.Intervalo, 1, 60),
            DataInicio = dto.DataInicio,
            DataFim = dto.DataFim,
            Observacao = Validar.Opcional(dto.Observacao),
        };

        if (regra.Tipo == TipoLancamento.Transferencia)
            throw new RegraNegocioException("Regras são para receitas ou despesas.");
        if (regra.Modo == ModoRegra.Parcelado)
        {
            if (regra.Tipo != TipoLancamento.Despesa) throw new RegraNegocioException("Somente despesas podem ser parceladas.");
            if (dto.Parcelas is not (>= 2 and <= 120)) throw new RegraNegocioException("Informe de 2 a 120 parcelas.");
            regra.Parcelas = dto.Parcelas;
            regra.Frequencia = Frequencia.Mensal;
            regra.Intervalo = 1;
            regra.DataFim = null;
        }
        if (regra.Modo == ModoRegra.Recorrente)
        {
            AplicarVencimento(regra, dto.Vencimento, dto.DiaUtilNumero, null);
            // A primeira ocorrência é a primeira data da regra a partir da data informada.
            if (regra.Vencimento is not (null or RegraVencimento.DiaFixo) && Calendario.Ocorrencia(regra, 1) < dto.DataInicio)
                regra.DataInicio = Calendario.InicioMes(dto.DataInicio).AddMonths(1);
            if (regra.Vencimento is not (null or RegraVencimento.DiaFixo))
                regra.DataInicio = Calendario.Ocorrencia(regra, 1);
            regra.IniciaPeriodo = regra.Tipo == TipoLancamento.Receita ? dto.IniciaPeriodo : null;
            regra.PagoComRegraId = await PagoCom(regra, dto.PagoComRegraId);
        }
        if (regra.DataFim is { } fim && fim < regra.DataInicio)
            throw new RegraNegocioException("A data final não pode ser anterior à data inicial.");

        await AplicarContaCategoria(regra, dto.ContaId, dto.CategoriaId);

        db.Regras.Add(regra);
        await gerador.Gerar(regra, HorizontePadrao());
        await db.SaveChangesAsync();
        return regra;
    }

    public async Task<Regra> Editar(Guid id, EditarRegraDto dto)
    {
        var regra = await Obter(id);
        regra.Descricao = Validar.Texto(dto.Descricao, "Descrição");
        regra.Observacao = Validar.Opcional(dto.Observacao);
        await AplicarContaCategoria(regra, dto.ContaId, dto.CategoriaId);

        if (regra.Modo == ModoRegra.Recorrente)
        {
            regra.Valor = Validar.ValorPositivo(dto.Valor);
            regra.ValorVariavel = dto.ValorVariavel;
            regra.FormaPagamento = dto.FormaPagamento;
            if (dto.DataFim is { } fim && fim < regra.DataInicio)
                throw new RegraNegocioException("A data final não pode ser anterior à data inicial.");
            regra.DataFim = dto.DataFim;
            if (regra.Tipo == TipoLancamento.Receita && dto.IniciaPeriodo is not null) regra.IniciaPeriodo = dto.IniciaPeriodo;
            regra.PagoComRegraId = await PagoCom(regra, dto.PagoComRegraId);
        }

        // Mudou o dia do vencimento (ex.: "dia 9" para "5º dia útil"): recalcula as datas dos previstos
        // a partir deste mês. O que já foi pago ou cancelado fica como está.
        var mudouVencimento = regra.Modo == ModoRegra.Recorrente && AplicarVencimento(regra, dto.Vencimento, dto.DiaUtilNumero, dto.Dia);

        var previstos = await db.Lancamentos
            .Where(l => l.RegraId == id && l.Status == StatusLancamento.Previsto)
            .ToListAsync();
        var conta = (await db.Contas.FindAsync(regra.ContaId))!;

        foreach (var l in previstos)
        {
            // Ocorrências além da nova data final deixam de existir como compromisso.
            if (regra.DataFim is { } dataFim && l.Data > dataFim)
            {
                l.Status = StatusLancamento.Cancelado;
                l.MotivoCancelamento = "Data final da recorrência alterada";
                continue;
            }
            if (mudouVencimento && l.Data >= Calendario.InicioMes(Hoje) && l.Sequencia is { } seq)
            {
                l.Data = Calendario.Ocorrencia(regra, seq);
                if (conta.EhCartao) l.FaturaMes = Calendario.FaturaDaCompra(conta, l.Data);
            }
            if (!dto.AplicarAosPrevistos || l.Data < Hoje) continue;

            l.Descricao = GeradorDeOcorrencias.Descricao(regra, l.Sequencia ?? 1);
            l.CategoriaId = regra.CategoriaId;
            l.ContaId = regra.ContaId;
            l.FaturaMes = conta.EhCartao ? Calendario.FaturaDaCompra(conta, l.Data) : null;
            if (regra.Modo == ModoRegra.Recorrente)
            {
                l.Valor = regra.Valor;
                l.FormaPagamento = conta.EhCartao ? FormaPagamento.CartaoCredito : regra.FormaPagamento;
            }
        }

        db.DefinirAuditoria("Correção", dto.Motivo);
        await db.SaveChangesAsync();
        return regra;
    }

    /// <summary>Para de gerar novas ocorrências. O passado nunca é tocado.</summary>
    public async Task<Regra> Desativar(Guid id, DesativarRegraDto dto)
    {
        var regra = await Obter(id);
        if (!regra.Ativa) throw new RegraNegocioException("A regra já está desativada.");
        regra.Ativa = false;

        if (dto.CancelarPrevistos)
        {
            var futuros = await db.Lancamentos
                .Where(l => l.RegraId == id && l.Status == StatusLancamento.Previsto && l.Data >= Hoje)
                .ToListAsync();
            foreach (var l in futuros)
            {
                l.Status = StatusLancamento.Cancelado;
                l.MotivoCancelamento = Validar.Opcional(dto.Motivo) ?? "Recorrência desativada";
            }
        }

        db.DefinirAuditoria("Desativação", dto.Motivo);
        await db.SaveChangesAsync();
        return regra;
    }

    public async Task<Regra> Reativar(Guid id)
    {
        var regra = await Obter(id);
        if (regra.Modo == ModoRegra.Parcelado)
            throw new RegraNegocioException("Parcelamentos cancelados não podem ser reativados; lance uma nova compra.");
        regra.Ativa = true;
        db.DefinirAuditoria("Reativação");
        await gerador.Gerar(regra, HorizontePadrao());
        await db.SaveChangesAsync();
        return regra;
    }

    /// <summary>
    /// Compra parcelada lançada no mês errado: todas as parcelas andam juntas N meses (data e fatura).
    /// No cartão, trava se alguma fatura das parcelas já tem pagamento; fora do cartão, se alguma parcela já foi paga.
    /// </summary>
    public async Task<Regra> MoverParcelas(Guid id, MoverParcelasDto dto)
    {
        var regra = await Obter(id);
        if (regra.Modo != ModoRegra.Parcelado) throw new RegraNegocioException("Só compras parceladas têm parcelas para mover.");
        if (dto.Meses is 0 or < -12 or > 12) throw new RegraNegocioException("Escolha mover de 1 a 12 meses.");

        var conta = (await db.Contas.FindAsync(regra.ContaId))!;
        var parcelas = await db.Lancamentos.Where(l => l.RegraId == id && l.Status != StatusLancamento.Cancelado).ToListAsync();
        if (conta.EhCartao)
        {
            var faturas = parcelas.Where(l => l.FaturaMes != null).Select(l => l.FaturaMes!.Value).Distinct().ToList();
            var faturaPaga = (await db.Lancamentos
                    .Where(l => l.Tipo == TipoLancamento.Transferencia && l.Status == StatusLancamento.Realizado
                                && l.ContaDestinoId == conta.Id && l.FaturaMes != null)
                    .Select(l => l.FaturaMes!.Value).ToListAsync())
                .Where(faturas.Contains).Order().FirstOrDefault();
            if (faturaPaga != default)
                throw new RegraNegocioException($"A fatura de {faturaPaga:MM/yyyy} já tem pagamento: as parcelas dela não podem mudar de mês.");
        }
        else if (parcelas.Any(l => l.Status == StatusLancamento.Realizado))
        {
            throw new RegraNegocioException("Esta compra já tem parcelas pagas; corrija as parcelas uma a uma.");
        }

        regra.DataInicio = regra.DataInicio.AddMonths(dto.Meses);
        foreach (var l in parcelas)
        {
            l.Data = Calendario.Ocorrencia(regra, l.Sequencia ?? 1);
            if (!conta.EhCartao) continue;
            l.FaturaMes = l.FaturaMes?.AddMonths(dto.Meses);
            // No cartão a parcela é cobrada sozinha na data: passada = realizada, futura = prevista.
            l.Status = l.Data <= Hoje ? StatusLancamento.Realizado : StatusLancamento.Previsto;
        }

        db.DefinirAuditoria("Parcelas movidas", Validar.Opcional(dto.Motivo) ?? $"{(dto.Meses > 0 ? "+" : "")}{dto.Meses} mês(es)");
        await db.SaveChangesAsync();
        return regra;
    }

    /// <summary>
    /// Garante as ocorrências de todas as regras ativas até a data informada e realiza automaticamente
    /// as cobranças que não dependem do usuário (cartão e débito automático com valor fixo).
    /// </summary>
    public async Task Sincronizar(DateOnly? ate = null)
    {
        var horizonte = ate is { } d && d > HorizontePadrao() ? d : HorizontePadrao();
        var ativas = await db.Regras.Where(r => r.Ativa).ToListAsync();
        foreach (var regra in ativas)
            await gerador.Gerar(regra, horizonte);
        if (db.ChangeTracker.HasChanges())
        {
            db.DefinirAuditoria("Geração automática");
            await db.SaveChangesAsync();
        }

        var automaticos = await db.Lancamentos
            .Include(l => l.Conta).Include(l => l.Regra)
            .Where(l => l.Status == StatusLancamento.Previsto && l.Data <= Hoje && l.Tipo != TipoLancamento.Transferencia)
            .ToListAsync();
        var confirmados = 0;
        foreach (var l in automaticos.Where(ConfirmaSozinho))
        {
            l.Status = StatusLancamento.Realizado;
            confirmados++;
        }
        if (confirmados > 0)
        {
            db.DefinirAuditoria("Confirmação automática", "Cobrança no cartão/débito automático ou recarga do vale");
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// O que acontece sem o usuário fazer nada: compra no cartão quando chega a data, cobrança fixa no
    /// cartão/débito automático e recarga do vale. Valores variáveis (ex.: conta de luz) esperam confirmação.
    /// </summary>
    private static bool ConfirmaSozinho(Lancamento l)
    {
        if (l.Regra is { ValorVariavel: true }) return false;
        if (l.Tipo == TipoLancamento.Despesa)
            return l.Conta!.EhCartao || (l.RegraId != null && l.FormaPagamento == FormaPagamento.DebitoAutomatico);
        return l.RegraId != null && l.Conta!.EhBeneficio;
    }

    /// <summary>Aplica a forma de vencimento (só regras mensais). Devolve true se a data das ocorrências mudou.</summary>
    private static bool AplicarVencimento(Regra regra, RegraVencimento? vencimento, int? diaUtil, int? dia)
    {
        if (vencimento is null) return false;
        if (regra.Frequencia != Frequencia.Mensal && vencimento != RegraVencimento.DiaFixo)
            throw new RegraNegocioException("Dia útil e último dia só valem para contas mensais.");
        if (vencimento == RegraVencimento.DiaUtil && diaUtil is not (>= 1 and <= 23))
            throw new RegraNegocioException("Informe qual dia útil (1 a 23).");
        if (dia is not null and not (>= 1 and <= 31))
            throw new RegraNegocioException("O dia do vencimento deve ser de 1 a 31.");

        var antes = (regra.Vencimento ?? RegraVencimento.DiaFixo, regra.DiaUtilNumero, regra.DataInicio);
        regra.Vencimento = vencimento == RegraVencimento.DiaFixo ? null : vencimento;
        regra.DiaUtilNumero = vencimento == RegraVencimento.DiaUtil ? diaUtil : null;
        if (regra.Vencimento is null && dia is { } d)
            regra.DataInicio = new DateOnly(regra.DataInicio.Year, regra.DataInicio.Month, Math.Min(d, DateTime.DaysInMonth(regra.DataInicio.Year, regra.DataInicio.Month)));
        else if (regra.Vencimento is { } v)
            regra.DataInicio = Calendario.DataNoMes(regra.DataInicio, v, regra.DiaUtilNumero);
        return antes != (regra.Vencimento ?? RegraVencimento.DiaFixo, regra.DiaUtilNumero, regra.DataInicio);
    }

    /// <summary>Valida o "pagar com": só contas, e só com uma entrada recorrente ativa (salário, adiantamento...).</summary>
    private async Task<Guid?> PagoCom(Regra regra, Guid? pagamentoId)
    {
        if (pagamentoId is null || regra.Tipo != TipoLancamento.Despesa) return null;
        var pagamento = await db.Regras.FindAsync(pagamentoId.Value);
        if (pagamento is not { Tipo: TipoLancamento.Receita, Modo: ModoRegra.Recorrente, Ativa: true })
            throw new RegraNegocioException("Escolha um dia de pagamento ativo (uma entrada recorrente, como o salário).");
        return pagamento.Id;
    }

    public async Task<Regra> Obter(Guid id) =>
        await db.Regras.FindAsync(id) ?? throw new RegraNegocioException("Regra não encontrada.");

    private DateOnly HorizontePadrao() => Calendario.FimMes(Hoje.AddMonths(MesesHorizonte - 1));

    private async Task AplicarContaCategoria(Regra regra, Guid contaId, Guid categoriaId)
    {
        var conta = await db.Contas.FindAsync(contaId) ?? throw new RegraNegocioException("Conta não encontrada.");
        if (!conta.Ativa && regra.ContaId != conta.Id) throw new RegraNegocioException($"A conta '{conta.Nome}' está desativada.");
        if (conta.EhCartao && regra.Tipo == TipoLancamento.Receita)
            throw new RegraNegocioException("Receitas programadas não podem cair no cartão de crédito.");
        if (conta.EhBeneficio && regra.Tipo == TipoLancamento.Despesa)
            throw new RegraNegocioException("O vale só serve para compras do dia a dia; contas fixas saem de uma conta bancária.");

        var categoria = await db.Categorias.FindAsync(categoriaId) ?? throw new RegraNegocioException("Informe a categoria.");
        var tipoEsperado = regra.Tipo == TipoLancamento.Receita ? TipoCategoria.Receita : TipoCategoria.Despesa;
        if (categoria.Tipo != tipoEsperado)
            throw new RegraNegocioException($"A categoria '{categoria.Nome}' é de {categoria.Tipo.ToString().ToLower()}.");
        if (!categoria.Ativa && regra.CategoriaId != categoria.Id)
            throw new RegraNegocioException($"A categoria '{categoria.Nome}' está desativada.");

        regra.ContaId = conta.Id;
        regra.CategoriaId = categoria.Id;
    }
}
