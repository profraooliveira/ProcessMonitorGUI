using SO.Monitor.Dominio.Enums;
using SO.Monitor.Infraestrutura.Windows;
using Xunit;

namespace SO.Monitor.Testes.Infraestrutura.Windows;

/// <summary>
/// Cobre todas as combinações principais de (Type, State, Protect) de
/// <c>MEMORY_BASIC_INFORMATION</c> que <see cref="ClassificadorDeRegiaoWindows"/> precisa
/// distinguir. É lógica pura, sem P/Invoke, então roda igual em qualquer plataforma — inclusive
/// aqui no macOS, onde o provedor Windows de verdade nunca poderia ser exercitado.
/// </summary>
public class ClassificadorDeRegiaoWindowsTests
{
    [Fact]
    public void Classificar_ImagemExecutavel_ClassificaComoCodigo()
    {
        var (tipo, permissoes, rotulo) = ClassificadorDeRegiaoWindows.Classificar(
            ClassificadorDeRegiaoWindows.MEM_IMAGE,
            ClassificadorDeRegiaoWindows.MEM_COMMIT,
            ClassificadorDeRegiaoWindows.PAGE_EXECUTE_READ);

        Assert.Equal(TipoDeRegiao.Codigo, tipo);
        Assert.Equal("r-x", permissoes);
        Assert.Null(rotulo);
    }

    [Fact]
    public void Classificar_ImagemNaoExecutavel_ClassificaComoBibliotecaCompartilhada()
    {
        var (tipo, permissoes, rotulo) = ClassificadorDeRegiaoWindows.Classificar(
            ClassificadorDeRegiaoWindows.MEM_IMAGE,
            ClassificadorDeRegiaoWindows.MEM_COMMIT,
            ClassificadorDeRegiaoWindows.PAGE_READONLY);

        Assert.Equal(TipoDeRegiao.BibliotecaCompartilhada, tipo);
        Assert.Equal("r--", permissoes);
        Assert.Null(rotulo);
    }

    [Fact]
    public void Classificar_Mapeado_ClassificaComoArquivoMapeado()
    {
        var (tipo, permissoes, _) = ClassificadorDeRegiaoWindows.Classificar(
            ClassificadorDeRegiaoWindows.MEM_MAPPED,
            ClassificadorDeRegiaoWindows.MEM_COMMIT,
            ClassificadorDeRegiaoWindows.PAGE_READWRITE);

        Assert.Equal(TipoDeRegiao.ArquivoMapeado, tipo);
        Assert.Equal("rw-", permissoes);
    }

    [Fact]
    public void Classificar_PrivadoComprometido_ClassificaComoHeap()
    {
        var (tipo, permissoes, _) = ClassificadorDeRegiaoWindows.Classificar(
            ClassificadorDeRegiaoWindows.MEM_PRIVATE,
            ClassificadorDeRegiaoWindows.MEM_COMMIT,
            ClassificadorDeRegiaoWindows.PAGE_READWRITE);

        Assert.Equal(TipoDeRegiao.Heap, tipo);
        Assert.Equal("rw-", permissoes);
    }

    [Fact]
    public void Classificar_Reservada_ClassificaComoReservada()
    {
        var (tipo, permissoes, _) = ClassificadorDeRegiaoWindows.Classificar(
            ClassificadorDeRegiaoWindows.MEM_PRIVATE,
            ClassificadorDeRegiaoWindows.MEM_RESERVE,
            ClassificadorDeRegiaoWindows.PAGE_NOACCESS);

        Assert.Equal(TipoDeRegiao.Reservada, tipo);
        Assert.Equal("---", permissoes);
    }

    [Fact]
    public void Classificar_Livre_ClassificaComoOutra()
    {
        // MEM_FREE: nem sequer há uma VAD ali — o Type devolvido pelo Windows nesse caso é 0 e
        // não deve ser interpretado como MEM_IMAGE/MEM_MAPPED/MEM_PRIVATE.
        var (tipo, _, _) = ClassificadorDeRegiaoWindows.Classificar(
            tipo: 0,
            estado: ClassificadorDeRegiaoWindows.MEM_FREE,
            protect: 0);

        Assert.Equal(TipoDeRegiao.Outra, tipo);
    }

    [Fact]
    public void Classificar_PaginaDeGuarda_AnotaRotuloGuardMasPermissoesRefletemAProtecaoBase()
    {
        var protecaoComGuarda = ClassificadorDeRegiaoWindows.PAGE_READWRITE | ClassificadorDeRegiaoWindows.PAGE_GUARD;

        var (tipo, permissoes, rotulo) = ClassificadorDeRegiaoWindows.Classificar(
            ClassificadorDeRegiaoWindows.MEM_PRIVATE,
            ClassificadorDeRegiaoWindows.MEM_COMMIT,
            protecaoComGuarda);

        Assert.Equal(TipoDeRegiao.Heap, tipo);
        Assert.Equal("rw-", permissoes); // guard não é uma permissão rwx; só o rótulo denuncia
        Assert.Equal("(guard)", rotulo);
    }

    [Theory]
    [InlineData(0x01u /* PAGE_NOACCESS */, "---")]
    [InlineData(0x02u /* PAGE_READONLY */, "r--")]
    [InlineData(0x04u /* PAGE_READWRITE */, "rw-")]
    [InlineData(0x08u /* PAGE_WRITECOPY */, "rw-")]
    [InlineData(0x10u /* PAGE_EXECUTE */, "--x")]
    [InlineData(0x20u /* PAGE_EXECUTE_READ */, "r-x")]
    [InlineData(0x40u /* PAGE_EXECUTE_READWRITE */, "rwx")]
    [InlineData(0x80u /* PAGE_EXECUTE_WRITECOPY */, "rwx")]
    public void Classificar_TodasAsProtecoesBase_FormatamPermissoesNoEstiloUnix(uint protect, string permissoesEsperadas)
    {
        var (_, permissoes, _) = ClassificadorDeRegiaoWindows.Classificar(
            ClassificadorDeRegiaoWindows.MEM_PRIVATE,
            ClassificadorDeRegiaoWindows.MEM_COMMIT,
            protect);

        Assert.Equal(permissoesEsperadas, permissoes);
    }

    [Fact]
    public void Classificar_PrivadoReservadoAindaNaoComprometido_NaoEHeap()
    {
        // MEM_PRIVATE + MEM_RESERVE (sem COMMIT) não deve cair na regra "PRIVATE+COMMIT -> Heap";
        // deve cair na regra de Reservada.
        var (tipo, _, _) = ClassificadorDeRegiaoWindows.Classificar(
            ClassificadorDeRegiaoWindows.MEM_PRIVATE,
            ClassificadorDeRegiaoWindows.MEM_RESERVE,
            ClassificadorDeRegiaoWindows.PAGE_READWRITE);

        Assert.Equal(TipoDeRegiao.Reservada, tipo);
    }
}
