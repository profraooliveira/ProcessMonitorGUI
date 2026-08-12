using SO.Monitor.Aplicacao.Ports;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.Servicos;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Aplicacao.CasosDeUso;

/// <summary>
/// Caso de uso: obtém o mapa de memória real de um processo através de <see cref="IFonteDeMapaDeMemoria"/>;
/// quando o sistema operacional nega o acesso ou não suporta a leitura nesta plataforma, cai
/// para um mapa simulado (<see cref="SimuladorDePaginacao"/>) — mas nunca em silêncio: o motivo
/// do fallback acompanha o resultado, para a interface sempre rotular o que é simulado e por quê.
/// </summary>
public sealed class ObterMapaDeMemoria(IFonteDeMapaDeMemoria fonte)
{
    /// <summary>
    /// Tenta obter o mapa real do processo <paramref name="pid"/>; se a fonte real não tiver o
    /// valor, simula um mapa plausível a partir do perfil de memória e do tamanho de página
    /// informados, preservando o motivo original da falha para exibição na interface.
    /// </summary>
    public async ValueTask<ResultadoDoMapa> ExecutarAsync(
        Pid pid,
        PerfilDeMemoria perfilDeMemoria,
        TamanhoPagina tamanhoPagina,
        CancellationToken cancellationToken)
    {
        var leitura = await fonte.ObterMapaAsync(pid, cancellationToken).ConfigureAwait(false);

        if (leitura.TemValor)
            return new ResultadoDoMapa(leitura.Valor!, MotivoDoFallback: null);

        var mapaSimulado = SimuladorDePaginacao.Simular(
            pid,
            perfilDeMemoria.ConjuntoResidente.ValorOu(TamanhoBytes.Zero),
            perfilDeMemoria.MemoriaVirtual.ValorOu(TamanhoBytes.Zero),
            tamanhoPagina,
            DateTimeOffset.UtcNow);

        return new ResultadoDoMapa(mapaSimulado, MotivoDoFallback: leitura.Estado);
    }
}
