namespace SO.Monitor.Infraestrutura;

/// <summary>
/// Serializa execuções concorrentes de uma operação potencialmente lenta com um
/// <see cref="SemaphoreSlim"/>(1,1): defesa em profundidade contra violações da premissa de uso
/// sequencial documentada em <see cref="FonteDeProcessosDotNet"/> — nunca duas execuções da
/// operação embrulhada rodam ao mesmo tempo nesta instância, mesmo que dois chamadores a invoquem
/// concorrentemente por engano. Não é uma mudança de contrato (a premissa de chamadas sequenciais
/// continua sendo o uso previsto), é uma rede de segurança. Extraído como tipo próprio, em vez de
/// embutido no adapter, para ser testável isoladamente sem depender de
/// <see cref="System.Diagnostics.Process.GetProcesses"/> real.
/// </summary>
internal sealed class ExecucaoSerializada<T>(Func<CancellationToken, ValueTask<T>> operacao) : IDisposable
{
    private readonly SemaphoreSlim _semaforo = new(1, 1);

    public async ValueTask<T> ExecutarAsync(CancellationToken cancellationToken)
    {
        await _semaforo.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operacao(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaforo.Release();
        }
    }

    public void Dispose() => _semaforo.Dispose();
}
