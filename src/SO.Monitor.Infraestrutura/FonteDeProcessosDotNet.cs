using System.ComponentModel;
using System.Diagnostics;
using SO.Monitor.Aplicacao.Ports;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.Servicos;
using SO.Monitor.Dominio.ValueObjects;
using ThreadState = System.Diagnostics.ThreadState;

namespace SO.Monitor.Infraestrutura;

/// <summary>
/// Adapter de <see cref="System.Diagnostics.Process"/> para o domínio — a fonte de processos
/// portátil que funciona (com graus de detalhe diferentes) nas três plataformas, sem nenhum
/// P/Invoke. Cada campo falível é lido isoladamente: uma falha em um campo vira
/// <see cref="Leitura{T}"/> negada/não suportada/encerrada; uma falha que impede reconstruir o
/// processo inteiro (ex.: ele terminou entre a enumeração e a leitura) incrementa
/// <see cref="AmostraDoSistema.ProcessosInacessiveis"/> em vez de ser silenciosamente descartada.
/// </summary>
/// <remarks>
/// <b>Premissa de concorrência:</b> esta classe assume que <see cref="ColetarAsync"/> é chamado
/// sequencialmente por um único consumidor (é exatamente como <c>MonitorDeProcessos</c>, da
/// camada de Aplicação, a utiliza — nunca duas coletas em paralelo). Por isso o cache de CPU por
/// PID é um <see cref="Dictionary{TKey,TValue}"/> simples, sem lock: sincronizar o cache em si
/// seria complexidade sem benefício real, dado o uso previsto. Como rede de segurança contra uma
/// violação dessa premissa (não uma mudança de contrato), <see cref="ColetarAsync"/> serializa as
/// execuções com <see cref="ExecucaoSerializada{T}"/> — ver essa classe para o porquê.
/// </remarks>
public sealed class FonteDeProcessosDotNet : IFonteDeProcessos, IDisposable
{
    private readonly Dictionary<int, (TimeSpan CpuAcumulado, DateTimeOffset Instante)> _cacheDeCpuPorPid = new();
    private readonly ExecucaoSerializada<AmostraDoSistema> _coletaSerializada;

    public FonteDeProcessosDotNet()
    {
        _coletaSerializada = new ExecucaoSerializada<AmostraDoSistema>(ColetarNoPoolAsync);
    }

    /// <summary>
    /// Serializa a coleta (defesa em profundidade — ver a documentação de
    /// <see cref="ExecucaoSerializada{T}"/>) e delega o trabalho de fato a
    /// <see cref="ColetarNoPoolAsync"/>.
    /// </summary>
    public ValueTask<AmostraDoSistema> ColetarAsync(CancellationToken cancellationToken) =>
        _coletaSerializada.ExecutarAsync(cancellationToken);

    /// <summary>
    /// Move a coleta síncrona (<see cref="Process.GetProcesses"/> e a leitura campo a campo de
    /// cada processo) para o thread pool via <see cref="Task.Run(Func{Task},CancellationToken)"/>.
    /// Sem isso, a primeira coleta de cada início do monitor rodaria inline na thread chamadora —
    /// tipicamente a UI, a partir de <c>MainWindowViewModel</c> — travando a interface até
    /// terminar de enumerar todos os processos do sistema.
    /// </summary>
    private ValueTask<AmostraDoSistema> ColetarNoPoolAsync(CancellationToken cancellationToken) =>
        new(Task.Run(() => ColetarSincrono(cancellationToken), cancellationToken));

    private AmostraDoSistema ColetarSincrono(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var instanteDaColeta = DateTimeOffset.UtcNow;
        var tamanhoDePagina = new TamanhoPagina(Environment.SystemPageSize);

        var processos = new List<Processo>();
        var processosInacessiveis = 0;
        var pidsVistos = new HashSet<int>();

        foreach (var processoOs in Process.GetProcesses())
        {
            using var _ = processoOs;

            // Checagem periódica (uma vez por processo): sem ela, cancelar o monitoramento só
            // teria efeito depois que a enumeração inteira terminasse — em uma máquina com
            // milhares de processos, isso pode levar tempo suficiente para o cancelamento parecer
            // travado.
            cancellationToken.ThrowIfCancellationRequested();

            int pidValor;
            try
            {
                pidValor = processoOs.Id;
            }
            catch (InvalidOperationException)
            {
                processosInacessiveis++;
                continue;
            }

            pidsVistos.Add(pidValor);

            var processo = TentarLerProcesso(processoOs, pidValor, instanteDaColeta, tamanhoDePagina);
            if (processo is null)
            {
                processosInacessiveis++;
                continue;
            }

            processos.Add(processo);
        }

        LimparCacheDePidsAusentes(pidsVistos);

        return new AmostraDoSistema(instanteDaColeta, tamanhoDePagina, processos, processosInacessiveis);
    }

    public void Dispose() => _coletaSerializada.Dispose();

    /// <summary>
    /// Tenta montar o <see cref="Processo"/> completo. Devolve <c>null</c> quando um dado
    /// essencial (nome, tempo de CPU acumulado, ou a própria lista de threads) não pôde ser lido
    /// — nesse caso o chamador conta o processo como inacessível em vez de publicar um
    /// <see cref="Processo"/> incompleto demais para ser útil.
    /// </summary>
    private Processo? TentarLerProcesso(
        Process processoOs,
        int pidValor,
        DateTimeOffset instanteDaColeta,
        TamanhoPagina tamanhoDePagina)
    {
        string nome;
        TimeSpan tempoDeCpuAtual;

        try
        {
            nome = processoOs.ProcessName;
            tempoDeCpuAtual = processoOs.TotalProcessorTime;
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }

        var threads = LerThreads(processoOs);
        if (threads is null)
            return null;

        var pid = new Pid(pidValor);
        var usoDeCpu = CalcularUsoDeCpu(pidValor, tempoDeCpuAtual, instanteDaColeta);
        var threadsBloqueadas = threads.Count(thread => thread.EstadoThread == EstadoThread.Bloqueada);

        var inicioDaExecucao = LerInicioDaExecucao(processoOs);

        // Sem um instante de início confiável não há como calcular a idade real do processo;
        // tratamos como zero (documentado): não é "o processo acabou de nascer", é "não sabemos
        // há quanto tempo ele existe".
        var tempoDeVida = inicioDaExecucao.TemValor
            ? instanteDaColeta - inicioDaExecucao.Valor
            : TimeSpan.Zero;

        var metricas = new MetricasDeExecucao(
            usoDeCpu,
            tempoDeCpuAtual,
            tempoDeVida,
            LerContagemDeHandles(processoOs),
            threads.Count,
            threadsBloqueadas);

        var perfilDeMemoria = LerPerfilDeMemoria(processoOs, tamanhoDePagina);
        var estadoProcesso = DeterminarEstadoProcesso(threads);

        return new Processo(
            pid,
            nome,
            LerCaminhoDoExecutavel(processoOs),
            estadoProcesso,
            inicioDaExecucao,
            metricas,
            perfilDeMemoria,
            LerPrioridadeBase(processoOs),
            threads);
    }

    /// <summary>
    /// Calcula o percentual de uso de CPU por delta entre a amostra anterior e a atual — a
    /// mesma técnica de top/htop, não a média de vida inteira do processo. Na primeira amostra
    /// de um PID não há referência anterior, então o percentual reportado é zero (documentado);
    /// a partir da segunda coleta o delta já reflete a utilização real do intervalo.
    /// </summary>
    private Percentual CalcularUsoDeCpu(int pid, TimeSpan cpuAtual, DateTimeOffset instanteAtual)
    {
        if (_cacheDeCpuPorPid.TryGetValue(pid, out var anterior))
        {
            var percentual = CalculadoraDeUsoDeCpu.Calcular(
                anterior.CpuAcumulado,
                cpuAtual,
                instanteAtual - anterior.Instante,
                Environment.ProcessorCount);

            _cacheDeCpuPorPid[pid] = (cpuAtual, instanteAtual);
            return percentual;
        }

        _cacheDeCpuPorPid[pid] = (cpuAtual, instanteAtual);
        return Percentual.Zero;
    }

    /// <summary>Remove do cache os PIDs que não apareceram na última coleta, evitando vazamento de memória.</summary>
    private void LimparCacheDePidsAusentes(IReadOnlySet<int> pidsVistos)
    {
        var pidsParaRemover = _cacheDeCpuPorPid.Keys.Where(pid => !pidsVistos.Contains(pid)).ToList();
        foreach (var pid in pidsParaRemover)
            _cacheDeCpuPorPid.Remove(pid);
    }

    private static Leitura<string> LerCaminhoDoExecutavel(Process processoOs)
    {
        try
        {
            var caminho = processoOs.MainModule?.FileName;
            return caminho is not null ? Leitura<string>.Ok(caminho) : Leitura<string>.NaoSuportada();
        }
        catch (Win32Exception)
        {
            return Leitura<string>.Negada();
        }
        catch (InvalidOperationException)
        {
            return Leitura<string>.ProcessoEncerrado();
        }
        catch (NotSupportedException)
        {
            return Leitura<string>.NaoSuportada();
        }
    }

    private static Leitura<DateTimeOffset> LerInicioDaExecucao(Process processoOs)
    {
        try
        {
            return Leitura<DateTimeOffset>.Ok(new DateTimeOffset(processoOs.StartTime));
        }
        catch (Win32Exception)
        {
            return Leitura<DateTimeOffset>.Negada();
        }
        catch (InvalidOperationException)
        {
            return Leitura<DateTimeOffset>.ProcessoEncerrado();
        }
        catch (NotSupportedException)
        {
            return Leitura<DateTimeOffset>.NaoSuportada();
        }
    }

    private static Leitura<int> LerPrioridadeBase(Process processoOs)
    {
        try
        {
            return Leitura<int>.Ok(processoOs.BasePriority);
        }
        catch (Win32Exception)
        {
            return Leitura<int>.Negada();
        }
        catch (InvalidOperationException)
        {
            return Leitura<int>.ProcessoEncerrado();
        }
        catch (NotSupportedException)
        {
            return Leitura<int>.NaoSuportada();
        }
    }

    /// <summary>
    /// Contagem de handles/descritores abertos: exposta pelo Windows, mas não pelo macOS/Linux
    /// via <c>System.Diagnostics</c> — nesse caso <see cref="PlatformNotSupportedException"/> vira
    /// <see cref="Leitura{T}.NaoSuportada"/>, não um zero enganoso.
    /// </summary>
    private static Leitura<int> LerContagemDeHandles(Process processoOs)
    {
        try
        {
            return Leitura<int>.Ok(processoOs.HandleCount);
        }
        catch (Win32Exception)
        {
            return Leitura<int>.Negada();
        }
        catch (InvalidOperationException)
        {
            return Leitura<int>.ProcessoEncerrado();
        }
        catch (PlatformNotSupportedException)
        {
            return Leitura<int>.NaoSuportada();
        }
        catch (NotSupportedException)
        {
            return Leitura<int>.NaoSuportada();
        }
    }

    private static PerfilDeMemoria LerPerfilDeMemoria(Process processoOs, TamanhoPagina tamanhoPagina) => new(
        LerTamanhoDeMemoria(processoOs, p => p.WorkingSet64),
        LerTamanhoDeMemoria(processoOs, p => p.VirtualMemorySize64),
        LerTamanhoDeMemoria(processoOs, p => p.PrivateMemorySize64),
        tamanhoPagina);

    private static Leitura<TamanhoBytes> LerTamanhoDeMemoria(Process processoOs, Func<Process, long> seletor)
    {
        try
        {
            var bytes = seletor(processoOs);
            return bytes >= 0 ? Leitura<TamanhoBytes>.Ok(new TamanhoBytes(bytes)) : Leitura<TamanhoBytes>.NaoSuportada();
        }
        catch (Win32Exception)
        {
            return Leitura<TamanhoBytes>.Negada();
        }
        catch (InvalidOperationException)
        {
            return Leitura<TamanhoBytes>.ProcessoEncerrado();
        }
        catch (NotSupportedException)
        {
            return Leitura<TamanhoBytes>.NaoSuportada();
        }
    }

    /// <summary>
    /// Lê a lista de threads do processo. Devolve <c>null</c> (falha total, não uma lista vazia)
    /// quando a própria coleção de threads não pôde ser obtida — nesse caso o processo inteiro é
    /// tratado como inacessível pelo chamador, já que threads são conteúdo central deste monitor.
    /// </summary>
    private static IReadOnlyList<ThreadDoProcesso>? LerThreads(Process processoOs)
    {
        ProcessThreadCollection threadsOs;
        try
        {
            threadsOs = processoOs.Threads;
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }

        var threads = new List<ThreadDoProcesso>(threadsOs.Count);
        foreach (ProcessThread threadOs in threadsOs)
            threads.Add(LerThread(threadOs));

        return threads;
    }

    private static ThreadDoProcesso LerThread(ProcessThread threadOs) => new(
        LerTid(threadOs),
        LerEstadoThread(threadOs),
        LerMotivoDeBloqueio(threadOs),
        LerPrioridadeBaseDaThread(threadOs),
        LerTempoDeCpuDaThread(threadOs));

    /// <summary>
    /// No macOS/Linux, <see cref="ProcessThread.Id"/> devolve o identificador de thread do
    /// kernel truncado em <see cref="int"/>: como esse identificador é, por natureza, um valor
    /// sem sinal, reinterpretamos os mesmos bits como <see cref="uint"/> em vez de deixar o TID
    /// negativo estourar a validação de <see cref="Tid"/> — nenhuma informação é perdida, só a
    /// leitura do sinal, que nunca foi significativa aqui.
    /// </summary>
    private static Tid LerTid(ProcessThread threadOs) => new(unchecked((uint)threadOs.Id));

    /// <summary>
    /// Em Unix/macOS o kernel restringe a leitura de estado de thread por processo
    /// (<see cref="ProcessThread.ThreadState"/> lança); nesse caso o valor honesto é
    /// <see cref="EstadoThread.Desconhecido"/> — não fingimos que a thread está pronta ou em execução.
    /// </summary>
    private static EstadoThread LerEstadoThread(ProcessThread threadOs)
    {
        try
        {
            return MapeadorDeEstadoDeThread.ParaEstadoThread(threadOs.ThreadState);
        }
        catch (Win32Exception)
        {
            return EstadoThread.Desconhecido;
        }
        catch (PlatformNotSupportedException)
        {
            return EstadoThread.Desconhecido;
        }
        catch (NotSupportedException)
        {
            return EstadoThread.Desconhecido;
        }
    }

    /// <summary>
    /// <see cref="ProcessThread.WaitReason"/> só é legível quando <see cref="ProcessThread.ThreadState"/>
    /// é <see cref="ThreadState.Wait"/> — fora disso a própria API lança
    /// <see cref="InvalidOperationException"/> (e a maioria das threads, na maior parte do
    /// tempo, não está bloqueada). Quando a thread não está esperando, o motivo de bloqueio não
    /// falha por limitação da plataforma: ele simplesmente <see cref="Disponibilidade.NaoSeAplica"/>
    /// — a pergunta "por que está bloqueada" não faz sentido para uma thread que não está.
    /// Checar o estado antes evita depender de exceção para esse caso esperado; em Unix/macOS,
    /// mesmo com a thread de fato bloqueada, o motivo específico pode não ser exposto
    /// (<see cref="PlatformNotSupportedException"/>) — nesse caso, sim, é
    /// <see cref="Leitura{T}.NaoSuportada"/>: o refinamento por plataforma vem dos provedores
    /// reais (<c>FonteDeThreadsMacOs</c>/<c>FonteDeThreadsLinux</c>, via
    /// <see cref="SO.Monitor.Aplicacao.Ports.IFonteDeDetalhesDeThreads"/>), não desta classe.
    /// </summary>
    private static Leitura<MotivoDeBloqueio> LerMotivoDeBloqueio(ProcessThread threadOs)
    {
        try
        {
            if (threadOs.ThreadState != ThreadState.Wait)
                return Leitura<MotivoDeBloqueio>.NaoSeAplica();

            return Leitura<MotivoDeBloqueio>.Ok(MapeadorDeEstadoDeThread.ParaMotivoDeBloqueio(threadOs.WaitReason));
        }
        catch (Win32Exception)
        {
            return Leitura<MotivoDeBloqueio>.Negada();
        }
        catch (PlatformNotSupportedException)
        {
            return Leitura<MotivoDeBloqueio>.NaoSuportada();
        }
        catch (NotSupportedException)
        {
            return Leitura<MotivoDeBloqueio>.NaoSuportada();
        }
        catch (InvalidOperationException)
        {
            // Janela de corrida: a thread saiu do estado Wait entre a checagem acima e a leitura
            // de WaitReason — ou seja, no instante da leitura ela já não estava mais bloqueada.
            // Mesmo caso semântico do "if" acima: o motivo não se aplica, não é uma limitação de
            // plataforma.
            return Leitura<MotivoDeBloqueio>.NaoSeAplica();
        }
    }

    private static Leitura<int> LerPrioridadeBaseDaThread(ProcessThread threadOs)
    {
        try
        {
            return Leitura<int>.Ok(threadOs.BasePriority);
        }
        catch (Win32Exception)
        {
            return Leitura<int>.Negada();
        }
        catch (InvalidOperationException)
        {
            return Leitura<int>.ProcessoEncerrado();
        }
        catch (NotSupportedException)
        {
            return Leitura<int>.NaoSuportada();
        }
    }

    private static Leitura<TimeSpan> LerTempoDeCpuDaThread(ProcessThread threadOs)
    {
        try
        {
            return Leitura<TimeSpan>.Ok(threadOs.TotalProcessorTime);
        }
        catch (Win32Exception)
        {
            return Leitura<TimeSpan>.Negada();
        }
        catch (InvalidOperationException)
        {
            return Leitura<TimeSpan>.ProcessoEncerrado();
        }
        catch (NotSupportedException)
        {
            return Leitura<TimeSpan>.NaoSuportada();
        }
    }

    /// <summary>
    /// <see cref="Process.Responding"/> <c>== false</c> não significa Bloqueado — pode ser só uma
    /// janela ocupada processando algo. O kernel não expõe um estado agregado de processo via
    /// <c>System.Diagnostics</c>, então esta heurística deriva o estado a partir das threads: se
    /// alguma está em execução, o processo está em execução; senão, se todas estão bloqueadas, o
    /// processo está bloqueado; caso contrário, é mais honesto admitir que não sabemos.
    /// </summary>
    private static EstadoProcesso DeterminarEstadoProcesso(IReadOnlyList<ThreadDoProcesso> threads)
    {
        if (threads.Count == 0)
            return EstadoProcesso.Desconhecido;

        if (threads.Any(thread => thread.EstadoThread == EstadoThread.EmExecucao))
            return EstadoProcesso.EmExecucao;

        if (threads.All(thread => thread.EstadoThread == EstadoThread.Bloqueada))
            return EstadoProcesso.Bloqueado;

        return EstadoProcesso.Desconhecido;
    }
}
