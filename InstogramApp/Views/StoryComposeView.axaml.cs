using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class StoryComposeView : UserControl
{
    public StoryComposeView() => InitializeComponent();

    StoryComposeViewModel VM => (StoryComposeViewModel)DataContext!;

    void OnPost(object? s, RoutedEventArgs e)   => VM.PostCommand.Execute(null);
    void OnCancel(object? s, RoutedEventArgs e) => VM.CancelCommand.Execute(null);
    void OnSelectColor(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is string color)
            VM.SelectColorCommand.Execute(color);
    }
}
