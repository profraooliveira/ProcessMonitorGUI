using SO.Monitor.Dominio.Enums;

namespace SO.Monitor.Aplicacao.CasosDeUso;

/// <summary>
/// Critérios que moldam uma amostra classificada: por qual perfil de execução filtrar (se
/// algum) e quantos processos, no máximo, devolver — tipicamente os de maior uso de CPU, já que
/// <see cref="ObterAmostraClassificada"/> sempre ordena por uso de CPU decrescente antes de cortar.
/// </summary>
public sealed record CriteriosDeAmostragem(PerfilDeExecucao? FiltrarPorPerfil, int Limite)
{
    /// <summary>Critérios padrão: sem filtro de perfil, sem corte — devolve todos os processos coletados.</summary>
    public static CriteriosDeAmostragem Padrao => new(FiltrarPorPerfil: null, Limite: int.MaxValue);
}
