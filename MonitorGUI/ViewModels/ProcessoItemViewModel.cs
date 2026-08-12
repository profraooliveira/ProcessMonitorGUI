using CommunityToolkit.Mvvm.ComponentModel;
using SO.Monitor.Dominio.Modelo;

namespace MonitorGUI.ViewModels;

/// <summary>
/// Embrulha um <see cref="Processo"/> do domínio para exibição na DataGrid mestre, expondo
/// propriedades tipadas e observáveis. Peça central da correção do bug de scroll/seleção: a
/// mesma instância é reaproveitada entre ciclos do monitor (reconciliação por PID feita em
/// <see cref="MainWindowViewModel"/>) — <see cref="AtualizarDe"/> atualiza os valores no lugar,
/// preservando a instância (e, com ela, a seleção e a posição de rolagem da DataGrid) em vez de
/// recriar o item a cada amostra, como fazia o antigo Clear()+re-Add().
/// </summary>
public sealed partial class ProcessoItemViewModel : ObservableObject
{
    /// <summary>PID do processo — chave de reconciliação; nunca muda após a construção.</summary>
    public int Pid { get; }

    [ObservableProperty]
    private string _nome = string.Empty;

    [ObservableProperty]
    private string _estadoProcessoTexto = string.Empty;

    [ObservableProperty]
    private string _perfilTexto = string.Empty;

    /// <summary>O "porquê" da classificação — pensado para virar tooltip didático na interface.</summary>
    [ObservableProperty]
    private string _criterioClassificacao = string.Empty;

    [ObservableProperty]
    private double _porcentagemCpu;

    /// <summary>Conjunto residente em bytes, ou <c>null</c> quando a leitura não tem valor.</summary>
    [ObservableProperty]
    private long? _memoriaResidente;

    /// <summary>Páginas residentes, ou <c>null</c> quando a leitura não tem valor.</summary>
    [ObservableProperty]
    private long? _paginas;

    [ObservableProperty]
    private int _threadCount;

    /// <summary>Contagem de handles/descritores abertos, ou <c>null</c> quando indisponível nesta plataforma.</summary>
    [ObservableProperty]
    private int? _handleCount;

    [ObservableProperty]
    private DateTimeOffset? _inicioDaExecucao;

    /// <summary>Processo de domínio mais recente — usado pelo VM pai para carregar threads/mapa sob demanda.</summary>
    public Processo Processo { get; private set; }

    public ProcessoItemViewModel(Processo processo)
    {
        Pid = processo.Pid.Valor;
        Processo = processo;
        AtualizarDe(processo);
    }

    /// <summary>
    /// Atualiza todas as propriedades observáveis a partir de uma nova amostra do mesmo
    /// processo (mesmo PID) — o coração da reconciliação: nunca recria o item, só atualiza.
    /// </summary>
    public void AtualizarDe(Processo processo)
    {
        Processo = processo;
        Nome = processo.Nome;
        EstadoProcessoTexto = RotulosPtBr.Texto(processo.EstadoProcesso);
        PerfilTexto = processo.Classificacao is { } classificacao
            ? RotulosPtBr.Texto(classificacao.Perfil)
            : "Não classificado";
        CriterioClassificacao = processo.Classificacao?.Criterio ?? "Ainda não classificado nesta amostra.";
        PorcentagemCpu = processo.MetricasDeExecucao.UsoDeCpu.Valor;
        MemoriaResidente = processo.PerfilDeMemoria.ConjuntoResidente.TemValor
            ? processo.PerfilDeMemoria.ConjuntoResidente.Valor.Valor
            : null;
        Paginas = processo.PerfilDeMemoria.PaginasResidentes.TemValor
            ? processo.PerfilDeMemoria.PaginasResidentes.Valor
            : null;
        ThreadCount = processo.MetricasDeExecucao.TotalDeThreads;
        HandleCount = processo.MetricasDeExecucao.ContagemDeHandles.TemValor
            ? processo.MetricasDeExecucao.ContagemDeHandles.Valor
            : null;
        InicioDaExecucao = processo.InicioDaExecucao.TemValor
            ? processo.InicioDaExecucao.Valor
            : null;
    }
}
