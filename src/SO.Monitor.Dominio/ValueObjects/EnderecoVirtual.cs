namespace SO.Monitor.Dominio.ValueObjects;

/// <summary>
/// Um endereço no espaço de endereçamento virtual de um processo — o endereço que o programa
/// enxerga, traduzido pela MMU/tabela de páginas para um endereço físico (Tanenbaum, memória
/// virtual). A representação canônica de um endereço é hexadecimal, não decimal.
/// </summary>
public readonly record struct EnderecoVirtual(ulong Valor) : IComparable<EnderecoVirtual>
{
    public override string ToString() => $"0x{Valor:X12}";

    public int CompareTo(EnderecoVirtual outro) => Valor.CompareTo(outro.Valor);

    public static bool operator <(EnderecoVirtual esquerda, EnderecoVirtual direita) => esquerda.Valor < direita.Valor;

    public static bool operator >(EnderecoVirtual esquerda, EnderecoVirtual direita) => esquerda.Valor > direita.Valor;

    public static bool operator <=(EnderecoVirtual esquerda, EnderecoVirtual direita) => esquerda.Valor <= direita.Valor;

    public static bool operator >=(EnderecoVirtual esquerda, EnderecoVirtual direita) => esquerda.Valor >= direita.Valor;
}
