namespace SO.Monitor.Dominio.ValueObjects;

/// <summary>
/// Identificador de thread (TID) dentro de um processo. Tanenbaum descreve a thread como a
/// unidade de execução que compartilha o espaço de endereçamento do processo dono; o TID é
/// o que diferencia uma thread das demais dentro desse mesmo processo.
/// </summary>
public readonly record struct Tid
{
    /// <summary>Valor numérico do identificador, atribuído pelo sistema operacional.</summary>
    public long Valor { get; }

    public Tid(long valor)
    {
        if (valor < 0)
            throw new ArgumentOutOfRangeException(nameof(valor), valor, "TID não pode ser negativo.");

        Valor = valor;
    }

    public override string ToString() => Valor.ToString();
}
