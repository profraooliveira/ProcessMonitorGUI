namespace SO.Monitor.Dominio.ValueObjects;

/// <summary>
/// Identificador de processo (PID) — a chave que o sistema operacional usa para localizar
/// o Bloco de Controle de Processo (PCB) correspondente na tabela de processos, conforme
/// o modelo de gerência de processos descrito por Tanenbaum.
/// </summary>
public readonly record struct Pid
{
    /// <summary>Valor numérico do identificador, atribuído pelo sistema operacional.</summary>
    public int Valor { get; }

    public Pid(int valor)
    {
        if (valor < 0)
            throw new ArgumentOutOfRangeException(nameof(valor), valor, "PID não pode ser negativo.");

        Valor = valor;
    }

    public override string ToString() => Valor.ToString();
}
