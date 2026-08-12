using SO.Monitor.Aplicacao.CasosDeUso;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Servicos;
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
}
