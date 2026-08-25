using MonitorGUI.ViewModels;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using Xunit;

namespace SO.Monitor.Testes.Apresentacao;

/// <summary>
/// Cobre as traduções pt-BR de <see cref="RotulosPtBr"/> introduzidas pela revisão de aceite:
/// o novo estado <see cref="Disponibilidade.NaoSeAplica"/> (FIX6) e a formatação de TID
/// posicional vs. real (FIX7).
/// </summary>
public class RotulosPtBrTests
{
    [Fact]
    public void TextoMotivoDeBloqueio_EstadoNaoBloqueada_RetornaTraco()
    {
        var texto = RotulosPtBr.TextoMotivoDeBloqueio(Leitura<MotivoDeBloqueio>.NaoSeAplica(), EstadoThread.EmExecucao);

        Assert.Equal("—", texto);
    }

    [Fact]
    public void TextoMotivoDeBloqueio_BloqueadaComLeituraNaoSeAplica_RetornaTraco()
    {
        // Caso defensivo: uma thread reportada como Bloqueada cuja leitura de motivo, por algum
        // motivo, carregue NaoSeAplica (não deveria acontecer nos produtores reais, mas a
        // apresentação nunca deve travar ou mostrar "motivo desconhecido" nesse caso).
        var texto = RotulosPtBr.TextoMotivoDeBloqueio(Leitura<MotivoDeBloqueio>.NaoSeAplica(), EstadoThread.Bloqueada);

        Assert.Equal("—", texto);
    }

    [Fact]
    public void TextoMotivoDeBloqueio_BloqueadaComLeituraDisponivel_RetornaTextoDoMotivo()
    {
        var texto = RotulosPtBr.TextoMotivoDeBloqueio(Leitura<MotivoDeBloqueio>.Ok(MotivoDeBloqueio.EsperandoES), EstadoThread.Bloqueada);

        Assert.Equal("esperando E/S", texto);
    }

    [Fact]
    public void TextoTid_NaoPosicional_RetornaNumeroPuro()
    {
        var texto = RotulosPtBr.TextoTid(4321, tidEhPosicional: false);

        Assert.Equal("4321", texto);
    }

    [Fact]
    public void TextoTid_Posicional_RetornaFormatoComHashtagESufixo()
    {
        var texto = RotulosPtBr.TextoTid(2, tidEhPosicional: true);

        Assert.Equal("#2 (posicional)", texto);
    }

    [Fact]
    public void TooltipTid_NaoPosicional_RetornaNull()
    {
        Assert.Null(RotulosPtBr.TooltipTid(tidEhPosicional: false));
    }

    [Fact]
    public void TooltipTid_Posicional_RetornaTextoExplicativo()
    {
        var tooltip = RotulosPtBr.TooltipTid(tidEhPosicional: true);

        Assert.NotNull(tooltip);
        Assert.Contains("ps", tooltip);
    }

    /// <summary>Trava contra um `switch` esquecido: se <see cref="TipoDeRegiao"/> ganhar um novo valor, este teste falha até alguém preencher os dois métodos.</summary>
    [Theory]
    [MemberData(nameof(TodosOsTiposDeRegiao))]
    public void TextoETextoDidatico_TodoTipoDeRegiao_RetornamTextoNaoVazio(TipoDeRegiao tipo)
    {
        Assert.False(string.IsNullOrWhiteSpace(RotulosPtBr.Texto(tipo)));
        Assert.False(string.IsNullOrWhiteSpace(RotulosPtBr.TextoDidatico(tipo)));
    }

    public static IEnumerable<object[]> TodosOsTiposDeRegiao() =>
        Enum.GetValues<TipoDeRegiao>().Select(tipo => new object[] { tipo });
}
