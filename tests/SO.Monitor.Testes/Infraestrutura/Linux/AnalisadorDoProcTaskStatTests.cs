using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.ValueObjects;
using SO.Monitor.Infraestrutura.Linux;
using Xunit;

namespace SO.Monitor.Testes.Infraestrutura.Linux;

/// <summary>
/// Cobre o mapeamento de estado de <c>/proc/&lt;pid&gt;/task/&lt;tid&gt;/stat</c>, com atenção
/// especial ao campo <c>comm</c> patológico (nome de thread com espaços e parênteses internos) e
/// à conversão de ticks de relógio para <see cref="TimeSpan"/>.
/// </summary>
public class AnalisadorDoProcTaskStatTests
{
    /// <summary>
    /// Monta uma linha de stat sintética com o estado, utime, stime e priority informados — os
    /// únicos campos que este analisador lê — preenchendo os demais com valores plausíveis.
    /// Campos, 1-indexados: 1 pid, 2 (comm), 3 state, 4 ppid, 5 pgrp, 6 session, 7 tty_nr,
    /// 8 tpgid, 9 flags, 10 minflt, 11 cminflt, 12 majflt, 13 cmajflt, 14 utime, 15 stime,
    /// 16 cutime, 17 cstime, 18 priority, 19 nice, 20 num_threads.
    /// </summary>
    private static string MontarLinha(string comm, char estado, long utime, long stime, int prioridade) =>
        $"4321 ({comm}) {estado} 1 1 1 0 -1 4194304 0 0 0 0 {utime} {stime} 0 0 {prioridade} 0 1 0";

    [Theory]
    [InlineData('R', EstadoThread.EmExecucao)]
    [InlineData('S', EstadoThread.Bloqueada)]
    [InlineData('D', EstadoThread.Bloqueada)]
    [InlineData('T', EstadoThread.Bloqueada)]
    [InlineData('t', EstadoThread.Bloqueada)]
    [InlineData('Z', EstadoThread.Terminada)]
    [InlineData('I', EstadoThread.Bloqueada)]
    [InlineData('X', EstadoThread.Terminada)]
    [InlineData('W', EstadoThread.Desconhecido)] // caractere fora do vocabulário documentado do kernel
    public void TentarAnalisar_MapeiaOEstadoPrincipal(char estadoBruto, EstadoThread estadoEsperado)
    {
        var linha = MontarLinha("minhathread", estadoBruto, utime: 50, stime: 10, prioridade: 20);

        var sucesso = AnalisadorDoProcTaskStat.TentarAnalisar(linha, new Tid(4321), out var thread);

        Assert.True(sucesso);
        Assert.Equal(estadoEsperado, thread!.EstadoThread);
    }

    [Theory]
    [InlineData('D', MotivoDeBloqueio.EsperandoES)] // uninterruptible disk sleep — a própria E/S bloqueante do Tanenbaum
    [InlineData('S', MotivoDeBloqueio.EsperandoEvento)]
    [InlineData('I', MotivoDeBloqueio.EsperandoEvento)]
    [InlineData('T', MotivoDeBloqueio.Suspensa)]
    public void TentarAnalisar_QuandoBloqueada_MapeiaOMotivoDeBloqueio(char estadoBruto, MotivoDeBloqueio motivoEsperado)
    {
        var linha = MontarLinha("minhathread", estadoBruto, utime: 0, stime: 0, prioridade: 20);

        AnalisadorDoProcTaskStat.TentarAnalisar(linha, new Tid(1), out var thread);

        Assert.True(thread!.MotivoDeBloqueio.TemValor);
        Assert.Equal(motivoEsperado, thread.MotivoDeBloqueio.Valor);
    }

    [Fact]
    public void TentarAnalisar_QuandoEmExecucao_MotivoDeBloqueioNaoSeAplica()
    {
        // Thread não bloqueada: o motivo não é uma limitação de plataforma (NaoSuportadoNaPlataforma),
        // é que a pergunta "por que está bloqueada" não se aplica a uma thread em execução.
        var linha = MontarLinha("minhathread", 'R', utime: 0, stime: 0, prioridade: 20);

        AnalisadorDoProcTaskStat.TentarAnalisar(linha, new Tid(1), out var thread);

        Assert.False(thread!.MotivoDeBloqueio.TemValor);
        Assert.Equal(Disponibilidade.NaoSeAplica, thread.MotivoDeBloqueio.Estado);
    }

    [Fact]
    public void TentarAnalisar_ComCommContendoEspacosEParenteses_UsaOUltimoFechaParenteseComoLimite()
    {
        // Nome de thread patológico: "meu app) legal" contém tanto espaço quanto ')' interno.
        // A técnica documentada do procfs é procurar o ÚLTIMO ')' da linha, não o primeiro.
        var linha = MontarLinha("meu app) legal", 'S', utime: 50, stime: 10, prioridade: 20);

        var sucesso = AnalisadorDoProcTaskStat.TentarAnalisar(linha, new Tid(4321), out var thread);

        Assert.True(sucesso);
        Assert.Equal(EstadoThread.Bloqueada, thread!.EstadoThread);
        Assert.Equal(20, thread.PrioridadeBase.Valor);
    }

    [Fact]
    public void TentarAnalisar_TidVemDoParametroInformadoPeloChamador_NaoDoConteudoDoArquivo()
    {
        var linha = MontarLinha("thread", 'R', utime: 0, stime: 0, prioridade: 0);

        AnalisadorDoProcTaskStat.TentarAnalisar(linha, new Tid(99999), out var thread);

        Assert.Equal(new Tid(99999), thread!.Tid);
    }

    [Fact]
    public void TentarAnalisar_UtimeEStimeEmTicksDeRelogio_ConvertePara100TicksPorSegundo()
    {
        // 50 + 10 ticks a 100 Hz = 60 ticks = 0,6 segundo.
        var linha = MontarLinha("thread", 'R', utime: 50, stime: 10, prioridade: 0);

        AnalisadorDoProcTaskStat.TentarAnalisar(linha, new Tid(1), out var thread);

        Assert.True(thread!.TempoDeCpu.TemValor);
        Assert.Equal(TimeSpan.FromSeconds(0.6), thread.TempoDeCpu.Valor);
    }

    [Fact]
    public void TentarAnalisar_PrioridadeBase_LeDoCampo18()
    {
        var linha = MontarLinha("thread", 'R', utime: 0, stime: 0, prioridade: -42);

        AnalisadorDoProcTaskStat.TentarAnalisar(linha, new Tid(1), out var thread);

        Assert.True(thread!.PrioridadeBase.TemValor);
        Assert.Equal(-42, thread.PrioridadeBase.Valor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("linha sem parenteses nenhum")]
    [InlineData("123 (comm sem fechamento S 1 1")]
    [InlineData("123 (sh) S 1")] // parênteses ok, mas campos insuficientes para chegar em priority
    public void TentarAnalisar_LinhasMalformadas_DevolveFalseSemLancar(string linha)
    {
        var sucesso = AnalisadorDoProcTaskStat.TentarAnalisar(linha, new Tid(1), out var thread);

        Assert.False(sucesso);
        Assert.Null(thread);
    }
}
