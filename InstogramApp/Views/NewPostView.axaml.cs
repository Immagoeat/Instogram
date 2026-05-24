using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class NewPostView : UserControl
{
    public NewPostView() => InitializeComponent();
    NewPostViewModel VM => (NewPostViewModel)DataContext!;
    void OnSubmit(object? s, RoutedEventArgs e) => VM.SubmitCommand.Execute(null);
    void OnCancel(object? s, RoutedEventArgs e) => VM.CancelCommand.Execute(null);
}
