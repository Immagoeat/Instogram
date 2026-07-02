using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class StoryViewerView : UserControl
{
    private DispatcherTimer? _timer;
    private double _timerProgress;   // 0.0 → 1.0 over 5 seconds
    private const double TickMs  = 50;
    private const double TotalMs = 5000;

    // per-segment track + fill borders (index = story index)
    private Border[] _trackBorders = [];
    private Border[] _fillBorders  = [];

    public StoryViewerView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => { SubscribeToVm(); BuildProgressBars(); RestartTimer(); };
    }

    // ── Command dispatch (supports both local and server VM) ──────────────────

    IRelayCommand PrevCmd  => DataContext is ServerStoryViewerViewModel sv
        ? sv.PrevCommand  : ((StoryViewerViewModel)DataContext!).PrevCommand;
    IRelayCommand NextCmd  => DataContext is ServerStoryViewerViewModel sv2
        ? sv2.NextCommand : ((StoryViewerViewModel)DataContext!).NextCommand;
    IRelayCommand CloseCmd => DataContext is ServerStoryViewerViewModel sv3
        ? sv3.CloseCommand : ((StoryViewerViewModel)DataContext!).CloseCommand;

    void OnPrev(object? s, RoutedEventArgs e)  { PrevCmd.Execute(null);  _timerProgress = 0; UpdateProgressBars(); }
    void OnNext(object? s, RoutedEventArgs e)  { NextCmd.Execute(null);  _timerProgress = 0; UpdateProgressBars(); }
    void OnClose(object? s, RoutedEventArgs e) { StopTimer(); CloseCmd.Execute(null); }

    // ── Auto-scroll timer ─────────────────────────────────────────────────────

    private void RestartTimer()
    {
        StopTimer();
        _timerProgress = 0;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickMs) };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _timerProgress += TickMs / TotalMs;
        if (_timerProgress >= 1.0)
        {
            _timerProgress = 0;
            var hasNext = DataContext is ServerStoryViewerViewModel sv2
                ? sv2.HasNext : DataContext is StoryViewerViewModel lv ? lv.HasNext : false;
            if (hasNext)
                NextCmd.Execute(null);
            else
            {
                StopTimer();
                _timerProgress = 1.0;
            }
        }
        UpdateProgressBars();
    }

    // ── Progress bar segments ─────────────────────────────────────────────────

    private void BuildProgressBars()
    {
        if (ProgressBars == null) return;
        ProgressBars.Children.Clear();

        int total = DataContext is ServerStoryViewerViewModel sv
            ? sv.TotalCount : DataContext is StoryViewerViewModel lv ? lv.TotalCount : 1;

        _trackBorders = new Border[total];
        _fillBorders  = new Border[total];

        for (int i = 0; i < total; i++)
        {
            var fill = new Border
            {
                Height           = 3,
                CornerRadius     = new CornerRadius(2),
                Background       = Brushes.White,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Width            = 0
            };
            var track = new Border
            {
                Height           = 3,
                CornerRadius     = new CornerRadius(2),
                Background       = new SolidColorBrush(Color.Parse("#44ffffff")),
                ClipToBounds     = true,
                Child            = fill
            };
            // tracks grow to fill available horizontal space equally
            ProgressBars.Children.Add(track);
            _trackBorders[i] = track;
            _fillBorders[i]  = fill;
        }
        UpdateProgressBars();
    }

    private void UpdateProgressBars()
    {
        if (_trackBorders.Length == 0) return;
        int total   = _trackBorders.Length;
        int current = (DataContext is ServerStoryViewerViewModel sv
            ? sv.CurrentIndex : DataContext is StoryViewerViewModel lv ? lv.CurrentIndex : 1) - 1;

        // Calculate segment width: parent width minus gaps, divided evenly
        double parentW = ProgressBars?.Bounds.Width > 0 ? ProgressBars.Bounds.Width : 440;
        double gap     = 4.0 * (total - 1);
        double segW    = System.Math.Max(4, (parentW - gap) / total);

        for (int i = 0; i < total; i++)
        {
            var track = _trackBorders[i];
            var fill  = _fillBorders[i];
            track.Width = segW;
            if (i < current)
                fill.Width = segW;
            else if (i == current)
                fill.Width = segW * _timerProgress;
            else
                fill.Width = 0;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateProgressBars();
        ApplyTextOverlay();
    }

    // ── Text overlay positioning ──────────────────────────────────────────────

    private INotifyPropertyChanged? _subscribedVm;

    private void SubscribeToVm()
    {
        if (_subscribedVm != null)
            _subscribedVm.PropertyChanged -= OnVmPropertyChanged;

        _subscribedVm = DataContext as INotifyPropertyChanged;
        if (_subscribedVm != null)
            _subscribedVm.PropertyChanged += OnVmPropertyChanged;

        ApplyTextOverlay();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ServerStoryViewerViewModel.TextOffsetX)
                           or nameof(ServerStoryViewerViewModel.TextOffsetY)
                           or nameof(ServerStoryViewerViewModel.TextScale)
                           or nameof(ServerStoryViewerViewModel.TextRotation)
                           or nameof(ServerStoryViewerViewModel.StoryText)
                           or nameof(StoryViewerViewModel.TextOffsetX)
                           or nameof(StoryViewerViewModel.TextOffsetY)
                           or nameof(StoryViewerViewModel.TextScale)
                           or nameof(StoryViewerViewModel.StoryText))
            ApplyTextOverlay();

        // reset timer when story changes
        if (e.PropertyName is nameof(ServerStoryViewerViewModel.CurrentIndex)
                           or nameof(StoryViewerViewModel.CurrentIndex))
        {
            _timerProgress = 0;
            UpdateProgressBars();
            if (_timer == null) RestartTimer();
        }
    }

    private void ApplyTextOverlay()
    {
        if (TextOverlay == null || StoryGrid == null) return;

        var w = StoryGrid.Bounds.Width;
        var h = StoryGrid.Bounds.Height;
        if (w <= 0 || h <= 0) { w = 440; h = 500; }

        double fx, fy, scale, rotation;
        if (DataContext is ServerStoryViewerViewModel sv)
        {
            fx = sv.TextOffsetX; fy = sv.TextOffsetY;
            scale = sv.TextScale; rotation = sv.TextRotation;
        }
        else if (DataContext is StoryViewerViewModel lv)
        {
            fx = lv.TextOffsetX; fy = lv.TextOffsetY;
            scale = lv.TextScale; rotation = 0;
        }
        else return;

        OverlayText.FontSize = 22 * scale;

        var ow = TextOverlay.Bounds.Width  > 0 ? TextOverlay.Bounds.Width  : 160;
        var oh = TextOverlay.Bounds.Height > 0 ? TextOverlay.Bounds.Height : 48;

        var x = fx * w - ow / 2;
        var y = fy * h - oh / 2;
        x = System.Math.Clamp(x, 0, System.Math.Max(0, w - ow));
        y = System.Math.Clamp(y, 0, System.Math.Max(0, h - oh));

        TextOverlay.Margin = new Thickness(x, y, 0, 0);
        ((RotateTransform)TextOverlay.RenderTransform!).Angle = rotation;
    }
}
