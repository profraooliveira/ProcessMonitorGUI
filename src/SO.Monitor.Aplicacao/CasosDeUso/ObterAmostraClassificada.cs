using SO.Monitor.Aplicacao.Ports;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.Servicos;

namespace SO.Monitor.Aplicacao.CasosDeUso;

/// <summary>
/// Caso de uso (GRASP Controller): coleta uma amostra do sistema, classifica cada processo com
/// a política informada, filtra pelo perfil de execução pedido e devolve os processos de maior
/// uso de CPU até o limite informado. Os registros do domínio são imutáveis, então cada etapa
/// deriva uma nova versão da amostra com <c>with</c> em vez de mutar o que a fonte devolveu.
/// </summary>
public sealed class ObterAmostraClassificada(IFonteDeProcessos fonte, IPoliticaDeClassificacao politica)
{
    /// <summary>Executa a coleta, classificação, filtro e corte descritos em <paramref name="criterios"/>.</summary>
    public async ValueTask<AmostraDoSistema> ExecutarAsync(
        CriteriosDeAmostragem criterios,
        CancellationToken cancellationToken)
    {
        var amostra = await fonte.ColetarAsync(cancellationToken).ConfigureAwait(false);

        var classificados = amostra.Processos
            .Select(processo => processo with
            {
                Classificacao = politica.Classificar(processo.MetricasDeExecucao)
            })
            .ToList();

        IEnumerable<Processo> filtrados = criterios.FiltrarPorPerfil is { } perfilDesejado
            ? classificados.Where(processo => processo.Classificacao!.Value.Perfil == perfilDesejado)
            : classificados;

        var processosFinais = filtrados
            .OrderByDescending(processo => processo.MetricasDeExecucao.UsoDeCpu.Valor)
            .Take(criterios.Limite)
            .ToList();

        // Processo selecionado na interface: fica sempre visível mesmo que sua %CPU caia fora do
        // corte por Limite nesta rodada — sem isso, uma oscilação de %CPU perto da borda derruba
        // a seleção do usuário a cada poucos ciclos. Só é pinado se ainda existir E ainda bater
        // com o filtro de perfil (ver doc de CriteriosDeAmostragem.PidFixado).
        if (criterios.PidFixado is { } pidFixado && processosFinais.All(processo => processo.Pid != pidFixado))
        {
            var processoFixado = classificados.FirstOrDefault(processo => processo.Pid == pidFixado);
            if (processoFixado is not null && filtrados.Contains(processoFixado))
                processosFinais.Add(processoFixado);
        }

        return amostra with { Processos = processosFinais };
    }
}
