using MonitorGUI.ViewModels;
using SO.Monitor.Aplicacao.CasosDeUso;
using SO.Monitor.Dominio.Servicos;

namespace MonitorGUI.DesignTime;

/// <summary>
/// VM real (<see cref="MainWindowViewModel"/>) alimentado inteiramente por fontes falsas de
/// design-time, para uso exclusivo do <c>Design.DataContext</c> do previewer do Avalonia. Herda
/// em vez de embrulhar por composição porque o previewer precisa de um tipo compatível com
/// <c>x:DataType="vm:MainWindowViewModel"</c> nos bindings compilados de <c>MainWindow.axaml</c>
/// — a herança aqui é puramente um detalhe de infraestrutura de design-time, não uma hierarquia
/// de domínio. O construtor sem parâmetros é exigido pelo XAML (<c>&lt;vm:DesignTimeMainWindowViewModel/&gt;</c>).
/// </summary>
public sealed class DesignTimeMainWindowViewModel : MainWindowViewModel
{
    public DesignTimeMainWindowViewModel() : base(
        new MonitorDeProcessos(
            new ObterAmostraClassificada(
                new FonteDeProcessosDesignTime(),
                new PoliticaComposta(
                [
                    new PoliticaPorComportamentoDeBurst(),
                    new PoliticaPorContagemDeHandles()
                ]))),
        new ObterMapaDeMemoria(new FonteDeMapaDeMemoriaDesignTime()),
        new FonteDeDetalhesDeThreadsDesignTime())
    {
    }
}
