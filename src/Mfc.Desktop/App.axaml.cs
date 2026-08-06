using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Mfc.Desktop.Configuration;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Microsoft.Extensions.Configuration;

namespace Mfc.Desktop;

public sealed class App : Application, IAsyncDisposable
{
    private ShellViewModel? _shell;
    private bool _disposed;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        DesktopOptions options = LoadOptions();
        ControllerConnectionService connection = new(options);
        _shell = new ShellViewModel(connection, options);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(_shell);
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_shell is not null)
        {
            await _shell.DisposeAsync().ConfigureAwait(false);
            _shell = null;
        }
    }

    private static DesktopOptions LoadOptions()
    {
        string basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppContext.BaseDirectory;

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "MFC_DESKTOP__")
            .Build();

        return configuration.GetSection(DesktopOptions.SectionName).Get<DesktopOptions>()
            ?? new DesktopOptions();
    }
}
