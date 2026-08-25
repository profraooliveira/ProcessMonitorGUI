using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
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
    private readonly MonitorDeProcessos _monitorDeProcessos;
    private readonly ObterMapaDeMemoria _obterMapaDeMemoria;
    private readonly IFonteDeDetalhesDeThreads _fonteDeDetalhesDeThreads;

    private readonly Dictionary<int, ProcessoItemViewModel> _itensPorPid = new();
    private readonly Dictionary<long, ThreadItemViewModel> _itensPorTid = new();

    /// <summary>
    /// PID cujas threads estão em <see cref="ThreadsSelecionadas"/> no momento. Ao trocar de
    /// processo, a reconciliação por TID precisa de um reset explícito ANTES de reconciliar —
    /// sem isso, no macOS (onde TIDs são posicionais 0..N para qualquer processo), a troca de
    /// processo faria a grade "morphing" os itens do processo antigo em vez de trocar de fato.
    /// </summary>
    private int? _pidDasThreadsExibidas;

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

    /// <summary>
    /// CTS/Task do laço de auto-refresh do mapa de memória (<see cref="ExecutarLoopDoMapaAsync"/>)
    /// — deliberadamente SEPARADO de <see cref="_cts"/>/<see cref="_loopTask"/>: o custo de
    /// <c>vmmap</c> (~1-1,5s medido) é ~30x o de uma coleta de processos, então a cadência do mapa
    /// não pode ser acoplada à do "Ciclo (ms)" (que pode ser configurado tão baixo quanto 500ms).
    /// </summary>
    private CancellationTokenSource? _ctsMapa;

    private Task? _loopDoMapaTask;

    /// <summary>
    /// Arbitra entre o tick do laço de auto-refresh e uma ação do usuário (botão "Atualizar
    /// mapa", troca de seleção) para nunca disparar dois <c>vmmap</c> concorrentes: o tick usa
    /// <c>WaitAsync(0)</c> e PULA se ocupado (não vale a pena enfileirar uma leitura para um
    /// estado que o usuário já passou); a ação do usuário usa <c>WaitAsync(token)</c> e ESPERA
    /// sua vez, porque ela tem prioridade sobre o laço automático.
    /// </summary>
    private readonly SemaphoreSlim _semaforoDoMapa = new(1, 1);

    private TamanhoPagina _tamanhoDePaginaAtual = TamanhoPagina.Kib4;

    /// <summary>
    /// Últimas regiões brutas lidas do processo selecionado (antes do corte de exibição) — cache
    /// local para que trocar <see cref="QuantidadeDeBlocosSelecionada"/> apenas re-fatie
    /// localmente via <see cref="ConstruirBlocos"/>, sem disparar uma nova leitura de <c>vmmap</c>
    /// (medida em ~1-1,5s por chamada — cara demais para refazer só por causa do seletor).
    /// </summary>
    private IReadOnlyList<RegiaoDeMemoria> _ultimasRegioesDoMapa = [];

    public ObservableCollection<ProcessoItemViewModel> Processos { get; } = new();

    public ObservableCollection<ThreadItemViewModel> ThreadsSelecionadas { get; } = new();

    public ObservableCollection<BlocoDeMemoriaViewModel> BlocosDeMemoria { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AtualizarMapaCommand))]
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

    /// <summary>120 preserva o comportamento anterior ao seletor (feature puramente aditiva).</summary>
    public int[] OpcoesQuantidadeDeBlocos { get; } = [30, 60, 120, 250, 500];

    [ObservableProperty]
    private int _quantidadeDeBlocosSelecionada = 120;

    [ObservableProperty]
    private bool _atualizacaoAutomaticaDoMapa = true;

    /// <summary>Mínimo de 2s: mesmo nesse piso, com vmmap custando até ~1,5s, já é ~75% de duty cycle de um subprocesso pesado.</summary>
    public int[] OpcoesIntervaloDoMapaEmSegundos { get; } = [2, 5, 10, 30];

    [ObservableProperty]
    private int _intervaloDoMapaEmSegundos = 5;

    /// <summary>Duração medida da última leitura de mapa (ex.: "1,2 s") — resposta honesta (medição, não texto) para "por que o mapa não atualiza tão rápido quanto a lista".</summary>
    [ObservableProperty]
    private string? _duracaoDaUltimaLeituraDoMapa;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AtualizarMapaCommand))]
    private bool _mapaEmAtualizacao;

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

    private bool PodeAtualizarMapa => ProcessoSelecionado is not null && !MapaEmAtualizacao;

    [RelayCommand(CanExecute = nameof(PodeIniciar))]
    private async Task StartMonitoringAsync() => await ReiniciarLoopAsync();

    [RelayCommand(CanExecute = nameof(PodeParar))]
    private void StopMonitoring()
    {
        _cts?.Cancel();
        _ctsMapa?.Cancel();
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

    /// <summary>
    /// Botão explícito "Atualizar mapa": lê sob demanda, mesmo com auto-refresh ligado — a ação
    /// do usuário tem prioridade sobre o laço automático (<see cref="ExecutarLoopDoMapaAsync"/>),
    /// arbitrada pelo <see cref="_semaforoDoMapa"/> com <c>WaitAsync(token)</c> (espera sua vez,
    /// nunca é pulada como o tick do laço seria). Nenhum <c>await</c> aqui usa <c>ConfigureAwait(false)</c>
    /// — mesmo motivo documentado em <see cref="ReiniciarLoopAsync"/>: este comando é sempre
    /// disparado a partir da UI thread, e queremos retomar nela após cada await para que
    /// <see cref="MapaEmAtualizacao"/> possa ser atribuída direto, sem <c>Dispatcher.UIThread</c>
    /// explícito (que exigiria um laço de dispatcher rodando — inexistente nos testes deste VM).
    /// </summary>
    [RelayCommand(CanExecute = nameof(PodeAtualizarMapa))]
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

            await _semaforoDoMapa.WaitAsync(token);
            try
            {
                MapaEmAtualizacao = true;
                await AtualizarMapaDeMemoriaAsync(ProcessoSelecionado, token);
            }
            finally
            {
                _semaforoDoMapa.Release();
                MapaEmAtualizacao = false;
            }
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

    /// <summary>Liga/desliga o auto-refresh do mapa; descartada pelo mesmo motivo documentado acima. ReiniciarLoopDoMapaAsync é no-op seguro se o monitor não estiver rodando.</summary>
    partial void OnAtualizacaoAutomaticaDoMapaChanged(bool value) => _ = ReiniciarLoopDoMapaAsync();

    partial void OnIntervaloDoMapaEmSegundosChanged(int value) => _ = ReiniciarLoopDoMapaAsync();

    /// <summary>
    /// Troca a quantidade de blocos NUNCA busca dado novo — só re-fatia <see cref="_ultimasRegioesDoMapa"/>
    /// (já em memória) com o novo corte e reconcilia a exibição. Síncrono de propósito: evita
    /// buscar um novo mapa (caro, ~1-1,5s de <c>vmmap</c>) só porque o usuário quer ver mais/menos blocos.
    /// </summary>
    partial void OnQuantidadeDeBlocosSelecionadaChanged(int value)
    {
        if (_ultimasRegioesDoMapa.Count == 0)
            return;

        var blocos = ConstruirBlocos(_ultimasRegioesDoMapa, value, SelecionarBloco);
        ReconciliadorDeBlocosDeMemoria.Reconciliar(BlocosDeMemoria, blocos);

        if (BlocoSelecionado is not null && !BlocosDeMemoria.Contains(BlocoSelecionado))
            BlocoSelecionado = null;
    }

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

    /// <summary>
    /// PidFixado mantém o processo selecionado visível mesmo que sua %CPU caia fora do corte por
    /// "Limite" numa rodada — sem isso, a lista removeria e (se voltasse) recriaria o item com
    /// nova instância a cada oscilação, derrubando a seleção sem o usuário ter feito nada.
    /// </summary>
    private CriteriosDeAmostragem ObterCriterios() =>
        new(FiltroSelecionado.Perfil, LimiteSelecionado, ProcessoSelecionado?.Processo.Pid);

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

        // "Iniciar Monitor" liga os dois laços juntos — "Pausar" (StopMonitoring) os pausa juntos.
        await ReiniciarLoopDoMapaAsync();
    }

    private async Task ReiniciarSeMonitorandoAsync()
    {
        if (IsMonitoring)
            await ReiniciarLoopAsync();
    }

    /// <summary>
    /// Mesmo trio Cancel→await→Dispose→new de <see cref="ReiniciarLoopAsync"/>, aplicado ao laço
    /// do mapa. Só dispara se <see cref="AtualizacaoAutomaticaDoMapa"/> estiver ligado E o monitor
    /// estiver rodando (<see cref="IsMonitoring"/>) — "Pausar" pausa tudo, e o previewer de
    /// design-time (que nunca chama <c>StartMonitoringCommand</c>) nunca aciona <c>vmmap</c>.
    /// </summary>
    private async Task ReiniciarLoopDoMapaAsync()
    {
        _ctsMapa?.Cancel();

        if (_loopDoMapaTask is not null)
            await _loopDoMapaTask;

        _ctsMapa?.Dispose();
        _ctsMapa = null;
        _loopDoMapaTask = null;

        if (!IsMonitoring || !AtualizacaoAutomaticaDoMapa)
            return;

        _ctsMapa = new CancellationTokenSource();
        _loopDoMapaTask = ExecutarLoopDoMapaAsync(_ctsMapa.Token);
    }

    /// <summary>
    /// Laço próprio do mapa, com cadência DECOUPLED do "Ciclo (ms)" do laço de processos — o
    /// custo real de <c>vmmap</c> (~1-1,5s, medido) é ~30x o de uma coleta de processos, então
    /// não pode reaproveitar o mesmo temporizador. A cada tick, se há processo selecionado,
    /// tenta o semáforo com <c>WaitAsync(0)</c>: se já houver uma leitura em voo (o próprio laço
    /// ou o botão manual), PULA o tick — enfileirar uma leitura pra um estado que o usuário já
    /// passou é puro desperdício de um subprocesso caro.
    /// </summary>
    private async Task ExecutarLoopDoMapaAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(IntervaloDoMapaEmSegundos));

            while (await temporizador.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                var item = await Dispatcher.UIThread.InvokeAsync(() => ProcessoSelecionado);
                if (item is null)
                    continue;

                if (!await _semaforoDoMapa.WaitAsync(0, cancellationToken).ConfigureAwait(false))
                    continue;

                try
                {
                    await Dispatcher.UIThread.InvokeAsync(() => MapaEmAtualizacao = true);
                    await AtualizarMapaDeMemoriaAsync(item, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    _semaforoDoMapa.Release();
                    await Dispatcher.UIThread.InvokeAsync(() => MapaEmAtualizacao = false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelamento limpo (Parar, ou reinício por troca de intervalo/toggle) — não é uma falha.
        }
        catch (Exception excecao)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                StatusMessage = $"Erro no auto-refresh do mapa: {excecao.Message}");
        }
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
            var intervalo = TimeSpan.FromMilliseconds(IntervalMs);

            // ObterCriterios passado como PROVEDOR (não invocado aqui): MonitorDeProcessos.ObservarAsync
            // chama de novo a cada tick, então PidFixado sempre reflete o ProcessoSelecionado ATUAL —
            // uma troca de seleção passa a valer no próximo tick, sem precisar reiniciar este laço
            // (o que também reiniciaria sem necessidade o laço independente do mapa de memória).
            await foreach (var amostra in _monitorDeProcessos.ObservarAsync(intervalo, ObterCriterios, cancellationToken))
            {
                var amostraAtual = amostra;
                await Dispatcher.UIThread.InvokeAsync(() => ReconciliarProcessos(amostraAtual));

                // Threads do processo selecionado acompanham o mesmo ciclo: custo de `ps -M` é
                // desprezível (dezenas de ms, medido) frente ao ciclo do monitor, então não
                // precisa de laço próprio — ao contrário do mapa de memória (vmmap custa
                // ~1-1,5s, ver ExecutarLoopDoMapaAsync). Roda sob o TOKEN DO PRÓPRIO LAÇO (não
                // _cancelamentoSelecao) para não repetir a corrida de CTS descartado documentada
                // em AtualizarMapaAsync — o cancelamento deste laço já cobre o ciclo de vida certo.
                var selecionado = await Dispatcher.UIThread.InvokeAsync(() => ProcessoSelecionado);
                if (selecionado is not null)
                    await AtualizarThreadsAsync(selecionado, cancellationToken).ConfigureAwait(false);
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
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _pidDasThreadsExibidas = null;
                _itensPorTid.Clear();
                ThreadsSelecionadas.Clear();
            });
            return;
        }

        var leitura = await _fonteDeDetalhesDeThreads.ObterThreadsAsync(item.Processo.Pid, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var threadsFonte = leitura.TemValor ? leitura.Valor! : item.Processo.Threads;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Resultado obsoleto: a seleção já trocou de novo enquanto ObterThreadsAsync rodava.
            // Checagem por identidade (não por token) — cobre tick do laço, troca de seleção e
            // botão manual pelo mesmo caminho, independente de qual CTS governava esta chamada.
            if (!ReferenceEquals(ProcessoSelecionado, item))
                return;

            // Troca de processo: reset explícito antes de reconciliar (ver doc de _pidDasThreadsExibidas).
            if (_pidDasThreadsExibidas != item.Pid)
            {
                _itensPorTid.Clear();
                ThreadsSelecionadas.Clear();
                _pidDasThreadsExibidas = item.Pid;
            }

            ReconciliadorDeThreads.Reconciliar(ThreadsSelecionadas, _itensPorTid, threadsFonte);
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
                _ultimasRegioesDoMapa = [];
                BlocosDeMemoria.Clear();
                BlocoSelecionado = null;
                RotuloOrigemDoMapa = string.Empty;
                MotivoDoFallbackTexto = null;
            });
            return;
        }

        var cronometro = Stopwatch.StartNew();
        var resultado = await _obterMapaDeMemoria.ExecutarAsync(
            item.Processo.Pid,
            item.Processo.PerfilDeMemoria,
            _tamanhoDePaginaAtual,
            cancellationToken).ConfigureAwait(false);
        cronometro.Stop();

        cancellationToken.ThrowIfCancellationRequested();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Resultado obsoleto: mesma checagem por identidade de AtualizarThreadsAsync.
            if (!ReferenceEquals(ProcessoSelecionado, item))
                return;

            _ultimasRegioesDoMapa = resultado.Mapa.Regioes;
            var blocos = ConstruirBlocos(_ultimasRegioesDoMapa, QuantidadeDeBlocosSelecionada, SelecionarBloco);
            ReconciliadorDeBlocosDeMemoria.Reconciliar(BlocosDeMemoria, blocos);

            // O bloco selecionado só é limpo se a região dele realmente sumiu do mapa nesta
            // leitura — preservar a seleção entre atualizações é o que torna o painel de
            // detalhes utilizável quando o mapa está se atualizando sozinho.
            if (BlocoSelecionado is not null && !BlocosDeMemoria.Contains(BlocoSelecionado))
                BlocoSelecionado = null;

            RotuloOrigemDoMapa = RotulosPtBr.RotuloDeOrigemDoMapa(resultado.Mapa.OrigemDosDados);

            MotivoDoFallbackTexto = resultado.MotivoDoFallback is { } motivo
                ? RotulosPtBr.DescreverFallbackDeMapa(motivo)
                : null;

            DuracaoDaUltimaLeituraDoMapa = $"leitura em {cronometro.Elapsed.TotalSeconds.ToString("F1", CultureInfo.GetCultureInfo("pt-BR"))} s";
        });
    }

    /// <summary>
    /// Dois critérios de ordenação diferentes, deliberadamente não fundidos em um único
    /// <c>OrderBy</c>: o CORTE de quais regiões ganham bloco individual é por extensão
    /// decrescente (as maiores primeiro, até <paramref name="maximoDeBlocos"/>) — é o que
    /// mantém o mapa legível; mas a EXIBIÇÃO final é por endereço crescente
    /// (<see cref="FaixaDeEnderecos.Inicio"/>), porque é a ordem por endereço que ensina o layout
    /// real do espaço de endereçamento virtual do processo. O bloco agregado ("…+K regiões
    /// menores") não tem um endereço próprio, então fica sempre por último.
    /// </summary>
    internal static List<BlocoDeMemoriaViewModel> ConstruirBlocos(
        IReadOnlyList<RegiaoDeMemoria> regioes,
        int maximoDeBlocos,
        Action<BlocoDeMemoriaViewModel> aoSelecionar)
    {
        var ordenadasPorExtensao = regioes.OrderByDescending(regiao => regiao.FaixaDeEnderecos.Extensao.Valor).ToList();

        var maioresRegioes = ordenadasPorExtensao.Take(maximoDeBlocos);
        var restantes = ordenadasPorExtensao.Skip(maximoDeBlocos).ToList();

        var resultado = maioresRegioes
            .OrderBy(regiao => regiao.FaixaDeEnderecos.Inicio.Valor)
            .Select(regiao => new BlocoDeMemoriaViewModel(regiao, aoSelecionar))
            .ToList();

        if (restantes.Count > 0)
            resultado.Add(BlocoDeMemoriaViewModel.CriarAgregado(restantes, aoSelecionar));

        return resultado;
    }

    /// <summary>Garante idempotência do <see cref="Dispose"/>: ele é chamado duas vezes no encerramento (pelo <c>OnClosed</c> da janela e pelo <c>ServiceProvider</c>).</summary>
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _cancelamentoSelecao?.Cancel();
        _cancelamentoSelecao?.Dispose();
        _ctsMapa?.Cancel();
        _ctsMapa?.Dispose();
        _semaforoDoMapa.Dispose();
        GC.SuppressFinalize(this);
    }
}
