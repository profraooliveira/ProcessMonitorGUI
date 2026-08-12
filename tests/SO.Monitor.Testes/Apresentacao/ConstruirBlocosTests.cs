using System.Globalization;
using MonitorGUI.ViewModels;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;
using Xunit;

namespace SO.Monitor.Testes.Apresentacao;

/// <summary>
/// Cobre <c>MainWindowViewModel.ConstruirBlocos</c> (exposto como <c>internal</c> via
/// <c>InternalsVisibleTo</c> só para este teste): o corte de quais regiões viram bloco individual
/// é por EXTENSÃO decrescente (as maiores primeiro), mas a exibição final deve estar ordenada por
/// ENDEREÇO crescente — para ensinar o layout real do espaço de endereçamento, não a ordem de
/// tamanho. Testado diretamente, sem instanciar <see cref="MainWindowViewModel"/> nem depender de
/// um Dispatcher Avalonia inicializado.
/// </summary>
public class ConstruirBlocosTests
{
    private static RegiaoDeMemoria CriarRegiao(ulong inicio, ulong extensao) => new(
        new FaixaDeEnderecos(new EnderecoVirtual(inicio), new EnderecoVirtual(inicio + extensao)),
        TipoDeRegiao.Heap,
        "rw-",
        Leitura<TamanhoBytes>.NaoSuportada(),
        Leitura<TamanhoBytes>.NaoSuportada(),
        Rotulo: null);

    private static ulong ExtrairEnderecoInicio(string faixaHexTexto)
    {
        var inicioHex = faixaHexTexto.Split('-')[0].Replace("0x", string.Empty, StringComparison.Ordinal);
        return ulong.Parse(inicioHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    [Fact]
    public void ConstruirBlocos_MenosQueOMaximo_ExibeEmOrdemDeEnderecoAscendente()
    {
        // Propositalmente fora de ordem de endereço E de extensão, para garantir que a saída não
        // é "por acaso" a mesma ordem de entrada nem a ordem por extensão.
        var regioes = new List<RegiaoDeMemoria>
        {
            CriarRegiao(0x3000, 0x100),
            CriarRegiao(0x1000, 0x500),
            CriarRegiao(0x2000, 0x50),
        };

        var blocos = MainWindowViewModel.ConstruirBlocos(regioes, _ => { });

        Assert.Equal(3, blocos.Count);
        Assert.All(blocos, bloco => Assert.False(bloco.EhAgregado));

        var enderecos = blocos.Select(bloco => ExtrairEnderecoInicio(bloco.FaixaHexTexto)).ToList();
        Assert.Equal(new List<ulong> { 0x1000, 0x2000, 0x3000 }, enderecos);
    }

    [Fact]
    public void ConstruirBlocos_MaisQueOMaximo_CortaPorExtensaoMasExibePorEndereco()
    {
        // 120 regiões "grandes" (extensão decrescente conforme o endereço cresce) + 5 regiões
        // "minúsculas" nos endereços mais altos: o corte por extensão deve escolher exatamente as
        // 120 grandes (nunca as 5 minúsculas), mas a exibição final deve ordená-las por endereço.
        var regioes = new List<RegiaoDeMemoria>();

        for (var i = 0; i < 120; i++)
        {
            var inicio = (ulong)(0x1000 * (i + 1));
            var extensao = (ulong)((120 - i) * 0x1000); // sempre >= 0x1000, bem maior que as minúsculas abaixo
            regioes.Add(CriarRegiao(inicio, extensao));
        }

        for (var i = 120; i < 125; i++)
        {
            var inicio = (ulong)(0x1000 * (i + 1));
            regioes.Add(CriarRegiao(inicio, extensao: 0x10)); // minúscula: nunca deveria virar bloco individual
        }

        var blocos = MainWindowViewModel.ConstruirBlocos(regioes, _ => { });

        Assert.Equal(121, blocos.Count); // 120 individuais + 1 agregado

        var individuais = blocos.Take(120).ToList();
        Assert.All(individuais, bloco => Assert.False(bloco.EhAgregado));

        var enderecos = individuais.Select(bloco => ExtrairEnderecoInicio(bloco.FaixaHexTexto)).ToList();
        var enderecosOrdenados = enderecos.OrderBy(endereco => endereco).ToList();
        Assert.Equal(enderecosOrdenados, enderecos); // já veio ordenado por endereço crescente

        // As 5 regiões minúsculas (endereços mais altos) nunca aparecem como bloco individual.
        Assert.DoesNotContain(enderecos, endereco => endereco >= 0x1000 * 121);

        var agregado = blocos[^1];
        Assert.True(agregado.EhAgregado);
        Assert.Equal(5 * 0x10L, agregado.ExtensaoBytes);
    }
}
