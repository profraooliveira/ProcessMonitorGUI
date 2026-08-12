using SO.Monitor.Dominio.Enums;
using SO.Monitor.Infraestrutura.MacOs;
using Xunit;

namespace SO.Monitor.Testes.Infraestrutura.MacOs;

/// <summary>
/// Cobre o analisador puro de <c>ps -M -p &lt;pid&gt;</c> com uma amostra real coletada em um
/// macOS 26.5 (Apple Silicon), embutida como literal de string bruta.
/// </summary>
public class AnalisadorDeSaidaDoPsTests
{
    /// <summary>Amostra real de <c>ps -M -p 640</c> (Finder): 1 linha de cabeçalho + 6 linhas de thread.</summary>
    private const string AmostraCompleta = """
        USER            PID   TT   %CPU STAT PRI     STIME     UTIME COMMAND
        raonioliveira   640   ??    0.0 S    46T   0:13.55   1:15.53 /System/Library/CoreServices/Finder.app/Contents/MacOS/Finder
                        640         0.0 S    46T   0:00.80   0:00.93
                        640         0.0 S    31T   0:00.00   0:00.00
                        640         0.0 S    20T   0:00.00   0:00.00
                        640         0.0 S    20T   0:00.03   0:00.03
                        640         0.0 S    31T   0:00.00   0:00.00
        """;

    [Fact]
    public void AnalisarThreads_AmostraCompleta_ContaUmaThreadPorLinhaDeDados()
    {
        var threads = AnalisadorDeSaidaDoPs.AnalisarThreads(AmostraCompleta);

        Assert.Equal(6, threads.Count);
    }

    [Fact]
    public void AnalisarThreads_StatS_ViraBloqueadaEsperandoEvento()
    {
        var threads = AnalisadorDeSaidaDoPs.AnalisarThreads(AmostraCompleta);

        Assert.All(threads, thread =>
        {
            Assert.Equal(EstadoThread.Bloqueada, thread.EstadoThread);
            Assert.True(thread.MotivoDeBloqueio.TemValor);
            Assert.Equal(MotivoDeBloqueio.EsperandoEvento, thread.MotivoDeBloqueio.Valor);
        });
    }

    [Fact]
    public void AnalisarThreads_PrimeiraThread_SomaStimeMaisUtimeCorretamente()
    {
        var threads = AnalisadorDeSaidaDoPs.AnalisarThreads(AmostraCompleta);

        var primeira = threads[0];
        var esperado = new TimeSpan(0, 0, 0, 13, 550) + new TimeSpan(0, 0, 1, 15, 530);

        Assert.True(primeira.TempoDeCpu.TemValor);
        Assert.Equal(esperado, primeira.TempoDeCpu.Valor);
    }

    [Fact]
    public void AnalisarThreads_PriComSufixoDeLetra_ExtraiApenasOsDigitos()
    {
        var threads = AnalisadorDeSaidaDoPs.AnalisarThreads(AmostraCompleta);

        var primeira = threads[0];

        Assert.True(primeira.PrioridadeBase.TemValor);
        Assert.Equal(46, primeira.PrioridadeBase.Valor);
    }

    [Fact]
    public void AnalisarThreads_TidsSaoPosicionaisAPartirDeZero()
    {
        var threads = AnalisadorDeSaidaDoPs.AnalisarThreads(AmostraCompleta);

        for (var i = 0; i < threads.Count; i++)
            Assert.Equal(i, threads[i].Tid.Valor);
    }

    [Fact]
    public void AnalisarThreads_MarcaTidComoPosicional_PoisPsDoMacOsNaoExpoeTidsDeKernel()
    {
        var threads = AnalisadorDeSaidaDoPs.AnalisarThreads(AmostraCompleta);

        Assert.All(threads, thread => Assert.True(thread.TidEhPosicional));
    }

    [Fact]
    public void AnalisarThreads_LinhaMalformadaMisturadaComValida_IgnoraApenasAMalformada()
    {
        const string entrada = """
            isso não é uma linha de thread válida
            raonioliveira   640   ??    0.0 S    46T   0:13.55   1:15.53 /System/Library/CoreServices/Finder.app/Contents/MacOS/Finder
            """;

        var threads = AnalisadorDeSaidaDoPs.AnalisarThreads(entrada);

        Assert.Single(threads);
    }
}
