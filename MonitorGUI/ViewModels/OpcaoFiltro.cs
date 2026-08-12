using SO.Monitor.Dominio.Enums;

namespace MonitorGUI.ViewModels;

/// <summary>
/// Uma opção do filtro de perfil de execução exibido no combo da toolbar — o texto amigável em
/// pt-BR pareado com o valor de <see cref="PerfilDeExecucao"/> (ou <c>null</c> para "sem filtro")
/// que efetivamente alimenta <c>CriteriosDeAmostragem.FiltrarPorPerfil</c>.
/// </summary>
public readonly record struct OpcaoFiltro(string Texto, PerfilDeExecucao? Perfil)
{
    public override string ToString() => Texto;
}
