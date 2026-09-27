using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using static KeyButtonMaker.NativeMethods;

namespace KeyButtonMaker;

/// <summary>
/// 低レベルキーボードフックでショートカットを検出し、割り当てた動作(マクロ)を実行する。
/// フックはメッセージループのあるスレッド(UIスレッド)で開始すること。
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private static readonly Dictionary<int, ModifierKeys> ModifierMap = new()
    {
        [0xA0] = ModifierKeys.Shift, [0xA1] = ModifierKeys.Shift, [0x10] = ModifierKeys.Shift,
        [0xA2] = ModifierKeys.Control, [0xA3] = ModifierKeys.Control, [0x11] = ModifierKeys.Control,
        [0xA4] = ModifierKeys.Alt, [0xA5] = ModifierKeys.Alt, [0x12] = ModifierKeys.Alt,
        [0x5B] = ModifierKeys.Windows, [0x5C] = ModifierKeys.Windows,
    };

    private readonly Func<AppSettings> _settings;
    private readonly LowLevelKeyboardProc _proc; // GC に回収されないよう保持
    private readonly uint _ownPid = (uint)Environment.ProcessId;
    private IntPtr _hook;

    private readonly HashSet<int> _heldModifiers = [];  // 物理的に押されている修飾キー
    private readonly HashSet<int> _suppressedKeys = []; // 発動に使ったキー(リピートと離す操作も握りつぶす)
    private readonly Dictionary<uint, string> _processCache = [];
    private long _cacheTick;
    private volatile bool _running;

    public KeyboardHook(Func<AppSettings> settings)
    {
        _settings = settings;
        _proc = HookProc;
    }

    public void Start()
    {
        if (_hook != IntPtr.Zero) return;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException($"キーボードフックの設定に失敗しました (Win32 エラー {Marshal.GetLastWin32Error()})");
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                if (Handle((int)wParam, info)) return 1; // 元の入力を破棄
            }
            catch (Exception ex) { Debug.WriteLine(ex); }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <returns>元の入力を破棄する場合 true</returns>
    private bool Handle(int msg, KBDLLHOOKSTRUCT info)
    {
        if (info.dwExtraInfo == InputSender.Signature) return false; // 自分が送った入力

        int vk = (int)info.vkCode;
        bool down = msg is WM_KEYDOWN or WM_SYSKEYDOWN;

        if (ModifierMap.ContainsKey(vk))
        {
            if (down) _heldModifiers.Add(vk); else _heldModifiers.Remove(vk);
            return false;
        }

        if (!down) return _suppressedKeys.Remove(vk);
        if (_suppressedKeys.Contains(vk)) return true; // 押しっぱなしのリピート

        var settings = _settings();
        if (settings.Paused || _running) return false;

        var shortcut = FindShortcut(settings, CurrentModifiers(), vk);
        if (shortcut is null) return false;

        _suppressedKeys.Add(vk);
        Run(shortcut);
        return true;
    }

    private ModifierKeys CurrentModifiers() =>
        _heldModifiers.Aggregate(ModifierKeys.None, (acc, vk) => acc | ModifierMap[vk]);

    /// <summary>最前面アプリ専用のプロファイルを優先し、なければ全アプリ共通から探す</summary>
    private Shortcut? FindShortcut(AppSettings s, ModifierKeys mods, int vk)
    {
        var (pid, process) = ForegroundProcess();
        if (pid == _ownPid) return null; // 設定画面でのキー入力は邪魔しない

        return s.Profiles.Where(p => !p.IsGlobal && p.Matches(process))
                   .Concat(s.Profiles.Where(p => p.IsGlobal))
                   .SelectMany(p => p.Shortcuts)
                   .FirstOrDefault(sc => sc.Matches(mods, vk));
    }

    private void Run(Shortcut shortcut)
    {
        var steps = shortcut.Steps.Select(s => (s.Kind, Key: new KeyCombo { VirtualKey = s.Key.VirtualKey, Modifiers = s.Key.Modifiers }, s.Milliseconds)).ToList();
        var held = _heldModifiers.ToList();
        _running = true;

        Task.Run(() =>
        {
            try
            {
                // 押しっぱなしの Ctrl などが送信キーに混ざらないよう離しておく
                InputSender.ReleaseModifiers(held);
                foreach (var step in steps)
                {
                    if (step.Kind == StepKind.Wait) Thread.Sleep(step.Milliseconds);
                    else InputSender.Tap(step.Key);
                }
            }
            catch (Exception ex) { Debug.WriteLine(ex); }
            finally { _running = false; }
        });
    }

    private (uint Pid, string Name) ForegroundProcess()
    {
        if (Environment.TickCount64 - _cacheTick > 60_000)
        {
            _processCache.Clear(); // PID 再利用への対策
            _cacheTick = Environment.TickCount64;
        }
        GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
        if (!_processCache.TryGetValue(pid, out var name))
        {
            try { name = Process.GetProcessById((int)pid).ProcessName; }
            catch (Exception) { name = ""; }
            _processCache[pid] = name;
        }
        return (pid, name);
    }
}
