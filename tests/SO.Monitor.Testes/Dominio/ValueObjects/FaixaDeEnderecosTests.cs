using SO.Monitor.Dominio.ValueObjects;
using Xunit;

namespace SO.Monitor.Testes.Dominio.ValueObjects;

public class FaixaDeEnderecosTests
{
    [Fact]
    public void Construtor_FimAnteriorAoInicio_LancaArgumentOutOfRangeException()
    {
        var inicio = new EnderecoVirtual(0x2000);
        var fim = new EnderecoVirtual(0x1000);

        Assert.Throws<ArgumentOutOfRangeException>(() => new FaixaDeEnderecos(inicio, fim));
    }

    [Fact]
    public void Construtor_InicioIgualAFim_CriaFaixaComExtensaoZero()
    {
        var endereco = new EnderecoVirtual(0x1000);

        var faixa = new FaixaDeEnderecos(endereco, endereco);

        Assert.Equal(0, faixa.Extensao.Valor);
    }

    [Fact]
    public void Extensao_CalculaDiferencaEntreFimEInicio()
    {
        var faixa = new FaixaDeEnderecos(new EnderecoVirtual(0x1000), new EnderecoVirtual(0x3000));

        Assert.Equal(0x2000L, faixa.Extensao.Valor);
    }
}
