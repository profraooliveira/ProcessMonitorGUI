using System.Diagnostics;
using System.Text;

namespace SO.Monitor.Infraestrutura.MacOs;

/// <summary>
/// Único ponto deste projeto que efetivamente inicia um processo externo (<c>vmmap</c>,
/// <c>ps</c>...) e captura sua saída. Isolar essa responsabilidade aqui mantém os analisadores
/// (<see cref="AnalisadorDeSaidaDoVmmap"/>, <see cref="AnalisadorDeSaidaDoPs"/>) como funções
/// puras — sem processo externo nenhum — o que os torna triviais de testar com amostras fixas.
/// </summary>
internal sealed class ExecutorDeComandoExterno
{
    private static readonly TimeSpan TimeoutPadrao = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Executa <paramref name="caminhoDoExecutavel"/> com os argumentos informados e aguarda sua
    /// conclusão. Se o processo não terminar dentro de <paramref name="timeout"/> (10 segundos
    /// por padrão) ou se <paramref name="cancellationToken"/> for cancelado antes disso, o
    /// processo é morto — nunca fica órfão rodando em segundo plano.
    /// </summary>
    public async Task<ResultadoDoComando> ExecutarAsync(
        string caminhoDoExecutavel,
        IReadOnlyList<string> argumentos,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        var infoDeInicio = new ProcessStartInfo(caminhoDoExecutavel)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argumento in argumentos)
            infoDeInicio.ArgumentList.Add(argumento);

        using var processo = new Process { StartInfo = infoDeInicio };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        processo.OutputDataReceived += (_, dados) =>
        {
            if (dados.Data is not null)
                stdout.AppendLine(dados.Data);
        };
        processo.ErrorDataReceived += (_, dados) =>
        {
            if (dados.Data is not null)
                stderr.AppendLine(dados.Data);
        };

        processo.Start();
        processo.BeginOutputReadLine();
        processo.BeginErrorReadLine();

        using var tokenComTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        tokenComTimeout.CancelAfter(timeout ?? TimeoutPadrao);

        try
        {
            await processo.WaitForExitAsync(tokenComTimeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            MatarSemFalhar(processo);

            // Se o token do chamador foi cancelado, respeitamos o contrato usual de
            // cancelamento (a exceção carrega o token original). Caso contrário, quem expirou
            // foi o timeout interno — um problema distinto, sinalizado como TimeoutException.
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException(
                $"O comando '{caminhoDoExecutavel}' não terminou dentro do tempo limite de {(timeout ?? TimeoutPadrao).TotalSeconds:0}s.");
        }

        // Após a conclusão assíncrona, um WaitForExit síncrono (sem timeout, processo já
        // encerrado) garante que os eventos pendentes de OutputDataReceived/ErrorDataReceived já
        // disparados sejam totalmente processados antes de lermos os StringBuilders — uma
        // particularidade documentada da leitura assíncrona de saída redirecionada no .NET.
        processo.WaitForExit();

        return new ResultadoDoComando(processo.ExitCode, stdout.ToString(), stderr.ToString());
    }

    /// <summary>Tenta matar o processo, tolerando a corrida em que ele já terminou sozinho entre a checagem e o Kill.</summary>
    private static void MatarSemFalhar(Process processo)
    {
        try
        {
            if (!processo.HasExited)
                processo.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException excecao)
        {
            // O processo terminou entre a checagem HasExited e a chamada a Kill — não é uma
            // falha real, é exatamente o resultado que queríamos (processo não ficou órfão).
            Debug.WriteLine($"Processo externo já havia terminado ao tentar matá-lo: {excecao.Message}");
        }
    }
}

/// <summary>Resultado bruto (ainda não interpretado) da execução de um comando externo.</summary>
internal sealed record ResultadoDoComando(int CodigoDeSaida, string Stdout, string Stderr);
