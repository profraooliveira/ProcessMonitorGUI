using SO.Monitor.Dominio.Enums;
using SO.Monitor.Infraestrutura;
using Xunit;
using ThreadState = System.Diagnostics.ThreadState;
using ThreadWaitReason = System.Diagnostics.ThreadWaitReason;

namespace SO.Monitor.Testes.Infraestrutura;

/// <summary>
/// Cobre os ramos principais do mapeamento entre os enums de thread de
/// <c>System.Diagnostics</c> e o vocabulário do domínio, sem depender de threads reais do
/// sistema operacional (o que tornaria os testes não determinísticos).
/// </summary>
public class MapeadorDeEstadoDeThreadTests
{
    [Theory]
    [InlineData(ThreadState.Running, EstadoThread.EmExecucao)]
    [InlineData(ThreadState.Wait, EstadoThread.Bloqueada)]
    [InlineData(ThreadState.Ready, EstadoThread.Pronta)]
    [InlineData(ThreadState.Standby, EstadoThread.Pronta)]
    [InlineData(ThreadState.Terminated, EstadoThread.Terminada)]
    [InlineData(ThreadState.Initialized, EstadoThread.Desconhecido)]
    [InlineData(ThreadState.Transition, EstadoThread.Desconhecido)]
    public void ParaEstadoThread_MapeiaOsEstadosPrincipais(ThreadState estadoOs, EstadoThread esperado)
    {
        Assert.Equal(esperado, MapeadorDeEstadoDeThread.ParaEstadoThread(estadoOs));
    }

    [Theory]
    [InlineData(ThreadWaitReason.PageIn, MotivoDeBloqueio.EsperandoPagina)]
    [InlineData(ThreadWaitReason.PageOut, MotivoDeBloqueio.EsperandoPagina)]
    [InlineData(ThreadWaitReason.EventPairHigh, MotivoDeBloqueio.EsperandoEvento)]
    [InlineData(ThreadWaitReason.EventPairLow, MotivoDeBloqueio.EsperandoEvento)]
    [InlineData(ThreadWaitReason.UserRequest, MotivoDeBloqueio.EsperandoEvento)]
    [InlineData(ThreadWaitReason.Executive, MotivoDeBloqueio.EsperandoSincronizacao)]
    [InlineData(ThreadWaitReason.Suspended, MotivoDeBloqueio.Desconhecido)]
    public void ParaMotivoDeBloqueio_MapeiaOsMotivosPrincipais(ThreadWaitReason motivoOs, MotivoDeBloqueio esperado)
    {
        Assert.Equal(esperado, MapeadorDeEstadoDeThread.ParaMotivoDeBloqueio(motivoOs));
    }
}
