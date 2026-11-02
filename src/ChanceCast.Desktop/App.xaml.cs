using System.Windows;
using System.Windows.Threading;
using ChanceCast.App.Services;
using ChanceCast.App.ViewModels;
using ChanceCast.Core.Infrastructure;
using ChanceCast.Core.Services;
using ChanceCast.Desktop.Infrastructure;
using ChanceCast.Desktop.Services;
using ChanceCast.Desktop.Views;

namespace ChanceCast.Desktop;

/// <summary>
/// Composición manual (sin contenedor DI): rutas, logger, instancia única,
/// almacenamiento, servicios, ViewModels y ventana principal.
/// </summary>
public partial class App : Application
{
    private SingleInstanceGuard? _guard;
    private GlobalHotkeyService? _hotkeys;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        AppPaths.EnsureCreated();
        var logger = new FileLogger(AppPaths.LogsDirectory);
        logger.Info("Inicio", "ChanceCast 1.0.0");

        _guard = SingleInstanceGuard.TryAcquire();
        if (!_guard.IsPrimary)
        {
            MessageBox.Show(
                "La aplicación ya está abierta. Esta instancia se cerrará sin modificar datos.",
                "ChanceCast", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var store = new JsonAppStateStore(AppPaths.DataFile, AppPaths.BackupFile, logger);
        var load = store.Load();

        var service = new MatchService(store, logger);
        var status = new AppStatus();
        var dialogs = new DialogService();
        _hotkeys = new GlobalHotkeyService();
        var exporter = new CsvExportService(dialogs, logger);

        var main = new MainViewModel(
            service,
            dialogs,
            _hotkeys,
            status,
            new CounterViewModel(service, dialogs, status),
            new HistoryViewModel(service, dialogs, exporter, status),
            new StatisticsViewModel(service));

        var window = new MainWindow { DataContext = main };
        dialogs.Attach(window);
        _hotkeys.Attach(window);
        window.SourceInitialized += (_, _) => main.RefreshHotkeys();

        main.ApplyLoadResult(load);

        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeys?.Dispose();
        _guard?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "Ocurrió un error inesperado y la aplicación se cerrará para no dejar datos inconsistentes.\n\n" +
            e.Exception.Message,
            "ChanceCast", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(1);
    }
}
