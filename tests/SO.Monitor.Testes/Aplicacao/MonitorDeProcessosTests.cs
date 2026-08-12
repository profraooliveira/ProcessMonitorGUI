using SO.Monitor.Aplicacao.CasosDeUso;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.Servicos;
using Xunit;

namespace SO.Monitor.Testes.Aplicacao;

public class MonitorDeProcessosTests
{
    [Fact]
    public async Task ObservarAsync_ComIntervaloCurto_EmiteVariasAmostrasECancelaLimpo()
    {
        var obterAmostra = new ObterAmostraClassificada(new FonteDeProcessosFalsa(), new PoliticaPorComportamentoDeBurst());
        var monitor = new MonitorDeProcessos(obterAmostra);

        using var cts = new CancellationTokenSource();
        var amostrasRecebidas = new List<AmostraDoSistema>();
        Exception? excecaoCapturada = null;

        try
        {
            await foreach (var amostra in monitor.ObservarAsync(TimeSpan.FromMilliseconds(50), CriteriosDeAmostragem.Padrao, cts.Token))
            {
                amostrasRecebidas.Add(amostra);
                if (amostrasRecebidas.Count >= 2)
                    cts.Cancel();
            }
        }
        catch (Exception excecao)
        {
            excecaoCapturada = excecao;
        }

        Assert.True(amostrasRecebidas.Count >= 2, $"Esperava >= 2 amostras, recebeu {amostrasRecebidas.Count}.");
        Assert.Null(excecaoCapturada);
    }

    [Fact]
    public async Task ObservarAsync_CancelamentoAntesDoInicio_TerminaSemEmitirNemLancar()
    {
        var obterAmostra = new ObterAmostraClassificada(new FonteDeProcessosFalsa(), new PoliticaPorComportamentoDeBurst());
        var monitor = new MonitorDeProcessos(obterAmostra);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var amostrasRecebidas = new List<AmostraDoSistema>();
        Exception? excecaoCapturada = null;

        try
        {
            await foreach (var amostra in monitor.ObservarAsync(TimeSpan.FromMilliseconds(50), CriteriosDeAmostragem.Padrao, cts.Token))
                amostrasRecebidas.Add(amostra);
        }
        catch (Exception excecao)
        {
            excecaoCapturada = excecao;
        }

        Assert.Empty(amostrasRecebidas);
        Assert.Null(excecaoCapturada);
    }
}
