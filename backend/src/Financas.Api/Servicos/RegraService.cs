using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Servicos;

/// <summary>
/// Regras geram lançamentos previstos (contas fixas, salário, parcelas). A geração é idempotente:
/// cada ocorrência tem um número de sequência único por regra, então rodar de novo nunca duplica,
/// e uma ocorrência cancelada não volta a ser criada.
/// </summary>
public class RegraService(AppDbContext db, IRelogio relogio)
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
        if (regra.DataFim is { } fim && fim < regra.DataInicio)
            throw new RegraNegocioException("A data final não pode ser anterior à data inicial.");

        await AplicarContaCategoria(regra, dto.ContaId, dto.CategoriaId);

        db.Regras.Add(regra);
        await GerarParaRegra(regra, HorizontePadrao());
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
        }

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
            if (!dto.AplicarAosPrevistos || l.Data < Hoje) continue;

            l.Descricao = DescricaoOcorrencia(regra, l.Sequencia ?? 1);
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
        await GerarParaRegra(regra, HorizontePadrao());
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
            await GerarParaRegra(regra, horizonte);
        if (db.ChangeTracker.HasChanges())
        {
            db.DefinirAuditoria("Geração automática");
            await db.SaveChangesAsync();
        }

        var automaticos = await db.Lancamentos
            .Include(l => l.Conta).Include(l => l.Regra)
            .Where(l => l.Status == StatusLancamento.Previsto && l.RegraId != null && l.Data <= Hoje
                        && l.Tipo == TipoLancamento.Despesa)
            .ToListAsync();
        var confirmados = 0;
        foreach (var l in automaticos.Where(l =>
                     (l.Conta!.EhCartao || l.FormaPagamento == FormaPagamento.DebitoAutomatico)
                     && !l.Regra!.ValorVariavel))
        {
            l.Status = StatusLancamento.Realizado;
            confirmados++;
        }
        if (confirmados > 0)
        {
            db.DefinirAuditoria("Confirmação automática", "Cobrança automática no cartão/débito");
            await db.SaveChangesAsync();
        }
    }

    public async Task<Regra> Obter(Guid id) =>
        await db.Regras.FindAsync(id) ?? throw new RegraNegocioException("Regra não encontrada.");

    private DateOnly HorizontePadrao() => Calendario.FimMes(Hoje.AddMonths(MesesHorizonte - 1));

    private async Task GerarParaRegra(Regra regra, DateOnly horizonte)
    {
        if (!regra.Ativa) return;
        var conta = await db.Contas.FindAsync(regra.ContaId) ?? throw new RegraNegocioException("Conta não encontrada.");

        var existentes = db.Entry(regra).State == EntityState.Added
            ? new HashSet<int>()
            : (await db.Lancamentos.Where(l => l.RegraId == regra.Id).Select(l => l.Sequencia ?? 0).ToListAsync()).ToHashSet();

        if (regra.Modo == ModoRegra.Parcelado)
        {
            var n = regra.Parcelas!.Value;
            var parcelas = DividirParcelas(regra.Valor, n);
            var primeiraFatura = conta.EhCartao ? Calendario.FaturaDaCompra(conta, regra.DataInicio) : (DateOnly?)null;
            for (var i = 1; i <= n; i++)
            {
                if (existentes.Contains(i)) continue;
                var data = Calendario.Ocorrencia(regra, i);
                db.Lancamentos.Add(NovaOcorrencia(regra, conta, i, data, parcelas[i - 1], primeiraFatura?.AddMonths(i - 1)));
            }
            return;
        }

        for (var i = 1; i < 2000; i++)
        {
            var data = Calendario.Ocorrencia(regra, i);
            if (data > horizonte || (regra.DataFim is { } fim && data > fim)) break;
            if (existentes.Contains(i)) continue;
            var fatura = conta.EhCartao ? Calendario.FaturaDaCompra(conta, data) : (DateOnly?)null;
            db.Lancamentos.Add(NovaOcorrencia(regra, conta, i, data, regra.Valor, fatura));
        }
    }

    private Lancamento NovaOcorrencia(Regra regra, Conta conta, int seq, DateOnly data, decimal valor, DateOnly? fatura) => new()
    {
        Tipo = regra.Tipo,
        // Compra parcelada no cartão já está comprometida: parcelas passadas nascem realizadas.
        Status = regra.Modo == ModoRegra.Parcelado && conta.EhCartao && data <= Hoje
            ? StatusLancamento.Realizado
            : StatusLancamento.Previsto,
        Descricao = DescricaoOcorrencia(regra, seq),
        Valor = valor,
        Data = data,
        ContaId = conta.Id,
        CategoriaId = regra.CategoriaId,
        FormaPagamento = conta.EhCartao ? FormaPagamento.CartaoCredito : regra.FormaPagamento,
        Observacao = regra.Observacao,
        Programado = true,
        RegraId = regra.Id,
        Sequencia = seq,
        FaturaMes = fatura,
    };

    private static string DescricaoOcorrencia(Regra regra, int seq) =>
        regra.Modo == ModoRegra.Parcelado ? $"{regra.Descricao} ({seq}/{regra.Parcelas})" : regra.Descricao;

    /// <summary>Divide em parcelas iguais; a diferença de centavos vai na primeira, e a soma fecha exatamente com o total.</summary>
    public static decimal[] DividirParcelas(decimal total, int n)
    {
        var baseParcela = Math.Floor(total * 100m / n) / 100m;
        var parcelas = Enumerable.Repeat(baseParcela, n).ToArray();
        parcelas[0] += total - baseParcela * n;
        return parcelas;
    }

    private async Task AplicarContaCategoria(Regra regra, Guid contaId, Guid categoriaId)
    {
        var conta = await db.Contas.FindAsync(contaId) ?? throw new RegraNegocioException("Conta não encontrada.");
        if (!conta.Ativa && regra.ContaId != conta.Id) throw new RegraNegocioException($"A conta '{conta.Nome}' está desativada.");
        if (conta.EhCartao && regra.Tipo == TipoLancamento.Receita)
            throw new RegraNegocioException("Receitas programadas não podem cair no cartão de crédito.");

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
