using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ClipNestWpf;

public enum DockEdge { None, Left, Right, Top }

public partial class MainWindow : Window
{
    public Storage Store { get; set; } = null!;
    public IndicatorWindow Indicator { get; set; } = null!;

    public DockEdge Docked { get; set; }
    public bool Expanded { get; set; }
    public double FloatX { get; set; }
    public double FloatY { get; set; }

    readonly ObservableCollection<ItemVM> _items = new();
    readonly List<ClipboardItem> _raw = new();
    string _filter = "";

    readonly double _snapDist = 40;
    readonly double _panelW = 400;
    readonly double _panelH = 540;
    bool _indPosSet;
    DateTime _shownAt;
    bool _rightMenuOpen;

    public MainWindow()
    {
        InitializeComponent();
        ItemList.ItemsSource = _items;

        var sw = SystemParameters.PrimaryScreenWidth;
        var sh = SystemParameters.PrimaryScreenHeight;
        FloatX = (sw - _panelW) / 2;
        FloatY = (sh - _panelH) / 2;
        Left = FloatX;
        Top = FloatY;

        Logger.Info("启动", "粘贴板窗口已创建");
    }

    // ─── Data ─────────────────────────────────────────────────

    public void LoadItems()
    {
        _raw.Clear();
        _raw.AddRange(Store.List(200));
        ApplyFilter();
    }

    public void OnClipAdded(ClipboardItem item)
    {
        _raw.Insert(0, item);
        if (_raw.Count > 500) _raw.RemoveRange(500, _raw.Count - 500);
        ApplyFilter();
        Logger.Info("剪贴板", $"捕获新内容 type={item.Type} len={item.Content.Length}");
    }

    void ApplyFilter()
    {
        var filtered = string.IsNullOrEmpty(_filter)
            ? _raw
            : _raw.Where(x => x.Type == "text" && x.Content.Contains(_filter, StringComparison.OrdinalIgnoreCase)).ToList();

        _items.Clear();
        foreach (var x in filtered)
        {
            var ts = TimeZoneInfo.ConvertTimeFromUtc(
            DateTimeOffset.FromUnixTimeSeconds(x.CreatedAt).UtcDateTime,
            TimeZoneInfo.Local);
            _items.Add(new ItemVM
            {
                Id = x.Id,
                DisplayText = x.Type == "image" ? $"[Image/{x.Format.ToUpper()}]" : Truncate(x.Content, 120),
                TimeStr = x.IsPinned ? $"📌 {ts:MM-dd HH:mm}" : $"{ts:MM-dd HH:mm}",
                TypeLabel = x.Type == "image" ? "🖼" : "📝",
                IsPinned = x.IsPinned,
            });
        }
        CountLabel.Text = $"{_raw.Count} items";
        EmptyHint.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ─── Show / Hide / Collapse ───────────────────────────────

    public void ShowPanel()
    {
        Expanded = true;
        Width = _panelW;
        Height = _panelH;

        double px = FloatX, py = FloatY;
        switch (Docked)
        {
            case DockEdge.Left:
                px = 0;
                py = Indicator.Top + 32 - _panelH / 2;
                break;
            case DockEdge.Right:
                px = SystemParameters.PrimaryScreenWidth - _panelW;
                py = Indicator.Top + 32 - _panelH / 2;
                break;
            case DockEdge.Top:
                px = Indicator.Left + 32 - _panelW / 2;
                py = 0;
                break;
        }

        // Clamp to visible screen area
        var sh = SystemParameters.PrimaryScreenHeight;
        var sw = SystemParameters.PrimaryScreenWidth;
        if (py < 0) py = 0;
        if (py + _panelH > sh) py = sh - _panelH;
        if (px < 0) px = 0;
        if (px + _panelW > sw) px = sw - _panelW;

        Left = px; Top = py;
        Show();
        Indicator.Hide();
        Activate();
        SearchBox.Focus();
        AnimateShow();

        _shownAt = DateTime.Now;
        Logger.Info("显示", $"面板弹出 Docked={Docked} pos=({(int)px},{(int)py})");
    }

    void AnimateShow()
    {
        double ox = 0.5, oy = 0.5;
        switch (Docked)
        {
            case DockEdge.Left:
                ox = 0;
                oy = (Indicator.Top + 32) / _panelH;
                break;
            case DockEdge.Right:
                ox = 1;
                oy = (Indicator.Top + 32) / _panelH;
                break;
            case DockEdge.Top:
                ox = (Indicator.Left + 32) / _panelW;
                oy = 0;
                break;
        }
        RootBorder.RenderTransformOrigin = new Point(Clamp01(ox), Clamp01(oy));

        RootScale.ScaleX = 0;
        RootScale.ScaleY = 0;
        Opacity = 0;

        var dur = TimeSpan.FromMilliseconds(280);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, dur) { EasingFunction = ease });
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, dur) { EasingFunction = ease });
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, dur) { EasingFunction = ease });
    }

    public async void HidePanel()
    {
        var dur = TimeSpan.FromMilliseconds(250);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseIn };

        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0, dur) { EasingFunction = ease });
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0, dur) { EasingFunction = ease });
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, dur) { EasingFunction = ease });

        await Task.Delay(300);
        Dispatcher.Invoke(FinishCollapse);
    }

    void FinishCollapse()
    {
        Expanded = false;
        Hide();
        RootScale.ScaleX = 1;
        RootScale.ScaleY = 1;
        Opacity = 0.95;

        if (Docked == DockEdge.None) return;

        if (!_indPosSet)
        {
            double ix = 0, iy = 0;
            var iw = Docked == DockEdge.Top ? 64.0 : 32.0;
            var ih = Docked == DockEdge.Top ? 32.0 : 64.0;
            switch (Docked)
            {
                case DockEdge.Left:  ix = 0; iy = (SystemParameters.PrimaryScreenHeight - ih) / 2; break;
                case DockEdge.Right: ix = SystemParameters.PrimaryScreenWidth - iw; iy = (SystemParameters.PrimaryScreenHeight - ih) / 2; break;
                case DockEdge.Top:   ix = (SystemParameters.PrimaryScreenWidth - iw) / 2; iy = 0; break;
            }
            Indicator.Left = ix; Indicator.Top = iy;
            _indPosSet = true;
        }

        Indicator.SetDock(Docked);
        Indicator.Show();
        Logger.Info("隐藏", $"面板缩回指示器 Docked={Docked} indicatorPos=({(int)Indicator.Left},{(int)Indicator.Top})");
    }

    // ─── Edge snap ────────────────────────────────────────────

    void TrySnap()
    {
        var cx = Left + Width / 2;
        var cy = Top + Height / 2;
        var sw = SystemParameters.PrimaryScreenWidth;

        var edge = DockEdge.None;
        if (cx < _snapDist + Width / 2) edge = DockEdge.Left;
        else if (cx > sw - _snapDist - Width / 2) edge = DockEdge.Right;
        else if (cy < _snapDist + Height / 2) edge = DockEdge.Top;

        if (edge != DockEdge.None)
        {
            if (Docked != edge) _indPosSet = false;
            Docked = edge;
            FinishCollapse();
            Logger.Info("吸附", $"吸附到 {edge} 边缘");
        }
        else
        {
            if (Docked != DockEdge.None) Logger.Info("移动", "拖离边缘，切换到浮动模式");
            Docked = DockEdge.None;
        }
    }

    // ─── Window events ────────────────────────────────────────

    void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.GetPosition(this).Y < 36)
        {
            DragMove();
            TrySnap();
        }
    }

    void Window_LocationChanged(object sender, EventArgs e)
    {
        if (Expanded && Docked == DockEdge.None)
        {
            FloatX = Left; FloatY = Top;
        }
    }

    void Window_MouseEnter(object sender, MouseEventArgs e) { }

    void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_rightMenuOpen) return;
        if ((DateTime.Now - _shownAt).TotalMilliseconds < 500) return;
        if (Docked != DockEdge.None)
            HidePanel();
    }

    void Window_Deactivated(object sender, EventArgs e)
    {
        if (IsVisible && Expanded)
        {
            Topmost = true;
            Topmost = false;
            Topmost = true;
        }
    }

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RootScale.ScaleX = 1;
        RootScale.ScaleY = 1;
        Opacity = 0.95;
    }

    // ─── Controls ─────────────────────────────────────────────

    void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _filter = SearchBox.Text;
        ClearSearchBtn.Visibility = string.IsNullOrEmpty(_filter) ? Visibility.Collapsed : Visibility.Visible;
        ApplyFilter();
    }

    void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        _filter = "";
        ApplyFilter();
        Logger.Info("搜索", "搜索已清空");
    }

    void ItemList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemList.SelectedItem is ItemVM vm)
        {
            var item = _raw.FirstOrDefault(x => x.Id == vm.Id);
            if (item != null)
            {
                ((App)Application.Current).MarkSelfSetting();

                if (item.Type == "image")
                {
                    // Decode from base64 and set to clipboard as image
                    try
                    {
                        var bytes = Convert.FromBase64String(item.Content);
                        using var ms = new MemoryStream(bytes);
                        var img = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default).Frames[0];
                        Clipboard.Clear();
                        Clipboard.SetImage(img);
                        Logger.Info("粘贴", $"选中粘贴图片 format={item.Format} size={img.PixelWidth}x{img.PixelHeight}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("粘贴图片", ex.Message);
                    }
                }
                else
                {
                    Clipboard.SetText(item.Content);
                    Logger.Info("粘贴", $"选中粘贴: \"{Truncate(item.Content, 40)}\"");
                }

                if (Docked != DockEdge.None)
                {
                    HidePanel();
                }
                else
                {
                    Logger.Info("粘贴", "浮动模式，面板保持显示");
                }

                var pt = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
                pt.Tick += (_, _) => { pt.Stop(); System.Windows.Forms.SendKeys.SendWait("^v"); };
                pt.Start();
            }
        }
    }

    void DeleteClick(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ItemVM vm)
        {
            Store.Delete(vm.Id);
            _raw.RemoveAll(x => x.Id == vm.Id);
            ApplyFilter();
        }
        e.Handled = true;
    }

    void ClearAllBtn_Click(object sender, RoutedEventArgs e)
    {
        var unpinned = _raw.Where(x => !x.IsPinned).ToList();
        foreach (var item in unpinned)
            Store.Delete(item.Id);
        _raw.RemoveAll(x => !x.IsPinned);
        ApplyFilter();
        Logger.Info("清空", $"清除了 {unpinned.Count} 条未固定记录");
    }

    // ─── Right-click Context Menu ──────────────────────────

    void ListBox_RightBtnUp(object sender, MouseButtonEventArgs e)
    {
        _rightMenuOpen = true;
        var src = e.OriginalSource as DependencyObject;
        var listItem = FindParent<ListBoxItem>(src);
        if (listItem?.DataContext is ItemVM vm)
        {
            var cm = new ContextMenu();
            var pinItem = new MenuItem { Header = vm.IsPinned ? "取消固定" : "固定" };
            pinItem.Click += (_, _2) => { _rightMenuOpen = false; PinItem(vm); };
            cm.Items.Add(pinItem);
            cm.Items.Add(new Separator());
            var delItem = new MenuItem { Header = "删除" };
            delItem.Click += (_, _2) => { _rightMenuOpen = false; DeleteItem(vm); };
            cm.Items.Add(delItem);
            cm.Closed += (_, _2) => _rightMenuOpen = false;
            listItem.ContextMenu = cm;
            cm.IsOpen = true;
        }
        e.Handled = true;
    }

    void PinItem(ItemVM vm)
    {
        var raw = _raw.FirstOrDefault(x => x.Id == vm.Id);
        if (raw != null)
        {
            Store.TogglePin(vm.Id, !raw.IsPinned);
            _raw.Clear();
            _raw.AddRange(Store.List(200));
            ApplyFilter();
            Logger.Info("固定", raw.IsPinned ? "取消固定" : "已固定");
        }
    }

    void DeleteItem(ItemVM vm)
    {
        Store.Delete(vm.Id);
        _raw.RemoveAll(x => x.Id == vm.Id);
        ApplyFilter();
    }

    static T FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent) return parent;
            child = VisualTreeHelper.GetParent(child);
        }
        return null!;
    }

    static string Truncate(string s, int max)
    {
        if (s.Length > max) return s[..max] + "…";
        return s;
    }

    static double Clamp01(double v) => Math.Max(0, Math.Min(1, v));

    // ─── Hotkey ───────────────────────────────────────────────

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var src = HwndSource.FromHwnd(hwnd)!;
        src.AddHook(WndProc);
        NativeMethods.RegisterHotKey(hwnd, NativeMethods.HOTKEY_ID, NativeMethods.MOD_CONTROL, 0xC0);
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_SYSCOMMAND)
        {
            int cmd = wParam.ToInt32() & 0xFFF0;
            if (cmd == NativeMethods.SC_MAXIMIZE || cmd == NativeMethods.SC_MINIMIZE)
            {
                handled = true;
                return IntPtr.Zero;
            }
        }
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == NativeMethods.HOTKEY_ID)
        {
            if (Docked != DockEdge.None)
                ShowPanel();
            else if (IsVisible)
                HidePanel();
            else
                ShowPanel();
            handled = true;
        }
        else if (msg == NativeMethods.WM_GETMINMAXINFO)
        {
            NativeMethods.LimitMaxTrackSize(lParam, 800, 900);
            handled = true;
            return IntPtr.Zero;
        }
        return IntPtr.Zero;
    }
}

public class ItemVM : INotifyPropertyChanged
{
    public string Id { get; set; } = "";
    public string DisplayText { get; set; } = "";
    public string TimeStr { get; set; } = "";
    public string TypeLabel { get; set; } = "";
    public bool IsPinned { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropChanged([CallerMemberName] string name = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
