using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Dominio.Servicos;

/// <summary>
/// Gera um mapa de memória plausível — porém fictício — para quando o sistema operacional não
/// expõe o mapa real de um processo, ou puramente para fins de ensino. É uma "Pure Fabrication"
/// (GRASP): não existe no mundo real, existe para ensinar a forma de um mapa de memória sem
/// nunca mentir sobre a origem dos dados (todo mapa aqui gerado carrega
/// <see cref="OrigemDosDados.Simulada"/>). A semente é o PID: o mesmo processo sempre produz
/// exatamente o mesmo mapa, para a interface não "piscar" a cada ciclo de atualização.
/// </summary>
public static class SimuladorDePaginacao
{
    private sealed record Segmento(TipoDeRegiao Tipo, string Permissoes, string RotuloBase, double FracaoDoEspaco, bool SofrePaginacao);

    // Ordem intencional: código no início do espaço de endereçamento, como um executável
    // ELF/Mach-O recém-carregado; heap e bibliotecas ocupam a maior fatia do espaço virtual.
    private static readonly Segmento[] TemplateDeSegmentos =
    [
        new(TipoDeRegiao.Codigo, "r-x", "__TEXT", FracaoDoEspaco: 0.05, SofrePaginacao: false),
        new(TipoDeRegiao.DadosEstaticos, "rw-", "__DATA", FracaoDoEspaco: 0.03, SofrePaginacao: false),
        new(TipoDeRegiao.Heap, "rw-", "MALLOC_SMALL", FracaoDoEspaco: 0.35, SofrePaginacao: true),
        new(TipoDeRegiao.BibliotecaCompartilhada, "r-x", "", FracaoDoEspaco: 0.25, SofrePaginacao: true),
        new(TipoDeRegiao.ArquivoMapeado, "r--", "arquivo mapeado", FracaoDoEspaco: 0.10, SofrePaginacao: true),
        new(TipoDeRegiao.Pilha, "rw-", "pilha (stack)", FracaoDoEspaco: 0.02, SofrePaginacao: false)
    ];

    private static readonly string[] NomesDeBibliotecas =
    [
        "libsystem_kernel.dylib",
        "libobjc.A.dylib",
        "libc++.1.dylib",
        "CoreFoundation.framework",
        "libSystem.B.dylib"
    ];

    /// <summary>
    /// Simula um mapa de memória determinístico para o processo <paramref name="pid"/>: mesma
    /// entrada sempre produz o mesmo mapa (regiões, endereços, rótulos idênticos).
    /// </summary>
    public static MapaDeMemoria Simular(
        Pid pid,
        TamanhoBytes conjuntoResidente,
        TamanhoBytes memoriaVirtual,
        TamanhoPagina pagina,
        DateTimeOffset instante)
    {
        var aleatorio = new Random(pid.Valor);

        var razaoResidente = memoriaVirtual.Valor > 0
            ? Math.Clamp((double)conjuntoResidente.Valor / memoriaVirtual.Valor, 0.0, 1.0)
            : 0.0;

        var regioes = new List<RegiaoDeMemoria>(TemplateDeSegmentos.Length + 1);

        // A primeira página é deixada de fora: o endereço nulo (0x0) é convencionalmente
        // reservado/protegido, para que um ponteiro nulo desreferenciado sempre falhe.
        var proximoEndereco = (ulong)pagina.EmBytes;
        var bytesRestantes = memoriaVirtual.Valor;

        foreach (var segmento in TemplateDeSegmentos)
        {
            var tamanhoAlinhado = Math.Min(
                AlinharAPagina((long)(memoriaVirtual.Valor * segmento.FracaoDoEspaco), pagina),
                bytesRestantes);

            if (tamanhoAlinhado <= 0)
                continue;

            var rotulo = segmento.Tipo == TipoDeRegiao.BibliotecaCompartilhada
                ? NomesDeBibliotecas[aleatorio.Next(NomesDeBibliotecas.Length)]
                : segmento.RotuloBase;

            proximoEndereco = AdicionarRegiao(regioes, proximoEndereco, tamanhoAlinhado, segmento, rotulo, razaoResidente);
            bytesRestantes -= tamanhoAlinhado;
        }

        if (bytesRestantes > 0)
        {
            var reservada = new Segmento(TipoDeRegiao.Reservada, "---", "reservado", FracaoDoEspaco: 0, SofrePaginacao: false);
            AdicionarRegiao(regioes, proximoEndereco, bytesRestantes, reservada, "reservado", razaoResidente: 0);
        }

        return new MapaDeMemoria(pid, OrigemDosDados.Simulada, regioes, instante);
    }

    private static long AlinharAPagina(long bytes, TamanhoPagina pagina)
    {
        if (bytes <= 0)
            return 0;

        return new TamanhoBytes(bytes).EmPaginas(pagina) * pagina.EmBytes;
    }

    private static ulong AdicionarRegiao(
        ICollection<RegiaoDeMemoria> regioes,
        ulong inicio,
        long tamanho,
        Segmento segmento,
        string rotulo,
        double razaoResidente)
    {
        var fim = inicio + (ulong)tamanho;
        var faixa = new FaixaDeEnderecos(new EnderecoVirtual(inicio), new EnderecoVirtual(fim));

        long bytesResidentes;
        long bytesEmSwap;

        if (segmento.Tipo == TipoDeRegiao.Reservada)
        {
            // Espaço reservado, mas nunca comprometido (committed): não ocupa RAM nem swap.
            bytesResidentes = 0;
            bytesEmSwap = 0;
        }
        else if (segmento.SofrePaginacao)
        {
            bytesResidentes = (long)(tamanho * razaoResidente);
            bytesEmSwap = tamanho - bytesResidentes;
        }
        else
        {
            // Código, dados estáticos e pilha são tratados, na simulação, como sempre residentes.
            bytesResidentes = tamanho;
            bytesEmSwap = 0;
        }

        regioes.Add(new RegiaoDeMemoria(
            faixa,
            segmento.Tipo,
            segmento.Permissoes,
            Leitura<TamanhoBytes>.Ok(new TamanhoBytes(bytesResidentes)),
            Leitura<TamanhoBytes>.Ok(new TamanhoBytes(bytesEmSwap)),
            rotulo));

        return fim;
    }
}
