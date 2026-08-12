using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MonitorGUI.ViewModels;
using MonitorGUI.Views;
using SO.Monitor.Aplicacao.CasosDeUso;
using SO.Monitor.Aplicacao.Ports;
using SO.Monitor.Dominio.Servicos;
using SO.Monitor.Infraestrutura;

namespace MonitorGUI;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            _serviceProvider = ConfigurarServicos().BuildServiceProvider();

            desktop.MainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>(),
            };

            desktop.ShutdownRequested += (_, _) => _serviceProvider?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Composition root: este é o único lugar da Apresentação que sabe montar as implementações
    /// concretas por trás das portas da Aplicação. As fontes reais vêm de
    /// <see cref="FabricaDeFontes"/> — a fiação por plataforma (macOS/Linux/Windows) é decidida
    /// inteiramente dentro dela, sem qualquer impacto aqui.
    /// </summary>
    private static IServiceCollection ConfigurarServicos()
    {
        var servicos = new ServiceCollection();

        servicos.AddSingleton<IFonteDeProcessos>(_ => FabricaDeFontes.CriarFonteDeProcessos());
        servicos.AddSingleton<IFonteDeMapaDeMemoria>(_ => FabricaDeFontes.CriarFonteDeMapaDeMemoria());
        servicos.AddSingleton<IFonteDeDetalhesDeThreads>(_ => FabricaDeFontes.CriarFonteDeDetalhesDeThreads());

        // Cadeia de responsabilidade: comportamento de burst primeiro (funciona em qualquer
        // plataforma), contagem de handles como segunda opinião quando a primeira ficar indecisa.
        servicos.AddSingleton<IPoliticaDeClassificacao>(_ => new PoliticaComposta(
        [
            new PoliticaPorComportamentoDeBurst(),
            new PoliticaPorContagemDeHandles()
        ]));

        servicos.AddSingleton<ObterAmostraClassificada>();
        servicos.AddSingleton<ObterMapaDeMemoria>();
        servicos.AddSingleton<MonitorDeProcessos>();

        servicos.AddSingleton<MainWindowViewModel>();

        return servicos;
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
