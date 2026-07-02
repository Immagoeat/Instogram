using System;
using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InstogramApp.ViewModels;

public partial class CropViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;
    private readonly string              _sourcePath;
    private readonly Action<string>      _onConfirm;  // called with path; must also navigate away
    private readonly Action              _onCancel;   // navigate back without changes

    // The loaded source bitmap — read by the View
    public Bitmap? SourceBitmap { get; }

    // Crop rect in image pixel coordinates — set by the View's drag logic
    public double CropX { get; set; }
    public double CropY { get; set; }
    public double CropSize { get; set; }

    [ObservableProperty] private string _statusText = "Drag to reposition · handles to resize";

    public CropViewModel(MainWindowViewModel main, string sourcePath,
        Action<string> onConfirm, Action onCancel)
    {
        _main      = main;
        _sourcePath = sourcePath;
        _onConfirm  = onConfirm;
        _onCancel   = onCancel;

        try { SourceBitmap = new Bitmap(sourcePath); }
        catch { }

        if (SourceBitmap != null)
        {
            // Default crop: largest centred square
            int w = SourceBitmap.PixelSize.Width;
            int h = SourceBitmap.PixelSize.Height;
            CropSize = Math.Min(w, h);
            CropX    = (w - CropSize) / 2.0;
            CropY    = (h - CropSize) / 2.0;
        }
    }

    [RelayCommand]
    void Confirm()
    {
        if (SourceBitmap == null) { _onCancel(); return; }

        try
        {
            int px = (int)Math.Round(CropX);
            int py = (int)Math.Round(CropY);
            int ps = (int)Math.Round(CropSize);

            int srcW = SourceBitmap.PixelSize.Width;
            int srcH = SourceBitmap.PixelSize.Height;

            px = Math.Clamp(px, 0, srcW - 1);
            py = Math.Clamp(py, 0, srcH - 1);
            ps = Math.Clamp(ps, 1, Math.Min(srcW - px, srcH - py));

            const int outSize = 400;
            var renderBitmap = new RenderTargetBitmap(
                new Avalonia.PixelSize(outSize, outSize), new Avalonia.Vector(96, 96));

            using var ctx = renderBitmap.CreateDrawingContext();
            ctx.DrawImage(SourceBitmap,
                new Avalonia.Rect(px, py, ps, ps),
                new Avalonia.Rect(0, 0, outSize, outSize));

            var dir = Path.Combine(AppContext.BaseDirectory, "avatars");
            Directory.CreateDirectory(dir);
            var outPath = Path.Combine(dir, $"avatar_{Guid.NewGuid():N}.png");
            renderBitmap.Save(outPath);

            // Callback handles navigation
            _onConfirm(outPath);
        }
        catch (Exception ex)
        {
            StatusText = $"Crop failed: {ex.Message}";
        }
    }

    [RelayCommand]
    void Cancel() => _onCancel();
}
