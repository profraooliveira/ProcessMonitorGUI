namespace SO.Monitor.Dominio.ValueObjects;

/// <summary>
/// Uma quantidade de memória em bytes — a unidade "bruta" de medida, antes de traduzida para
/// páginas, working set ou qualquer outra abstração de gerência de memória.
/// </summary>
public readonly record struct TamanhoBytes
{
    /// <summary>Quantidade de bytes.</summary>
    public long Valor { get; }

    public TamanhoBytes(long valor)
    {
        if (valor < 0)
            throw new ArgumentOutOfRangeException(nameof(valor), valor, "Tamanho em bytes não pode ser negativo.");

        Valor = valor;
    }

    /// <summary>O tamanho zero, útil como valor inicial de somas.</summary>
    public static TamanhoBytes Zero => new(0);

    /// <summary>
    /// Converte o tamanho em número de páginas, arredondando para cima: mesmo 1 byte além de
    /// um limite de página já consome a página inteira (fragmentação interna — Tanenbaum).
    /// </summary>
    public long EmPaginas(TamanhoPagina pagina)
    {
        var (quociente, resto) = Math.DivRem(Valor, pagina.EmBytes);
        return resto == 0 ? quociente : quociente + 1;
    }

    public static TamanhoBytes operator +(TamanhoBytes esquerda, TamanhoBytes direita) =>
        new(esquerda.Valor + direita.Valor);

    public static TamanhoBytes operator -(TamanhoBytes esquerda, TamanhoBytes direita) =>
        new(esquerda.Valor - direita.Valor);

    public override string ToString() => $"{Valor} bytes";
}
