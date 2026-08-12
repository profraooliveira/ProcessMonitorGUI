using SO.Monitor.Aplicacao.CasosDeUso;
using SO.Monitor.Aplicacao.Ports;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;
using Xunit;

namespace SO.Monitor.Testes.Aplicacao;

public class ObterMapaDeMemoriaTests
{
    private sealed class FonteDeMapaDeMemoriaFalsa(Leitura<MapaDeMemoria> leitura) : IFonteDeMapaDeMemoria
    {
        public ValueTask<Leitura<MapaDeMemoria>> ObterMapaAsync(Pid pid, CancellationToken cancellationToken) =>
            ValueTask.FromResult(leitura);
    }

    private static readonly Pid PidQualquer = new(123);

    private static readonly PerfilDeMemoria PerfilQualquer = new(
        Leitura<TamanhoBytes>.Ok(new TamanhoBytes(10 * 1024 * 1024)),
        Leitura<TamanhoBytes>.Ok(new TamanhoBytes(40 * 1024 * 1024)),
        Leitura<TamanhoBytes>.Ok(new TamanhoBytes(5 * 1024 * 1024)),
        TamanhoPagina.Kib4);

    [Fact]
    public async Task ExecutarAsync_FonteRealDisponivel_DevolveMapaRealSemMotivoDeFallback()
    {
        var mapaReal = new MapaDeMemoria(PidQualquer, OrigemDosDados.MedidaReal, [], DateTimeOffset.UtcNow);
        var casoDeUso = new ObterMapaDeMemoria(new FonteDeMapaDeMemoriaFalsa(Leitura<MapaDeMemoria>.Ok(mapaReal)));

        var resultado = await casoDeUso.ExecutarAsync(PidQualquer, PerfilQualquer, TamanhoPagina.Kib4, CancellationToken.None);

        Assert.Equal(OrigemDosDados.MedidaReal, resultado.Mapa.OrigemDosDados);
        Assert.Null(resultado.MotivoDoFallback);
    }

    [Fact]
    public async Task ExecutarAsync_FonteRealNegada_CaiParaSimuladoComMotivoDoFallback()
    {
        var casoDeUso = new ObterMapaDeMemoria(new FonteDeMapaDeMemoriaFalsa(Leitura<MapaDeMemoria>.Negada()));

        var resultado = await casoDeUso.ExecutarAsync(PidQualquer, PerfilQualquer, TamanhoPagina.Kib4, CancellationToken.None);

        Assert.Equal(OrigemDosDados.Simulada, resultado.Mapa.OrigemDosDados);
        Assert.Equal(Disponibilidade.AcessoNegado, resultado.MotivoDoFallback);
    }

    [Fact]
    public async Task ExecutarAsync_FonteRealNaoSuportada_CaiParaSimuladoComMotivoDoFallback()
    {
        var casoDeUso = new ObterMapaDeMemoria(new FonteDeMapaDeMemoriaFalsa(Leitura<MapaDeMemoria>.NaoSuportada()));

        var resultado = await casoDeUso.ExecutarAsync(PidQualquer, PerfilQualquer, TamanhoPagina.Kib4, CancellationToken.None);

        Assert.Equal(OrigemDosDados.Simulada, resultado.Mapa.OrigemDosDados);
        Assert.Equal(Disponibilidade.NaoSuportadoNaPlataforma, resultado.MotivoDoFallback);
        Assert.NotEmpty(resultado.Mapa.Regioes);
    }
}
