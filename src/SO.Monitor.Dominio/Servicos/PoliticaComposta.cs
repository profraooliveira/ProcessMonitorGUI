using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;

namespace SO.Monitor.Dominio.Servicos;

/// <summary>
/// Combina várias políticas em uma cadeia de responsabilidade: tenta cada uma, na ordem
/// informada, e usa o primeiro resultado que não seja Indeterminado. Se todas não souberem
/// responder, o resultado final também é Indeterminado — a incerteza se propaga, não é escondida.
/// </summary>
public sealed class PoliticaComposta(IReadOnlyList<IPoliticaDeClassificacao> politicas) : IPoliticaDeClassificacao
{
    public string Nome => "Composta (" + string.Join(" -> ", politicas.Select(politica => politica.Nome)) + ")";

    public ResultadoDaClassificacao Classificar(MetricasDeExecucao metricas)
    {
        foreach (var politica in politicas)
        {
            var resultado = politica.Classificar(metricas);
            if (resultado.Perfil != PerfilDeExecucao.Indeterminado)
                return resultado;
        }

        return new ResultadoDaClassificacao(
            PerfilDeExecucao.Indeterminado,
            "Nenhuma política da cadeia conseguiu classificar o processo com confiança.");
    }
}
