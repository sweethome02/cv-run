using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ClipNestWpf;

public partial class App : Application
{
    MainWindow _main = null!;
    IndicatorWindow _indicator = null!;
    Storage _store = null!;
    System.Windows.Forms.NotifyIcon? _tray;
    IntPtr _hwnd;
    DateTime _selfIgnoreUntil;

    public void MarkSelfSetting() => _selfIgnoreUntil = DateTime.Now.AddMilliseconds(300);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        Logger.CleanupOldLogs();
        _store = new Storage(Path.Combine(dataDir, "clipnest.db"));

        _main = new MainWindow { Store = _store };
        _indicator = new IndicatorWindow();
        _main.Indicator = _indicator;

        _indicator.ShowRequested += (x, y) =>
        {
            if (!double.IsNaN(x) && !double.IsNaN(y))
            {
                _main.FloatX = x - _main.Width / 2;
                _main.FloatY = y - _main.Height / 2;
                _main.Docked = DockEdge.None;
            }
            _main.ShowPanel();
        };
        _indicator.Repositioned += () => { };

        _main.SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(_main).Handle;
            var src = HwndSource.FromHwnd(_hwnd)!;
            src.AddHook(WndProc);
            NativeMethods.AddClipboardFormatListener(_hwnd);
        };

        SetupTray();
        if (!IsAutoStartEnabled()) SetAutoStart(true);
        _main.LoadItems();
        _main.Show();
        Logger.Info("启动", "粘贴板已启动");
        System.Windows.Application.Current.SessionEnding += (_, args) =>
        {
            Logger.Info("退出", $"系统{args.ReasonSessionEnding}，清除未固定记录");
            _store.ClearTemp();
            _store.Dispose();
        };
    }

    void SetupTray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = MakeTrayIcon(),
            Text = "粘贴板",
            Visible = true
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("显示/隐藏", null, (_, _) => TogglePanel());

        var autoStartItem = new System.Windows.Forms.ToolStripMenuItem("开机自启");
        autoStartItem.Checked = IsAutoStartEnabled();
        autoStartItem.Click += (_, _) =>
        {
            autoStartItem.Checked = !autoStartItem.Checked;
            SetAutoStart(autoStartItem.Checked);
        };
        menu.Items.Add(autoStartItem);

        menu.Items.Add("-");
        menu.Items.Add("退出", null, (_, _) => ShutdownApp());
        _tray.ContextMenuStrip = menu;

        _tray.MouseClick += (_, args) =>
        {
            if (args.Button == System.Windows.Forms.MouseButtons.Left)
                TogglePanel();
        };
    }

    static Icon MakeTrayIcon()
    {
        var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(System.Drawing.Color.FromArgb(70, 130, 200));
            g.FillEllipse(brush, 2, 2, 28, 28);
            using var font = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Bold);
            using var textBrush = new SolidBrush(System.Drawing.Color.White);
            var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("C", font, textBrush, new RectangleF(0, 0, 32, 32), fmt);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    static bool IsAutoStartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue("ClipNest") != null;
    }

    static void SetAutoStart(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (key == null) return;
        if (enabled)
            key.SetValue("ClipNest", Environment.ProcessPath ?? "");
        else
            key.DeleteValue("ClipNest", false);
    }

    void TogglePanel()
    {
        Dispatcher.Invoke(() =>
        {
            if (_main.IsVisible)
            {
                if (_main.Docked == DockEdge.None)
                    _main.Docked = DockEdge.Right;
                _main.HidePanel();
            }
            else
                _main.ShowPanel();
        });
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            ReadClipboard();
            handled = true;
        }
        else if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == NativeMethods.HOTKEY_ID)
        {
            TogglePanel();
            handled = true;
        }
        return IntPtr.Zero;
    }

    static BitmapEncoder SelectEncoder(BitmapSource bmp)
    {
        // Always use PNG as storage format — lossless, handles alpha,
        // supports all pixel formats, zero encoding failures.
        return new PngBitmapEncoder();
    }

    void ReadClipboard()
    {
        Dispatcher.Invoke(() =>
        {
            if (DateTime.Now < _selfIgnoreUntil) return;
            try
            {
                // Check image FIRST — many apps put text alongside images
                // (URL, file path, alt text), so ContainsText would
                // match before we ever reach ContainsImage.
                if (Clipboard.ContainsImage())
                {
                    var bmp = Clipboard.GetImage();
                    if (bmp != null && bmp.PixelWidth > 0 && bmp.PixelHeight > 0)
                    {
                        using var ms = new MemoryStream();
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bmp));
                        encoder.Save(ms);

                        var b64 = Convert.ToBase64String(ms.ToArray());
                        var item = new ClipboardItem
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Type = "image",
                            Format = "png",
                            Content = b64,
                        };
                        _store.Save(item);
                        _main.OnClipAdded(item);
                        Logger.Info("剪贴板", $"捕获图片 size={bmp.PixelWidth}x{bmp.PixelHeight} encoded={ms.Length}B");
                    }
                }
                else if (Clipboard.ContainsText())
                {
                    var text = Clipboard.GetText().Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        var item = new ClipboardItem
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Type = "text",
                            Content = text,
                        };
                        _store.Save(item);
                        _main.OnClipAdded(item);
                        Logger.Info("剪贴板", $"捕获文本 len={text.Length}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("ReadClipboard", ex.Message);
            }
        });
    }

    public void OnSystemShutdown()
    {
        Logger.Info("退出", "系统关机，清除未固定记录");
        _store.ClearTemp();
        _store.Dispose();
    }

    void ShutdownApp()
    {
        NativeMethods.RemoveClipboardFormatListener(_hwnd);
        NativeMethods.UnregisterHotKey(_hwnd, NativeMethods.HOTKEY_ID);
        _tray?.Dispose();
        _store.ClearTemp();
        _store.Dispose();
        Logger.Info("退出", "粘贴板已关闭");
        Environment.Exit(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ShutdownApp();
        base.OnExit(e);
    }
}
