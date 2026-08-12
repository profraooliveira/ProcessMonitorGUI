using SO.Monitor.Dominio.Enums;
using ThreadState = System.Diagnostics.ThreadState;

namespace SO.Monitor.Infraestrutura;

/// <summary>
/// Traduz os enums de thread do <c>System.Diagnostics</c> (a visão do Windows sobre threads,
/// historicamente) para o vocabulário do domínio. Isolado em uma classe própria e <c>internal</c>
/// para ser testável diretamente (via <c>InternalsVisibleTo</c>), sem depender de threads reais
/// do sistema operacional para cobrir cada ramo do mapeamento.
/// </summary>
internal static class MapeadorDeEstadoDeThread
{
    /// <summary>
    /// Mapeia o estado bruto de <see cref="System.Diagnostics.ProcessThread.ThreadState"/> para
    /// <see cref="EstadoThread"/>. Estados que o domínio não distingue (ex.: Initialized,
    /// Transition) caem em <see cref="EstadoThread.Desconhecido"/> — o domínio tem o direito de
    /// não saber, em vez de inventar uma correspondência sem lastro.
    /// </summary>
    internal static EstadoThread ParaEstadoThread(ThreadState estadoOs) => estadoOs switch
    {
        ThreadState.Running => EstadoThread.EmExecucao,
        ThreadState.Wait => EstadoThread.Bloqueada,
        ThreadState.Ready => EstadoThread.Pronta,
        ThreadState.Standby => EstadoThread.Pronta,
        ThreadState.Terminated => EstadoThread.Terminada,
        _ => EstadoThread.Desconhecido
    };

    /// <summary>
    /// Mapeia o motivo bruto de <see cref="System.Diagnostics.ProcessThread.WaitReason"/> para
    /// <see cref="MotivoDeBloqueio"/>, seguindo a classificação de Tanenbaum: paginação,
    /// sincronização (mutex/semáforo) e eventos arbitrários são motivos de bloqueio distintos.
    /// </summary>
    internal static MotivoDeBloqueio ParaMotivoDeBloqueio(System.Diagnostics.ThreadWaitReason motivoOs) => motivoOs switch
    {
        System.Diagnostics.ThreadWaitReason.PageIn => MotivoDeBloqueio.EsperandoPagina,
        System.Diagnostics.ThreadWaitReason.PageOut => MotivoDeBloqueio.EsperandoPagina,
        System.Diagnostics.ThreadWaitReason.EventPairHigh => MotivoDeBloqueio.EsperandoEvento,
        System.Diagnostics.ThreadWaitReason.EventPairLow => MotivoDeBloqueio.EsperandoEvento,
        System.Diagnostics.ThreadWaitReason.UserRequest => MotivoDeBloqueio.EsperandoEvento,
        System.Diagnostics.ThreadWaitReason.Executive => MotivoDeBloqueio.EsperandoSincronizacao,
        _ => MotivoDeBloqueio.Desconhecido
    };
}
