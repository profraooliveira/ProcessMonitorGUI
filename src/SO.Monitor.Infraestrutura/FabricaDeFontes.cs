using SO.Monitor.Aplicacao.Ports;
using SO.Monitor.Infraestrutura.Linux;
using SO.Monitor.Infraestrutura.MacOs;
using SO.Monitor.Infraestrutura.Windows;

namespace SO.Monitor.Infraestrutura;

/// <summary>
/// Fábrica (Factory Method) das fontes de dados reais, escolhendo a implementação adequada à
/// plataforma atual. Usa <see cref="OperatingSystem"/> como guarda: é o padrão que o analisador
/// de plataforma (CA1416) reconhece, e o JIT resolve essas checagens como constantes.
/// </summary>
public static class FabricaDeFontes
{
    /// <summary>
    /// Cria a fonte de processos adequada à plataforma atual. Neste baseline, uma única
    /// implementação sobre <c>System.Diagnostics.Process</c> já é portátil o bastante para
    /// atender as três plataformas.
    /// </summary>
    public static IFonteDeProcessos CriarFonteDeProcessos() => new FonteDeProcessosDotNet();

    /// <summary>
    /// Cria a fonte de mapa de memória REAL adequada à plataforma atual:
    /// <c>/proc/&lt;pid&gt;/smaps</c> no Linux, <c>vmmap</c> no macOS e
    /// <c>VirtualQueryEx</c>+<c>QueryWorkingSetEx</c> no Windows. Em plataformas fora dessas
    /// três, o stub "não suportado" aciona o fallback simulado (rotulado) do caso de uso
    /// <c>ObterMapaDeMemoria</c>.
    /// </summary>
    public static IFonteDeMapaDeMemoria CriarFonteDeMapaDeMemoria()
    {
        if (OperatingSystem.IsWindows())
        {
            return new FonteDeMapaDeMemoriaWindows();
        }

        if (OperatingSystem.IsLinux())
        {
            return new FonteDeMapaDeMemoriaLinux();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new FonteDeMapaDeMemoriaMacOs();
        }

        return new FonteDeMapaDeMemoriaIndisponivel();
    }

    /// <summary>
    /// Cria a fonte de detalhes finos de threads adequada à plataforma atual: <c>ps -M</c> no
    /// macOS e <c>/proc/&lt;pid&gt;/task/*/stat</c> no Linux. No Windows o baseline
    /// <see cref="FonteDeProcessosDotNet"/> já é fiel (<c>ThreadState</c>/<c>WaitReason</c>
    /// funcionam nativamente), então o Null Object indica ao chamador que use as threads da
    /// própria amostra.
    /// </summary>
    public static IFonteDeDetalhesDeThreads CriarFonteDeDetalhesDeThreads()
    {
        if (OperatingSystem.IsLinux())
        {
            return new FonteDeThreadsLinux();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new FonteDeThreadsMacOs();
        }

        return new FonteDeDetalhesDeThreadsIndisponivel();
    }
}
