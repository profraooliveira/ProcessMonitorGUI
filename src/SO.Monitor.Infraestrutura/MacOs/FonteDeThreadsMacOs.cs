using System.ComponentModel;
using SO.Monitor.Aplicacao.Ports;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Infraestrutura.MacOs;

/// <summary>
/// Fonte real de detalhes de threads no macOS: usa <c>ps -M -p &lt;pid&gt;</c>, a ferramenta de
/// linha de comando que expõe o mapa de threads de um processo sem exigir privilégios elevados
/// (ao contrário do vmmap, que o kernel restringe para processos de outros donos). Limitação
/// didaticamente relevante: o <c>ps</c> do macOS não expõe o TID real de cada thread — ver
/// <see cref="AnalisadorDeSaidaDoPs"/> para o porquê de usarmos um identificador posicional.
/// </summary>
public sealed class FonteDeThreadsMacOs : IFonteDeDetalhesDeThreads
{
    private const string CaminhoDoExecutavel = "/bin/ps";

    private readonly ExecutorDeComandoExterno _executor = new();

    /// <inheritdoc />
    public async ValueTask<Leitura<IReadOnlyList<ThreadDoProcesso>>> ObterThreadsAsync(Pid pid, CancellationToken cancellationToken)
    {
        ResultadoDoComando resultado;
        try
        {
            resultado = await _executor
                .ExecutarAsync(CaminhoDoExecutavel, new[] { "-M", "-p", pid.Valor.ToString() }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Win32Exception)
        {
            // /bin/ps não existe nesta máquina — não deveria acontecer num macOS real, mas é
            // honesto tratar como "esta plataforma não suporta" em vez de propagar.
            return Leitura<IReadOnlyList<ThreadDoProcesso>>.NaoSuportada();
        }
        catch (TimeoutException)
        {
            return Leitura<IReadOnlyList<ThreadDoProcesso>>.Negada();
        }

        if (resultado.CodigoDeSaida != 0)
            return Leitura<IReadOnlyList<ThreadDoProcesso>>.Negada();

        var threads = AnalisadorDeSaidaDoPs.AnalisarThreads(resultado.Stdout);
        if (threads.Count == 0)
            return Leitura<IReadOnlyList<ThreadDoProcesso>>.Negada();

        return Leitura<IReadOnlyList<ThreadDoProcesso>>.Ok(threads);
    }
}
