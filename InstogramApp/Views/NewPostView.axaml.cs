using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class NewPostView : UserControl
{
    public NewPostView() => InitializeComponent();
    NewPostViewModel VM => (NewPostViewModel)DataContext!;
    void OnSubmit(object? s, RoutedEventArgs e)      => VM.SubmitCommand.Execute(null);
    void OnCancel(object? s, RoutedEventArgs e)      => VM.CancelCommand.Execute(null);
    void OnPickImage(object? s, RoutedEventArgs e)   => VM.PickImageCommand.Execute(null);
    void OnRemoveImage(object? s, RoutedEventArgs e) => VM.RemoveImageCommand.Execute(null);
    void OnPickVideo(object? s, RoutedEventArgs e)   => VM.PickVideoCommand.Execute(null);
    void OnRemoveVideo(object? s, RoutedEventArgs e) => VM.RemoveVideoCommand.Execute(null);

    void OnTagChipClick(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is TagChipViewModel chip)
            chip.ToggleCommand.Execute(null);
    }
}
