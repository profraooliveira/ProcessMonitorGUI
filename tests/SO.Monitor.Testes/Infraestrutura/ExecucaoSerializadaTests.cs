using SO.Monitor.Infraestrutura;
using Xunit;

namespace SO.Monitor.Testes.Infraestrutura;

/// <summary>
/// Cobre <see cref="ExecucaoSerializada{T}"/>: a defesa em profundidade que
/// <see cref="FonteDeProcessosDotNet"/> usa para nunca deixar duas coletas rodarem ao mesmo tempo,
/// mesmo que dois chamadores a invoquem concorrentemente por engano — a corrida de coletas que
/// mutava <c>_cacheDeCpuPorPid</c> sem proteção. Testado aqui com uma operação falsa e lenta
/// (Task.Delay respeitando o token), sem depender de Process.GetProcesses real.
/// </summary>
public class ExecucaoSerializadaTests
{
    [Fact]
    public async Task ExecutarAsync_ChamadasConcorrentes_NuncaExecutamSobrepostas()
    {
        var emExecucao = 0;
        var maximoDeReentrancia = 0;
        var trava = new object();

        async ValueTask<int> OperacaoLenta(CancellationToken cancellationToken)
        {
            var atual = Interlocked.Increment(ref emExecucao);
            lock (trava)
            {
                if (atual > maximoDeReentrancia)
                    maximoDeReentrancia = atual;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(60), cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref emExecucao);
            }

            return atual;
        }

        using var serializador = new ExecucaoSerializada<int>(OperacaoLenta);

        await Task.WhenAll(
            serializador.ExecutarAsync(CancellationToken.None).AsTask(),
            serializador.ExecutarAsync(CancellationToken.None).AsTask());

        Assert.Equal(1, maximoDeReentrancia);
    }

    [Fact]
    public async Task ExecutarAsync_ChamadaUnica_DevolveOResultadoDaOperacao()
    {
        using var serializador = new ExecucaoSerializada<int>(_ => ValueTask.FromResult(42));

        var resultado = await serializador.ExecutarAsync(CancellationToken.None);

        Assert.Equal(42, resultado);
    }

    [Fact]
    public async Task ExecutarAsync_TokenJaCancelado_LancaSemExecutarAOperacao()
    {
        var operacaoFoiChamada = false;

        using var serializador = new ExecucaoSerializada<int>(_ =>
        {
            operacaoFoiChamada = true;
            return ValueTask.FromResult(1);
        });

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await serializador.ExecutarAsync(cts.Token));

        Assert.False(operacaoFoiChamada);
    }
}
