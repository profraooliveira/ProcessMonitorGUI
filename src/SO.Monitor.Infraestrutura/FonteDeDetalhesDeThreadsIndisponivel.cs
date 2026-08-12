using SO.Monitor.Aplicacao.Ports;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Infraestrutura;

/// <summary>
/// Implementação nula (Null Object) de <see cref="IFonteDeDetalhesDeThreads"/> para plataformas
/// ainda sem provedor fino de threads: devolve sempre "não suportado", e o chamador usa as
/// threads já presentes na amostra do baseline.
/// </summary>
public sealed class FonteDeDetalhesDeThreadsIndisponivel : IFonteDeDetalhesDeThreads
{
    /// <inheritdoc />
    public ValueTask<Leitura<IReadOnlyList<ThreadDoProcesso>>> ObterThreadsAsync(Pid pid, CancellationToken cancellationToken)
        => ValueTask.FromResult(Leitura<IReadOnlyList<ThreadDoProcesso>>.NaoSuportada());
}
