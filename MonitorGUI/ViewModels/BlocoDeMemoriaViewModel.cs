using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;

namespace MonitorGUI.ViewModels;

/// <summary>
/// Embrulha uma <see cref="RegiaoDeMemoria"/> real (ou um agregado de regiões pequenas demais
/// para exibir individualmente) para o mapa de memória visual. Carrega o próprio comando de
/// seleção — <see cref="SelecionarCommand"/> — para que o botão de cada bloco no
/// <c>ItemsControl</c> não precise de <c>ReflectionBinding</c> para alcançar o VM da janela: o
/// item já sabe como se selecionar, delegando ao callback recebido do VM pai.
/// </summary>
public sealed partial class BlocoDeMemoriaViewModel : ObservableObject
{
    private readonly Action<BlocoDeMemoriaViewModel> _aoSelecionar;

    public TipoDeRegiao TipoDeRegiao { get; }

    public string Rotulo { get; }

    public string PermissoesTexto { get; }

    /// <summary>Faixa de endereços reais em hexadecimal, para o tooltip do bloco.</summary>
    public string FaixaHexTexto { get; }

    public long? ResidenteBytes { get; }

    public long? EmSwapBytes { get; }

    public long ExtensaoBytes { get; }

    /// <summary>Verdadeiro quando este bloco representa o agregado "…+K regiões menores".</summary>
    public bool EhAgregado { get; }

    [ObservableProperty]
    private bool _isSelected;

    public BlocoDeMemoriaViewModel(RegiaoDeMemoria regiao, Action<BlocoDeMemoriaViewModel> aoSelecionar)
    {
        _aoSelecionar = aoSelecionar;
        TipoDeRegiao = regiao.TipoDeRegiao;
        Rotulo = string.IsNullOrWhiteSpace(regiao.Rotulo) ? TextoPadraoDoTipo(regiao.TipoDeRegiao) : regiao.Rotulo!;
        PermissoesTexto = regiao.PermissoesTexto;
        FaixaHexTexto = regiao.FaixaDeEnderecos.ToString();
        ResidenteBytes = regiao.Residente.TemValor ? regiao.Residente.Valor.Valor : null;
        EmSwapBytes = regiao.EmSwap.TemValor ? regiao.EmSwap.Valor.Valor : null;
        ExtensaoBytes = regiao.FaixaDeEnderecos.Extensao.Valor;
        EhAgregado = false;
    }

    private BlocoDeMemoriaViewModel(
        string rotulo,
        long extensaoBytes,
        long? residenteBytes,
        long? emSwapBytes,
        Action<BlocoDeMemoriaViewModel> aoSelecionar)
    {
        _aoSelecionar = aoSelecionar;
        TipoDeRegiao = TipoDeRegiao.Outra;
        Rotulo = rotulo;
        PermissoesTexto = "—";
        FaixaHexTexto = "múltiplas regiões agregadas";
        ResidenteBytes = residenteBytes;
        EmSwapBytes = emSwapBytes;
        ExtensaoBytes = extensaoBytes;
        EhAgregado = true;
    }

    /// <summary>
    /// Agrega as regiões menores que não couberam entre as N maiores exibidas individualmente,
    /// somando extensão, residente e swap — mantém o mapa legível mesmo quando o processo tem
    /// centenas de regiões, sem simplesmente descartar a informação das menores.
    /// </summary>
    public static BlocoDeMemoriaViewModel CriarAgregado(
        IReadOnlyList<RegiaoDeMemoria> regioesMenores,
        Action<BlocoDeMemoriaViewModel> aoSelecionar)
    {
        var extensaoTotal = regioesMenores.Sum(regiao => regiao.FaixaDeEnderecos.Extensao.Valor);

        long? residenteTotal = regioesMenores.Any(regiao => regiao.Residente.TemValor)
            ? regioesMenores.Where(regiao => regiao.Residente.TemValor).Sum(regiao => regiao.Residente.Valor.Valor)
            : null;

        long? emSwapTotal = regioesMenores.Any(regiao => regiao.EmSwap.TemValor)
            ? regioesMenores.Where(regiao => regiao.EmSwap.TemValor).Sum(regiao => regiao.EmSwap.Valor.Valor)
            : null;

        return new BlocoDeMemoriaViewModel(
            $"…+{regioesMenores.Count} regiões menores",
            extensaoTotal,
            residenteTotal,
            emSwapTotal,
            aoSelecionar);
    }

    private static string TextoPadraoDoTipo(TipoDeRegiao tipo) => tipo switch
    {
        TipoDeRegiao.Codigo => "Código (.text)",
        TipoDeRegiao.DadosEstaticos => "Dados estáticos",
        TipoDeRegiao.Heap => "Heap",
        TipoDeRegiao.Pilha => "Pilha (stack)",
        TipoDeRegiao.BibliotecaCompartilhada => "Biblioteca compartilhada",
        TipoDeRegiao.ArquivoMapeado => "Arquivo mapeado",
        TipoDeRegiao.Reservada => "Reservado",
        _ => "Outra região"
    };

    [RelayCommand]
    private void Selecionar() => _aoSelecionar(this);
}
