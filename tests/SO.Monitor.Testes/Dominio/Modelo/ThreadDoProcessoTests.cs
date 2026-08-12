using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;
using Xunit;

namespace SO.Monitor.Testes.Dominio.Modelo;

/// <summary>
/// Cobre <see cref="ThreadDoProcesso.TidEhPosicional"/>: o baseline .NET (Windows/Unix via
/// <c>System.Diagnostics.ProcessThread</c>) sempre expõe TIDs reais de kernel, então nunca marca a
/// flag — só o parser do <c>ps</c> do macOS (<c>AnalisadorDeSaidaDoPs</c>), que não tem acesso a
/// TIDs reais, marca <c>true</c> explicitamente.
/// </summary>
public class ThreadDoProcessoTests
{
    private static ThreadDoProcesso CriarThread(bool? tidEhPosicional = null)
    {
        var tid = new Tid(1);
        var motivo = Leitura<MotivoDeBloqueio>.NaoSeAplica();
        var prioridade = Leitura<int>.Ok(8);
        var tempoDeCpu = Leitura<TimeSpan>.Ok(TimeSpan.FromSeconds(1));

        return tidEhPosicional is { } valor
            ? new ThreadDoProcesso(tid, EstadoThread.EmExecucao, motivo, prioridade, tempoDeCpu, valor)
            : new ThreadDoProcesso(tid, EstadoThread.EmExecucao, motivo, prioridade, tempoDeCpu);
    }

    [Fact]
    public void TidEhPosicional_NaoInformado_PadraoEhFalse()
    {
        // Call sites que não passam o parâmetro (baseline .NET) continuam compilando e
        // reportando um TID real, não posicional.
        var thread = CriarThread();

        Assert.False(thread.TidEhPosicional);
    }

    [Fact]
    public void TidEhPosicional_InformadoExplicitamenteComoTrue_EhPreservado()
    {
        var thread = CriarThread(tidEhPosicional: true);

        Assert.True(thread.TidEhPosicional);
    }
}
