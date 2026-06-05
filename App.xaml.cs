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
    string _imageDir = null!;
    System.Windows.Forms.NotifyIcon? _tray;
    IntPtr _hwnd;
    DateTime _selfIgnoreUntil;

    public void MarkSelfSetting() => _selfIgnoreUntil = DateTime.Now.AddMilliseconds(800);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        var imageDir = Path.Combine(dataDir, "images");
        _imageDir = imageDir;
        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(imageDir);
        Logger.CleanupOldLogs();
        _store = new Storage(Path.Combine(dataDir, "clipnest.db"), imageDir);

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
        // Sync auto-start: update if missing or pointing to a different exe
        var currentPath = Environment.ProcessPath ?? "";
        if (!IsAutoStartEnabled())
        {
            SetAutoStart(true);
        }
        else
        {
            // Verify the registered path still matches the current exe
            var regPath = GetAutoStartPath();
            if (!string.Equals(regPath, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                SetAutoStart(true);
                Logger.Info("注册表", $"开机自启路径已更新: {currentPath}");
            }
        }
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

    static string? GetAutoStartPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue("ClipNest") as string;
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
            // Dispatch asynchronously to avoid reentrancy issues
            // (accessing clipboard inside a clipboard notification can deadlock)
            Dispatcher.InvokeAsync(() => ReadClipboard());
            handled = true;
        }
        else if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == NativeMethods.HOTKEY_ID)
        {
            TogglePanel();
            handled = true;
        }
        return IntPtr.Zero;
    }

    void ReadClipboard()
    {
        if (DateTime.Now < _selfIgnoreUntil) return;
        try
        {
            var dataObj = Clipboard.GetDataObject();
            if (dataObj == null) return;

            // Check for image data (Browser / screenshot / file copy)
            bool isImage = dataObj.GetDataPresent("Bitmap");
            bool isDib = dataObj.GetDataPresent("DeviceIndependentBitmap");
            bool isFileDrop = dataObj.GetDataPresent("FileDrop");

            if (isImage || isDib || isFileDrop)
            {
                BitmapSource? bmp = null;

                // 1) Try Bitmap format (most common for browser copy-image / screenshots)
                if (isImage)
                {
                    try { bmp = Clipboard.GetImage(); } catch { }
                }

                // 2) Try FileDrop — read file bytes directly (preserves original format)
                if (bmp == null && isFileDrop)
                {
                    try
                    {
                        var files = (string[])dataObj.GetData("FileDrop");
                        if (files != null && files.Length > 0)
                        {
                            var ext = Path.GetExtension(files[0]).ToLowerInvariant();
                            if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".tiff" or ".tif" or ".webp")
                            {
                                var rawBytes = File.ReadAllBytes(files[0]);
                                var id = Guid.NewGuid().ToString("N");
                                var fileName = $"{id}.{ext.TrimStart('.')}";
                                var destPath = Path.Combine(_imageDir, fileName);
                                File.WriteAllBytes(destPath, rawBytes);
                                var item = new ClipboardItem
                                {
                                    Id = id,
                                    Type = "image",
                                    Format = ext.TrimStart('.'),
                                    Name = Path.GetFileName(files[0]),
                                    Content = fileName,
                                };
                                _store.Save(item);
                                _main.OnClipAdded(item);
                                Logger.Info("剪贴板", $"捕获图片文件 {files[0]} format={item.Format} raw={rawBytes.Length}B");
                                return;
                            }
                        }
                    }
                    catch (Exception fex)
                    {
                        Logger.Error("剪贴板/FileDrop", fex.Message);
                    }
                }

                // 3) DIB fallback — get DIB data and decode
                if (bmp == null && isDib)
                {
                    try
                    {
                        bmp = Clipboard.GetImage();
                    }
                    catch { }
                }

                // Encode whatever we got as PNG
                if (bmp != null && bmp.PixelWidth > 0 && bmp.PixelHeight > 0)
                {
                    using var ms = new MemoryStream();
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bmp));
                    encoder.Save(ms);

                    var rawBytes = ms.ToArray();
                    var id = Guid.NewGuid().ToString("N");
                    var fileName = $"{id}.png";
                    var destPath = Path.Combine(_imageDir, fileName);
                    File.WriteAllBytes(destPath, rawBytes);

                    var ts = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss");
                    var item = new ClipboardItem
                    {
                        Id = id,
                        Type = "image",
                        Format = "png",
                        Name = $"截图 {ts}",
                        Content = fileName,
                    };
                    _store.Save(item);
                    _main.OnClipAdded(item);
                    Logger.Info("剪贴板", $"捕获图片 size={bmp.PixelWidth}x{bmp.PixelHeight} file={fileName}");
                }
            }
            else if (dataObj.GetDataPresent("Text") || dataObj.GetDataPresent("UnicodeText"))
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
    }

    public void OnSystemShutdown()
    {
        Logger.Info("退出", "系统关机，清除未固定记录");
        _store.ClearTempPasteFiles();
        _store.ClearTemp();
        _store.Dispose();
    }

    void ShutdownApp()
    {
        NativeMethods.RemoveClipboardFormatListener(_hwnd);
        NativeMethods.UnregisterHotKey(_hwnd, NativeMethods.HOTKEY_ID);
        _tray?.Dispose();
        _store.ClearTempPasteFiles();
        _store.ClearTempPasteFiles();
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
