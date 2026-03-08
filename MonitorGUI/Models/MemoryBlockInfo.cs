using CommunityToolkit.Mvvm.ComponentModel;

namespace MonitorGUI.Models;

public partial class MemoryBlockInfo : ObservableObject
{
    [ObservableProperty]
    private string _corHex = "#333333";

    [ObservableProperty]
    private string _toolTip = string.Empty;

    [ObservableProperty]
    private string _tipo = string.Empty;

    [ObservableProperty]
    private string _conteudoSimulado = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}
