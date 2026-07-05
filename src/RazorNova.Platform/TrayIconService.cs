using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using RazorNova.Core.Interfaces;

namespace RazorNova.Platform;

public sealed class TrayIconService : ITrayService
{
    private TaskbarIcon? _taskbarIcon;
    private MenuItem? _playPauseMenuItem;
    private IntPtr _iconHandle;

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public event EventHandler? PlayPauseRequested;
    public event EventHandler? NextRequested;
    public event EventHandler? PreviousRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? TrayIconActivated;

    public void Initialize()
    {
        if (_taskbarIcon is not null)
            return;

        _playPauseMenuItem = new MenuItem { Header = "Play" };
        _playPauseMenuItem.Click += (_, _) => PlayPauseRequested?.Invoke(this, EventArgs.Empty);

        var nextMenuItem = new MenuItem { Header = "Next" };
        nextMenuItem.Click += (_, _) => NextRequested?.Invoke(this, EventArgs.Empty);

        var previousMenuItem = new MenuItem { Header = "Previous" };
        previousMenuItem.Click += (_, _) => PreviousRequested?.Invoke(this, EventArgs.Empty);

        var exitMenuItem = new MenuItem { Header = "Exit" };
        exitMenuItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        var contextMenu = new ContextMenu
        {
            Items = { _playPauseMenuItem, nextMenuItem, previousMenuItem, new Separator(), exitMenuItem }
        };

        _taskbarIcon = new TaskbarIcon
        {
            Icon = CreateTrayIcon(),
            ToolTipText = "Razor Nova",
            ContextMenu = contextMenu
        };

        _taskbarIcon.TrayMouseDoubleClick += (_, _) =>
            TrayIconActivated?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateTooltip(string text)
    {
        if (_taskbarIcon is not null)
            _taskbarIcon.ToolTipText = text;
    }

    public void UpdatePlayPauseState(bool isPlaying)
    {
        if (_playPauseMenuItem is not null)
            _playPauseMenuItem.Header = isPlaying ? "Pause" : "Play";
    }

    public void ShowNotification(string title, string message) =>
        _taskbarIcon?.ShowNotification(title, message);

    public void Remove()
    {
        if (_taskbarIcon is null)
            return;

        _taskbarIcon.Dispose();
        _taskbarIcon = null;
        _playPauseMenuItem = null;

        if (_iconHandle != IntPtr.Zero)
        {
            DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }
    }

    public void Dispose() => Remove();

    /// <summary>
    /// Creates a System.Drawing.Icon from GDI+ drawing (play triangle on dark circle).
    /// No URI involved — completely bypasses the BitmapImage/Uri problem.
    /// </summary>
    private Icon CreateTrayIcon()
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            using var bgBrush = new SolidBrush(Color.FromArgb(0x0D, 0x0D, 0x0D));
            graphics.FillEllipse(bgBrush, 0, 0, size, size);

            using var accentBrush = new SolidBrush(Color.FromArgb(0x8A, 0x2B, 0xE2));
            PointF[] playTriangle =
            {
                new(size * 0.36f, size * 0.26f),
                new(size * 0.76f, size * 0.50f),
                new(size * 0.36f, size * 0.74f)
            };
            graphics.FillPolygon(accentBrush, playTriangle);
        }

        // Get native icon handle and create Icon from it
        _iconHandle = bitmap.GetHicon();
        return Icon.FromHandle(_iconHandle);
    }
}