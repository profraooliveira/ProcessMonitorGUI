using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Aplicacao.Ports;

/// <summary>
/// Porta para obter, sob demanda, os detalhes reais das threads de um único processo
/// (estados, prioridades, tempos de CPU) com a melhor precisão que a plataforma oferecer.
/// Existe separada de <see cref="IFonteDeProcessos"/> porque enriquecer as threads de
/// TODOS os processos a cada ciclo seria caro — a interface exibe detalhes apenas do
/// processo selecionado, então a leitura fina acontece só para ele.
/// </summary>
public interface IFonteDeDetalhesDeThreads
{
    /// <summary>
    /// Tenta obter as threads do processo com estados reais da plataforma. Quando a leitura
    /// não está disponível (plataforma sem provedor, processo protegido), o chamador deve
    /// recorrer às threads já presentes na amostra coletada por <see cref="IFonteDeProcessos"/>.
    /// </summary>
    ValueTask<Leitura<IReadOnlyList<ThreadDoProcesso>>> ObterThreadsAsync(Pid pid, CancellationToken cancellationToken);
}
