using System.Collections.ObjectModel;
using SO.Monitor.Dominio.Modelo;

namespace MonitorGUI.ViewModels;

/// <summary>
/// Concilia uma <see cref="ObservableCollection{T}"/> de <see cref="ProcessoItemViewModel"/> com
/// uma nova <see cref="AmostraDoSistema"/>, por PID: atualiza itens existentes no lugar, adiciona
/// os novos, remove os ausentes — nunca <c>Clear()+re-Add()</c>. É essa preservação de instância
/// que corrige o bug histórico de perda de seleção/rolagem na DataGrid a cada ciclo do monitor.
/// Extraído de <see cref="MainWindowViewModel"/> para ser testável isoladamente, sem depender do
/// laço de monitoramento nem do <c>Dispatcher</c> da UI — a reconciliação em si é manipulação
/// pura de coleção, sem nenhuma afinidade de thread própria.
/// </summary>
public static class ReconciliadorDeProcessos
{
    public static void Reconciliar(
        ObservableCollection<ProcessoItemViewModel> processos,
        Dictionary<int, ProcessoItemViewModel> itensPorPid,
        AmostraDoSistema amostra)
    {
        var pidsNaAmostra = new HashSet<int>(amostra.Processos.Count);

        foreach (var processo in amostra.Processos)
        {
            pidsNaAmostra.Add(processo.Pid.Valor);

            if (itensPorPid.TryGetValue(processo.Pid.Valor, out var item))
            {
                item.AtualizarDe(processo);
            }
            else
            {
                var novoItem = new ProcessoItemViewModel(processo);
                itensPorPid[processo.Pid.Valor] = novoItem;
                processos.Add(novoItem);
            }
        }

        for (var indice = processos.Count - 1; indice >= 0; indice--)
        {
            var pid = processos[indice].Pid;
            if (pidsNaAmostra.Contains(pid))
                continue;

            itensPorPid.Remove(pid);
            processos.RemoveAt(indice);
        }
    }
}
