using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace KeyButtonMaker;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnChanged(name);
    }

    protected void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>修飾キー + キーの組み合わせ</summary>
public class KeyCombo : Observable
{
    private int _virtualKey;
    private ModifierKeys _modifiers;

    public int VirtualKey { get => _virtualKey; set { Set(ref _virtualKey, value); OnChanged(nameof(Text)); } }
    public ModifierKeys Modifiers { get => _modifiers; set { Set(ref _modifiers, value); OnChanged(nameof(Text)); } }

    [JsonIgnore]
    public bool IsEmpty => VirtualKey == 0;

    [JsonIgnore]
    public string Text
    {
        get
        {
            if (IsEmpty) return "(クリックしてキーを押す)";
            var parts = new List<string>();
            if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            parts.Add(KeyInterop.KeyFromVirtualKey(VirtualKey).ToString());
            return string.Join(" + ", parts);
        }
    }

    public void Clear()
    {
        VirtualKey = 0;
        Modifiers = ModifierKeys.None;
    }
}

public enum StepKind { Key, Wait }

/// <summary>動作の 1 手順(キー入力 or 待ち時間)</summary>
public class MacroStep : Observable
{
    private StepKind _kind;
    private int _milliseconds = 100;

    public StepKind Kind { get => _kind; set => Set(ref _kind, value); }
    public KeyCombo Key { get; set; } = new();
    public int Milliseconds { get => _milliseconds; set => Set(ref _milliseconds, Math.Max(0, value)); }
}

public class Shortcut : Observable
{
    private string _name = "";
    private bool _enabled = true;

    public string Name { get => _name; set => Set(ref _name, value); }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public KeyCombo Trigger { get; set; } = new();
    public ObservableCollection<MacroStep> Steps { get; set; } = [];

    public bool Matches(ModifierKeys mods, int vk) =>
        Enabled && !Trigger.IsEmpty && Trigger.VirtualKey == vk && Trigger.Modifiers == mods;
}

public class Profile : Observable
{
    private string _processName = "";

    /// <summary>対象プロセス名(例: notepad)。空なら全アプリ共通。</summary>
    public string ProcessName
    {
        get => _processName;
        set
        {
            Set(ref _processName, value);
            OnChanged(nameof(IsGlobal));
            OnChanged(nameof(DisplayName));
        }
    }

    [JsonIgnore]
    public string DisplayName => IsGlobal ? "全アプリ共通" : ProcessName;

    public ObservableCollection<Shortcut> Shortcuts { get; set; } = [];

    [JsonIgnore]
    public bool IsGlobal => string.IsNullOrWhiteSpace(ProcessName);

    public bool Matches(string processName)
    {
        var name = ProcessName.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return string.Equals(name, processName, StringComparison.OrdinalIgnoreCase);
    }
}

public class AppSettings
{
    public bool Paused { get; set; }
    public ObservableCollection<Profile> Profiles { get; set; } = [];
}
