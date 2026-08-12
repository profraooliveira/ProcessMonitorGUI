namespace SO.Monitor.Dominio.Enums;

/// <summary>
/// Por que uma thread está bloqueada — o motivo concreto por trás do estado
/// <see cref="EstadoThread.Bloqueada"/>, que Tanenbaum trata como uma transição disparada por
/// um evento específico, nunca por acaso.
/// </summary>
public enum MotivoDeBloqueio
{
    /// <summary>Esperando a conclusão de uma operação de entrada/saída (disco, rede, etc.).</summary>
    EsperandoES,

    /// <summary>Esperando o sistema de memória virtual trazer uma página para a RAM (page fault).</summary>
    EsperandoPagina,

    /// <summary>Esperando um mecanismo de sincronização (mutex, semáforo, lock) ser liberado.</summary>
    EsperandoSincronizacao,

    /// <summary>Esperando um evento arbitrário sinalizado por outra thread ou processo.</summary>
    EsperandoEvento,

    /// <summary>Suspensa deliberadamente (ex.: SIGSTOP), sem estar esperando um evento específico.</summary>
    Suspensa,

    /// <summary>A thread está bloqueada, mas o motivo não pôde ser determinado.</summary>
    Desconhecido,

    /// <summary>
    /// Caso de primeira classe (Null Object): a plataforma atual não expõe o motivo de bloqueio
    /// de forma alguma (ex.: kernel do macOS restringe o acesso). Não é um erro nem um "zero" —
    /// é um fato observável e exibível: "aqui, o SO simplesmente não conta essa história".
    /// </summary>
    NaoObservavelNestaPlataforma
}
