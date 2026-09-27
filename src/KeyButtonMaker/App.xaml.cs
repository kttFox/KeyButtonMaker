using System.Windows;
using Forms = System.Windows.Forms;

namespace KeyButtonMaker;

public partial class App : Application
{
    private Mutex? _mutex;
    private KeyboardHook? _hook;
    private AutoSaver? _autoSaver;
    private Forms.NotifyIcon? _tray;
    private Forms.ToolStripMenuItem? _pausedMenu;
    private MainWindow? _window;

    public AppSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, "KeyButtonMaker.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("KeyButtonMaker は既に起動しています。タスクトレイを確認してください。", "KeyButtonMaker");
            Shutdown();
            return;
        }

        Settings = SettingsStore.Load();
        _autoSaver = new AutoSaver(Settings);

        _hook = new KeyboardHook(() => Settings);
        try { _hook.Start(); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "KeyButtonMaker", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        CreateTray();
        if (!e.Args.Contains("/tray")) ShowMainWindow();
    }

    private void CreateTray()
    {
        _pausedMenu = new Forms.ToolStripMenuItem("一時停止", null, (_, _) => SetPaused(!Settings.Paused)) { Checked = Settings.Paused };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("設定を開く", null, (_, _) => ShowMainWindow());
        menu.Items.Add(_pausedMenu);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => ExitApp());

        _tray = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "KeyButtonMaker",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => ShowMainWindow();
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        using var stream = GetResourceStream(new Uri("pack://application:,,,/app.ico")).Stream;
        // トレイ用に小さいサイズを選ばせる
        return new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }

    public void SetPaused(bool paused)
    {
        Settings.Paused = paused;
        if (_pausedMenu is not null) _pausedMenu.Checked = paused;
        _autoSaver?.Flush();
    }

    public void ShowMainWindow()
    {
        _window ??= new MainWindow(this);
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void ExitApp()
    {
        _autoSaver?.Flush();
        _window?.CloseForExit();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hook?.Dispose();
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
