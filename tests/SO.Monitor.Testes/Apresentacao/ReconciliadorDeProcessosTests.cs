using System.Collections.ObjectModel;
using MonitorGUI.ViewModels;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;
using Xunit;

namespace SO.Monitor.Testes.Apresentacao;

/// <summary>
/// Cobre a reconciliação por PID que corrige o bug histórico de perda de seleção/rolagem na
/// DataGrid: a cada ciclo do monitor, a coleção observável de processos deve atualizar itens
/// existentes NO LUGAR (mesma instância de <see cref="ProcessoItemViewModel"/>), adicionar os
/// novos e remover os ausentes — nunca <c>Clear()+re-Add()</c>.
/// </summary>
public class ReconciliadorDeProcessosTests
{
    private static readonly TamanhoPagina Pagina = TamanhoPagina.Kib4;

    private static Processo CriarProcesso(int pid, string nome, double usoDeCpu) => new(
        new Pid(pid),
        nome,
        Leitura<string>.Ok($"/usr/bin/{nome}"),
        EstadoProcesso.EmExecucao,
        Leitura<DateTimeOffset>.Ok(DateTimeOffset.UtcNow),
        new MetricasDeExecucao(new Percentual(usoDeCpu), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), Leitura<int>.Ok(10), 1, 0),
        new PerfilDeMemoria(
            Leitura<TamanhoBytes>.Ok(new TamanhoBytes(1024)),
            Leitura<TamanhoBytes>.Ok(new TamanhoBytes(2048)),
            Leitura<TamanhoBytes>.Ok(new TamanhoBytes(512)),
            Pagina),
        Leitura<int>.Ok(8),
        [new ThreadDoProcesso(new Tid(0), EstadoThread.EmExecucao, Leitura<MotivoDeBloqueio>.NaoSuportada(), Leitura<int>.Ok(8), Leitura<TimeSpan>.Ok(TimeSpan.FromSeconds(1)))]);

    private static AmostraDoSistema CriarAmostra(params Processo[] processos) =>
        new(DateTimeOffset.UtcNow, Pagina, processos, ProcessosInacessiveis: 0);

    [Fact]
    public void Reconciliar_ProcessoNovo_AdicionaItemNaColecao()
    {
        var processos = new ObservableCollection<ProcessoItemViewModel>();
        var itensPorPid = new Dictionary<int, ProcessoItemViewModel>();

        ReconciliadorDeProcessos.Reconciliar(processos, itensPorPid, CriarAmostra(CriarProcesso(100, "compilador", 50)));

        Assert.Single(processos);
        Assert.Equal(100, processos[0].Pid);
        Assert.Equal("compilador", processos[0].Nome);
    }

    [Fact]
    public void Reconciliar_ProcessoExistente_AtualizaNoLugarSemTrocarInstancia()
    {
        var processos = new ObservableCollection<ProcessoItemViewModel>();
        var itensPorPid = new Dictionary<int, ProcessoItemViewModel>();

        ReconciliadorDeProcessos.Reconciliar(processos, itensPorPid, CriarAmostra(CriarProcesso(100, "compilador", 50)));
        var instanciaOriginal = processos[0];

        ReconciliadorDeProcessos.Reconciliar(processos, itensPorPid, CriarAmostra(CriarProcesso(100, "compilador", 92.5)));

        Assert.Single(processos);
        Assert.Same(instanciaOriginal, processos[0]); // mesma instância -> preserva seleção/scroll na DataGrid
        Assert.Equal(92.5, processos[0].PorcentagemCpu);
    }

    [Fact]
    public void Reconciliar_ProcessoAusenteNaAmostra_RemoveDaColecaoEDoIndice()
    {
        var processos = new ObservableCollection<ProcessoItemViewModel>();
        var itensPorPid = new Dictionary<int, ProcessoItemViewModel>();

        ReconciliadorDeProcessos.Reconciliar(
            processos, itensPorPid,
            CriarAmostra(CriarProcesso(100, "compilador", 50), CriarProcesso(200, "servidor-web", 5)));
        Assert.Equal(2, processos.Count);

        ReconciliadorDeProcessos.Reconciliar(processos, itensPorPid, CriarAmostra(CriarProcesso(200, "servidor-web", 6)));

        Assert.Single(processos);
        Assert.Equal(200, processos[0].Pid);
        Assert.False(itensPorPid.ContainsKey(100));
    }

    [Fact]
    public void Reconciliar_VariosCiclos_PreservaInstanciaSelecionadaEAtualizaValores()
    {
        var processos = new ObservableCollection<ProcessoItemViewModel>();
        var itensPorPid = new Dictionary<int, ProcessoItemViewModel>();

        ReconciliadorDeProcessos.Reconciliar(processos, itensPorPid, CriarAmostra(
            CriarProcesso(100, "compilador", 10),
            CriarProcesso(200, "servidor-web", 5),
            CriarProcesso(300, "editor", 2)));

        var selecionado = processos.Single(processo => processo.Pid == 200);

        // Três ciclos seguidos, como o monitor faria a cada tick do PeriodicTimer.
        for (var ciclo = 0; ciclo < 3; ciclo++)
        {
            ReconciliadorDeProcessos.Reconciliar(processos, itensPorPid, CriarAmostra(
                CriarProcesso(100, "compilador", 10 + ciclo),
                CriarProcesso(200, "servidor-web", 5 + ciclo),
                CriarProcesso(300, "editor", 2 + ciclo)));
        }

        var aindaSelecionado = processos.Single(processo => processo.Pid == 200);
        Assert.Same(selecionado, aindaSelecionado);
        Assert.Equal(7, aindaSelecionado.PorcentagemCpu); // 5 + (última iteração: ciclo = 2)
    }
}
