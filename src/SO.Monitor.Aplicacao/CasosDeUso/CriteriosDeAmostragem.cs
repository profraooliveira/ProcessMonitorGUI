using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Aplicacao.CasosDeUso;

/// <summary>
/// Critérios que moldam uma amostra classificada: por qual perfil de execução filtrar (se
/// algum) e quantos processos, no máximo, devolver — tipicamente os de maior uso de CPU, já que
/// <see cref="ObterAmostraClassificada"/> sempre ordena por uso de CPU decrescente antes de cortar.
/// </summary>
/// <param name="PidFixado">
/// PID que deve permanecer na amostra mesmo que caia fora do corte por <see cref="Limite"/> — o
/// processo selecionado na interface, para que sua %CPU oscilar perto da borda do "Limite" nunca
/// derrube a seleção do usuário. NÃO ignora <see cref="FiltrarPorPerfil"/>: se o perfil do
/// processo não bater com o filtro pedido, ele some mesmo assim — isso é o filtro funcionando
/// como pedido, diferente do corte por Limite (que é só um teto de exibição, não uma exclusão
/// deliberada).
/// </param>
public sealed record CriteriosDeAmostragem(PerfilDeExecucao? FiltrarPorPerfil, int Limite, Pid? PidFixado = null)
{
    /// <summary>Critérios padrão: sem filtro de perfil, sem corte — devolve todos os processos coletados.</summary>
    public static CriteriosDeAmostragem Padrao => new(FiltrarPorPerfil: null, Limite: int.MaxValue);
}
