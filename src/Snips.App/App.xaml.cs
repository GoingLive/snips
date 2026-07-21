using System.IO;
using System.Windows;
using Snips.Core.Storage;
using Snips.Data;
using Snips.Interop.Foreground;
using Snips.Interop.Hotkeys;

namespace Snips.App;

public partial class App : Application
{
    private const uint VK_SPACE = 0x20;

    private SnipsDatabase? _database;
    private ForegroundWindowTracker? _foregroundTracker;
    private HotKeyManager? _hotKeyManager;
    private MainWindow? _mainWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var dbPath = ResolveDatabasePath();
        _database = await SnipsDatabase.OpenAsync(dbPath);

        // Must exist before the window so WM_HOTKEY delivery (via SetWinEventHook,
        // WINEVENT_OUTOFCONTEXT) starts as soon as the Dispatcher message loop is pumping.
        _foregroundTracker = new ForegroundWindowTracker();

        _mainWindow = new MainWindow(_database, _foregroundTracker);

        _hotKeyManager = new HotKeyManager(_mainWindow);
        var registered = _hotKeyManager.Register(
            HotKeyModifiers.Control | HotKeyModifiers.Alt, VK_SPACE, _mainWindow.ShowAndFocus);

        if (registered is null)
        {
            // Default combo already claimed by another app (SPEC.md §5.8). The tray icon and
            // double-click still work; a Settings view to rebind it is Phase 6.
            MessageBox.Show(
                "Ctrl+Alt+Space is already in use by another application. Snips is still " +
                "available from the tray icon.",
                "Snips", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Intentionally not shown here — starts minimised to the tray (SPEC.md §10 default).
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotKeyManager?.Dispose();
        _foregroundTracker?.Dispose();
        _database?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.OnExit(e);
    }

    private static string ResolveDatabasePath()
    {
        var exeDirectory = AppContext.BaseDirectory;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var portableMarkerExists = File.Exists(Path.Combine(exeDirectory, DatabasePathResolver.PortableMarkerFileName));
        return DatabasePathResolver.Resolve(exeDirectory, localAppData, portableMarkerExists);
    }
}
