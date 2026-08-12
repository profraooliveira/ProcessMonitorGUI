using SO.Monitor.Dominio.Enums;

namespace SO.Monitor.Infraestrutura.Windows;

/// <summary>
/// Classifica uma região de memória do Windows a partir dos três campos crus que
/// <c>VirtualQueryEx</c> devolve em <c>MEMORY_BASIC_INFORMATION</c> — lógica pura, sem nenhuma
/// chamada ao sistema operacional (por isso vive separada de <see cref="NativoWin32"/>), o que
/// permite testá-la em qualquer plataforma, inclusive fora do Windows, onde o P/Invoke real nunca
/// rodaria.
/// </summary>
internal static class ClassificadorDeRegiaoWindows
{
    // MEMORY_BASIC_INFORMATION.Type (winnt.h)
    internal const uint MEM_IMAGE = 0x1000000;
    internal const uint MEM_MAPPED = 0x40000;
    internal const uint MEM_PRIVATE = 0x20000;

    // MEMORY_BASIC_INFORMATION.State (winnt.h)
    internal const uint MEM_COMMIT = 0x1000;
    internal const uint MEM_RESERVE = 0x2000;
    internal const uint MEM_FREE = 0x10000;

    // MEMORY_BASIC_INFORMATION.Protect (winnt.h) — os PAGE_* "base"; GUARD/NOCACHE/WRITECOMBINE
    // são modificadores combinados via OR com um dos valores acima.
    internal const uint PAGE_NOACCESS = 0x01;
    internal const uint PAGE_READONLY = 0x02;
    internal const uint PAGE_READWRITE = 0x04;
    internal const uint PAGE_WRITECOPY = 0x08;
    internal const uint PAGE_EXECUTE = 0x10;
    internal const uint PAGE_EXECUTE_READ = 0x20;
    internal const uint PAGE_EXECUTE_READWRITE = 0x40;
    internal const uint PAGE_EXECUTE_WRITECOPY = 0x80;
    internal const uint PAGE_GUARD = 0x100;
    internal const uint PAGE_NOCACHE = 0x200;
    internal const uint PAGE_WRITECOMBINE = 0x400;

    /// <summary>Máscara dos 8 bits baixos de Protect: isola o PAGE_* "base", removendo GUARD/NOCACHE/WRITECOMBINE.</summary>
    private const uint MascaraDeProtecaoBase = 0xFF;

    /// <summary>
    /// Classifica a região e formata suas permissões no mesmo estilo "rwx" usado no restante do
    /// domínio (inspirado em <c>/proc/&lt;pid&gt;/maps</c> do Linux). Quando a página é protegida
    /// por <see cref="PAGE_GUARD"/> (o mecanismo clássico de detecção de estouro de pilha —
    /// Tanenbaum), o rótulo de saída vem marcado com "(guard)".
    /// </summary>
    public static (TipoDeRegiao Tipo, string Permissoes, string? RotuloAdicional) Classificar(uint tipo, uint estado, uint protect)
    {
        var protecaoBase = protect & MascaraDeProtecaoBase;
        var ehGuarda = (protect & PAGE_GUARD) != 0;

        var tipoDeRegiao = ClassificarTipo(tipo, estado, protecaoBase);
        var permissoes = FormatarPermissoes(protecaoBase);
        var rotulo = ehGuarda ? "(guard)" : null;

        return (tipoDeRegiao, permissoes, rotulo);
    }

    private static TipoDeRegiao ClassificarTipo(uint tipo, uint estado, uint protecaoBase) => (tipo, estado) switch
    {
        _ when estado == MEM_FREE => TipoDeRegiao.Outra, // espaço de endereçamento sequer reservado ainda
        (MEM_IMAGE, _) when EhProtecaoExecutavel(protecaoBase) => TipoDeRegiao.Codigo,
        (MEM_IMAGE, _) => TipoDeRegiao.BibliotecaCompartilhada,
        (MEM_MAPPED, _) => TipoDeRegiao.ArquivoMapeado,
        (MEM_PRIVATE, MEM_COMMIT) => TipoDeRegiao.Heap,
        _ when estado == MEM_RESERVE => TipoDeRegiao.Reservada,
        _ => TipoDeRegiao.Outra
    };

    private static bool EhProtecaoExecutavel(uint protecaoBase) =>
        protecaoBase is PAGE_EXECUTE or PAGE_EXECUTE_READ or PAGE_EXECUTE_READWRITE or PAGE_EXECUTE_WRITECOPY;

    private static string FormatarPermissoes(uint protecaoBase)
    {
        var (leitura, escrita, execucao) = protecaoBase switch
        {
            PAGE_NOACCESS => (false, false, false),
            PAGE_READONLY => (true, false, false),
            PAGE_READWRITE => (true, true, false),
            PAGE_WRITECOPY => (true, true, false),
            PAGE_EXECUTE => (false, false, true),
            PAGE_EXECUTE_READ => (true, false, true),
            PAGE_EXECUTE_READWRITE => (true, true, true),
            PAGE_EXECUTE_WRITECOPY => (true, true, true),
            _ => (false, false, false)
        };

        return $"{(leitura ? 'r' : '-')}{(escrita ? 'w' : '-')}{(execucao ? 'x' : '-')}";
    }
}
