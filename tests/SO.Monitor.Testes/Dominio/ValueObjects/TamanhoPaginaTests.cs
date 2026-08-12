using SO.Monitor.Dominio.ValueObjects;
using Xunit;

namespace SO.Monitor.Testes.Dominio.ValueObjects;

public class TamanhoPaginaTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-4096)]
    [InlineData(3000)] // não é potência de 2
    [InlineData(4095)]
    public void Construtor_ValorInvalido_LancaArgumentOutOfRangeException(int emBytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TamanhoPagina(emBytes));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4096)]
    [InlineData(16384)]
    [InlineData(65536)]
    public void Construtor_PotenciaDeDoisPositiva_CriaComSucesso(int emBytes)
    {
        var pagina = new TamanhoPagina(emBytes);

        Assert.Equal(emBytes, pagina.EmBytes);
    }

    [Fact]
    public void Kib4_TemQuatroMilNoventaESeisBytes()
    {
        Assert.Equal(4096, TamanhoPagina.Kib4.EmBytes);
    }

    [Fact]
    public void Kib16_TemDezesseisMilTrezentosEOitentaEQuatroBytes()
    {
        Assert.Equal(16384, TamanhoPagina.Kib16.EmBytes);
    }
}
