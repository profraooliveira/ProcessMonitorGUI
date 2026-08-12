using SO.Monitor.Aplicacao.Ports;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Infraestrutura;

/// <summary>
/// Fonte de mapa de memória baseline: Windows, Linux e macOS já têm provedores reais próprios
/// (<c>FonteDeMapaDeMemoriaWindows</c>, <c>FonteDeMapaDeMemoriaLinux</c>,
/// <c>FonteDeMapaDeMemoriaMacOs</c>, escolhidos por <c>FabricaDeFontes</c>) — esta classe só é
/// instanciada fora dessas três plataformas. Toda chamada devolve "não suportado", o que faz o
/// caso de uso <c>ObterMapaDeMemoria</c> cair, de forma honesta e rotulada, para o mapa simulado
/// do domínio.
/// </summary>
public sealed class FonteDeMapaDeMemoriaIndisponivel : IFonteDeMapaDeMemoria
{
    public ValueTask<Leitura<MapaDeMemoria>> ObterMapaAsync(Pid pid, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Leitura<MapaDeMemoria>.NaoSuportada());
}
