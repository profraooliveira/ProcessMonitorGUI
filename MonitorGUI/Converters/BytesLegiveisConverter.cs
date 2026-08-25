using System.Globalization;
using Avalonia.Data.Converters;

namespace MonitorGUI.Converters;

/// <summary>
/// Formata uma quantidade de bytes (<c>long?</c>) em texto legível ("12,3 MB"), em pt-BR. As
/// colunas de memória sempre fazem binding no valor numérico bruto — para ordenação correta na
/// DataGrid — e este conversor cuida apenas da exibição.
/// </summary>
public sealed class BytesLegiveisConverter : IValueConverter
{
    private static readonly string[] Sufixos = ["B", "KB", "MB", "GB", "TB"];
    private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Lógica de formatação pura, reaproveitada por quem precisa do texto fora de um binding XAML (ex.: <see cref="MonitorGUI.ViewModels.BlocoDeMemoriaViewModel.TooltipDidatico"/>).</summary>
    public static string Formatar(long? bytes)
    {
        if (bytes is null)
            return "—";

        var tamanho = (double)bytes.Value;
        var indice = 0;
        while (tamanho >= 1024 && indice < Sufixos.Length - 1)
        {
            tamanho /= 1024;
            indice++;
        }

        return $"{tamanho.ToString("N1", CulturaPtBr)} {Sufixos[indice]}";
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        long? bytes = value switch
        {
            long valor => valor,
            int valor => valor,
            double valor => (long)valor,
            _ => null
        };

        return Formatar(bytes);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("BytesLegiveisConverter só converte em uma direção (exibição).");
}
