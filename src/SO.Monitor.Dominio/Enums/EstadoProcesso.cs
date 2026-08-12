namespace SO.Monitor.Dominio.Enums;

/// <summary>
/// O modelo de estados de processo de Tanenbaum: todo processo transita entre estes estados
/// ao longo de sua vida, movido pelo escalonador e por eventos como pedidos de E/S e término.
/// </summary>
public enum EstadoProcesso
{
    /// <summary>Processo recém-criado, ainda em admissão pelo sistema operacional.</summary>
    Novo,

    /// <summary>Pronto para executar, aguardando apenas a vez de usar a CPU (fila de prontos).</summary>
    Pronto,

    /// <summary>Executando ativamente em um núcleo de CPU no instante da amostra.</summary>
    EmExecucao,

    /// <summary>Bloqueado aguardando um evento (E/S, sincronização, página) — não progride sem ele.</summary>
    Bloqueado,

    /// <summary>Encerrado; liberou, ou está liberando, seus recursos junto ao sistema operacional.</summary>
    Terminado,

    /// <summary>O monitor não conseguiu determinar o estado deste processo nesta amostra.</summary>
    Desconhecido
}
