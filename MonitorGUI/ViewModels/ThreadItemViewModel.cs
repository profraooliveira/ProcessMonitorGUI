using SO.Monitor.Dominio.Modelo;

namespace MonitorGUI.ViewModels;

/// <summary>
/// Retrato somente-leitura de uma <see cref="ThreadDoProcesso"/> para exibição na DataGrid de
/// detalhe. Ao contrário da lista de processos (reconciliada por PID a cada ciclo do monitor), a
/// lista de threads é substituída por completo a cada seleção de processo: a leitura fina de
/// threads (<c>IFonteDeDetalhesDeThreads</c>) é sob demanda, não contínua — não há "ciclo" para
/// reconciliar contra.
/// </summary>
public sealed record ThreadItemViewModel(
    long Tid,
    string TidTexto,
    string? TidTooltip,
    string EstadoTexto,
    string MotivoDeBloqueioTexto,
    int? Prioridade,
    TimeSpan? TempoDeCpu)
{
    public static ThreadItemViewModel De(ThreadDoProcesso thread) => new(
        thread.Tid.Valor,
        RotulosPtBr.TextoTid(thread.Tid.Valor, thread.TidEhPosicional),
        RotulosPtBr.TooltipTid(thread.TidEhPosicional),
        RotulosPtBr.Texto(thread.EstadoThread),
        RotulosPtBr.TextoMotivoDeBloqueio(thread.MotivoDeBloqueio, thread.EstadoThread),
        thread.PrioridadeBase.TemValor ? thread.PrioridadeBase.Valor : null,
        thread.TempoDeCpu.TemValor ? thread.TempoDeCpu.Valor : null);
}
