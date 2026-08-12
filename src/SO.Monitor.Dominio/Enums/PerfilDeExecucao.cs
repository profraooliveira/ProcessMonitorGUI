namespace SO.Monitor.Dominio.Enums;

/// <summary>
/// Classificação do padrão de comportamento de um processo, no sentido de Tanenbaum: processos
/// alternam rajadas ("bursts") de CPU e de E/S, e o perfil predominante determina que tipo de
/// escalonamento serve melhor a esse processo.
/// </summary>
public enum PerfilDeExecucao
{
    /// <summary>Rajadas longas de computação entre operações de E/S — o processo satura a CPU.</summary>
    LimitadoPorCpu,

    /// <summary>Rajadas curtas de CPU intercaladas com bloqueios frequentes de E/S.</summary>
    LimitadoPorES,

    /// <summary>Nenhum padrão dominante foi observado — a política tem o direito de não saber.</summary>
    Indeterminado
}
