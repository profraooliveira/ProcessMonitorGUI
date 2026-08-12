using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.ValueObjects;
using SO.Monitor.Infraestrutura.Linux;
using Xunit;

namespace SO.Monitor.Testes.Infraestrutura.Linux;

/// <summary>
/// Teste "ao vivo" contra o processo atual — só faz sentido em Linux de verdade (não roda aqui,
/// desenvolvido em macOS), então é guardado no topo e vira um no-op em qualquer outra plataforma.
/// Fica pronto para o CI futuro em Linux exercitar o caminho real de <c>/proc/&lt;pid&gt;/smaps</c>.
/// </summary>
public class FonteDeMapaDeMemoriaLinuxTests
{
    [Fact]
    public async Task ObterMapaAsync_ProcessoAtual_DevolveMapaComOrigemMedidaReal()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var fonte = new FonteDeMapaDeMemoriaLinux();
        var pidAtual = new Pid(Environment.ProcessId);

        var leitura = await fonte.ObterMapaAsync(pidAtual, CancellationToken.None);

        Assert.True(leitura.TemValor);
        Assert.Equal(OrigemDosDados.MedidaReal, leitura.Valor!.OrigemDosDados);
        Assert.NotEmpty(leitura.Valor.Regioes);
    }

    [Fact]
    public async Task ObterMapaAsync_PidInexistente_DevolveProcessoEncerrado()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var fonte = new FonteDeMapaDeMemoriaLinux();

        // PID improvável de existir: acima do limite típico de pid_max em qualquer distro comum.
        var leitura = await fonte.ObterMapaAsync(new Pid(2_000_000_000), CancellationToken.None);

        Assert.False(leitura.TemValor);
        Assert.Equal(Disponibilidade.ProcessoEncerrado, leitura.Estado);
    }
}
