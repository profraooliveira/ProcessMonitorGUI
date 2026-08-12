using SO.Monitor.Dominio.Servicos;
using Xunit;

namespace SO.Monitor.Testes.Dominio.Servicos;

public class CalculadoraDeUsoDeCpuTests
{
    [Fact]
    public void Calcular_UmSegundoDeCpuEmDoisSegundosComQuatroNucleos_Retorna12Virgula5PorCento()
    {
        var resultado = CalculadoraDeUsoDeCpu.Calcular(
            cpuAnterior: TimeSpan.Zero,
            cpuAtual: TimeSpan.FromSeconds(1),
            intervaloDecorrido: TimeSpan.FromSeconds(2),
            nucleosLogicos: 4);

        Assert.Equal(12.5, resultado.Valor, precision: 3);
    }

    [Fact]
    public void Calcular_IntervaloZero_RetornaZero()
    {
        var resultado = CalculadoraDeUsoDeCpu.Calcular(
            cpuAnterior: TimeSpan.Zero,
            cpuAtual: TimeSpan.FromSeconds(1),
            intervaloDecorrido: TimeSpan.Zero,
            nucleosLogicos: 4);

        Assert.Equal(0.0, resultado.Valor);
    }

    [Fact]
    public void Calcular_IntervaloNegativo_RetornaZero()
    {
        var resultado = CalculadoraDeUsoDeCpu.Calcular(
            cpuAnterior: TimeSpan.Zero,
            cpuAtual: TimeSpan.FromSeconds(1),
            intervaloDecorrido: TimeSpan.FromSeconds(-1),
            nucleosLogicos: 4);

        Assert.Equal(0.0, resultado.Valor);
    }

    [Fact]
    public void Calcular_DeltaDeCpuNegativo_ProcessoReiniciado_RetornaZero()
    {
        var resultado = CalculadoraDeUsoDeCpu.Calcular(
            cpuAnterior: TimeSpan.FromSeconds(100),
            cpuAtual: TimeSpan.FromSeconds(1), // contador de CPU voltou a zero: processo foi reiniciado
            intervaloDecorrido: TimeSpan.FromSeconds(2),
            nucleosLogicos: 4);

        Assert.Equal(0.0, resultado.Valor);
    }

    [Fact]
    public void Calcular_ProcessoMultithreadSaturandoVariosNucleos_UltrapassaCemPorCentoDeUmNucleo()
    {
        // 2s de CPU consumidos em 1s de relógio com 4 núcleos disponíveis: um processo com
        // múltiplas threads pode saturar mais de um núcleo ao mesmo tempo.
        var resultado = CalculadoraDeUsoDeCpu.Calcular(
            cpuAnterior: TimeSpan.Zero,
            cpuAtual: TimeSpan.FromSeconds(2),
            intervaloDecorrido: TimeSpan.FromSeconds(1),
            nucleosLogicos: 4);

        Assert.Equal(50.0, resultado.Valor, precision: 3);
    }

    [Fact]
    public void Calcular_NucleosLogicosZero_RetornaZero()
    {
        var resultado = CalculadoraDeUsoDeCpu.Calcular(
            cpuAnterior: TimeSpan.Zero,
            cpuAtual: TimeSpan.FromSeconds(1),
            intervaloDecorrido: TimeSpan.FromSeconds(2),
            nucleosLogicos: 0);

        Assert.Equal(0.0, resultado.Valor);
    }
}
