using SO.Monitor.Dominio.Modelo;

namespace SO.Monitor.Dominio.Servicos;

/// <summary>
/// Uma política de classificação de processos entre CPU-bound e I/O-bound. Cada implementação
/// representa uma heurística diferente — nenhuma é "a verdade absoluta"; cada uma tem o direito
/// de dizer que não sabe (<see cref="Enums.PerfilDeExecucao.Indeterminado"/>) em vez de chutar.
/// </summary>
public interface IPoliticaDeClassificacao
{
    /// <summary>Nome descritivo da política, usado em diagnóstico e na interface.</summary>
    string Nome { get; }

    /// <summary>Classifica o processo a partir de suas métricas de execução mais recentes.</summary>
    ResultadoDaClassificacao Classificar(MetricasDeExecucao metricas);
}
