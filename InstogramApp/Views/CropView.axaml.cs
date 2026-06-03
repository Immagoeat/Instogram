using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class CropView : UserControl
{
    // ── drag state ────────────────────────────────────────────────────────────
    private enum DragMode { None, Move, TopLeft, TopRight, BottomLeft, BottomRight }

    private DragMode _drag      = DragMode.None;
    private Point    _dragStart;
    private double   _dragCropX0, _dragCropY0, _dragCropSize0;

    // ── layout: image rect inside the canvas ─────────────────────────────────
    private Rect   _imageRect;          // display rect of image inside OverlayCanvas
    private double _scale;              // image pixels → canvas pixels

    // ── crop rect in canvas pixels ───────────────────────────────────────────
    private Rect _cropCanvas;          // current crop square in canvas coords

    // ── handle hit radius ─────────────────────────────────────────────────────
    private const double HandleR  = 14;
    private const double HandleHit = 22;

    // ── overlay shapes ───────────────────────────────────────────────────────
    // dark dimmer rects (top/bottom/left/right around the crop circle)
    private readonly Rectangle _dimTop    = MakeDim();
    private readonly Rectangle _dimBottom = MakeDim();
    private readonly Rectangle _dimLeft   = MakeDim();
    private readonly Rectangle _dimRight  = MakeDim();
    // circle outline
    private readonly Ellipse   _cropRing  = new() { Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false };
    // corner handles
    private readonly Ellipse[] _handles   = new Ellipse[4]; // TL TR BL BR

    public CropView()
    {
        InitializeComponent();

        for (int i = 0; i < 4; i++)
            _handles[i] = new Ellipse
            {
                Width  = HandleR * 2, Height = HandleR * 2,
                Fill   = Brushes.White,
                Stroke = new SolidColorBrush(Color.Parse("#8b5cf6")),
                StrokeThickness = 2,
                Cursor = Cursor.Parse("SizeAll"),
                IsHitTestVisible = false   // we handle hits manually
            };
    }

    // called once after DataContext is set and layout has happened
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        var vm = VM;
        if (vm.SourceBitmap == null) return;

        // Put the bitmap into the Image control
        SourceImage.Source = vm.SourceBitmap;

        // Populate overlay canvas with shapes
        var c = OverlayCanvas;
        c.Children.Add(_dimTop);
        c.Children.Add(_dimBottom);
        c.Children.Add(_dimLeft);
        c.Children.Add(_dimRight);
        c.Children.Add(_cropRing);
        foreach (var h in _handles) c.Children.Add(h);

        // Wire pointer events on the canvas
        c.PointerPressed  += OnPointerPressed;
        c.PointerMoved    += OnPointerMoved;
        c.PointerReleased += OnPointerReleased;

        // SizeChanged fires once real bounds are known (reliable vs LayoutUpdated)
        c.SizeChanged += OnCanvasSizeChanged;
    }

    private void OnCanvasSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0) return;
        OverlayCanvas.SizeChanged -= OnCanvasSizeChanged;
        RecalcLayout();
        Redraw();
    }

    // ── layout ────────────────────────────────────────────────────────────────

    private void RecalcLayout()
    {
        var vm  = VM;
        var bmp = vm.SourceBitmap;
        if (bmp == null) return;

        double cw = OverlayCanvas.Bounds.Width;
        double ch = OverlayCanvas.Bounds.Height;
        if (cw <= 0 || ch <= 0) return;

        double iw = bmp.PixelSize.Width;
        double ih = bmp.PixelSize.Height;

        _scale    = Math.Min(cw / iw, ch / ih);
        double dw = iw * _scale;
        double dh = ih * _scale;
        _imageRect = new Rect((cw - dw) / 2, (ch - dh) / 2, dw, dh);

        // Convert stored image-pixel crop rect → canvas pixels
        _cropCanvas = new Rect(
            _imageRect.X + vm.CropX * _scale,
            _imageRect.Y + vm.CropY * _scale,
            vm.CropSize   * _scale,
            vm.CropSize   * _scale);
    }

    // ── drawing ───────────────────────────────────────────────────────────────

    private void Redraw()
    {
        var cr = _cropCanvas;
        double cw = OverlayCanvas.Bounds.Width;
        double ch = OverlayCanvas.Bounds.Height;

        var dim = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0));
        SetRect(_dimTop,    0,         0,         cw,                 cr.Top,    dim);
        SetRect(_dimBottom, 0,         cr.Bottom, cw,                 ch - cr.Bottom, dim);
        SetRect(_dimLeft,   0,         cr.Top,    cr.Left,            cr.Height, dim);
        SetRect(_dimRight,  cr.Right,  cr.Top,    cw - cr.Right,      cr.Height, dim);

        // Crop ring (circle inside the square)
        Canvas.SetLeft(_cropRing,  cr.X);
        Canvas.SetTop(_cropRing,   cr.Y);
        _cropRing.Width  = cr.Width;
        _cropRing.Height = cr.Height;

        // Corner handles: TL TR BL BR
        double[] hx = [cr.Left, cr.Right, cr.Left,  cr.Right];
        double[] hy = [cr.Top,  cr.Top,   cr.Bottom, cr.Bottom];
        for (int i = 0; i < 4; i++)
        {
            Canvas.SetLeft(_handles[i], hx[i] - HandleR);
            Canvas.SetTop(_handles[i],  hy[i] - HandleR);
        }
    }

    private static void SetRect(Rectangle r, double x, double y, double w, double h, IBrush b)
    {
        Canvas.SetLeft(r, x); Canvas.SetTop(r, y);
        r.Width  = Math.Max(0, w);
        r.Height = Math.Max(0, h);
        r.Fill   = b;
    }

    // ── pointer events ────────────────────────────────────────────────────────

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(OverlayCanvas).Properties.IsLeftButtonPressed) return;
        var pt = e.GetPosition(OverlayCanvas);
        _drag     = HitTest(pt);
        _dragStart     = pt;
        _dragCropX0    = VM.CropX;
        _dragCropY0    = VM.CropY;
        _dragCropSize0 = VM.CropSize;
        if (_drag != DragMode.None)
            e.Pointer.Capture(OverlayCanvas);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_drag == DragMode.None) return;
        var pt = e.GetPosition(OverlayCanvas);
        double dx = (pt.X - _dragStart.X) / _scale;
        double dy = (pt.Y - _dragStart.Y) / _scale;

        var vm  = VM;
        var bmp = vm.SourceBitmap!;
        double iw = bmp.PixelSize.Width;
        double ih = bmp.PixelSize.Height;

        if (_drag == DragMode.Move)
        {
            vm.CropX = Math.Clamp(_dragCropX0 + dx, 0, iw - vm.CropSize);
            vm.CropY = Math.Clamp(_dragCropY0 + dy, 0, ih - vm.CropSize);
        }
        else
        {
            // For resize: delta is the change in the dragged corner.
            // We keep the opposite corner fixed.
            double minSize = 40;

            switch (_drag)
            {
                case DragMode.BottomRight:
                {
                    double ns = Math.Clamp(_dragCropSize0 + Math.Min(dx, dy),
                                           minSize, Math.Min(iw - _dragCropX0, ih - _dragCropY0));
                    vm.CropX    = _dragCropX0;
                    vm.CropY    = _dragCropY0;
                    vm.CropSize = ns;
                    break;
                }
                case DragMode.BottomLeft:
                {
                    double delta = Math.Min(-dx, dy);
                    double ns    = Math.Clamp(_dragCropSize0 + delta, minSize,
                                              Math.Min(_dragCropX0 + _dragCropSize0, ih - _dragCropY0));
                    vm.CropX    = _dragCropX0 + _dragCropSize0 - ns;
                    vm.CropY    = _dragCropY0;
                    vm.CropSize = ns;
                    break;
                }
                case DragMode.TopRight:
                {
                    double delta = Math.Min(dx, -dy);
                    double ns    = Math.Clamp(_dragCropSize0 + delta, minSize,
                                              Math.Min(iw - _dragCropX0, _dragCropY0 + _dragCropSize0));
                    vm.CropX    = _dragCropX0;
                    vm.CropY    = _dragCropY0 + _dragCropSize0 - ns;
                    vm.CropSize = ns;
                    break;
                }
                case DragMode.TopLeft:
                {
                    double delta = Math.Min(-dx, -dy);
                    double ns    = Math.Clamp(_dragCropSize0 + delta, minSize,
                                              Math.Min(_dragCropX0 + _dragCropSize0,
                                                       _dragCropY0 + _dragCropSize0));
                    vm.CropX    = _dragCropX0 + _dragCropSize0 - ns;
                    vm.CropY    = _dragCropY0 + _dragCropSize0 - ns;
                    vm.CropSize = ns;
                    break;
                }
            }
        }

        // Sync canvas rect and redraw
        _cropCanvas = new Rect(
            _imageRect.X + vm.CropX * _scale,
            _imageRect.Y + vm.CropY * _scale,
            vm.CropSize  * _scale,
            vm.CropSize  * _scale);
        Redraw();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _drag = DragMode.None;
        e.Pointer.Capture(null);
    }

    // ── hit testing ──────────────────────────────────────────────────────────

    private DragMode HitTest(Point pt)
    {
        var cr = _cropCanvas;
        // Corner handles
        if (Dist(pt, cr.TopLeft)     < HandleHit) return DragMode.TopLeft;
        if (Dist(pt, cr.TopRight)    < HandleHit) return DragMode.TopRight;
        if (Dist(pt, cr.BottomLeft)  < HandleHit) return DragMode.BottomLeft;
        if (Dist(pt, cr.BottomRight) < HandleHit) return DragMode.BottomRight;
        // Inside crop circle → move
        double cx = cr.X + cr.Width  / 2;
        double cy = cr.Y + cr.Height / 2;
        double r  = cr.Width / 2;
        if (Math.Sqrt((pt.X - cx) * (pt.X - cx) + (pt.Y - cy) * (pt.Y - cy)) < r)
            return DragMode.Move;
        return DragMode.None;
    }

    private static double Dist(Point a, Point b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    // ── helpers ───────────────────────────────────────────────────────────────

    private static Rectangle MakeDim() => new() { IsHitTestVisible = false };

    CropViewModel VM => (CropViewModel)DataContext!;

    void OnConfirm(object? s, RoutedEventArgs e) => VM.ConfirmCommand.Execute(null);
    void OnCancel(object? s, RoutedEventArgs e)  => VM.CancelCommand.Execute(null);
}
