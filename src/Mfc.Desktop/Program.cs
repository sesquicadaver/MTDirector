using System;
using Avalonia;

namespace Mfc.Desktop;

/// <summary>
/// Desktop process entry. Feature modules land in later milestones.
/// </summary>
internal static class Program
{
    // Preserve Contracts project reference for architecture analysis.
    private static readonly Type ContractsAnchor = typeof(Contracts.AssemblyMarker);

    [STAThread]
    public static void Main(string[] args)
    {
        _ = ContractsAnchor;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
