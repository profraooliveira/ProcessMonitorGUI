using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Dominio.Modelo;

/// <summary>
/// Um retrato do espaço de endereçamento virtual completo de um processo em um instante — a
/// soma de todas as suas regiões de memória. Carrega sempre a origem dos dados (medida real ou
/// simulada), para que a interface nunca apresente uma como se fosse a outra.
/// </summary>
public sealed record MapaDeMemoria(
    Pid Pid,
    OrigemDosDados OrigemDosDados,
    IReadOnlyList<RegiaoDeMemoria> Regioes,
    DateTimeOffset InstanteDaColeta)
{
    /// <summary>Soma da memória residente de todas as regiões cuja leitura está disponível.</summary>
    public TamanhoBytes TotalResidente => Regioes
        .Where(regiao => regiao.Residente.TemValor)
        .Aggregate(TamanhoBytes.Zero, (total, regiao) => total + regiao.Residente.Valor);

    /// <summary>Soma da memória em swap de todas as regiões cuja leitura está disponível.</summary>
    public TamanhoBytes TotalEmSwap => Regioes
        .Where(regiao => regiao.EmSwap.TemValor)
        .Aggregate(TamanhoBytes.Zero, (total, regiao) => total + regiao.EmSwap.Valor);
}
