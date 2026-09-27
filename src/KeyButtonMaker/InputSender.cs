using System.Runtime.InteropServices;
using System.Windows.Input;
using static KeyButtonMaker.NativeMethods;

namespace KeyButtonMaker;

internal static class InputSender
{
    /// <summary>自分が送出した入力を識別するためのマーカー</summary>
    public static readonly IntPtr Signature = new(0x4B424D31);

    /// <summary>Alt/Win を離したときのメニュー・スタート表示を抑えるためのダミーキー(未割り当て)</summary>
    private const int VK_DUMMY = 0xE8;

    private static readonly HashSet<int> ExtendedKeys =
    [
        0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, // PgUp PgDn End Home ← ↑ → ↓
        0x2D, 0x2E, 0x5B, 0x5C, 0x5D, 0x6F, 0x90, 0xA3, 0xA5, // Ins Del Win Apps / NumLock RCtrl RAlt
        0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xAB, 0xAC, 0xAD, 0xAE, 0xAF, 0xB0, 0xB1, 0xB2, 0xB3, // ブラウザ・メディアキー
    ];

    private static IEnumerable<int> ModifierVks(ModifierKeys m)
    {
        if (m.HasFlag(ModifierKeys.Control)) yield return 0x11;
        if (m.HasFlag(ModifierKeys.Shift)) yield return 0x10;
        if (m.HasFlag(ModifierKeys.Alt)) yield return 0x12;
        if (m.HasFlag(ModifierKeys.Windows)) yield return 0x5B;
    }

    private static INPUT Key(int vk, bool up)
    {
        uint flags = up ? KEYEVENTF_KEYUP : 0;
        if (ExtendedKeys.Contains(vk)) flags |= KEYEVENTF_EXTENDEDKEY;
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)vk, dwFlags = flags, dwExtraInfo = Signature } },
        };
    }

    /// <summary>修飾キー込みでキーを 1 回押して離す</summary>
    public static void Tap(KeyCombo combo)
    {
        if (combo.IsEmpty) return;
        var mods = ModifierVks(combo.Modifiers).ToList();
        Send([
            .. mods.Select(vk => Key(vk, false)),
            Key(combo.VirtualKey, false),
            Key(combo.VirtualKey, true),
            .. Enumerable.Reverse(mods).Select(vk => Key(vk, true)),
        ]);
    }

    /// <summary>ユーザーが押したままの修飾キーを一時的に離した状態にする</summary>
    public static void ReleaseModifiers(IReadOnlyCollection<int> heldVks)
    {
        if (heldVks.Count == 0) return;
        Send([Key(VK_DUMMY, false), Key(VK_DUMMY, true), .. heldVks.Select(vk => Key(vk, true))]);
    }

    private static void Send(INPUT[] inputs) =>
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
}
