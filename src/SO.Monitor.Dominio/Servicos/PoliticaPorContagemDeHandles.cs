using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;

namespace SO.Monitor.Dominio.Servicos;

/// <summary>
/// A heurística original do projeto — contagem de handles/descritores abertos como sinal de
/// processo limitado por E/S — agora isolada como política nomeada e testável. Não é observável
/// em todas as plataformas: no macOS a contagem de handles não é exposta, e a política admite
/// essa limitação em vez de inventar um número.
/// </summary>
public sealed class PoliticaPorContagemDeHandles : IPoliticaDeClassificacao
{
    /// <summary>Acima deste número de handles abertos, o processo é considerado limitado por E/S.</summary>
    private const int LimiarDeHandles = 500;

    public string Nome => "Contagem de handles abertos";

    public ResultadoDaClassificacao Classificar(MetricasDeExecucao metricas)
    {
        if (!metricas.ContagemDeHandles.TemValor)
        {
            return new ResultadoDaClassificacao(
                PerfilDeExecucao.Indeterminado,
                "Contagem de handles indisponível nesta plataforma (ex.: o macOS não a expõe).");
        }

        var handles = metricas.ContagemDeHandles.Valor;

        return handles > LimiarDeHandles
            ? new ResultadoDaClassificacao(
                PerfilDeExecucao.LimitadoPorES,
                $"{handles} handles abertos > {LimiarDeHandles}: muitos recursos de E/S em uso simultâneo.")
            : new ResultadoDaClassificacao(
                PerfilDeExecucao.Indeterminado,
                $"{handles} handles abertos, dentro do normal: sem sinal claro de E/S.");
    }
}
