using System.Globalization;
using System.Text.RegularExpressions;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Infraestrutura.MacOs;

/// <summary>
/// Analisador puro (sem I/O) da saída de <c>ps -M -p &lt;pid&gt;</c> no macOS: colunas
/// USER PID TT %CPU STAT PRI STIME UTIME COMMAND, uma linha por thread (a primeira linha traz
/// também USER/TT/COMMAND, que as linhas seguintes deixam em branco, mas STAT/PRI/STIME/UTIME
/// aparecem em toda linha — é a partir delas que reconstruímos cada <see cref="ThreadDoProcesso"/>).
/// </summary>
internal static class AnalisadorDeSaidaDoPs
{
    /// <summary>Identifica a coluna %CPU (único token puramente decimal da linha) para ancorar a leitura das colunas seguintes.</summary>
    private static readonly Regex RegexPercentualDeCpu = new(@"^\d+\.\d+$", RegexOptions.Compiled);

    /// <summary>Formato de tempo do ps: "M:SS.ss" (minutos:segundos.centésimos) ou "HH:MM:SS".</summary>
    private static readonly Regex RegexTempo = new(@"^\d+(:\d{2}){1,2}(\.\d{1,2})?$", RegexOptions.Compiled);

    /// <summary>
    /// Extrai as threads descritas na saída de <c>ps -M -p &lt;pid&gt;</c>. O macOS não expõe
    /// TIDs reais nessa ferramenta (ao contrário do Linux, onde /proc/&lt;pid&gt;/task lista os
    /// TIDs de verdade); por isso o <see cref="Tid"/> de cada thread aqui é apenas sua posição
    /// ordinal na saída do comando (0, 1, 2...) — uma limitação da ferramenta, não do domínio.
    /// </summary>
    public static IReadOnlyList<ThreadDoProcesso> AnalisarThreads(string saidaDoPs)
    {
        var threads = new List<ThreadDoProcesso>();
        var indiceOrdinal = 0;

        foreach (var linhaBruta in saidaDoPs.Split('\n'))
        {
            var linha = linhaBruta.Trim();
            if (linha.Length == 0)
                continue;

            var thread = TentarAnalisarLinha(linha, indiceOrdinal);
            if (thread is null)
                continue;

            threads.Add(thread);
            indiceOrdinal++;
        }

        return threads;
    }

    private static ThreadDoProcesso? TentarAnalisarLinha(string linha, int indiceOrdinal)
    {
        var tokens = linha.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        var indiceCpu = Array.FindIndex(tokens, token => RegexPercentualDeCpu.IsMatch(token));
        if (indiceCpu < 0 || indiceCpu + 4 >= tokens.Length)
            return null; // Linha sem o formato esperado (ex.: cabeçalho "USER PID TT ...") — pulamos.

        var statBruto = tokens[indiceCpu + 1];
        var priBruto = tokens[indiceCpu + 2];
        var stimeBruto = tokens[indiceCpu + 3];
        var utimeBruto = tokens[indiceCpu + 4];

        var estado = ClassificarEstado(statBruto, out var motivoDeBloqueio);
        var prioridade = AnalisarPrioridade(priBruto);
        var tempoDeCpu = AnalisarTempoTotal(stimeBruto, utimeBruto);

        return new ThreadDoProcesso(new Tid(indiceOrdinal), estado, motivoDeBloqueio, prioridade, tempoDeCpu, TidEhPosicional: true);
    }

    /// <summary>
    /// Mapeia o primeiro caractere de STAT para <see cref="EstadoThread"/>: R (executando), S/I
    /// (bloqueada esperando evento), U (bloqueada em E/S não-interrompível — tipicamente disco,
    /// um estado didaticamente interessante: nem sinal consegue tirar a thread dele), T
    /// (suspensa), Z (terminada/zumbi). O motivo de bloqueio só é observável quando o estado é
    /// de fato Bloqueada; fora disso o motivo <see cref="Disponibilidade.NaoSeAplica"/> — mesmo
    /// padrão usado em <c>FonteDeProcessosDotNet.LerMotivoDeBloqueio</c>.
    /// </summary>
    private static EstadoThread ClassificarEstado(string statBruto, out Leitura<MotivoDeBloqueio> motivoDeBloqueio)
    {
        var primeiroCaractere = statBruto.Length > 0 ? statBruto[0] : '\0';

        var estado = primeiroCaractere switch
        {
            'R' => EstadoThread.EmExecucao,
            'S' or 'I' or 'U' or 'T' => EstadoThread.Bloqueada,
            'Z' => EstadoThread.Terminada,
            _ => EstadoThread.Desconhecido
        };

        motivoDeBloqueio = estado == EstadoThread.Bloqueada
            ? Leitura<MotivoDeBloqueio>.Ok(MotivoDeBloqueioPara(primeiroCaractere))
            : Leitura<MotivoDeBloqueio>.NaoSeAplica();

        return estado;
    }

    private static MotivoDeBloqueio MotivoDeBloqueioPara(char primeiroCaractereDoStat) => primeiroCaractereDoStat switch
    {
        'S' or 'I' => MotivoDeBloqueio.EsperandoEvento,
        'U' => MotivoDeBloqueio.EsperandoES,
        'T' => MotivoDeBloqueio.Suspensa,
        _ => MotivoDeBloqueio.Desconhecido
    };

    /// <summary>Interpreta PRI de forma tolerante: o ps do macOS sufixa a prioridade com uma letra (ex.: "46T"), que descartamos.</summary>
    private static Leitura<int> AnalisarPrioridade(string priBruto)
    {
        var digitos = new string(priBruto.TakeWhile(char.IsDigit).ToArray());
        return digitos.Length > 0 && int.TryParse(digitos, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor)
            ? Leitura<int>.Ok(valor)
            : Leitura<int>.NaoSuportada();
    }

    /// <summary>Soma STIME (tempo em modo sistema) e UTIME (tempo em modo usuário) no tempo total de CPU da thread.</summary>
    private static Leitura<TimeSpan> AnalisarTempoTotal(string stimeBruto, string utimeBruto)
    {
        if (!TentarAnalisarTempo(stimeBruto, out var stime) || !TentarAnalisarTempo(utimeBruto, out var utime))
            return Leitura<TimeSpan>.NaoSuportada();

        return Leitura<TimeSpan>.Ok(stime + utime);
    }

    /// <summary>Interpreta um tempo do ps nos formatos "M:SS.ss" ou "HH:MM:SS".</summary>
    private static bool TentarAnalisarTempo(string texto, out TimeSpan tempo)
    {
        tempo = TimeSpan.Zero;

        if (!RegexTempo.IsMatch(texto))
            return false;

        var indiceDoPonto = texto.IndexOf('.');
        var parteInteira = indiceDoPonto >= 0 ? texto[..indiceDoPonto] : texto;
        var partes = parteInteira.Split(':');

        int horas, minutos, segundos;
        if (partes.Length == 3)
        {
            if (!int.TryParse(partes[0], out horas) || !int.TryParse(partes[1], out minutos) || !int.TryParse(partes[2], out segundos))
                return false;
        }
        else if (partes.Length == 2)
        {
            horas = 0;
            if (!int.TryParse(partes[0], out minutos) || !int.TryParse(partes[1], out segundos))
                return false;
        }
        else
        {
            return false;
        }

        var milissegundos = 0;
        if (indiceDoPonto >= 0)
        {
            var textoCentesimos = texto[(indiceDoPonto + 1)..].PadRight(2, '0');
            if (!int.TryParse(textoCentesimos, out var centesimos))
                return false;
            milissegundos = centesimos * 10;
        }

        tempo = new TimeSpan(0, horas, minutos, segundos, milissegundos);
        return true;
    }
}
