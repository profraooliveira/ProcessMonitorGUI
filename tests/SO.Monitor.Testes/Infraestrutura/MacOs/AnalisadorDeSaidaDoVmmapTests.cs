using SO.Monitor.Dominio.Enums;
using SO.Monitor.Infraestrutura.MacOs;
using Xunit;

namespace SO.Monitor.Testes.Infraestrutura.MacOs;

/// <summary>
/// Cobre o analisador puro de <c>vmmap -interleaved</c> com amostras reais coletadas em um
/// macOS 26.5 (Apple Silicon), embutidas aqui como literais de string bruta — sem depender de
/// processo externo nenhum, o que torna estes testes determinísticos em qualquer plataforma.
/// </summary>
public class AnalisadorDeSaidaDoVmmapTests
{
    /// <summary>Amostra completa de <c>vmmap -interleaved 640</c> (Finder), incluindo cabeçalho e todas as regiões.</summary>
    private const string AmostraCompleta = """
        Process:         Finder [640]
        Path:            /System/Library/CoreServices/Finder.app/Contents/MacOS/Finder
        Load Address:    0x1023f0000
        Identifier:      com.apple.finder
        Version:         26.4 (1828.5.2)
        Build Info:      Finder_FE-1828005002000000~18
        Code Type:       ARM64E
        Platform:        macOS
        Parent Process:  launchd [1]
        Target Type:     live task

        Date/Time:       2026-08-12 11:17:27.100 -0300
        Launch Time:     2026-08-08 08:31:23.320 -0300
        OS Version:      macOS 26.5.2 (25F84)
        Report Version:  7
        Analysis Tool:   /usr/bin/vmmap

        Physical footprint:         135.3M
        Physical footprint (peak):  236.3M
        Idle exit:                  untracked
        ----

        Virtual Memory Map of process 640 (Finder)
        Output report format:  2.4  -- 64-bit process
        VM page size:  16384 bytes
        Collected with PhysFootprint mode enabled

        ==== regions for process 640  (non-writable and writable regions are interleaved)
        REGION TYPE                    START - END         [ VSIZE  RSDNT  DIRTY   SWAP] PRT/MAX SHRMOD PURGE    REGION DETAIL
        __TEXT                      1023f0000-102e9c000    [ 10.7M  1712K     0K     0K] r-x/r-x SM=COW          /System/Library/CoreServices/Finder.app/Contents/MacOS/Finder
        __DATA_CONST                102e9c000-102f40000    [  656K   368K    16K     0K] r--/rw- SM=COW          /System/Library/CoreServices/Finder.app/Contents/MacOS/Finder
        __DATA                      102f40000-102fc0000    [  512K   144K   144K    80K] rw-/rw- SM=COW          /System/Library/CoreServices/Finder.app/Contents/MacOS/Finder
        __DATA                      102fc0000-102fd0000    [   64K    48K    48K    16K] rw-/rw- SM=PRV          /System/Library/CoreServices/Finder.app/Contents/MacOS/Finder
        __LINKEDIT                  102fd0000-103064000    [  592K     0K     0K     0K] r--/r-- SM=COW          /System/Library/CoreServices/Finder.app/Contents/MacOS/Finder
        VM_ALLOCATE                 103064000-103068000    [   16K     0K     0K     0K] ---/rwx SM=NUL
        VM_ALLOCATE                 103068000-1030e8000    [  512K     0K     0K    32K] r--/rwx SM=PRV
        __TEXT                      1030e8000-1031e4000    [ 1008K    16K     0K     0K] r-x/rwx SM=COW          /System/Library/PrivateFrameworks/TimelineUI.framework/Versions/A/TimelineUI
        __DATA_CONST                1031e4000-1031e8000    [   16K     0K     0K    16K] rw-/rw- SM=COW          /System/Library/PrivateFrameworks/TimelineUI.framework/Versions/A/TimelineUI
        __AUTH_CONST                1031e8000-1031f4000    [   48K     0K     0K     0K] rw-/rw- SM=COW          /System/Library/PrivateFrameworks/TimelineUI.framework/Versions/A/TimelineUI
        __AUTH                      1031f4000-1031f8000    [   16K     0K     0K    16K] rw-/rw- SM=COW          /System/Library/PrivateFrameworks/TimelineUI.framework/Versions/A/TimelineUI
        __DATA                      1031f8000-1031fc000    [   16K     0K     0K    16K] rw-/rw- SM=COW          /System/Library/PrivateFrameworks/TimelineUI.framework/Versions/A/TimelineUI
        __DATA                      1031fc000-103204000    [   32K     0K     0K     0K] rw-/rwx SM=NUL          /System/Library/PrivateFrameworks/TimelineUI.framework/Versions/A/TimelineUI
        __LINKEDIT                  103204000-103284000    [  512K     0K     0K     0K] r--/rw- SM=COW          /System/Library/PrivateFrameworks/TimelineUI.framework/Versions/A/TimelineUI
        Kernel Alloc Once           103284000-10328c000    [   32K    16K    16K     0K] rw-/rwx SM=PRV
        shared memory               10328c000-103294000    [   32K    32K    32K     0K] r--/r-- SM=SHM
        shared memory               103294000-103298000    [   16K    16K    16K     0K] r--/r-- SM=SHM
        Activity Tracing            103298000-1032d8000    [  256K    48K    32K    16K] rw-/rwx SM=ALI PURGE=N
        shared memory               1032d8000-1032dc000    [   16K    16K    16K     0K] r--/r-- SM=SHM
        mapped file                 1032dc000-1032e0000    [   16K     0K     0K     0K] r--/rw- SM=COW          /System/Library/CoreServices/SystemVersion.bundle/pt.lproj/SystemVersion.strings
        shared memory               1032e0000-1032e4000    [   16K    16K    16K     0K] rw-/rw- SM=SHM
        shared memory               1032e4000-1032e8000    [   16K    16K    16K     0K] r--/r-- SM=SHM
        dyld private memory         1032e8000-10330c000    [  144K     0K     0K     0K] ---/--- SM=PRV
        MALLOC guard page           10336c000-103570000    [ 2064K     0K     0K     0K] ---/--- SM=SHM
        MALLOC metadata             103570000-1035ac000    [  240K   112K   112K    96K] rw-/rwx SM=PRV          DefaultMallocZone_0x103570000
        MALLOC metadata             1035ac000-1035b0000    [   16K    16K    16K     0K] r--/rwx SM=PRV
        MALLOC_NANO metadata        1035b0000-1035b8000    [   32K    32K    32K     0K] rw-/rwx SM=PRV          DefaultMallocZone_0x103570000
        MALLOC metadata             1035b8000-103638000    [  512K   288K   288K   224K] rw-/rwx SM=PRV          DefaultMallocZone_0x103570000
        mapped file                 103638000-10364c000    [   80K    80K     0K     0K] r--/r-- SM=COW          /Library/Preferences/Logging/.plist-cache.6hoO9RIn
        shared memory               10364c000-103650000    [   16K    16K    16K     0K] r--/r-- SM=SHM
        shared memory               103650000-103658000    [   32K    16K    16K     0K] r--/r-- SM=SHM
        mapped file                 103658000-103664000    [   48K     0K     0K     0K] r--/rw- SM=COW          /System/Library/CoreServices/Finder.app/Contents/Resources/InfoPlist.loctable
        shared memory               103664000-103668000    [   16K    16K    16K     0K] r--/r-- SM=SHM
        VM_ALLOCATE                 103668000-10366c000    [   16K     0K     0K     0K] r--/r-- SM=COW
        shared memory               10366c000-103670000    [   16K     0K     0K    16K] rw-/rw- SM=SHM
        MALLOC guard page           103670000-1037dc000    [ 1456K     0K     0K     0K] ---/--- SM=SHM
        MALLOC guard page           1037dc000-1037e0000    [   16K     0K     0K     0K] ---/rwx SM=SHM
        MALLOC_TINY                 1037e0000-103be0000    [ 4096K   208K   208K    64K] rw-/rwx SM=PRV          DefaultMallocZone_0x103570000
        MALLOC guard page           103be0000-103be4000    [   16K     0K     0K     0K] ---/rwx SM=SHM
        Memory Tag 22               103be4000-107be8000    [ 64.0M    16K    16K     0K] rw-/rw- SM=PRV
        MALLOC_NANO metadata        107be8000-107bf0000    [   32K     0K     0K    16K] rw-/rwx SM=PRV          DefaultMallocZone_0x103570000
        MALLOC metadata             107bf0000-107c14000    [  144K     0K     0K   144K] rw-/rwx SM=PRV          SIQueryMallocZone_0x107bf0000 zone structure
        mapped file                 107c14000-107c18000    [   16K     0K     0K     0K] r--/rw- SM=COW          /System/Library/Frameworks/AppKit.framework/Versions/C/Resources/InputManager.loctable
        mapped file                 107c18000-107c1c000    [   16K     0K     0K     0K] r--/rw- SM=COW          /System/Library/CoreServices/Finder.app/Contents/Resources/pt_BR.lproj/ArrangeByMenu.strings
        Foundation                  107c1c000-107c20000    [   16K     0K     0K    16K] rw-/rwx SM=PRV
        mapped file                 107c20000-107c2c000    [   48K    16K     0K     0K] r-x/rwx SM=COW          /usr/lib/libobjc-trampolines.dylib
        mapped file                 107c2c000-107c30000    [   16K     0K     0K     0K] r--/rw- SM=COW          /System/Library/Frameworks/AppKit.framework/Versions/C/Resources/Placeholders.loctable
        mapped file                 107c30000-107c38000    [   32K     0K     0K     0K] r--/rw- SM=COW          /System/Library/Frameworks/ColorSync.framework/Versions/A/Resources/InfoPlist.loctable
        mapped file                 107c38000-107c44000    [   48K     0K     0K     0K] r--/rw- SM=COW          /System/Library/PrivateFrameworks/CloudDocs.framework/Versions/A/Resources/Localizable.loctable
        mapped file                 107c44000-107c48000    [   16K     0K     0K     0K] r--/rw- SM=COW          /System/Library/Frameworks/AppKit.framework/Versions/C/Resources/DictationManager.loctable
        mapped file                 107c48000-107c4c000    [   16K     0K     0K     0K] r--/rw- SM=COW          /System/Library/Frameworks/AppKit.framework/Versions/C/Resources/WritingTools.loctable
        mapped file                 107c4c000-107c50000    [   16K     0K     0K     0K] r--/rw- SM=COW          /System/Library/Frameworks/FileProvider.framework/Versions/A/Resources/InfoPlist.loctable
        IOKit                       107c50000-107c54000    [   16K    16K    16K     0K] r--/r-- SM=SHM
        shared memory               107c54000-107c58000    [   16K     0K     0K    16K] r--/r-- SM=SHM
        IOAccelerator                107c58000-107c5c000    [   16K    16K    16K     0K] r--/r-- SM=SHM
        __TEXT                      107c5c000-107c68000    [   48K    16K     0K     0K] r-x/rwx SM=COW          /usr/lib/libobjc-trampolines.dylib
        __LINKEDIT                  107c68000-107c70000    [   32K     0K     0K     0K] r--/rw- SM=COW          /usr/lib/libobjc-trampolines.dylib
        mapped file                 107c70000-107eb4000    [ 2320K     0K     0K     0K] r--/rw- SM=COW          /System/Library/Fonts/Helvetica.ttc
        Foundation                  107eb4000-10840c000    [ 5472K     0K     0K  5472K] rw-/rw- SM=COW
        shared memory               10840c000-108410000    [   16K     0K     0K    16K] r--/r-- SM=SHM
        mapped file                 108410000-108430000    [  128K     0K     0K     0K] r--/rw- SM=COW          /System/Library/Frameworks/QuickLook.framework/Versions/A/Resources/Localizable.loctable
        mapped file                 108430000-108684000    [ 2384K     0K     0K     0K] r--/rw- SM=COW          /System/Library/CoreServices/Finder.app/Contents/Resources/Assets.car
        mapped file                 108684000-108688000    [   16K     0K     0K     0K] r--/rw- SM=COW          /System/Library/Frameworks/CoreServices.framework/Versions/A/Frameworks/Metadata.framework/Versions/A/Resources/pt.lproj/GroupNames.strings
        shared memory               108688000-10868c000    [   16K     0K     0K    16K] r--/r-- SM=SHM
        mapped file                 10868c000-1086d8000    [  304K     0K     0K     0K] r--/rw- SM=COW          /System/Library/Frameworks/FileProvider.framework/Versions/A/Resources/FileProvider.loctable
        MALLOC metadata             1086d8000-1086fc000    [  144K     0K     0K   144K] rw-/rwx SM=PRV          AttributeGraph graph data_0x1086d8000 zone structure
        mapped file                 1086fc000-10870c000    [   64K     0K     0K     0K] r--/rw- SM=COW          /System/Library/Frameworks/AppKit.framework/Versions/C/Resources/Common.loctable
        CoreGraphics                10870c000-108710000    [   16K     0K     0K    16K] r--/r-- SM=SHM
        CoreAnimation                108710000-108714000    [   16K     0K     0K    16K] r--/r-- SM=PRV
        CoreAnimation                108714000-108718000    [   16K     0K     0K    16K] r--/r-- SM=PRV
        CG image                    108718000-10871c000    [   16K     0K     0K    16K] rw-/rwx SM=PRV
        CoreAnimation                10871c000-108720000    [   16K     0K     0K    16K] r--/r-- SM=PRV
        shared memory               108720000-108724000    [   16K     0K     0K    16K] r--/r-- SM=SHM
        CoreAnimation                108724000-108728000    [   16K     0K     0K    16K] r--/r-- SM=PRV
        mapped file                 10872c000-108758000    [  176K     0K     0K     0K] r--/rw- SM=COW          /System/Library/Frameworks/AppKit.framework/Versions/C/Resources/MenuCommands.loctable
        IOKit                       108758000-10875c000    [   16K    16K    16K     0K] rw-/rw- SM=SHM PURGE=N
        IOAccelerator                10875c000-108768000    [   48K    48K    48K     0K] r--/r-- SM=SHM
        IOKit                       108768000-10876c000    [   16K    16K    16K     0K] r--/r-- SM=SHM
        IOAccelerator (graphics)    10876c000-10877c000    [   64K    64K     0K    64K] rw-/rw- SM=SHM PURGE=N
        shared memory               10877c000-108780000    [   16K     0K     0K    16K] r--/r-- SM=SHM
        shared memory               108780000-108784000    [   16K     0K     0K    16K] r--/r-- SM=SHM
        VM_ALLOCATE                 108784000-108788000    [   16K     0K     0K    16K] rw-/rwx SM=PRV
        mapped file                 108788000-1087ac000    [  144K     0K     0K     0K] r--/rw- SM=COW          /System/Library/Caches/com.apple.IntlDataCache.le.kbdx
        mapped file                 1087ac000-1087ec000    [  256K     0K     0K     0K] r--/rw- SM=COW          /System/Library/Keyboard Layouts/AppleKeyboardLayouts.bundle/Contents/Resources/InfoPlist.loctable
        VM_ALLOCATE                 1087ec000-1087f0000    [   16K     0K     0K    16K] rw-/rwx SM=PRV
        mapped file                 1087f0000-1087f4000    [   16K     0K     0K     0K] r--/r-- SM=ALI          /Library/Caches/com.apple.iconservices.store/6DBA50CD-6FEA-3BC2-AF10-3DFB0EDD83D0.isdata
        ColorSync                    1087f4000-1087f8000    [   16K     0K     0K    16K] r--/r-- SM=PRV
        CG image                    1087f8000-1087fc000    [   16K     0K     0K    16K] rw-/rwx SM=PRV
        AttributeGraph Data (old    1087fc000-1088fc000    [ 1024K     0K     0K  1024K] rw-/rwx SM=SHM
        mapped file                 1088fc000-108910000    [   80K     0K     0K     0K] r--/rw- SM=COW          /System/Library/CoreServices/CoreTypes.bundle/Contents/Resources/UnknownFSObjectIcon.icns
        mapped file                 108910000-108918000    [   32K     0K     0K     0K] r--/r-- SM=ALI          /Library/Caches/com.apple.iconservices.store/6DBA50CD-6FEA-3BC2-AF10-3DFB0EDD83D0.isdata
        """;

    [Fact]
    public void ExtrairTamanhoDePagina_CabecalhoComPaginaDe16384_ExtraiCorretamente()
    {
        var tamanho = AnalisadorDeSaidaDoVmmap.ExtrairTamanhoDePagina(AmostraCompleta);

        Assert.NotNull(tamanho);
        Assert.Equal(16384, tamanho!.Value.EmBytes);
    }

    [Fact]
    public void ExtrairTamanhoDePagina_SemCabecalho_RetornaNull()
    {
        var tamanho = AnalisadorDeSaidaDoVmmap.ExtrairTamanhoDePagina("nada relevante aqui");

        Assert.Null(tamanho);
    }

    [Fact]
    public void AnalisarRegioes_LinhaText_ViraCodigoComFaixaHexEResidenteCorretos()
    {
        const string linha =
            "__TEXT                      1023f0000-102e9c000    [ 10.7M  1712K     0K     0K] r-x/r-x SM=COW          /System/Library/CoreServices/Finder.app/Contents/MacOS/Finder";

        var regioes = AnalisadorDeSaidaDoVmmap.AnalisarRegioes(linha);

        var regiao = Assert.Single(regioes);
        Assert.Equal(TipoDeRegiao.Codigo, regiao.TipoDeRegiao);
        Assert.Equal(0x1023f0000UL, regiao.FaixaDeEnderecos.Inicio.Valor);
        Assert.Equal(0x102e9c000UL, regiao.FaixaDeEnderecos.Fim.Valor);
        Assert.True(regiao.Residente.TemValor);
        Assert.Equal(1712L * 1024, regiao.Residente.Valor.Valor);
        Assert.Equal("r-x", regiao.PermissoesTexto);
    }

    [Fact]
    public void AnalisarRegioes_LinhaMalloc_ViraHeap()
    {
        const string linha =
            "MALLOC_TINY                 1037e0000-103be0000    [ 4096K   208K   208K    64K] rw-/rwx SM=PRV          DefaultMallocZone_0x103570000";

        var regioes = AnalisadorDeSaidaDoVmmap.AnalisarRegioes(linha);

        var regiao = Assert.Single(regioes);
        Assert.Equal(TipoDeRegiao.Heap, regiao.TipoDeRegiao);
    }

    [Fact]
    public void AnalisarRegioes_AmostraCompleta_ContagemMaiorQueZeroSemLancarExcecao()
    {
        var regioes = AnalisadorDeSaidaDoVmmap.AnalisarRegioes(AmostraCompleta);

        Assert.True(regioes.Count > 0);
    }

    [Fact]
    public void AnalisarRegioes_LinhaMalformadaMisturadaComValida_IgnoraApenasAMalformada()
    {
        const string entrada = """
            isso não é uma linha de região válida de jeito nenhum
            __TEXT                      1023f0000-102e9c000    [ 10.7M  1712K     0K     0K] r-x/r-x SM=COW          /System/Library/CoreServices/Finder.app/Contents/MacOS/Finder
            """;

        var regioes = AnalisadorDeSaidaDoVmmap.AnalisarRegioes(entrada);

        var regiao = Assert.Single(regioes);
        Assert.Equal(TipoDeRegiao.Codigo, regiao.TipoDeRegiao);
    }
}
