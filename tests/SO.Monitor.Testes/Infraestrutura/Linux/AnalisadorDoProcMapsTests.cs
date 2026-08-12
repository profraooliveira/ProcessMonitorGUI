using SO.Monitor.Dominio.Enums;
using SO.Monitor.Infraestrutura.Linux;
using Xunit;

namespace SO.Monitor.Testes.Infraestrutura.Linux;

/// <summary>
/// Cobre a classificação de regiões a partir de um <c>/proc/&lt;pid&gt;/maps</c> fabricado — sem
/// nenhuma dependência de um processo real, o que torna o teste determinístico em qualquer SO
/// (inclusive o macOS onde este suite roda de fato).
/// </summary>
public class AnalisadorDoProcMapsTests
{
    // Pathname com espaço interno ("/usr/bin/meu programa") verifica que o parser não corta a
    // linha no primeiro espaço encontrado após os cinco campos fixos.
    private const string FixtureMaps = """
        00400000-00401000 r-xp 00000000 08:01 131099 /usr/bin/meu programa
        00401000-00402000 r--p 00001000 08:01 131099 /usr/bin/meu programa
        7f2c8a000000-7f2c8a021000 r-xp 00000000 08:01 1234 /usr/lib/libc.so.6
        7f2c8b000000-7f2c8b021000 rw-p 00000000 00:00 0
        7f2c8c000000-7f2c8c001000 rw-p 00000000 00:00 0 [heap]
        7ffee0000000-7ffee0021000 rw-p 00000000 00:00 0 [stack]
        7f2c8d000000-7f2c8d010000 r--p 00000000 08:01 5678 /usr/share/fonte de dados.dat
        7f2c8e000000-7f2c8e001000 ---p 00000000 00:00 0
        7ffff7fcd000-7ffff7fcf000 r-xp 00000000 00:00 0 [vdso]
        linha totalmente invalida sem formato
        """;

    [Fact]
    public void Analisar_IgnoraLinhaMalformada_SemLancar()
    {
        var regioes = AnalisadorDoProcMaps.Analisar(FixtureMaps);

        // 10 linhas na fixture, 1 malformada -> 9 regiões válidas.
        Assert.Equal(9, regioes.Count);
    }

    [Fact]
    public void Analisar_SegmentoDeCodigoDoExecutavel_ClassificaComoCodigo()
    {
        var regioes = AnalisadorDoProcMaps.Analisar(FixtureMaps);

        var regiao = regioes.Single(r => r.Rotulo == "/usr/bin/meu programa" && r.PermissoesTexto == "r-x");

        Assert.Equal(TipoDeRegiao.Codigo, regiao.TipoDeRegiao);
    }

    [Fact]
    public void Analisar_SegmentoDeDadosDoExecutavelSemExecucao_ClassificaComoArquivoMapeado()
    {
        var regioes = AnalisadorDoProcMaps.Analisar(FixtureMaps);

        var regiao = regioes.Single(r => r.Rotulo == "/usr/bin/meu programa" && r.PermissoesTexto == "r--");

        Assert.Equal(TipoDeRegiao.ArquivoMapeado, regiao.TipoDeRegiao);
    }

    [Fact]
    public void Analisar_BibliotecaCompartilhadaExecutavel_ClassificaComoBibliotecaCompartilhada()
    {
        var regioes = AnalisadorDoProcMaps.Analisar(FixtureMaps);

        var regiao = regioes.Single(r => r.Rotulo == "/usr/lib/libc.so.6");

        Assert.Equal(TipoDeRegiao.BibliotecaCompartilhada, regiao.TipoDeRegiao);
        Assert.Equal("r-x", regiao.PermissoesTexto);
    }

    [Fact]
    public void Analisar_MapeamentoAnonimoLerEscrever_ClassificaComoHeap()
    {
        var regioes = AnalisadorDoProcMaps.Analisar(FixtureMaps);

        var regiao = regioes.Single(r => r.FaixaDeEnderecos.Inicio.Valor == 0x7f2c8b000000UL);

        Assert.Equal(TipoDeRegiao.Heap, regiao.TipoDeRegiao);
        Assert.Null(regiao.Rotulo);
    }

    [Fact]
    public void Analisar_TagHeap_ClassificaComoHeap()
    {
        var regioes = AnalisadorDoProcMaps.Analisar(FixtureMaps);

        var regiao = regioes.Single(r => r.Rotulo == "[heap]");

        Assert.Equal(TipoDeRegiao.Heap, regiao.TipoDeRegiao);
    }

    [Fact]
    public void Analisar_TagStack_ClassificaComoPilha()
    {
        var regioes = AnalisadorDoProcMaps.Analisar(FixtureMaps);

        var regiao = regioes.Single(r => r.Rotulo == "[stack]");

        Assert.Equal(TipoDeRegiao.Pilha, regiao.TipoDeRegiao);
    }

    [Fact]
    public void Analisar_ArquivoMapeadoComEspacoNoPathname_PreservaPathnameCompleto()
    {
        var regioes = AnalisadorDoProcMaps.Analisar(FixtureMaps);

        var regiao = regioes.Single(r => r.Rotulo == "/usr/share/fonte de dados.dat");

        Assert.Equal(TipoDeRegiao.ArquivoMapeado, regiao.TipoDeRegiao);
    }

    [Fact]
    public void Analisar_SemPermissaoAlguma_ClassificaComoReservada()
    {
        var regioes = AnalisadorDoProcMaps.Analisar(FixtureMaps);

        var regiao = regioes.Single(r => r.PermissoesTexto == "---");

        Assert.Equal(TipoDeRegiao.Reservada, regiao.TipoDeRegiao);
        Assert.Null(regiao.Rotulo);
    }

    [Fact]
    public void Analisar_TagVdso_ClassificaComoOutra()
    {
        var regioes = AnalisadorDoProcMaps.Analisar(FixtureMaps);

        var regiao = regioes.Single(r => r.Rotulo == "[vdso]");

        Assert.Equal(TipoDeRegiao.Outra, regiao.TipoDeRegiao);
    }

    [Fact]
    public void Analisar_TodasAsRegioes_NuncaTemInformacaoDeResidencia()
    {
        // /proc/<pid>/maps não traz Rss/Swap — só smaps traz. A residência é responsabilidade de
        // AnalisadorDoProcSmaps, não deste analisador.
        var regioes = AnalisadorDoProcMaps.Analisar(FixtureMaps);

        Assert.All(regioes, regiao =>
        {
            Assert.False(regiao.Residente.TemValor);
            Assert.False(regiao.EmSwap.TemValor);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("endereco-invalido r-xp 00000000 08:01 1234 /bin/x")]
    [InlineData("00400000 r-xp 00000000 08:01 1234 /bin/x")] // faltando o hífen na faixa
    [InlineData("00400000-00401000 rx 00000000 08:01 1234 /bin/x")] // permissões com menos de 4 caracteres
    [InlineData("00400000-00401000 r-xp 00000000 08:01")] // faltando o campo inode
    public void TentarAnalisarLinha_LinhasMalformadas_DevolveFalseSemLancar(string linha)
    {
        var resultado = AnalisadorDoProcMaps.TentarAnalisarLinha(linha, out var regiao);

        Assert.False(resultado);
        Assert.Null(regiao);
    }
}
