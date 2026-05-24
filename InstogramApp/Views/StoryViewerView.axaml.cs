using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class StoryViewerView : UserControl
{
    public StoryViewerView() => InitializeComponent();

    StoryViewerViewModel VM => (StoryViewerViewModel)DataContext!;

    void OnPrev(object? s, RoutedEventArgs e)  => VM.PrevCommand.Execute(null);
    void OnNext(object? s, RoutedEventArgs e)  => VM.NextCommand.Execute(null);
    void OnClose(object? s, RoutedEventArgs e) => VM.CloseCommand.Execute(null);
}
