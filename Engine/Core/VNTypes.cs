using System;
using System.Runtime.InteropServices;

namespace Visunovia.Engine.Core;

public class VNState
{
    public string CurrentScene { get; set; } = string.Empty;
    public string CurrentSpeaker { get; set; } = string.Empty;
    public string CurrentText { get; set; } = string.Empty;
    public string BackgroundImage { get; set; } = string.Empty;
    public VNTransition? BackgroundTransition { get; set; }
    public string? CurrentBgmPath { get; set; }
    public int CurrentBgmVolume { get; set; } = 100;
    public int BgmFadeIn { get; set; }
    public int BgmFadeOut { get; set; }
    public bool IsPlaying { get; set; }
    public bool IsTextComplete { get; set; }
    public Dictionary<string, VNCharacter> Characters { get; set; } = new();
    public List<VNChoice> CurrentChoices { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public Stack<string> History { get; set; } = new();
    public VNTextEffect? CurrentTextEffect { get; set; }
}

public class VNCharacter
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public bool IsVisible { get; set; }
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public VNTransition? Transition { get; set; }
}

public class VNChoice
{
    public string Text { get; set; } = string.Empty;
    public string TargetScene { get; set; } = string.Empty;
}

public enum VNTextEffectType
{
    None,
    Typewriter,
    FadeIn,
    FadeOut,
    FadeInOut
}

public class VNTextEffect
{
    public VNTextEffectType Type { get; set; } = VNTextEffectType.None;
    public int Speed { get; set; } = 50;
    public bool Shake { get; set; }
    public int FadeDuration { get; set; } = 500;
    public int DelayBeforeStart { get; set; } = 0;
}

public class VNAnimation
{
    public string Type { get; set; } = "none";
    public int Duration { get; set; } = 300;
}

public class VNBgm
{
    public string Path { get; set; } = string.Empty;
    public int Volume { get; set; } = 80;
    public bool Loop { get; set; } = true;
}

public class VNSoundEffect
{
    public string Path { get; set; } = string.Empty;
    public int Volume { get; set; } = 100;
}

public class VNSprite
{
    public string Path { get; set; } = string.Empty;
    public string Position { get; set; } = "center";
    public int Layer { get; set; }
    public VNAnimation Animation { get; set; } = new();
}

public class VNScene
{
    public string Id { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public VNBgm? Bgm { get; set; }
    public List<VNDialogue> Dialogues { get; set; } = new();
}

public enum VNDialogueType
{
    Dialogue,
    Branch,
    Event
}

public enum VNWindowEffectType
{
    None,
    Shake,
    Pulse,
    MoveTo,
    BorderFlash
}

public class VNWindowEffectParameters
{
    public VNWindowEffectType EffectType { get; set; } = VNWindowEffectType.None;

    public int ShakeAmplitude { get; set; } = 15;
    public int ShakeDurationMs { get; set; } = 1000;

    public float PulseScaleMin { get; set; } = 0.8f;
    public float PulseScaleMax { get; set; } = 1.2f;
    public float PulseFrequency { get; set; } = 1f;
    public int PulseDurationMs { get; set; } = 2000;

    public int MoveToX { get; set; } = 0;
    public int MoveToY { get; set; } = 0;

    public string BorderFlashColor { get; set; } = "#FF0000";
    public int BorderFlashCount { get; set; } = 3;
    public int BorderFlashIntervalMs { get; set; } = 200;
}

public enum VNEventType
{
    JumpScene,
    SetVariable,
    PlaySound,
    ChangeBackground,
    ChangeBgm,
    BgmStop,
    ShowCharacter,
    HideCharacter,
    Pause,
    WaitSeconds,
    Custom,
    InvokePlugin,
    InvokeCode,
    WindowEffect
}

public enum VNTransitionEffect
{
    None,
    Instant,
    FadeIn,
    FadeOut,
    CrossFade,
    SlideLeft,
    SlideRight,
    SlideUp,
    SlideDown
}

public class VNTransition
{
    public VNTransitionEffect Effect { get; set; } = VNTransitionEffect.None;
    public int Duration { get; set; } = 500;
}

public class VNChoiceOption
{
    public string Text { get; set; } = string.Empty;
    public string TargetScene { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
}

public class VNBranch
{
    public List<VNChoiceOption> Choices { get; set; } = new();
}

public class VNEvent
{
    public VNEventType EventType { get; set; } = VNEventType.Custom;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public VNTransition? Transition { get; set; }
}

public class VNDialogue
{
    public VNDialogueType Type { get; set; } = VNDialogueType.Dialogue;
    public string Speaker { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public List<VNSprite> Sprites { get; set; } = new();
    public string Voice { get; set; } = string.Empty;
    public VNTextEffect? TextEffect { get; set; }
    public VNAnimation? Animation { get; set; }
    public VNBranch? Branch { get; set; }
    public VNEvent? Event { get; set; }
    public VNTransition? Transition { get; set; }
}

public struct WindowPosition
{
    public int X { get; set; }
    public int Y { get; set; }

    public WindowPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override string ToString() => $"({X}, {Y})";

    public override bool Equals(object? obj) => obj is WindowPosition other && X == other.X && Y == other.Y;

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public static bool operator ==(WindowPosition left, WindowPosition right) => left.Equals(right);

    public static bool operator !=(WindowPosition left, WindowPosition right) => !left.Equals(right);

    public static WindowPosition Zero => new WindowPosition(0, 0);

    public static WindowPosition Center => new WindowPosition(-1, -1);
}

public struct WindowSize
{
    public int Width { get; set; }
    public int Height { get; set; }

    public WindowSize(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public override string ToString() => $"{Width}x{Height}";

    public override bool Equals(object? obj) => obj is WindowSize other && Width == other.Width && Height == other.Height;

    public override int GetHashCode() => HashCode.Combine(Width, Height);

    public static bool operator ==(WindowSize left, WindowSize right) => left.Equals(right);

    public static bool operator !=(WindowSize left, WindowSize right) => !left.Equals(right);

    public static WindowSize Zero => new WindowSize(0, 0);

    public int Area => Width * Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;
}

public enum WindowPreset
{
    Default,
    Centered1280x720,
    Centered1920x1080,
    Maximized,
    Compact800x600,
    TheaterMode1920x1080,
    SideBySide2560x720,
    Custom
}

public class WindowPresetConfiguration
{
    public WindowPreset Preset { get; set; } = WindowPreset.Default;
    public WindowPosition Position { get; set; } = WindowPosition.Zero;
    public WindowSize Size { get; set; } = WindowSize.Zero;
    public bool IsFullScreen { get; set; }
    public bool ShowInTaskbar { get; set; } = true;
    public bool Resizable { get; set; } = true;

    public static WindowPresetConfiguration GetPreset(WindowPreset preset)
    {
        return preset switch
        {
            WindowPreset.Centered1280x720 => new WindowPresetConfiguration
            {
                Preset = preset,
                Position = WindowPosition.Center,
                Size = new WindowSize(1280, 720),
                IsFullScreen = false,
                ShowInTaskbar = true,
                Resizable = true
            },
            WindowPreset.Centered1920x1080 => new WindowPresetConfiguration
            {
                Preset = preset,
                Position = WindowPosition.Center,
                Size = new WindowSize(1920, 1080),
                IsFullScreen = false,
                ShowInTaskbar = true,
                Resizable = true
            },
            WindowPreset.Maximized => new WindowPresetConfiguration
            {
                Preset = preset,
                Position = WindowPosition.Zero,
                Size = WindowSize.Zero,
                IsFullScreen = true,
                ShowInTaskbar = true,
                Resizable = true
            },
            WindowPreset.Compact800x600 => new WindowPresetConfiguration
            {
                Preset = preset,
                Position = WindowPosition.Center,
                Size = new WindowSize(800, 600),
                IsFullScreen = false,
                ShowInTaskbar = true,
                Resizable = true
            },
            WindowPreset.TheaterMode1920x1080 => new WindowPresetConfiguration
            {
                Preset = preset,
                Position = new WindowPosition(0, 0),
                Size = new WindowSize(1920, 1080),
                IsFullScreen = false,
                ShowInTaskbar = false,
                Resizable = false
            },
            WindowPreset.SideBySide2560x720 => new WindowPresetConfiguration
            {
                Preset = preset,
                Position = WindowPosition.Zero,
                Size = new WindowSize(2560, 720),
                IsFullScreen = false,
                ShowInTaskbar = true,
                Resizable = true
            },
            _ => new WindowPresetConfiguration
            {
                Preset = WindowPreset.Default,
                Position = WindowPosition.Center,
                Size = new WindowSize(1280, 720),
                IsFullScreen = false,
                ShowInTaskbar = true,
                Resizable = true
            }
        };
    }
}

public static class WindowController
{
    private static readonly object _lockObj = new();
    private static IntPtr _windowHandle = IntPtr.Zero;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetForegroundWindow(IntPtr hWnd);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_BORDER = 0x00800000;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;
    private const int SW_RESTORE = 9;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    public static void Initialize(IntPtr windowHandle)
    {
        lock (_lockObj)
        {
            _windowHandle = windowHandle;
        }
    }

    public static void ApplyPreset(WindowPreset preset)
    {
        var config = WindowPresetConfiguration.GetPreset(preset);
        ApplyConfiguration(config);
    }

    public static void ApplyPreset(WindowPresetConfiguration config)
    {
        ApplyConfiguration(config);
    }

    public static void ApplyConfiguration(WindowPresetConfiguration config)
    {
        IntPtr hwnd;
        lock (_lockObj)
        {
            hwnd = _windowHandle;
        }

        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Window handle is not initialized. Call Initialize() first.");
        }

        try
        {
            if (config.IsFullScreen)
            {
                EnterFullScreenInternal(hwnd);
            }
            else
            {
                ShowWindow(hwnd, SW_RESTORE);

                int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                int screenHeight = GetSystemMetrics(SM_CYSCREEN);

                int targetX, targetY, targetWidth, targetHeight;

                if (config.Position == WindowPosition.Center)
                {
                    targetWidth = config.Size.Width > 0 ? config.Size.Width : 1280;
                    targetHeight = config.Size.Height > 0 ? config.Size.Height : 720;
                    targetX = (screenWidth - targetWidth) / 2;
                    targetY = (screenHeight - targetHeight) / 2;
                }
                else
                {
                    targetX = config.Position.X;
                    targetY = config.Position.Y;
                    targetWidth = config.Size.Width > 0 ? config.Size.Width : 1280;
                    targetHeight = config.Size.Height > 0 ? config.Size.Height : 720;
                }

                uint flags = SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED;
                SetWindowPos(hwnd, IntPtr.Zero, targetX, targetY, targetWidth, targetHeight, flags);

                int style = GetWindowLong(hwnd, GWL_STYLE);
                if (config.Resizable)
                {
                    style |= WS_THICKFRAME;
                }
                else
                {
                    style &= ~WS_THICKFRAME;
                }
                SetWindowLong(hwnd, GWL_STYLE, style);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to apply window configuration: {ex.Message}", ex);
        }
    }

    public static void EnterFullScreen()
    {
        IntPtr hwnd;
        lock (_lockObj)
        {
            hwnd = _windowHandle;
        }

        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Window handle is not initialized. Call Initialize() first.");
        }

        EnterFullScreenInternal(hwnd);
    }

    private static void EnterFullScreenInternal(IntPtr hwnd)
    {
        IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

        if (!GetMonitorInfo(hMonitor, ref mi))
        {
            throw new InvalidOperationException("Failed to get monitor information.");
        }

        SetWindowPos(hwnd, IntPtr.Zero,
            mi.rcMonitor.Left, mi.rcMonitor.Top,
            mi.rcMonitor.Right - mi.rcMonitor.Left,
            mi.rcMonitor.Bottom - mi.rcMonitor.Top,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    public static WindowSize GetWindowSize()
    {
        IntPtr hwnd;
        lock (_lockObj)
        {
            hwnd = _windowHandle;
        }

        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Window handle is not initialized. Call Initialize() first.");
        }

        if (!GetClientRect(hwnd, out RECT rect))
        {
            throw new InvalidOperationException("Failed to get window client rectangle.");
        }

        return new WindowSize(rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    public static WindowPosition GetWindowPosition()
    {
        IntPtr hwnd;
        lock (_lockObj)
        {
            hwnd = _windowHandle;
        }

        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Window handle is not initialized. Call Initialize() first.");
        }

        if (!GetWindowRect(hwnd, out RECT rect))
        {
            throw new InvalidOperationException("Failed to get window rectangle.");
        }

        return new WindowPosition(rect.Left, rect.Top);
    }

    public static void SetWindowPosition(int x, int y)
    {
        IntPtr hwnd;
        lock (_lockObj)
        {
            hwnd = _windowHandle;
        }

        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Window handle is not initialized. Call Initialize() first.");
        }

        if (!SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE))
        {
            throw new InvalidOperationException("Failed to set window position.");
        }
    }

    public static void SetWindowSize(int width, int height)
    {
        IntPtr hwnd;
        lock (_lockObj)
        {
            hwnd = _windowHandle;
        }

        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Window handle is not initialized. Call Initialize() first.");
        }

        if (!SetWindowPos(hwnd, IntPtr.Zero, 0, 0, width, height, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE))
        {
            throw new InvalidOperationException("Failed to set window size.");
        }
    }

    public static void BringToFront()
    {
        IntPtr hwnd;
        lock (_lockObj)
        {
            hwnd = _windowHandle;
        }

        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Window handle is not initialized. Call Initialize() first.");
        }

        SetForegroundWindow(hwnd);
    }

    public static void Shake(int amplitude = 15, int durationMs = 1000)
    {
        IntPtr hwnd;
        lock (_lockObj)
        {
            hwnd = _windowHandle;
        }

        if (hwnd == IntPtr.Zero) return;

        if (!GetWindowRect(hwnd, out RECT originalRect)) return;

        int originalX = originalRect.Left;
        int originalY = originalRect.Top;
        int originalWidth = originalRect.Right - originalRect.Left;
        int originalHeight = originalRect.Bottom - originalRect.Top;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            var startTime = DateTime.Now;
            var random = new Random();

            while ((DateTime.Now - startTime).TotalMilliseconds < durationMs)
            {
                try
                {
                    float elapsed = (float)(DateTime.Now - startTime).TotalMilliseconds;
                    float shakeAmount = amplitude * (float)Math.Sin(2.0 * Math.PI * 2.0 * elapsed / 1000.0);
                    float decay = 1.0f - (float)(DateTime.Now - startTime).TotalMilliseconds / durationMs;

                    int offsetX = (int)(shakeAmount * decay * (random.NextDouble() - 0.5) * 2);
                    int offsetY = (int)(shakeAmount * decay * (random.NextDouble() - 0.5) * 2);

                    SetWindowPos(hwnd, IntPtr.Zero,
                        originalX + offsetX, originalY + offsetY,
                        originalWidth, originalHeight,
                        SWP_NOZORDER | SWP_NOACTIVATE);

                    Thread.Sleep(16);
                }
                catch { }
            }

            SetWindowPos(hwnd, IntPtr.Zero,
                originalX, originalY, originalWidth, originalHeight,
                SWP_NOZORDER | SWP_NOACTIVATE);
        });
    }

    public static void Pulse(float scaleMin = 0.8f, float scaleMax = 1.2f, float frequency = 1f, int durationMs = 2000)
    {
        IntPtr hwnd;
        lock (_lockObj)
        {
            hwnd = _windowHandle;
        }

        if (hwnd == IntPtr.Zero) return;

        if (!GetWindowRect(hwnd, out RECT originalRect)) return;

        int originalX = originalRect.Left;
        int originalY = originalRect.Top;
        int originalWidth = originalRect.Right - originalRect.Left;
        int originalHeight = originalRect.Bottom - originalRect.Top;
        int centerX = originalX + originalWidth / 2;
        int centerY = originalY + originalHeight / 2;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            var startTime = DateTime.Now;

            while ((DateTime.Now - startTime).TotalMilliseconds < durationMs)
            {
                try
                {
                    float elapsed = (float)(DateTime.Now - startTime).TotalMilliseconds / 1000f;
                    float t = (float)Math.Sin(2.0 * Math.PI * frequency * elapsed);
                    float scale = scaleMin + (scaleMax - scaleMin) * (0.5f + 0.5f * t);

                    int newWidth = (int)(originalWidth * scale);
                    int newHeight = (int)(originalHeight * scale);
                    int newX = centerX - newWidth / 2;
                    int newY = centerY - newHeight / 2;

                    SetWindowPos(hwnd, IntPtr.Zero, newX, newY, newWidth, newHeight,
                        SWP_NOZORDER | SWP_NOACTIVATE);

                    Thread.Sleep(16);
                }
                catch { }
            }

            SetWindowPos(hwnd, IntPtr.Zero,
                originalX, originalY, originalWidth, originalHeight,
                SWP_NOZORDER | SWP_NOACTIVATE);
        });
    }

    public static void MoveTo(int x, int y)
    {
        IntPtr hwnd;
        lock (_lockObj)
        {
            hwnd = _windowHandle;
        }

        if (hwnd == IntPtr.Zero) return;

        SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    public static void BorderFlash(int flashCount = 3, int flashIntervalMs = 200)
    {
        IntPtr hwnd;
        lock (_lockObj)
        {
            hwnd = _windowHandle;
        }

        if (hwnd == IntPtr.Zero) return;

        int originalStyle = GetWindowLong(hwnd, GWL_STYLE);
        bool hadCaption = (originalStyle & WS_CAPTION) != 0;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            for (int i = 0; i < flashCount * 2; i++)
            {
                try
                {
                    int style = GetWindowLong(hwnd, GWL_STYLE);
                    bool isHidden = i % 2 == 0;

                    if (isHidden)
                    {
                        style &= ~(WS_CAPTION | WS_BORDER | WS_THICKFRAME);
                    }
                    else
                    {
                        if (hadCaption) style |= WS_CAPTION | WS_BORDER | WS_THICKFRAME;
                        else style |= WS_BORDER | WS_THICKFRAME;
                    }

                    SetWindowLong(hwnd, GWL_STYLE, style);
                    SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                        SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

                    Thread.Sleep(flashIntervalMs);
                }
                catch { }
            }

            SetWindowLong(hwnd, GWL_STYLE, originalStyle);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        });
    }

    public static void ExecuteWindowEffect(VNWindowEffectType effectType, Dictionary<string, object> parameters)
    {
        switch (effectType)
        {
            case VNWindowEffectType.Shake:
                {
                    int amplitude = 15;
                    int durationMs = 1000;
                    if (parameters.TryGetValue("ShakeAmplitude", out var ampObj) && int.TryParse(ampObj?.ToString(), out var amp)) amplitude = amp;
                    if (parameters.TryGetValue("ShakeDurationMs", out var durObj) && int.TryParse(durObj?.ToString(), out var dur)) durationMs = dur;
                    Shake(amplitude, durationMs);
                }
                break;

            case VNWindowEffectType.Pulse:
                {
                    float scaleMin = 0.8f;
                    float scaleMax = 1.2f;
                    float frequency = 1f;
                    int durationMs = 2000;
                    if (parameters.TryGetValue("PulseScaleMin", out var sminObj) && float.TryParse(sminObj?.ToString(), out var smin)) scaleMin = smin;
                    if (parameters.TryGetValue("PulseScaleMax", out var smaxObj) && float.TryParse(smaxObj?.ToString(), out var smax)) scaleMax = smax;
                    if (parameters.TryGetValue("PulseFrequency", out var freqObj) && float.TryParse(freqObj?.ToString(), out var freq)) frequency = freq;
                    if (parameters.TryGetValue("PulseDurationMs", out var pdurObj) && int.TryParse(pdurObj?.ToString(), out var pdur)) durationMs = pdur;
                    Pulse(scaleMin, scaleMax, frequency, durationMs);
                }
                break;

            case VNWindowEffectType.MoveTo:
                {
                    int x = 0, y = 0;
                    if (parameters.TryGetValue("MoveToX", out var xObj) && int.TryParse(xObj?.ToString(), out var xv)) x = xv;
                    if (parameters.TryGetValue("MoveToY", out var yObj) && int.TryParse(yObj?.ToString(), out var yv)) y = yv;
                    MoveTo(x, y);
                }
                break;

            case VNWindowEffectType.BorderFlash:
                {
                    int flashCount = 3;
                    int flashIntervalMs = 200;
                    if (parameters.TryGetValue("BorderFlashCount", out var cntObj) && int.TryParse(cntObj?.ToString(), out var cnt)) flashCount = cnt;
                    if (parameters.TryGetValue("BorderFlashIntervalMs", out var intObj) && int.TryParse(intObj?.ToString(), out var intv)) flashIntervalMs = intv;
                    BorderFlash(flashCount, flashIntervalMs);
                }
                break;

            case VNWindowEffectType.None:
            default:
                break;
        }
    }
}