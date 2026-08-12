using SO.Monitor.Dominio.ValueObjects;
using Xunit;

namespace SO.Monitor.Testes.Dominio.ValueObjects;

public class TidTests
{
    [Fact]
    public void Construtor_ValorNegativo_LancaArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tid(-1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999999)]
    public void Construtor_ValorNaoNegativo_ExpoeOMesmoValor(long valor)
    {
        var tid = new Tid(valor);

        Assert.Equal(valor, tid.Valor);
    }

    [Fact]
    public void ToString_RetornaValorNumericoComoTexto()
    {
        var tid = new Tid(7);

        Assert.Equal("7", tid.ToString());
    }
}
