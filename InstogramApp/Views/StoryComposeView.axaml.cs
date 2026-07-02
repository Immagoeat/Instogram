using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class StoryComposeView : UserControl
{
    public StoryComposeView() => InitializeComponent();

    StoryComposeViewModel VM => (StoryComposeViewModel)DataContext!;

    // ── Button handlers ───────────────────────────────────────────────────────

    void OnPost(object? s, RoutedEventArgs e)   => VM.PostCommand.Execute(null);
    void OnCancel(object? s, RoutedEventArgs e) => VM.CancelCommand.Execute(null);

    void OnSelectColor(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is string color)
            VM.SelectColorCommand.Execute(color);
    }

    void OnAddTag(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is TagSuggestionViewModel suggestion)
            VM.AddTagCommand.Execute(suggestion);
    }

    void OnRemoveTag(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is TaggedUserChipViewModel chip)
            chip.RemoveCommand.Execute(null);
    }

    void OnScaleChanged(object? s, RangeBaseValueChangedEventArgs e)   => UpdateTextOverlayTransform();
    void OnRotationChanged(object? s, RangeBaseValueChangedEventArgs e) => UpdateTextOverlayTransform();

    // ── Background image picker ───────────────────────────────────────────────

    async void OnPickBgImage(object? s, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Choose background image",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("Images")
                    {
                        Patterns  = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp"],
                        MimeTypes = ["image/*"]
                    }
                ]
            });

        if (files.Count > 0)
            VM.BgImagePath = files[0].Path.LocalPath;
    }

    void OnClearBgImage(object? s, RoutedEventArgs e) => VM.BgImagePath = "";

    async void OnPickVideo(object? s, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Choose background video",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("Videos")
                    {
                        Patterns  = ["*.mp4", "*.webm", "*.mov", "*.avi", "*.mkv"],
                        MimeTypes = ["video/*"]
                    }
                ]
            });

        if (files.Count > 0)
        {
            VM.VideoPath   = files[0].Path.LocalPath;
            VM.BgImagePath = ""; // clear image if video chosen
        }
    }

    void OnClearVideo(object? s, RoutedEventArgs e) => VM.VideoPath = "";

    // ── Draggable text overlay ────────────────────────────────────────────────

    private bool   _dragging;
    private Point  _dragStart;       // pointer position when drag started
    private Point  _overlayStart;    // overlay Margin.Left/Top when drag started

    void OnTextOverlayPressed(object? s, PointerPressedEventArgs e)
    {
        if (s is not Border overlay) return;
        _dragging     = true;
        _dragStart    = e.GetPosition(PreviewCanvas);
        _overlayStart = new Point(overlay.Margin.Left, overlay.Margin.Top);
        e.Pointer.Capture(overlay);
        e.Handled = true;
    }

    void OnTextOverlayMoved(object? s, PointerEventArgs e)
    {
        if (!_dragging || s is not Border overlay) return;

        var current = e.GetPosition(PreviewCanvas);
        var dx = current.X - _dragStart.X;
        var dy = current.Y - _dragStart.Y;

        var newX = _overlayStart.X + dx;
        var newY = _overlayStart.Y + dy;

        // Clamp so the overlay stays inside the canvas
        var canvasW = PreviewCanvas.Bounds.Width;
        var canvasH = PreviewCanvas.Bounds.Height;
        var ow = overlay.Bounds.Width;
        var oh = overlay.Bounds.Height;
        newX = System.Math.Clamp(newX, 0, System.Math.Max(0, canvasW - ow));
        newY = System.Math.Clamp(newY, 0, System.Math.Max(0, canvasH - oh));

        overlay.Margin = new Thickness(newX, newY, 0, 0);

        // Persist fractional position to VM
        if (canvasW > 0 && canvasH > 0)
        {
            VM.TextOffsetX = (newX + ow / 2) / canvasW;
            VM.TextOffsetY = (newY + oh / 2) / canvasH;
        }
        e.Handled = true;
    }

    void OnTextOverlayReleased(object? s, PointerReleasedEventArgs e)
    {
        _dragging = false;
        if (s is Border overlay)
            e.Pointer.Capture(null);
        e.Handled = true;
    }

    // Apply scale + rotation to the text overlay
    private void UpdateTextOverlayTransform()
    {
        if (PreviewText == null || TextOverlay == null) return;
        PreviewText.FontSize = 20 * VM.TextScale;
        ((RotateTransform)TextOverlay.RenderTransform!).Angle = VM.TextRotation;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        PositionOverlayFromVm();
        UpdateTextOverlayTransform();
    }

    private void PositionOverlayFromVm()
    {
        if (TextOverlay == null || PreviewCanvas == null) return;
        var w = PreviewCanvas.Bounds.Width;
        var h = PreviewCanvas.Bounds.Height;
        if (w <= 0 || h <= 0) return;

        var ow = TextOverlay.Bounds.Width  > 0 ? TextOverlay.Bounds.Width  : 120;
        var oh = TextOverlay.Bounds.Height > 0 ? TextOverlay.Bounds.Height : 50;

        var x = VM.TextOffsetX * w - ow / 2;
        var y = VM.TextOffsetY * h - oh / 2;
        x = System.Math.Clamp(x, 0, System.Math.Max(0, w - ow));
        y = System.Math.Clamp(y, 0, System.Math.Max(0, h - oh));
        TextOverlay.Margin = new Thickness(x, y, 0, 0);
    }
}
