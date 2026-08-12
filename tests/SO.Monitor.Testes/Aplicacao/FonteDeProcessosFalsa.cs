using SO.Monitor.Aplicacao.Ports;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Testes.Aplicacao;

/// <summary>
/// Fonte de processos falsa (test double), reutilizável entre os testes de casos de uso da
/// Aplicação: devolve uma amostra fixa, construída inteiramente com tipos reais do domínio —
/// sem mocks — com três processos que cobrem os três perfis de classificação por burst:
/// "compilador" (CPU alta -&gt; LimitadoPorCpu), "servidor-web" (muitas threads bloqueadas ->
/// LimitadoPorES) e "editor" (nem um nem outro -&gt; Indeterminado).
/// </summary>
public sealed class FonteDeProcessosFalsa : IFonteDeProcessos
{
    public static readonly TamanhoPagina TamanhoDePaginaPadrao = TamanhoPagina.Kib4;

    public AmostraDoSistema Amostra { get; set; } = CriarAmostraPadrao();

    public ValueTask<AmostraDoSistema> ColetarAsync(CancellationToken cancellationToken)
    {
        // Espelha o contrato da fonte real (FonteDeProcessosDotNet): honra o cancelamento em vez
        // de ignorá-lo, para que testes de cancelamento limpo (ex.: MonitorDeProcessosTests)
        // exercitem o mesmo caminho que a implementação real percorre.
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Amostra);
    }

    public static AmostraDoSistema CriarAmostraPadrao()
    {
        var instante = DateTimeOffset.UtcNow;

        var compilador = CriarProcesso(pid: 100, nome: "compilador", usoDeCpu: 80.0, threadsBloqueadas: 0, totalDeThreads: 4);
        var servidorWeb = CriarProcesso(pid: 200, nome: "servidor-web", usoDeCpu: 5.0, threadsBloqueadas: 8, totalDeThreads: 10);
        var editor = CriarProcesso(pid: 300, nome: "editor", usoDeCpu: 10.0, threadsBloqueadas: 1, totalDeThreads: 2);

        return new AmostraDoSistema(instante, TamanhoDePaginaPadrao, [compilador, servidorWeb, editor], ProcessosInacessiveis: 0);
    }

    private static Processo CriarProcesso(int pid, string nome, double usoDeCpu, int threadsBloqueadas, int totalDeThreads)
    {
        var threads = Enumerable.Range(0, totalDeThreads)
            .Select(indice => new ThreadDoProcesso(
                new Tid(indice),
                indice < threadsBloqueadas ? EstadoThread.Bloqueada : EstadoThread.EmExecucao,
                Leitura<MotivoDeBloqueio>.Ok(MotivoDeBloqueio.EsperandoES),
                Leitura<int>.Ok(8),
                Leitura<TimeSpan>.Ok(TimeSpan.FromSeconds(1))))
            .ToList();

        var metricas = new MetricasDeExecucao(
            new Percentual(usoDeCpu),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMinutes(5),
            Leitura<int>.Ok(50),
            totalDeThreads,
            threadsBloqueadas);

        var perfilDeMemoria = new PerfilDeMemoria(
            Leitura<TamanhoBytes>.Ok(new TamanhoBytes(1024 * 1024)),
            Leitura<TamanhoBytes>.Ok(new TamanhoBytes(4 * 1024 * 1024)),
            Leitura<TamanhoBytes>.Ok(new TamanhoBytes(512 * 1024)),
            TamanhoDePaginaPadrao);

        return new Processo(
            new Pid(pid),
            nome,
            Leitura<string>.Ok($"/usr/bin/{nome}"),
            EstadoProcesso.EmExecucao,
            Leitura<DateTimeOffset>.Ok(DateTimeOffset.UtcNow.AddMinutes(-5)),
            metricas,
            perfilDeMemoria,
            Leitura<int>.Ok(8),
            threads);
    }
}
