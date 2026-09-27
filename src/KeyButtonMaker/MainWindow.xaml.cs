using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KeyButtonMaker;

public partial class MainWindow : Window
{
    private readonly App _app;
    private bool _exiting;

    public MainWindow(App app)
    {
        InitializeComponent();
        _app = app;
        ProfileList.ItemsSource = app.Settings.Profiles;
        ProfileList.SelectedIndex = 0;
    }

    public void CloseForExit()
    {
        _exiting = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_exiting) return;
        // 閉じてもトレイに常駐させる
        e.Cancel = true;
        Hide();
    }

    private Profile? SelectedProfile => ProfileList.SelectedItem as Profile;
    private Shortcut? SelectedShortcut => ShortcutList.SelectedItem as Shortcut;

    // ---- 対象アプリ ----

    private void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var p = SelectedProfile;
        RemoveProfileButton.IsEnabled = p is { IsGlobal: false };
        ShortcutList.ItemsSource = p?.Shortcuts;
        ShortcutList.SelectedIndex = 0;
        ShortcutList_SelectionChanged(sender, e);
    }

    private void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new AppPickerWindow { Owner = this };
        if (picker.ShowDialog() != true) return;

        // 登録済みならそれを選択するだけ
        var p = _app.Settings.Profiles.FirstOrDefault(x => x.Matches(picker.ProcessName));
        if (p is null)
        {
            p = new Profile { ProcessName = picker.ProcessName };
            _app.Settings.Profiles.Add(p);
        }
        ProfileList.SelectedItem = p;
    }

    private void RemoveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is not { IsGlobal: false } p) return;
        if (!ConfirmDelete($"「{p.DisplayName}」とそのショートカットをすべて削除しますか?")) return;
        _app.Settings.Profiles.Remove(p);
        ProfileList.SelectedIndex = 0;
    }

    // ---- ショートカット ----

    private void ShortcutList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var s = SelectedShortcut;
        ShortcutEditor.DataContext = s;
        ShortcutEditor.IsEnabled = s is not null;
        StepList.ItemsSource = s?.Steps;
    }

    private void AddShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is not { } p) return;
        var s = new Shortcut { Name = $"ショートカット {p.Shortcuts.Count + 1}" };
        p.Shortcuts.Add(s);
        ShortcutList.SelectedItem = s;
    }

    private void RemoveShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is not { } p || SelectedShortcut is not { } s) return;
        if (!ConfirmDelete($"ショートカット「{s.Name}」を削除しますか?")) return;
        p.Shortcuts.Remove(s);
        ShortcutList.SelectedIndex = 0;
    }

    // ---- 動作の手順 ----

    private void AddKeyStep_Click(object sender, RoutedEventArgs e) =>
        SelectedShortcut?.Steps.Add(new MacroStep { Kind = StepKind.Key });

    private void AddWaitStep_Click(object sender, RoutedEventArgs e) =>
        SelectedShortcut?.Steps.Add(new MacroStep { Kind = StepKind.Wait, Milliseconds = 100 });

    private void MoveStepUp_Click(object sender, RoutedEventArgs e) => MoveStep(sender, -1);
    private void MoveStepDown_Click(object sender, RoutedEventArgs e) => MoveStep(sender, +1);

    private void MoveStep(object sender, int offset)
    {
        if (SelectedShortcut is not { } s || ((FrameworkElement)sender).DataContext is not MacroStep step) return;
        int from = s.Steps.IndexOf(step), to = from + offset;
        if (to >= 0 && to < s.Steps.Count) s.Steps.Move(from, to);
    }

    private void RemoveStep_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not MacroStep step) return;
        SelectedShortcut?.Steps.Remove(step);
    }

    // ---- 共通 ----

    private bool ConfirmDelete(string message) =>
        MessageBox.Show(this, message, "削除の確認", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            == MessageBoxResult.Yes;

    private void KeyCapture_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (((FrameworkElement)sender).DataContext is not KeyCombo combo) return;

        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };
        // 修飾キー単体は確定しない(組み合わせるキーを待つ)
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin) return;

        var mods = Keyboard.Modifiers;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) mods |= ModifierKeys.Windows;

        combo.Modifiers = mods;
        combo.VirtualKey = KeyInterop.VirtualKeyFromKey(key);
    }
}
