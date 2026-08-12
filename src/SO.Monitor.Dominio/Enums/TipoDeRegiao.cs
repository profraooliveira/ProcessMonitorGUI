namespace SO.Monitor.Dominio.Enums;

/// <summary>
/// O propósito de uma região do espaço de endereçamento virtual de um processo — a mesma
/// divisão (código, dados, heap, pilha...) que Tanenbaum usa para explicar como um programa
/// em execução organiza sua memória.
/// </summary>
public enum TipoDeRegiao
{
    /// <summary>Segmento de texto/código executável (tipicamente somente leitura e execução).</summary>
    Codigo,

    /// <summary>Dados estáticos e globais inicializados pelo carregador do programa.</summary>
    DadosEstaticos,

    /// <summary>Heap — memória alocada dinamicamente em tempo de execução (malloc/new).</summary>
    Heap,

    /// <summary>Pilha de execução (stack) — quadros de chamada, variáveis locais, retorno.</summary>
    Pilha,

    /// <summary>Biblioteca compartilhada (.so/.dylib/.dll) mapeada no espaço de endereçamento.</summary>
    BibliotecaCompartilhada,

    /// <summary>Arquivo mapeado em memória (memory-mapped file).</summary>
    ArquivoMapeado,

    /// <summary>Espaço de endereçamento reservado, mas ainda não mapeado a nenhum conteúdo.</summary>
    Reservada,

    /// <summary>Região cujo propósito não se encaixa nas demais categorias.</summary>
    Outra
}
