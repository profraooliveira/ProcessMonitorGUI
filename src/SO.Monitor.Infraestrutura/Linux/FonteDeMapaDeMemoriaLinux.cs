using SO.Monitor.Aplicacao.Ports;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Infraestrutura.Linux;

/// <summary>
/// Fonte real de mapa de memória no Linux, lendo diretamente de <c>/proc/&lt;pid&gt;/smaps</c>
/// (preferido, pois já traz residência por região) com fallback para <c>/proc/&lt;pid&gt;/maps</c>
/// quando <c>smaps</c> não existe nesta configuração de kernel (ex.: <c>CONFIG_PROC_PAGE_MONITOR</c>
/// desabilitado) — nesse caso o mapa ainda é real, só sem informação de residência por região.
/// </summary>
public sealed class FonteDeMapaDeMemoriaLinux : IFonteDeMapaDeMemoria
{
    public async ValueTask<Leitura<MapaDeMemoria>> ObterMapaAsync(Pid pid, CancellationToken cancellationToken)
    {
        var caminhoSmaps = $"/proc/{pid.Valor}/smaps";
        var caminhoMaps = $"/proc/{pid.Valor}/maps";

        try
        {
            IReadOnlyList<RegiaoDeMemoria> regioes;

            if (File.Exists(caminhoSmaps))
            {
                var conteudo = await File.ReadAllTextAsync(caminhoSmaps, cancellationToken).ConfigureAwait(false);
                regioes = AnalisadorDoProcSmaps.Analisar(conteudo);
            }
            else
            {
                var conteudo = await File.ReadAllTextAsync(caminhoMaps, cancellationToken).ConfigureAwait(false);
                regioes = AnalisadorDoProcMaps.Analisar(conteudo);
            }

            var mapa = new MapaDeMemoria(pid, OrigemDosDados.MedidaReal, regioes, DateTimeOffset.UtcNow);
            return Leitura<MapaDeMemoria>.Ok(mapa);
        }
        catch (UnauthorizedAccessException)
        {
            // Didático: o kernel nega a leitura de /proc/<pid>/{maps,smaps} de processos que não
            // pertencem ao usuário atual (fora de CAP_SYS_PTRACE) — a mesma proteção de memória
            // entre processos que o Tanenbaum descreve como razão de ser da memória virtual.
            return Leitura<MapaDeMemoria>.Negada();
        }
        catch (FileNotFoundException)
        {
            // O processo terminou entre a checagem de existência e a leitura do arquivo.
            return Leitura<MapaDeMemoria>.ProcessoEncerrado();
        }
        catch (DirectoryNotFoundException)
        {
            // /proc/<pid> nem existe: o processo já havia terminado (ou nunca existiu).
            return Leitura<MapaDeMemoria>.ProcessoEncerrado();
        }
    }
}
