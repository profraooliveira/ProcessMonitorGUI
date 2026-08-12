using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.Servicos;
using SO.Monitor.Dominio.ValueObjects;
using Xunit;

namespace SO.Monitor.Testes.Dominio.Servicos;

public class PoliticaPorComportamentoDeBurstTests
{
    private static readonly PoliticaPorComportamentoDeBurst Politica = new();

    [Fact]
    public void Classificar_UsoDeCpuAcimaDoLimiar_RetornaLimitadoPorCpu()
    {
        var metricas = new MetricasDeExecucao(
            UsoDeCpu: new Percentual(80.0),
            TempoDeCpuAcumulado: TimeSpan.FromMinutes(5),
            TempoDeVida: TimeSpan.FromMinutes(10),
            ContagemDeHandles: Leitura<int>.NaoSuportada(),
            TotalDeThreads: 4,
            ThreadsBloqueadas: 0);

        var resultado = Politica.Classificar(metricas);

        Assert.Equal(PerfilDeExecucao.LimitadoPorCpu, resultado.Perfil);
        Assert.NotEmpty(resultado.Criterio);
    }

    [Fact]
    public void Classificar_CpuBaixaEMaioriaDasThreadsBloqueadas_RetornaLimitadoPorES()
    {
        var metricas = new MetricasDeExecucao(
            UsoDeCpu: new Percentual(2.0),
            TempoDeCpuAcumulado: TimeSpan.FromSeconds(5),
            TempoDeVida: TimeSpan.FromMinutes(10),
            ContagemDeHandles: Leitura<int>.NaoSuportada(),
            TotalDeThreads: 10,
            ThreadsBloqueadas: 7);

        var resultado = Politica.Classificar(metricas);

        Assert.Equal(PerfilDeExecucao.LimitadoPorES, resultado.Perfil);
    }

    [Fact]
    public void Classificar_CpuBaixaEPoucasThreadsBloqueadas_RetornaIndeterminado()
    {
        var metricas = new MetricasDeExecucao(
            UsoDeCpu: new Percentual(2.0),
            TempoDeCpuAcumulado: TimeSpan.FromSeconds(5),
            TempoDeVida: TimeSpan.FromMinutes(10),
            ContagemDeHandles: Leitura<int>.NaoSuportada(),
            TotalDeThreads: 10,
            ThreadsBloqueadas: 1);

        var resultado = Politica.Classificar(metricas);

        Assert.Equal(PerfilDeExecucao.Indeterminado, resultado.Perfil);
    }

    [Fact]
    public void Classificar_SemThreads_NaoLancaExcecaoENaoClassificaComoLimitadoPorES()
    {
        var metricas = new MetricasDeExecucao(
            UsoDeCpu: new Percentual(0.0),
            TempoDeCpuAcumulado: TimeSpan.Zero,
            TempoDeVida: TimeSpan.Zero,
            ContagemDeHandles: Leitura<int>.NaoSuportada(),
            TotalDeThreads: 0,
            ThreadsBloqueadas: 0);

        var resultado = Politica.Classificar(metricas);

        Assert.Equal(PerfilDeExecucao.Indeterminado, resultado.Perfil);
    }
}
