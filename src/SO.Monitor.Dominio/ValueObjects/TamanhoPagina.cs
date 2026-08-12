namespace SO.Monitor.Dominio.ValueObjects;

/// <summary>
/// Tamanho da página de memória usada pelo sistema operacional na tradução de endereços
/// virtuais em físicos (paginação — Tanenbaum, capítulo de Gerência de Memória). Precisa ser
/// uma potência de 2: é isso que permite ao hardware (MMU) separar um endereço em número de
/// página e deslocamento apenas com máscaras de bits, sem divisão.
/// </summary>
public readonly record struct TamanhoPagina
{
    /// <summary>Tamanho da página, em bytes.</summary>
    public int EmBytes { get; }

    public TamanhoPagina(int emBytes)
    {
        if (emBytes <= 0 || (emBytes & (emBytes - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(emBytes), emBytes, "Tamanho de página deve ser uma potência de 2 positiva.");
        }

        EmBytes = emBytes;
    }

    /// <summary>Página de 4 KiB — o tamanho histórico em x86/x86-64 e na maioria dos SOs de propósito geral.</summary>
    public static TamanhoPagina Kib4 => new(4096);

    /// <summary>Página de 16 KiB — usada pelo macOS/iOS em processadores Apple Silicon (ARM64).</summary>
    public static TamanhoPagina Kib16 => new(16384);

    public override string ToString() => $"{EmBytes} bytes";
}
