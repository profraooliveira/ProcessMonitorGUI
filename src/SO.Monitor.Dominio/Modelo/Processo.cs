using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Servicos;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Dominio.Modelo;

/// <summary>
/// Um processo do sistema operacional — a unidade de alocação de recursos (memória, arquivos
/// abertos) e, em Tanenbaum, o "contêiner" dentro do qual as threads executam. Uma thread não
/// tem vida fora de um processo: por isso <see cref="Processo"/> é a raiz do agregado, e
/// <see cref="ThreadDoProcesso"/> só existe pendurada em uma lista de <see cref="Threads"/>.
/// </summary>
public sealed record Processo(
    Pid Pid,
    string Nome,
    Leitura<string> CaminhoDoExecutavel,
    EstadoProcesso EstadoProcesso,
    Leitura<DateTimeOffset> InicioDaExecucao,
    MetricasDeExecucao MetricasDeExecucao,
    PerfilDeMemoria PerfilDeMemoria,
    Leitura<int> PrioridadeBase,
    IReadOnlyList<ThreadDoProcesso> Threads,
    ResultadoDaClassificacao? Classificacao = null);
