using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Dominio.Modelo;

/// <summary>
/// Métricas agregadas de execução de um processo em um instante de amostragem: quanto de CPU
/// ele está consumindo agora, há quanto tempo existe, e como suas threads se distribuem entre
/// execução e bloqueio — a base sobre a qual políticas de classificação decidem se o processo
/// é limitado por CPU ou por E/S.
/// </summary>
public readonly record struct MetricasDeExecucao(
    Percentual UsoDeCpu,
    TimeSpan TempoDeCpuAcumulado,
    TimeSpan TempoDeVida,
    Leitura<int> ContagemDeHandles,
    int TotalDeThreads,
    int ThreadsBloqueadas);
