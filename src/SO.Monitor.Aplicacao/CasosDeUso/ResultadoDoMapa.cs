using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;

namespace SO.Monitor.Aplicacao.CasosDeUso;

/// <summary>
/// O mapa de memória obtido para um processo, acompanhado do motivo do eventual fallback para
/// simulação. Quando <see cref="MotivoDoFallback"/> não é nulo, <see cref="Mapa"/> foi gerado
/// artificialmente (<see cref="Servicos.SimuladorDePaginacao"/>) porque o sistema operacional
/// negou o acesso ou não suporta a leitura real nesta plataforma — a interface deve sempre
/// rotular esse mapa como simulado e exibir o motivo, nunca apresentá-lo como medição real.
/// </summary>
public sealed record ResultadoDoMapa(MapaDeMemoria Mapa, Disponibilidade? MotivoDoFallback);
