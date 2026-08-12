using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Dominio.Modelo;

/// <summary>
/// Uma amostra do estado do sistema, coletada em um instante específico. Um monitor de
/// processos nunca observa o "estado atual" do sistema operacional — ele só consegue tirar
/// fotografias periódicas (polling). Daí o nome: é sempre uma amostra, nunca a realidade instantânea.
/// </summary>
public sealed record AmostraDoSistema(
    DateTimeOffset InstanteDaColeta,
    TamanhoPagina TamanhoDePaginaDoSistema,
    IReadOnlyList<Processo> Processos,
    int ProcessosInacessiveis);
