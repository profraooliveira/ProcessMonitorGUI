using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.ValueObjects;
using SO.Monitor.Infraestrutura.Linux;
using Xunit;

namespace SO.Monitor.Testes.Infraestrutura.Linux;

/// <summary>
/// Teste "ao vivo" contra o processo atual — só faz sentido em Linux de verdade (não roda aqui,
/// desenvolvido em macOS), então é guardado no topo e vira um no-op em qualquer outra plataforma.
/// Fica pronto para o CI futuro em Linux exercitar a enumeração real de <c>/proc/&lt;pid&gt;/task</c>.
/// </summary>
public class FonteDeThreadsLinuxTests
{
    [Fact]
    public async Task ObterThreadsAsync_ProcessoAtual_DevolvePeloMenosAThreadPrincipal()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var fonte = new FonteDeThreadsLinux();
        var pidAtual = new Pid(Environment.ProcessId);

        var leitura = await fonte.ObterThreadsAsync(pidAtual, CancellationToken.None);

        Assert.True(leitura.TemValor);
        Assert.NotEmpty(leitura.Valor!);
    }

    [Fact]
    public async Task ObterThreadsAsync_PidInexistente_DevolveProcessoEncerrado()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var fonte = new FonteDeThreadsLinux();

        var leitura = await fonte.ObterThreadsAsync(new Pid(2_000_000_000), CancellationToken.None);

        Assert.False(leitura.TemValor);
        Assert.Equal(Disponibilidade.ProcessoEncerrado, leitura.Estado);
    }
}
