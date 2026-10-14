namespace Financas.Tests;

/// <summary>Nada é apagado e toda correção fica registrada.</summary>
public class AuditoriaTests
{
    [Fact]
    public async Task Corrigir_lancamento_mantem_versao_anterior_na_auditoria()
    {
        using var a = new Ambiente();
        var l = await a.Despesa(150m, D(2026, 10, 10));

        await a.Lancamentos.Editar(l.Id, new EditarLancamentoDto("Despesa", 105m, l.Data, a.Banco.Id, null,
            a.Mercado.Id, FormaPagamento.Pix, null, null, "Valor digitado errado"));

        var log = await a.Db.AuditLogs.Where(x => x.EntidadeId == l.Id && x.Operacao == "Correção").SingleAsync();
        Assert.Contains("150", log.Antes);
        Assert.Contains("105", log.Depois);
        Assert.Equal("Valor digitado errado", log.Motivo);
        Assert.Equal(105m, (await a.Db.Lancamentos.FindAsync(l.Id))!.Valor);
    }

    [Fact]
    public async Task Cancelar_nao_apaga_e_deixa_de_afetar_o_saldo()
    {
        using var a = new Ambiente();
        var l = await a.Despesa(200m, D(2026, 10, 10));
        Assert.Equal(800m, (await a.Resumo.Obter()).SaldoDisponivel);

        await a.Lancamentos.Cancelar(l.Id, "Lançado em duplicidade");

        a.Db.ChangeTracker.Clear();
        var salvo = await a.Db.Lancamentos.FindAsync(l.Id);
        Assert.Equal(StatusLancamento.Cancelado, salvo!.Status);
        Assert.Equal(1000m, (await a.Resumo.Obter()).SaldoDisponivel);
    }

    [Fact]
    public async Task Cancelar_exige_motivo()
    {
        using var a = new Ambiente();
        var l = await a.Despesa(50m, D(2026, 10, 10));
        await Assert.ThrowsAsync<RegraNegocioException>(() => a.Lancamentos.Cancelar(l.Id, " "));
    }

    [Fact]
    public async Task Exclusao_fisica_e_bloqueada_no_codigo_e_no_banco()
    {
        using var a = new Ambiente();
        var l = await a.Despesa(50m, D(2026, 10, 10));

        a.Db.Lancamentos.Remove(l);
        await Assert.ThrowsAsync<RegraNegocioException>(() => a.Db.SaveChangesAsync());
        a.Db.ChangeTracker.Clear();

        await Assert.ThrowsAnyAsync<Exception>(() => a.Db.Database.ExecuteSqlRawAsync("DELETE FROM Lancamentos"));
        await Assert.ThrowsAnyAsync<Exception>(() => a.Db.Database.ExecuteSqlRawAsync("DELETE FROM AuditLogs"));
        await Assert.ThrowsAnyAsync<Exception>(() => a.Db.Database.ExecuteSqlRawAsync("UPDATE AuditLogs SET Motivo = 'x'"));
        Assert.Equal(1, await a.Db.Lancamentos.CountAsync());
    }
}
