using MonitorGUI.DesignTime;
using MonitorGUI.ViewModels;
using SO.Monitor.Aplicacao.CasosDeUso;
using SO.Monitor.Aplicacao.Ports;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.Servicos;
using SO.Monitor.Dominio.ValueObjects;
using SO.Monitor.Testes.Aplicacao;
using Xunit;

namespace SO.Monitor.Testes.Apresentacao;

/// <summary>
/// Cobre a correção da corrida de coletas em <see cref="MainWindowViewModel"/> (FIX1, lado do
/// VM), o trio Cancel→Dispose→new do CancellationTokenSource do laço (FIX3) e o cancelamento do
/// mapa de memória em voo ao trocar de seleção (FIX4). Todos os testes usam fontes falsas que
/// NUNCA completam normalmente — só terminam quando canceladas — para exercitar os caminhos de
/// reinício/cancelamento sem precisar de um Dispatcher Avalonia inicializado (a primeira amostra
/// nunca chega a ser emitida, então <c>Dispatcher.UIThread.InvokeAsync</c> nunca é alcançado nos
/// caminhos aguardados pelo teste).
/// </summary>
public class MainWindowViewModelTests
{
    private static readonly TimeSpan LimiteDeEspera = TimeSpan.FromSeconds(5);

    /// <summary>Fonte de processos que só termina (com OperationCanceledException) quando o token é cancelado.</summary>
    private sealed class FonteDeProcessosQueNuncaCompleta : IFonteDeProcessos
    {
        public async ValueTask<AmostraDoSistema> ColetarAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("ColetarAsync não deveria completar normalmente neste teste.");
        }
    }

    /// <summary>Fonte de mapa de memória que só termina (com OperationCanceledException) quando o token é cancelado.</summary>
    private sealed class FonteDeMapaQueNuncaCompleta : IFonteDeMapaDeMemoria
    {
        public bool FoiCancelada { get; private set; }

        public async ValueTask<Leitura<MapaDeMemoria>> ObterMapaAsync(Pid pid, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                FoiCancelada = true;
                throw;
            }

            throw new InvalidOperationException("ObterMapaAsync não deveria completar normalmente neste teste.");
        }
    }

    private static MainWindowViewModel CriarViewModel(IFonteDeProcessos fonteDeProcessos, IFonteDeMapaDeMemoria? fonteDeMapa = null)
    {
        var obterAmostra = new ObterAmostraClassificada(fonteDeProcessos, new PoliticaPorComportamentoDeBurst());
        var monitor = new MonitorDeProcessos(obterAmostra);
        var obterMapa = new ObterMapaDeMemoria(fonteDeMapa ?? new FonteDeMapaDeMemoriaDesignTime());

        return new MainWindowViewModel(monitor, obterMapa, new FonteDeDetalhesDeThreadsDesignTime());
    }

    /// <summary>Executa uma task real com um teto de tempo — protege o teste contra travar a suíte inteira se uma premissa estiver errada.</summary>
    private static async Task<TResultado> ComLimiteDeTempoAsync<TResultado>(Task<TResultado> tarefa)
    {
        var concluida = await Task.WhenAny(tarefa, Task.Delay(LimiteDeEspera));
        Assert.True(ReferenceEquals(concluida, tarefa), "A operação não terminou dentro do limite de tempo do teste.");
        return await tarefa;
    }

    private static async Task ComLimiteDeTempoAsync(Task tarefa)
    {
        var concluida = await Task.WhenAny(tarefa, Task.Delay(LimiteDeEspera));
        Assert.True(ReferenceEquals(concluida, tarefa), "A operação não terminou dentro do limite de tempo do teste.");
        await tarefa;
    }

    [Fact]
    public async Task ReiniciosRepetidos_AguardamOLoopAnterior_NuncaLancamObjectDisposedException()
    {
        var vm = CriarViewModel(new FonteDeProcessosQueNuncaCompleta());

        var excecao = await Record.ExceptionAsync(async () =>
        {
            await ComLimiteDeTempoAsync(vm.StartMonitoringCommand.ExecuteAsync(null));

            // Cada reinício cancela o CTS anterior, aguarda o loop anterior terminar (FIX1) e só
            // então faz Dispose + new (FIX3, trio Cancel->Dispose->new). Se o trio estivesse
            // quebrado (ex.: Dispose antes do loop anterior observar o cancelamento), alguma
            // dessas chamadas lançaria ObjectDisposedException.
            for (var i = 0; i < 5; i++)
                await ComLimiteDeTempoAsync(vm.IncreaseIntervalCommand.ExecuteAsync(null));

            vm.StopMonitoringCommand.Execute(null);
        });

        Assert.Null(excecao);
        vm.Dispose();
    }

    /// <summary>
    /// Regressão do crash de encerramento (exit code 134): no shutdown real, <c>Dispose</c> é
    /// chamado DUAS vezes — primeiro pelo <c>OnClosed</c> da janela, depois pelo
    /// <c>ServiceProvider</c> ao descartar o singleton. A segunda chamada lançava
    /// ObjectDisposedException em <c>_cts.Cancel()</c>; o contrato de IDisposable exige idempotência.
    /// </summary>
    [Fact]
    public async Task Dispose_ChamadoDuasVezes_NaoLancaObjectDisposedException()
    {
        var vm = CriarViewModel(new FonteDeProcessosQueNuncaCompleta());

        // Liga o monitor para que _cts exista de verdade, como no cenário do crash.
        await ComLimiteDeTempoAsync(vm.StartMonitoringCommand.ExecuteAsync(null));

        var excecao = Record.Exception(() =>
        {
            vm.Dispose(); // OnClosed da janela
            vm.Dispose(); // ServiceProvider.Dispose() no ShutdownRequested
        });

        Assert.Null(excecao);
    }

    /// <summary>
    /// Regressão do laço de auto-refresh do mapa (<c>_ctsMapa</c>/<c>_loopDoMapaTask</c>,
    /// separado de <c>_cts</c>/<c>_loopTask</c> porque a cadência do mapa é decoupled do "Ciclo
    /// (ms)"): "Iniciar Monitor" liga os dois laços juntos (<c>AtualizacaoAutomaticaDoMapa</c>
    /// tem default <c>true</c>), e <c>Dispose</c> chamado duas vezes (mesmo cenário do crash de
    /// encerramento real) precisa continuar limpo mesmo com o segundo laço em voo.
    /// </summary>
    [Fact]
    public async Task Dispose_ChamadoDuasVezes_ComLoopDoMapaJaIniciado_ContinuaLimpo()
    {
        var vm = CriarViewModel(new FonteDeProcessosQueNuncaCompleta());

        await ComLimiteDeTempoAsync(vm.StartMonitoringCommand.ExecuteAsync(null));
        Assert.True(vm.AtualizacaoAutomaticaDoMapa); // default — o laço do mapa deveria ter iniciado junto

        var excecao = Record.Exception(() =>
        {
            vm.Dispose();
            vm.Dispose();
        });

        Assert.Null(excecao);
    }

    [Fact]
    public async Task AtualizarMapaAsync_UsaTokenDeSelecao_CanceladoQuandoSelecaoTroca()
    {
        var fonteMapa = new FonteDeMapaQueNuncaCompleta();
        var vm = CriarViewModel(new FonteDeProcessosFalsa(), fonteMapa);

        var item = new ProcessoItemViewModel(FonteDeProcessosFalsa.CriarAmostraPadrao().Processos[0]);
        vm.ProcessoSelecionado = item;

        // O comando reaproveita o MESMO token de seleção (FIX4, não mais CancellationToken.None):
        // como a fonte falsa nunca completa por conta própria, esta chamada só termina quando
        // algo cancelar o token de seleção corrente.
        var tarefaDeAtualizacao = vm.AtualizarMapaCommand.ExecuteAsync(null);

        // Trocar de seleção cancela e substitui _cancelamentoSelecao — é exatamente essa troca
        // que deve derrubar o mapa antigo em voo.
        vm.ProcessoSelecionado = null;

        var excecao = await Record.ExceptionAsync(() => ComLimiteDeTempoAsync(tarefaDeAtualizacao));

        Assert.Null(excecao); // AtualizarMapaAsync captura OperationCanceledException internamente
        Assert.True(fonteMapa.FoiCancelada);

        vm.Dispose();
    }
}
