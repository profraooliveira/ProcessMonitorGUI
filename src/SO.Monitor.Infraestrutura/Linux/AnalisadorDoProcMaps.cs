using System.Globalization;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Infraestrutura.Linux;

/// <summary>
/// Parser puro de <c>/proc/&lt;pid&gt;/maps</c> — o formato com que o Linux expõe o espaço de
/// endereçamento virtual de um processo (Tanenbaum: cada linha é uma VMA, "Virtual Memory Area").
/// Cada linha tem o formato <c>endereco-endereco perms offset dev:dev inode pathname</c>, em que
/// <c>pathname</c> é opcional (mapeamentos anônimos) e pode conter espaços — por isso ele nunca é
/// obtido por um simples <c>Split(' ')</c>, e sim como "tudo que sobra" após os cinco primeiros
/// campos. Não faz nenhuma chamada ao sistema operacional: recebe texto, devolve regiões.
/// </summary>
internal static class AnalisadorDoProcMaps
{
    /// <summary>Analisa o conteúdo completo de um arquivo <c>maps</c>, uma linha por região.</summary>
    public static IReadOnlyList<RegiaoDeMemoria> Analisar(string conteudoMaps)
    {
        var regioes = new List<RegiaoDeMemoria>();

        foreach (var linhaBruta in conteudoMaps.Split('\n'))
        {
            var linha = linhaBruta.TrimEnd('\r');
            if (TentarAnalisarLinha(linha, out var regiao))
                regioes.Add(regiao!);
        }

        return regioes;
    }

    /// <summary>
    /// Tenta analisar uma única linha de <c>maps</c>. Devolve <c>false</c> (nunca lança) quando a
    /// linha não segue o formato esperado — uma linha malformada é simplesmente ignorada, não
    /// interrompe a leitura das demais.
    /// </summary>
    internal static bool TentarAnalisarLinha(string linha, out RegiaoDeMemoria? regiao)
    {
        regiao = null;

        ReadOnlySpan<char> texto = linha.AsSpan();
        var posicao = 0;

        if (!TentarProximoCampo(texto, ref posicao, out var faixaTexto)) return false;
        if (!TentarParsearFaixa(faixaTexto, out var faixa)) return false;

        if (!TentarProximoCampo(texto, ref posicao, out var permissoesBrutas)) return false;
        if (permissoesBrutas.Length < 4) return false;

        // offset, dev e inode são exigidos pelo formato, mas não são usados por este analisador.
        if (!TentarProximoCampo(texto, ref posicao, out _)) return false;
        if (!TentarProximoCampo(texto, ref posicao, out _)) return false;
        if (!TentarProximoCampo(texto, ref posicao, out _)) return false;

        // O que sobra da linha (podendo conter espaços internos) é o pathname — ou nada, no caso
        // de um mapeamento anônimo (heap, pilha, memória alocada sem arquivo por trás).
        while (posicao < texto.Length && texto[posicao] == ' ')
            posicao++;
        var pathname = texto[posicao..].TrimEnd().ToString();

        var permissoesCompletas = permissoesBrutas.ToString();
        var permissoesTexto = permissoesBrutas[..3].ToString();
        var tipo = ClassificarTipo(permissoesCompletas, pathname);

        regiao = new RegiaoDeMemoria(
            faixa,
            tipo,
            permissoesTexto,
            Leitura<TamanhoBytes>.NaoSuportada(),
            Leitura<TamanhoBytes>.NaoSuportada(),
            string.IsNullOrEmpty(pathname) ? null : pathname);

        return true;
    }

    /// <summary>
    /// Classifica o propósito de uma região a partir das permissões e do pathname — a mesma
    /// leitura que um humano faria olhando para <c>/proc/&lt;pid&gt;/maps</c>: presença do bit de
    /// execução, extensão do arquivo mapeado, ou uma das tags especiais entre colchetes que o
    /// kernel usa para regiões sem arquivo (<c>[heap]</c>, <c>[stack]</c>, <c>[vdso]</c>...).
    /// </summary>
    private static TipoDeRegiao ClassificarTipo(string permissoes, string pathname)
    {
        var temExecucao = permissoes[2] == 'x';
        var lerEEscrever = permissoes[0] == 'r' && permissoes[1] == 'w';
        var semPermissaoAlguma = permissoes[0] == '-' && permissoes[1] == '-' && permissoes[2] == '-';

        return (temExecucao, pathname) switch
        {
            (true, var p) when EhBibliotecaCompartilhada(p) => TipoDeRegiao.BibliotecaCompartilhada,
            (true, var p) when EhArquivoRegular(p) => TipoDeRegiao.Codigo,
            (_, "[heap]") => TipoDeRegiao.Heap,
            (_, var p) when EhPilha(p) => TipoDeRegiao.Pilha,
            (false, var p) when EhArquivoRegular(p) => TipoDeRegiao.ArquivoMapeado,
            _ when string.IsNullOrEmpty(pathname) && lerEEscrever => TipoDeRegiao.Heap,
            _ when semPermissaoAlguma => TipoDeRegiao.Reservada,
            _ => TipoDeRegiao.Outra
        };
    }

    private static bool EhBibliotecaCompartilhada(string pathname) =>
        !string.IsNullOrEmpty(pathname) && pathname.Contains(".so", StringComparison.Ordinal);

    private static bool EhArquivoRegular(string pathname) =>
        !string.IsNullOrEmpty(pathname) && pathname[0] != '[';

    private static bool EhPilha(string pathname) =>
        pathname == "[stack]" || pathname.StartsWith("[stack:", StringComparison.Ordinal);

    /// <summary>Extrai o próximo campo separado por espaço a partir de <paramref name="posicao"/>, avançando-a.</summary>
    private static bool TentarProximoCampo(ReadOnlySpan<char> texto, ref int posicao, out ReadOnlySpan<char> campo)
    {
        while (posicao < texto.Length && texto[posicao] == ' ')
            posicao++;

        var inicio = posicao;
        while (posicao < texto.Length && texto[posicao] != ' ')
            posicao++;

        campo = texto[inicio..posicao];
        return campo.Length > 0;
    }

    /// <summary>Parseia o primeiro campo da linha, no formato hexadecimal <c>inicio-fim</c>.</summary>
    private static bool TentarParsearFaixa(ReadOnlySpan<char> faixaTexto, out FaixaDeEnderecos faixa)
    {
        faixa = default;

        var indiceHifen = faixaTexto.IndexOf('-');
        if (indiceHifen <= 0 || indiceHifen == faixaTexto.Length - 1)
            return false;

        if (!ulong.TryParse(faixaTexto[..indiceHifen], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var inicio))
            return false;
        if (!ulong.TryParse(faixaTexto[(indiceHifen + 1)..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var fim))
            return false;
        if (fim < inicio)
            return false;

        faixa = new FaixaDeEnderecos(new EnderecoVirtual(inicio), new EnderecoVirtual(fim));
        return true;
    }
}
