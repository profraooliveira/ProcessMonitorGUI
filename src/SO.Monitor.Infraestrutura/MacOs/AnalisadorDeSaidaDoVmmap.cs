using System.Globalization;
using System.Text.RegularExpressions;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Infraestrutura.MacOs;

/// <summary>
/// Analisador puro (sem I/O, sem processo externo) da saída de <c>vmmap -interleaved &lt;pid&gt;</c>
/// no macOS. Cada linha é interpretada isoladamente e de forma tolerante: o formato de saída do
/// vmmap varia entre versões do macOS, então uma linha que não corresponde ao layout esperado é
/// simplesmente ignorada — o analisador nunca lança por causa de uma linha malformada, apenas
/// perde aquela região específica.
/// </summary>
internal static class AnalisadorDeSaidaDoVmmap
{
    private static readonly Regex RegexTamanhoDePagina =
        new(@"VM page size:\s*(?<paginaEmBytes>\d+)\s*bytes", RegexOptions.Compiled);

    /// <summary>
    /// Casa uma linha de região do formato "REGION TYPE START-END [ VSIZE RSDNT DIRTY SWAP]
    /// PRT/MAX SHRMOD PURGE REGION DETAIL". VSIZE e DIRTY são casados, mas não usados: a faixa de
    /// endereços já dá a extensão da região, e o domínio não modela memória "suja" separadamente.
    /// </summary>
    private static readonly Regex RegexLinhaDeRegiao = new(
        @"^(?<tipo>.+?)\s{2,}(?<inicio>[0-9a-fA-F]+)-(?<fim>[0-9a-fA-F]+)\s+" +
        @"\[\s*[^\s\]]+\s+(?<rsdnt>[^\s\]]+)\s+[^\s\]]+\s+(?<swap>[^\s\]]+)\]\s+" +
        @"(?<prt>\S+)\s+\S+(?:\s+PURGE=\S+)?\s*(?<detalhe>.*)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Extrai o tamanho de página declarado no cabeçalho do relatório ("VM page size: N bytes"),
    /// ou <c>null</c> se o cabeçalho não estiver presente ou não puder ser interpretado.
    /// </summary>
    public static TamanhoPagina? ExtrairTamanhoDePagina(string saidaDoVmmap)
    {
        var correspondencia = RegexTamanhoDePagina.Match(saidaDoVmmap);
        if (!correspondencia.Success)
            return null;

        return int.TryParse(correspondencia.Groups["paginaEmBytes"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor)
            ? new TamanhoPagina(valor)
            : null;
    }

    /// <summary>
    /// Extrai as regiões de memória descritas na saída de <c>vmmap -interleaved</c>. Linhas de
    /// cabeçalho, sumário ou em branco não casam com o layout de região e são silenciosamente
    /// puladas — comportamento esperado, não uma falha.
    /// </summary>
    public static IReadOnlyList<RegiaoDeMemoria> AnalisarRegioes(string saidaDoVmmap)
    {
        var regioes = new List<RegiaoDeMemoria>();

        foreach (var linha in saidaDoVmmap.Split('\n'))
        {
            var regiao = TentarAnalisarLinhaDeRegiao(linha.TrimEnd('\r', '\n'));
            if (regiao is not null)
                regioes.Add(regiao);
        }

        return regioes;
    }

    private static RegiaoDeMemoria? TentarAnalisarLinhaDeRegiao(string linha)
    {
        var correspondencia = RegexLinhaDeRegiao.Match(linha);
        if (!correspondencia.Success)
            return null;

        try
        {
            var inicio = ulong.Parse(correspondencia.Groups["inicio"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var fim = ulong.Parse(correspondencia.Groups["fim"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var faixa = new FaixaDeEnderecos(new EnderecoVirtual(inicio), new EnderecoVirtual(fim));

            if (!TentarConverterTamanho(correspondencia.Groups["rsdnt"].Value, out var residenteEmBytes))
                return null;
            if (!TentarConverterTamanho(correspondencia.Groups["swap"].Value, out var emSwapEmBytes))
                return null;

            var tipoBruto = correspondencia.Groups["tipo"].Value.Trim();
            var permissoes = correspondencia.Groups["prt"].Value.Split('/')[0];
            var detalhe = correspondencia.Groups["detalhe"].Value.Trim();
            var rotulo = detalhe.Length > 0 ? detalhe : null;

            var tipo = ClassificarTipo(tipoBruto, permissoes, rotulo, residenteEmBytes);

            return new RegiaoDeMemoria(
                faixa,
                tipo,
                permissoes,
                Leitura<TamanhoBytes>.Ok(new TamanhoBytes(residenteEmBytes)),
                Leitura<TamanhoBytes>.Ok(new TamanhoBytes(emSwapEmBytes)),
                rotulo);
        }
        catch (Exception excecao) when (excecao is FormatException or OverflowException or ArgumentOutOfRangeException)
        {
            // A linha casou com o layout geral, mas algum campo veio em formato inesperado (ex.:
            // endereço fora da faixa de 64 bits). O analisador precisa tolerar variações entre
            // versões do vmmap: pulamos a região, nunca lançamos.
            return null;
        }
    }

    /// <summary>
    /// Classifica o <see cref="TipoDeRegiao"/> a partir do REGION TYPE bruto do vmmap, das
    /// permissões e do rótulo (REGION DETAIL) — a mesma divisão didática de memória de processo
    /// (código, dados, heap, pilha, bibliotecas...) que Tanenbaum descreve, adaptada ao
    /// vocabulário específico do vmmap.
    /// </summary>
    private static TipoDeRegiao ClassificarTipo(string tipoBruto, string permissoes, string? rotulo, long residenteEmBytes)
    {
        if (tipoBruto.StartsWith("__TEXT", StringComparison.Ordinal))
            return TipoDeRegiao.Codigo;

        if (tipoBruto.StartsWith("__DATA", StringComparison.Ordinal) || tipoBruto.StartsWith("__AUTH", StringComparison.Ordinal))
            return TipoDeRegiao.DadosEstaticos;

        if (tipoBruto.StartsWith("MALLOC", StringComparison.Ordinal) || string.Equals(tipoBruto, "VM_ALLOCATE", StringComparison.Ordinal))
            return TipoDeRegiao.Heap;

        if (tipoBruto.StartsWith("Stack", StringComparison.Ordinal) || tipoBruto.StartsWith("STACK GUARD", StringComparison.Ordinal))
            return TipoDeRegiao.Pilha;

        if (tipoBruto.StartsWith("__LINKEDIT", StringComparison.Ordinal) || tipoBruto.StartsWith("dyld", StringComparison.Ordinal))
            return TipoDeRegiao.BibliotecaCompartilhada;

        if (rotulo is not null && (rotulo.EndsWith(".dylib", StringComparison.Ordinal) || rotulo.Contains(".framework/", StringComparison.Ordinal)))
            return TipoDeRegiao.BibliotecaCompartilhada;

        if (string.Equals(tipoBruto, "mapped file", StringComparison.Ordinal))
            return TipoDeRegiao.ArquivoMapeado;

        if (residenteEmBytes == 0 && permissoes.StartsWith("---", StringComparison.Ordinal))
            return TipoDeRegiao.Reservada;

        return TipoDeRegiao.Outra;
    }

    /// <summary>
    /// Converte um tamanho no formato do vmmap (ex.: "10.7M", "1712K", "0K") para bytes. Aceita
    /// sufixos K/M/G/T (potências de 1024) e números decimais com ponto; sem sufixo, o valor já é
    /// interpretado como bytes.
    /// </summary>
    private static bool TentarConverterTamanho(string texto, out long bytes)
    {
        bytes = 0;
        if (string.IsNullOrEmpty(texto))
            return false;

        var multiplicador = char.ToUpperInvariant(texto[^1]) switch
        {
            'K' => 1024d,
            'M' => 1024d * 1024,
            'G' => 1024d * 1024 * 1024,
            'T' => 1024d * 1024 * 1024 * 1024,
            _ => 1d
        };

        var temSufixo = multiplicador != 1d;
        var textoNumerico = temSufixo ? texto[..^1] : texto;

        if (!double.TryParse(textoNumerico, NumberStyles.Float, CultureInfo.InvariantCulture, out var numero) || numero < 0)
            return false;

        bytes = (long)Math.Round(numero * multiplicador, MidpointRounding.AwayFromZero);
        return true;
    }
}
