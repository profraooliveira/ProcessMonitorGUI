using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using Xunit;

namespace SO.Monitor.Testes.Dominio;

public class LeituraTests
{
    [Fact]
    public void Ok_CriaLeituraDisponivelComOValor()
    {
        var leitura = Leitura<int>.Ok(42);

        Assert.True(leitura.TemValor);
        Assert.Equal(Disponibilidade.Disponivel, leitura.Estado);
        Assert.Equal(42, leitura.Valor);
    }

    [Fact]
    public void Negada_CriaLeituraComEstadoAcessoNegado()
    {
        var leitura = Leitura<int>.Negada();

        Assert.False(leitura.TemValor);
        Assert.Equal(Disponibilidade.AcessoNegado, leitura.Estado);
    }

    [Fact]
    public void NaoSuportada_CriaLeituraComEstadoNaoSuportadoNaPlataforma()
    {
        var leitura = Leitura<int>.NaoSuportada();

        Assert.False(leitura.TemValor);
        Assert.Equal(Disponibilidade.NaoSuportadoNaPlataforma, leitura.Estado);
    }

    [Fact]
    public void ProcessoEncerrado_CriaLeituraComEstadoProcessoEncerrado()
    {
        var leitura = Leitura<int>.ProcessoEncerrado();

        Assert.False(leitura.TemValor);
        Assert.Equal(Disponibilidade.ProcessoEncerrado, leitura.Estado);
    }

    [Fact]
    public void ValorOu_LeituraDisponivel_RetornaValorLido()
    {
        var leitura = Leitura<int>.Ok(10);

        Assert.Equal(10, leitura.ValorOu(-1));
    }

    [Fact]
    public void ValorOu_LeituraIndisponivel_RetornaValorPadrao()
    {
        var leitura = Leitura<int>.Negada();

        Assert.Equal(-1, leitura.ValorOu(-1));
    }

    [Fact]
    public void ValorOu_TipoReferencia_LeituraIndisponivel_RetornaValorPadrao()
    {
        var leitura = Leitura<string>.NaoSuportada();

        Assert.Equal("n/d", leitura.ValorOu("n/d"));
    }

    [Fact]
    public void Default_TemValorEhFalse()
    {
        // Regressão: antes de Disponibilidade.Indefinida existir como valor 0 do enum,
        // Disponivel ocupava o valor 0 — o que fazia default(Leitura<T>) mentir TemValor == true
        // mesmo sem nenhuma leitura ter sido de fato realizada.
        var leitura = default(Leitura<int>);

        Assert.False(leitura.TemValor);
        Assert.Equal(Disponibilidade.Indefinida, leitura.Estado);
    }

    [Fact]
    public void NaoSeAplica_CriaLeituraComEstadoNaoSeAplica()
    {
        var leitura = Leitura<int>.NaoSeAplica();

        Assert.False(leitura.TemValor);
        Assert.Equal(Disponibilidade.NaoSeAplica, leitura.Estado);
    }
}
