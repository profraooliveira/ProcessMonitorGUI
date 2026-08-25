using CommunityToolkit.Mvvm.ComponentModel;
using SO.Monitor.Dominio.Modelo;

namespace MonitorGUI.ViewModels;

/// <summary>
/// Retrato de uma <see cref="ThreadDoProcesso"/> para exibição na DataGrid de detalhe. A mesma
/// instância é reaproveitada entre atualizações (reconciliação por <see cref="Tid"/> feita em
/// <see cref="ReconciliadorDeThreads"/>) — <see cref="AtualizarDe"/> atualiza os valores no lugar,
/// preservando a instância (e a posição de rolagem da grade) em vez de recriar cada item, mesmo
/// padrão já usado por <see cref="ProcessoItemViewModel"/>.
/// <para/>
/// No macOS, <see cref="Tid"/> é POSICIONAL (a posição ordinal na saída do <c>ps</c>, não um
/// identificador de kernel real — ver <c>AnalisadorDeSaidaDoPs</c>), então a "identidade"
/// reconciliada aqui é um slot da lista, não a thread do SO em si: se a thread #0 morrer, todas as
/// posições seguintes deslocam e uma linha pode passar a descrever outra thread entre uma
/// atualização e outra (inclusive <see cref="TempoDeCpu"/> "voltando" no tempo). Já é honestamente
/// rotulado como "#N (posicional)" na interface — isso só documenta a limitação explicitamente
/// agora que a reconciliação depende dela. No Linux o TID é real e a identidade é genuína.
/// </summary>
public sealed partial class ThreadItemViewModel : ObservableObject
{
    /// <summary>Chave de reconciliação — nunca muda após a construção (ver ressalva de TID posicional acima).</summary>
    public long Tid { get; }

    public string TidTexto { get; }

    public string? TidTooltip { get; }

    [ObservableProperty]
    private string _estadoTexto;

    [ObservableProperty]
    private string _motivoDeBloqueioTexto;

    [ObservableProperty]
    private int? _prioridade;

    [ObservableProperty]
    private TimeSpan? _tempoDeCpu;

    public ThreadItemViewModel(ThreadDoProcesso thread)
    {
        Tid = thread.Tid.Valor;
        TidTexto = RotulosPtBr.TextoTid(thread.Tid.Valor, thread.TidEhPosicional);
        TidTooltip = RotulosPtBr.TooltipTid(thread.TidEhPosicional);
        _estadoTexto = string.Empty;
        _motivoDeBloqueioTexto = string.Empty;
        AtualizarDe(thread);
    }

    /// <summary>Atualiza os campos que variam entre leituras a partir da mesma thread (mesmo <see cref="Tid"/>).</summary>
    public void AtualizarDe(ThreadDoProcesso thread)
    {
        EstadoTexto = RotulosPtBr.Texto(thread.EstadoThread);
        MotivoDeBloqueioTexto = RotulosPtBr.TextoMotivoDeBloqueio(thread.MotivoDeBloqueio, thread.EstadoThread);
        Prioridade = thread.PrioridadeBase.TemValor ? thread.PrioridadeBase.Valor : null;
        TempoDeCpu = thread.TempoDeCpu.TemValor ? thread.TempoDeCpu.Valor : null;
    }
}
