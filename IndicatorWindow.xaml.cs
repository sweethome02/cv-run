using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClipNestWpf;

public partial class IndicatorWindow : Window
{
    public event Action<double, double>? ShowRequested;  // (screenX, screenY), NaN=default
    public event Action? Repositioned;

    bool _isDragging;
    Point _dragStartScreen, _dragStartWindow;
    DockEdge _dockEdge;
    const double DragThreshold = 5;
    const double DetachThreshold = 100;

    public IndicatorWindow()
    {
        InitializeComponent();
    }

    public void SetDock(DockEdge edge)
    {
        _dockEdge = edge;
        switch (edge)
        {
            case DockEdge.Left:
                Width = 32; Height = 64;
                SemiPath.Data = Geometry.Parse("M 0,0 A 32,32 0 0 1 0,64 Z");
                RootGrid.RenderTransformOrigin = new Point(0, 0.5);
                break;
            case DockEdge.Right:
                Width = 32; Height = 64;
                SemiPath.Data = Geometry.Parse("M 32,0 A 32,32 0 0 0 32,64 Z");
                RootGrid.RenderTransformOrigin = new Point(1, 0.5);
                break;
            case DockEdge.Top:
                Width = 64; Height = 32;
                SemiPath.Data = Geometry.Parse("M 0,0 A 32,32 0 0 1 64,0 Z");
                RootGrid.RenderTransformOrigin = new Point(0.5, 0);
                break;
        }
    }

    // ─── Hover scale ──────────────────────────────────────

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        AnimateScale(1.0, 200, new QuadraticEase { EasingMode = EasingMode.EaseOut });
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
            AnimateScale(0.8, 200, new QuadraticEase { EasingMode = EasingMode.EaseIn });
    }

    void AnimateScale(double to, int ms, IEasingFunction ease)
    {
        ContentScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease });
        ContentScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease });
    }

    // ─── Drag / Click ─────────────────────────────────────

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _dragStartScreen = PointToScreen(e.GetPosition(this));
        _dragStartWindow = new Point(Left, Top);
        RootGrid.CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var screenPt = PointToScreen(e.GetPosition(this));
        var dx = screenPt.X - _dragStartScreen.X;
        var dy = screenPt.Y - _dragStartScreen.Y;

        switch (_dockEdge)
        {
            case DockEdge.Left:
            case DockEdge.Right:
                Top = Clamp(_dragStartWindow.Y + dy, 0, SystemParameters.PrimaryScreenHeight - Height);
                break;
            case DockEdge.Top:
                Left = Clamp(_dragStartWindow.X + dx, 0, SystemParameters.PrimaryScreenWidth - Width);
                break;
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        RootGrid.ReleaseMouseCapture();
        _isDragging = false;

        var screenPt = PointToScreen(e.GetPosition(this));
        var totalDx = screenPt.X - _dragStartScreen.X;
        var totalDy = screenPt.Y - _dragStartScreen.Y;
        var totalDelta = System.Math.Sqrt(totalDx * totalDx + totalDy * totalDy);

        double perpDist;
        switch (_dockEdge)
        {
            case DockEdge.Left:   perpDist = screenPt.X; break;
            case DockEdge.Right:  perpDist = SystemParameters.PrimaryScreenWidth - screenPt.X; break;
            case DockEdge.Top:    perpDist = screenPt.Y; break;
            default:              perpDist = 0; break;
        }

        if (totalDelta < DragThreshold)
        {
            // Pure click → show panel
            ShowRequested?.Invoke(double.NaN, double.NaN);
        }
        else if (perpDist > DetachThreshold)
        {
            // Dragged away from edge → show panel at cursor position
            ShowRequested?.Invoke(screenPt.X, screenPt.Y);
        }
        else
        {
            // Dragged along edge → just repositioned
            Repositioned?.Invoke();
        }
    }

    static double Clamp(double val, double min, double max) =>
        val < min ? min : val > max ? max : val;
}
