using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Servicos;
using SO.Monitor.Dominio.ValueObjects;
using Xunit;

namespace SO.Monitor.Testes.Dominio.Servicos;

public class SimuladorDePaginacaoTests
{
    [Fact]
    public void Simular_MesmoPidEMesmasEntradas_ProduzMapaIdentico()
    {
        var pid = new Pid(4242);
        var conjuntoResidente = new TamanhoBytes(50 * 1024 * 1024);
        var memoriaVirtual = new TamanhoBytes(200 * 1024 * 1024);
        var pagina = TamanhoPagina.Kib4;
        var instante = DateTimeOffset.UtcNow;

        var primeiroMapa = SimuladorDePaginacao.Simular(pid, conjuntoResidente, memoriaVirtual, pagina, instante);
        var segundoMapa = SimuladorDePaginacao.Simular(pid, conjuntoResidente, memoriaVirtual, pagina, instante);

        Assert.Equal(primeiroMapa.Regioes.Count, segundoMapa.Regioes.Count);
        Assert.Equal(primeiroMapa.Regioes, segundoMapa.Regioes);
    }

    [Fact]
    public void Simular_PidsDiferentes_ProduzemMapasComRegioesDiferentes()
    {
        var conjuntoResidente = new TamanhoBytes(50 * 1024 * 1024);
        var memoriaVirtual = new TamanhoBytes(200 * 1024 * 1024);
        var pagina = TamanhoPagina.Kib4;
        var instante = DateTimeOffset.UtcNow;

        var mapaDoPid1 = SimuladorDePaginacao.Simular(new Pid(1), conjuntoResidente, memoriaVirtual, pagina, instante);
        var mapaDoPid2 = SimuladorDePaginacao.Simular(new Pid(2), conjuntoResidente, memoriaVirtual, pagina, instante);

        Assert.NotEqual(mapaDoPid1.Regioes, mapaDoPid2.Regioes);
    }

    [Fact]
    public void Simular_MarcaOrigemComoSimulada()
    {
        var mapa = SimuladorDePaginacao.Simular(
            new Pid(1),
            new TamanhoBytes(10 * 1024 * 1024),
            new TamanhoBytes(50 * 1024 * 1024),
            TamanhoPagina.Kib4,
            DateTimeOffset.UtcNow);

        Assert.Equal(OrigemDosDados.Simulada, mapa.OrigemDosDados);
    }

    [Fact]
    public void Simular_SomaDasExtensoesDasRegioes_IgualAMemoriaVirtualSolicitada()
    {
        var memoriaVirtual = new TamanhoBytes(200 * 1024 * 1024);

        var mapa = SimuladorDePaginacao.Simular(
            new Pid(777),
            new TamanhoBytes(80 * 1024 * 1024),
            memoriaVirtual,
            TamanhoPagina.Kib4,
            DateTimeOffset.UtcNow);

        var somaDasExtensoes = mapa.Regioes.Sum(regiao => regiao.FaixaDeEnderecos.Extensao.Valor);

        Assert.Equal(memoriaVirtual.Valor, somaDasExtensoes);
    }

    [Fact]
    public void Simular_PrimeiraRegiao_EhDeCodigo()
    {
        var mapa = SimuladorDePaginacao.Simular(
            new Pid(99),
            new TamanhoBytes(10 * 1024 * 1024),
            new TamanhoBytes(50 * 1024 * 1024),
            TamanhoPagina.Kib4,
            DateTimeOffset.UtcNow);

        Assert.Equal(TipoDeRegiao.Codigo, mapa.Regioes[0].TipoDeRegiao);
    }

    [Fact]
    public void Simular_ConjuntoResidenteMenorQueVirtual_GeraTotalEmSwapMaiorQueZero()
    {
        var mapa = SimuladorDePaginacao.Simular(
            new Pid(555),
            conjuntoResidente: new TamanhoBytes(10 * 1024 * 1024),
            memoriaVirtual: new TamanhoBytes(500 * 1024 * 1024),
            TamanhoPagina.Kib4,
            DateTimeOffset.UtcNow);

        Assert.True(mapa.TotalEmSwap.Valor > 0);
    }

    [Fact]
    public void Simular_TodasAsRegioes_TemLeiturasDeMemoriaDisponiveis()
    {
        // Todo dado de um mapa simulado é conhecido por construção — nunca há "acesso negado" aqui.
        var mapa = SimuladorDePaginacao.Simular(
            new Pid(3),
            new TamanhoBytes(10 * 1024 * 1024),
            new TamanhoBytes(50 * 1024 * 1024),
            TamanhoPagina.Kib4,
            DateTimeOffset.UtcNow);

        Assert.All(mapa.Regioes, regiao =>
        {
            Assert.True(regiao.Residente.TemValor);
            Assert.True(regiao.EmSwap.TemValor);
        });
    }
}
