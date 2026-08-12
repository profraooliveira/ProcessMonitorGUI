using SO.Monitor.Infraestrutura;
using Xunit;

namespace SO.Monitor.Testes.Infraestrutura;

/// <summary>
/// Teste de integração real: roda em qualquer sistema operacional suportado, coletando
/// processos de verdade via <see cref="System.Diagnostics.Process"/>. Os asserts são
/// deliberadamente robustos (não fixam números exatos de processos, threads ou memória) porque
/// o ambiente de CI/execução varia — só o processo do próprio teste é uma referência estável.
/// </summary>
public class FonteDeProcessosDotNetTests
{
    [Fact]
    public async Task ColetarAsync_EncontraOProcessoAtualComDadosBasicosValidos()
    {
        var fonte = new FonteDeProcessosDotNet();
        var pidAtual = Environment.ProcessId;

        var amostra = await fonte.ColetarAsync(CancellationToken.None);

        var processoAtual = Assert.Single(amostra.Processos, processo => processo.Pid.Valor == pidAtual);
        Assert.False(string.IsNullOrWhiteSpace(processoAtual.Nome));
        Assert.True(amostra.TamanhoDePaginaDoSistema.EmBytes > 0);
        Assert.All(amostra.Processos, processo => Assert.NotNull(processo.Threads));
    }

    [Fact]
    public async Task ColetarAsync_SegundaColeta_CalculaPercentualDeCpuNaoNegativoParaOProcessoAtual()
    {
        var fonte = new FonteDeProcessosDotNet();
        var pidAtual = Environment.ProcessId;

        await fonte.ColetarAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        var segundaAmostra = await fonte.ColetarAsync(CancellationToken.None);

        var processoAtual = segundaAmostra.Processos.Single(processo => processo.Pid.Valor == pidAtual);

        Assert.True(processoAtual.MetricasDeExecucao.UsoDeCpu.Valor >= 0);
    }

    [Fact]
    public async Task ColetarAsync_NenhumProcessoTemListaDeThreadsNula()
    {
        var fonte = new FonteDeProcessosDotNet();

        var amostra = await fonte.ColetarAsync(CancellationToken.None);

        Assert.All(amostra.Processos, processo => Assert.NotNull(processo.Threads));
    }

    /// <summary>
    /// Regressão: a coleta agora roda em Task.Run (thread pool), não mais inline — este teste
    /// garante que o CancellationToken continua honrado de ponta a ponta mesmo depois dessa
    /// mudança (FIX2), tanto na entrada do semáforo (ExecucaoSerializada) quanto no laço de
    /// processos em si (checagem periódica adicionada em FIX1).
    /// </summary>
    [Fact]
    public async Task ColetarAsync_TokenJaCancelado_LancaOperationCanceledException()
    {
        using var fonte = new FonteDeProcessosDotNet();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await fonte.ColetarAsync(cts.Token));
    }

    /// <summary>
    /// Regressão da defesa em profundidade (FIX1b): duas chamadas concorrentes à mesma instância
    /// não corrompem o cache de CPU por PID nem lançam — o semáforo interno as serializa. Não dá
    /// para observar a serialização diretamente aqui (a fonte real não é "lenta" o bastante para
    /// garantir sobreposição determinística), mas o teste garante que o uso concorrente, se algum
    /// dia acontecer por engano, continua seguro em vez de corromper estado compartilhado.
    /// </summary>
    [Fact]
    public async Task ColetarAsync_DuasChamadasConcorrentes_AmbasCompletamSemLancar()
    {
        using var fonte = new FonteDeProcessosDotNet();

        var amostras = await Task.WhenAll(
            fonte.ColetarAsync(CancellationToken.None).AsTask(),
            fonte.ColetarAsync(CancellationToken.None).AsTask());

        Assert.All(amostras, amostra => Assert.NotEmpty(amostra.Processos));
    }
}
