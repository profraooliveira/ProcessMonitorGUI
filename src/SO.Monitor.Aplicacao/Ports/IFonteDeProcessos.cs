using SO.Monitor.Dominio.Modelo;

namespace SO.Monitor.Aplicacao.Ports;

/// <summary>
/// Porta (arquitetura hexagonal) para uma fonte de amostras do sistema — a fronteira entre os
/// casos de uso e o sistema operacional real (ou uma fonte falsa em testes). As implementações
/// concretas, que sabem falar com <c>System.Diagnostics</c> ou com APIs específicas de cada
/// plataforma, residem na camada de Infraestrutura.
/// </summary>
public interface IFonteDeProcessos
{
    /// <summary>Coleta uma amostra completa dos processos do sistema no instante atual.</summary>
    ValueTask<AmostraDoSistema> ColetarAsync(CancellationToken cancellationToken);
}
