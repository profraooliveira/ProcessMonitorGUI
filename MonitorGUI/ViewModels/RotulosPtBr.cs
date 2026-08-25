using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;

namespace MonitorGUI.ViewModels;

/// <summary>
/// Traduções pt-BR didáticas dos enums do domínio para exibição na interface. Vive na camada de
/// Apresentação de propósito: o domínio (Tanenbaum em código) é deliberadamente livre de qualquer
/// preocupação de UI — é este tipo quem decide como um <see cref="EstadoThread.Bloqueada"/>, por
/// exemplo, vira texto para o usuário final.
/// </summary>
public static class RotulosPtBr
{
    public static string Texto(EstadoProcesso estado) => estado switch
    {
        EstadoProcesso.Novo => "Novo",
        EstadoProcesso.Pronto => "Pronto",
        EstadoProcesso.EmExecucao => "Executando",
        EstadoProcesso.Bloqueado => "Bloqueado",
        EstadoProcesso.Terminado => "Terminado",
        _ => "Desconhecido"
    };

    public static string Texto(EstadoThread estado) => estado switch
    {
        EstadoThread.Pronta => "Pronta",
        EstadoThread.EmExecucao => "Executando",
        EstadoThread.Bloqueada => "Bloqueada",
        EstadoThread.Terminada => "Terminada",
        _ => "Desconhecido"
    };

    public static string Texto(PerfilDeExecucao perfil) => perfil switch
    {
        PerfilDeExecucao.LimitadoPorCpu => "CPU-bound",
        PerfilDeExecucao.LimitadoPorES => "E/S-bound",
        _ => "Indeterminado"
    };

    /// <summary>Rótulo curto de um tipo de região de memória (ex.: "Heap", "Pilha (stack)") — usado como nome de exibição do bloco.</summary>
    public static string Texto(TipoDeRegiao tipo) => tipo switch
    {
        TipoDeRegiao.Codigo => "Código (.text)",
        TipoDeRegiao.DadosEstaticos => "Dados estáticos",
        TipoDeRegiao.Heap => "Heap",
        TipoDeRegiao.Pilha => "Pilha (stack)",
        TipoDeRegiao.BibliotecaCompartilhada => "Biblioteca compartilhada",
        TipoDeRegiao.ArquivoMapeado => "Arquivo mapeado",
        TipoDeRegiao.Reservada => "Reservado",
        _ => "Outra região"
    };

    /// <summary>
    /// Explicação didática (1-2 frases, estilo Tanenbaum) do que um tipo de região de memória
    /// representa e por que importa — usada na tooltip do bloco e no painel de detalhes, expande
    /// a doc XML já existente em <see cref="TipoDeRegiao"/> com o "porquê" prático de cada uma.
    /// </summary>
    public static string TextoDidatico(TipoDeRegiao tipo) => tipo switch
    {
        TipoDeRegiao.Codigo =>
            "Instruções executáveis do programa, tipicamente somente leitura e execução — o SO compartilha essas páginas entre processos que rodam o mesmo binário.",
        TipoDeRegiao.DadosEstaticos =>
            "Variáveis globais e estáticas, inicializadas pelo carregador antes do main() começar — existem durante toda a vida do processo, ao contrário do heap e da pilha.",
        TipoDeRegiao.Heap =>
            "Memória alocada dinamicamente em tempo de execução (malloc/new). Cresce sob demanda em direção à pilha; memória liberada pelo programa costuma continuar mapeada no processo, então residência não é o mesmo que liveness.",
        TipoDeRegiao.Pilha =>
            "Pilha de execução (stack): quadros de chamada, variáveis locais e endereço de retorno de cada função. Cada thread do processo tem a sua própria.",
        TipoDeRegiao.BibliotecaCompartilhada =>
            "Biblioteca dinâmica (.so/.dylib/.dll) mapeada no espaço de endereçamento — o código é compartilhado entre processos que a usam; só os dados privados de cada um ocupam memória à parte.",
        TipoDeRegiao.ArquivoMapeado =>
            "Arquivo em disco mapeado diretamente na memória virtual (memory-mapped file) — o SO carrega páginas sob demanda conforme são acessadas, em vez de ler o arquivo inteiro de uma vez.",
        TipoDeRegiao.Reservada =>
            "Espaço de endereçamento reservado, mas ainda não comprometido (committed) a nenhum conteúdo — não ocupa RAM nem swap até que o processo de fato o use.",
        _ => "Região cujo propósito não se encaixa nas categorias didáticas acima."
    };

    /// <summary>
    /// Texto didático do motivo de bloqueio de uma thread. Uma thread só tem motivo quando está
    /// de fato <see cref="EstadoThread.Bloqueada"/>; fora disso "—" (não se aplica). Esse curto-
    /// circuito por <paramref name="estado"/> continua aqui como defesa (não é redundante):
    /// embora os produtores de <see cref="ThreadDoProcesso"/> na Infraestrutura já devolvam
    /// <see cref="Disponibilidade.NaoSeAplica"/> quando a thread não está bloqueada, fontes de
    /// dados de design-time/teste nem sempre seguem essa mesma disciplina — o curto-circuito
    /// garante "—" de qualquer forma. Quando bloqueada, o motivo pode falhar em dois níveis
    /// diferentes: a própria <see cref="Leitura{T}"/> pode ter sido negada pelo kernel (ex.:
    /// AcessoNegado), ou a leitura pode ter sucesso e ainda assim carregar o valor de primeira
    /// classe <see cref="MotivoDeBloqueio.NaoObservavelNestaPlataforma"/> — os dois casos são
    /// tratados aqui para nunca deixar a interface muda sobre o porquê.
    /// </summary>
    public static string TextoMotivoDeBloqueio(Leitura<MotivoDeBloqueio> leitura, EstadoThread estado)
    {
        if (estado != EstadoThread.Bloqueada)
            return "—";

        if (!leitura.TemValor)
        {
            return leitura.Estado switch
            {
                Disponibilidade.AcessoNegado => "🔒 acesso negado",
                Disponibilidade.NaoSuportadoNaPlataforma => "não observável nesta plataforma",
                Disponibilidade.NaoSeAplica => "—",
                Disponibilidade.ProcessoEncerrado => "processo encerrado",
                _ => "motivo desconhecido"
            };
        }

        return leitura.Valor switch
        {
            MotivoDeBloqueio.EsperandoES => "esperando E/S",
            MotivoDeBloqueio.EsperandoPagina => "esperando página (page fault)",
            MotivoDeBloqueio.EsperandoSincronizacao => "esperando sincronização",
            MotivoDeBloqueio.EsperandoEvento => "esperando evento",
            MotivoDeBloqueio.Suspensa => "suspensa",
            MotivoDeBloqueio.NaoObservavelNestaPlataforma => "não observável nesta plataforma",
            _ => "motivo desconhecido"
        };
    }

    /// <summary>
    /// Formata o TID para exibição. TIDs reais (kernel) aparecem como o número puro; TIDs
    /// posicionais (<see cref="ThreadDoProcesso.TidEhPosicional"/> — hoje só o <c>ps</c> do
    /// macOS) ganham o prefixo "#" e o sufixo "(posicional)" para nunca serem confundidos com um
    /// identificador de sistema real.
    /// </summary>
    public static string TextoTid(long tid, bool tidEhPosicional) =>
        tidEhPosicional ? $"#{tid} (posicional)" : tid.ToString();

    /// <summary>Tooltip que explica por que um TID é posicional — só preenchido quando ele de fato é.</summary>
    public static string? TooltipTid(bool tidEhPosicional) => tidEhPosicional
        ? "O ps do macOS não expõe TIDs de kernel; este número é apenas a posição da thread na saída do comando, não um identificador real do sistema operacional."
        : null;

    /// <summary>Descreve, em texto didático, por que o mapa de memória real não pôde ser lido.</summary>
    public static string DescreverFallbackDeMapa(Disponibilidade motivo) => motivo switch
    {
        Disponibilidade.AcessoNegado => "acesso negado pelo kernel — processo protegido",
        Disponibilidade.NaoSuportadoNaPlataforma => "leitura de mapa de memória não suportada nesta plataforma",
        Disponibilidade.ProcessoEncerrado => "processo encerrado antes da leitura",
        _ => "motivo desconhecido"
    };

    /// <summary>
    /// Rótulo obrigatório de origem do mapa de memória: a interface nunca pode apresentar um
    /// mapa simulado como se fosse uma medição real do sistema operacional.
    /// </summary>
    public static string RotuloDeOrigemDoMapa(OrigemDosDados origem) => origem switch
    {
        OrigemDosDados.MedidaReal => "MAPA DE MEMÓRIA REAL (medido do SO)",
        _ => "MAPA SIMULADO (modo didático)"
    };
}
