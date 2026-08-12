using System.Globalization;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Infraestrutura.Linux;

/// <summary>
/// Parser puro de <c>/proc/&lt;pid&gt;/smaps</c> — a versão "estendida" de <c>maps</c> (Tanenbaum:
/// mesma tabela de VMAs, agora com estatísticas de residência por região). Cada região começa com
/// a mesma linha de cabeçalho de <c>maps</c>, seguida por várias linhas <c>Chave: valor kB</c>; a
/// próxima linha de cabeçalho marca o início da região seguinte. Este analisador reaproveita
/// <see cref="AnalisadorDoProcMaps"/> para reconhecer cabeçalhos, e só acrescenta a leitura de
/// <c>Rss</c> (residente) e <c>Swap</c> (em swap) — como <c>smaps</c> contém tudo que <c>maps</c>
/// já contém, o resultado aqui já vem com residência, sem precisar combinar dois arquivos.
/// </summary>
internal static class AnalisadorDoProcSmaps
{
    /// <summary>Analisa o conteúdo completo de um arquivo <c>smaps</c>, devolvendo regiões já com residência.</summary>
    public static IReadOnlyList<RegiaoDeMemoria> Analisar(string conteudoSmaps)
    {
        var regioes = new List<RegiaoDeMemoria>();

        RegiaoDeMemoria? regiaoPendente = null;
        long rssEmKb = 0;
        long swapEmKb = 0;

        void FinalizarRegiaoPendente()
        {
            if (regiaoPendente is null)
                return;

            regioes.Add(regiaoPendente with
            {
                Residente = Leitura<TamanhoBytes>.Ok(new TamanhoBytes(rssEmKb * 1024)),
                EmSwap = Leitura<TamanhoBytes>.Ok(new TamanhoBytes(swapEmKb * 1024))
            });
        }

        foreach (var linhaBruta in conteudoSmaps.Split('\n'))
        {
            var linha = linhaBruta.TrimEnd('\r');

            if (AnalisadorDoProcMaps.TentarAnalisarLinha(linha, out var cabecalho))
            {
                FinalizarRegiaoPendente();
                regiaoPendente = cabecalho;
                rssEmKb = 0;
                swapEmKb = 0;
                continue;
            }

            if (regiaoPendente is null)
                continue; // linhas antes do primeiro cabeçalho válido; nada a acumular ainda

            if (TentarLerCampoEmKb(linha, "Rss", out var rss))
                rssEmKb = rss;
            else if (TentarLerCampoEmKb(linha, "Swap", out var swap))
                swapEmKb = swap;
        }

        FinalizarRegiaoPendente();
        return regioes;
    }

    /// <summary>
    /// Lê uma linha no formato <c>Chave:            123 kB</c>. O ':' logo após a chave evita
    /// confundir, por exemplo, <c>Swap:</c> com <c>SwapPss:</c> — prefixos parecidos que o kernel
    /// expõe lado a lado no mesmo bloco.
    /// </summary>
    private static bool TentarLerCampoEmKb(string linha, string chave, out long valorEmKb)
    {
        valorEmKb = 0;

        var linhaAparada = linha.TrimStart();
        var prefixo = chave + ":";
        if (!linhaAparada.StartsWith(prefixo, StringComparison.Ordinal))
            return false;

        var resto = linhaAparada[prefixo.Length..].Trim();
        var indiceDoEspaco = resto.IndexOf(' ');
        var numeroTexto = indiceDoEspaco >= 0 ? resto[..indiceDoEspaco] : resto;

        return long.TryParse(numeroTexto, NumberStyles.None, CultureInfo.InvariantCulture, out valorEmKb);
    }
}
