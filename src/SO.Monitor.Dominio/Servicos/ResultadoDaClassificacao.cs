using SO.Monitor.Dominio.Enums;

namespace SO.Monitor.Dominio.Servicos;

/// <summary>
/// O resultado de classificar o comportamento de um processo (Tanenbaum: rajadas de CPU vs.
/// rajadas de E/S). Carrega também o critério em texto — o "porquê" da classificação, pensado
/// para virar tooltip didático na interface em vez de um rótulo sem explicação.
/// </summary>
public readonly record struct ResultadoDaClassificacao(PerfilDeExecucao Perfil, string Criterio);
