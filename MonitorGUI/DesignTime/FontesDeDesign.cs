using SO.Monitor.Aplicacao.Ports;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.Servicos;
using SO.Monitor.Dominio.ValueObjects;

namespace MonitorGUI.DesignTime;

/// <summary>
/// Fonte de processos falsa, exclusiva do previewer do Avalonia: devolve sempre os mesmos três
/// processos ilustrativos, sem tocar o sistema operacional real. Mantida deliberadamente isolada
/// da Infraestrutura (nenhuma dependência em <c>FabricaDeFontes</c> nem nas fontes reais) para
/// que o design-time nunca dependa de trabalho em andamento nos provedores por plataforma — e
/// para que o previewer nunca quebre, seja lá o que estiver acontecendo em outras frentes do
/// projeto.
/// </summary>
public sealed class FonteDeProcessosDesignTime : IFonteDeProcessos
{
    public ValueTask<AmostraDoSistema> ColetarAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(CriarAmostraIlustrativa());

    private static AmostraDoSistema CriarAmostraIlustrativa()
    {
        var instante = DateTimeOffset.Now;
        var paginaDeDesign = TamanhoPagina.Kib16;

        var compilador = CriarProcesso(
            pid: 4210, nome: "compilador-cs", usoDeCpu: 78.4,
            perfil: PerfilDeExecucao.LimitadoPorCpu,
            criterio: "Uso de CPU de 78,4% >= 25%: rajadas longas de computação, típicas de processo limitado por CPU.",
            residenteMiB: 210, virtualMiB: 640, totalDeThreads: 6, threadsBloqueadas: 1,
            handles: 64, pagina: paginaDeDesign);

        var servidorWeb = CriarProcesso(
            pid: 5310, nome: "servidor-web", usoDeCpu: 4.1,
            perfil: PerfilDeExecucao.LimitadoPorES,
            criterio: "9/10 threads bloqueadas (90%) >= 60%: bloqueios frequentes de E/S.",
            residenteMiB: 85, virtualMiB: 320, totalDeThreads: 10, threadsBloqueadas: 9,
            handles: 812, pagina: paginaDeDesign);

        var editor = CriarProcesso(
            pid: 6120, nome: "editor-texto", usoDeCpu: 9.7,
            perfil: PerfilDeExecucao.Indeterminado,
            criterio: "Uso de CPU baixo e poucas threads bloqueadas: nenhum padrão claro de CPU nem de E/S.",
            residenteMiB: 140, virtualMiB: 410, totalDeThreads: 3, threadsBloqueadas: 0,
            handles: 37, pagina: paginaDeDesign);

        return new AmostraDoSistema(instante, paginaDeDesign, [compilador, servidorWeb, editor], ProcessosInacessiveis: 2);
    }

    private static Processo CriarProcesso(
        int pid, string nome, double usoDeCpu, PerfilDeExecucao perfil, string criterio,
        long residenteMiB, long virtualMiB, int totalDeThreads, int threadsBloqueadas,
        int handles, TamanhoPagina pagina)
    {
        var threads = Enumerable.Range(0, totalDeThreads)
            .Select(indice => new ThreadDoProcesso(
                new Tid(indice),
                indice < threadsBloqueadas ? EstadoThread.Bloqueada : EstadoThread.EmExecucao,
                indice < threadsBloqueadas ? Leitura<MotivoDeBloqueio>.Ok(MotivoDeBloqueio.EsperandoES) : Leitura<MotivoDeBloqueio>.NaoSuportada(),
                Leitura<int>.Ok(8),
                Leitura<TimeSpan>.Ok(TimeSpan.FromSeconds(indice + 1))))
            .ToList();

        var metricas = new MetricasDeExecucao(
            new Percentual(usoDeCpu),
            TimeSpan.FromSeconds(42),
            TimeSpan.FromMinutes(12),
            Leitura<int>.Ok(handles),
            totalDeThreads,
            threadsBloqueadas);

        var perfilDeMemoria = new PerfilDeMemoria(
            Leitura<TamanhoBytes>.Ok(new TamanhoBytes(residenteMiB * 1024 * 1024)),
            Leitura<TamanhoBytes>.Ok(new TamanhoBytes(virtualMiB * 1024 * 1024)),
            Leitura<TamanhoBytes>.Ok(new TamanhoBytes(residenteMiB * 1024 * 1024 / 2)),
            pagina);

        return new Processo(
            new Pid(pid),
            nome,
            Leitura<string>.Ok($"/usr/local/bin/{nome}"),
            EstadoProcesso.EmExecucao,
            Leitura<DateTimeOffset>.Ok(DateTimeOffset.Now.AddMinutes(-12)),
            metricas,
            perfilDeMemoria,
            Leitura<int>.Ok(8),
            threads,
            new ResultadoDaClassificacao(perfil, criterio));
    }
}

/// <summary>
/// Fonte de mapa de memória falsa do previewer: sempre "não suportado", para que o design-time
/// exercite (e mostre corretamente rotulado) o fallback simulado do caso de uso
/// <c>ObterMapaDeMemoria</c> — o mesmo caminho que roda de verdade em uma plataforma sem
/// provedor real conectado.
/// </summary>
public sealed class FonteDeMapaDeMemoriaDesignTime : IFonteDeMapaDeMemoria
{
    public ValueTask<Leitura<MapaDeMemoria>> ObterMapaAsync(Pid pid, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Leitura<MapaDeMemoria>.NaoSuportada());
}

/// <summary>
/// Fonte de detalhes de threads falsa do previewer: sempre "não suportado", para que o
/// design-time exercite o fallback documentado (threads da própria amostra).
/// </summary>
public sealed class FonteDeDetalhesDeThreadsDesignTime : IFonteDeDetalhesDeThreads
{
    public ValueTask<Leitura<IReadOnlyList<ThreadDoProcesso>>> ObterThreadsAsync(Pid pid, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Leitura<IReadOnlyList<ThreadDoProcesso>>.NaoSuportada());
}
