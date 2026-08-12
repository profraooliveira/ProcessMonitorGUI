using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.Servicos;
using SO.Monitor.Dominio.ValueObjects;
using Xunit;

namespace SO.Monitor.Testes.Dominio.Servicos;

public class PoliticaPorContagemDeHandlesTests
{
    private static readonly PoliticaPorContagemDeHandles Politica = new();

    [Fact]
    public void Classificar_HandlesIndisponiveis_RetornaIndeterminado()
    {
        var metricas = MetricasComHandles(Leitura<int>.NaoSuportada());

        var resultado = Politica.Classificar(metricas);

        Assert.Equal(PerfilDeExecucao.Indeterminado, resultado.Perfil);
        Assert.Contains("plataforma", resultado.Criterio, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Classificar_HandlesAcimaDoLimiar_RetornaLimitadoPorES()
    {
        var metricas = MetricasComHandles(Leitura<int>.Ok(1000));

        var resultado = Politica.Classificar(metricas);

        Assert.Equal(PerfilDeExecucao.LimitadoPorES, resultado.Perfil);
    }

    [Fact]
    public void Classificar_HandlesDentroDoNormal_RetornaIndeterminado()
    {
        var metricas = MetricasComHandles(Leitura<int>.Ok(10));

        var resultado = Politica.Classificar(metricas);

        Assert.Equal(PerfilDeExecucao.Indeterminado, resultado.Perfil);
    }

    private static MetricasDeExecucao MetricasComHandles(Leitura<int> contagemDeHandles) => new(
        UsoDeCpu: Percentual.Zero,
        TempoDeCpuAcumulado: TimeSpan.Zero,
        TempoDeVida: TimeSpan.Zero,
        ContagemDeHandles: contagemDeHandles,
        TotalDeThreads: 1,
        ThreadsBloqueadas: 0);
}
