using System.Globalization;
using SO.Monitor.Aplicacao.Ports;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Infraestrutura.Linux;

/// <summary>
/// Fonte real de detalhes de threads no Linux, enumerando <c>/proc/&lt;pid&gt;/task/</c> — um
/// subdiretório por thread, cujo nome é o próprio TID — e lendo o <c>stat</c> de cada uma.
/// </summary>
public sealed class FonteDeThreadsLinux : IFonteDeDetalhesDeThreads
{
    public async ValueTask<Leitura<IReadOnlyList<ThreadDoProcesso>>> ObterThreadsAsync(Pid pid, CancellationToken cancellationToken)
    {
        var caminhoTask = $"/proc/{pid.Valor}/task";

        try
        {
            var threads = new List<ThreadDoProcesso>();

            foreach (var diretorioDaThread in Directory.EnumerateDirectories(caminhoTask))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var nomeDoDiretorio = Path.GetFileName(diretorioDaThread);
                if (!long.TryParse(nomeDoDiretorio, NumberStyles.None, CultureInfo.InvariantCulture, out var tidValor))
                    continue; // entrada inesperada dentro de task/ (não deveria acontecer); ignorada

                var caminhoStat = Path.Combine(diretorioDaThread, "stat");

                string conteudoStat;
                try
                {
                    conteudoStat = await File.ReadAllTextAsync(caminhoStat, cancellationToken).ConfigureAwait(false);
                }
                catch (FileNotFoundException)
                {
                    // Janela de corrida: a thread terminou entre a enumeração do diretório e a
                    // leitura do seu stat. Não invalida o processo inteiro — só essa thread não
                    // entra na lista desta amostra.
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }

                if (AnalisadorDoProcTaskStat.TentarAnalisar(conteudoStat, new Tid(tidValor), out var thread))
                    threads.Add(thread!);
            }

            return Leitura<IReadOnlyList<ThreadDoProcesso>>.Ok(threads);
        }
        catch (UnauthorizedAccessException)
        {
            return Leitura<IReadOnlyList<ThreadDoProcesso>>.Negada();
        }
        catch (FileNotFoundException)
        {
            return Leitura<IReadOnlyList<ThreadDoProcesso>>.ProcessoEncerrado();
        }
        catch (DirectoryNotFoundException)
        {
            return Leitura<IReadOnlyList<ThreadDoProcesso>>.ProcessoEncerrado();
        }
    }
}
