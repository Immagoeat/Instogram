using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace InstogramApp.Controls;

public partial class AsyncImage : UserControl
{
    // ── Source (input) ────────────────────────────────────────────────────────

    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<AsyncImage, string?>(nameof(Source));

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    // ── BitmapSource (output — bound by AXAML Image) ──────────────────────────

    public static readonly DirectProperty<AsyncImage, Bitmap?> BitmapSourceProperty =
        AvaloniaProperty.RegisterDirect<AsyncImage, Bitmap?>(nameof(BitmapSource),
            o => o.BitmapSource);

    private Bitmap? _bitmapSource;
    public Bitmap? BitmapSource
    {
        get => _bitmapSource;
        private set => SetAndRaise(BitmapSourceProperty, ref _bitmapSource, value);
    }

    // ── IsLoading ─────────────────────────────────────────────────────────────

    public static readonly DirectProperty<AsyncImage, bool> IsLoadingProperty =
        AvaloniaProperty.RegisterDirect<AsyncImage, bool>(nameof(IsLoading),
            o => o.IsLoading);

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
    }

    // ── Load logic ────────────────────────────────────────────────────────────

    private static readonly HttpClient _http = new();
    private CancellationTokenSource? _cts;

    public AsyncImage() => InitializeComponent();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
            _ = LoadAsync(change.NewValue as string);
    }

    private async Task LoadAsync(string? url)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        BitmapSource = null;

        if (string.IsNullOrEmpty(url)) return;

        IsLoading = true;
        try
        {
            Bitmap? bmp;
            if (url.StartsWith("http://") || url.StartsWith("https://"))
            {
                var bytes = await _http.GetByteArrayAsync(url, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                bmp = new Bitmap(new MemoryStream(bytes));
            }
            else
            {
                bmp = File.Exists(url) ? new Bitmap(url) : null;
            }

            if (token.IsCancellationRequested) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                BitmapSource = bmp;
                IsLoading    = false;
            });
        }
        catch (TaskCanceledException) { }
        catch
        {
            if (!token.IsCancellationRequested)
                await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
        }
    }
}
