using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZeusFiscalMonitor.Desktop.Services;
using ZeusFiscalMonitor.Desktop.ViewModels;
using ZeusFiscalMonitor.Infraestrutura;

namespace ZeusFiscalMonitor.Desktop;

/// <summary>Composition Root: único ponto que conhece as implementações concretas.</summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += AoOcorrerErroNaoTratado;

        var configuracao = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Local.json", optional: true) // segredos locais (fora do git)
            .Build();

        _services = ConfigurarServicos(configuracao).BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var janela = _services.GetRequiredService<MainWindow>();
        janela.DataContext = _services.GetRequiredService<MainViewModel>();
        janela.Show();
    }

    private static IServiceCollection ConfigurarServicos(IConfiguration configuracao)
    {
        var services = new ServiceCollection();

        services.AddSingleton(configuracao);
        services.AddInfraestrutura(configuracao);

        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton<IDialogService, DialogService>();

        services.AddSingleton<ConsultaSefazViewModel>();
        services.AddSingleton<ValidadorAssinadorViewModel>();
        services.AddSingleton<HistoricoViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }

    private static void AoOcorrerErroNaoTratado(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "Erro inesperado", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
