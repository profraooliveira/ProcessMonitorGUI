using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using MonitorGUI.ViewModels;

namespace MonitorGUI.Views;

public partial class MainWindow : Window
{
    /// <summary>
    /// Posição da coluna "Páginas" na DataGrid mestre (PID, Nome, Estado, Perfil, %CPU, Threads,
    /// Handles, Memória Residente, Páginas, Início da Execução) — usada para atualizar seu
    /// cabeçalho dinamicamente com o tamanho de página real da amostra (ex.: "Páginas (16 KiB)").
    /// Mantida em sincronia com a ordem das colunas declaradas em MainWindow.axaml.
    /// </summary>
    private const int IndiceColunaPaginas = 8;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        AtualizarTituloColunaPaginas(viewModel);
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.TituloColunaPaginas) || sender is not MainWindowViewModel viewModel)
            return;

        Dispatcher.UIThread.Post(() => AtualizarTituloColunaPaginas(viewModel));
    }

    private void AtualizarTituloColunaPaginas(MainWindowViewModel viewModel)
    {
        var coluna = GradeDeProcessos.Columns.ElementAtOrDefault(IndiceColunaPaginas);
        if (coluna is not null)
            coluna.Header = viewModel.TituloColunaPaginas;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        (DataContext as IDisposable)?.Dispose();
        base.OnClosed(e);
    }
}
