using SO.Monitor.Dominio.ValueObjects;
using Xunit;

namespace SO.Monitor.Testes.Dominio.ValueObjects;

public class PercentualTests
{
    [Fact]
    public void Construtor_ValorNegativo_LancaArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Percentual(-0.1));
    }

    [Fact]
    public void Construtor_ValorInfinito_LancaArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Percentual(double.PositiveInfinity));
    }

    [Fact]
    public void Construtor_ValorNaN_LancaArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Percentual(double.NaN));
    }

    [Fact]
    public void Construtor_ValorAcimaDeCem_NaoLancaExcecao()
    {
        // Um processo multithread pode consumir mais de 100% somando o tempo de vários núcleos.
        var percentual = new Percentual(350.0);

        Assert.Equal(350.0, percentual.Valor);
    }
}
