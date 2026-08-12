using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.Servicos;
using SO.Monitor.Dominio.ValueObjects;
using Xunit;

namespace SO.Monitor.Testes.Dominio.Servicos;

public class PoliticaCompostaTests
{
    private sealed class PoliticaFalsa(PerfilDeExecucao perfil, string criterio) : IPoliticaDeClassificacao
    {
        public string Nome => "Falsa";

        public ResultadoDaClassificacao Classificar(MetricasDeExecucao metricas) => new(perfil, criterio);
    }

    private static readonly MetricasDeExecucao MetricasQuaisquer = new(
        UsoDeCpu: Percentual.Zero,
        TempoDeCpuAcumulado: TimeSpan.Zero,
        TempoDeVida: TimeSpan.Zero,
        ContagemDeHandles: Leitura<int>.NaoSuportada(),
        TotalDeThreads: 0,
        ThreadsBloqueadas: 0);

    [Fact]
    public void Classificar_PrimeiraPoliticaDecide_RetornaResultadoDaPrimeira()
    {
        var primeira = new PoliticaFalsa(PerfilDeExecucao.LimitadoPorCpu, "primeira decidiu");
        var segunda = new PoliticaFalsa(PerfilDeExecucao.LimitadoPorES, "segunda decidiu");
        var composta = new PoliticaComposta([primeira, segunda]);

        var resultado = composta.Classificar(MetricasQuaisquer);

        Assert.Equal(PerfilDeExecucao.LimitadoPorCpu, resultado.Perfil);
        Assert.Equal("primeira decidiu", resultado.Criterio);
    }

    [Fact]
    public void Classificar_PrimeiraIndeterminada_UsaResultadoDaSegunda()
    {
        var primeira = new PoliticaFalsa(PerfilDeExecucao.Indeterminado, "primeira não sabe");
        var segunda = new PoliticaFalsa(PerfilDeExecucao.LimitadoPorES, "segunda decidiu");
        var composta = new PoliticaComposta([primeira, segunda]);

        var resultado = composta.Classificar(MetricasQuaisquer);

        Assert.Equal(PerfilDeExecucao.LimitadoPorES, resultado.Perfil);
    }

    [Fact]
    public void Classificar_TodasIndeterminadas_RetornaIndeterminado()
    {
        var primeira = new PoliticaFalsa(PerfilDeExecucao.Indeterminado, "primeira não sabe");
        var segunda = new PoliticaFalsa(PerfilDeExecucao.Indeterminado, "segunda não sabe");
        var composta = new PoliticaComposta([primeira, segunda]);

        var resultado = composta.Classificar(MetricasQuaisquer);

        Assert.Equal(PerfilDeExecucao.Indeterminado, resultado.Perfil);
    }

    [Fact]
    public void Classificar_ListaVazia_RetornaIndeterminado()
    {
        var composta = new PoliticaComposta([]);

        var resultado = composta.Classificar(MetricasQuaisquer);

        Assert.Equal(PerfilDeExecucao.Indeterminado, resultado.Perfil);
    }
}
