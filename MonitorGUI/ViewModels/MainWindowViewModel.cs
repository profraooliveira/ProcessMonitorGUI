using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SO.Monitor.Aplicacao.CasosDeUso;
using SO.Monitor.Aplicacao.Ports;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;

namespace MonitorGUI.ViewModels;

/// <summary>
/// Orquestra o laço de monitoramento e o estado de seleção da janela principal. Consome apenas
/// os casos de uso da Aplicação (<see cref="MonitorDeProcessos"/>, <see cref="ObterMapaDeMemoria"/>)
/// e a porta <see cref="IFonteDeDetalhesDeThreads"/> — nunca fala diretamente com o sistema
/// operacional nem conhece qual implementação de Infraestrutura está por trás de cada porta
/// (isso é decidido no composition root, <c>App.axaml.cs</c>, via <c>FabricaDeFontes</c>).
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    /// <summary>Regiões de memória demais poluem o mapa visual; as maiores por extensão ganham bloco próprio, o resto é agregado.</summary>
    private const int MaximoDeRegioesExibidas = 120;

    private readonly MonitorDeProcessos _monitorDeProcessos;
    private readonly ObterMapaDeMemoria _obterMapaDeMemoria;
    private readonly IFonteDeDetalhesDeThreads _fonteDeDetalhesDeThreads;

    private readonly Dictionary<int, ProcessoItemViewModel> _itensPorPid = new();

    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _cancelamentoSelecao;

    /// <summary>
    /// Task do laço de monitoramento em execução no momento (<see cref="ExecutarLoopAsync"/>).
    /// Guardada para que <see cref="ReiniciarLoopAsync"/> possa AGUARDAR o laço anterior realmente
    /// terminar antes de disparar um novo — sem isso, dois laços concorrentes chamariam
    /// <c>ColetarAsync</c> ao mesmo tempo, violando a premissa de uso sequencial da fonte de
    /// processos (ver <c>FonteDeProcessosDotNet</c>).
    /// </summary>
    private Task? _loopTask;

    private TamanhoPagina _tamanhoDePaginaAtual = TamanhoPagina.Kib4;

    public ObservableCollection<ProcessoItemViewModel> Processos { get; } = new();

    public ObservableCollection<ThreadItemViewModel> ThreadsSelecionadas { get; } = new();

    public ObservableCollection<BlocoDeMemoriaViewModel> BlocosDeMemoria { get; } = new();

    [ObservableProperty]
    private ProcessoItemViewModel? _processoSelecionado;

    [ObservableProperty]
    private BlocoDeMemoriaViewModel? _blocoSelecionado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartMonitoringCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopMonitoringCommand))]
    private bool _isMonitoring;

    [ObservableProperty]
    private int _intervalMs = 3000;

    [ObservableProperty]
    private string _statusMessage = "Monitor pronto.";

    [ObservableProperty]
    private string _tituloColunaPaginas = "Páginas";

    [ObservableProperty]
    private string _rotuloOrigemDoMapa = string.Empty;

    [ObservableProperty]
    private string? _motivoDoFallbackTexto;

    public IReadOnlyList<OpcaoFiltro> OpcoesFiltro { get; } =
    [
        new("Todos", null),
        new("CPU-bound", PerfilDeExecucao.LimitadoPorCpu),
        new("E/S-bound", PerfilDeExecucao.LimitadoPorES)
    ];

    [ObservableProperty]
    private OpcaoFiltro _filtroSelecionado;

    public int[] OpcoesLimite { get; } = [15, 30, 50, 100, 500];

    [ObservableProperty]
    private int _limiteSelecionado = 15;

    public MainWindowViewModel(
        MonitorDeProcessos monitorDeProcessos,
        ObterMapaDeMemoria obterMapaDeMemoria,
        IFonteDeDetalhesDeThreads fonteDeDetalhesDeThreads)
    {
        _monitorDeProcessos = monitorDeProcessos;
        _obterMapaDeMemoria = obterMapaDeMemoria;
        _fonteDeDetalhesDeThreads = fonteDeDetalhesDeThreads;
        _filtroSelecionado = OpcoesFiltro[0];
    }

    private bool PodeIniciar => !IsMonitoring;

    private bool PodeParar => IsMonitoring;

    [RelayCommand(CanExecute = nameof(PodeIniciar))]
    private async Task StartMonitoringAsync() => await ReiniciarLoopAsync();

    [RelayCommand(CanExecute = nameof(PodeParar))]
    private void StopMonitoring()
    {
        _cts?.Cancel();
        IsMonitoring = false;
        StatusMessage = "Monitor pausado.";
    }

    [RelayCommand]
    private async Task IncreaseIntervalAsync()
    {
        IntervalMs = Math.Min(IntervalMs + 500, 10000);
        await ReiniciarSeMonitorandoAsync();
    }

    [RelayCommand]
    private async Task DecreaseIntervalAsync()
    {
        IntervalMs = Math.Max(IntervalMs - 500, 500);
        await ReiniciarSeMonitorandoAsync();
    }

    /// <summary>Botão explícito "Atualizar mapa": o mapa de memória NÃO é regenerado a cada tick do monitor, só aqui e na troca de seleção.</summary>
    [RelayCommand]
    private async Task AtualizarMapaAsync()
    {
        if (ProcessoSelecionado is null)
            return;

        try
        {
            // Reaproveita o token de cancelamento da seleção corrente (não CancellationToken.None):
            // se o usuário trocar de processo enquanto este mapa ainda está carregando, o botão
            // "Atualizar mapa" não deve deixar um resultado obsoleto sobrescrever a seleção nova.
            var token = _cancelamentoSelecao?.Token ?? CancellationToken.None;
            await AtualizarMapaDeMemoriaAsync(ProcessoSelecionado, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Seleção trocada antes do mapa terminar de carregar — mesmo caminho silencioso de
            // CarregarDetalhesDoProcessoSelecionadoAsync.
        }
        catch (Exception excecao)
        {
            await Dispatcher.UIThread.InvokeAsync(() => StatusMessage = $"Erro ao atualizar mapa de memória: {excecao.Message}");
        }
    }

    /// <summary>
    /// Dispara o reinício assíncrono do laço sem aguardá-lo: métodos parciais gerados pelo
    /// CommunityToolkit.Mvvm para <c>On&lt;Propriedade&gt;Changed</c> são obrigatoriamente
    /// <c>void</c> (não podem retornar <see cref="Task"/>), então não há como propagar um
    /// <c>await</c> daqui. A tarefa descartada trata todas as próprias exceções internamente via
    /// <see cref="ReiniciarLoopAsync"/>/<see cref="ExecutarLoopAsync"/> — mesmo padrão documentado
    /// em <see cref="OnProcessoSelecionadoChanged"/>.
    /// </summary>
    partial void OnFiltroSelecionadoChanged(OpcaoFiltro value) => _ = ReiniciarSeMonitorandoAsync();

    partial void OnLimiteSelecionadoChanged(int value) => _ = ReiniciarSeMonitorandoAsync();

    /// <summary>
    /// Ao trocar de processo selecionado, threads e mapa de memória são recarregados sob
    /// demanda. A tarefa é deliberadamente descartada (não é um "fire-and-forget" cego: ela
    /// trata todas as próprias exceções internamente e nunca deixa nada escapar sem passar por
    /// <see cref="StatusMessage"/>) porque uma propriedade observável não pode ter um setter
    /// assíncrono — este é o padrão idiomático do CommunityToolkit.Mvvm para esse caso.
    /// </summary>
    partial void OnProcessoSelecionadoChanged(ProcessoItemViewModel? value)
    {
        _cancelamentoSelecao?.Cancel();
        _cancelamentoSelecao?.Dispose();
        _cancelamentoSelecao = new CancellationTokenSource();

        _ = CarregarDetalhesDoProcessoSelecionadoAsync(value, _cancelamentoSelecao.Token);
    }

    private async Task CarregarDetalhesDoProcessoSelecionadoAsync(ProcessoItemViewModel? item, CancellationToken cancellationToken)
    {
        try
        {
            await AtualizarThreadsAsync(item, cancellationToken).ConfigureAwait(false);
            await AtualizarMapaDeMemoriaAsync(item, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Seleção trocada antes de terminar de carregar os detalhes anteriores — descarta o resultado obsoleto.
        }
        catch (Exception excecao)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                StatusMessage = $"Erro ao carregar detalhes do processo: {excecao.Message}");
        }
    }

    private void SelecionarBloco(BlocoDeMemoriaViewModel bloco)
    {
        if (BlocoSelecionado is not null)
            BlocoSelecionado.IsSelected = false;

        bloco.IsSelected = true;
        BlocoSelecionado = bloco;
    }

    private CriteriosDeAmostragem ObterCriterios() => new(FiltroSelecionado.Perfil, LimiteSelecionado);

    /// <summary>
    /// Cancela o laço anterior (se algum estiver rodando) e AGUARDA ele terminar antes de disparar
    /// um novo — a correção da corrida de coletas: sem esperar, o laço antigo (ainda desenrolando
    /// sua última iteração) e o laço novo podiam chamar <c>ColetarAsync</c> ao mesmo tempo,
    /// violando a premissa de uso sequencial da fonte de processos (ver
    /// <c>FonteDeProcessosDotNet</c>). O <c>await</c> aqui é direto (sem <c>ConfigureAwait(false)</c>)
    /// de propósito: este método é sempre chamado a partir de um contexto de UI (comando ou
    /// handler de mudança de propriedade), e queremos retomar no thread de UI depois do await para
    /// que <see cref="IsMonitoring"/>/<see cref="StatusMessage"/> continuem sendo atualizados sem
    /// precisar de <c>Dispatcher.UIThread</c> explícito aqui — não deadlocka porque
    /// <see cref="ExecutarLoopAsync"/> nunca bloqueia esperando por este mesmo método.
    /// </summary>
    private async Task ReiniciarLoopAsync()
    {
        _cts?.Cancel();

        if (_loopTask is not null)
            await _loopTask;

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        IsMonitoring = true;
        StatusMessage = "Monitorando...";

        // Descartada deliberadamente, pelo mesmo motivo documentado em OnProcessoSelecionadoChanged:
        // ExecutarLoopAsync trata todas as próprias exceções internamente (nunca deixa nada
        // escapar sem passar por StatusMessage). Guardada em _loopTask (não descartada de fato)
        // justamente para que o PRÓXIMO reinício possa aguardá-la.
        _loopTask = ExecutarLoopAsync(_cts.Token);
    }

    private async Task ReiniciarSeMonitorandoAsync()
    {
        if (IsMonitoring)
            await ReiniciarLoopAsync();
    }

    /// <summary>
    /// O laço principal: um único <c>await foreach</c> sobre <see cref="MonitorDeProcessos.ObservarAsync"/>.
    /// Não há <c>Task.Run</c> AQUI porque a espera entre ciclos já é assíncrona por natureza
    /// (PeriodicTimer) — mas isso descreve só a ESPERA, não o TRABALHO: a coleta em si (enumerar
    /// processos do sistema, ler cada campo) é potencialmente lenta e síncrona por baixo, então o
    /// <c>Task.Run</c> que a torna não-bloqueante para a UI vive na fronteira de infraestrutura
    /// (<c>FonteDeProcessosDotNet.ColetarAsync</c>), não aqui — é lá que o trabalho de fato
    /// acontece. Uma exceção do laço nunca é engolida: ela sempre vira <see cref="StatusMessage"/>
    /// visível ao usuário.
    /// </summary>
    private async Task ExecutarLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            var criterios = ObterCriterios();
            var intervalo = TimeSpan.FromMilliseconds(IntervalMs);

            await foreach (var amostra in _monitorDeProcessos.ObservarAsync(intervalo, criterios, cancellationToken))
            {
                var amostraAtual = amostra;
                await Dispatcher.UIThread.InvokeAsync(() => ReconciliarProcessos(amostraAtual));
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelamento limpo (Parar, ou reinício por troca de intervalo/filtro/limite) — não é uma falha.
        }
        catch (Exception excecao)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusMessage = $"Erro no monitor: {excecao.Message}";
                IsMonitoring = false;
            });
        }
    }

    /// <summary>
    /// Concilia a coleção observável com a nova amostra por PID: atualiza itens existentes no
    /// lugar, adiciona os novos, remove os ausentes — nunca <c>Clear()+re-Add()</c>. É essa
    /// preservação de instância que corrige o bug histórico de perda de seleção/rolagem na
    /// DataGrid a cada ciclo do monitor. Deve rodar na UI thread (chamado via Dispatcher).
    /// </summary>
    private void ReconciliarProcessos(AmostraDoSistema amostra)
    {
        _tamanhoDePaginaAtual = amostra.TamanhoDePaginaDoSistema;
        TituloColunaPaginas = $"Páginas ({amostra.TamanhoDePaginaDoSistema.EmBytes / 1024} KiB)";

        ReconciliadorDeProcessos.Reconciliar(Processos, _itensPorPid, amostra);

        AtualizarStatusMessage(amostra);
    }

    private void AtualizarStatusMessage(AmostraDoSistema amostra)
    {
        var horario = amostra.InstanteDaColeta.ToLocalTime().ToString("HH:mm:ss");
        var paginaKib = amostra.TamanhoDePaginaDoSistema.EmBytes / 1024;
        StatusMessage =
            $"Coletados {amostra.Processos.Count} processos ({amostra.ProcessosInacessiveis} protegidos pelo kernel) " +
            $"em {horario} — página de {paginaKib} KiB";
    }

    /// <summary>
    /// Busca as threads reais do processo selecionado; quando a leitura fina não tem valor
    /// (plataforma sem provedor, processo protegido), recorre às threads já presentes na
    /// amostra coletada — fallback documentado, nunca uma lista vazia silenciosa.
    /// </summary>
    private async Task AtualizarThreadsAsync(ProcessoItemViewModel? item, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            await Dispatcher.UIThread.InvokeAsync(ThreadsSelecionadas.Clear);
            return;
        }

        var leitura = await _fonteDeDetalhesDeThreads.ObterThreadsAsync(item.Processo.Pid, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var threadsFonte = leitura.TemValor ? leitura.Valor! : item.Processo.Threads;
        var itensDeThread = threadsFonte.Select(ThreadItemViewModel.De).ToList();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ThreadsSelecionadas.Clear();
            foreach (var threadItem in itensDeThread)
                ThreadsSelecionadas.Add(threadItem);
        });
    }

    /// <summary>
    /// Carrega o mapa de memória uma única vez (na troca de seleção, ou no botão "Atualizar
    /// mapa"): nunca a cada tick do monitor — é isso que corrige o bug do painel que se apagava
    /// a cada ciclo e mantém o bloco selecionado vivo entre atualizações da lista de processos.
    /// </summary>
    private async Task AtualizarMapaDeMemoriaAsync(ProcessoItemViewModel? item, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                BlocosDeMemoria.Clear();
                BlocoSelecionado = null;
                RotuloOrigemDoMapa = string.Empty;
                MotivoDoFallbackTexto = null;
            });
            return;
        }

        var resultado = await _obterMapaDeMemoria.ExecutarAsync(
            item.Processo.Pid,
            item.Processo.PerfilDeMemoria,
            _tamanhoDePaginaAtual,
            cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var blocos = ConstruirBlocos(resultado.Mapa.Regioes, SelecionarBloco);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            BlocosDeMemoria.Clear();
            foreach (var bloco in blocos)
                BlocosDeMemoria.Add(bloco);

            BlocoSelecionado = null;

            RotuloOrigemDoMapa = RotulosPtBr.RotuloDeOrigemDoMapa(resultado.Mapa.OrigemDosDados);

            MotivoDoFallbackTexto = resultado.MotivoDoFallback is { } motivo
                ? RotulosPtBr.DescreverFallbackDeMapa(motivo)
                : null;
        });
    }

    /// <summary>
    /// Dois critérios de ordenação diferentes, deliberadamente não fundidos em um único
    /// <c>OrderBy</c>: o CORTE de quais regiões ganham bloco individual é por extensão
    /// decrescente (as maiores primeiro, até <see cref="MaximoDeRegioesExibidas"/>) — é o que
    /// mantém o mapa legível; mas a EXIBIÇÃO final é por endereço crescente
    /// (<see cref="FaixaDeEnderecos.Inicio"/>), porque é a ordem por endereço que ensina o layout
    /// real do espaço de endereçamento virtual do processo. O bloco agregado ("…+K regiões
    /// menores") não tem um endereço próprio, então fica sempre por último.
    /// </summary>
    internal static List<BlocoDeMemoriaViewModel> ConstruirBlocos(
        IReadOnlyList<RegiaoDeMemoria> regioes,
        Action<BlocoDeMemoriaViewModel> aoSelecionar)
    {
        var ordenadasPorExtensao = regioes.OrderByDescending(regiao => regiao.FaixaDeEnderecos.Extensao.Valor).ToList();

        var maioresRegioes = ordenadasPorExtensao.Take(MaximoDeRegioesExibidas);
        var restantes = ordenadasPorExtensao.Skip(MaximoDeRegioesExibidas).ToList();

        var resultado = maioresRegioes
            .OrderBy(regiao => regiao.FaixaDeEnderecos.Inicio.Valor)
            .Select(regiao => new BlocoDeMemoriaViewModel(regiao, aoSelecionar))
            .ToList();

        if (restantes.Count > 0)
            resultado.Add(BlocoDeMemoriaViewModel.CriarAgregado(restantes, aoSelecionar));

        return resultado;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cancelamentoSelecao?.Cancel();
        _cancelamentoSelecao?.Dispose();
        GC.SuppressFinalize(this);
    }
}
