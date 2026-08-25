using System.Runtime.CompilerServices;
using SO.Monitor.Dominio.Modelo;

namespace SO.Monitor.Aplicacao.CasosDeUso;

/// <summary>
/// Observa o sistema periodicamente, emitindo uma <see cref="AmostraDoSistema"/> classificada a
/// cada intervalo — o "polling loop" que transforma fotografias isoladas em um fluxo contínuo,
/// como top/htop em modo de atualização automática. Usa <see cref="PeriodicTimer"/> porque
/// async/await já basta aqui: não há necessidade de Channels nem de threads dedicadas para um
/// laço simples de "espera e coleta".
/// </summary>
public sealed class MonitorDeProcessos(ObterAmostraClassificada obterAmostra)
{
    /// <summary>
    /// Observa o sistema a cada <paramref name="intervalo"/>, emitindo uma amostra imediatamente
    /// e depois a cada disparo do temporizador, até o cancelamento. O cancelamento é sempre
    /// limpo: nenhuma <see cref="OperationCanceledException"/> escapa para quem consome o fluxo
    /// — o enumerador simplesmente termina (<c>yield break</c>).
    /// <para/>
    /// <paramref name="obterCriterios"/> é um PROVEDOR (chamado a cada tick), não um valor
    /// congelado no início do stream: o chamador tipicamente deriva os critérios de estado de UI
    /// que muda durante o próprio stream (ex.: processo selecionado, para o "pin" descrito em
    /// <see cref="CriteriosDeAmostragem.PidFixado"/>) — congelar os critérios na criação do
    /// stream faria uma troca de seleção só valer a partir do PRÓXIMO reinício do laço.
    /// </summary>
    public async IAsyncEnumerable<AmostraDoSistema> ObservarAsync(
        TimeSpan intervalo,
        Func<CriteriosDeAmostragem> obterCriterios,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var primeiraAmostra = await TentarObterAmostraAsync(obterCriterios(), cancellationToken).ConfigureAwait(false);
        if (primeiraAmostra is null)
            yield break;

        yield return primeiraAmostra;

        using var temporizador = new PeriodicTimer(intervalo);

        while (await TentarAguardarProximoTickAsync(temporizador, cancellationToken).ConfigureAwait(false))
        {
            var amostra = await TentarObterAmostraAsync(obterCriterios(), cancellationToken).ConfigureAwait(false);
            if (amostra is null)
                yield break;

            yield return amostra;
        }
    }

    // A linguagem não permite `yield` dentro de um bloco try/catch com catch (apenas dentro de
    // try/finally). Por isso a captura de OperationCanceledException fica isolada nestes
    // métodos auxiliares, que devolvem "não há mais nada" em vez de deixar a exceção escapar.
    private async ValueTask<AmostraDoSistema?> TentarObterAmostraAsync(
        CriteriosDeAmostragem criterios,
        CancellationToken cancellationToken)
    {
        try
        {
            return await obterAmostra.ExecutarAsync(criterios, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static async ValueTask<bool> TentarAguardarProximoTickAsync(
        PeriodicTimer temporizador,
        CancellationToken cancellationToken)
    {
        try
        {
            return await temporizador.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
