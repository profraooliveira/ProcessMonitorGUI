using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;

namespace SO.Monitor.Dominio.Servicos;

/// <summary>
/// Classifica processos pelo critério clássico de Tanenbaum: processos limitados por CPU têm
/// rajadas longas de computação entre operações de E/S; processos limitados por E/S têm rajadas
/// curtas de CPU e passam a maior parte do tempo com suas threads bloqueadas esperando E/S.
/// </summary>
public sealed class PoliticaPorComportamentoDeBurst : IPoliticaDeClassificacao
{
    /// <summary>
    /// Limiar de uso de CPU acima do qual o processo é considerado limitado por CPU: 25% de um
    /// núcleo já indica uma rajada de computação relevante, não um pico esporádico e passageiro.
    /// </summary>
    private const double LimiarDeCpuAltoPercentual = 25.0;

    /// <summary>
    /// Fração mínima de threads bloqueadas para considerar o processo limitado por E/S: acima
    /// de 60% das threads esperando é um padrão de bloqueio frequente, não ruído estatístico.
    /// </summary>
    private const double LimiarDeThreadsBloqueadas = 0.6;

    public string Nome => "Comportamento de burst (CPU vs. E/S)";

    public ResultadoDaClassificacao Classificar(MetricasDeExecucao metricas)
    {
        if (metricas.UsoDeCpu.Valor >= LimiarDeCpuAltoPercentual)
        {
            return new ResultadoDaClassificacao(
                PerfilDeExecucao.LimitadoPorCpu,
                $"Uso de CPU de {metricas.UsoDeCpu.Valor:F1}% >= {LimiarDeCpuAltoPercentual:F0}%: " +
                "rajadas longas de computação, típicas de processo limitado por CPU.");
        }

        var fracaoBloqueada = metricas.TotalDeThreads > 0
            ? (double)metricas.ThreadsBloqueadas / metricas.TotalDeThreads
            : 0.0;

        if (fracaoBloqueada >= LimiarDeThreadsBloqueadas)
        {
            return new ResultadoDaClassificacao(
                PerfilDeExecucao.LimitadoPorES,
                $"{metricas.ThreadsBloqueadas}/{metricas.TotalDeThreads} threads bloqueadas " +
                $"({fracaoBloqueada:P0}) >= {LimiarDeThreadsBloqueadas:P0}: bloqueios frequentes de E/S.");
        }

        return new ResultadoDaClassificacao(
            PerfilDeExecucao.Indeterminado,
            "Uso de CPU baixo e poucas threads bloqueadas: nenhum padrão claro de CPU nem de E/S.");
    }
}
