using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.ValueObjects;
using SO.Monitor.Infraestrutura.MacOs;
using Xunit;

namespace SO.Monitor.Testes.Infraestrutura.MacOs;

/// <summary>
/// Testes "ao vivo": executam o <c>vmmap</c> real da máquina contra processos reais, em vez de
/// amostras fixas. Só fazem sentido no macOS — em qualquer outra plataforma o executável
/// <c>/usr/bin/vmmap</c> simplesmente não existe, então cada teste sai cedo sem falhar.
/// </summary>
public class FonteDeMapaDeMemoriaMacOsTests
{
    [Fact]
    public async Task ObterMapaAsync_ProcessoAtual_RetornaMapaRealComRegioes()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var fonte = new FonteDeMapaDeMemoriaMacOs();
        var pid = new Pid(Environment.ProcessId);

        var leitura = await fonte.ObterMapaAsync(pid, CancellationToken.None);

        Assert.True(leitura.TemValor);
        Assert.Equal(OrigemDosDados.MedidaReal, leitura.Valor!.OrigemDosDados);
        Assert.True(leitura.Valor.Regioes.Count > 0);
    }

    [Fact]
    public async Task ObterMapaAsync_ProcessoProtegidoLaunchd_RetornaAcessoNegado()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var fonte = new FonteDeMapaDeMemoriaMacOs();
        var pid = new Pid(1); // launchd — protegido mesmo para o próprio usuário sem sudo.

        var leitura = await fonte.ObterMapaAsync(pid, CancellationToken.None);

        Assert.Equal(Disponibilidade.AcessoNegado, leitura.Estado);
    }
}
