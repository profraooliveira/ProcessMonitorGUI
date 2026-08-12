using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Dominio.Modelo;

/// <summary>
/// O retrato de memória de um processo: quanto de RAM ele realmente ocupa (conjunto residente,
/// o "working set" de Denning), quanto de espaço de endereçamento virtual reservou, e quanto é
/// memória privada — não compartilhada com outros processos via bibliotecas ou mapeamentos.
/// </summary>
public sealed record PerfilDeMemoria(
    Leitura<TamanhoBytes> ConjuntoResidente,
    Leitura<TamanhoBytes> MemoriaVirtual,
    Leitura<TamanhoBytes> MemoriaPrivada,
    TamanhoPagina TamanhoDePagina)
{
    /// <summary>
    /// Número de páginas físicas ocupadas pelo conjunto residente, derivado do próprio conjunto
    /// residente e do tamanho de página (Information Expert: quem já tem o dado, calcula).
    /// </summary>
    public Leitura<long> PaginasResidentes => ConjuntoResidente.Estado switch
    {
        Disponibilidade.Disponivel => Leitura<long>.Ok(ConjuntoResidente.Valor.EmPaginas(TamanhoDePagina)),
        Disponibilidade.AcessoNegado => Leitura<long>.Negada(),
        Disponibilidade.ProcessoEncerrado => Leitura<long>.ProcessoEncerrado(),
        _ => Leitura<long>.NaoSuportada()
    };
}
