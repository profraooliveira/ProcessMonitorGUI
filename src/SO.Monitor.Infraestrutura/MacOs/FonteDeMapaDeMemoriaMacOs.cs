using System.ComponentModel;
using SO.Monitor.Aplicacao.Ports;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Infraestrutura.MacOs;

/// <summary>
/// Fonte real de mapa de memória no macOS: executa <c>vmmap -interleaved &lt;pid&gt;</c>, a
/// ferramenta padrão da Apple para inspecionar o espaço de endereçamento virtual de um processo.
/// Processos protegidos (SIP, hardened runtime, ou simplesmente donos de outro usuário) fazem o
/// vmmap terminar com código de saída diferente de zero — tipicamente 255 — e essa negação é
/// conteúdo didático propositalmente exposto como <see cref="Leitura{T}.Negada"/>, não um erro a
/// esconder: é a mesma proteção de memória entre processos que Tanenbaum descreve.
/// </summary>
public sealed class FonteDeMapaDeMemoriaMacOs : IFonteDeMapaDeMemoria
{
    private const string CaminhoDoExecutavel = "/usr/bin/vmmap";

    private readonly ExecutorDeComandoExterno _executor = new();

    /// <inheritdoc />
    public async ValueTask<Leitura<MapaDeMemoria>> ObterMapaAsync(Pid pid, CancellationToken cancellationToken)
    {
        ResultadoDoComando resultado;
        try
        {
            resultado = await _executor
                .ExecutarAsync(CaminhoDoExecutavel, new[] { "-interleaved", pid.Valor.ToString() }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Win32Exception)
        {
            // /usr/bin/vmmap não existe nesta máquina — não deveria acontecer num macOS real,
            // mas é honesto tratar como "esta plataforma não suporta" em vez de propagar.
            return Leitura<MapaDeMemoria>.NaoSuportada();
        }
        catch (TimeoutException)
        {
            return Leitura<MapaDeMemoria>.Negada();
        }

        if (resultado.CodigoDeSaida != 0)
            return Leitura<MapaDeMemoria>.Negada();

        var regioes = AnalisadorDeSaidaDoVmmap.AnalisarRegioes(resultado.Stdout);
        var mapa = new MapaDeMemoria(pid, OrigemDosDados.MedidaReal, regioes, DateTimeOffset.UtcNow);
        return Leitura<MapaDeMemoria>.Ok(mapa);
    }
}
