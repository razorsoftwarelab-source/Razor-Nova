using System.Runtime.InteropServices;
using RazorNova.Core.Interfaces;

namespace RazorNova.Platform;

/// <summary>
/// Implements IMediaKeyListener using a Win32 low-level keyboard hook
/// (WH_KEYBOARD_LL), per the chosen approach. Captures Play/Pause, Next,
/// and Previous media keys system-wide — even while RazorNova is
/// minimized or unfocused — without ever swallowing the key: every event
/// is forwarded to CallNextHookEx so other apps and Windows itself still
/// see the key press normally.
/// </summary>
public sealed class MediaKeyListener : IMediaKeyListener
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;

    private const int VK_MEDIA_NEXT_TRACK = 0xB0;
    private const int VK_MEDIA_PREV_TRACK = 0xB1;
    private const int VK_MEDIA_PLAY_PAUSE = 0xB3;

    // Kept alive as a field for as long as the hook is installed — if this
    // delegate were only a local/temporary value, the GC could collect it
    // while Windows still holds a native pointer to it, crashing the hook callback.
    private LowLevelKeyboardProc? _hookProc;
    private IntPtr _hookHandle = IntPtr.Zero;

    public event EventHandler? PlayPausePressed;
    public event EventHandler? NextPressed;
    public event EventHandler? PreviousPressed;

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero) return; // already started — no-op

        _hookProc = HookCallback;
        // hMod = IntPtr.Zero and dwThreadId = 0 is the standard pattern
        // for a low-level hook whose callback lives in the same process —
        // no DLL injection or separate module handle is required.
        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, IntPtr.Zero, 0);

        if (_hookHandle == IntPtr.Zero)
            throw new InvalidOperationException("Failed to install the low-level keyboard hook for media keys.");
    }

    public void Stop()
    {
        if (_hookHandle == IntPtr.Zero) return;

        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
        _hookProc = null;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            switch (hookStruct.vkCode)
            {
                case VK_MEDIA_PLAY_PAUSE:
                    PlayPausePressed?.Invoke(this, EventArgs.Empty);
                    break;
                case VK_MEDIA_NEXT_TRACK:
                    NextPressed?.Invoke(this, EventArgs.Empty);
                    break;
                case VK_MEDIA_PREV_TRACK:
                    PreviousPressed?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose() => Stop();

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
}