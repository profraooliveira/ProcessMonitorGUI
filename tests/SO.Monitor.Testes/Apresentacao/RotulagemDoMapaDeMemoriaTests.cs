using MonitorGUI.DesignTime;
using MonitorGUI.ViewModels;
using SO.Monitor.Aplicacao.CasosDeUso;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;
using Xunit;

namespace SO.Monitor.Testes.Apresentacao;

/// <summary>
/// Cobre a rotulagem obrigatória de origem do mapa de memória na Apresentação: a interface nunca
/// pode apresentar um mapa simulado como se fosse uma medição real do sistema operacional.
/// </summary>
public class RotulagemDoMapaDeMemoriaTests
{
    [Fact]
    public void RotuloDeOrigemDoMapa_MedidaReal_RotulaComoReal()
    {
        var rotulo = RotulosPtBr.RotuloDeOrigemDoMapa(OrigemDosDados.MedidaReal);

        Assert.Equal("MAPA DE MEMÓRIA REAL (medido do SO)", rotulo);
    }

    [Fact]
    public void RotuloDeOrigemDoMapa_Simulada_RotulaComoSimulado()
    {
        var rotulo = RotulosPtBr.RotuloDeOrigemDoMapa(OrigemDosDados.Simulada);

        Assert.Equal("MAPA SIMULADO (modo didático)", rotulo);
    }

    [Fact]
    public async Task ExecutarAsync_ComFonteDeDesignTimeIndisponivel_CaiParaSimuladoComMotivoDidatico()
    {
        // A fonte de design-time nunca tem mapa real disponível — mesmo comportamento de uma
        // plataforma sem provedor real conectado ainda (o baseline de todas antes da Fase D).
        var casoDeUso = new ObterMapaDeMemoria(new FonteDeMapaDeMemoriaDesignTime());

        var perfil = new PerfilDeMemoria(
            Leitura<TamanhoBytes>.Ok(new TamanhoBytes(1024 * 1024)),
            Leitura<TamanhoBytes>.Ok(new TamanhoBytes(4 * 1024 * 1024)),
            Leitura<TamanhoBytes>.Ok(new TamanhoBytes(512 * 1024)),
            TamanhoPagina.Kib4);

        var resultado = await casoDeUso.ExecutarAsync(new Pid(1), perfil, TamanhoPagina.Kib4, CancellationToken.None);

        Assert.Equal(OrigemDosDados.Simulada, resultado.Mapa.OrigemDosDados);
        Assert.Equal("MAPA SIMULADO (modo didático)", RotulosPtBr.RotuloDeOrigemDoMapa(resultado.Mapa.OrigemDosDados));
        Assert.NotNull(resultado.MotivoDoFallback);
        Assert.Equal(
            "leitura de mapa de memória não suportada nesta plataforma",
            RotulosPtBr.DescreverFallbackDeMapa(resultado.MotivoDoFallback!.Value));
    }
}
