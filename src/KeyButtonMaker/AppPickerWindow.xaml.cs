using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KeyButtonMaker;

public partial class AppPickerWindow : Window
{
    public record RunningApp(string Name, string Title);

    /// <summary>選択されたプロセス名(.exe は除く)</summary>
    public string ProcessName { get; private set; } = "";

    public AppPickerWindow()
    {
        InitializeComponent();
        LoadProcesses();
        Loaded += (_, _) => ProcessBox.Focus();
    }

    private void LoadProcesses()
    {
        ProcessList.ItemsSource = Process.GetProcesses()
            .Where(p => p.MainWindowHandle != IntPtr.Zero && p.Id != Environment.ProcessId)
            .Select(p => new RunningApp(p.ProcessName, p.MainWindowTitle))
            .DistinctBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadProcesses();

    private void ProcessList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProcessList.SelectedItem is RunningApp app) ProcessBox.Text = app.Name;
    }

    private void ProcessList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProcessList.SelectedItem is RunningApp) Ok_Click(sender, e);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var name = ProcessBox.Text.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        if (name.Length == 0)
        {
            MessageBox.Show(this, "アプリを選択するか、プロセス名を入力してください。", Title);
            return;
        }
        ProcessName = name;
        DialogResult = true;
    }
}
