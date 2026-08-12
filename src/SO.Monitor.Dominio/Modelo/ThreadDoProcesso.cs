using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Dominio.Modelo;

/// <summary>
/// Uma thread dentro de um processo — a unidade de execução que o escalonador de fato
/// despacha para a CPU (Tanenbaum). Threads do mesmo processo compartilham o espaço de
/// endereçamento, mas cada uma mantém sua própria pilha, contador de programa e estado.
/// </summary>
/// <param name="TidEhPosicional">
/// Quando <c>true</c>, <see cref="Tid"/> não é o identificador de thread real atribuído pelo
/// kernel: é apenas a posição ordinal da thread na saída da ferramenta que a coletou (ex.: o
/// <c>ps</c> do macOS não expõe TIDs de kernel — ver <c>AnalisadorDeSaidaDoPs</c>). A
/// Apresentação usa esta flag para nunca exibir um TID posicional como se fosse um identificador
/// de sistema confiável. <c>false</c> (padrão) para todo TID real, incluindo o baseline .NET e o
/// <c>/proc/&lt;pid&gt;/task</c> do Linux, que expõem TIDs de kernel de verdade.
/// </param>
public sealed record ThreadDoProcesso(
    Tid Tid,
    EstadoThread EstadoThread,
    Leitura<MotivoDeBloqueio> MotivoDeBloqueio,
    Leitura<int> PrioridadeBase,
    Leitura<TimeSpan> TempoDeCpu,
    bool TidEhPosicional = false);
