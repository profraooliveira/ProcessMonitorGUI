using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Aplicacao.Ports;

/// <summary>
/// Porta para obter o mapa de memória real de um processo. O retorno é uma
/// <see cref="Leitura{T}"/>, não um <see cref="MapaDeMemoria"/> puro, porque o sistema
/// operacional pode legitimamente negar o acesso (processos protegidos) ou simplesmente não
/// suportar essa leitura na plataforma atual — um caso esperado do domínio, não uma exceção.
/// </summary>
public interface IFonteDeMapaDeMemoria
{
    /// <summary>Tenta obter o mapa de memória real do processo identificado por <paramref name="pid"/>.</summary>
    ValueTask<Leitura<MapaDeMemoria>> ObterMapaAsync(Pid pid, CancellationToken cancellationToken);
}
