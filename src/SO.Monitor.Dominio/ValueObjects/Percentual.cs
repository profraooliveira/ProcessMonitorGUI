namespace SO.Monitor.Dominio.ValueObjects;

/// <summary>
/// Um percentual de utilização — tipicamente de CPU. Não é limitado a 0..100: um processo
/// multithread pode consumir mais de 100% somando o tempo de vários núcleos simultaneamente,
/// exatamente como ferramentas como top/htop reportam.
/// </summary>
public readonly record struct Percentual
{
    /// <summary>Valor percentual (ex.: 12.5 representa 12,5%).</summary>
    public double Valor { get; }

    public Percentual(double valor)
    {
        if (!double.IsFinite(valor) || valor < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(valor), valor, "Percentual deve ser um valor finito e não negativo.");
        }

        Valor = valor;
    }

    /// <summary>O percentual zero.</summary>
    public static Percentual Zero => new(0);

    public override string ToString() => $"{Valor:F1}%";
}
