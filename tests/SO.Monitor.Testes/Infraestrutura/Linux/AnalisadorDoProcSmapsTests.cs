using SO.Monitor.Dominio.Enums;
using SO.Monitor.Infraestrutura.Linux;
using Xunit;

namespace SO.Monitor.Testes.Infraestrutura.Linux;

/// <summary>
/// Cobre a combinação de cabeçalho de região (mesmo formato de <c>maps</c>) com os blocos de
/// estatísticas de <c>/proc/&lt;pid&gt;/smaps</c>, garantindo que <c>Rss</c> e <c>Swap</c> caem
/// exatamente na região a que pertencem (não vazam para a região anterior ou seguinte) e que
/// <c>SwapPss</c> nunca é confundido com <c>Swap</c>.
/// </summary>
public class AnalisadorDoProcSmapsTests
{
    private const string FixtureSmaps = """
        7f2c8a000000-7f2c8a021000 r-xp 00000000 08:01 1234 /usr/lib/libc.so.6
        Size:                132 kB
        Rss:                  96 kB
        Pss:                  10 kB
        Shared_Clean:         96 kB
        Referenced:           96 kB
        Anonymous:             0 kB
        Swap:                  0 kB
        SwapPss:               4 kB
        7f2c8a021000-7f2c8a030000 rw-p 00021000 08:01 1234 /usr/lib/libc.so.6
        Size:                 60 kB
        Rss:                  60 kB
        Anonymous:            60 kB
        Swap:                 12 kB
        SwapPss:               8 kB
        """;

    [Fact]
    public void Analisar_DuasRegioes_DevolveAmbasComResidencia()
    {
        var regioes = AnalisadorDoProcSmaps.Analisar(FixtureSmaps);

        Assert.Equal(2, regioes.Count);
    }

    [Fact]
    public void Analisar_PrimeiraRegiao_RssESwapEmBytesCorretos()
    {
        var regioes = AnalisadorDoProcSmaps.Analisar(FixtureSmaps);

        var regiao = regioes[0];

        Assert.True(regiao.Residente.TemValor);
        Assert.Equal(96L * 1024, regiao.Residente.Valor.Valor);

        Assert.True(regiao.EmSwap.TemValor);
        Assert.Equal(0L, regiao.EmSwap.Valor.Valor);

        // Biblioteca compartilhada executável -> Codigo é para o binário principal; para uma
        // .so o tipo esperado é BibliotecaCompartilhada.
        Assert.Equal(TipoDeRegiao.BibliotecaCompartilhada, regiao.TipoDeRegiao);
    }

    [Fact]
    public void Analisar_SegundaRegiao_RssESwapNaoSeMisturamComAPrimeira()
    {
        var regioes = AnalisadorDoProcSmaps.Analisar(FixtureSmaps);

        var regiao = regioes[1];

        Assert.True(regiao.Residente.TemValor);
        Assert.Equal(60L * 1024, regiao.Residente.Valor.Valor);

        Assert.True(regiao.EmSwap.TemValor);
        Assert.Equal(12L * 1024, regiao.EmSwap.Valor.Valor);

        // Mesmo arquivo (libc.so.6), mas sem bit de execução: segmento de dados, mapeado como
        // arquivo comum, não como biblioteca em execução.
        Assert.Equal(TipoDeRegiao.ArquivoMapeado, regiao.TipoDeRegiao);
    }

    [Fact]
    public void Analisar_NaoConfundeSwapPssComSwap()
    {
        var regioes = AnalisadorDoProcSmaps.Analisar(FixtureSmaps);

        // Se "SwapPss:" fosse lido como "Swap:", a primeira região teria EmSwap = 4 kB em vez de 0.
        Assert.Equal(0L, regioes[0].EmSwap.Valor.Valor);
    }

    [Fact]
    public void Analisar_ConteudoSemCabecalhoValido_DevolveListaVaziaSemLancar()
    {
        const string conteudoSemCabecalho = """
            Rss:                  4 kB
            Swap:                 0 kB
            """;

        var regioes = AnalisadorDoProcSmaps.Analisar(conteudoSemCabecalho);

        Assert.Empty(regioes);
    }

    [Fact]
    public void Analisar_ConteudoVazio_DevolveListaVaziaSemLancar()
    {
        var regioes = AnalisadorDoProcSmaps.Analisar(string.Empty);

        Assert.Empty(regioes);
    }
}
