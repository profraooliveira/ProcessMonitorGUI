using System.Globalization;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Infraestrutura.Linux;

/// <summary>
/// Parser puro de <c>/proc/&lt;pid&gt;/task/&lt;tid&gt;/stat</c> — a fonte do estado real de cada
/// thread no Linux (Tanenbaum: o mesmo PCB/TCB exposto como texto pelo procfs). O campo mais
/// traiçoeiro é o segundo, <c>comm</c> (nome da thread entre parênteses): o próprio kernel pode
/// colocar ali qualquer string, inclusive com espaços e parênteses. A técnica documentada do
/// procfs para lidar com isso é procurar o <b>último</b> ')' da linha (não o primeiro) e tratar
/// tudo depois dele como os campos numéricos restantes — é o que este analisador faz.
/// </summary>
internal static class AnalisadorDoProcTaskStat
{
    /// <summary>
    /// Ticks de relógio por segundo assumidos para converter utime/stime em tempo real
    /// (<c>sysconf(_SC_CLK_TCK)</c>). Em Linux esse valor é, na prática universal, 100 — o kernel
    /// poderia expor um valor diferente via P/Invoke, mas 100 é a suposição didática de sempre
    /// funcionar nas distribuições comuns, sem exigir uma chamada nativa adicional só para isso.
    /// </summary>
    private const long TicksDeRelogioPorSegundo = 100;

    // Índices dentro do array de campos que sobra após o último ')' — esse resto começa no campo
    // 3 (state) do formato oficial de /proc/[pid]/stat, então campo N cai no índice (N - 3).
    private const int IndiceEstado = 3 - 3;
    private const int IndiceUtime = 14 - 3;
    private const int IndiceStime = 15 - 3;
    private const int IndicePrioridade = 18 - 3;

    /// <summary>
    /// Tenta analisar o conteúdo de um arquivo <c>stat</c>. Devolve <c>false</c> (nunca lança)
    /// quando o conteúdo não segue o formato esperado.
    /// </summary>
    public static bool TentarAnalisar(string conteudoStat, Tid tid, out ThreadDoProcesso? thread)
    {
        thread = null;

        if (string.IsNullOrWhiteSpace(conteudoStat))
            return false;

        var linha = conteudoStat.TrimEnd('\n', '\r');

        var aberturaComm = linha.IndexOf('(');
        var fechamentoComm = linha.LastIndexOf(')');
        if (aberturaComm < 0 || fechamentoComm < 0 || fechamentoComm <= aberturaComm)
            return false;
        if (fechamentoComm + 1 >= linha.Length)
            return false;

        var resto = linha[(fechamentoComm + 1)..].TrimStart();
        var campos = resto.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (campos.Length <= IndicePrioridade || campos[IndiceEstado].Length == 0)
            return false;

        if (!long.TryParse(campos[IndiceUtime], NumberStyles.None, CultureInfo.InvariantCulture, out var utime))
            return false;
        if (!long.TryParse(campos[IndiceStime], NumberStyles.None, CultureInfo.InvariantCulture, out var stime))
            return false;
        if (!int.TryParse(campos[IndicePrioridade], NumberStyles.Integer, CultureInfo.InvariantCulture, out var prioridade))
            return false;

        var (estado, motivoDeBloqueio) = MapearEstado(campos[IndiceEstado][0]);
        var tempoDeCpu = ConverterTicksParaTempoDeCpu(utime + stime);

        thread = new ThreadDoProcesso(
            tid,
            estado,
            motivoDeBloqueio,
            Leitura<int>.Ok(prioridade),
            Leitura<TimeSpan>.Ok(tempoDeCpu));

        return true;
    }

    /// <summary>
    /// Mapeia o caractere de estado (campo 3) para o vocabulário do domínio. <c>D</c> (uninterruptible
    /// disk sleep) é conteúdo didático por si só: é o estado clássico de uma thread esperando E/S
    /// de disco sem poder ser interrompida nem por um sinal — Tanenbaum usa exatamente esse
    /// cenário para justificar por que E/S bloqueante é cara. Para os estados que não são
    /// Bloqueada, o motivo de bloqueio <see cref="Disponibilidade.NaoSeAplica"/>: a pergunta "por
    /// que está bloqueada" não faz sentido para uma thread em execução, terminada ou desconhecida.
    /// </summary>
    private static (EstadoThread Estado, Leitura<MotivoDeBloqueio> Motivo) MapearEstado(char estadoBruto) => estadoBruto switch
    {
        'R' => (EstadoThread.EmExecucao, Leitura<MotivoDeBloqueio>.NaoSeAplica()),
        'S' => (EstadoThread.Bloqueada, Leitura<MotivoDeBloqueio>.Ok(MotivoDeBloqueio.EsperandoEvento)),
        'D' => (EstadoThread.Bloqueada, Leitura<MotivoDeBloqueio>.Ok(MotivoDeBloqueio.EsperandoES)),
        'T' or 't' => (EstadoThread.Bloqueada, Leitura<MotivoDeBloqueio>.Ok(MotivoDeBloqueio.Suspensa)),
        'Z' => (EstadoThread.Terminada, Leitura<MotivoDeBloqueio>.NaoSeAplica()),
        'I' => (EstadoThread.Bloqueada, Leitura<MotivoDeBloqueio>.Ok(MotivoDeBloqueio.EsperandoEvento)),
        'X' => (EstadoThread.Terminada, Leitura<MotivoDeBloqueio>.NaoSeAplica()),
        _ => (EstadoThread.Desconhecido, Leitura<MotivoDeBloqueio>.NaoSeAplica())
    };

    /// <summary>
    /// Converte ticks de relógio do kernel em <see cref="TimeSpan"/> usando aritmética inteira
    /// exata (1 tick a 100 Hz = 10 ms = 100.000 ticks de <see cref="TimeSpan"/>), evitando o
    /// arredondamento de ponto flutuante que uma divisão por segundos introduziria.
    /// </summary>
    private static TimeSpan ConverterTicksParaTempoDeCpu(long ticksDeRelogio) =>
        TimeSpan.FromTicks(ticksDeRelogio * (TimeSpan.TicksPerSecond / TicksDeRelogioPorSegundo));
}
