using System.Globalization;
using Avalonia.Data.Converters;

namespace MonitorGUI.Converters;

/// <summary>
/// Converte um valor nulo em um marcador textual de indisponibilidade: "—" por padrão, ou o
/// texto informado em <c>ConverterParameter</c> (ex.: "🔒" para dados protegidos pelo kernel).
/// Valores não nulos passam por <see cref="object.ToString"/> sem alteração.
/// </summary>
public sealed class NuloParaTextoConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            null => parameter as string ?? "—",
            _ => value.ToString() ?? "—"
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("NuloParaTextoConverter só converte em uma direção (exibição).");
}
