using SO.Monitor.Dominio.ValueObjects;
using Xunit;

namespace SO.Monitor.Testes.Dominio.ValueObjects;

public class EnderecoVirtualTests
{
    [Fact]
    public void ToString_FormataComoHexadecimalDeDozeDigitos()
    {
        var endereco = new EnderecoVirtual(0x1000);

        Assert.Equal("0x000000001000", endereco.ToString());
    }

    [Fact]
    public void OperadorMenorQue_EnderecoMenor_RetornaVerdadeiro()
    {
        var menor = new EnderecoVirtual(0x1000);
        var maior = new EnderecoVirtual(0x2000);

        Assert.True(menor < maior);
        Assert.False(maior < menor);
    }

    [Fact]
    public void OperadorMaiorOuIgualEMenorOuIgual_EnderecosIguais_RetornamVerdadeiro()
    {
        var a = new EnderecoVirtual(0x1000);
        var b = new EnderecoVirtual(0x1000);

        Assert.True(a >= b);
        Assert.True(a <= b);
    }

    [Fact]
    public void CompareTo_EnderecoMaior_RetornaPositivo()
    {
        var a = new EnderecoVirtual(0x2000);
        var b = new EnderecoVirtual(0x1000);

        Assert.True(a.CompareTo(b) > 0);
    }
}
