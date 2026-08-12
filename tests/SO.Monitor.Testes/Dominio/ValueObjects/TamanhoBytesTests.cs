using SO.Monitor.Dominio.ValueObjects;
using Xunit;

namespace SO.Monitor.Testes.Dominio.ValueObjects;

public class TamanhoBytesTests
{
    [Fact]
    public void Construtor_ValorNegativo_LancaArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TamanhoBytes(-1));
    }

    [Fact]
    public void Zero_RetornaTamanhoComValorZero()
    {
        Assert.Equal(0, TamanhoBytes.Zero.Valor);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(4096, 1)]
    [InlineData(4097, 2)]
    [InlineData(8192, 2)]
    [InlineData(8193, 3)]
    public void EmPaginas_Kib4_ArredondaParaCima(long bytes, long paginasEsperadas)
    {
        var tamanho = new TamanhoBytes(bytes);

        Assert.Equal(paginasEsperadas, tamanho.EmPaginas(TamanhoPagina.Kib4));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(16384, 1)]
    [InlineData(16385, 2)]
    public void EmPaginas_Kib16_ArredondaParaCima(long bytes, long paginasEsperadas)
    {
        var tamanho = new TamanhoBytes(bytes);

        Assert.Equal(paginasEsperadas, tamanho.EmPaginas(TamanhoPagina.Kib16));
    }

    [Fact]
    public void OperadorSoma_DoisTamanhos_RetornaSoma()
    {
        var resultado = new TamanhoBytes(100) + new TamanhoBytes(50);

        Assert.Equal(150, resultado.Valor);
    }

    [Fact]
    public void OperadorSubtracao_DoisTamanhos_RetornaDiferenca()
    {
        var resultado = new TamanhoBytes(100) - new TamanhoBytes(30);

        Assert.Equal(70, resultado.Valor);
    }

    [Fact]
    public void OperadorSubtracao_ResultadoNegativo_LancaArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TamanhoBytes(10) - new TamanhoBytes(20));
    }
}
