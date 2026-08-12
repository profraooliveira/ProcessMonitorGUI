namespace SO.Monitor.Dominio.ValueObjects;

/// <summary>
/// Um intervalo contíguo no espaço de endereçamento virtual de um processo — o formato em que
/// o SO expõe mapeamentos de memória (ex.: /proc/&lt;pid&gt;/maps no Linux, vmmap no macOS).
/// O intervalo é semiaberto [Início, Fim): Fim é o primeiro endereço que já não pertence à região.
/// </summary>
public readonly record struct FaixaDeEnderecos
{
    public EnderecoVirtual Inicio { get; }

    public EnderecoVirtual Fim { get; }

    public FaixaDeEnderecos(EnderecoVirtual inicio, EnderecoVirtual fim)
    {
        if (fim < inicio)
            throw new ArgumentOutOfRangeException(nameof(fim), fim, "O fim da faixa não pode ser anterior ao início.");

        Inicio = inicio;
        Fim = fim;
    }

    /// <summary>Extensão da faixa em bytes (Fim é exclusivo, então Extensao = Fim - Início).</summary>
    public TamanhoBytes Extensao => new(unchecked((long)(Fim.Valor - Inicio.Valor)));

    public override string ToString() => $"{Inicio}-{Fim}";
}
