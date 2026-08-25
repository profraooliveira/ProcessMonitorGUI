using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonitorGUI.Converters;
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

    /// <summary>Faixa de endereços reais em hexadecimal — chave de reconciliação entre atualizações do mapa (ver <see cref="ReconciliadorDeBlocosDeMemoria"/>) além de texto do tooltip.</summary>
    public string FaixaHexTexto { get; }

    /// <summary>Verdadeiro quando este bloco representa o agregado "…+K regiões menores".</summary>
    public bool EhAgregado { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TooltipDidatico))]
    [NotifyPropertyChangedFor(nameof(TextoDidatico))]
    private TipoDeRegiao _tipoDeRegiao;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TooltipDidatico))]
    private string _rotulo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TooltipDidatico))]
    private string _permissoesTexto;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TooltipDidatico))]
    private long? _residenteBytes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TooltipDidatico))]
    private long? _emSwapBytes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TooltipDidatico))]
    private long _extensaoBytes;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Tooltip do bloco (rótulo + endereço + permissões + residência + explicação didática do
    /// tipo de região). Computada — não <c>[ObservableProperty]</c> — mas cada campo que a
    /// alimenta é anotado com <c>[NotifyPropertyChangedFor(nameof(TooltipDidatico))]</c>: sem
    /// isso, a tooltip congelaria no primeiro valor assim que o mapa passasse a se atualizar
    /// sozinho (o auto-refresh só muda os campos, nunca recria a instância — ver <see cref="AtualizarDe"/>).
    /// </summary>
    public string TooltipDidatico => EhAgregado
        ? $"{Rotulo}\nO mapa mostra individualmente só as maiores regiões por extensão — aumente \"Blocos\" para ver mais em detalhe."
        : $"{Rotulo}\n{FaixaHexTexto}\nPermissões: {PermissoesTexto} · Residente: {BytesLegiveisConverter.Formatar(ResidenteBytes)} · Em swap: {BytesLegiveisConverter.Formatar(EmSwapBytes)}\n{RotulosPtBr.TextoDidatico(TipoDeRegiao)}";

    /// <summary>Mesma explicação didática do tooltip, para o painel de detalhes (que tem mais espaço para o texto completo). A contagem de regiões agregadas já aparece em <see cref="Rotulo"/>, exibido acima deste texto no painel — não repetida aqui.</summary>
    public string TextoDidatico => EhAgregado
        ? "Regiões menores agregadas em um único bloco. O mapa exibe individualmente apenas as maiores por extensão — aumente \"Blocos\" acima para ver mais em detalhe."
        : RotulosPtBr.TextoDidatico(TipoDeRegiao);

    public BlocoDeMemoriaViewModel(RegiaoDeMemoria regiao, Action<BlocoDeMemoriaViewModel> aoSelecionar)
    {
        _aoSelecionar = aoSelecionar;
        _tipoDeRegiao = regiao.TipoDeRegiao;
        _rotulo = string.IsNullOrWhiteSpace(regiao.Rotulo) ? RotulosPtBr.Texto(regiao.TipoDeRegiao) : regiao.Rotulo!;
        _permissoesTexto = regiao.PermissoesTexto;
        FaixaHexTexto = regiao.FaixaDeEnderecos.ToString();
        _residenteBytes = regiao.Residente.TemValor ? regiao.Residente.Valor.Valor : null;
        _emSwapBytes = regiao.EmSwap.TemValor ? regiao.EmSwap.Valor.Valor : null;
        _extensaoBytes = regiao.FaixaDeEnderecos.Extensao.Valor;
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
        _tipoDeRegiao = TipoDeRegiao.Outra;
        _rotulo = rotulo;
        _permissoesTexto = "—";
        FaixaHexTexto = "múltiplas regiões agregadas";
        _residenteBytes = residenteBytes;
        _emSwapBytes = emSwapBytes;
        _extensaoBytes = extensaoBytes;
        EhAgregado = true;
    }

    /// <summary>
    /// Atualiza os valores a partir de um gêmeo recém-construído para a MESMA faixa de endereços
    /// (mesmo <see cref="FaixaHexTexto"/>) — a instância antiga é preservada (seleção, rolagem,
    /// foco na DataGrid), só os valores mudam. Cobre blocos reais e o agregado pelo mesmo caminho,
    /// já que ambos passam por este método via <see cref="ReconciliadorDeBlocosDeMemoria"/>.
    /// <see cref="IsSelected"/> é deliberadamente preservado, não copiado do gêmeo.
    /// </summary>
    public void AtualizarDe(BlocoDeMemoriaViewModel outro)
    {
        TipoDeRegiao = outro.TipoDeRegiao;
        Rotulo = outro.Rotulo;
        PermissoesTexto = outro.PermissoesTexto;
        ResidenteBytes = outro.ResidenteBytes;
        EmSwapBytes = outro.EmSwapBytes;
        ExtensaoBytes = outro.ExtensaoBytes;
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

    [RelayCommand]
    private void Selecionar() => _aoSelecionar(this);
}
