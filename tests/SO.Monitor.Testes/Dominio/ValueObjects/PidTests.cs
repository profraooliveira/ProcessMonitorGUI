using SO.Monitor.Dominio.ValueObjects;
using Xunit;

namespace SO.Monitor.Testes.Dominio.ValueObjects;

public class PidTests
{
    [Fact]
    public void Construtor_ValorNegativo_LancaArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pid(-1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1234)]
    public void Construtor_ValorNaoNegativo_ExpoeOMesmoValor(int valor)
    {
        var pid = new Pid(valor);

        Assert.Equal(valor, pid.Valor);
    }

    [Fact]
    public void ToString_RetornaValorNumericoComoTexto()
    {
        var pid = new Pid(42);

        Assert.Equal("42", pid.ToString());
    }
}
