using SO.Monitor.Aplicacao.CasosDeUso;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Servicos;
using SO.Monitor.Dominio.ValueObjects;
using Xunit;

namespace SO.Monitor.Testes.Aplicacao;

public class ObterAmostraClassificadaTests
{
    private static ObterAmostraClassificada CriarCasoDeUso() =>
        new(new FonteDeProcessosFalsa(), new PoliticaPorComportamentoDeBurst());

    [Fact]
    public async Task ExecutarAsync_ClassificaTodosOsProcessosDaAmostra()
    {
        var casoDeUso = CriarCasoDeUso();

        var amostra = await casoDeUso.ExecutarAsync(CriteriosDeAmostragem.Padrao, CancellationToken.None);

        Assert.Equal(3, amostra.Processos.Count);
        Assert.All(amostra.Processos, processo => Assert.NotNull(processo.Classificacao));
    }

    [Fact]
    public async Task ExecutarAsync_FiltraPorPerfilQuandoInformado()
    {
        var casoDeUso = CriarCasoDeUso();
        var criterios = new CriteriosDeAmostragem(PerfilDeExecucao.LimitadoPorES, int.MaxValue);

        var amostra = await casoDeUso.ExecutarAsync(criterios, CancellationToken.None);

        var processo = Assert.Single(amostra.Processos);
        Assert.Equal("servidor-web", processo.Nome);
        Assert.Equal(PerfilDeExecucao.LimitadoPorES, processo.Classificacao!.Value.Perfil);
    }

    [Fact]
    public async Task ExecutarAsync_SemFiltro_OrdenaPorUsoDeCpuDecrescente()
    {
        var casoDeUso = CriarCasoDeUso();

        var amostra = await casoDeUso.ExecutarAsync(CriteriosDeAmostragem.Padrao, CancellationToken.None);

        Assert.Equal(["compilador", "editor", "servidor-web"], amostra.Processos.Select(processo => processo.Nome));
    }

    [Fact]
    public async Task ExecutarAsync_RespeitaOLimite()
    {
        var casoDeUso = CriarCasoDeUso();
        var criterios = new CriteriosDeAmostragem(FiltrarPorPerfil: null, Limite: 2);

        var amostra = await casoDeUso.ExecutarAsync(criterios, CancellationToken.None);

        Assert.Equal(["compilador", "editor"], amostra.Processos.Select(processo => processo.Nome));
    }

    /// <summary>
    /// Regressão: o processo selecionado na interface não pode desaparecer da lista só porque
    /// sua %CPU oscilou pra fora do corte por Limite nesta rodada — sem isso, a seleção do
    /// usuário "anda"/some a cada poucos ciclos (o item é removido e, se voltar, vira uma
    /// instância NOVA, órfã da seleção anterior).
    /// </summary>
    [Fact]
    public async Task ExecutarAsync_PidFixadoForaDoLimite_PermaneceNaAmostra()
    {
        var casoDeUso = CriarCasoDeUso();
        // servidor-web (pid 200, 5% CPU) é o 3º por CPU — cairia fora de Limite=2 sem o pin.
        var criterios = new CriteriosDeAmostragem(FiltrarPorPerfil: null, Limite: 2, PidFixado: new Pid(200));

        var amostra = await casoDeUso.ExecutarAsync(criterios, CancellationToken.None);

        Assert.Contains(amostra.Processos, processo => processo.Nome == "servidor-web");
        Assert.Equal(3, amostra.Processos.Count); // top-2 + o fixado
    }

    [Fact]
    public async Task ExecutarAsync_PidFixadoJaNoLimite_NaoDuplica()
    {
        var casoDeUso = CriarCasoDeUso();
        // compilador (pid 100) já está no top-2 — não deveria virar 3 itens.
        var criterios = new CriteriosDeAmostragem(FiltrarPorPerfil: null, Limite: 2, PidFixado: new Pid(100));

        var amostra = await casoDeUso.ExecutarAsync(criterios, CancellationToken.None);

        Assert.Equal(2, amostra.Processos.Count);
    }

    /// <summary>
    /// O pin NÃO sobrepõe o filtro de perfil: se o processo selecionado deixar de bater com o
    /// filtro explicitamente pedido pelo usuário, ele some mesmo assim — isso é o filtro
    /// funcionando como pedido, diferente do corte por Limite (um teto de exibição arbitrário).
    /// </summary>
    [Fact]
    public async Task ExecutarAsync_PidFixadoNaoBateComFiltroDePerfil_NaoAparece()
    {
        var casoDeUso = CriarCasoDeUso();
        // servidor-web classifica como LimitadoPorES; filtro pede só LimitadoPorCpu.
        var criterios = new CriteriosDeAmostragem(PerfilDeExecucao.LimitadoPorCpu, Limite: 10, PidFixado: new Pid(200));

        var amostra = await casoDeUso.ExecutarAsync(criterios, CancellationToken.None);

        Assert.DoesNotContain(amostra.Processos, processo => processo.Nome == "servidor-web");
    }
}
