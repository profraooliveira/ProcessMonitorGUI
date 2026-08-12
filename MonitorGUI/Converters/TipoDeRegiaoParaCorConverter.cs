using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SO.Monitor.Dominio.Enums;

namespace MonitorGUI.Converters;

/// <summary>
/// Mapeia o tipo de uma região de memória (<see cref="TipoDeRegiao"/>) para a cor correspondente
/// no mapa de memória, buscando o pincel nomeado em <c>App.axaml</c> — as cores em si vivem como
/// recursos da aplicação (reutilizáveis, por exemplo, na legenda de cores do mapa), não
/// hardcoded neste conversor.
/// </summary>
public sealed class TipoDeRegiaoParaCorConverter : IValueConverter
{
    private static readonly IReadOnlyDictionary<TipoDeRegiao, string> ChaveDoRecursoPorTipo = new Dictionary<TipoDeRegiao, string>
    {
        [TipoDeRegiao.Codigo] = "CorRegiaoCodigo",
        [TipoDeRegiao.DadosEstaticos] = "CorRegiaoDados",
        [TipoDeRegiao.Heap] = "CorRegiaoHeap",
        [TipoDeRegiao.Pilha] = "CorRegiaoPilha",
        [TipoDeRegiao.BibliotecaCompartilhada] = "CorRegiaoBiblioteca",
        [TipoDeRegiao.ArquivoMapeado] = "CorRegiaoArquivoMapeado",
        [TipoDeRegiao.Reservada] = "CorRegiaoReservada",
        [TipoDeRegiao.Outra] = "CorRegiaoOutra"
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var chave = value is TipoDeRegiao tipo && ChaveDoRecursoPorTipo.TryGetValue(tipo, out var nomeRecurso)
            ? nomeRecurso
            : "CorRegiaoOutra";

        if (Application.Current is { } aplicacao && aplicacao.TryFindResource(chave, out var recurso) && recurso is IBrush pincel)
            return pincel;

        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("TipoDeRegiaoParaCorConverter só converte em uma direção (exibição).");
}
