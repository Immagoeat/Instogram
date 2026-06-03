using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class StoryViewerView : UserControl
{
    public StoryViewerView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SubscribeToVm();
    }

    // ── Command dispatch (supports both local and server VM) ──────────────────

    IRelayCommand PrevCmd  => DataContext is ServerStoryViewerViewModel sv
        ? sv.PrevCommand  : ((StoryViewerViewModel)DataContext!).PrevCommand;
    IRelayCommand NextCmd  => DataContext is ServerStoryViewerViewModel sv2
        ? sv2.NextCommand : ((StoryViewerViewModel)DataContext!).NextCommand;
    IRelayCommand CloseCmd => DataContext is ServerStoryViewerViewModel sv3
        ? sv3.CloseCommand : ((StoryViewerViewModel)DataContext!).CloseCommand;

    void OnPrev(object? s, RoutedEventArgs e)  => PrevCmd.Execute(null);
    void OnNext(object? s, RoutedEventArgs e)  => NextCmd.Execute(null);
    void OnClose(object? s, RoutedEventArgs e) => CloseCmd.Execute(null);

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
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyTextOverlay();
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

        // Apply font size scale
        OverlayText.FontSize = 22 * scale;

        // Layout pass needed to get the overlay's rendered size; use last known or estimate
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
