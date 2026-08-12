using SO.Monitor.Dominio.ValueObjects;
using SO.Monitor.Infraestrutura.MacOs;
using Xunit;

namespace SO.Monitor.Testes.Infraestrutura.MacOs;

/// <summary>
/// Teste "ao vivo": executa o <c>ps -M</c> real da máquina contra o processo atual. Só faz
/// sentido no macOS — em qualquer outra plataforma o executável <c>/bin/ps</c> não tem o formato
/// esperado (ou nem existe), então o teste sai cedo sem falhar.
/// </summary>
public class FonteDeThreadsMacOsTests
{
    [Fact]
    public async Task ObterThreadsAsync_ProcessoAtual_RetornaThreadsReais()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var fonte = new FonteDeThreadsMacOs();
        var pid = new Pid(Environment.ProcessId);

        var leitura = await fonte.ObterThreadsAsync(pid, CancellationToken.None);

        Assert.True(leitura.TemValor);
        Assert.True(leitura.Valor!.Count > 0);
    }
}
