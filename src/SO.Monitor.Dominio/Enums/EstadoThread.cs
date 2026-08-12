namespace SO.Monitor.Dominio.Enums;

/// <summary>
/// Estados possíveis de uma thread — a mesma máquina de estados de um processo (Tanenbaum),
/// só que aplicada à unidade de execução que o escalonador de fato despacha para a CPU.
/// </summary>
public enum EstadoThread
{
    /// <summary>Pronta para executar, aguardando a vez de usar a CPU.</summary>
    Pronta,

    /// <summary>Executando ativamente em um núcleo de CPU no instante da amostra.</summary>
    EmExecucao,

    /// <summary>Bloqueada aguardando um evento — ver <see cref="MotivoDeBloqueio"/> para o porquê.</summary>
    Bloqueada,

    /// <summary>Encerrada; não será mais escalonada.</summary>
    Terminada,

    /// <summary>O monitor não conseguiu determinar o estado desta thread nesta amostra.</summary>
    Desconhecido
}
